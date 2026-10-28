# Window Layout

Every window has a **root layout**: the thing that turns its children into a picture. It
decides which children claim edges, which share the scrolling middle, and what scrolling even
means for that window.

The root is chosen per window and can be replaced.

---

## The default: `DockRootLayout`

Unless told otherwise, a window uses `DockRootLayout`: four edges around a scrolling body.

```csharp
window.DockTop.Add(toolbar);
window.DockLeft.Add(sidebar);
window.DockBottom.Add(statusBar);
window.DockBody.Add(content);     // the scrolling middle
```

A child added with `window.AddControl(...)` and no edge goes to the body, which is why
ordinary code needs to know none of this. `StickyTop()` / `StickyBottom()` map onto the top
and bottom edges, so existing applications keep working unchanged.

The edges, the surfaces and the allocation rule are documented in
**[Docking](DOCKING.md)**.

---

## Replacing the root

### For one window

```csharp
window.Root = () => new DockRootLayout();   // this window only
window.Root = null;                          // back to the application default
```

### For the application

```csharp
WindowRenderer.RootLayoutFactory = () => new MyRootLayout();
```

`Root` is a **factory, not an instance**. The renderer rebuilds its root whenever the DOM tree
is rebuilt, and a shared instance would carry one window's banding and scroll position into
the next rebuild. Assigning takes effect immediately — the window rebuilds its tree.

---

## Writing a root layout

A root implements `IWindowRootLayout`:

```csharp
public interface IWindowRootLayout : ILayoutContainer, IRegionClippingLayout
{
    int ScrollOffset { get; set; }
    int ScrollableContentHeight { get; }
    int MaxScrollOffset { get; }

    void ScrollBy(int delta);
    void ScrollToTop();
    void ScrollToBottom();
    void PageUp();
    void PageDown();
}
```

Three parts, and only the first is unusual:

**Scrolling is part of the contract.** A root that bands its children is the only thing that
knows *which* band scrolls — the window cannot work it out from outside. A root with no
scrolling region reports a zero range and ignores the verbs; `window.ScrollBy()` then does
nothing, which is the honest answer for a layout that does not scroll.

**`ILayoutContainer`** is the ordinary measure/arrange pair every container implements. See
[DOM layout](DOM_LAYOUT_SYSTEM.md).

**`IRegionClippingLayout`** lets the root clip each child separately. A root with pinned edges
needs this: without it, scrolling body content paints straight over a docked toolbar once the
body is long enough to reach it.

### A minimal example

A root that stacks children top to bottom and never scrolls:

```csharp
public sealed class PlainStackRoot : IWindowRootLayout
{
    public int ScrollOffset { get; set; }
    public int ScrollableContentHeight => 0;
    public int MaxScrollOffset => 0;

    public void ScrollBy(int delta) { }
    public void ScrollToTop() { }
    public void ScrollToBottom() { }
    public void PageUp() { }
    public void PageDown() { }

    public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
    {
        int y = 0;
        foreach (var child in node.Children)
        {
            if (!child.IsVisible) continue;
            var size = child.Measure(new LayoutConstraints(
                0, constraints.MaxWidth, 0, Math.Max(0, constraints.MaxHeight - y)));
            y += size.Height;
        }
        return new LayoutSize(constraints.MaxWidth, constraints.MaxHeight);
    }

    public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
    {
        int y = 0;
        foreach (var child in node.Children)
        {
            if (!child.IsVisible) { child.Arrange(LayoutRect.Empty); continue; }
            int h = child.DesiredSize.Height;
            child.Arrange(new LayoutRect(0, y, finalRect.Width, h));
            y += h;
        }
    }

    public LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect)
        => parentClipRect;
}
```

```csharp
window.Root = () => new PlainStackRoot();
```

---

## Available root layouts

| Root | Shape | Scrolls |
|------|-------|---------|
| **`DockRootLayout`** (default) | Four edges around a body | the body |

One ships today. The seam exists so that is not a permanent answer — a tabbed root, a split
root or a fixed-pane root are all expressible against the same interface, and adding one
requires no change to `Window` or the renderer.

---

## How a window reaches its root

```
Window
  └── WindowRenderer
        └── IWindowRootLayout          ← window.Root, or RootLayoutFactory
              ├── docked children      (DockRootLayout: edge strips)
              └── body node            (a ScrollLayout over the undocked children)
                    └── your controls
```

The window's scroll API is a facade over the root:

```csharp
window.ScrollOffset        // → root.ScrollOffset
window.ScrollBy(5)         // → root.ScrollBy(5)
window.TotalLines          // → root.ScrollableContentHeight
```

Below the root, every container chooses its own layout by control type through
`LayoutNodeFactory` — grid, scroll, stack, tab, collapsible. The root was the last node that
did not, and making it polymorphic is what `IWindowRootLayout` is for.

---

## Border-delegated scrollbars

A scrolling region can hand its scrollbar to the window frame, reclaiming the column or row
it would otherwise spend drawing one. Covered in [Docking](DOCKING.md#border-delegated-scrollbars).

---

## See also

- [Docking](DOCKING.md) — the edges, the surfaces, and the allocation rule
- [DOM layout](DOM_LAYOUT_SYSTEM.md) — how measure, arrange and paint work
- [Windows](WINDOWS.md) — window lifecycle and the rest of the window API
- [GridControl](controls/GridControl.md) — docking inside a grid
