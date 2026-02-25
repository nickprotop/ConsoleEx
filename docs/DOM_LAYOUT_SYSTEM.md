# DOM-Based Layout System Documentation

## Overview

SharpConsoleUI uses a DOM-based layout system inspired by WPF's two-pass layout model. This replaces the old string-list-based rendering with a proper tree-based layout engine that handles measurement, arrangement, and painting of controls within windows.

## Architecture

### Core Flow

```
1. BUILD TREE    → LayoutNode tree mirrors control hierarchy
2. MEASURE PASS  → Bottom-up: "How much space do you need?"
3. ARRANGE PASS  → Top-down: "Here's your final rect"
4. PAINT PASS    → Render to character buffer at computed positions
5. OUTPUT        → Flush buffer to console
```

### Key Components

```
SharpConsoleUI/
├── Layout/
│   ├── ILayoutContainer.cs      # Layout algorithm interface + enums
│   ├── LayoutNode.cs            # DOM node with tree + layout state
│   ├── LayoutConstraints.cs     # Min/max width/height constraints
│   ├── LayoutRect.cs            # Rectangle for bounds
│   ├── LayoutSize.cs            # Width/Height size struct
│   ├── CharacterBuffer.cs       # 2D cell array render target
│   ├── Cell.cs                  # Character + fg/bg colors
│   ├── AnsiParser.cs            # Parse ANSI sequences to cells
│   ├── VerticalStackLayout.cs   # Vertical stacking algorithm
│   └── IDOMPaintable.cs         # Interface for DOM-aware controls
├── Controls/
│   └── IWindowControl.cs        # Base control interface
└── Window.cs                    # Uses LayoutNode tree for rendering
```

---

## Key Interfaces

### IDOMPaintable

Controls that participate in DOM layout must implement this interface:

```csharp
public interface IDOMPaintable
{
    /// <summary>
    /// Measures the control given constraints. Returns desired size.
    /// Called during the measure pass (bottom-up).
    /// </summary>
    LayoutSize MeasureDOM(LayoutConstraints constraints);

    /// <summary>
    /// Paints the control to the character buffer.
    /// Called during the paint pass after arrangement.
    /// </summary>
    void PaintDOM(CharacterBuffer buffer, LayoutRect bounds,
                  LayoutRect clipRect, Color defaultFg, Color defaultBg);
}
```

### IWindowControl

Base interface for all controls:

```csharp
public interface IWindowControl : IDisposable
{
    int? ActualWidth { get; }
    int? Width { get; set; }
    HorizontalAlignment HorizontalAlignment { get; set; }
    VerticalAlignment VerticalAlignment { get; set; }
    Margin Margin { get; set; }
    StickyPosition StickyPosition { get; set; }
    bool Visible { get; set; }
    object? Tag { get; set; }
    IContainer? Container { get; set; }

    Size GetLogicalContentSize();
    void Invalidate();
}
```

### ILayoutContainer

Interface for layout algorithms:

```csharp
public interface ILayoutContainer
{
    LayoutSize Measure(LayoutNode node, LayoutConstraints constraints);
    void Arrange(LayoutNode node, LayoutRect finalRect);
}
```

---

## Alignment Enums

Located in `Layout/ILayoutContainer.cs`:

```csharp
public enum HorizontalAlignment
{
    Left,
    Center,
    Right,
    Stretch  // Expand to fill available width
}

public enum VerticalAlignment
{
    Top,
    Center,
    Bottom,
    Fill     // Expand to fill available height (replaces old FillHeight)
}
```

---

## LayoutNode

The `LayoutNode` class represents a node in the layout tree:

```csharp
public class LayoutNode
{
    // Tree structure
    public LayoutNode? Parent { get; }
    public IReadOnlyList<LayoutNode> Children { get; }
    public IWindowControl? Control { get; }

    // Layout input (from control)
    public int? ExplicitWidth { get; set; }
    public bool IsVisible { get; set; }
    public VerticalAlignment VerticalAlignment { get; set; }
    public StickyPosition StickyPosition { get; }
    public Margin Margin { get; }

    // Layout output (computed)
    public LayoutSize DesiredSize { get; }
    public LayoutRect Bounds { get; }           // Relative to parent
    public LayoutRect AbsoluteBounds { get; }   // Screen coordinates

    // Methods
    public LayoutSize Measure(LayoutConstraints constraints);
    public void Arrange(LayoutRect finalRect);
    public void Paint(CharacterBuffer buffer, LayoutRect clipRect,
                      Color defaultFg, Color defaultBg);
}
```

### Tree Building

The layout tree is built by `Window.RebuildLayoutTree()`:

