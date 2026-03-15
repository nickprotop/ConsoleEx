# SharpConsoleUI Rendering Pipeline

## Overview

SharpConsoleUI uses a sophisticated multi-stage rendering pipeline optimized for console applications. The architecture employs **double buffering at two levels** (window and screen), **dirty tracking at three levels** (window, cell, and line), and **occlusion culling** to minimize unnecessary rendering work.

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                        │
│  • Window creation and updates                              │
│  • Control invalidation (AddControl, SetTitle, etc.)        │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              ConsoleWindowSystem (Coordinator)              │
│  • Event loop (Run)                                         │
│  • Frame limiting and dirty checking                        │
│  • UpdateDisplay() orchestration                            │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                  Renderer (Multi-Pass)                      │
│  • Build render list (Z-order, occlusion)                   │
│  • Three passes: Normal → Active → AlwaysOnTop              │
│  • Per-window visible regions calculation                   │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│            Window Content Rendering (DOM-based)             │
│  • Measure → Arrange → Paint (layout stages)                │
│  • CharacterBuffer (window-level buffer)                    │
│  • Pre/PostBufferPaint hooks (compositor effects)           │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼ (Direct Cell Path — no ANSI strings)
┌─────────────────────────────────────────────────────────────┐
│              Console Driver (Screen Buffer)                 │
│  • ConsoleBuffer (screen-level double buffer)               │
│  • Direct cell copy from CharacterBuffer                    │
│  • Diff-based rendering (Cell/Line/Smart modes)             │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    Physical Console                         │
│  • Raw libc write (Unix) / Console.Write (Windows)          │
│  • ANSI codes generated once during final output            │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Responsibility | Location |
|-----------|---------------|----------|
| **ConsoleWindowSystem** | Main event loop, rendering coordinator | `ConsoleWindowSystem.cs` |
| **Renderer** | Multi-pass rendering, occlusion culling | `Renderer.cs` |
| **VisibleRegions** | Rectangle subtraction, overlap detection | `VisibleRegions.cs` |
| **Window** | Content rendering, DOM rebuilding | `Window.cs` |
| **CharacterBuffer** | Window-level cell buffer, DOM paint target | `Layout/CharacterBuffer.cs` |
| **ConsoleBuffer** | Screen-level buffer, direct cell copy, diff-based rendering | `Drivers/ConsoleBuffer.cs` |
| **NetConsoleDriver** | Console abstraction, I/O handling | `Drivers/NetConsoleDriver.cs` |

---

## 1. Entry Points

### Main Event Loop: `ConsoleWindowSystem.Run()`
*File: `ConsoleWindowSystem.cs:822-975`*

The primary entry point for application rendering. Runs continuously until the application exits.

```csharp
public void Run()
{
    while (!_shouldQuit)
    {
        ProcessOnce(); // Single frame processing

        // Frame limiting (default: ~60 FPS)
        int sleepTime = Math.Max(1, FrameDelayMilliseconds - (int)_stopwatch.ElapsedMilliseconds);
        if (sleepTime > 0)
            Thread.Sleep(sleepTime);

        _stopwatch.Restart();
    }
}
```

**Key behaviors:**
- Infinite loop until `_shouldQuit` flag set
- Calls `ProcessOnce()` for each frame
- Frame rate limiting via `FrameDelayMilliseconds` (default: 16ms ≈ 60 FPS)
- Precise timing using `Stopwatch`

### Single Frame Processing: `ProcessOnce()`
*File: `ConsoleWindowSystem.cs:977-1002`*

Handles input, processes events, and conditionally renders.

```csharp
public void ProcessOnce()
{
    ProcessInput();           // Handle keyboard/mouse input
    ProcessPendingEvents();   // Execute queued events

    // Only render if something changed
    if (_needsFullRedraw || _windows.Any(w => w.NeedsRedraw))
    {
        UpdateDisplay();
        _needsFullRedraw = false;
    }
}
```

**Optimization: Dirty Checking**
- Rendering only occurs when `_needsFullRedraw` or any window has `NeedsRedraw` flag set
- Avoids wasting CPU cycles when UI is static
- Typical frame: ~99% of frames skip rendering in idle applications

### Manual Rendering: `UpdateDisplay()`
*File: `ConsoleWindowSystem.cs:2522-2766`*

The central rendering coordinator. Can also be called manually for immediate updates.

---

## 2. Core Rendering Coordinator: `UpdateDisplay()`

*File: `ConsoleWindowSystem.cs:2522-2766`*

The heart of the rendering pipeline, orchestrating all rendering phases.

### The Five Phases

```csharp
private void UpdateDisplay()
{
    lock (_renderLock)
    {
        var startTime = DateTime.UtcNow;

        // PHASE 1: Atomic desktop clearing
        if (_needsDesktopClear)
        {
            _driver.ClearScreen();
            _needsDesktopClear = false;
        }

        // PHASE 2: Build render list (occlusion culling)
        var renderList = BuildRenderList();

        // PHASE 3: Render passes (Normal → Active → AlwaysOnTop)
        foreach (var pass in new[] { WindowPass.Normal,
                                     WindowPass.Active,
                                     WindowPass.AlwaysOnTop })
        {
            var windowsInPass = renderList.Where(w => GetWindowPass(w) == pass);
            foreach (var window in windowsInPass)
            {
                _renderer.RenderWindow(window, renderList);
            }
        }

        // PHASE 4: Flush to screen
        _driver.Render();

        // PHASE 5: Status bar rendering
        if (_statusBar != null)
        {
            RenderStatusBar(DateTime.UtcNow - startTime);
        }
    }
}
```

### Phase Breakdown

#### Phase 1: Atomic Desktop Clearing
- Clears entire screen when requested (window resize, theme change)
- Sets `_needsDesktopClear` flag on major layout changes
- **Atomic operation**: No partial clearing

#### Phase 2: Build Render List
*File: `ConsoleWindowSystem.cs:2634-2670`*

Determines which windows to render and in what order:

```csharp
private List<Window> BuildRenderList()
{
    return _windows
        .Where(w => w.Visible && !w.IsMinimized)
        .OrderBy(w => w.ZOrder)  // Back-to-front ordering
        .ToList();
}
```

**Z-Order management:**
- Lower `ZOrder` = rendered first (background)
- Higher `ZOrder` = rendered last (foreground)
- Modal windows automatically get highest Z-order

#### Phase 3: Three-Pass Rendering

| Pass | Windows Rendered | Purpose |
|------|------------------|---------|
| **Normal** | Standard windows | Base UI layer |
| **Active** | Focused/Active window | Ensure focus visible |
| **AlwaysOnTop** | Notifications, tooltips | UI chrome that must be visible |

This ensures correct visual stacking regardless of Z-order values.

Within each window, [portal overlays](PORTAL_SYSTEM.md) (dropdowns, context menus, tooltips) render after all normal controls, appearing on top of the window's content.

#### Phase 4: Screen Flush
- Calls `_driver.Render()` to commit all buffered changes
- **Single flush per frame**: All windows rendered before screen update
- Diff-based rendering minimizes console I/O

#### Phase 5: Status Bar
- Rendered last, overlays all content
- Shows FPS, dirty characters, memory usage
- Optional, controlled by `ShowPerformanceMetrics` property

---

## 3. Window Rendering Pipeline

### Entry: `Renderer.RenderWindow()`
*File: `Renderer.cs:211-270`*

Renders a single window with occlusion culling.

```csharp
public void RenderWindow(Window window, List<Window> allWindows)
{
    // Calculate visible regions (occlusion culling)
    var visibleRegions = VisibleRegions.CalculateVisibleRegions(
        window.Bounds,
        allWindows.Where(w => w.ZOrder > window.ZOrder).Select(w => w.Bounds)
    );

    if (visibleRegions.Count == 0)
        return; // Completely occluded, skip rendering

    // Render background fill
    foreach (var region in visibleRegions)
    {
        FillRegion(region, window.BackgroundColor);
    }

    // Render border (if present)
    if (window.HasBorder)
    {
        RenderBorder(window, visibleRegions);
    }

    // Render window content (direct cell path)
    var buffer = window.EnsureContentReady(visibleRegions);
    if (buffer != null)
    {
        RenderVisibleWindowContentFromBuffer(window, buffer, visibleRegions);
    }
}
```

> **Note:** The direct cell path bypasses ANSI serialization entirely. `EnsureContentReady()` runs the DOM layout pipeline (Measure → Arrange → Paint) to populate the window's `CharacterBuffer`. Then `RenderVisibleWindowContentFromBuffer()` copies cells directly from `CharacterBuffer` to `ConsoleBuffer` using `WriteBufferRegion()`, avoiding the previous serialize-to-ANSI → clip-with-regex → parse-ANSI round-trip.

### Visible Regions Calculation
*File: `VisibleRegions.cs:40-158`*

The most complex optimization: determining which parts of a window are actually visible.

**Algorithm: Rectangle Subtraction**

```
Original Window Bounds:
┌─────────────────┐
│                 │
│                 │
│                 │
└─────────────────┘

Overlapping Window (higher Z-order):
        ┌─────────┐
        │ Overlap │
        └─────────┘

Result (visible regions):
┌───────┐         ┌──┐
│  R1   │         │R2│
├───────┴─────────┤  │
│       R3        │  │
└─────────────────┴──┘
```

