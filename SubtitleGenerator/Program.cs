using System.Diagnostics;
using System.Text;
using Spectre.Console;
using Whisper.net;
using Whisper.net.Ggml;

// ============================================================================
//  Configuration defaults (overridden by command-line arguments)
// ============================================================================
const string DefaultModel = "base";
const string DefaultLanguage = "en";
const int DefaultThreads = 0; // 0 = all cores

string[] videoExtensions = [".mp4", ".webm", ".mkv", ".avi", ".mov"];

// ============================================================================
//  Parse command-line arguments
// ============================================================================
var (targetFolder, modelName, language, threads) = ParseArguments(args);
threads = threads <= 0 ? Environment.ProcessorCount : threads;

// ============================================================================
//  Display banner
// ============================================================================
var headerTable = new Table()
    .Border(TableBorder.Rounded)
    .BorderColor(Color.Cyan1)
    .AddColumn(new TableColumn("[bold cyan]Subtitle Generator[/]").Centered())
    .HideHeaders();

headerTable.AddRow($"[bold]Model[/]    : [green]{modelName}[/]");
headerTable.AddRow($"[bold]Threads[/]  : [green]{threads}[/] / {Environment.ProcessorCount} available");
headerTable.AddRow($"[bold]Language[/] : [green]{(string.IsNullOrEmpty(language) ? "auto-detect" : language)}[/]");
headerTable.AddRow($"[bold]Folder[/]   : [green]{Markup.Escape(targetFolder)}[/]");

AnsiConsole.WriteLine();
AnsiConsole.Write(headerTable);
AnsiConsole.WriteLine();

// ============================================================================
//  Find FFmpeg
// ============================================================================
var ffmpegPath = FindFfmpeg();
if (ffmpegPath is null)
{
    AnsiConsole.MarkupLine("[red bold]ERROR:[/] FFmpeg not found.");
    AnsiConsole.MarkupLine("Install it with: [cyan]winget install Gyan.FFmpeg[/]");
    return 1;
}

// ============================================================================
//  Scan for videos
// ============================================================================
var (videosToProcess, alreadyHaveSrt) = ScanForVideos(targetFolder, videoExtensions);

var summaryTable = new Table()
    .Border(TableBorder.Simple)
    .AddColumn("Status")
    .AddColumn("Count")
    .HideHeaders();

summaryTable.AddRow("[yellow]Videos needing subtitles[/]", $"[bold yellow]{videosToProcess.Count}[/]");
summaryTable.AddRow("[green]Already have subtitles[/]", $"[bold green]{alreadyHaveSrt}[/]");

AnsiConsole.Write(summaryTable);
AnsiConsole.WriteLine();

if (videosToProcess.Count == 0)
{
    AnsiConsole.MarkupLine("[green]Nothing to do — all videos already have subtitles![/]");
    return 0;
}

// ============================================================================
//  Download / load Whisper model
// ============================================================================
var ggmlType = ParseGgmlType(modelName);
var modelDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".cache", "whisper-net");
Directory.CreateDirectory(modelDir);
var modelPath = Path.Combine(modelDir, $"ggml-{modelName}.bin");

if (!File.Exists(modelPath))
{
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("cyan"))
        .StartAsync($"Downloading Whisper '{modelName}' model (first time only)...", async ctx =>
        {
            using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
            using var fileWriter = File.OpenWrite(modelPath);
            await modelStream.CopyToAsync(fileWriter);
        });
    AnsiConsole.MarkupLine($"[green]Model downloaded to:[/] {Markup.Escape(modelPath)}");
}
else
{
    AnsiConsole.MarkupLine($"[dim]Using cached model:[/] {Markup.Escape(modelPath)}");
}

AnsiConsole.WriteLine();

// ============================================================================
//  Load model and process videos
// ============================================================================
using var whisperFactory = WhisperFactory.FromPath(modelPath);

var totalSuccess = 0;
var totalFailed = 0;
var stopwatch = Stopwatch.StartNew();

await AnsiConsole.Progress()
    .AutoClear(false)
    .HideCompleted(false)
    .Columns(
        new TaskDescriptionColumn(),
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn(),
        new SpinnerColumn())
    .StartAsync(async ctx =>
    {
        var overallTask = ctx.AddTask("[bold]Overall progress[/]", maxValue: videosToProcess.Count);

        for (var i = 0; i < videosToProcess.Count; i++)
        {
            var videoPath = videosToProcess[i];
            var relativePath = Path.GetRelativePath(targetFolder, videoPath);
            var srtPath = Path.ChangeExtension(videoPath, ".srt");

            overallTask.Description = $"[bold][[{i + 1}/{videosToProcess.Count}]][/] {Markup.Escape(relativePath)}";

            try
            {
                // Extract audio to WAV using FFmpeg
                var wavPath = Path.Combine(Path.GetTempPath(), $"whisper_{Guid.NewGuid():N}.wav");
                try
                {
                    await ExtractAudio(ffmpegPath, videoPath, wavPath);

                    // Transcribe
                    using var processor = whisperFactory.CreateBuilder()
                        .WithLanguage(string.IsNullOrEmpty(language) ? "auto" : language)
                        .WithThreads(threads)
                        .Build();

                    var segments = new List<SubtitleSegment>();
                    await using var audioStream = File.OpenRead(wavPath);
                    await foreach (var result in processor.ProcessAsync(audioStream))
                    {
                        segments.Add(new SubtitleSegment(result.Start, result.End, result.Text));
                    }

                    WriteSrt(segments, srtPath);
                    totalSuccess++;
                    AnsiConsole.MarkupLine($"  [green]OK[/] ({segments.Count} segments) — {Markup.Escape(relativePath)}");
                }
                finally
                {
                    if (File.Exists(wavPath)) File.Delete(wavPath);
                }
            }
            catch (Exception ex)
            {
                totalFailed++;
                AnsiConsole.MarkupLine($"  [red]FAILED[/] — {Markup.Escape(relativePath)}: {Markup.Escape(ex.Message)}");
            }

            overallTask.Increment(1);
        }

        overallTask.Description = "[bold green]Complete[/]";
    });