```csharp
private void RebuildLayoutTree()
{
    _layoutRoot = new LayoutNode(null, new VerticalStackLayout());

    foreach (var control in _controls)
    {
        if (control.Visible)
        {
            var childNode = new LayoutNode(control);
            _layoutRoot.AddChild(childNode);

            // Handle nested containers (ColumnContainer, HorizontalGridControl)
            if (control is ILayoutContainer container)
            {
                BuildChildNodes(childNode, container);
            }
        }
    }
}
```

---

## Layout Constraints

```csharp
public readonly record struct LayoutConstraints(
    int MinWidth,
    int MaxWidth,
    int MinHeight,
    int MaxHeight
)
{
    public static LayoutConstraints Unbounded =>
        new(0, int.MaxValue, 0, int.MaxValue);

    public static LayoutConstraints Fixed(int width, int height) =>
        new(width, width, height, height);
}
```

---

## CharacterBuffer

The render target for all controls:

```csharp
public class CharacterBuffer
{
    public int Width { get; }
    public int Height { get; }

    // Core operations
    public void SetCell(int x, int y, char ch, Color fg, Color bg);
    public void FillRect(LayoutRect rect, char ch, Color fg, Color bg);
    public void WriteString(int x, int y, string text, Color fg, Color bg);
    public void WriteCells(int x, int y, IEnumerable<Cell> cells);
    public void WriteCellsClipped(int x, int y, IEnumerable<Cell> cells,
                                   LayoutRect clipRect);

    // Get cell at position
    public Cell GetCell(int x, int y);

    // Clear buffer
    public void Clear(Color backgroundColor);
}
```

---

## Layout Algorithms

### VerticalStackLayout

Stacks children vertically (used by Window and ColumnContainer):

**Measure Pass:**
1. Measure each child with remaining height constraint
2. Sum all child heights
3. Return max width, total height

**Arrange Pass:**
1. Calculate fixed heights for non-Fill children
2. Distribute remaining space to `VerticalAlignment.Fill` children
3. Position children top-to-bottom
4. Apply horizontal alignment within available width

```csharp
// Horizontal alignment during arrange
switch (child.Control?.HorizontalAlignment ?? HorizontalAlignment.Stretch)
{
    case HorizontalAlignment.Left:
        childX = 0;
        childWidth = child.DesiredSize.Width;
        break;
    case HorizontalAlignment.Center:
        childWidth = child.DesiredSize.Width;
        childX = (availableWidth - childWidth) / 2;
        break;
    case HorizontalAlignment.Right:
        childWidth = child.DesiredSize.Width;
        childX = availableWidth - childWidth;
        break;
    case HorizontalAlignment.Stretch:
    default:
        childX = 0;
        childWidth = availableWidth;
        break;
}
```

### HorizontalGridControl Layout

The `HorizontalGridControl` handles its own layout for columns:
- Distributes width among columns based on explicit widths and flex factors
- All columns share the same height
- Splitters between columns allow resizing

---

## Sticky Positioning

Controls can be "sticky" to remain visible during scrolling:

```csharp
public enum StickyPosition
{
    None,    // Scrolls normally
    Top,     // Stays at top of viewport
    Bottom   // Stays at bottom of viewport
}
```

The layout system handles sticky controls by:
1. Measuring sticky-top controls first
2. Measuring sticky-bottom controls
3. Remaining space goes to scrollable content

---

## Integration Points

### Window Integration

`Window.cs` uses the DOM system:

```csharp
// In Window.Render()
private void RenderWithDOM()
{
    // 1. Rebuild tree if controls changed
    if (_layoutDirty)
    {
        RebuildLayoutTree();
        _layoutDirty = false;
    }

    // 2. Measure pass
    var constraints = new LayoutConstraints(0, contentWidth, 0, contentHeight);
    _layoutRoot.Measure(constraints);

    // 3. Arrange pass
    _layoutRoot.Arrange(new LayoutRect(0, 0, contentWidth, contentHeight));

    // 4. Paint pass
    _contentBuffer.Clear(backgroundColor);
    _layoutRoot.Paint(_contentBuffer, clipRect, foregroundColor, backgroundColor);

    // 5. Output to console
    FlushToConsole();
}
```

### Control Implementation Pattern

Every control follows this pattern:

```csharp
public class MyControl : IWindowControl, IDOMPaintable
{
    // Fields
    private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
    private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
    private Margin _margin = new Margin(0, 0, 0, 0);
    // ... other fields

    // IWindowControl properties
    public HorizontalAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set { _horizontalAlignment = value; Container?.Invalidate(true); }
    }

    public VerticalAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set { _verticalAlignment = value; Container?.Invalidate(true); }
    }

    // IDOMPaintable implementation
    public LayoutSize MeasureDOM(LayoutConstraints constraints)
    {
        // Calculate desired size based on content
        int width = CalculateContentWidth();
        int height = CalculateContentHeight();

        return new LayoutSize(
            Math.Clamp(width + _margin.Left + _margin.Right,
                       constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(height + _margin.Top + _margin.Bottom,
                       constraints.MinHeight, constraints.MaxHeight)
        );
    }

    public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds,
                         LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        // Calculate content area (inside margins)
        int startX = bounds.X + _margin.Left;
        int startY = bounds.Y + _margin.Top;
        int contentWidth = bounds.Width - _margin.Left - _margin.Right;
        int contentHeight = bounds.Height - _margin.Top - _margin.Bottom;

        // Paint content
        // ... render to buffer using buffer.SetCell(), buffer.WriteString(), etc.
    }
}
```

---

## AnsiParser

Converts ANSI escape sequences to Cell arrays for rendering:

```csharp
public static class AnsiParser
{
    /// <summary>
    /// Parses an ANSI string into a sequence of cells with colors.
    /// </summary>
    public static IEnumerable<Cell> Parse(string ansiString,
                                           Color defaultFg, Color defaultBg);
}
```

Used when controls render Spectre.Console markup or other ANSI-formatted content.

---

## Invalidation

Controls call `Container?.Invalidate(true)` when their properties change:

```csharp
public int? Width
{
    get => _width;
    set
    {
        if (_width != value)
        {
            _width = value;
            Container?.Invalidate(true);  // Triggers re-layout
        }
    }
}
```

The invalidation propagates up through the container hierarchy to the Window, which marks the layout as dirty and triggers a re-render on the next frame.

---

## Coordinate Systems

1. **Logical coordinates**: Relative to control's content (0,0 = top-left of content)
2. **Bounds coordinates**: Relative to parent container
3. **Absolute coordinates**: Screen position (for hit-testing and cursor positioning)

```csharp
// LayoutNode provides both
public LayoutRect Bounds { get; }          // Relative to parent
public LayoutRect AbsoluteBounds { get; }  // Screen position
```

---

## Scrolling

Scrollable containers (like ListControl, TreeControl) handle scrolling internally:

1. `MeasureDOM` returns the full content size
2. `PaintDOM` receives the visible `bounds` and `clipRect`
3. Control paints only visible portion, offset by scroll position
4. Scroll indicators are painted when content exceeds viewport

```csharp
// Example in ListControl.PaintDOM
int scrollOffset = CurrentScrollOffset;
int visibleStart = scrollOffset;
int visibleEnd = Math.Min(scrollOffset + visibleItemCount, _items.Count);

for (int i = visibleStart; i < visibleEnd; i++)
{
    int paintY = startY + (i - scrollOffset);
    // Paint item at paintY
}
```

---

## Cursor Management

Controls that show a cursor implement `ILogicalCursorProvider`:

```csharp
public interface ILogicalCursorProvider
{
    Point? GetLogicalCursorPosition();
    void SetLogicalCursorPosition(Point position);
}
```

The `CursorStateService` translates logical positions to screen coordinates using the control's `AbsoluteBounds` from its `LayoutNode`.

---

## Future Considerations

### Deferred Work
- `ControlBounds.cs` still exists for legacy hit-testing (can be removed when Window.cs is fully updated)

### Potential Enhancements
- Virtual scrolling for large lists (only create nodes for visible items)
- Dirty region tracking for partial updates
- Animation support with interpolated layout

### Implemented
- [Portal system](PORTAL_SYSTEM.md) for dropdowns, menus, and arbitrary overlay content (render outside allocated bounds)

---

## Quick Reference

| Concept | Location | Purpose |
|---------|----------|---------|
| `IDOMPaintable` | `Layout/IDOMPaintable.cs` | Interface for DOM-aware controls |
| `LayoutNode` | `Layout/LayoutNode.cs` | Tree node with measure/arrange/paint |
| `CharacterBuffer` | `Layout/CharacterBuffer.cs` | Render target |
| `VerticalStackLayout` | `Layout/VerticalStackLayout.cs` | Vertical stacking algorithm |
| `HorizontalAlignment` | `Layout/ILayoutContainer.cs` | Left/Center/Right/Stretch |
| `VerticalAlignment` | `Layout/ILayoutContainer.cs` | Top/Center/Bottom/Fill |
| `AnsiParser` | `Helpers/AnsiParser.cs` | ANSI → Cell conversion |

---

*Last updated: Phase 8 cleanup complete - January 2025*