**Implementation highlights:**
- **Dual-buffer pooling**: Reuses `List<LayoutRect>` to avoid allocations
- **Early exit**: Returns empty list if completely occluded
- **Iterative subtraction**: Each overlapping window subtracts from remaining regions

```csharp
public static List<LayoutRect> CalculateVisibleRegions(
    LayoutRect targetBounds,
    IEnumerable<LayoutRect> overlappingBounds)
{
    var visible = new List<LayoutRect> { targetBounds };

    foreach (var overlap in overlappingBounds)
    {
        var temp = new List<LayoutRect>();

        foreach (var region in visible)
        {
            if (!region.IntersectsWith(overlap))
            {
                temp.Add(region); // No overlap, keep as-is
            }
            else
            {
                // Subtract overlap, potentially creating 4 new rectangles
                temp.AddRange(SubtractRectangle(region, overlap));
            }
        }

        visible = temp;
        if (visible.Count == 0)
            break; // Completely occluded
    }

    return visible;
}
```

**Performance impact:**
- **Worst case**: Window completely visible = 1 region
- **Best case**: Window completely occluded = 0 regions (skip rendering)
- **Typical case**: 2-5 regions per partially occluded window

---

## 4. Window Content Rendering (DOM-Based)

### Entry: `Window.EnsureContentReady()`
*File: `Window.Rendering.cs`*

The single entry point for all window content rendering. Rebuilds the window's `CharacterBuffer` via the DOM pipeline without ANSI serialization. Used by the Renderer for both normal windows and overlay windows.

```csharp
internal CharacterBuffer? EnsureContentReady(List<Rectangle>? visibleRegions = null)
{
    if (_state == WindowState.Minimized) return null;
    lock (_lock)
    {
        if (_invalidated)
        {
            var availableWidth = Width - 2;
            var availableHeight = Height - 2;
            RebuildContentBufferOnly(availableWidth, availableHeight, visibleRegions);
            bool isInRenderingPipeline = visibleRegions != null && visibleRegions.Count > 0;
            if (!isInRenderingPipeline) _invalidated = true;
        }
        return _renderer?.Buffer;
    }
}
```

A convenience wrapper `RenderAndGetVisibleContent()` exists for test code and diagnostics — it calls `EnsureContentReady()` internally then serializes the buffer to ANSI strings via `buffer.ToLines()`. No separate code path exists; the buffer is always the source of truth.

### The Three-Stage Layout Pipeline
*File: `Window.cs:2500-2803`*

DOM-based rendering follows the classic layout model:

#### Stage 1: Measure
*File: `Window.cs:2550-2600`*

Each control calculates its desired size given available space.

```csharp
private void MeasureStage(LayoutRect availableSpace)
{
    foreach (var control in _controls)
    {
        var desiredSize = control.Measure(availableSpace.Width, availableSpace.Height);
        control._measuredSize = desiredSize;
    }
}
```

**Considerations:**
- Text controls measure string lengths (with ANSI stripping)
- Container controls recursively measure children
- Scrollable controls report virtual size vs viewport size

#### Stage 2: Arrange
*File: `Window.cs:2602-2670`*

Assigns final positions and sizes to controls based on layout rules.

```csharp
private void ArrangeStage(LayoutRect finalBounds)
{
    int currentY = finalBounds.Y;

    foreach (var control in _controls)
    {
        var controlBounds = new LayoutRect(
            finalBounds.X,
            currentY,
            finalBounds.Width,
            control._measuredSize.Height
        );

        control.Arrange(controlBounds);
        currentY += control._measuredSize.Height;
    }
}
```

**Layout strategies:**
- **Stack layout**: Controls stacked vertically (default)
- **Column layout**: Side-by-side using `ColumnContainer`
- **Absolute positioning**: Fixed X/Y coordinates
- **Fill**: Control expands to fill available space

#### Stage 3: Paint
*File: `Window.cs:2672-2803`*

Controls draw themselves into the window's `CharacterBuffer`.

```csharp
private void PaintStage(CharacterBuffer buffer)
{
    foreach (var control in _controls)
    {
        var controlBounds = control.ArrangedBounds;
        control.PaintDOM(buffer, controlBounds, _clipRect);
    }
}
```

**CharacterBuffer key methods:**
```csharp
// Full Cell (preserves flags: IsWideContinuation, Combiners, Decorations)
// Use for parsed markup cells and cell copies between buffers
void SetCell(int x, int y, Cell cell);

// Narrow character (clears all flags, assumes width-1)
// Use for literal characters: borders, padding, fill chars, track chars
void SetNarrowCell(int x, int y, char character, Color fg, Color bg);
void SetNarrowCell(int x, int y, Rune character, Color fg, Color bg);

// Text rendering with automatic wide character and combiner handling
void WriteString(int x, int y, string text, Color fg, Color bg);

// Bulk fill
void FillRect(LayoutRect rect, char c, Color fg, Color bg);
```

> **Note:** The old `SetCell(x, y, Rune, Color, Color)` overload was renamed to `SetNarrowCell`
> to make misuse a compile error. Use `SetCell(Cell)` for parsed/copied cells, `SetNarrowCell`
> for literal narrow characters.

#### Stage 3.5: Pre/Post Buffer Paint Hooks (Compositor Effects)
*File: `Windows/WindowRenderer.cs`*

Two compositor-style hooks allow buffer manipulation at different points in the rendering pipeline:

```csharp
public bool RebuildContentBuffer(...)
{
    // Stage 1-2: Measure, Arrange
    RebuildDOMTree();
    PerformDOMLayout();

    // Clear buffer with background color
    _buffer.Clear(backgroundColor);

    // ◄── PRE-PAINT HOOK POINT (backgrounds, fractals)
    if (PreBufferPaint != null && _buffer != null)
    {
        var dirtyRegion = new LayoutRect(0, 0, _buffer.Width, _buffer.Height);
        PreBufferPaint.Invoke(_buffer, dirtyRegion, clipRect);
    }

    // Stage 3: Paint controls ON TOP of pre-paint content
    PaintDOMWithoutClear(clipRect);

    // ◄── POST-PAINT HOOK POINT (effects, overlays)
    if (PostBufferPaint != null && _buffer != null)
    {
        var dirtyRegion = new LayoutRect(0, 0, _buffer.Width, _buffer.Height);
        PostBufferPaint.Invoke(_buffer, dirtyRegion, clipRect);
    }

    // Buffer is now ready — cells flow directly to ConsoleBuffer
    // (ToLines() is only called when AnsiLines diagnostic layer is enabled)
    return true;
}
```

**PreBufferPaint** - Fires after buffer clear, before controls paint:
- **Custom backgrounds**: Animated patterns, gradients
- **Full-buffer graphics**: Fractals, visualizations
- Controls render ON TOP of pre-paint content

**PostBufferPaint** - Fires after controls paint, before buffer is consumed by the driver:
- **Transitions**: Fade in/out, slide, wipe effects
- **Filters**: Blur, desaturate, brightness adjustments
- **Overlays**: Glow effects, highlights, custom decorations
- **Capture**: Screenshots, recording via `BufferSnapshot`

**API:**
```csharp
// Pre-paint: custom backgrounds (controls render on top)
window.Renderer.PreBufferPaint += (buffer, dirtyRegion, clipRect) =>
{
    // Draw animated fractal or pattern
    RenderFractal(buffer);
};

// Post-paint: effects applied to final content
window.Renderer.PostBufferPaint += (buffer, dirtyRegion, clipRect) =>
{
    // Apply fade or blur effect
    ApplyFadeEffect(buffer, _fadeProgress);
};
```

**Thread Safety**: Both events fire within existing `_renderLock`, ensuring safe buffer manipulation.

**Performance**: Zero overhead when events not subscribed. Only process dirty regions for optimal performance.

See [Compositor Effects](COMPOSITOR_EFFECTS.md) for comprehensive examples and best practices.

---

## 5. Character Buffer System

*File: `Layout/CharacterBuffer.cs`*

Window-level buffer storing character, foreground, and background color for each cell.

### Buffer Structure

```csharp
public class CharacterBuffer
{
    private Cell[,] _cells;  // 2D array [col, row]
    private int _width;
    private int _height;
}

public struct Cell : IEquatable<Cell>
{
    public Rune Character;          // Unicode scalar value (supports emoji, CJK)
    public Color Foreground;
    public Color Background;
    public TextDecoration Decorations;
    public bool Dirty;              // Changed since last render
    public bool IsWideContinuation; // Right half of a wide character
    public string? Combiners;       // Zero-width combining marks (e.g., diacritics)
}
```

**Key changes from the original `char`-based Cell:**
- `Rune` replaces `char` — a `System.Text.Rune` represents a full Unicode scalar value, enabling correct handling of emoji and characters above U+FFFF (which require surrogate pairs as `char`)
- `IsWideContinuation` marks the right-half placeholder cell for CJK/emoji characters that occupy 2 terminal columns
- `Combiners` stores zero-width combining marks (diacritics, variation selectors) attached to the base character
- `TextDecoration` supports underline, strikethrough, and other text effects

**Memory layout example (3x2 buffer):**
```
     Col 0           Col 1           Col 2
   ┌───────────┐   ┌───────────┐   ┌───────────┐
R0 │ 'H', W, B │   │ 'e', W, B │   │ 'l', W, B │
   └───────────┘   └───────────┘   └───────────┘
   ┌───────────┐   ┌───────────┐   ┌───────────┐
R1 │ 'l', W, B │   │ 'o', W, B │   │ ' ', W, B │
   └───────────┘   └───────────┘   └───────────┘

(W = White foreground, B = Black background)
```

