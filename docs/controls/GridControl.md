# GridControl

A WinUI-`<Grid>`-style two-dimensional layout container for the terminal: arrange child controls into rows and columns with fixed, size-to-content, or proportional tracks, span cells across rows and columns, add CSS-style gaps, and style each cell individually with a background, border, and padding.

## Overview

GridControl is a pure layout container. It divides its area into a grid of rows and columns — each sized as a fixed number of cells, `Auto` (sized to its content), or `Star` (a proportional share of the leftover space) — and places one child control per cell. Cells can span multiple rows and/or columns, and the grid honours each child's `HorizontalAlignment`/`VerticalAlignment` (default `Stretch`/`Fill`) and `Margin` within its cell. This makes it the building block for dashboards, tiled status displays, settings forms, and any layout that is naturally a 2D matrix rather than a single row or column.

Children are placed explicitly with `grid.Place(control, row, col, rowSpan, colSpan)`, or appended in row-major **AutoFlow** order with `grid.AddControl(control)` (each lands in the next free cell, growing rows as needed). `RowGap`/`ColumnGap` insert blank tracks between cells (a spanned cell absorbs the gaps it crosses). The grid has its own `Margin` and `Padding`, and a content control marked `Wrap=true` reflows to its column width via a two-pass measure, growing `Auto` rows to the wrapped height.

A cell can hold **any** control — a `PanelControl`, `ListControl`, `LineGraphControl`, an editable `MultilineEditControl`/`PromptControl`, a `ScrollablePanelControl`, or even a nested grid. The grid is a full first-class container and focus scope: Tab moves focus row-major across cells (left-to-right, top-to-bottom), Shift+Tab reverses, a mouse click focuses the cell under the cursor, and the terminal cursor works in editable cells. The grid itself never scrolls (it is pure layout); compose it with `ScrollablePanelControl` for the WinUI `<Grid>`-in-`<ScrollViewer>` pattern.

See also: [HorizontalGridControl](HorizontalGridControl.md), [ScrollablePanelControl](ScrollablePanelControl.md)

## Quick Start

```csharp
var grid = Controls.Grid()
    .Columns(GridLength.Star(1), GridLength.Star(1))
    .Rows(GridLength.Auto(), GridLength.Star(1))
    .RowGap(1)
    .ColumnGap(2)
    .Place(Controls.Markup("[bold]Header[/]").Build(), 0, 0, colSpan: 2)
    .Place(leftPanel, 1, 0)
    .Place(rightPanel, 1, 1)
    .Build();

window.AddControl(grid);
```

## Sizing Model

Every row and column is described by a `GridLength`, created with one of three factories:

| Factory | Type | Behavior |
|---------|------|----------|
| `GridLength.Cells(n, min?, max?)` | Fixed | Exactly `n` cells wide/tall |
| `GridLength.Auto(min?, max?)` | Auto | Sizes to fit the cell's content |
| `GridLength.Star(weight, min?, max?)` | Star | A proportional share of the leftover space, by `weight` |

`Star` tracks split whatever space remains after the `Fixed` and `Auto` tracks are sized, in proportion to their weights — `Star(2)` gets twice the share of `Star(1)`. Every factory takes optional `min`/`max` cell clamps.

```csharp
var grid = Controls.Grid()
    // A fixed 20-cell sidebar, then two content columns sharing the rest 1:2.
    .Columns(GridLength.Cells(20), GridLength.Star(1), GridLength.Star(2))
    // A toolbar row sized to its content, then a body row that fills the rest
    // (but never below 5 cells tall).
    .Rows(GridLength.Auto(), GridLength.Star(1, min: 5))
    .Place(sidebar, 1, 0)
    .Place(mainPanel, 1, 1)
    .Place(detailPanel, 1, 2)
    .Place(toolbar, 0, 0, colSpan: 3)
    .Build();
```

### Content-sized Star tracks (`ContentSizedStars`)

