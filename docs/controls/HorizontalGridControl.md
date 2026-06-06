# HorizontalGridControl

Multi-column layout container that arranges child columns horizontally with optional draggable splitters between them.

## Overview

HorizontalGridControl is a layout container that places its children side by side in columns. Each column is a `ColumnContainer` that can hold one or more controls and can be sized with a fixed width, minimum/maximum widths, or a flex factor that distributes remaining space proportionally. This makes it the primary building block for side-by-side layouts such as master/detail views, IDE-style panes, status bars, and dialog button rows.

Splitters can be inserted between adjacent columns to let the user resize them at runtime. A splitter is a focusable Tab stop: when focused, the Left/Right arrow keys move it by one column (Shift+Left/Right moves by five), and it can also be dragged with the mouse. When a column adjacent to a splitter becomes hidden, the grid automatically releases the neighbor's explicit width so it can flex to fill the freed space, restoring the saved width when both columns become visible again.

The grid itself is a transparent focus scope: it never receives focus directly (`CanReceiveFocus` is always `false`). Instead, Tab navigation flows through the focusable controls inside the columns in left-to-right order, with each column's splitter inserted as a Tab stop after it. Nested scopes such as `ScrollablePanelControl` and inner `HorizontalGridControl` instances are entered transparently during traversal. The grid uses the internal `HorizontalLayout` algorithm for measuring and arranging columns; this is assigned automatically by the window during tree building and is not something you interact with directly.

See also: [TabControl](TabControl.md), [ScrollablePanelControl](ScrollablePanelControl.md)

## Quick Start

```csharp
var grid = Controls.HorizontalGrid()
    .Column(col => col.Width(30).Add(list))
    .Column(col => col.Flex().Add(detailPanel))
    .WithSplitterAfter(0)
    .WithAlignment(HorizontalAlignment.Stretch)
    .Build();

window.AddControl(grid);
```

## Builder API

### Columns

```csharp
Controls.HorizontalGrid()
    .Column(col => col.Width(48).Add(control1))          // Fixed-width column
    .Column(col => col.Flex(2.0).Add(control2))          // Flexible column (2x share)
    .Column(col => col.MinWidth(10).MaxWidth(40)         // Width constraints
                      .Add(control3));
```

The `Column(Action<ColumnBuilder>)` method configures a single column. The `ColumnBuilder` exposes:

```csharp
col.Width(int width)            // Fixed width in characters
col.MinWidth(int minWidth)      // Minimum width in characters
col.MaxWidth(int maxWidth)      // Maximum width in characters
col.Flex(double factor = 1.0)   // Flex factor for distributing remaining space
col.Add(IWindowControl control) // Add a control to the column (call repeatedly to stack)
```

### Scrollable Columns

`AsScrollable()` wraps a column's contents in a `ScrollablePanelControl`; subsequent `Add()` calls target that panel. The scroll-configuration helpers auto-enable scrollable mode when needed:

```csharp
col.AsScrollable(panel => { /* configure ScrollablePanelControl */ })
col.WithScrollbar(bool show, ScrollbarPosition position = ScrollbarPosition.Right)
col.WithMouseWheel(bool enable)
col.WithVerticalScroll(ScrollMode mode)
col.WithHorizontalScroll(ScrollMode mode)
```

### Splitters

```csharp
.WithSplitterAfter(int columnIndex)   // Insert a draggable splitter after the given column
```

### Alignment and Layout

```csharp
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.WithMargin(int left, int top, int right, int bottom)
.WithMargin(int margin)
.WithMargin(Margin margin)
.WithStickyPosition(StickyPosition position)
.StickyTop()
.StickyBottom()
```

### Identity and Visibility

```csharp
.WithName(string name)   // Name for FindControl queries
.WithTag(object tag)     // Custom data storage
.Visible(bool visible = true)
.Build()                 // Builds and returns the HorizontalGridControl
```

### Factory Methods (Static)

`HorizontalGridControl` also offers static factory methods for common patterns:

```csharp
// Centered dialog button row
HorizontalGridControl.ButtonRow(okButton, cancelButton);
HorizontalGridControl.ButtonRow(buttons, HorizontalAlignment.Center);

// Wrap arbitrary controls, one per column
HorizontalGridControl.FromControls(control1, control2, control3);
HorizontalGridControl.FromControls(controls, HorizontalAlignment.Left);

// Fluent builder entry point (equivalent to Controls.HorizontalGrid())
HorizontalGridControl.Create();
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Columns` | `List<ColumnContainer>` | empty | Snapshot copy of the columns in the grid (read-only snapshot) |
| `Splitters` | `IReadOnlyList<SplitterControl>` | empty | Snapshot copy of the splitters in the grid (read-only) |
| `FocusedContent` | `IInteractiveControl?` | `null` | Currently focused child control within the grid (read-only) |
| `HorizontalAlignment` | `HorizontalAlignment` | `Left` | Horizontal alignment of the grid |
| `VerticalAlignment` | `VerticalAlignment` | `Top` | Vertical alignment of the grid |
| `BackgroundColor` | `Color?` | `null` | Background color (transparent when null) |
| `ForegroundColor` | `Color?` | `null` | Foreground color (resolved from container/theme when null) |
| `Width` | `int?` | `null` | Fixed width (clamped to a minimum of 0; auto-sized when null) |
| `Visible` | `bool` | `true` | Whether the grid is visible |
| `IsEnabled` | `bool` | `true` | Enables/disables keyboard and mouse handling |
| `HasFocus` | `bool` | `false` | True when this grid or one of its descendants is focused (read-only) |
| `CanReceiveFocus` | `bool` | `false` | Always false — the grid is a transparent container; focus goes to column contents |
| `CanFocusWithMouse` | `bool` | `IsEnabled` | Whether the grid participates in mouse focus (read-only) |
| `WantsMouseEvents` | `bool` | `IsEnabled` | Whether the grid receives mouse events (read-only) |
| `PreferredCursorShape` | `CursorShape?` | from focused child | Cursor shape requested by the focused child (read-only) |
| `SavedFocus` | `IFocusableControl?` | `null` | Required by `IFocusScope`; intentionally ignored — the grid always re-enters at its first/last focusable child |
| `ContentWidth` | `int?` | computed | Sum of column and splitter widths plus margins (read-only) |

## Events

The following mouse events are declared (from `IMouseAwareControl`). Note that `MouseRightClick` and `MouseClick` are raised only when a grid-level click does not land on a child control; the remaining events are present for interface compatibility and are not raised by the grid itself.

| Event | Arguments | Description |
|-------|-----------|-------------|
| `MouseClick` | `MouseEventArgs` | Raised on a left click that does not hit a child control |
| `MouseRightClick` | `MouseEventArgs` | Raised on a right click that does not hit a child control |
| `MouseDoubleClick` | `MouseEventArgs` | Declared for interface compatibility (not raised by the grid) |
| `MouseEnter` | `MouseEventArgs` | Declared for interface compatibility (not raised by the grid) |
| `MouseLeave` | `MouseEventArgs` | Declared for interface compatibility (not raised by the grid) |
| `MouseMove` | `MouseEventArgs` | Declared for interface compatibility (not raised by the grid) |

Splitters expose their own `SplitterMoved` / `SplitterMovedAsync` events on the `SplitterControl` instances.

## Methods

### Column Management

| Method | Description |
|--------|-------------|
| `AddColumn(ColumnContainer column)` | Add a column to the grid |
| `AddColumnWithSplitter(ColumnContainer column)` | Add a column and automatically create a splitter before it (no splitter for the first column); returns the created `SplitterControl?` |
| `RemoveColumn(ColumnContainer column)` | Remove a column and any splitters connected to it |
| `ClearColumns()` | Remove all columns and splitters |

### Splitter Management

| Method | Description |
|--------|-------------|
| `AddSplitter(int leftColumnIndex, SplitterControl splitter)` | Add a splitter between two adjacent columns by index; returns `false` if the index is invalid |
| `AddSplitterAfter(ColumnContainer column, SplitterControl? splitter = null)` | Add a splitter after the given column (creates a new splitter if null); returns `false` if not found or last column |
| `AddSplitterBefore(ColumnContainer column, SplitterControl? splitter = null)` | Add a splitter before the given column; returns `false` if not found or first column |
| `GetSplitterLeftColumnIndex(SplitterControl splitter)` | Get the index of the column to the left of a splitter, or -1 if not found |

### Animation

| Method | Description |
|--------|-------------|
| `AnimateColumnWidth(int columnIndex, int targetWidth, TimeSpan duration, EasingFunction? easing = null)` | Animate a column's width via an integer tween (defaults to `EaseOut`); returns an `IAnimation?` handle, or applies the width immediately if no animation manager is available |

### Container / Focus

| Method | Description |
|--------|-------------|
| `GetChildren()` | Ordered list of children (columns interleaved with their splitters) for Tab traversal |
| `GetInitialFocus(bool backward)` | Returns the first (or last) focusable child when focus enters the grid |
| `GetNextFocus(IFocusableControl current, bool backward)` | Returns the next/previous focusable child within the grid |

## Keyboard Support

The grid implements `IInteractiveControl` and routes keys to its focused child first, then handles Tab navigation itself.

| Key | Action |
|-----|--------|
| **Tab** | Move focus to the next focusable control within the grid (advances to next sibling when the grid is exhausted) |
| **Shift+Tab** | Move focus to the previous focusable control within the grid |

