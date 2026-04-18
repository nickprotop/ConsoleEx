# Driver / Compositor Separation Design

**Date:** 2026-03-21
**Status:** Approved for implementation
**Scope:** SharpConsoleUI — internal restructuring only; zero public API changes

---

## Goal

The SharpConsoleUI compositor (window management, layout, controls) should produce a composed cell buffer and hand it to a driver. The driver decides how to render it. No ANSI, no terminal concepts, no Console.Write anywhere outside the driver layer.

This enables future output surfaces (WinForms GDI+, Direct2D, browser, remote) to be added by implementing one interface, without touching the compositor.

---

## Current State (what was found)

| Component | Location | Problem |
|-----------|----------|---------|
| `ConsoleBuffer` | `Drivers/` | Shared by both drivers; stores ANSI-baked strings in internal `Cell` struct; contains `FormatCellAnsi()`, `WriteOutput()` — terminal coupling inside shared code |
| `HeadlessConsoleDriver` | `Drivers/` | Owns a `ConsoleBuffer`; calling `Flush()` emits ANSI to `Console.Out` (since `TerminalRawMode.IsRawModeActive` is false in headless) |
| `Layout.Cell` | `Layout/` | Has raw `Color Foreground`, `Color Background`, `Decorations` — correct model, but color data is discarded when copied into `ConsoleBuffer` |
| `CharacterBuffer` | `Layout/` | Window-level buffer storing `Layout.Cell[,]` with full color data — correct, unchanged |
| `ConsoleCell` | `Diagnostics/Snapshots/` | Has `AnsiEscape` string field; populated from `ConsoleBuffer`'s ANSI-baked internal cell — must be updated to carry raw `Color` fields after the refactor |

**Input is already separated.** `AnsiInputParser` / `UnixStdinReader` live entirely in the driver and communicate via events. No change needed there.

---

## Design

### New component: `CellScreenBuffer` (`Drivers/CellScreenBuffer.cs`)

Replaces `ConsoleBuffer`. A pure screen-level double buffer with no ANSI and no terminal output.

**Stores:** `Layout.Cell[,]` directly — raw `Color Foreground`, `Color Background`, `Decorations`, `Character`, `IsWideContinuation`, `Combiners`. No pre-baked ANSI strings.

**Exposes:**
- `SetNarrowCell(x, y, character, fg, bg)` — same signature as today
- `FillCells(x, y, width, character, fg, bg)` — same signature as today
- `SetCellsFromBuffer(destX, destY, source, srcX, srcY, width, fallbackBg)` — same signature as today; copies `Layout.Cell` values directly (no `FormatCellAnsi` call)
- `Clear()` — resets back buffer
- `GetDirtyCharacterCount() → int`
- `GetSnapshot() → Layout.Cell[,]` — deep copy of the back buffer (the composed frame ready to render on the next `Flush()`; not necessarily what the terminal currently shows)
- `Lock` property — unchanged
- Wide char orphan-pair cleanup stays here (data integrity, not rendering)

**Does NOT contain:** `FormatCellAnsi`, `Render`, `WriteOutput`, any `StringBuilder` accumulation, any reference to `TerminalRawMode`.

---

### New component: `AnsiRenderer` (`Drivers/AnsiRenderer.cs`)

Internal class. Owned exclusively by `NetConsoleDriver`. Never referenced by `HeadlessConsoleDriver` or the compositor.

**Owns:** The front buffer (`Layout.Cell[,]`) — what the terminal currently displays. Moves here from `ConsoleBuffer`.

**Constructor:**
```csharp
// Production — writes to terminal via TerminalRawMode.WriteStdout
internal AnsiRenderer(ConsoleWindowSystemOptions options, object consoleLock)

// Capture mode — same ANSI code, output lands in the injected sink instead of the terminal
internal AnsiRenderer(ConsoleWindowSystemOptions options, object consoleLock,
                      Action<StringBuilder> outputSink)
```

Default output sink writes via `TerminalRawMode.WriteStdout` (or `Console.Out` fallback). When a custom `Action<StringBuilder>` is injected it is used instead — same ANSI serialization code path, different destination.

**Entry point:** `void Render(CellScreenBuffer buffer)` — called by `NetConsoleDriver.Flush()`.

