# Subtitle Generator (.NET)

A C# console application that automatically generates `.srt` subtitle files for video files using [Whisper.net](https://github.com/sandrohanea/whisper.net) and [Spectre.Console](https://spectreconsole.net/). Runs entirely on your machine — no internet needed after setup.

**No Python required.** This is a standalone .NET app — just install the .NET SDK and FFmpeg.

**Supported video formats:** `.mp4`, `.webm`, `.mkv`, `.avi`, `.mov`

---

## What Does It Do?

This tool scans a folder for video files, listens to the audio using an AI speech recognition model (Whisper), and creates `.srt` subtitle files next to each video.

```
My Video.mp4    →    My Video.srt     ← created automatically
Lecture 01.webm →    Lecture 01.srt   ← created automatically
```

Most video players (VLC, PotPlayer, mpv) will **automatically** load the subtitles when you play the video.

---

## Table of Contents

- [How It Works](#how-it-works)
- [Prerequisites](#prerequisites)
  - [Windows](#windows)
  - [Linux](#linux)
- [Usage](#usage)
  - [Basic Usage](#basic-usage)
  - [Prompt Walkthrough](#prompt-walkthrough)
  - [Command-Line Options](#command-line-options)
  - [Examples](#examples)
- [Performance Presets](#performance-presets)
- [Settings](#settings)
  - [Whisper Model (Accuracy)](#whisper-model-accuracy)
  - [Sampling Strategy](#sampling-strategy)
  - [CPU Threads](#cpu-threads)
  - [Language](#language)
- [FAQ](#faq)
- [Troubleshooting](#troubleshooting)

---

## How It Works

1. **Prompts** you to select a performance preset (Fast, Balanced, Quality, or Custom)
2. **Scans** the target folder (and all subfolders) for video files
3. **Skips** any video that already has a matching `.srt` file
4. **Extracts** audio from the video using FFmpeg (converted to 16kHz mono WAV)
5. **Transcribes** the audio using the Whisper AI model via Whisper.net, showing a **per-video progress bar** that advances in real time
6. **Saves** the transcription as a timestamped `.srt` subtitle file

On the **first run**, the Whisper model is downloaded automatically (~142 MB for `base`) and cached at `~/.cache/whisper-net/`. Subsequent runs start instantly.

---

## Prerequisites

You need two things installed:

| # | Program | What It Does | How to Install |
|---|---------|-------------|----------------|
| 1 | **.NET 8 SDK** | Runs the application | See below |
| 2 | **FFmpeg** | Extracts audio from video files | See below |

### Windows

<details>
<summary><strong>Click to expand Windows installation steps</strong></summary>

#### Step 1 — Install .NET 8 SDK

1. Open **PowerShell** or **Terminal**
2. Run:
   ```powershell
   winget install Microsoft.DotNet.SDK.8
   ```
3. **Close and reopen** your terminal
4. Verify:
   ```powershell
   dotnet --version
   ```
   You should see `8.x.x` or higher.

#### Step 2 — Install FFmpeg

1. Run:
   ```powershell
   winget install Gyan.FFmpeg
   ```
2. **Close and reopen** your terminal
3. Verify:
   ```powershell
   ffmpeg -version
   ```

> **Tip:** The app can auto-detect FFmpeg installed via winget even if it's not on your PATH.

</details>

### Linux

<details>
<summary><strong>Click to expand Linux installation steps</strong></summary>

#### Ubuntu/Debian

```bash
# .NET 8 SDK
sudo apt update
sudo apt install dotnet-sdk-8.0 -y
dotnet --version

# FFmpeg
sudo apt install ffmpeg -y
ffmpeg -version
```

#### Fedora

```bash
sudo dnf install dotnet-sdk-8.0 -y
sudo dnf install https://mirrors.rpmfusion.org/free/fedora/rpmfusion-free-release-$(rpm -E %fedora).noarch.rpm -y
sudo dnf install ffmpeg -y
```

#### Arch

```bash
sudo pacman -S dotnet-sdk ffmpeg --noconfirm
```

</details>

---

## Usage

### Basic Usage

```bash
# Clone the repository
git clone https://github.com/vaibhavPH/subtitle-generator-dotnet.git
cd subtitle-generator-dotnet/SubtitleGenerator

# Run — process current directory
dotnet run

# Run — process a specific folder
dotnet run -- "D:\Videos\My Course"
```

> **First run** will download the Whisper model (~142 MB for `base`). This only happens once.

### Prompt Walkthrough

When you launch the app, you'll be guided through an interactive setup before processing begins:

**Step 1 — Select a performance preset:**
```
Select a performance preset:
> Fast
  Balanced
  Quality
  Custom
```
Use arrow keys to navigate, Enter to select. If you pick **Custom**, you'll be asked to configure each parameter individually (see below).

**Step 1a — Custom mode prompts (only if you selected Custom):**
```
Whisper model size (larger = better quality, slower):
> tiny
  base
  small
  medium
  large

Sampling strategy (greedy = fast, beam = more accurate):
> greedy
  beam

Best-of decodings (1-10, higher = slower but picks best result): 1
Temperature (0.0 = deterministic, higher = more creative): 0.2
Entropy threshold (lower = stricter fallback, default 2.4): 2.4
Threads (1-16): 16
```
If you select **beam** as the strategy, you'll also be asked for the beam size (1–10).

**Step 2 — Configuration banner is displayed:**
```
╭──────────────────────────────────────────────╮
│             Subtitle Generator               │
├──────────────────────────────────────────────┤
│ Preset   : Fast                              │
│ Model    : tiny                              │
│ Strategy : greedy (best-of: 1)               │
│ Threads  : 16 / 16 available                 │
│ Language  : en                               │
│ Folder   : D:\Videos\My Course               │
╰──────────────────────────────────────────────╯
```

**Step 3 — Processing begins with per-video progress:**
```
 ───────────────────────────────────────────────
  Videos needing subtitles   3
  Already have subtitles     1
 ───────────────────────────────────────────────

  [1/3] Lecture 01.mp4
    Lecture 01.mp4  ━━━━━━━━━━━━━━━━━━━━━━━  62%  00:01:12
```
The progress bar advances in real time as Whisper processes the audio, based on the current timestamp relative to the video's total duration.

**Step 4 — Summary:**
```
╭──────────────────╮
│     Results      │
├──────────────────┤
│ Time    : 2m 14s │
│ Success : 3      │
╰──────────────────╯
```

### Command-Line Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `folder` | | Current directory | Target folder to scan for videos |
| `--model` | `-m` | `base` | Whisper model size: `tiny`, `base`, `small`, `medium`, `large` |
| `--threads` | `-t` | `0` (all cores) | Number of CPU threads to use |
| `--language` | `-l` | `en` | Language code (`en`, `es`, `fr`, `de`, `hi`, etc.) |

### Examples

**Process a specific folder:**
```bash
dotnet run -- "D:\Videos\My Course"
```

**Use the medium model for better accuracy:**
```bash
dotnet run -- "D:\Videos" --model medium
```

**Limit CPU usage so your computer stays responsive:**
```bash
dotnet run -- "D:\Videos" --threads 4
```

**Process Spanish-language videos:**
```bash
dotnet run -- "D:\Videos" --language es
```

**Combine options:**
```bash
dotnet run -- "D:\Videos\My Course" --model small --threads 4 --language en
```

---

## Performance Presets

When you run the app, you'll be prompted to select a preset that controls the quality vs. speed trade-off:

| Preset | Model | Strategy | Beam Size | Best-of | Temperature | Best For |
|--------|-------|----------|-----------|---------|-------------|----------|
| **Fast** | `tiny` | Greedy | 1 | 1 | 0.0 | Quick drafts, clear audio |
| **Balanced** | `base` | Beam search | 5 | 1 | 0.2 | General use (default) |
| **Quality** | `medium` | Beam search | 8 | 5 | 0.2 | Best accuracy, slower |
| **Custom** | You pick | You pick | You pick | You pick | You pick | Full control |

**Example:** A 3-minute video that takes ~6 minutes on Balanced can finish in under a minute on Fast.

The **Custom** preset lets you fine-tune each parameter individually:
- **Model size** — tiny/base/small/medium/large
- **Sampling strategy** — greedy (fast) or beam search (more accurate)
- **Beam size** — number of beams to explore (1–10, beam search only)
- **Best-of decodings** — run N decodings and pick the best (1–10)
- **Temperature** — 0.0 = deterministic, higher = more random
- **Entropy threshold** — controls decoder fallback sensitivity
- **Threads** — CPU thread count

---

## Settings

### Whisper Model (Accuracy)

Bigger models produce more accurate subtitles but take longer, especially on CPU.

| Model | Download Size | RAM Needed | Time for 10-min Video | Quality |
|-------|--------------|------------|----------------------|---------|
| `tiny` | 39 MB | ~1 GB | ~1 minute | Poor — many errors |
| `base` | 142 MB | ~1 GB | ~2 minutes | Good — works for clear speech |
| `small` | 466 MB | ~2 GB | ~5 minutes | Better — fewer mistakes |
| `medium` | 1.5 GB | ~5 GB | ~20 minutes | Great — recommended if you can wait |
| `large` | 2.9 GB | ~10 GB | ~50 minutes | Best — really needs a GPU |

**Which should I pick?**
- **Fast results on CPU:** `base` (default)
- **Better quality, more patience:** `medium`
- **NVIDIA GPU available:** `medium` or `large`

The model is downloaded once and cached at `~/.cache/whisper-net/`.

### Sampling Strategy

Controls how Whisper decodes speech into text.

| Strategy | Speed | Accuracy | How It Works |
|----------|-------|----------|--------------|
| **Greedy** | Fast | Good | Picks the most likely token at each step |
| **Beam search** | Slower | Better | Explores multiple candidate sequences and picks the best |

- **Beam size** (beam search only): More beams = more candidates explored = better results but slower. Default: 5.
- **Best-of** (greedy): Runs multiple decodings and picks the one with highest confidence. Default: 1.

### CPU Threads

Controls how much of your CPU the tool uses.

| Setting | Value | Speed | System Impact |
|---------|-------|-------|---------------|
| **Maximum** | `--threads 0` (default) | Fastest | Computer may lag |
| **Balanced** | `--threads 4` (on 8-core) | Good | Computer stays usable |
| **Background** | `--threads 2` | Slow | Barely noticeable |

**Find your core count:**
- **Windows:** Task Manager → Performance → CPU → "Logical processors"
- **Linux:** `nproc`

### Language

Setting the language explicitly is faster because Whisper skips auto-detection.

| Code | Language |
|------|----------|
| `en` | English |
| `es` | Spanish |
| `fr` | French |
| `de` | German |
| `hi` | Hindi |
| `ja` | Japanese |
| `zh` | Chinese |
| `pt` | Portuguese |

Use `--language ""` (empty) to auto-detect the language per video.

See the [full list of ISO 639-1 codes](https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes).

---

## FAQ

**Q: Can I use my computer while it runs?**
Yes. Set threads to half your cores: `dotnet run -- . --threads 4`

**Q: Can I stop and resume later?**
Yes. Press Ctrl+C to stop. Videos that already have `.srt` files are skipped on the next run.

**Q: Will it overwrite existing subtitles?**
No. If a `.srt` file already exists, that video is skipped.

**Q: The subtitles have errors. How do I improve them?**
Select the **Quality** preset, or use **Custom** to pick a bigger model and beam search. Delete the `.srt` files you want to redo first.

**Q: How much disk space do subtitle files use?**
Almost none. A typical `.srt` is 5–20 KB. Even 1,000 videos use under 20 MB total.

**Q: Can I use my NVIDIA GPU?**
Yes. Replace the `Whisper.net.Runtime` package with the CUDA runtime:
```bash
cd SubtitleGenerator
dotnet remove package Whisper.net.Runtime
dotnet add package Whisper.net.Runtime.Cuda
```
Whisper will automatically use the GPU, making it 5–10x faster.

**Q: How is this different from the Python version?**
This version uses [Whisper.net](https://github.com/sandrohanea/whisper.net) (C++ whisper.cpp under the hood) instead of OpenAI's Python Whisper. It doesn't require Python, pip, or PyTorch — just the .NET SDK and FFmpeg.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `dotnet` not recognized | Close and reopen your terminal. Verify with `dotnet --version`. |
| `ffmpeg` not recognized | Close and reopen your terminal. The app can auto-detect winget installs. |
| Shows 0 videos | Make sure the app is pointed at the right folder. Check video file extensions are supported. |
| Out of memory | Select the **Fast** preset or use a smaller model (`tiny`/`base`). Close other programs. |
| Very slow | Normal on CPU. Select the **Fast** preset for quickest results. Consider GPU (see FAQ). |
| Model download fails | Check your internet connection. The model downloads from Hugging Face on first run. |
| `AVX is not supported` | Your CPU doesn't support AVX instructions. Run: `dotnet remove package Whisper.net.Runtime` then `dotnet add package Whisper.net.Runtime.NoAvx` |

---

## Project Structure

```
subtitle-generator-dotnet/
├── .gitignore
├── README.md
└── SubtitleGenerator/
    ├── SubtitleGenerator.csproj    # Project file with NuGet dependencies
    └── Program.cs                  # Application code
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| [Spectre.Console](https://www.nuget.org/packages/Spectre.Console) | 0.55.0 | Rich terminal UI (tables, progress bars, colors) |
| [Whisper.net](https://www.nuget.org/packages/Whisper.net) | 1.9.0 | .NET bindings for whisper.cpp speech recognition |
| [Whisper.net.Runtime.NoAvx](https://www.nuget.org/packages/Whisper.net.Runtime.NoAvx) | 1.9.0 | Native whisper.cpp runtime (CPU, no AVX required) |
