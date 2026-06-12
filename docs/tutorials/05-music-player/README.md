# TermTunes — source for Tutorial 5

The complete, runnable source for [Tutorial 5: Now Playing — a Terminal Music Player](../05-music-player.md).
Every file here is reproduced verbatim in that tutorial. Playback is **simulated** (no audio, no FFT) —
the focus is the rich UI: an animated spectrum visualizer, a canvas-drawn gradient seek bar, album art,
and per-track accent theming.

## Run

```bash
cd TermTunes
dotnet run
```

Keys: `↑↓` pick track · `Enter`/click plays · `space` play/pause · `⏮ ⏯ ⏭` transport · `q` quit.

## Test

```bash
dotnet test TermTunes.Tests/TermTunes.Tests.csproj
```

## Regenerate cover art

```bash
python3 TermTunes/assets/gen_covers.py   # needs Pillow
```

## Layout

| Path | Responsibility |
|---|---|
| `TermTunes/Program.cs` | Bootstrap: `ConsoleWindowSystem` + the player window |
| `TermTunes/Models/Track.cs` | Plain track data (title/artist/album/duration/cover/accent) |
| `TermTunes/ViewModels/` | `ViewModelBase`, `TrackViewModel`, and `PlayerViewModel` (testable `Tick`) |
| `TermTunes/UI/PlayerWindow.cs` | Two-pane layout + the ~14 fps async frame loop |
| `TermTunes/UI/Visualizer.cs` | `CanvasControl` spectrum renderer |
| `TermTunes/UI/SeekBar.cs` | `CanvasControl` gradient progress renderer |
| `TermTunes/UI/ColorScheme.cs` | Palette + per-accent gradient ramp |
| `TermTunes/Data/SamplePlaylist.cs` | In-memory playlist |
| `TermTunes/assets/` | Cover generator + generated PNGs |
| `TermTunes.Tests/` | xUnit tests for the player view model |

Targets `net10.0`, references the in-repo `SharpConsoleUI` (with a NuGet fallback so it still builds
if you copy this folder out of the repository).