### Key Operations

#### 1. SetCell / SetNarrowCell (Individual Cell Update)
*File: `CharacterBuffer.cs`*

Two write APIs with distinct purposes:

- **`SetCell(int x, int y, Cell cell)`** — preserves all flags (`IsWideContinuation`, `Combiners`, `Decorations`). Use for cells from `MarkupParser.Parse()` or when copying cells between buffers.
- **`SetNarrowCell(int x, int y, char/Rune, Color fg, Color bg)`** — clears all flags, assumes width-1. Use for literal narrow characters: border chars, padding spaces, fill chars.

> The old `SetCell(x, y, Rune, Color, Color)` overload was renamed to `SetNarrowCell` to make misuse a compile error.

```csharp
public void SetNarrowCell(int x, int y, Rune character, Color foreground, Color background)
{
    if (x < 0 || x >= Width || y < 0 || y >= Height)
        return;

    CleanupWideCharAt(x, y);  // Fix orphaned wide char pairs

    ref var cell = ref _cells[x, y];
    if (cell.Character != character ||
        !cell.Foreground.Equals(foreground) ||
        !cell.Background.Equals(background) ||
        cell.IsWideContinuation ||
        cell.Combiners != null)
    {
        cell.Character = character;
        cell.Foreground = foreground;
        cell.Background = background;
        cell.Decorations = TextDecoration.None;
        cell.IsWideContinuation = false;
        cell.Combiners = null;
        cell.Dirty = true;
    }
}
```

**Optimizations:**
- Dirty tracking: only marks cell dirty if value actually changes
- Wide character cleanup: when overwriting a cell that is part of a wide character pair, the orphaned partner cell is cleaned up automatically

#### 2. WriteString (Text Rendering with Wide Character Support)
*File: `CharacterBuffer.cs`*

The primary text rendering method. Handles wide characters and zero-width combiners automatically:

```csharp
public void WriteString(int x, int y, string text, Color foreground, Color background)
{
    int cx = x;
    foreach (var rune in text.EnumerateRunes())
    {
        int runeWidth = UnicodeWidth.GetRuneWidth(rune);

        if (runeWidth == 0)
        {
            // Zero-width: attach to previous cell as combiner
            // (skips past continuation cells to find the base cell)
        }
        else if (runeWidth == 2)
        {
            // Wide char: write base cell + continuation cell
            SetCell(cx, y, new Cell(rune, foreground, background));
            SetCell(cx + 1, y, continuation with IsWideContinuation = true);
            cx += 2;
        }
        else
        {
            SetCell(cx, y, new Cell(rune, foreground, background));
            cx++;
        }
    }
}
```

**Wide character handling:**
- Uses `UnicodeWidth.GetRuneWidth()` (backed by Wcwidth library) to determine display width
- Width 0: zero-width combiners attached to the preceding base cell via `AppendCombiner()`
- Width 2: CJK/emoji characters write a base cell + a continuation cell marked `IsWideContinuation = true`
- Width 1: standard characters, one cell each
- If a wide char doesn't fit at the buffer edge, a space is written instead

#### 3. FillRect (Bulk Operations)
*File: `CharacterBuffer.cs`*

Used for backgrounds, borders, clearing regions. Delegates to `SetCell()` per cell.

#### 4. ToLines (ANSI Serialization — Diagnostic Only)
*File: `CharacterBuffer.cs`*

Converts buffer to ANSI-formatted strings. **Not called during rendering.** Cells flow directly from `CharacterBuffer` to `ConsoleBuffer` via `SetCellsFromBuffer()`. `ToLines()` is only invoked for:
- The `AnsiLines` diagnostic layer (test snapshots and debugging)
- The `RenderAndGetVisibleContent()` convenience API (used by tests to get string-based output)

```csharp
public List<string> ToLines()
{
    var lines = new List<string>();

    for (int y = 0; y < _height; y++)
    {
        var sb = new StringBuilder();
        Color? currentFg = null;
        Color? currentBg = null;

        for (int x = 0; x < _width; x++)
        {
            var cell = _buffer[y, x];

            // Optimization: Only emit ANSI codes when colors change
            if (cell.Foreground != currentFg)
            {
                sb.Append(AnsiCodes.Foreground(cell.Foreground));
                currentFg = cell.Foreground;
            }

            if (cell.Background != currentBg)
            {
                sb.Append(AnsiCodes.Background(cell.Background));
                currentBg = cell.Background;
            }

            sb.Append(cell.Character);
        }

        lines.Add(sb.ToString());
    }

    return lines;
}
```

**ANSI Optimization: Color Runs** (used in diagnostic and test output)
- Tracks current foreground/background colors
- Only emits ANSI escape codes when colors change
- Typical reduction: 80% fewer ANSI codes vs naive approach

**Example output:**
```
\x1b[37;40mHello \x1b[31mWorld\x1b[37m!
         ^^^^^^^^^^       ^^^^
         Set colors       Change FG only
```

---

## 6. Console Driver Layer

### Screen-Level Buffering: `ConsoleBuffer`
*File: `Drivers/ConsoleBuffer.cs`*

Second level of double buffering, operating at the screen level. ConsoleBuffer is a **pure cell-level buffer** — all writes go through cell-level methods, never ANSI string parsing.

```csharp
public class ConsoleBuffer
{
    private Cell[,] _frontBuffer;  // Currently displayed
    private Cell[,] _backBuffer;   // Being rendered
    private int _width;
    private int _height;

    private struct Cell
    {
        public Rune Character;          // Unicode scalar value
        public string AnsiEscape;       // Pre-formatted ANSI color string
        public bool IsWideContinuation; // Right half of wide character
        public string? Combiners;       // Zero-width combining marks
    }
}
```

### Three Write Methods (Unified Cell API)

All data enters ConsoleBuffer through exactly three cell-level methods:

#### 1. SetCell (Single Cell)
Used by borders (vertical, scrollbar), invisible borders. Accepts both `char` and `Rune`.
```csharp
public void SetCell(int x, int y, Rune character, Color fg, Color bg)
{
    // Fix wide char pair split: clean up orphaned base/continuation cells
    if (_backBuffer[x, y].IsWideContinuation && x > 0)
        _backBuffer[x - 1, y].Reset();  // Orphaned base
    if (x + 1 < _width && _backBuffer[x + 1, y].IsWideContinuation)
        _backBuffer[x + 1, y].Reset();  // Orphaned continuation

    string ansi = FormatCellAnsi(fg, bg);
    ref var cell = ref _backBuffer[x, y];
    if (cell.Character != character || cell.AnsiEscape != ansi
        || cell.IsWideContinuation || cell.Combiners != null)
    {
        cell.Character = character;
        cell.AnsiEscape = ansi;
        cell.IsWideContinuation = false;
        cell.Combiners = null;
    }
}
```

#### 2. FillCells (Horizontal Run)
Used by `Renderer.FillRect` (background fills), invisible border rows. Accepts both `char` and `Rune`.
```csharp
public void FillCells(int x, int y, int width, Rune character, Color fg, Color bg)
{
    // Fix wide char pair split at left and right boundaries
    if (x > 0 && _backBuffer[x, y].IsWideContinuation)
        _backBuffer[x - 1, y].Reset();
    int rightEdge = x + maxWidth;
    if (rightEdge < _width && _backBuffer[rightEdge, y].IsWideContinuation)
        _backBuffer[rightEdge, y].Reset();

    string ansi = FormatCellAnsi(fg, bg);
    for (int i = 0; i < maxWidth; i++)
    {
        ref var cell = ref _backBuffer[x + i, y];
        if (cell.Character != character || cell.AnsiEscape != ansi
            || cell.IsWideContinuation || cell.Combiners != null)
        {
            cell.Character = character;
            cell.AnsiEscape = ansi;
            cell.IsWideContinuation = false;
            cell.Combiners = null;
        }
    }
}
```

#### 3. SetCellsFromBuffer (Bulk Cell Copy from CharacterBuffer)
Used by window content, overlay content, border lines (top/bottom), status bars. Preserves wide character pairs and combiners from the source buffer.
```csharp
public void SetCellsFromBuffer(int destX, int destY, CharacterBuffer source,
    int srcX, int srcY, int width, Color fallbackBg)
{
    // Fix wide char pair split at left and right boundaries
    // (same orphan cleanup as FillCells)

    for (int i = 0; i < maxWidth; i++)
    {
        var srcCell = source.GetCell(srcX + i, srcY);
        string ansi = FormatCellAnsi(srcCell.Foreground, srcCell.Background);
        ref var destCell = ref _backBuffer[destX + i, destY];
        destCell.Character = srcCell.Character;
        destCell.AnsiEscape = ansi;
        destCell.IsWideContinuation = srcCell.IsWideContinuation;
        destCell.Combiners = srcCell.Combiners;
    }
}
```