**Contains (moved from `ConsoleBuffer`):**
- `FormatCellAnsi()` with last-cell caching
- The dirty-tracking render loop (cell / line / smart modes)
- Wide char dirty coherence pre-pass
- `AppendLineToBuilder`, `AppendRegionToBuilder`, `GetDirtyRegionsInLine`, `AnalyzeLine`
- `WriteOutput` (now calls the injected sink)
- All diagnostics capture: `CaptureConsoleOutput`, `RecordMetrics`, and `CaptureConsoleBufferSnapshot` — moved here because this is the only place with simultaneous access to both the back buffer (from `CellScreenBuffer`) and the front buffer (owned by `AnsiRenderer`). See Diagnostics section below.

After rendering, `Render()` syncs front buffer ← back buffer (the flip step).

---

### Changes to `NetConsoleDriver`

- `_consoleBuffer: ConsoleBuffer` → `_cellBuffer: CellScreenBuffer` + `_ansiRenderer: AnsiRenderer`
- `Start()` creates `_cellBuffer` and `_ansiRenderer` (passing `consoleLock`, `options`)
- `Flush()` → `_ansiRenderer.Render(_cellBuffer)`
- `SetNarrowCell`, `FillCells`, `WriteBufferRegion` → delegate to `_cellBuffer`
- `GetDirtyCharacterCount()` → delegates to `_cellBuffer`
- `ScreenBuffer` property → returns `_cellBuffer`
- Direct-mode ANSI (`RenderMode.Direct` path) stays in `NetConsoleDriver.cs` as today
- All Win32 P/Invoke, raw mode, input loops, resize loop — unchanged

**Capture-mode constructor (for tests):**
```csharp
public NetConsoleDriver(RenderMode renderMode, Action<StringBuilder> outputSink)
```

The existing primary constructor (`NetConsoleDriver(NetConsoleDriverOptions)`) runs Win32 `GetConsoleMode`/`SetConsoleMode` and Unix `SuppressUnixSignal` unconditionally in its body. The capture-mode constructor must avoid these calls.

Implementation: a `private readonly bool _captureMode` field. The capture-mode constructor sets `_captureMode = true` before chaining. At the top of the constructor body, a guard skips all P/Invoke when `_captureMode` is true:
```csharp
if (_captureMode) { _captureOutputSink = outputSink; return; }
// ... existing Win32/Unix init follows
```

`Start()` likewise checks `_captureMode`: when true, it creates `_cellBuffer` and `_ansiRenderer` (with the injected sink) and returns immediately — no raw mode, no input loop tasks, no resize loop, no alternate screen buffer.

This makes `NetConsoleDriver` safe to instantiate in CI environments with no console handle.

---

### Changes to `HeadlessConsoleDriver`

- `_consoleBuffer: ConsoleBuffer` → `_cellBuffer: CellScreenBuffer`
- `Initialize()` creates `_cellBuffer` — no `AnsiRenderer`, no ANSI ever
- `Flush()` → no-op (was incorrectly emitting ANSI to `Console.Out` — this bug is fixed)
- `SetNarrowCell`, `FillCells`, `WriteBufferRegion` → delegate to `_cellBuffer`
- `GetDirtyCharacterCount()` → delegates to `_cellBuffer`
- `ScreenBuffer` property → returns `_cellBuffer`

---

### `IConsoleDriver` — one addition

```csharp
/// <summary>
/// The screen-level cell buffer for this driver.
/// Returns null when using RenderMode.Direct (no buffer is maintained).
/// Callers that depend on this property should check for null.
/// ConsoleWindowSystem.GetLastFrame() and ExportSvg() silently no-op when null.
/// </summary>
CellScreenBuffer? ScreenBuffer { get; }
```

All other `IConsoleDriver` members are unchanged.

---

### Capture and export — implemented above the driver

`GetLastFrame()` and `ExportSvg()` are NOT driver responsibilities. The driver is a cell sink. The composed screen lives in `CellScreenBuffer`. Consumers read it from above.

**`CellScreenBuffer.GetSnapshot() → Layout.Cell[,]`** — deep copy of back buffer. Represents the composed frame as last written by the compositor (ready to render on next `Flush()`).