A `Star` track is *defined* as a share of leftover space, so it has no intrinsic size. When a grid is measured against an **unbounded** axis — i.e. a content-sizing parent asking "how big do you *want* to be?" (an auto-height stack, or any container that measures loose to discover natural size) — there is no finite space to take a share of, so by the WinUI contract a `Star` track **collapses to 0** at measure. That is correct for a general grid, but in terminal layouts (where content usually has a real natural size) it is a frequent surprise: a Star grid placed in a content-sizing parent can render as nothing.

Set `ContentSizedStars = true` to opt a grid out of that collapse: its `Star` tracks self-size to their content **at measure**, then still fan out to fill when arranged in a real, bounded box — *"content size at measure, fill at arrange."* It is **off by default** (standard WinUI behavior, unchanged for ordinary grids) and affects only the measure pass.

```csharp
var grid = Controls.Grid()
    .Rows(GridLength.Star(1))
    .Columns(GridLength.Star(1))
    .Build();
grid.ContentSizedStars = true; // self-size to content when a parent measures us unbounded
```

> `HorizontalGridControl` (a single-row grid) sets this internally — its flush column layout must self-size like the layout it replaced.

## Placement and Spanning

### Explicit placement

`Place(control, row, col, rowSpan = 1, colSpan = 1)` puts a control at a specific cell and returns the grid for fluent chaining. Spanning is first-class — a cell can occupy several rows and/or columns, absorbing any gaps it crosses.

```csharp
grid.Place(header, 0, 0, colSpan: 3);   // header spans all three columns
grid.Place(sidebar, 1, 0, rowSpan: 2);  // sidebar spans two rows
```

### AutoFlow