stopwatch.Stop();

// ============================================================================
//  Summary
// ============================================================================
AnsiConsole.WriteLine();
var resultTable = new Table()
    .Border(TableBorder.Rounded)
    .BorderColor(Color.Green)
    .AddColumn(new TableColumn("[bold green]Results[/]").Centered())
    .HideHeaders();

resultTable.AddRow($"[bold]Time[/]    : {stopwatch.Elapsed.Minutes}m {stopwatch.Elapsed.Seconds}s");
resultTable.AddRow($"[bold green]Success[/] : {totalSuccess}");
if (totalFailed > 0)
    resultTable.AddRow($"[bold red]Failed[/]  : {totalFailed}");

AnsiConsole.Write(resultTable);

return totalFailed > 0 ? 1 : 0;


// ============================================================================
//  Helper methods
// ============================================================================

static (string folder, string model, string language, int threads) ParseArguments(string[] args)
{
    var folder = Directory.GetCurrentDirectory();
    var model = DefaultModel;
    var language = DefaultLanguage;
    var threads = DefaultThreads;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--model" or "-m" when i + 1 < args.Length:
                model = args[++i];
                break;
            case "--language" or "-l" when i + 1 < args.Length:
                language = args[++i];
                break;
            case "--threads" or "-t" when i + 1 < args.Length:
                if (int.TryParse(args[++i], out var t)) threads = t;
                break;
            default:
                if (!args[i].StartsWith('-') && Directory.Exists(args[i]))
                    folder = Path.GetFullPath(args[i]);
                break;
        }
    }

    return (folder, model, language, threads);
}

static GgmlType ParseGgmlType(string model) => model.ToLowerInvariant() switch
{
    "tiny" => GgmlType.Tiny,
    "base" => GgmlType.Base,
    "small" => GgmlType.Small,
    "medium" => GgmlType.Medium,
    "large" => GgmlType.LargeV3,
    _ => GgmlType.Base,
};

static string? FindFfmpeg()
{
    // Check if ffmpeg is on PATH
    var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
    foreach (var dir in pathDirs)
    {
        var candidate = Path.Combine(dir, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        if (File.Exists(candidate)) return candidate;
    }

    // Try winget install location on Windows
    if (OperatingSystem.IsWindows())
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wingetDir = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wingetDir))
        {
            foreach (var file in Directory.EnumerateFiles(wingetDir, "ffmpeg.exe", SearchOption.AllDirectories))
            {
                return file;
            }
        }
    }

    return null;
}

static (List<string> videosToProcess, int alreadyHaveSrt) ScanForVideos(string targetDir, string[] extensions)
{
    var videos = new List<string>();
    var alreadyCount = 0;

    foreach (var file in Directory.EnumerateFiles(targetDir, "*.*", SearchOption.AllDirectories))
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (!extensions.Contains(ext)) continue;

        var srtPath = Path.ChangeExtension(file, ".srt");
        if (File.Exists(srtPath))
            alreadyCount++;
        else
            videos.Add(file);
    }

    videos.Sort(StringComparer.OrdinalIgnoreCase);
    return (videos, alreadyCount);
}

static async Task ExtractAudio(string ffmpegPath, string videoPath, string wavPath)
{
    var psi = new ProcessStartInfo
    {
        FileName = ffmpegPath,
        Arguments = $"-i \"{videoPath}\" -ar 16000 -ac 1 -c:a pcm_s16le -y \"{wavPath}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    using var process = Process.Start(psi)!;
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        var error = await process.StandardError.ReadToEndAsync();
        throw new Exception($"FFmpeg failed (exit {process.ExitCode}): {error[..Math.Min(error.Length, 200)]}");
    }
}

static void WriteSrt(List<SubtitleSegment> segments, string srtPath)
{
    var sb = new StringBuilder();
    for (var i = 0; i < segments.Count; i++)
    {
        var seg = segments[i];
        sb.AppendLine((i + 1).ToString());
        sb.AppendLine($"{FormatTimestamp(seg.Start)} --> {FormatTimestamp(seg.End)}");
        sb.AppendLine(seg.Text.Trim());
        sb.AppendLine();
    }

    File.WriteAllText(srtPath, sb.ToString(), Encoding.UTF8);
}

static string FormatTimestamp(TimeSpan ts)
{
    return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
}

record SubtitleSegment(TimeSpan Start, TimeSpan End, string Text);
