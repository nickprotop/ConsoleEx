# Video Playback

SharpConsoleUI can play video files directly in the terminal using `VideoControl`. Frames are decoded via FFmpeg and rendered using one of four modes:

- **Kitty graphics protocol** — true-color pixel-accurate playback on supporting terminals (Kitty, WezTerm, Ghostty). Uses zlib-compressed raw RGB frame updates over the [Kitty graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/) animation-frame path (`a=f,r=1`) for smooth in-place updates.
- **Half-block** — best color fidelity via the `▀` cell encoding; works in every terminal.
- **ASCII** — density ramp for a retro look / low bandwidth.
- **Braille** — highest spatial resolution via 2×4 dot patterns.

The default mode is `Auto`: Kitty on supporting terminals, half-block everywhere else — no configuration needed to get the best output on each terminal.

> **Inspiration:** The cell-based rendering approach is inspired by [buddy](https://github.com/JVSCHANDRADITHYA/buddy), a Python terminal video player that pioneered half-block + braille + ASCII modes with FFmpeg frame decoding. VideoControl brings the same concept natively into .NET with SharpConsoleUI's compositing pipeline, pre-allocated buffers, dynamic resize, and overlay controls — and layers Kitty graphics on top for pixel-accurate playback where supported.

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [Quick Start](#quick-start)
4. [Streaming](#streaming)
5. [VideoControl](#videocontrol)
6. [Render Modes](#render-modes)
7. [Builder API](#builder-api)
8. [Playback Controls](#playback-controls)
9. [Overlay Status Bar](#overlay-status-bar)
10. [Dynamic Resize](#dynamic-resize)
11. [Events](#events)
12. [Architecture](#architecture)
13. [Error Handling](#error-handling)
14. [Performance Notes](#performance-notes)
15. [Sample Videos](#sample-videos)
16. [Complete Example](#complete-example--video-player-app)

## Overview

The video system consists of:

- **`VideoControl`** — A `BaseControl` that plays video files; picks the best render backend for the current terminal
- **`VideoFrameReader`** — Manages the FFmpeg subprocess, piping raw RGB24 frames
- **`IVideoFrameSink`** — Strategy interface for frame delivery; concrete implementations are:
  - **`CellVideoFrameSink`** — Cell-based rendering (HalfBlock / ASCII / Braille) via `VideoFrameRenderer`
  - **`KittyVideoFrameSink`** — Kitty graphics protocol playback with zlib-compressed in-place frame updates
- **`VideoFrameRenderer`** — Converts raw pixel data to `Cell[,]` arrays (used by the cell sink)
- **`VideoRenderMode`** — Enum selecting `Auto`, `Kitty`, `HalfBlock`, `Ascii`, or `Braille`
- **`VideoPlaybackState`** — Enum tracking stopped, playing, or paused state
- **`VideoDefaults`** — Configuration constants (FPS, timeouts, overlay timing, Kitty pixel density, etc.)
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

## Streaming

VideoControl accepts any source that FFmpeg understands — not just local files. Pass a URL to `Source`, `Stream()`, or the builder's `WithSource()`:

```csharp
// HTTP/HTTPS — remote video file
video.Stream("https://example.com/video.mp4");

// HLS — adaptive streaming playlist
video.Stream("https://live.example.com/stream/playlist.m3u8");

// RTSP — IP camera or security feed
video.Stream("rtsp://camera.local:554/live");

// RTMP — live stream
video.Stream("rtmp://live.twitch.tv/app/stream_key");

// FTP
video.Stream("ftp://server.local/videos/clip.mp4");

// Via builder
var video = Controls.Video("https://example.com/video.mp4")
    .Fill()
    .WithOverlay()
    .Build();
```

### Streaming Notes

- **No seeking on live streams** — `Stream()` starts from the current point; seek is automatically disabled when the source has no known duration
- **Duration unknown** — `DurationSeconds` returns 0 for live streams; the overlay shows elapsed time only
- **Buffering** — FFmpeg handles network buffering internally; frames arrive as they're decoded
- **Reconnection** — if the stream drops, playback stops; call `Stream()` again to reconnect
- **Dynamic resize** works with streams — FFmpeg restarts at the new resolution

## VideoControl

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Source` | `string?` | Video source — file path or URL (HTTP, RTSP, HLS, RTMP, FTP, etc.) |
| `FilePath` | `string?` | Alias for `Source` (backward compatibility) |
| `RenderMode` | `VideoRenderMode` | Requested render mode (default: `Auto`) |
| `EffectiveRenderMode` | `VideoRenderMode` | The mode actually in use — equal to `RenderMode` for concrete modes, resolves to `Kitty` or `HalfBlock` for `Auto` depending on terminal capability (read-only) |
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
| `CycleRenderMode()` | Cycles through concrete modes. On Kitty-capable terminals: HalfBlock → ASCII → Braille → Kitty → HalfBlock. On other terminals: HalfBlock → ASCII → Braille → HalfBlock. `Auto` is skipped (it's a meta-mode); pressing M while in Auto jumps to HalfBlock as the first concrete step. |
| `PlayFile(string path)` | Stops current, sets file path, starts playing |
| `Stream(string url)` | Stops current, sets source URL, starts playing |

## Render Modes

### Auto (Default)

Picks the best backend for the current terminal:

- **Kitty-capable terminal** (Kitty, WezTerm, Ghostty, …) → **Kitty graphics** (pixel-accurate).
- **Any other terminal** → **HalfBlock** (universal cell rendering).

`Auto` is the zero-configuration default: the same code gets crisp true-color video on modern terminals and a graceful fallback everywhere else. The actual backend in use is visible on `EffectiveRenderMode` and in the status-bar overlay.

### Kitty Graphics Protocol

Transmits video frames as real pixel data to the terminal using the [Kitty graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/). The first frame creates an image with a virtual placement (`a=T,U=1,c=cols,r=rows,o=z`) and subsequent frames update the root frame data in place (`a=f,r=1,o=z`) — this is the only sequence that avoids the protocol's "retransmit with same id deletes all placements" behaviour, so frames update smoothly with no flicker.

- **Resolution**: True pixel output — Kitty upscales the decoded frame into the placement rectangle.
- **Color fidelity**: Full 24-bit RGB, no cell-grid quantisation.
- **Best for**: Pixel-accurate playback on supporting terminals.
- **Wire format**: `f=24` raw RGB24 with `o=z` zlib compression — keeps bandwidth manageable at 30 fps.
- **Detection**: Via `TerminalCapabilities.SupportsKittyGraphics` (active APC probe + `KITTY_PID` / `WEZTERM_PANE` env-var fallback).
- **Fallback**: Requesting `Kitty` on a non-Kitty terminal silently falls back to HalfBlock and surfaces a one-time warning via `ErrorMessage`.

### Half-Block

Uses the Unicode upper half block character (`▀`, U+2580). Each terminal cell encodes **2 vertical pixels**: the foreground color is the top pixel, the background color is the bottom pixel. This is the universal fallback used whenever Kitty isn't available.

- **Resolution**: 2x vertical pixel density
- **Color fidelity**: Best cell-based option — full 24-bit RGB per pixel
- **Best for**: Color-rich video content in terminals without Kitty graphics

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
    .WithSource("movie.mp4")                // File path, URL, or stream URI
    // .WithFile("movie.mp4")              // Alias — same as WithSource
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
| `R` | Refresh Kitty image (recovers from the rare stuck-black state in Kitty mode; no-op in cell modes) |
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
- **Keyboard hints**: `Space:Play M:Mode L:Loop` (plus `R:Refresh` when Kitty mode is active)

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
                              ┌─── IVideoFrameSink ───┐
                              │                       │
┌──────────┐    raw RGB24     │  ┌────────────────┐   │    Cell[,] / Kitty APC   ┌──────────┐
│  FFmpeg  │ ──────────────── ├─▶│ CellVideoFrameSink│ ─┼──── placeholder cells ──▶│ Terminal │
│subprocess│   byte[] pipe    │  └────────────────┘   │                          └──────────┘
└──────────┘                  │  ┌────────────────┐   │    ESC_G… image bytes ────▶
     ▲                        └─▶│KittyVideoFrameSink│ ─┘    (bypasses cell buffer)
     │                           └────────────────┘
     │ stdin redirected (terminal safe)
     │
     └─── ffmpeg -i file -f rawvideo -pix_fmt rgb24 -s WxH -
```

`VideoControl.ResolveSink()` picks the sink on first paint based on `RenderMode` and the driver's `IGraphicsProtocol.SupportsKittyGraphics`. Both sinks implement `IngestFrame` (called on the playback thread with a fresh RGB24 byte buffer) and `Paint` (called on the render thread). The cell sink converts to a pre-allocated `Cell[,]` and blits on paint; the Kitty sink transmits the frame over the graphics protocol and writes stable placeholder cells into the buffer.

### Threading Model

- **UI thread**: `PaintDOM`, property changes, overlay, resize detection — calls `sink.Paint(...)`.
- **Background thread**: `PlaybackLoopAsync` — frame reading, calls `sink.IngestFrame(...)` which does the heavy work (cell conversion or Kitty transmission), then `Container?.Invalidate(true)`.
- **Thread safety**: Each sink carries its own lock. Sinks must be safe against concurrent `IngestFrame` (background) / `Paint` (UI) calls.
- **UI marshaling**: `EnqueueOnUIThread` for state changes from background (looping restart, error messages).

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

### Cell-based modes (HalfBlock / ASCII / Braille)

- **Pre-allocated cell buffers**: `VideoFrameRenderer.RenderFrameInto()` writes into a pre-allocated `Cell[,]` array — no per-frame GC allocation.
- **Pixel dimensions match render mode**: FFmpeg scales to exactly the needed pixel count — half-block needs `(cols, rows*2)`, ASCII needs `(cols, rows)`, braille needs `(cols*2, rows*4)`.

### Kitty mode

- **Decode resolution**: FFmpeg is asked for roughly `cellCols * KittyPixelsPerCellX × cellRows * KittyPixelsPerCellY` pixels, capped at `KittyMaxPixelWidth × KittyMaxPixelHeight`. Defaults (in `VideoDefaults`) are `4×8` per cell capped at `960×540` — tuned for smooth playback on typical terminal font sizes. Kitty scales the transmitted buffer into the placement rectangle.
- **In-place frame updates**: Only the first frame is a full transmit (`a=T,U=1,…`); every subsequent frame is an `a=f,r=1` root-frame edit. Placements are preserved across updates, so there's no tear-down flicker.
- **zlib compression (`o=z`)**: Every frame is compressed with `CompressionLevel.Fastest` before base64 encoding. Typical 2–4× wire reduction on natural video — the difference between "smooth playback" and "terminal choked on APC bytes".
- **Stable image ID + cell re-emit**: The image id is allocated once per sink and reused. Kitty doesn't automatically redraw a placement when its data changes, so the sink flips the low bit of the cell background each frame — invisible to the user (opaque pixels cover the cell background) but enough for the buffer diff to re-emit the placeholder cells and prompt a repaint.
- **Image recreated on resize only**: If FFmpeg's output size or the placement cell span changes, the old image is deleted and a new one is transmitted. Otherwise the same image id lives for the entire playback session.

### Shared

- **Frame skipping**: When rendering falls behind, up to 5 frames are skipped to catch up with wall-clock time.
- **~30 fps target**: Configurable via `TargetFps`, capped at the video's native FPS.

## Sample Videos

The DemoApp includes two bundled sample videos:

| File | Duration | Resolution | Size | Purpose |
|------|----------|------------|------|---------|
| `sample.mp4` | 4s | 360p | 322 KB | Quick demo (colorful particles) |
| `sample_bunny.mp4` | 30s | 480p | 4 MB | Extended demo (Big Buck Bunny) |

The Video Player demo falls back to `sample.mp4` if the user cancels the file picker.

For automated testing, a deterministic test pattern is embedded as a resource:
- `SharpConsoleUI/Resources/test_sample.mp4` — 3s, 160×120, 10fps (49 KB)

## Complete Example — Video Player App

A full, runnable console application that opens a file picker and plays the selected video. Copy this into a new project to get started immediately.

```bash
# Create a new project and add SharpConsoleUI
dotnet new console -n MyVideoPlayer
cd MyVideoPlayer
dotnet add package SharpConsoleUI
```

```csharp
// Program.cs — Complete terminal video player

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Video;

// 1. Create the window system
//    RenderMode.Buffer enables double-buffered rendering for smooth output.
//    Configure panels for a clean single-window app look.
var windowSystem = new ConsoleWindowSystem(
    RenderMode.Buffer,
    options: new ConsoleWindowSystemOptions(
        TopPanelConfig: panel => panel
            .Left(Elements.StatusText("")),
        ShowBottomPanel: false));

// 2. Set up panel text — this appears at the top of the terminal
windowSystem.PanelStateService.TopStatus =
    "Video Player — Space: Play/Pause | M: Mode | L: Loop | R: Refresh | Esc: Stop";

// 3. Handle Ctrl+C — shut down cleanly instead of hard-killing the process
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    windowSystem.Shutdown(0);
};

// 4. Build the VideoControl
//    Fill()        — stretch to use the entire window area
//    WithOverlay() — bottom status bar appears on key/click, hides after 3s
//    WithLooping() — restart from the beginning when the video ends
var videoControl = Controls.Video()
    .Fill()
    .WithOverlay()
    .WithLooping()
    .Build();

// 5. (Optional) React to playback state changes
videoControl.PlaybackStateChanged += (_, state) =>
{
    string mode = videoControl.RenderMode.ToString();
    string status = state switch
    {
        VideoPlaybackState.Playing => $"Playing ({mode})",
        VideoPlaybackState.Paused  => $"Paused ({mode})",
        _                          => "Stopped",
    };
    windowSystem.PanelStateService.TopStatus = $"Video Player — {status}";
};

// 6. Create the window and open a file picker asynchronously
//    WithAsyncWindowThread runs a background task tied to the window's lifetime.
//    The file picker is modal — it blocks this thread but not the UI.
//    BuildAndShow() creates the Window, registers it with the system, and displays it.
var window = new WindowBuilder(windowSystem)
    .WithTitle("Video Player")
    .Maximized()
    .WithColors(Color.White, Color.Black)
    .AddControl(videoControl)
    .WithAsyncWindowThread(async (win, ct) =>
    {
        // Open the file picker dialog
        var filePath = await FileDialogs.ShowFilePickerAsync(windowSystem,
            filter: "*.mp4;*.mkv;*.avi;*.webm;*.mov;*.flv;*.wmv");

        if (string.IsNullOrEmpty(filePath))
        {
            // User cancelled — exit the app
            windowSystem.EnqueueOnUIThread(() => windowSystem.Shutdown(0));
            return;
        }

        // Start playback on the UI thread
        // PlayFile() sets the path, launches FFmpeg, and begins decoding
        windowSystem.EnqueueOnUIThread(() =>
        {
            win.Title = $"Video — {Path.GetFileName(filePath)}";
            videoControl.PlayFile(filePath);
        });

        // Keep alive until the window closes (ct is cancelled)
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }
    })
    .BuildAndShow();

// 7. Clean up when the window closes
//    Stop() cancels the playback loop; Dispose() kills the FFmpeg process.
window.OnClosed += (_, _) =>
{
    videoControl.Stop();
    videoControl.Dispose();
};

// 8. Run the window system — blocks until Shutdown() is called
await Task.Run(() => windowSystem.Run());
```

### What this does

1. Creates a **maximized window** with a black background — ideal for video
2. Opens a **file picker** on launch to select a video file
3. Plays the video with the **overlay enabled** — press any key to see playback info
4. Updates the **status bar** in real-time with the current state and render mode
5. Handles **cleanup** — FFmpeg process is killed when the window closes
6. **Exits cleanly** on Ctrl+C or when the user cancels the file picker
