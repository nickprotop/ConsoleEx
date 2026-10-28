# Docking and Window Root Layouts

Controls can claim an edge of their container instead of flowing with the rest of its
children. A docked child takes a strip off the container's rectangle, and whatever is left is
shared by everything else.

This works at two levels: the **window root**, and any **`GridControl`**.

---

## Quick start

```csharp
// At the window
window.DockTop.Add(Controls.Markup("Toolbar").Build());
window.DockLeft.Add(sidebar);
window.DockBottom.Add(statusBar);
window.DockBody.Add(content);          // the scrolling middle

// In a grid
grid.DockChild(toolbar, Dock.Top);
grid.DockChild(sidebar, Dock.Left);
grid.Place(editor, row: 0, col: 1);    // cells live inside what is left
```

Two demo pages show both: `dotnet run --project Examples/DemoApp -- --demo "Window Docking"`
and `--demo "Grid Docking"`.

---

## The four edges

```csharp
public enum Dock
{
    None,     // not docked - flows with the body
    Left,
    Top,
    Right,
    Bottom
}
```

`Dock` is a property of the **child**, not an instruction to the parent:

```csharp
var sidebar = Controls.Markup("Files").Build();
sidebar.Dock = Dock.Left;
```

That matters for two reasons. A control can be configured before it reaches a container, and
an existing property already works this way — see [Sticky is docking](#sticky-is-docking).

### Allocation order

Edges are claimed **in declaration order**, each shrinking the rectangle left for the rest:

```csharp
window.DockTop.Add(toolbar);     // claims the full width first
window.DockLeft.Add(sidebar);    // then a column of what remains, below the toolbar
```

Reverse those two lines and the sidebar runs full height while the toolbar spans only the
width beside it. Declaration order *is* the layout.

### Far edges keep author order

`Dock.Bottom` and `Dock.Right` children are allocated in reverse **within a run of the same
edge**, so the first one declared ends up nearest the content:

```csharp
window.DockBottom.Add(rule);        // paints ABOVE
window.DockBottom.Add(helpLine);    // paints BELOW
```

This departs from WPF's `DockPanel` deliberately. Under the strict rule each bottom child
consumes the bottom edge in turn, so a rule declared before a help line ends up *under* it —
correct by the algorithm, backwards to anyone writing a footer top to bottom.

The reversal applies within a run only. A bottom band declared before a sidebar is still
allocated before it, and so still spans the full width.

### Sizing

| Edge | Takes the child's | The child gets |
|------|-------------------|----------------|
| `Left` / `Right` | **width** | full height of what remains |
| `Top` / `Bottom` | **height** | full width of what remains |

A child with no explicit size reports its content size, so a left-docked label is as wide as
its text. **A container that stretches by default — a `PanelControl`, for instance — will
claim the entire rectangle unless you give it an explicit width:**

```csharp
window.DockLeft.Add(Controls.Panel()
    .WithHeader("Files")
    .WithWidth(20)        // without this the panel takes the whole window
    .AddControl(tree)
    .Build());
```

---

## At the window

A window exposes five surfaces:

```csharp
window.DockLeft
window.DockTop
window.DockRight
window.DockBottom
window.DockBody        // undocked children: the scrolling middle
window.Surface(edge)   // the same, chosen at runtime
```

Each is an `IDockSurface`:

```csharp
public interface IDockSurface : IEnumerable<IWindowControl>
{
    Dock Edge { get; }
    IReadOnlyList<IWindowControl> Controls { get; }
    int Count { get; }

    void Add(IWindowControl control);
    void Insert(int index, IWindowControl control);
    bool Remove(IWindowControl control);
    void Clear();
    bool Contains(IWindowControl control);
    IWindowControl this[int index] { get; }
}
```

A surface is a **live view over the window's child list**, not a second collection. Re-dock a
control and it moves between surfaces with nothing to notify:

```csharp
window.DockLeft.Add(panel);
Console.WriteLine(window.DockLeft.Count);    // 1
panel.Dock = Dock.Right;
Console.WriteLine(window.DockLeft.Count);    // 0
Console.WriteLine(window.DockRight.Count);   // 1
```

There is no registry to drift out of step, because there is only one list.

### The body scrolls

Undocked children share the middle, and that is the only region that scrolls. The window's
scroll API drives it:

```csharp
window.ScrollBy(5);
window.ScrollOffset = 0;
```

Docked edges never scroll. That is what makes them useful for toolbars and status bars.

---

## In a grid

A `GridControl` takes both placements at once. Docked children are allocated **outside the
tracks**; the track algorithm then runs on what is left.

```csharp
grid.DockChild(toolbar, Dock.Top);     // outside the tracks
grid.DockChild(sidebar, Dock.Left);    // outside the tracks
grid.Place(left,  row: 0, col: 0);     // inside them
grid.Place(right, row: 0, col: 1);

grid.Undock(sidebar);                  // returns the strip to the tracks
```

The method is `DockChild`, not `Dock`, because a grid also has a `Dock` property of its own -
the edge it claims in its parent. Full detail in
[GridControl](controls/GridControl.md#docking).

**Docking never shifts a cell coordinate.** A docked child claims no row and no column, so
`grid[1, 0]` means the same cell however many children are docked. This is why docks are
allocated outside the tracks rather than being turned into them: synthesising tracks from
docks would make every coordinate depend on how much was docked.

> `GridControl.HorizontalAlignment` defaults to `Left`, which sizes the grid to its content.
> Star tracks only divide the real width under `Stretch`.

---

## Sticky is docking

`StickyPosition` predates `Dock` and is still fully supported. It is now a **projection over
the same stored value**:

| `StickyPosition` | `Dock` |
|------------------|--------|
| `Top`            | `Dock.Top` |
| `Bottom`         | `Dock.Bottom` |
| `None`           | `Dock.None` |

Setting either property sets the other, because there is one field behind both:

```csharp
var bar = Controls.Markup("status").StickyBottom().Build();
Console.WriteLine(bar.Dock);              // Bottom

bar.Dock = Dock.Top;
Console.WriteLine(bar.StickyPosition);    // Top
```

Existing code needs no change. `StickyPosition` keeps its three values and its meaning, and
there are no plans to remove it. What `Dock` adds is the horizontal axis, which sticky never
had.

`Dock` reaches `IWindowControl` as a **default interface member**, so an external
implementation of that interface keeps compiling without defining it.

### Sticky inside a scrollable panel

A `ScrollablePanelControl` honors `StickyPosition` on its children: sticky children are
pinned to the top or bottom of the panel's viewport and the rest scroll between them.

```csharp
panel.AddControl(Controls.Markup("Header").StickyTop().Build());
foreach (var row in rows) panel.AddControl(row);
panel.AddControl(Controls.Markup("Footer").StickyBottom().Build());
```

Only the vertical edges pin. `Dock.Left` and `Dock.Right` fall through to the body and scroll
as ordinary children, because a panel stacks vertically and a horizontal edge has no band to
claim. For left/right docking inside a scrolling region, nest a `GridControl`.

---

## Replacing the window root

Docking is what the *default* root does. The root itself is replaceable per window, and a
different root can define different regions entirely. See
**[Window layout](WINDOW_LAYOUT.md)**.

---

## Border-delegated scrollbars

A scrolling region can hand its scrollbar to the window frame, reclaiming the column (or row)
it would otherwise spend drawing one:

```csharp
window.DelegateScrollbarToBorder(
    PanelBorderScrollSource.ForVertical(panel), ScrollAxis.Vertical);

window.DelegateScrollbarToBorder(null, ScrollAxis.Vertical);   // hand it back
```

Delegating hides the region's own scrollbar, so only the border draws one; releasing restores
it. The window's own body driving a thumb in the right border is this same arrangement, and
is what happens when nothing is delegated.

**One region per axis.** The border is a single track, so a second registration on the same
axis throws rather than silently winning. When two regions scroll independently, the primary
one takes the border and the other keeps its own bar.

Any region can drive the border by implementing `IBorderScrollSource`:

```csharp
public interface IBorderScrollSource
{
    int Offset { get; }     // how far scrolled, in the source's own units
    int Extent { get; }     // full size of the content
    int Viewport { get; }   // visible size
    void ScrollTo(int offset);
}
```

`PanelBorderScrollSource` implements it for `ScrollablePanelControl` as a stateless view, so
the thumb cannot drift from the content.

---

## Choosing between them

| You want | Use |
|----------|-----|
| A toolbar, sidebar or status bar around a scrolling body | Window dock surfaces |
| Edges around a 2D arrangement of cells | `GridControl` docking |
| A pinned header or footer inside a scrolling panel | `StickyPosition` on panel children |
| Rows and columns with no edges | `GridControl.Place` alone |
| A vertical stack | Add to the window, or a `ColumnContainer` |

Grid docking and window docking are the same rule at two levels. There is no separate
dock-panel control: a grid cell holds one control, and a grid with docked edges already does
everything such a control would, with real column gaps.

---

## See also

- [Window layout](WINDOW_LAYOUT.md) — root layouts, and writing your own
- [GridControl](controls/GridControl.md) — docking inside a grid
- [DOM layout](DOM_LAYOUT_SYSTEM.md) — how the layout tree measures and arranges
- [Windows](WINDOWS.md) — window lifecycle and the rest of the window API
- [Controls](CONTROLS.md) — the control catalog
