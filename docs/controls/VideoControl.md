# VideoControl

Plays video in the terminal — local files or network streams — decoded with FFmpeg and rendered
through Kitty graphics or a cell-based fallback.

> **The full reference for this control lives in [Video Playback](../VIDEO_PLAYBACK.md).**
> This page is a short orientation and a set of links into it.

## Overview

`VideoControl` decodes with FFmpeg and picks a render path to match the terminal. On Kitty-capable
terminals it draws real pixels; elsewhere it falls back to half-block, ASCII, or braille cells. The
default `Auto` mode resolves this for you, and the user can cycle concrete modes at runtime.

Sources are anything FFmpeg understands — a file path, or an HTTP/RTSP/HLS/RTMP/FTP URL. An optional
overlay bar shows transport state and keyboard hints, and the control handles resize, looping, and
playback events.

**FFmpeg must be installed** — see [Requirements](../VIDEO_PLAYBACK.md#requirements).

## Quick Start

```csharp
var video = Controls.Video("movie.mp4")
    .WithOverlay()
    .Fill()
    .Build();

window.AddControl(video);
video.Play();
```

Streaming works the same way:

```csharp
var stream = Controls.Video()
    .WithSource("https://example.com/live.m3u8")
    .Fill()
    .Build();
```

## Where to read more

| Topic | Section |
|-------|---------|
| Installing FFmpeg per platform | [Requirements](../VIDEO_PLAYBACK.md#requirements) |
| All properties (`Source`, `RenderMode`, `PlaybackState`, `TargetFps`, `Looping`, …) | [VideoControl → Properties](../VIDEO_PLAYBACK.md#properties) |
| `Play`, `Pause`, `Stop`, `TogglePlayPause`, `PlayFile`, `Stream`, `CycleRenderMode` | [VideoControl → Methods](../VIDEO_PLAYBACK.md#methods) |
| All builder methods | [Builder API](../VIDEO_PLAYBACK.md#builder-api) |
| Sizing and alignment | [Sizing and alignment](../VIDEO_PLAYBACK.md#sizing-and-alignment) |
| Auto / Kitty / HalfBlock / ASCII / Braille | [Render Modes](../VIDEO_PLAYBACK.md#render-modes) |
| Network sources and their caveats | [Streaming](../VIDEO_PLAYBACK.md#streaming) |
| Keyboard and mouse controls | [Playback Controls](../VIDEO_PLAYBACK.md#playback-controls) |
| The status bar and its hints | [Overlay Status Bar](../VIDEO_PLAYBACK.md#overlay-status-bar) |
| `PlaybackStateChanged`, `PlaybackEnded` | [Events](../VIDEO_PLAYBACK.md#events) |
| Missing FFmpeg, missing file, decode failure | [Error Handling](../VIDEO_PLAYBACK.md#error-handling) |
| Frame pipeline and threading model | [Architecture](../VIDEO_PLAYBACK.md#architecture) |
| A full working player | [Complete Example — Video Player App](../VIDEO_PLAYBACK.md#complete-example--video-player-app) |

## See Also

- [Video Playback](../VIDEO_PLAYBACK.md) — the complete guide
- [ImageControl](ImageControl.md) — still images, same rendering backends
- [CanvasControl](CanvasControl.md) — draw your own frames
- [TerminalControl](TerminalControl.md) — embed a PTY-backed shell
- [Controls Reference](../CONTROLS.md) — all controls

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)
