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
│  • PostBufferPaint hook (compositor effects)                │
│  • ANSI serialization with color optimization               │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Console Driver (Screen Buffer)                 │
│  • ConsoleBuffer (screen-level double buffer)               │
│  • ANSI parsing and storage                                 │
│  • Diff-based line rendering                                │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    Physical Console                         │
│  • Console.SetCursorPosition()                              │
│  • Console.Write() with ANSI codes                          │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Responsibility | Location |
|-----------|---------------|----------|
| **ConsoleWindowSystem** | Main event loop, rendering coordinator | `ConsoleWindowSystem.cs` |
| **Renderer** | Multi-pass rendering, occlusion culling | `Renderer.cs` |
| **VisibleRegions** | Rectangle subtraction, overlap detection | `VisibleRegions.cs` |
| **Window** | Content rendering, DOM rebuilding | `Window.cs` |
| **CharacterBuffer** | Window-level buffer, ANSI serialization | `Layout/CharacterBuffer.cs` |
| **ConsoleBuffer** | Screen-level buffer, diff-based rendering | `Drivers/ConsoleBuffer.cs` |
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

    // Render window content
    var contentLines = window.RenderAndGetVisibleContent(visibleRegions);
    foreach (var line in contentLines)
    {
        _driver.AddContent(line.X, line.Y, line.Content);
    }
}
```

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

### Entry: `Window.RenderAndGetVisibleContent()`
*File: `Window.cs:2295-2430`*

Converts window controls into ANSI-formatted strings for display.

```csharp
public List<(int X, int Y, string Content)> RenderAndGetVisibleContent(
    List<LayoutRect> visibleRegions)
{
    lock (_lock)
    {
        // Rebuild DOM if needed (controls added/removed/invalidated)
        if (NeedsRedraw)
        {
            RebuildContentCacheDOM();
            NeedsRedraw = false;
        }

        // Extract lines from character buffer
        var allLines = _contentBuffer.ToLines();

        // Clip to visible regions only
        return ClipLinesToVisibleRegions(allLines, visibleRegions);
    }
}
```

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

**CharacterBuffer interface:**
```csharp
public interface ICharacterBuffer
{
    void SetChar(int x, int y, char c, Color fg, Color bg);
    void FillRect(LayoutRect rect, char c, Color fg, Color bg);
    void DrawText(int x, int y, string text, Color fg, Color bg);
}
```

#### Stage 3.5: Pre/Post Buffer Paint Hooks (Compositor Effects)
*File: `Windows/WindowRenderer.cs`*

Two compositor-style hooks allow buffer manipulation at different points in the rendering pipeline:

```csharp
private List<string> RebuildContentCacheDOM(...)
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

    // Continue to ANSI serialization
    return BufferToLines(foregroundColor, backgroundColor);
}
```

**PreBufferPaint** - Fires after buffer clear, before controls paint:
- **Custom backgrounds**: Animated patterns, gradients
- **Full-buffer graphics**: Fractals, visualizations
- Controls render ON TOP of pre-paint content

**PostBufferPaint** - Fires after controls paint, before ANSI conversion:
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

*File: `Layout/CharacterBuffer.cs:23-505`*

Window-level buffer storing character, foreground, and background color for each cell.

### Buffer Structure

```csharp
public class CharacterBuffer
{
    private Cell[,] _buffer;  // 2D array [row, col]
    private int _width;
    private int _height;