When a splitter has focus, it handles its own keys:

| Key | Action |
|-----|--------|
| **Left Arrow** | Move the splitter one column to the left (shrink left column) |
| **Right Arrow** | Move the splitter one column to the right (grow left column) |
| **Shift+Left Arrow** | Move the splitter 5 columns to the left |
| **Shift+Right Arrow** | Move the splitter 5 columns to the right |

## Mouse Support

The grid implements `IMouseAwareControl`.

- **Click** in a column routes the event to the control under the cursor and sets focus on it if it can receive focus.
- **Click/drag** on a splitter routes the event to that splitter, allowing interactive resizing of the adjacent columns.
- Wheel and motion events that bubble up from a child (e.g. a scrollable panel at its scroll limit) do **not** steal focus.
- A left or right click that lands on empty grid area (no child control) raises `MouseClick` / `MouseRightClick`.

## Examples

### Dialog Button Row

```csharp
var okButton = Controls.Button("OK").Build();
var cancelButton = Controls.Button("Cancel").Build();

var buttons = HorizontalGridControl.ButtonRow(okButton, cancelButton);
window.AddControl(buttons);
```

### Master/Detail Layout with a Resizable Splitter

```csharp
var grid = Controls.HorizontalGrid()
    .Column(col => col.Width(30).Add(list))
    .Column(col => col.Flex().Add(detailPanel))
    .WithSplitterAfter(0)
    .WithAlignment(HorizontalAlignment.Stretch)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .Build();

window.AddControl(grid);
```

### Proportional (Flex) Columns

```csharp
// The first column gets 3 shares of the available width, the second gets 1.
var grid = Controls.HorizontalGrid()
    .WithAlignment(HorizontalAlignment.Stretch)
    .Column(col => col.Flex(3).Add(mainPanel))
    .Column(col => col.Flex(1).Add(sidebar))
    .Build();
```

### Status Bar with Fixed and Filling Columns

```csharp
var statusBar = Controls.HorizontalGrid()
    .WithAlignment(HorizontalAlignment.Left)
    .StickyBottom()
    .Column(c => c.Width(20).Add(fileStatus))
    .Column(c => c.Width(16).Add(positionStatus))
    .Column(c => c.Width(8).Add(Controls.Label("UTF-8")))
    .Column(c => c.Add(Controls.Label("")))   // Spacer column
    .Column(c => c.Width(10).Add(timeStatus))
    .Build();

window.AddControl(statusBar);
```

### Scrollable Column

```csharp
var grid = Controls.HorizontalGrid()
    .Column(col => col.Width(28).Add(treeView))
    .Column(col => col
        .Flex()
        .AsScrollable()
        .WithScrollbar(true)
        .WithVerticalScroll(ScrollMode.Auto)
        .Add(longContent))
    .WithSplitterAfter(0)
    .WithAlignment(HorizontalAlignment.Stretch)
    .Build();
```

### Animating a Column (Collapsible Sidebar)

```csharp
var grid = Controls.HorizontalGrid()
    .Column(col => col.Width(0).Add(sidebar))   // Start collapsed
    .Column(col => col.Flex().Add(content))
    .Build();

window.AddControl(grid);

// Expand the sidebar to 30 columns over 250ms
grid.AnimateColumnWidth(0, targetWidth: 30, duration: TimeSpan.FromMilliseconds(250));

// Collapse it again (animating to width 0 hides the column on completion)
grid.AnimateColumnWidth(0, targetWidth: 0, duration: TimeSpan.FromMilliseconds(250));
```

## Best Practices

1. **Mix fixed and flexible columns**: Give navigation/list panes a fixed `Width()` and let the main content column use `Flex()` to absorb remaining space.
2. **Add splitters by index after building columns**: `WithSplitterAfter(0)` is the simplest way to make the boundary between the first two columns user-resizable.
3. **Use `Stretch` alignment for full-width layouts**: Combine `WithAlignment(HorizontalAlignment.Stretch)` with a flex column so the grid fills the window width.
4. **Use a spacer column for status bars**: An empty flex column (or a column with an empty label) pushes the following fixed-width columns to the right edge.
5. **Wrap tall content in scrollable columns**: Use `AsScrollable()` plus `WithVerticalScroll(ScrollMode.Auto)` so a column scrolls instead of overflowing.
6. **Animate width from background threads safely**: Width changes triggered by user input are fine on the UI thread; if you trigger `AnimateColumnWidth` from a background thread, marshal the call with `EnqueueOnUIThread`.

## See Also

- [TabControl](TabControl.md) - For switchable multi-page layouts
- [ScrollablePanelControl](ScrollablePanelControl.md) - Recommended container for scrollable column content

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