**`SvgExporter`** (`Drivers/SvgExporter.cs`) — static utility, no driver dependency:
```csharp
public static class SvgExporter
{
    public static string ToSvg(Layout.Cell[,] frame,
                               int cellWidth = 10, int cellHeight = 20,
                               string fontFamily = "'Cascadia Code', monospace");
    public static void ExportSvg(Layout.Cell[,] frame, string path,
                                 int cellWidth = 10, int cellHeight = 20,
                                 string fontFamily = "'Cascadia Code', monospace");
}
```
For each cell: `<rect fill="background"/>` + `<text fill="foreground">character</text>`.
SVG root has explicit `width`/`height`. Wide continuation cells are skipped.

**`ConsoleWindowSystem`** gains two convenience methods:
```csharp
/// <summary>
/// Returns a snapshot of the last composed screen frame, or null if the driver
/// uses RenderMode.Direct (no buffer is maintained).
/// </summary>
public Layout.Cell[,]? GetLastFrame()
    => _consoleDriver.ScreenBuffer?.GetSnapshot();

/// <summary>
/// Exports the last composed screen frame to an SVG file.
/// Does nothing if the driver uses RenderMode.Direct.
/// </summary>
public void ExportSvg(string path)
{
    var frame = GetLastFrame();
    if (frame != null) SvgExporter.ExportSvg(frame, path);
}
```

Tests call `system.GetLastFrame()` — no driver cast needed.

---

### Diagnostics migration

`ConsoleBuffer` currently contains three diagnostics capture points:
- `CaptureConsoleOutput(string output)` — captures ANSI output string → `AnsiLinesSnapshot`
- `RecordMetrics(RenderingMetrics)` — captures frame metrics
- `CaptureConsoleBufferSnapshot()` — captures front+back buffer state → `ConsoleBufferSnapshot`

All three move into `AnsiRenderer.Render()`, which is the only place with simultaneous access to the back buffer (from `CellScreenBuffer`) and the front buffer (owned by `AnsiRenderer`). The diagnostics `Diagnostics` property is set on `AnsiRenderer` instead of (formerly) `ConsoleBuffer`.

**`ConsoleCell` is extended** — it keeps `AnsiEscape: string` AND gains `Foreground: Color`, `Background: Color`, `Decorations: TextDecoration`. No fields are removed. `ConsoleBufferSnapshot` struct is unchanged.

`CaptureConsoleBufferSnapshot()` (now in `AnsiRenderer`) populates all fields. It has simultaneous access to `CellScreenBuffer`'s back buffer (raw `Layout.Cell` with colors) and its own front buffer (also raw `Layout.Cell`). For each cell:
- `Character` from `Layout.Cell.Character`
- `Foreground`, `Background`, `Decorations` from `Layout.Cell` directly
- `AnsiEscape` by calling `FormatCellAnsi(cell.Foreground, cell.Background, cell.Decorations)` — the same method used during rendering

This means all existing tests that assert on `cell.AnsiEscape` (`HeadlessConsoleDriverTests`, `DecorationRenderingPipelineTests`, etc.) continue to pass **unchanged**. `DecorationRenderingPipelineTests` in particular relies on parsing `AnsiEscape` for SGR decoration codes (underline, bold, italic, reset) — these assertions remain valid and are the correct way to test ANSI decoration correctness.

`RenderOutputSnapshot` is populated from the `_screenBuilder` content before writing — this was already inside `ConsoleBuffer.Render()` and moves to `AnsiRenderer.Render()` unchanged.

---

### Test infrastructure migration

`MockConsoleDriver` currently subclasses `HeadlessConsoleDriver`. After the refactor it subclasses `NetConsoleDriver` in capture mode:

```csharp
public class MockConsoleDriver : NetConsoleDriver
{
    public MockConsoleDriver() : base(RenderMode.Buffer, sb => { }) { }
    public MockConsoleDriver(int width, int height) : base(RenderMode.Buffer, sb => { }) { }
}
```

The `outputSink` lambda is a discard (`sb => { }`) because tests never inspect raw ANSI bytes directly — they use `RenderingDiagnostics.LastAnsiSnapshot` (captured inside `AnsiRenderer.Render()` unconditionally) and `RenderingDiagnostics.LastConsoleSnapshot`. The output sink and the diagnostics capture are two completely independent mechanisms on `AnsiRenderer`:

- **`outputSink: Action<StringBuilder>`** — called in place of `TerminalRawMode.WriteStdout`. Controls where rendered ANSI bytes go (terminal in production, discarded in tests). Has no effect on diagnostics.
- **`Diagnostics: RenderingDiagnostics`** — set via property (same path as today, assigned by `ConsoleWindowSystem` after initialization). `AnsiRenderer.Render()` calls `_diagnostics?.CaptureConsoleOutput(...)`, `_diagnostics?.RecordMetrics(...)`, `_diagnostics?.CaptureConsoleBufferSnapshot(...)` unconditionally when diagnostics are enabled. Has no relationship to the output sink.

These two injection points do not interact. Setting `outputSink` to a discard lambda does not disable diagnostics.

All tests using `TestWindowSystemBuilder.CreateTestSystem()` automatically get the migrated `MockConsoleDriver`. Existing test assertions that use `RenderingDiagnostics.LastAnsiSnapshot`, `metrics.BytesWritten`, etc. continue to work unchanged because diagnostics capture now happens inside `AnsiRenderer.Render()`.

---

## What does NOT change

- `IConsoleDriver` public interface — one addition (`ScreenBuffer`) only
- `ConsoleWindowSystem`, `Renderer`, `RenderCoordinator`, `BorderRenderer` — zero changes
- `Layout.Cell` struct — zero changes
- `Layout.CharacterBuffer` — zero changes
- All controls, layout, markup, focus, input event model — zero changes
- `AnsiInputParser`, `UnixStdinReader`, `TerminalRawMode` — zero changes
- All P/Invoke, Win32 console mode, raw mode setup — stays in `NetConsoleDriver`
- Existing applications (`DemoApp`, `LazyNuGet`, etc.) — compile and run unchanged

---

## Risks

1. **`SetCellsFromBuffer()` no longer calls `FormatCellAnsi` at write time.** ANSI is generated at render time in `AnsiRenderer`. Same number of calls total (once per dirty cell per frame), timing shifts from write to flush. No observable behavior difference.

2. **Dirty comparison changes from ANSI string equality to `Layout.Cell` field equality.** Structurally equivalent — same cells are considered dirty. Slightly more precise in edge cases (two `Color` values that happened to produce the same ANSI string are now correctly treated as distinct).

3. **`ConsoleCell` public type extends** — `Foreground: Color`, `Background: Color`, `Decorations: TextDecoration` are added alongside the existing `AnsiEscape: string`. No fields removed. Existing test assertions on `AnsiEscape` continue to compile and pass. `AnsiEscape` is now populated by `AnsiRenderer.CaptureConsoleBufferSnapshot()` via `FormatCellAnsi` rather than read from a pre-baked buffer field — semantically identical output.

4. **`MockConsoleDriver` changes base type.** Tests that explicitly reference `MockConsoleDriver` as a `HeadlessConsoleDriver` subclass need the cast updated. Tests referencing `IConsoleDriver` are unaffected.

5. **`ConsoleBuffer` is currently `public`.** Only instantiated inside the two drivers (confirmed by search). Renaming to `CellScreenBuffer` does not affect the public NuGet surface. Safe.

6. **`NetConsoleDriver` capture-mode constructor must not call Win32/Unix terminal APIs.** The `_captureMode` sentinel guards the constructor body and `Start()`. Verified safe for CI environments with no console handle.

---

## New file layout

```
Drivers/
  IConsoleDriver.cs           +ScreenBuffer property
  CellScreenBuffer.cs         replaces ConsoleBuffer.cs (pure cell buffer, Layout.Cell[,])
  AnsiRenderer.cs             new — ANSI serialization + diagnostics capture, owned by NetConsoleDriver
  SvgExporter.cs              new — static SVG utility, no driver dependency
  NetConsoleDriver.cs         uses CellScreenBuffer + AnsiRenderer; +capture-mode constructor
  HeadlessConsoleDriver.cs    uses CellScreenBuffer only; no AnsiRenderer ever
  NetConsoleDriverOptions.cs  unchanged
  MouseFlags.cs               unchanged
  Input/...                   unchanged

Diagnostics/Snapshots/
  ConsoleBufferSnapshot.cs    ConsoleCell: +Foreground/Background (Color) +Decorations (TextDecoration) alongside existing AnsiEscape
```