    private struct Cell
    {
        public char Character;
        public Color Foreground;
        public Color Background;
        public bool Dirty;  // Changed since last render
    }
}
```

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

#### 1. SetChar (Individual Cell Update)
*File: `CharacterBuffer.cs:145-165`*

```csharp
public void SetChar(int x, int y, char c, Color fg, Color bg)
{
    if (x < 0 || x >= _width || y < 0 || y >= _height)
        return; // Out of bounds, silently ignore

    ref var cell = ref _buffer[y, x];

    // Only mark dirty if actually changed
    if (cell.Character != c || cell.Foreground != fg || cell.Background != bg)
    {
        cell.Character = c;
        cell.Foreground = fg;
        cell.Background = bg;
        cell.Dirty = true;
    }
}
```

**Optimization: Dirty tracking**
- Only marks cell dirty if value actually changes
- Avoids redundant ANSI generation for unchanged cells

#### 2. FillRect (Bulk Operations)
*File: `CharacterBuffer.cs:200-235`*

Used for backgrounds, borders, clearing regions.

```csharp
public void FillRect(LayoutRect rect, char c, Color fg, Color bg)
{
    var clipped = ClipRect(rect);

    for (int y = clipped.Y; y < clipped.Bottom; y++)
    {
        for (int x = clipped.X; x < clipped.Right; x++)
        {
            SetChar(x, y, c, fg, bg);
        }
    }
}
```

#### 3. ToLines (ANSI Serialization)
*File: `CharacterBuffer.cs:350-505`*

Converts buffer to ANSI-formatted strings for console output.

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

**ANSI Optimization: Color Runs**
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
*File: `Drivers/ConsoleBuffer.cs:23-343`*

Second level of double buffering, operating at the screen level.

```csharp
public class ConsoleBuffer
{
    private BufferLine[] _frontBuffer;  // Currently displayed
    private BufferLine[] _backBuffer;   // Being rendered
    private int _width;
    private int _height;

    private class BufferLine
    {
        public Cell[] Cells;
        public bool Dirty;  // Line changed since last render
    }