**ANSI color cache** — exploits spatial locality (adjacent cells often share colors):
```csharp
private string FormatCellAnsi(Color fg, Color bg)
{
    if (fg.Equals(_lastCellFg) && bg.Equals(_lastCellBg))
        return _lastCellAnsi;  // Cache hit
    _lastCellAnsi = $"\x1b[38;2;{fg.R};{fg.G};{fg.B};48;2;{bg.R};{bg.G};{bg.B}m";
    _lastCellFg = fg;
    _lastCellBg = bg;
    return _lastCellAnsi;
}
```

### Driver Interface (Cell-Level Only)
*File: `Drivers/IConsoleDriver.cs`*

The driver interface exposes three cell-level output methods. No ANSI string writes exist:

```csharp
public interface IConsoleDriver
{
    void SetCell(int x, int y, char character, Color fg, Color bg);  // char convenience
    void FillCells(int x, int y, int width, char character, Color fg, Color bg);  // char convenience
    void WriteBufferRegion(int destX, int destY, CharacterBuffer source,
        int srcX, int srcY, int width, Color fallbackBg);
    // Note: char overloads wrap in Rune internally. All paths support
    // full Unicode including wide characters and combining marks.
}
```

In **Buffer mode**, all three delegate to the corresponding `ConsoleBuffer` methods.
In **Direct mode**, they format inline ANSI strings and write immediately to stdout.

### Render (Diff-Based Screen Output)
*File: `ConsoleBuffer.cs`*

The final step: writing to the physical console. Builds the entire frame output as a single string for atomic write.

```csharp
public void Render()
{
    lock (_consoleLock)
    {
        // 1. Pre-process wide character dirty pair coherence
        //    When either half of a wide char pair changes, force both dirty
        for (int y = 0; y < _height; y++)
            for (int x = 1; x < _width; x++)
            {
                // If continuation changed but base is clean → force base dirty
                // If front had continuation but back doesn't → force base dirty
            }

        // 2. Build entire screen in one string (atomic output)
        _screenBuilder.Clear();

        // 3. Choose rendering strategy per configured DirtyTrackingMode:
        //    - Cell:  render only changed regions within lines (minimal output)
        //    - Line:  render entire line when any cell changes
        //    - Smart: analyze each line and choose Cell vs Line dynamically
        for (int y = 0; y < _height; y++)
        {
            // ... strategy-specific rendering ...
        }

        // 4. Single atomic write via raw libc write() on Unix
        if (_screenBuilder.Length > 0)
            WriteOutput(_screenBuilder);
    }
}
```

**Four levels of optimization:**

1. **Wide character coherence**: Pre-processes wide char pairs so both halves are re-emitted together
2. **Line-level dirty checking**: Skip unchanged lines entirely
3. **Region diffing**: Within dirty lines, only update changed regions (Cell/Smart modes)
4. **Atomic output**: Single `WriteOutput()` call eliminates flicker from multiple cursor moves

**Three dirty tracking modes** (configurable via `ConsoleWindowSystemOptions.DirtyTrackingMode`):

| Mode | Strategy | Best For |
|------|----------|----------|
| **Cell** | Render only changed cell ranges per line | Sparse updates (text input, cursor blink) |
| **Line** | Render entire line when any cell changes | Dense updates (full redraws, scrolling) |
| **Smart** | Per-line analysis: coverage > threshold or fragmented runs → Line mode, else Cell mode | General purpose (default) |

### AppendRegionToBuilder (Wide Character Aware Output)
*File: `ConsoleBuffer.cs`*

Appends a dirty region to the output string, handling wide characters and combiners:

```csharp
private void AppendRegionToBuilder(int y, int startX, int endX, StringBuilder builder)
{
    string lastOutputAnsi = string.Empty;

    for (int x = startX; x <= endX && x < _width; x++)
    {
        ref var backCell = ref _backBuffer[x, y];
        ref var frontCell = ref _frontBuffer[x, y];
        frontCell.CopyFrom(backCell);  // Sync buffers

        // Skip continuation cells — terminal auto-advances for wide chars
        if (backCell.IsWideContinuation) { /* emit combiners only */ continue; }

        // Wide char ghost cleanup: if terminal had different content at x+1,
        // emit space there first to clear old content, then reposition
        bool isWideChar = x + 1 < _width && _backBuffer[x + 1, y].IsWideContinuation;
        if (isWideChar) { /* clear ghost content at x+1 if needed */ }

        // Emit ANSI only when color changes
        if (backCell.AnsiEscape != lastOutputAnsi)
            builder.Append(backCell.AnsiEscape);

        builder.AppendRune(backCell.Character);  // Rune → UTF-16 encoding
        if (backCell.Combiners != null)
            builder.Append(backCell.Combiners);

        if (isWideChar) x++;  // Skip continuation in loop
    }
}
```

**Wide character output handling:**
- Continuation cells are skipped (terminal auto-advances past them)
- Ghost content at x+1 is cleared before emitting a wide char (prevents old content from persisting)
- `AppendRune()` handles surrogate pair encoding for characters above U+FFFF
- Combiners are appended after both base and continuation cells

**Typical performance:**
- **Idle frame**: 0 lines rendered (all clean)
- **Text input**: 1-2 lines rendered (input line + cursor)
- **Window drag**: 10-50 lines rendered (only affected regions)
- **Full redraw**: All lines rendered (rare, only on resize/theme change)

---

## 7. Console Driver Abstraction: `NetConsoleDriver`

*File: `Drivers/NetConsoleDriver.cs:65-1027`*

Abstracts platform-specific console operations and manages console state.

### Responsibilities

1. **Console initialization** (colors, cursor visibility, buffer size)
2. **Render mode management** (Buffer vs Direct)
3. **Input handling** (background thread with blocking read)
4. **Resize detection** (background thread with polling)
5. **Screen coordinate mapping**

### Two Render Modes

#### Buffer Mode (Recommended)
*File: `NetConsoleDriver.cs:145-200`*

Uses `ConsoleBuffer` for double-buffered rendering.

```csharp
public void Render() // Buffer mode
{
    if (_renderMode == RenderMode.Buffer)
    {
        _consoleBuffer.Render();
    }
}
```

**Advantages:**
- Diff-based rendering (minimal console I/O)
- Flicker-free updates
- Handles overlapping windows gracefully
- Best performance for complex UIs

**Use when:**
- Multiple overlapping windows
- Frequent UI updates
- Production applications

#### Direct Mode
*File: `NetConsoleDriver.cs`*

Immediate rendering without buffering. All three cell-level methods format inline ANSI and write directly:

```csharp
// SetCell in Direct mode
public void SetCell(int x, int y, char character, Color fg, Color bg)
{
    var ansi = $"\x1b[38;2;{fg.R};{fg.G};{fg.B};48;2;{bg.R};{bg.G};{bg.B}m{character}\x1b[0m";
    WriteOutput($"\x1b[{y + 1};{x + 1}H");
    WriteOutput(ansi);
}

// WriteBufferRegion in Direct mode
public void WriteBufferRegion(int destX, int destY, CharacterBuffer source,
    int srcX, int srcY, int width, Color fallbackBg)
{
    var sb = new StringBuilder();
    for (int i = 0; i < width; i++)
    {
        var cell = source.GetCell(srcX + i, srcY);
        sb.Append($"\x1b[38;2;{cell.Foreground.R};...;48;2;{cell.Background.R};...m");
        sb.Append(cell.Character);
    }
    Console.SetCursorPosition(destX, destY);
    Console.Write(sb.ToString());
}
```