`AddControl(control)` (or the builder's `.Add(control)`) appends a control in row-major order: it lands in the next free cell given the current column count, and the grid grows row definitions automatically when the flow runs past the last defined row. AutoFlow is span-aware — it skips over cells already occupied by a spanning child.

```csharp
var grid = Controls.Grid()
    .Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Star(1))
    .Add(tile1)   // -> row 0, col 0
    .Add(tile2)   // -> row 0, col 1
    .Add(tile3)   // -> row 0, col 2
    .Add(tile4)   // -> row 1, col 0 (a new row is grown automatically)
    .Build();
```

## Gaps

`RowGap` and `ColumnGap` insert blank tracks between adjacent cells, like CSS grid gaps. They default to 0. A spanned cell absorbs the gaps it crosses, so spanning tiles stay visually continuous.

```csharp
var grid = Controls.Grid()
    .Columns(GridLength.Star(1), GridLength.Star(1))
    .Rows(GridLength.Star(1), GridLength.Star(1))
    .RowGap(1)       // one blank cell between rows
    .ColumnGap(2)    // two blank cells between columns
    .Build();
```

## Cells and Per-Cell Styling

`grid[row, col]` (the indexer) and `grid.Cell(row, col)` both return a `GridCell` — a lightweight value-type handle that reads and writes the grid's cell store directly. It is not a control and stores no state of its own, so writing through it mutates the grid. The handle lets you frame and fill individual cells without wrapping each child in a `PanelControl`.

| Member | Type | Description |
|--------|------|-------------|
| `Content` | `IWindowControl?` | Get the cell's control; set to place/replace it (keeping styling), or `null` to clear the content |
| `Background` | `Color?` | Per-cell background fill (`null` = no fill, cell shows through). Setting `null` is a no-op — use `ResetStyle()` to clear |
| `Border` | `BorderStyle` | Per-cell border; a non-`None` border draws a one-cell box and insets content. Default `None` |
| `Padding` | `Padding` | Insets the cell's content from the cell edges (or from inside the border). Default `Padding.None` |
| `Placement` | `GridPlacement?` | The cell's full placement (position, spans, styling), or `null` if the cell has neither content nor styling |
| `Row` / `Col` | `int` | The cell's top-left coordinate |
| `IsEmpty` | `bool` | `true` when the cell holds no content (a styled-but-empty cell still reports `true`) |
| `Clear()` | — | Drops the cell's content (equivalent to `Content = null`) |
| `ResetStyle()` | — | Clears the cell's background, border, and padding, keeping its content |

```csharp
// Frame the CPU tile with a rounded border and a subtle slate fill.
grid.Cell(1, 0).Border = BorderStyle.Rounded;
grid.Cell(1, 0).Background = new Color(40, 44, 60);
grid.Cell(1, 0).Padding = new Padding(1, 0, 1, 0);

// Replace a cell's content at runtime, keeping its styling.
grid[2, 1].Content = newPanel;

// Strip styling but keep content; or clear the cell entirely.
grid.Cell(1, 0).ResetStyle();
grid.Cell(2, 1).Clear();
```

A cell can be styled before it has any content — a styled empty cell carries chrome (border/background) with no control, so you can lay out framed tiles up front and fill them later.

## Splitters

A splitter is a draggable boundary between two adjacent tracks that lets the user resize them at runtime, like a WinUI `GridSplitter`. A column splitter "after N" sits on the boundary between column `N` and column `N+1`; a row splitter "after N" sits between row `N` and row `N+1`.

### Adding splitters

On the builder:

```csharp
.ColumnSplitterAfter(int index)   // splitter between column index and index+1
.RowSplitterAfter(int index)      // splitter between row index and index+1
```

On the control (runtime CRUD):

| Method | Description |
|--------|-------------|
| `AddColumnSplitterAfter(int index)` | Add a draggable boundary after column `index` |
| `AddRowSplitterAfter(int index)` | Add a draggable boundary after row `index` |
| `RemoveColumnSplitterAfter(int index)` | Remove the column splitter after `index` |
| `RemoveRowSplitterAfter(int index)` | Remove the row splitter after `index` |
| `ClearSplitters()` | Remove all splitters |

### Resize semantics

Dragging a splitter resizes **only the two adjacent tracks**; the total grid size is conserved (the rest of the layout never shifts). The result depends on the two tracks' types:

| Adjacent tracks | Behavior |
|-----------------|----------|
| `Star` \| `Star` | Weight is redistributed keeping the sum constant — both stay responsive and continue to reflow on window resize |
| `Fixed` \| `Fixed` | The boundary moves cells from one track to the other |
| `Star` \| `Fixed` | The `Star` side absorbs the change; the `Fixed` side is held |
| `Auto` | An `Auto` track bakes to `Fixed(currentSize)` on its first drag, then behaves as fixed |

Min/max clamps are respected, and the boundary stops at a neighbor's minimum so a track can never be dragged below its `min`.

### Input

- **Mouse:** drag the splitter handle.
- **Keyboard:** a splitter is a real focus stop, so **Tab / Shift+Tab** cycles onto it in reading order (column splitters interleave during the first row's pass; a row splitter follows its row's last cell). You can also focus one programmatically with `FocusColumnSplitter(int index)` / `FocusRowSplitter(int index)`. Once focused, use the arrow keys — **Left/Right** for a column splitter, **Up/Down** for a row splitter. Hold **Shift** for a ×5 step.

### Auto-gap

A splitter forces a `≥1`-cell gap on its axis so the handle has a home to live in. This does **not** change the `ColumnGap`/`RowGap` property values — it is only a floor applied where a splitter sits.

### Persistence

A resize writes the new size straight into the live `RowDefinitions`/`ColumnDefinitions`, so the new layout survives a re-render, and `Star` tracks still reflow proportionally when the window is resized.

### Colors

The highlight is **foreground-only**: the handle's **background is the same in every state** (idle / focused / hovered / dragging) — only the glyph (`║` / `═`) changes. By default everything is theme- and `ColorRole`-driven, so the splitter matches the rest of the grid's chrome with no manual colours:

- **Idle:** the grid's `ColorRole` border colour, **shaded dimmer**, so the resting handle reads as a quiet line.
- **Focused / dragging:** the **full-bright** role border (its focused state is brightened), drawn **bold** — a clear dim→bright, normal→bold highlight on the same hue, visible on every theme.

| Property | Type | Description |
|----------|------|-------------|
| `SplitterColor` | `Color?` | Idle handle glyph colour; `null` resolves to the grid's `ColorRole` border colour (shaded dimmer) |
| `SplitterFocusedForeground` | `Color?` | Glyph colour when focused/hovered; `null` resolves to the bright role border / theme accent |
| `SplitterDraggingForeground` | `Color?` | Glyph colour while dragging; `null` resolves the same as focused |
| `SplitterFocusedBackground` | `Color?` | Optional fixed handle background; `null` keeps it transparent. **Does not change between states** — it pins the constant background |
| `SplitterDraggingBackground` | `Color?` | Alias for pinning the same constant background (kept for API symmetry) |

All are `Color?`; a `null` value resolves to a theme/role default.

### Example

```csharp
var grid = Controls.Grid()
    .Columns(GridLength.Star(1), GridLength.Star(1))
    .Rows(GridLength.Auto(), GridLength.Star(1))
    .ColumnSplitterAfter(0)   // drag boundary between col 0 and 1
    .RowSplitterAfter(0)
    .Build();
```

## Gridlines

Gridlines are thin, passive rules drawn *between* columns and/or rows — a lighter alternative to per-cell borders, like a spreadsheet grid or an HTML table's inner borders. Like splitters they live in the track gap (enabling them auto-bumps the gap to `≥1`), and like cell borders they are fully theme- and `ColorRole`-driven. Unlike splitters they are not interactive.

### Enabling gridlines

Grid-level (a rule between *every* adjacent track on an axis):

```csharp
grid.ShowColumnGridlines = true;   // vertical │ between every column
grid.ShowRowGridlines = true;      // horizontal ─ between every row
```

Per-boundary (a rule at a *specific* boundary only); composes with the grid-level flags as a union:

| Method | Description |
|--------|-------------|
| `AddGridlineAfterColumn(int index)` | Vertical rule between column `index` and `index+1` |
| `AddGridlineAfterRow(int index)` | Horizontal rule between row `index` and `index+1` |
| `RemoveGridlineAfterColumn(int index)` / `RemoveGridlineAfterRow(int index)` | Remove a per-boundary rule |
| `HasGridlineAfterColumn(int index)` / `HasGridlineAfterRow(int index)` | Query a per-boundary rule |
| `ClearGridlines()` | Remove all per-boundary rules (does not change the `Show*` flags) |

### Style and colour

| Property | Type | Description |
|----------|------|-------------|
| `GridlineStyle` | `BorderStyle` | Box-drawing style. Default `Single` (`│ ─ ┼`); also `DoubleLine` (`║ ═ ╬`), `Rounded` |
| `GridlineColor` | `Color?` | Glyph colour; `null` resolves to the grid's `ColorRole` border colour **shaded dimmer** (a rule reads lighter than a full border). Set it for full border-weight colour |

Gridlines are static (no focus/hover reaction) — quiet structure, the same colour every frame.

### Junctions, spans, and splitters

Where a vertical and a horizontal rule cross, the correct junction glyph is chosen from which arms are present: `┼` interior, `┬`/`┴`/`├`/`┤` at an edge, `┌`/`┐`/`└`/`┘` at a corner. A cell that **spans** a boundary suppresses the rule (and adjusts the junction) across its span — e.g. a column-spanning header makes the boundary below it a `┬` rather than a `┼`.

A boundary that carries a **splitter wins**: the interactive splitter handle is drawn there and the gridline is suppressed at that boundary; gridlines fill every other boundary.

Lines are **centred** in their gap, so a `ColumnGap(2)`/`RowGap` grid shows symmetric rules (this applies to splitters too).

### Example

```csharp
var grid = Controls.Grid()
    .Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Star(1))
    .Rows(GridLength.Star(1), GridLength.Star(1))
    .ColumnGridlines()                  // rules between all columns
    .RowGridlines()                     // rules between all rows
    .GridlineStyle(BorderStyle.Single)  // │ ─ ┼
    .Build();
```

## Animation

A row or column track can be animated to a target size — smooth collapse/expand/retarget — reusing the window system's `AnimationManager`. The track is held at a fixed size during the tween and restores its original sizing type on completion (a `Star` track resumes proportional reflow, an `Auto` track returns to content sizing).

```csharp
IAnimation? AnimateColumnWidth(int columnIndex, int targetCells, TimeSpan duration, EasingFunction? easing = null);
IAnimation? AnimateRowHeight(int rowIndex,   int targetCells, TimeSpan duration, EasingFunction? easing = null);
```

- `targetCells` is in terminal cells; **collapse** is `Animate…(…, 0, …)`, expand passes the target width/height (the caller remembers the restore size).
- Returns the `IAnimation` handle (or `null` when no window/manager is available yet — the target is applied immediately). Default easing is `EaseOut`.
- Re-animating the same track auto-cancels its prior animation, so rapid toggles retarget cleanly.
- `GetColumnArrangedWidth(int index)` / `GetRowArrangedHeight(int index)` return a track's current arranged size (cells), handy for reading a size before collapsing it so it can be restored on expand.

```csharp
// Toggle a panel column: collapse to 0, or expand back to a remembered width.
int current = grid.GetColumnArrangedWidth(1);
if (current > 0) { saved = current; grid.AnimateColumnWidth(1, 0, TimeSpan.FromMilliseconds(250)); }
else             { grid.AnimateColumnWidth(1, saved ?? 16, TimeSpan.FromMilliseconds(250)); }
```

## Builder API

Create a builder with `Controls.Grid()` (or `new GridBuilder()`). A builder implicitly converts to a `GridControl`, so it can be passed directly where a control is expected.

### Tracks, Gaps, and Sizing

```csharp
.Columns(params GridLength[] columns)   // Column tracks, left to right
.Rows(params GridLength[] rows)          // Row tracks, top to bottom
.RowGap(int gap)                         // Blank cells between rows
.ColumnGap(int gap)                      // Blank cells between columns
.WithPadding(Padding padding)
.WithPadding(int left, int top, int right, int bottom)
.WithMargin(Margin margin)
.WithMargin(int left, int top, int right, int bottom)
.WithSize(int width, int height)
.WithWidth(int width)
.WithHeight(int height)
```

### Placement

```csharp
.Place(IWindowControl control, int row, int col, int rowSpan = 1, int colSpan = 1)
.Add(IWindowControl control)             // AutoFlow into the next free cell
```

### Splitters and Gridlines

```csharp
.ColumnSplitterAfter(int index)          // draggable boundary between columns index/index+1
.RowSplitterAfter(int index)             // draggable boundary between rows index/index+1
.ColumnGridlines(bool show = true)       // rules between every column
.RowGridlines(bool show = true)          // rules between every row
.GridlineAfterColumn(int index)          // rule at one column boundary
.GridlineAfterRow(int index)             // rule at one row boundary
.GridlineStyle(BorderStyle style)        // Single (default) / DoubleLine / Rounded
.GridlineColor(Color color)              // explicit gridline colour (else role-derived, dimmer)
```

### Alignment, Theming, and Identity

```csharp
.WithAlignment(HorizontalAlignment alignment)
.WithVerticalAlignment(VerticalAlignment alignment)
.WithColorRole(ColorRole role, ThemeMode? mode = null)
.Outline(bool outline = true)
.WithName(string name)                   // For FindControl queries
.Build()                                 // Builds and returns the GridControl
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RowDefinitions` | `IList<GridLength>` | empty | Live row track definitions; mutating at runtime rebuilds and invalidates the grid |
| `ColumnDefinitions` | `IList<GridLength>` | empty | Live column track definitions; mutating at runtime rebuilds and invalidates the grid |
| `RowGap` | `int` | `0` | Blank cells between adjacent rows |
| `ColumnGap` | `int` | `0` | Blank cells between adjacent columns |
| `Padding` | `Padding` | `Padding.None` | The grid's own inner padding |
| `HorizontalAlignment` | `HorizontalAlignment` | `Left` | Horizontal alignment of the grid within its container |
| `VerticalAlignment` | `VerticalAlignment` | `Top` | Vertical alignment of the grid within its container |
| `BackgroundColor` | `Color` | `Transparent` | Background fill (transparent shows through when unset) |
| `ForegroundColor` | `Color` | from theme | Foreground (text) colour for the grid and its children |
| `Width` | `int?` | `null` | Fixed width (auto-sized when null) |
| `Height` | `int?` | `null` | Fixed height (auto-sized when null) |
| `ColorRole` | `ColorRole` | `Default` | Semantic role tinting per-cell chrome (borders and surface fills) from the theme palette |
| `Outline` | `bool` | `false` | Renders role chrome in outline style |
| `ShowColumnGridlines` / `ShowRowGridlines` | `bool` | `false` | Draw a rule between every adjacent column / row (see [Gridlines](#gridlines)) |
| `GridlineStyle` | `BorderStyle` | `Single` | Box-drawing style for gridlines (`Single` → `│ ─ ┼`; `DoubleLine`; `Rounded`) |
| `GridlineColor` | `Color?` | `null` | Gridline glyph colour; `null` = role border shaded dimmer |
| `Visible` | `bool` | `true` | Whether the grid is visible |
| `IsEnabled` | `bool` | `true` | Enables/disables keyboard and mouse handling |
| `HasFocus` | `bool` | `false` | True when this grid or one of its descendants is focused (read-only) |

## Methods

### Placement and Cells

| Method | Description |
|--------|-------------|
| `Place(control, row, col, rowSpan = 1, colSpan = 1)` | Place a control at a cell with optional spanning; returns the grid for chaining |
| `this[int row, int col]` / `Cell(int row, int col)` | Returns a `GridCell` handle for getting/setting the cell's content and styling |

### Runtime CRUD

| Method | Description |
|--------|-------------|
| `AddControl(control)` | Append a control in row-major AutoFlow order (grows rows as needed) |
| `RemoveControl(control)` | Remove a control (other cells are left in place — no repacking) |
| `ReplaceControl(oldControl, newControl)` | Replace a control, keeping the old control's cell placement and spans |
| `RemoveAt(row, col)` | Remove the control whose placement starts at the given cell |
| `ClearControls()` | Remove all child controls |

### Splitters and Gridlines

| Method | Description |
|--------|-------------|
| `AddColumnSplitterAfter(int)` / `AddRowSplitterAfter(int)` | Add a draggable boundary (see [Splitters](#splitters)) |
| `RemoveColumnSplitterAfter(int)` / `RemoveRowSplitterAfter(int)` / `ClearSplitters()` | Remove splitters |
| `FocusColumnSplitter(int)` / `FocusRowSplitter(int)` | Make a splitter the keyboard target |
| `AddGridlineAfterColumn(int)` / `AddGridlineAfterRow(int)` | Add a per-boundary rule (see [Gridlines](#gridlines)) |
| `RemoveGridlineAfterColumn(int)` / `RemoveGridlineAfterRow(int)` / `ClearGridlines()` | Remove per-boundary rules |
| `HasGridlineAfterColumn(int)` / `HasGridlineAfterRow(int)` | Query a per-boundary rule |

### Animation

| Method | Description |
|--------|-------------|
| `AnimateColumnWidth(int index, int targetCells, TimeSpan duration, EasingFunction? easing = null)` | Animate a column to a target width (0 = collapse); returns `IAnimation?` (see [Animation](#animation)) |
| `AnimateRowHeight(int index, int targetCells, TimeSpan duration, EasingFunction? easing = null)` | Animate a row to a target height |
| `GetColumnArrangedWidth(int index)` / `GetRowArrangedHeight(int index)` | Current arranged track size in cells (or -1), e.g. to remember a size before collapsing |

`RowDefinitions` and `ColumnDefinitions` are live lists — adding, removing, clearing, or replacing entries at runtime rebuilds and invalidates the grid so the change shows on the next render.

> **Thread safety:** the grid locks its cell store internally, but mutate it from the UI thread per the library convention. From background work, marshal calls with `EnqueueOnUIThread`.

## Keyboard Support

The grid is a transparent focus scope: it routes keys to its focused child first, then handles Tab traversal itself.

| Key | Action |
|-----|--------|
| **Tab** | Move focus to the next focusable cell, row-major (left-to-right, top-to-bottom) |
| **Shift+Tab** | Move focus to the previous focusable cell |

When focus enters the grid it lands on the first (or, for Shift+Tab, the last) focusable cell; nested scopes such as a `ScrollablePanelControl` in a cell are entered transparently.

## Mouse Support

The grid implements `IMouseAwareControl`.

- **Click** in a cell routes the event to the control under the cursor and focuses it if it can receive focus.
- The terminal **cursor** works in editable cells (e.g. a `PromptControl` or `MultilineEditControl` placed in a cell).
- Wheel and motion events that bubble up from a child (e.g. a scrollable panel at its scroll limit) do not steal focus.

## Scrolling

The grid does **not** scroll itself — it is pure layout, and cells clip their content to the cell bounds. To make grid content scrollable, compose with [ScrollablePanelControl](ScrollablePanelControl.md), the WinUI `<Grid>`-in-`<ScrollViewer>` pattern:

```csharp
// Whole grid scrolls: put the grid inside a scrollable panel.
var scroller = Controls.ScrollablePanel()
    .AddControl(grid)
    .WithScrollbar(true)
    .Build();

// One cell scrolls: put a scrollable panel inside the cell.
grid.Place(Controls.ScrollablePanel().AddControl(longLog).Build(), 1, 2);
```

## ColorRole Theming

`WithColorRole(ColorRole.Primary)` (or the `ColorRole` property) themes the per-cell chrome — cell borders and the surface fill of cells that opt into chrome — from the active theme's role palette, so framed tiles re-tint when the theme changes. `Outline(true)` renders the role chrome in outline style. See [Control Roles](../THEMES.md#control-roles).

```csharp
var grid = Controls.Grid()
    .Columns(GridLength.Star(1), GridLength.Star(1))
    .WithColorRole(ColorRole.Primary)
    .Build();

grid.Cell(0, 0).Border = BorderStyle.Rounded;  // border tinted from the Primary role
```

GridControl is NativeAOT-clean.

## Examples

### Tiled Dashboard (Spans, Gaps, Per-Cell Styling)

A three-column grid with a header that spans all columns, a different control type in every tile, a tile that spans two rows, and per-cell borders and backgrounds.

```csharp
var header = Controls.Markup("[bold]System Dashboard[/]").WithMargin(1, 0, 1, 0).Build();

var cpuGraph = Controls.LineGraph()
    .WithTitle("CPU %")
    .WithColorRole(ColorRole.Info)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithMargin(1, 1, 1, 1)
    .Build();

var resourcePanel = Controls.ScrollablePanel()
    .AddControl(Controls.BarGraph().WithLabel("Mem ").WithValue(73).ShowValue().Build())
    .AddControl(Controls.BarGraph().WithLabel("Disk").WithValue(48).ShowValue().Build())
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .Build();

var alertsLog = Controls.ScrollablePanel()        // a scrollable cell
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .Build();

var services = Controls.List("Services")
    .AddItems("nginx", "postgres", "redis", "rabbitmq")
    .WithMargin(1, 1, 1, 1)
    .Build();

var commandPanel = Controls.ScrollablePanel()
    .AddControl(Controls.Prompt("hub> ").WithInputWidth(20).WithMargin(1, 1, 1, 1).Build())
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .Build();

var grid = Controls.Grid()
    .Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Star(1))
    .Rows(GridLength.Auto(), GridLength.Star(1), GridLength.Star(1))
    .RowGap(1)
    .ColumnGap(2)
    .WithColorRole(ColorRole.Primary)
    .WithPadding(1, 0, 1, 0)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithAlignment(HorizontalAlignment.Stretch)
    .Place(header, 0, 0, colSpan: 3)          // header spans all three columns
    .Place(cpuGraph, 1, 0)
    .Place(resourcePanel, 1, 1)
    .Place(alertsLog, 1, 2, rowSpan: 2)       // alerts span two rows
    .Place(services, 2, 0)
    .Place(commandPanel, 2, 1)
    .Build();

// Per-cell styling through the GridCell surface.
grid.Cell(1, 0).Border = BorderStyle.Rounded;        // frame the CPU tile
grid.Cell(2, 0).Border = BorderStyle.Single;         // frame the services tile
grid.Cell(1, 1).Background = new Color(40, 44, 60);  // slate fill behind the resource tile
grid.Cell(1, 2).Border = BorderStyle.Rounded;        // frame the spanning alerts tile

window.AddControl(grid);
```

### Settings Form (AutoFlow)

```csharp
var form = Controls.Grid()
    .Columns(GridLength.Auto(), GridLength.Star(1))   // labels size to content; inputs fill
    .RowGap(1)
    .Add(Controls.Label("Name:"))
    .Add(Controls.Prompt().Build())
    .Add(Controls.Label("Email:"))
    .Add(Controls.Prompt().Build())
    .Add(Controls.Label("Notes:"))
    .Add(Controls.MultilineEdit().Build())
    .Build();

window.AddControl(form);
```

### Replacing a Cell's Content at Runtime

```csharp
var grid = Controls.Grid()
    .Columns(GridLength.Star(1))
    .Rows(GridLength.Star(1))
    .Place(loadingSpinner, 0, 0)
    .Build();

window.AddControl(grid);

// Later, on the UI thread, swap in the loaded content.
grid[0, 0].Content = resultsPanel;
```

## Best Practices

1. **Reach for a grid when the layout is 2D**: use `GridControl` for matrices and tiled dashboards; use [HorizontalGridControl](HorizontalGridControl.md) for a single row of resizable columns and [ScrollablePanelControl](ScrollablePanelControl.md) for a single scrollable column.
2. **Mix track types**: give chrome (toolbars, headers) `Auto` rows, fixed sidebars `Cells(n)`, and the main content `Star` tracks so it absorbs leftover space.
3. **Use `Stretch`/`Fill` alignment for full-bleed grids**: combine `WithAlignment(HorizontalAlignment.Stretch)` and `WithVerticalAlignment(VerticalAlignment.Fill)` so the grid fills its window.
4. **Frame tiles with the cell surface, not nested panels**: `grid.Cell(r, c).Border` / `.Background` give per-cell chrome without wrapping each child in a `PanelControl`.
5. **Compose to scroll**: the grid never scrolls itself — wrap it (or a single cell) in a `ScrollablePanelControl`.
6. **Mutate on the UI thread**: `Place`, `AddControl`, cell writes, and `RowDefinitions`/`ColumnDefinitions` edits should run on the UI thread; from background work, marshal with `EnqueueOnUIThread`.

## See Also

- [HorizontalGridControl](HorizontalGridControl.md) - For a single row of variable-width, resizable columns
- [ScrollablePanelControl](ScrollablePanelControl.md) - Wrap a grid (or a cell) to make its content scrollable

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