    private struct Cell
    {
        public char Character;
        public Color Foreground;
        public Color Background;
    }
}
```

### AddContent (ANSI Parsing)
*File: `ConsoleBuffer.cs:180-245`*

Receives ANSI-formatted strings from window buffers, parses, and stores.

```csharp
public void AddContent(int x, int y, string ansiContent)
{
    if (y < 0 || y >= _height)
        return;

    var line = _backBuffer[y];
    int currentX = x;
    Color currentFg = Color.White;
    Color currentBg = Color.Black;

    // Parse ANSI string
    int i = 0;
    while (i < ansiContent.Length)
    {
        if (ansiContent[i] == '\x1b') // ANSI escape sequence
        {
            i = ParseAnsiSequence(ansiContent, i, ref currentFg, ref currentBg);
        }
        else
        {
            // Regular character
            if (currentX < _width)
            {
                line.Cells[currentX] = new Cell
                {
                    Character = ansiContent[i],
                    Foreground = currentFg,
                    Background = currentBg
                };
                currentX++;
            }
            i++;
        }
    }

    line.Dirty = true;
}
```

**ANSI parsing:**
- Inline parsing during storage (no separate pass)
- Extracts RGB colors from `\x1b[38;2;R;G;Bm` sequences
- Maintains color state across escape sequences

### Render (Diff-Based Screen Output)
*File: `ConsoleBuffer.cs:270-343`*

The final step: writing to the physical console.

```csharp
public void Render()
{
    for (int y = 0; y < _height; y++)
    {
        var backLine = _backBuffer[y];
        var frontLine = _frontBuffer[y];

        // Only render dirty lines
        if (!backLine.Dirty)
            continue;

        // Diff: Find changed regions within line
        var changedRegions = FindChangedRegions(backLine, frontLine);

        foreach (var region in changedRegions)
        {
            RenderLineRegion(y, region);
        }

        // Swap buffers for this line
        (_frontBuffer[y], _backBuffer[y]) = (_backBuffer[y], _frontBuffer[y]);
        _frontBuffer[y].Dirty = false;
    }
}
```

**Three levels of optimization:**

1. **Line-level dirty checking**: Skip unchanged lines entirely
2. **Region diffing**: Within dirty lines, only update changed regions
3. **Cursor movement optimization**: Minimize `SetCursorPosition` calls

### RenderLine (Cursor Movement Optimization)
*File: `ConsoleBuffer.cs:345-420`*

```csharp
private void RenderLineRegion(int y, Region region)
{
    Console.SetCursorPosition(region.StartX, y);

    Color? currentFg = null;
    Color? currentBg = null;

    for (int x = region.StartX; x <= region.EndX; x++)
    {
        var cell = _backBuffer[y].Cells[x];

        // Emit ANSI codes only when colors change
        if (cell.Foreground != currentFg)
        {
            Console.Write(AnsiCodes.Foreground(cell.Foreground));
            currentFg = cell.Foreground;
        }

        if (cell.Background != currentBg)
        {
            Console.Write(AnsiCodes.Background(cell.Background));
            currentBg = cell.Background;
        }

        Console.Write(cell.Character);
    }
}
```

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
*File: `NetConsoleDriver.cs:202-250`*

Immediate rendering without buffering.

```csharp
public void AddContent(int x, int y, string content) // Direct mode
{
    if (_renderMode == RenderMode.Direct)
    {
        Console.SetCursorPosition(x, y);
        Console.Write(content);
    }
    else
    {
        _consoleBuffer.AddContent(x, y, content);
    }
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
  │   _driver.FillRect(region, backgroundColor)    │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 6. Render Border (if present)                  │
  │ Using cached border strings                    │  ← Border cache
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 7. Render Content                              │
  │ var lines =                                    │
  │   window.RenderAndGetVisibleContent(regions)   │
  │ For each line:                                 │
  │   _driver.AddContent(line.X, line.Y, line.Txt) │
  └────────────────────┬───────────────────────────┘
                       │
                       │
WINDOW CONTENT (Window.RenderAndGetVisibleContent)
═════════════════════════════════════════════════════════════════
  ┌────────────────────▼───────────────────────────┐
  │ 8. Rebuild DOM (if needed)                     │
  │ if (NeedsRedraw)                               │
  │   RebuildContentCacheDOM()                     │
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
  │ 10. Serialize to ANSI                          │
  │ _contentBuffer.ToLines()                       │  ← ANSI generation
  │ • Color optimization (minimize escape codes)   │
  │ • Returns List<string> with ANSI formatting    │
  └────────────────────┬───────────────────────────┘
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 11. Clip to Visible Regions                    │
  │ ClipLinesToVisibleRegions(lines, regions)      │
  └────────────────────┬───────────────────────────┘
                       │
                       │
CONSOLE DRIVER (NetConsoleDriver + ConsoleBuffer)
═════════════════════════════════════════════════════════════════
  ┌────────────────────▼───────────────────────────┐
  │ 12. Parse and Buffer                           │
  │ ConsoleBuffer.AddContent(x, y, ansiString)     │
  │ • Parse ANSI escape sequences                  │  ← Screen buffer
  │ • Extract RGB colors                           │
  │ • Store in back buffer (Cell[,] array)         │
  │ • Mark line as dirty                           │
  └────────────────────┬───────────────────────────┘
                       │ (Repeat for all windows)
                       │
  ┌────────────────────▼───────────────────────────┐
  │ 13. Diff and Render                            │
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
private struct Cell
{
    public bool Dirty;  // Character/color changed
}

// Only mark dirty if value actually changes
if (cell.Character != newChar)
{
    cell.Character = newChar;
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

**Impact:**
- Idle frame: 0 console I/O operations
- Text input: 1-2 lines updated
- Window drag: 10-50 lines updated (only borders/overlaps)
- Full redraw: All lines (only on resize/theme change)

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

*File: `Window.cs:1850-1920`*

Pre-renders window borders as strings, reused each frame.

```csharp
private string[] _cachedBorderLines;

private void RebuildBorderCache()
{
    _cachedBorderLines = new string[Height];

    // Top border
    _cachedBorderLines[0] = "┌" + new string('─', Width - 2) + "┐";

    // Side borders
    for (int i = 1; i < Height - 1; i++)
        _cachedBorderLines[i] = "│" + new string(' ', Width - 2) + "│";

    // Bottom border
    _cachedBorderLines[Height - 1] = "└" + new string('─', Width - 2) + "┘";
}

// Usage: Just write cached strings
foreach (var line in _cachedBorderLines)
    _driver.AddContent(x, y++, line);
```

**Invalidation:**
- Window resize
- Border style change
- Theme change

**Impact:**
- **Without caching**: Build border strings every frame (~5-10% CPU)
- **With caching**: Simple string copy (<1% CPU)

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

**NEW**: Event-based buffer manipulation for post-processing effects.

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
- Event fires after painting, before ANSI conversion (optimal timing)
- Can use dirty region to minimize processing area

See [Compositor Effects](COMPOSITOR_EFFECTS.md) for comprehensive guide.

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
- Every `AddContent()` call = immediate `Console.Write()`
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
| **Event Loop** | `ConsoleWindowSystem.cs` | `Run()`, `ProcessOnce()` | 822-1002 |
| **Render Coordinator** | `ConsoleWindowSystem.cs` | `UpdateDisplay()` | 2522-2766 |
| **Window Renderer** | `Renderer.cs` | `RenderWindow()` | 211-270 |
| **Occlusion Culling** | `VisibleRegions.cs` | `CalculateVisibleRegions()` | 40-158 |
| **Window Content** | `Window.cs` | `RenderAndGetVisibleContent()` | 2295-2430 |
| **Layout Pipeline** | `Window.cs` | `RebuildContentCacheDOM()` | 2500-2803 |
| **Character Buffer** | `Layout/CharacterBuffer.cs` | `ToLines()`, `SetChar()` | 145-505 |
| **Console Buffer** | `Drivers/ConsoleBuffer.cs` | `AddContent()`, `Render()` | 23-343 |
| **Console Driver** | `Drivers/NetConsoleDriver.cs` | `Render()`, `ClearScreen()` | 65-1027 |

### Supporting Systems

| Component | File | Key Methods | Line Range |
|-----------|------|-------------|------------|
| **Input Handling** | `NetConsoleDriver.cs` | `InputLoop()`, `ProcessInput()` | 560-680 |
| **Resize Detection** | `NetConsoleDriver.cs` | `ResizeLoop()` | 720-800 |
| **Border Caching** | `Window.cs` | `RebuildBorderCache()` | 1850-1920 |
| **Z-Order Management** | `ConsoleWindowSystem.cs` | `BuildRenderList()` | 2634-2670 |
| **Performance Metrics** | `ConsoleWindowSystem.cs` | `RenderStatusBar()` | 2800-2850 |

### Helper Classes

| Component | File | Purpose |
|-----------|------|---------|
| **ANSI Codes** | `Rendering/AnsiCodes.cs` | ANSI escape sequence generation |
| **Color Helpers** | `Helpers/ColorResolver.cs` | Color resolution and inheritance |
| **Layout Rect** | `Models/ImmutableModels.cs` | Immutable rectangle structure |
| **Spectre Integration** | `Helpers/AnsiConsoleHelper.cs` | Spectre.Console markup parsing |

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

        // Draw text
        buffer.DrawText(bounds.X + 2, bounds.Y + 1, "Custom Control",
                       Color.Yellow, Color.DarkBlue);

        // Draw border
        for (int x = bounds.X; x < bounds.Right; x++)
        {
            buffer.SetChar(x, bounds.Y, '─', Color.Gray, Color.DarkBlue);
            buffer.SetChar(x, bounds.Bottom - 1, '─', Color.Gray, Color.DarkBlue);
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

### Pattern 6: Measuring Text with ANSI

When calculating text widths for layout, strip ANSI codes:

```csharp
using SharpConsoleUI.Helpers;

public int MeasureTextWidth(string markupText)
{
    // Strip Spectre.Console markup: "[red]Hello[/]" → "Hello"
    int visualWidth = AnsiConsoleHelper.StripSpectreLength(markupText);
    return visualWidth;
}
```

**Why needed:**
- ANSI escape codes don't consume screen space
- `"[red]Hi[/]"` displays as 2 characters, not 10
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
> Application invalidates windows → Event loop detects dirty windows → Multi-pass renderer calculates visible regions and draws to window buffers → Window buffers serialize to ANSI → Screen buffer diffs and writes only changed lines to console.