**Advantages:**
- Immediate visual feedback
- Simpler debugging (see what's being drawn)
- Lower memory usage

**Use when:**
- Debugging rendering issues
- Simple single-window applications
- Testing

### Background Threads

#### Input Loop
*File: `NetConsoleDriver.cs:560-680`*

Continuously reads keyboard/mouse input without blocking rendering.

```csharp
private void InputLoop()
{
    while (!_shouldStop)
    {
        if (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            lock (_inputQueue)
            {
                _inputQueue.Enqueue(keyInfo);
            }
        }
        else
        {
            Thread.Sleep(10); // Small sleep to avoid busy-waiting
        }
    }
}
```

**Design rationale:**
- `Console.ReadKey()` is blocking, would freeze rendering
- Background thread allows input + rendering concurrently
- Queue-based communication with main thread

#### Resize Loop
*File: `NetConsoleDriver.cs:720-800`*

Detects console window resizes and triggers full redraw.

```csharp
private void ResizeLoop()
{
    int lastWidth = Console.WindowWidth;
    int lastHeight = Console.WindowHeight;

    while (!_shouldStop)
    {
        Thread.Sleep(250); // Check 4 times per second

        int currentWidth = Console.WindowWidth;
        int currentHeight = Console.WindowHeight;

        if (currentWidth != lastWidth || currentHeight != lastHeight)
        {
            OnConsoleResized(new Size(currentWidth, currentHeight));
            lastWidth = currentWidth;
            lastHeight = currentHeight;
        }
    }
}
```

**Platform limitations:**
- .NET Console API has no resize event
- Polling is necessary (expensive, but infrequent operation)
- 250ms polling interval = imperceptible lag

## 7.5. Unicode & Wide Character Support

*Files: `Helpers/UnicodeWidth.cs`, `Layout/Cell.cs`, `Layout/CharacterBuffer.cs`, `Drivers/ConsoleBuffer.cs`*

The rendering pipeline fully supports Unicode, including characters that occupy multiple terminal columns (CJK ideographs, emoji) and zero-width combining marks (diacritics, variation selectors).

### Character Representation: `Rune`

All character storage uses `System.Text.Rune` instead of `char`. A `Rune` represents a single Unicode scalar value (up to U+10FFFF), while `char` is a UTF-16 code unit that can only represent U+0000–U+FFFF directly. Characters above U+FFFF (most emoji, some CJK) require surrogate pairs as `char` but are a single `Rune`.

### Display Width Detection

`UnicodeWidth` (backed by the Wcwidth library) determines how many terminal columns a character occupies:

| Width | Characters | Handling |
|-------|-----------|----------|
| **0** | Combining marks, variation selectors, ZWJ | Attached to preceding base cell via `Combiners` field |
| **1** | ASCII, Latin, Cyrillic, most scripts | Standard single-cell rendering |
| **2** | CJK ideographs, many emoji, fullwidth forms | Base cell + continuation cell (`IsWideContinuation`) |

**Special case:** Spacing Combining Marks (Unicode category Mc) are corrected to width 1, as they occupy visual space in terminals despite Wcwidth reporting them as zero-width.

### Wide Character Flow

```
1. CharacterBuffer.WriteString("日本語")
   ├─ '日' → width 2 → SetCell(x, y, '日') + SetCell(x+1, y, continuation)
   ├─ '本' → width 2 → SetCell(x+2, y, '本') + SetCell(x+3, y, continuation)
   └─ '語' → width 2 → SetCell(x+4, y, '語') + SetCell(x+5, y, continuation)

2. ConsoleBuffer.SetCellsFromBuffer()
   └─ Copies cells including IsWideContinuation and Combiners flags

3. ConsoleBuffer.Render()
   ├─ Pre-process: Force both halves of changed wide char pairs dirty
   └─ AppendRegionToBuilder():
      ├─ Skip continuation cells (terminal auto-advances)
      ├─ Clear ghost content at x+1 before emitting wide char
      └─ AppendRune() handles surrogate pair encoding
```

### Orphaned Wide Character Cleanup

When a write operation overwrites one cell of a wide character pair, the other cell becomes "orphaned." All write methods (`SetCell`, `FillCells`, `SetCellsFromBuffer`) automatically clean up orphaned cells:

- Overwriting a continuation cell → clear its base cell (at x-1) to a space
- Overwriting a base cell → clear its continuation cell (at x+1) to a space
- Fill operations clean up at both left and right boundaries

### Zero-Width Combining Marks

Zero-width characters (diacritics like ◌̈, variation selectors, ZWJ) are attached to the preceding base cell's `Combiners` string field. During output, combiners are appended after the base character in the ANSI output stream. The algorithm skips past continuation cells to find the correct base cell.

### `StringBuilderExtensions.AppendRune()`

*File: `Helpers/StringBuilderExtensions.cs`*

Since `StringBuilder.Append(char)` cannot handle characters above U+FFFF, `AppendRune()` encodes the `Rune` as UTF-16 (potentially a surrogate pair) before appending:

```csharp
public static StringBuilder AppendRune(this StringBuilder sb, Rune rune)
{
    Span<char> buf = stackalloc char[2];
    int charsWritten = rune.EncodeToUtf16(buf);
    for (int i = 0; i < charsWritten; i++)
        sb.Append(buf[i]);
    return sb;
}
```

---

## 8. Complete Data Flow Diagram

```
APPLICATION LAYER
═════════════════════════════════════════════════════════════════
  User calls:
  • window.AddControl(new MarkupControl("text"))
  • window.Title = "New Title"
  • window.BackgroundColor = Color.Blue
           │
           ▼
  Window.Invalidate() sets window.NeedsRedraw = true
           │
           │
MAIN EVENT LOOP (ConsoleWindowSystem.Run)
═════════════════════════════════════════════════════════════════
  ┌────────────────────────────────────────────────┐
  │ 1. ProcessInput()                              │  ← Input from background thread
  │    • Keyboard/mouse from NetConsoleDriver      │
  │    • Updates control states                    │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 2. ProcessPendingEvents()                      │
  │    • Timers, deferred actions                  │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 3. Dirty Check                                 │
  │    if (_needsFullRedraw ||                     │
  │        _windows.Any(w => w.NeedsRedraw))       │
  └────────────────────┬───────────────────────────┘
                       │ YES
                       ▼
RENDERING COORDINATOR (UpdateDisplay)
═════════════════════════════════════════════════════════════════
  ┌────────────────────────────────────────────────┐
  │ Phase 1: Desktop Clear (if needed)             │
  │ _driver.ClearScreen()                          │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ Phase 2: Build Render List                     │
  │ • Z-order sorting (back-to-front)              │
  │ • Filter visible, non-minimized windows        │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ Phase 3: Multi-Pass Rendering                  │
  │ For each pass (Normal, Active, AlwaysOnTop):   │
  │   For each window in pass:                     │
  │     Renderer.RenderWindow(window, renderList)  │
  └────────────────────┬───────────────────────────┘
                       │
                       │
WINDOW RENDERER (Renderer.RenderWindow)
═════════════════════════════════════════════════════════════════
  ┌────────────────────▼───────────────────────────┐
  │ 4. Calculate Visible Regions                   │
  │ VisibleRegions.CalculateVisibleRegions(...)    │  ← Occlusion culling
  │ • Rectangle subtraction algorithm              │
  │ • Returns list of visible rectangles           │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 5. Render Background                           │
  │ For each visible region:                       │
  │   _driver.FillCells(x, y, w, ' ', fg, bg)     │  ← Direct cell fill
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 6. Render Border (if present)                  │
  │ Cached CharacterBuffers + SetCell              │  ← Cell-level border cache
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 7. Render Content (Direct Cell Path)           │
  │ var buffer =                                   │
  │   window.EnsureContentReady(regions)           │
  │ RenderVisibleWindowContentFromBuffer(           │
  │   window, buffer, visibleRegions)              │
  │ → Copies cells via WriteBufferRegion()         │
  └────────────────────┬───────────────────────────┘
                       │
                       │
WINDOW CONTENT (Window.EnsureContentReady)
═════════════════════════════════════════════════════════════════
  ┌────────────────────▼───────────────────────────┐
  │ 8. Rebuild DOM (if needed)                     │
  │ if (_invalidated)                              │
  │   RebuildContentBufferOnly()                   │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 9. Layout & Measure                            │
  │ • MEASURE: Calculate desired sizes             │  ← DOM layout
  │ • ARRANGE: Assign final positions              │
  │ • Buffer cleared with background color         │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 9.5a PreBufferPaint Event (Optional)           │
  │ • Fire pre-paint hook                          │  ← BACKGROUNDS
  │ • Custom backgrounds, fractals, patterns       │
  │ • Controls will render ON TOP                  │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 9.5b Paint Controls                            │
  │ • PAINT: Draw controls to CharacterBuffer      │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 9.5c PostBufferPaint Event (Optional)          │
  │ • Fire post-paint hook                         │  ← EFFECTS
  │ • Transitions, filters, overlays, snapshots    │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 10. CharacterBuffer Ready                      │
  │ Buffer contains Cell[,] with char, fg, bg      │
  │ • No ANSI serialization in normal path         │
  │ • ToLines() only called for diagnostics        │
  └────────────────────┬───────────────────────────┘
                       │
                       │ (Returns CharacterBuffer to Renderer)
                       │
CONSOLE DRIVER (NetConsoleDriver + ConsoleBuffer)
═════════════════════════════════════════════════════════════════
  ┌────────────────────▼───────────────────────────┐
  │ 11. Cell-Level Writes to ConsoleBuffer         │
  │ Three paths, all cell-level:                   │
  │  • _driver.FillCells() ← background fills      │
  │  • _driver.SetCell()   ← vertical borders      │
  │  • _driver.WriteBufferRegion()                 │  ← Content + border lines
  │     → ConsoleBuffer.SetCellsFromBuffer()       │
  │     → ConsoleBuffer.SetCell()                  │
  │     → ConsoleBuffer.FillCells()                │
  │   All use cached ANSI formatting per cell      │
  │   No ANSI parsing at ConsoleBuffer level       │
  └────────────────────┬───────────────────────────┘
                       │ (Repeat for all windows)
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 12. Diff and Render                            │
  │ ConsoleBuffer.Render()                         │
  │ • Compare back buffer vs front buffer          │  ← Diff-based I/O
  │ • Identify changed regions per line            │
  │ • Console.SetCursorPosition() + Write()        │
  │ • Swap front/back buffers                      │
  └────────────────────┬───────────────────────────┘
                       │
                       ▼
═════════════════════════════════════════════════════════════════
                PHYSICAL CONSOLE OUTPUT
                 (User sees updated display)
═════════════════════════════════════════════════════════════════
```

---

## 9. Key Optimizations

### 1. Double Buffering (Two Levels)

**Window Level: CharacterBuffer**
- Each window has its own character buffer
- Allows independent window rendering
- Occlusion culling operates at this level

**Screen Level: ConsoleBuffer**
- Entire screen double-buffered
- Front buffer = currently displayed
- Back buffer = being rendered
- Swap on `Render()` call

**Impact:**
- Eliminates flicker completely
- Enables diff-based rendering
- Allows overlapping windows without corruption

### 2. Dirty Tracking (Three Levels)

**Level 1: Window Dirty Flag**
```csharp
public bool NeedsRedraw { get; private set; }

public void Invalidate()
{
    NeedsRedraw = true;
    // Frame check: if (any window dirty) → UpdateDisplay()
}
```

**Level 2: Cell Dirty Flag**
```csharp
public struct Cell
{
    public Rune Character;
    public bool Dirty;              // Character/color changed
    public bool IsWideContinuation; // Right half of wide char
    public string? Combiners;       // Zero-width combining marks
}

// Only mark dirty if value actually changes (including wide char/combiner state)
if (cell.Character != character || cell.IsWideContinuation || cell.Combiners != null)
{
    cell.Character = character;
    cell.IsWideContinuation = false;
    cell.Combiners = null;
    cell.Dirty = true;
}
```

**Level 3: Line Dirty Flag**
```csharp
private class BufferLine
{
    public bool Dirty;  // Any cell in line changed
}

// Skip entire line if not dirty
if (!line.Dirty)
    continue;
```

**Level 4: Smart Mode (Per-Line Analysis)**
```csharp
// Smart mode dynamically chooses Cell vs Line strategy per line
var (isDirty, useLineMode) = AnalyzeLine(y);
// coverage > SmartModeCoverageThreshold → Line mode
// dirtyRuns > SmartModeFragmentationThreshold → Line mode
// Otherwise → Cell mode
```

**Impact:**
- Idle frame: 0 console I/O operations
- Text input: 1-2 lines updated (Cell mode per line)
- Window drag: 10-50 lines updated (Smart picks Line mode for dense lines)
- Full redraw: All lines, Line mode (only on resize/theme change)

### 3. Occlusion Culling

**Problem:**
Without culling, background windows are fully rendered even when completely hidden.

**Solution:**
Rectangle subtraction algorithm calculates visible regions.

**Impact:**
- **Scenario**: 3 overlapping windows (A behind B behind C)
  - **Without culling**: All 3 rendered fully = 3x work
  - **With culling**: Only visible parts of A/B rendered ≈ 1.2x work
- **50-70% reduction** in rendering work for typical desktop layouts

### 4. Border Caching

*File: `Windows/BorderRenderer.cs`*

Pre-renders window border lines as `CharacterBuffer` objects (one row each for top and bottom borders), reused each frame. Vertical borders use direct `SetCell` calls — no intermediate buffer needed.

```csharp
// Cached as CharacterBuffers, not ANSI strings
internal CharacterBuffer? _cachedTopBorder;
internal CharacterBuffer? _cachedBottomBorder;

// Top/bottom borders: write cached CharacterBuffer via WriteBufferRegion
driver.WriteBufferRegion(borderStartX, screenY,
    _cachedTopBorder, srcX, 0, borderWidth, bg);

// Vertical borders: direct SetCell (single character, no buffer needed)
driver.SetCell(windowLeft, screenY, chars.Vertical, borderFg, bg);
```

**Invalidation:**
- Window resize
- Active/inactive state change (border color changes)

**Impact:**
- **Without caching**: Rebuild border cells every frame (~5-10% CPU)
- **With caching**: Single `WriteBufferRegion` call per visible region (<1% CPU)
- **No ANSI processing**: Border chars and colors are known at construction time

### 5. ANSI Optimization

**Color Run Compression**

Instead of emitting ANSI codes for every character:

```
❌ BAD (naive approach):
\x1b[37;40mH\x1b[37;40me\x1b[37;40ml\x1b[37;40ml\x1b[37;40mo
(240 bytes for "Hello")

✅ GOOD (optimized):
\x1b[37;40mHello
(14 bytes for "Hello")
```

**Implementation:**
```csharp
Color? currentFg = null;

for (int x = 0; x < width; x++)
{
    if (cell.Foreground != currentFg)
    {
        sb.Append(AnsiCodes.Foreground(cell.Foreground));
        currentFg = cell.Foreground;
    }
    sb.Append(cell.Character);
}
```

**Impact:**
- **Typical reduction**: 70-80% fewer ANSI escape codes
- **Example**: 1000-character line with 10 color changes
  - Naive: 1000 × 12 bytes = 12,000 bytes
  - Optimized: 1000 + (10 × 12) = 1,120 bytes (90% reduction)

### 6. Z-Order Rendering (Three Passes)

Ensures correct visual stacking without sorting overhead.

```
Pass 1: Normal Windows
┌──────────┐
│ Window A │
└──────────┘

Pass 2: Active Window (overlays normal)
        ┌──────────┐
        │ Window B │
        └──────────┘

Pass 3: AlwaysOnTop (overlays all)
                    ┌────────────┐
                    │ Tooltip/   │
                    │ Notification│
                    └────────────┘
```

**Impact:**
- Guarantees correct visual order
- Avoids complex Z-order sorting
- Predictable rendering behavior

### 7. Compositor Effects Hook

Event-based buffer manipulation for post-processing effects.

```csharp
// Zero-cost when not used
if (PostBufferPaint != null && _buffer != null)
{
    PostBufferPaint.Invoke(_buffer, dirtyRegion, clipRect);
}
```

**Benefits:**
- Enables transitions, filters, and overlays without core changes
- Fires within render lock (thread-safe)
- Only processes subscribed windows
- Provides dirty region for optimization

**Use Cases:**
- Fade in/out transitions
- Blur effects for modal backgrounds
- Glow effects around focused controls
- Screenshot capture via `BufferSnapshot`

**Performance:**
- Zero overhead when event not subscribed
- Event fires after painting, before buffer is consumed by the driver (optimal timing)
- Can use dirty region to minimize processing area

See [Compositor Effects](COMPOSITOR_EFFECTS.md) for comprehensive guide.

### 8. Unified Cell Pipeline (Single Rendering Path)

The most significant optimization: **all data enters ConsoleBuffer through cell-level methods only**. There is no ANSI string parsing anywhere in the ConsoleBuffer. This applies to everything: window content, borders, fills, status bars, and overlays.

**Eliminated paths:**
```
✗ AddContent(x, y, ansiString)     — ANSI state-machine parser (removed)
✗ WriteToConsole(x, y, ansiString) — driver ANSI string method (removed)
✗ SubstringAnsi()                  — regex-based ANSI clipping (removed)
✗ SubstringAnsiWithPadding()       — ANSI clip + pad (removed earlier)
```

**Current paths (all cell-level):**
```
Window content:   CharacterBuffer → SetCellsFromBuffer() → ConsoleBuffer
Border lines:     CharacterBuffer → SetCellsFromBuffer() → ConsoleBuffer
Vertical borders: SetCell(x, y, char, fg, bg)            → ConsoleBuffer
Background fills: FillCells(x, y, width, char, fg, bg)   → ConsoleBuffer
Status bars:      Markup → MarkupParser → CharacterBuffer → SetCellsFromBuffer()
```

**Markup parsing (never reaches ConsoleBuffer as strings):**
- Controls use `MarkupParser.Parse` → cells → `CharacterBuffer`
- Status bars use the same pattern: markup → `MarkupParser` → `CharacterBuffer`
- This is the **paint phase** inside controls — markup is parsed directly into cells. By the time data reaches ConsoleBuffer, it is always cells.

**ANSI color cache** exploits spatial locality:
- Adjacent cells typically share the same foreground/background colors
- `FormatCellAnsi()` caches the last fg/bg → ANSI string mapping
- Cache hit rate is typically >90% for UI content

**Impact:**
- **ConsoleBuffer has zero ANSI parsing code** — simpler, easier to reason about
- **Eliminates ~3 allocations per row per frame** (StringBuilder, string, List entry) for window content
- **Removes regex overhead** from border clipping (was `SubstringAnsi`, now `WriteBufferRegion` with srcX/width)
- **Removes ANSI parsing overhead** from fills and borders (was `AddContent`, now `SetCell`/`FillCells`)
- **Estimated 40-60% reduction** in per-window rendering cost

---

## 10. Threading & Locking

### Lock Hierarchy (Deadlock Prevention)

The library uses multiple locks with strict ordering:

```
1. _renderLock (ConsoleWindowSystem)
   • Protects rendering pipeline
   • Must be acquired first if multiple locks needed

2. window._lock (Window)
   • Protects window state
   • Acquired inside _renderLock

3. _consoleLock (NetConsoleDriver)
   • Protects console I/O
   • Acquired last, held briefly

RULE: Always acquire in order 1 → 2 → 3
NEVER acquire in reverse order (deadlock risk)
```

### Critical Sections

#### _renderLock (Rendering)
*File: `ConsoleWindowSystem.cs:2525`*

```csharp
private void UpdateDisplay()
{
    lock (_renderLock)  // Entire rendering pipeline protected
    {
        // Phase 1-5: All rendering happens here
    }
}
```

**Protects:**
- `_windows` list iteration
- Renderer state
- Driver calls

**Duration:** Entire frame (typically 1-5ms)

#### window._lock (Window State)
*File: `Window.cs:350`*

```csharp
public void AddControl(IWindowControl control)
{
    lock (_lock)
    {
        _controls.Add(control);
        Invalidate();
    }
}
```

**Protects:**
- `_controls` list
- Window properties (title, colors, size)
- Content buffer

**Duration:** Brief (microseconds)

#### _consoleLock (Console I/O)
*File: `NetConsoleDriver.cs:180`*

```csharp
public void Render()
{
    lock (_consoleLock)
    {
        Console.SetCursorPosition(x, y);
        Console.Write(text);
    }
}
```

**Protects:**
- Console.* API calls
- Cursor position consistency

**Duration:** Very brief (single I/O operation)

### Background Threads

| Thread | Purpose | Frequency | Synchronization |
|--------|---------|-----------|-----------------|
| **InputLoop** | Read keyboard/mouse | Continuous (10ms sleep) | Queue + lock |
| **ResizeLoop** | Detect window resize | 4 Hz (250ms sleep) | Event callback |

**Communication:**
```csharp
// Input thread → Main thread
lock (_inputQueue)
{
    _inputQueue.Enqueue(keyInfo);
}

// Main thread reads during ProcessInput()
lock (_inputQueue)
{
    while (_inputQueue.TryDequeue(out var keyInfo))
        HandleInput(keyInfo);
}
```

**Safety guarantees:**
- No shared mutable state between threads (except queues)
- All rendering occurs on main thread
- Background threads only feed data to main thread

---

## 11. Performance Metrics

### Frame Time Tracking
*File: `ConsoleWindowSystem.cs:2750-2766`*

```csharp
private void UpdateDisplay()
{
    var startTime = DateTime.UtcNow;

    // ... rendering ...

    var frameTime = DateTime.UtcNow - startTime;
    _recentFrameTimes.Enqueue(frameTime);

    if (_recentFrameTimes.Count > 60)
        _recentFrameTimes.Dequeue();
}
```

### Status Bar Display
*File: `ConsoleWindowSystem.cs:2800-2850`*

Shows real-time performance data:

```
═════════════════════════════════════════════════════════════
FPS: 58.3 | Frame: 2.1ms | Dirty: 143 chars | Mem: 42 MB
═════════════════════════════════════════════════════════════
```

**Metrics:**

| Metric | Calculation | Meaning |
|--------|-------------|---------|
| **FPS** | `1000 / avg(frameTimes)` | Frames per second |
| **Frame Time** | `DateTime.UtcNow - startTime` | Milliseconds per frame |
| **Dirty Chars** | Count of dirty cells in buffers | Characters changed this frame |
| **Memory** | `GC.GetTotalMemory(false)` | Managed heap size |

**Typical values:**

| Scenario | FPS | Frame Time | Dirty Chars |
|----------|-----|------------|-------------|
| Idle | 60 | 0.5ms | 0 |
| Text input | 60 | 1-2ms | 50-100 |
| Window drag | 45-55 | 3-5ms | 500-2000 |
| Full redraw | 30-40 | 10-20ms | 5000-20000 |

### Enabling Metrics

```csharp
var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);
windowSystem.ShowPerformanceMetrics = true;
```

---

## 12. Render Modes

### Buffer Mode (Default, Recommended)

```csharp
var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);
```

**Characteristics:**
- Double-buffered at screen level
- Diff-based rendering
- Minimal console I/O
- Flicker-free

**Use for:**
- ✅ Production applications
- ✅ Multiple overlapping windows
- ✅ Frequent UI updates
- ✅ Complex layouts

**Performance:**
- Idle: 0 console writes per frame
- Active: 1-50 console writes per frame
- Full redraw: 100-200 console writes

### Direct Mode

```csharp
var windowSystem = new ConsoleWindowSystem(RenderMode.Direct);
```

**Characteristics:**
- No screen buffering
- Immediate console I/O
- Visible rendering order
- Potential flicker

**Use for:**
- ✅ Debugging rendering issues
- ✅ Understanding rendering order
- ✅ Simple single-window apps
- ❌ Production (too much flicker)

**Performance:**
- Every `WriteBufferRegion()` call = immediate `Console.Write()`
- 10-100× more console I/O than Buffer mode
- Useful for seeing exactly what's being drawn when

### Comparison

| Aspect | Buffer Mode | Direct Mode |
|--------|-------------|-------------|
| **Performance** | Excellent | Poor |
| **Flicker** | None | Significant |
| **Debugging** | Harder (buffered) | Easier (immediate) |
| **Memory** | Higher | Lower |
| **Complexity** | Higher | Lower |
| **Production** | ✅ Yes | ❌ No |

---

## 13. File Reference Table

### Core Rendering Pipeline

| Component | File | Key Methods | Line Range |
|-----------|------|-------------|------------|
| **Event Loop** | `ConsoleWindowSystem.cs` | `Run()`, `ProcessOnce()` | — |
| **Render Coordinator** | `Rendering/RenderCoordinator.cs` | `UpdateDisplay()`, coverage caching, status bar caching | — |
| **Window Renderer** | `Renderer.cs` | `RenderWindow()`, `RenderVisibleWindowContentFromBuffer()` | — |
| **Overlay Renderer** | `Renderer.cs` | `RenderOverlayWindow()`, `RenderOverlayControlRegionsFromBuffer()` | — |
| **Occlusion Culling** | `VisibleRegions.cs` | `CalculateVisibleRegions()` | 40-158 |
| **Window Content** | `Window.Rendering.cs` | `EnsureContentReady()`, `ContentBuffer`, `RenderAndGetVisibleContent()` (test API) | — |
| **Layout Pipeline** | `Window.Layout.cs` | `RebuildContentBufferOnly()` | — |
| **DOM Rendering** | `Windows/WindowRenderer.cs` | `RebuildContentBuffer()` | — |
| **Character Buffer** | `Layout/CharacterBuffer.cs` | `SetChar()`, `GetCell()`, `ToLines()` (test/diagnostic) | — |
| **Console Buffer** | `Drivers/ConsoleBuffer.cs` | `SetCell()`, `FillCells()`, `SetCellsFromBuffer()`, `Render()` | — |
| **Console Driver** | `Drivers/NetConsoleDriver.cs` | `SetCell()`, `FillCells()`, `WriteBufferRegion()`, `Render()` | — |
| **Driver Interface** | `Drivers/IConsoleDriver.cs` | `SetCell()`, `FillCells()`, `WriteBufferRegion()` | — |

### Supporting Systems

| Component | File | Key Methods | Line Range |
|-----------|------|-------------|------------|
| **Input Handling** | `NetConsoleDriver.cs` | `InputLoop()`, `ProcessInput()` | 560-680 |
| **Resize Detection** | `NetConsoleDriver.cs` | `ResizeLoop()` | 720-800 |
| **Border Rendering** | `Windows/BorderRenderer.cs` | `RenderBorders()`, `BuildTopBorder()`, `BuildBottomBorder()` | — |
| **Z-Order Management** | `ConsoleWindowSystem.cs` | `BuildRenderList()` | 2634-2670 |
| **Performance Metrics** | `ConsoleWindowSystem.cs` | `RenderStatusBar()` | 2800-2850 |

### Helper Classes

| Component | File | Purpose |
|-----------|------|---------|
| **ANSI Codes** | `Rendering/AnsiCodes.cs` | ANSI escape sequence generation |
| **Color Helpers** | `Helpers/ColorResolver.cs` | Color resolution and inheritance |
| **Layout Rect** | `Models/ImmutableModels.cs` | Immutable rectangle structure |
| **Markup Parsing** | `Parsing/MarkupParser.cs` | Markup parsing and text measurement |
| **Unicode Width** | `Helpers/UnicodeWidth.cs` | Display width detection via Wcwidth (0/1/2 columns) |
| **Scrollbar Helper** | `Helpers/ScrollbarHelper.cs` | Shared scrollbar geometry, drawing, and hit testing |
| **StringBuilder Extensions** | `Helpers/StringBuilderExtensions.cs` | `AppendRune()` for Rune → UTF-16 StringBuilder output |

---

## 14. Common Patterns & Examples

### Pattern 1: Invalidating Content

When you modify a window's content, call `Invalidate()` to trigger a redraw:

```csharp
// Simple invalidation
window.AddControl(new MarkupControl("Hello"));
// AddControl() automatically calls Invalidate()

// Manual invalidation
window.Title = "New Title";  // Property setter calls Invalidate()

// Explicit invalidation
window.BackgroundColor = Color.Blue;
window.Invalidate();  // Force redraw
```

**What happens:**
1. `Invalidate()` sets `window.NeedsRedraw = true`
2. Next frame: `ProcessOnce()` detects dirty window
3. `UpdateDisplay()` called
4. Window rendered with new content

### Pattern 2: Custom Control Rendering

Implement `IWindowControl.PaintDOM()` to render custom controls:

```csharp
public class CustomControl : IWindowControl
{
    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect)
    {
        // Draw background
        buffer.FillRect(bounds, ' ', Color.White, Color.DarkBlue);

        // Draw text (supports full Unicode including CJK and emoji)
        buffer.WriteString(bounds.X + 2, bounds.Y + 1, "Custom Control",
                       Color.Yellow, Color.DarkBlue);

        // Draw border
        for (int x = bounds.X; x < bounds.Right; x++)
        {
            buffer.SetCell(x, bounds.Y, '─', Color.Gray, Color.DarkBlue);
            buffer.SetCell(x, bounds.Bottom - 1, '─', Color.Gray, Color.DarkBlue);
        }
    }
}
```

**Best practices:**
- Respect `clipRect` for scrollable containers
- Use `FillRect()` for backgrounds (faster than per-char)
- Cache expensive calculations (text measurements, layouts)
- Only draw within `bounds`

### Pattern 3: Efficient List Updates

When updating list items, use targeted invalidation:

```csharp
public class ListControl : IWindowControl
{
    private List<string> _items = new();

    public void AddItem(string item)
    {
        _items.Add(item);
        Container?.Invalidate(true);  // Invalidate parent window
    }

    public void UpdateItem(int index, string newValue)
    {
        if (index < 0 || index >= _items.Count)
            return;

        _items[index] = newValue;
        Container?.Invalidate(true);  // Only this window redraws
    }
}
```

**Optimization:**
- Batch updates: Modify multiple items, then call `Invalidate()` once
- Use `Container?.Invalidate(false)` for minor changes (no border redraw)
- Consider `InvalidationManager` for coalescing multiple invalidations

### Pattern 4: Modal Windows

Display modal windows that block interaction with other windows:

```csharp
// Create and show modal
var modal = new Window(windowSystem)
{
    Title = "Confirmation",
    Width = 40,
    Height = 10,
    IsModal = true,
    AlwaysOnTop = true
};

modal.AddControl(new MarkupControl("[yellow]Are you sure?[/]"));
modal.CenterOnScreen();
windowSystem.OpenWindow(modal);

// Check if modals active
if (windowSystem.ModalStateService.HasModals)
{
    // Background windows won't receive input
}

// Close modal
windowSystem.CloseWindow(modal);
```

**Behavior:**
- Modal windows have highest Z-order
- Input blocked to non-modal windows
- Escape key typically closes modal (customizable)

### Pattern 5: Real-Time Updates

For controls that update continuously (clocks, progress bars):

```csharp
public class ClockControl : IWindowControl
{
    private string _currentTime;

    public ClockControl()
    {
        // Update clock every second
        var timer = new System.Threading.Timer(_ =>
        {
            _currentTime = DateTime.Now.ToString("HH:mm:ss");
            Container?.Invalidate(true);
        }, null, 0, 1000);
    }

    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect)
    {
        buffer.DrawText(bounds.X, bounds.Y, _currentTime, Color.Green, Color.Black);
    }
}
```

**Considerations:**
- Timer callbacks run on background thread
- `Invalidate()` is thread-safe
- Rendering occurs on main thread (thread-safe by design)
- Avoid excessive update rates (>60 FPS wasted)

### Pattern 6: Measuring Text Width

When calculating text widths for layout, account for both markup tags and wide characters:

```csharp
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Helpers;

public int MeasureMarkupWidth(string markupText)
{
    // Strip markup tags: "[red]Hello[/]" → "Hello"
    int visualWidth = MarkupParser.StripLength(markupText);
    return visualWidth;
}

public int MeasureStringWidth(string plainText)
{
    // Accounts for wide characters (CJK = 2 columns, combining marks = 0)
    return UnicodeWidth.GetStringWidth(plainText);
}
```

**Why needed:**
- Markup tags don't consume screen space (`"[red]Hi[/]"` = 2 columns, not 10)
- CJK characters occupy 2 terminal columns (`"日本"` = 4 columns, not 2)
- Zero-width combiners don't consume space (`"é"` as e + combining accent = 1 column)
- Use for centering, alignment, truncation

### Pattern 7: Handling Overlapping Windows

The system handles overlaps automatically, but you can optimize:

```csharp
// Windows with AlwaysOnTop don't participate in occlusion culling
var tooltip = new Window(windowSystem)
{
    AlwaysOnTop = true,  // Rendered last, always visible
    IsModal = false      // Doesn't block input
};

// Background windows can be skipped if fully occluded
// System calculates this automatically via VisibleRegions
```

**Automatic optimizations:**
- Fully occluded windows skip content rendering
- Partially occluded windows render only visible regions
- Z-order ensures correct visual stacking

---

## 15. Debugging & Troubleshooting

### Rendering Issues

**Problem: Window content not updating**

Check:
1. Is `Invalidate()` being called?
2. Is `window.Visible` true?
3. Is window minimized?
4. Is window fully occluded by others?

Debug:
```csharp
// Force redraw
window.Invalidate();
windowSystem.FullRedraw();

// Check state using LogService (NEVER use Console.WriteLine - it corrupts UI!)
var logService = windowSystem.LogService;
logService.Log(LogLevel.Debug, "Window",
    $"NeedsRedraw: {window.NeedsRedraw}, Visible: {window.Visible}, Z-Order: {window.ZOrder}");

// Or write to a debug window
var debugWindow = new Window(windowSystem) { Title = "Debug Info" };
debugWindow.AddControl(new MarkupControl($"NeedsRedraw: {window.NeedsRedraw}"));
debugWindow.AddControl(new MarkupControl($"Visible: {window.Visible}"));
debugWindow.AddControl(new MarkupControl($"Z-Order: {window.ZOrder}"));
```

**Problem: Flickering**

Likely causes:
1. Using `RenderMode.Direct` (switch to `Buffer`)
2. Excessive invalidations (batch updates)
3. Console output interference (check for `Console.WriteLine()` or console logging providers - these WILL corrupt the UI!)

**Problem: Performance degradation**

Enable metrics:
```csharp
windowSystem.ShowPerformanceMetrics = true;
```

Watch for:
- FPS < 30: Too much rendering work
- Dirty chars > 10,000: Excessive invalidations
- Frame time > 16ms: Missing 60 FPS target

### Logging

**Method 1: Environment Variables**

Enable debug logging via environment:
```bash
export SHARPCONSOLEUI_DEBUG_LOG=/tmp/consoleui.log
export SHARPCONSOLEUI_DEBUG_LEVEL=Debug
dotnet run
```

**Method 2: Programmatic Initialization (Recommended for Debugging)**

Initialize logging directly in your code:
```csharp
var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer);

// Enable file logging programmatically
windowSystem.LogService.EnableFileLogging("/tmp/debug.log");
windowSystem.LogService.MinimumLevel = LogLevel.Debug;  // or LogLevel.Trace for maximum detail

// Your application code...
var window = new Window(windowSystem) { Title = "Test" };

// Log custom debug information
windowSystem.LogService.Log(LogLevel.Debug, "MyApp", "Window created");

// Access logs programmatically
var recentLogs = windowSystem.LogService.GetRecentLogs(50);
foreach (var entry in recentLogs)
{
    // Process log entries (store, analyze, etc.)
}
```

**Method 3: Subscribe to Log Events**

Real-time log monitoring:
```csharp
windowSystem.LogService.LogAdded += (sender, entry) =>
{
    // Write to your own file, database, or monitoring system
    File.AppendAllText("/tmp/myapp.log",
        $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] {entry.Category}: {entry.Message}\n");
};
```

**Log Categories to Search For:**
- `[System]` - Overall system lifecycle
- `[Render]` - Rendering operations
- `[Window]` - Window lifecycle (create, close, show, hide)
- `[WindowState]` - Window state management
- `[Focus]` - Focus changes
- `[Modal]` - Modal window handling
- `[Interaction]` - Drag/resize events
- `[Input]` - Keyboard/mouse input

**Example Log Output:**
```
[14:23:45.123] [Debug] Window: Window created - ID: win_001, Title: 'Main Window'
[14:23:45.134] [Trace] Render: UpdateDisplay started
[14:23:45.136] [Debug] Render: Building render list - 3 windows
[14:23:45.138] [Trace] Render: Rendering window 'Main Window' (Z-Order: 0)
[14:23:45.142] [Trace] Render: Visible regions calculated - 2 regions
[14:23:45.145] [Debug] Render: Frame completed - 2.1ms, 143 dirty chars
```

---

## Conclusion

The SharpConsoleUI rendering pipeline is designed for **performance, correctness, and maintainability**:

- **Performance**: Double buffering, dirty tracking, occlusion culling minimize CPU and I/O
- **Correctness**: Multi-pass rendering, locking hierarchy, diff-based output ensure visual accuracy
- **Maintainability**: Clear separation of concerns (Coordinator → Renderer → Window → Driver)

**Key takeaways:**

1. **Rendering is lazy**: Only dirty windows render, only dirty lines write to console
2. **Occlusion is expensive but worth it**: Rectangle subtraction adds complexity but saves 50%+ rendering
3. **Double buffering at two levels**: Window buffers for composition, screen buffer for output
4. **Three-pass rendering guarantees order**: Normal → Active → AlwaysOnTop
5. **Locking is simple**: One lock per layer, strict hierarchy, brief critical sections

For new contributors:
- Start at `ConsoleWindowSystem.UpdateDisplay()` to understand flow
- Read `Renderer.RenderWindow()` to see per-window logic
- Study `CharacterBuffer` and `ConsoleBuffer` to understand buffering
- Review `VisibleRegions` only if working on occlusion culling

For debugging:
- Use `RenderMode.Direct` to see immediate rendering
- Enable `ShowPerformanceMetrics` for real-time stats
- Enable debug logging for detailed operation traces
- Check lock contention if experiencing stuttering

**The pipeline in one sentence:**
> Application invalidates windows → Event loop detects dirty windows → RenderCoordinator orchestrates multi-pass rendering → Renderer calculates visible regions per window → DOM layout pipeline paints to CharacterBuffer (with full Unicode/wide character support) → All data enters ConsoleBuffer through cell-level methods only (SetCell, FillCells, SetCellsFromBuffer — no ANSI parsing) → Wide character coherence pre-processing ensures atomic pair updates → Smart dirty tracking selects optimal Cell/Line strategy per line → Single atomic write to console.
