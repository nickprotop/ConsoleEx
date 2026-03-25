# Video Playback

SharpConsoleUI can play video files directly in the terminal using `VideoControl`. Frames are decoded via FFmpeg and rendered using three visual modes: half-block (best color), ASCII (density characters), and braille (highest spatial resolution).

> **Inspiration:** The rendering approach is inspired by [buddy](https://github.com/JVSCHANDRADITHYA/buddy), a Python terminal video player that pioneered half-block + braille + ASCII modes with FFmpeg frame decoding. VideoControl brings the same concept natively into .NET with SharpConsoleUI's compositing pipeline, pre-allocated buffers, dynamic resize, and overlay controls.

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [Quick Start](#quick-start)
4. [VideoControl](#videocontrol)
5. [Render Modes](#render-modes)
6. [Builder API](#builder-api)
7. [Playback Controls](#playback-controls)
8. [Overlay Status Bar](#overlay-status-bar)
9. [Dynamic Resize](#dynamic-resize)
10. [Events](#events)
11. [Architecture](#architecture)
12. [Error Handling](#error-handling)
13. [Performance Notes](#performance-notes)
14. [Sample Videos](#sample-videos)

## Overview

The video system consists of:

- **`VideoControl`** — A `BaseControl` that plays video files with three render modes
- **`VideoFrameReader`** — Manages the FFmpeg subprocess, piping raw RGB24 frames
- **`VideoFrameRenderer`** — Converts raw pixel data to `Cell[,]` arrays for terminal display
- **`VideoRenderMode`** — Enum selecting half-block, ASCII, or braille rendering
- **`VideoPlaybackState`** — Enum tracking stopped, playing, or paused state
- **`VideoDefaults`** — Configuration constants (FPS, timeouts, overlay timing, etc.)
- **`VideoControlBuilder`** — Fluent builder for `VideoControl`

## Requirements

**FFmpeg** must be installed and available on the system PATH:

```bash
# Linux
sudo apt install ffmpeg

# macOS
brew install ffmpeg

# Windows
winget install ffmpeg
```

If FFmpeg is not found, `VideoControl` displays a friendly error message inside the control area with installation instructions.

## Quick Start

```csharp
// Simplest usage: play a video file
var video = Controls.Video("video.mp4")
    .Fill()
    .Build();

var window = new WindowBuilder(windowSystem)
    .WithTitle("Video Player")
    .WithSize(80, 30)
    .Centered()
    .AddControl(video)
    .BuildAndShow();

video.Play();

// Cleanup on close
window.OnClosed += (_, _) =>
{
    video.Stop();
    video.Dispose();
};
```

## VideoControl

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `FilePath` | `string?` | Path to the video file |
| `RenderMode` | `VideoRenderMode` | Current render mode (default: `HalfBlock`) |
| `PlaybackState` | `VideoPlaybackState` | Current state: `Stopped`, `Playing`, or `Paused` |
| `TargetFps` | `int` | Target frame rate, clamped 1–120 (default: 30) |
| `Looping` | `bool` | Whether playback loops (default: `false`) |
| `OverlayEnabled` | `bool` | Whether the overlay status bar is enabled |
| `DurationSeconds` | `double` | Total video duration in seconds (read-only) |
| `CurrentTime` | `double` | Current playback position in seconds (read-only) |
| `FrameCount` | `long` | Total frames rendered since play started (read-only) |
| `ErrorMessage` | `string?` | Error message shown in control (read-only) |
| `IsEnabled` | `bool` | Whether input is processed (default: `true`) |

### Methods

| Method | Description |
|--------|-------------|
| `Play()` | Starts or resumes playback |
| `Pause()` | Pauses playback |
| `TogglePlayPause()` | Toggles between play and pause |
| `Stop()` | Stops playback and releases FFmpeg |
| `CycleRenderMode()` | Cycles: HalfBlock → ASCII → Braille → HalfBlock |
| `PlayFile(string path)` | Stops current, sets path, starts playing |

## Render Modes

### Half-Block (Default)

Uses the Unicode upper half block character (`▀`, U+2580). Each terminal cell encodes **2 vertical pixels**: the foreground color is the top pixel, the background color is the bottom pixel.

- **Resolution**: 2x vertical pixel density
- **Color fidelity**: Best — full 24-bit RGB per pixel
- **Best for**: Color-rich video content

```
▀▀▀▀▀▀▀▀    ← Each cell = 2 pixels vertically
▀▀▀▀▀▀▀▀       fg = top pixel color
▀▀▀▀▀▀▀▀       bg = bottom pixel color
```

### ASCII

Maps pixel brightness to a density character ramp: ` .:-=+*#%@` (10 levels from sparse to dense). Each cell is colored with the pixel's RGB as foreground.

- **Resolution**: 1:1 with terminal grid
- **Color fidelity**: Good — foreground colored, dark background
- **Best for**: Retro aesthetic, lower bandwidth

### Braille

Uses Unicode braille characters (U+2800–U+28FF). Each terminal cell covers a **2×4 pixel region** (8 sub-pixels). Brightness is thresholded to activate dots; the cell's foreground color is the average RGB of the 8 pixels.

- **Resolution**: Highest — 8 sub-pixels per cell
- **Color fidelity**: Moderate — averaged colors
- **Best for**: Maximizing spatial detail in small areas

```
Braille dot layout:     Bit values:
col0 col1               0x01  0x08
●    ●    row0          0x02  0x10
●    ●    row1          0x04  0x20
●    ●    row2          0x40  0x80
●    ●    row3
```

### Switching Modes

```csharp
// Set mode directly
videoControl.RenderMode = VideoRenderMode.Braille;

// Or cycle through modes
videoControl.CycleRenderMode();
```

Changing the render mode during playback automatically restarts FFmpeg with the correct pixel dimensions for the new mode, seeking to the current playback position.

## Builder API

```csharp
var video = Controls.Video()                // Create builder
    .WithFile("movie.mp4")                  // Set video file
    .WithRenderMode(VideoRenderMode.Ascii)  // Set render mode
    .WithTargetFps(24)                      // Set target FPS
    .WithLooping()                          // Enable looping
    .WithOverlay()                          // Enable overlay bar
    .Fill()                                 // Fill available space
    .WithMargin(1, 0, 1, 0)                // Set margins
    .WithName("mainVideo")                  // Set control name
    .OnPlaybackStateChanged((s, state) =>   // Subscribe to state changes
    {
        // state is VideoPlaybackState
    })
    .OnPlaybackEnded((s, e) =>              // Subscribe to playback end
    {
        // Video finished
    })
    .Build();
```

Shorthand with file path:

```csharp
var video = Controls.Video("movie.mp4").Fill().Build();
```

## Playback Controls

### Keyboard

| Key | Action |
|-----|--------|
| `Space` | Play / Pause toggle |
| `M` | Cycle render mode |
| `L` | Toggle looping |
| `Esc` | Stop playback |

### Mouse

Clicking the video control gives it focus and shows the overlay status bar.

### Programmatic

```csharp
video.Play();
video.Pause();
video.TogglePlayPause();
video.Stop();
video.PlayFile("another.mp4");  // Stop + load + play
video.CycleRenderMode();
video.Looping = true;
```

## Overlay Status Bar

When enabled via `.WithOverlay()`, a bottom-row overlay bar appears on user interaction (key press or mouse click) and auto-hides after 3 seconds.

The overlay shows:
- **Playback state icon**: `>` (playing), `||` (paused), `[]` (stopped)
- **Current time / duration**: `01:23 / 05:00`
- **Render mode**: `HalfBlock`, `Ascii`, or `Braille`
- **Loop indicator**: `Loop` when enabled
- **Keyboard hints**: `Space:Play M:Mode L:Loop`

```csharp
// Enable overlay
var video = Controls.Video("movie.mp4")
    .WithOverlay()      // Enable
    .Build();

// Toggle at runtime
video.OverlayEnabled = true;
video.OverlayEnabled = false;
```

## Dynamic Resize

When the window is resized during playback, `VideoControl` automatically:
1. Detects the size change in `PaintDOM` (via DOM layout bounds)
2. Saves the current playback timestamp
3. Kills the current FFmpeg process
4. Relaunches FFmpeg with the new pixel dimensions
5. Seeks to the saved timestamp to continue seamlessly

This works in both `Playing` and `Paused` states.

## Events

```csharp
// Playback state changed (Playing, Paused, Stopped)
video.PlaybackStateChanged += (sender, state) =>
{
    Console.Title = $"State: {state}";
};

// Playback reached end of file
video.PlaybackEnded += (sender, args) =>
{
    // Cleanup or switch to next video
};

// Mouse events (from IMouseAwareControl)
video.MouseClick += (sender, args) => { /* ... */ };
```

## Architecture

### Frame Pipeline

```
┌──────────┐     raw RGB24     ┌──────────────────┐     Cell[,]     ┌──────────────┐
│  FFmpeg   │ ──────────────── │ VideoFrameRenderer│ ──────────────► │ CharacterBuffer│
│ subprocess│   byte[] pipe    │ (half/ascii/brail)│  pre-allocated  │  (PaintDOM)   │
└──────────┘                   └──────────────────┘                  └──────────────┘
     ▲                                                                      │
     │ stdin redirected (terminal safe)                                     ▼
     │                                                               ┌──────────┐
     └─── ffmpeg -i file -f rawvideo -pix_fmt rgb24 -s WxH -        │ Terminal │
                                                                     └──────────┘
```

### Threading Model

- **UI thread**: `PaintDOM`, property changes, overlay, resize detection
- **Background thread**: `PlaybackLoopAsync` — frame reading, rendering, timing
- **Thread safety**: `_frameLock` protects `_currentFrameCells`; `Container?.Invalidate(true)` is the only thread-safe call from background
- **UI marshaling**: `EnqueueOnUIThread` for state changes from background (looping restart, error messages)

### FFmpeg Integration

VideoControl shells out to the `ffmpeg` CLI rather than using a native binding:

- **Zero NuGet size impact** — no bundled native libraries
- **Cross-platform** — works wherever FFmpeg is installed
- **stdin redirected** — prevents FFmpeg from corrupting terminal settings
- **stderr discarded** — prevents pipe deadlock via `BeginErrorReadLine`
- **Process cleanup** — `Kill(entireProcessTree: true)` on dispose

## Error Handling

### FFmpeg Not Found

When FFmpeg is not on PATH, the control displays a centered warning message:

```
FFmpeg not found. Install it to play videos:
  Linux:   sudo apt install ffmpeg
  macOS:   brew install ffmpeg
  Windows: winget install ffmpeg
```

### File Not Found

A `FileNotFoundException` is thrown if the video path doesn't exist.

### Corrupt Video / Decode Failure

If FFmpeg fails during decoding, the playback loop catches the exception and stops gracefully via `EnqueueOnUIThread`.

## Performance Notes

- **Pre-allocated cell buffers**: `VideoFrameRenderer.RenderFrameInto()` writes into a pre-allocated `Cell[,]` array — no per-frame GC allocation
- **Frame skipping**: When rendering falls behind, up to 5 frames are skipped to catch up with wall-clock time
- **Pixel dimensions match render mode**: FFmpeg scales to exactly the needed pixel count — half-block needs `(cols, rows*2)`, ASCII needs `(cols, rows)`, braille needs `(cols*2, rows*4)`
- **~30 fps target**: Configurable via `TargetFps`, capped at the video's native FPS

## Sample Videos

The DemoApp includes two bundled sample videos:

| File | Duration | Resolution | Size | Purpose |
|------|----------|------------|------|---------|
| `sample.mp4` | 4s | 360p | 322 KB | Quick demo (colorful particles) |
| `sample_bunny.mp4` | 30s | 480p | 4 MB | Extended demo (Big Buck Bunny) |

The Video Player demo falls back to `sample.mp4` if the user cancels the file picker.

For automated testing, a deterministic test pattern is embedded as a resource:
- `SharpConsoleUI/Resources/test_sample.mp4` — 3s, 160×120, 10fps (49 KB)
