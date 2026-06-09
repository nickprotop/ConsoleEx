# Investigation: CanvasControl doesn't scroll inside ScrollablePanelControl

**Status:** Root-caused and **fixed**
**Date:** 2026-06-09
**Symptom:** A `CanvasControl` larger than the viewport, placed as the sole child of a
`ScrollablePanelControl` (both scroll modes = `Scroll`, scrollbar enabled), shows no scrollbars
and does not scroll. Downstream (LazyCaddy Topology): `CanvasWidth=248, CanvasHeight=33`,
`GetLogicalContentSize() = 248 × 1`.
**Repro / regression test:** `SharpConsoleUI.Tests/Controls/CanvasInScrollPanelTests.cs`

---

## Root cause

`CanvasControl` overrode `ContentWidth` (`CanvasControl.cs:66`) but **not**
`GetLogicalContentSize()`. The base implementation hardcodes height = 1:

```csharp
// BaseControl.cs:212-217
public virtual System.Drawing.Size GetLogicalContentSize()
{
    int width = ContentWidth ?? 0;
    int height = 1 + _margin.Top + _margin.Bottom;   // <-- the lie
    return new System.Drawing.Size(width, height);
}
```

So the canvas reported its logical height as **1** regardless of `CanvasHeight`. That one wrong
number suppressed scrolling through the panel's own logical size:

```csharp
// ScrollablePanelControl.cs:348-355 — the panel's logical size to ITS parent
public override System.Drawing.Size GetLogicalContentSize()
{
    int height = snapshot.Where(c => c.Visible)
                         .Sum(c => c.GetLogicalContentSize().Height);  // sums child = 1
    int width = Width ?? 80;
    return new System.Drawing.Size(width, height);   // panel reports height = 1
}
```

When the panel is **not** `VerticalAlignment.Fill`, its parent layout sizes the panel from this
logical height → the panel gets a **one-row slot** → `_viewportHeight ≈ 1` → there is nothing to
render or scroll, and at one row the horizontal extent is collapsed too — *both* scrollbars
vanish.

### Important nuance — two different height paths

There are two distinct ways the panel learns a child's height; only one used the buggy
`GetLogicalContentSize`:

1. **The panel's internal `_contentHeight`** (`CalculateContentHeight` → `MeasureChildrenHeight`
   → `ComputeChildHeight`) measures each child through the layout DOM:
   `LayoutNodeFactory.CreateSubtree(child)` → `LayoutNode.Measure` →
   `LayoutNode.MeasureControl` (`LayoutNode.cs:270-289`) → `control.MeasureDOM(constraints)`.
   `CanvasControl.MeasureDOM` (`CanvasControl.Rendering.cs:22-41`) **already reported the correct
   height** (`_canvasHeight` when not Fill). So a non-Fill canvas inside a **Fill** panel scrolled
   fine — its `_contentHeight` was correct via `MeasureDOM`, bypassing `GetLogicalContentSize`.

2. **The panel's own logical size to its parent** (`ScrollablePanelControl.GetLogicalContentSize`)
   sums children's `GetLogicalContentSize().Height` — this **did** use the buggy value. When the
   panel itself is non-Fill, this is what determines its slot, and height=1 starved it.

This is why the empirical repro depends on alignment:

| Config | panel logical H | `_contentHeight` (`TotalContentHeight`) | scrolls? |
|--------|-----------------|------------------------------------------|----------|
| canvas non-Fill, **panel Fill** | (panel gets window height) | 40 ✓ (via `MeasureDOM`) | yes (already worked) |
| canvas non-Fill, **panel non-Fill** | **1** (bug) → slot collapses | starved | **no — the bug** |
| **canvas Fill**, panel Fill | n/a | 13 = viewport (Fill collapses by design) | no (by design) |

The downstream report (`GetLogicalContentSize = 248 × 1`, "both scrollbars absent") matches the
**panel-non-Fill** row: the height=1 lie propagated through the panel's logical size.

---

## Scrollbar-decision path (file:line per hop)

Every scrollbar / `CanScroll*` decision is gated on `_contentHeight`/`_contentWidth` vs the
viewport. Those are produced in `PaintDOM`:

- `ScrollablePanelControl.Rendering.cs:66` — `_contentHeight = CalculateContentHeight(_viewportWidth, _viewportHeight)`
- `ScrollablePanelControl.Rendering.cs:67` — `_contentWidth = CalculateContentWidth()`
- `CalculateContentHeight` (`Rendering.cs:489-504`) → `MeasureChildrenHeight` (`:506-526`) →
  `ComputeChildHeight` (`Children.cs:219-232`) → `LayoutNode.Measure` → `MeasureControl`
  (`LayoutNode.cs:270-289`) → `CanvasControl.MeasureDOM` (`CanvasControl.Rendering.cs:22-41`).
- `CalculateContentWidth` (`Rendering.cs:528-542`) reads `child.GetLogicalContentSize().Width`
  directly (width was correct = 248, so horizontal extent was known *here*).

Vertical scrollbar visibility:
- `Rendering.cs:104` — `needsScrollbar = _showScrollbar && _verticalScrollMode == Scroll && _contentHeight > _viewportHeight`
- `CanScrollDown` — `ScrollablePanelControl.cs:245` — `_verticalScrollOffset < Max(0, _contentHeight - _viewportHeight)`
- `CanScrollRight` — `ScrollablePanelControl.cs:255` — `_horizontalScrollOffset < Max(0, _contentWidth - _viewportWidth)`

The panel's logical-size report (the suppression path for non-Fill panels):
- `ScrollablePanelControl.GetLogicalContentSize` — `ScrollablePanelControl.cs:348-355` — sums
  child `GetLogicalContentSize().Height` (= 1 before the fix).

### (a) Why the height=1 lie suppressed the vertical scrollbar
A non-Fill panel got a one-row slot from its parent (panel logical height = Σ child logical
heights = 1) → `_viewportHeight ≈ 1`, and the panel itself was never given the room to host or
overflow content → `needsScrollbar`/`CanScrollDown` false.

### (b) Why the horizontal scrollbar was also absent
Same one-row slot collapses the whole panel: with the panel laid out at ~1 row, there is no
content area to overflow horizontally either, and the scrollbar/decision code never runs against
a real viewport. The width was *measured* correctly (248) but a 1-row panel has nowhere to show a
horizontal scrollbar. So a single bad height number takes out both axes.

---

## Fix

Override `GetLogicalContentSize()` in `CanvasControl` to report the real buffer dimensions:

```csharp
// CanvasControl.cs
public override System.Drawing.Size GetLogicalContentSize() =>
    new(_canvasWidth + Margin.Left + Margin.Right,
        _canvasHeight + Margin.Top + Margin.Bottom);
```

**No `ContentHeight` override** — neither `IWindowControl` nor `BaseControl` declares a
`ContentHeight` member (only `ContentWidth`, which is `abstract`). Adding a `ContentHeight`
property would be a brand-new member that nothing reads; `GetLogicalContentSize()` is the method
the panel (and every other container) actually consumes, so the override there is the complete,
minimal fix.

**AutoSize safety:** when `AutoSize = true`, `PaintDOM` resizes `_canvasWidth`/`_canvasHeight` to
the assigned layout bounds (`CanvasControl.Rendering.cs:62-72`), so the reported logical size
tracks the slot and does not desync. Verified by `AutoSizeCanvas_LogicalSizeFollowsLayoutBounds`.

---

## Consumer-side requirement

`VerticalAlignment.Fill` (and `MeasureDOM`'s Fill branch, `CanvasControl.Rendering.cs:31-33`)
sizes the canvas **to the viewport** by design — a Fill canvas will never overflow, so it cannot
drive vertical scrolling. **To make a canvas drive the scroller, it must NOT be Fill:** set an
explicit `CanvasWidth`/`CanvasHeight`, `AutoSize = false`, and leave `VerticalAlignment` at its
default `Top` (and `HorizontalAlignment` non-Stretch if you want horizontal overflow too). The old
Topology config was AutoSize + Fill, which rendered fine but intentionally never scrolled; switching
to explicit-size + non-Fill (plus this fix) is what makes it scroll.

Pinned by `FillCanvas_DoesNotDriveVerticalScroll_ByDesign`.

---

## Tests (`CanvasInScrollPanelTests.cs`)

| Test | Asserts |
|------|---------|
| `Canvas_GetLogicalContentSize_ReportsRealBufferDimensions` | GLCS = 200 × 40 (was × 1) — **red before fix** |
| `Panel_LogicalHeight_ReflectsCanvasHeight_NotOne` | panel logical height = 40 (was 1) — **red before fix** |
| `FixedSizeCanvas_LargerThanViewport_DrivesPanelScrollExtent` | `TotalContentWidth/Height ≥ 200/40`, `CanScrollDown/Right` (end-to-end) |
| `NonFillCanvas_GetsItsRealHeightSlot_InPanelLayout` | the canvas's `ChildSlot.Height` = 40 (not 1, not viewport-collapsed) |
| `FillCanvas_DoesNotDriveVerticalScroll_ByDesign` | Fill canvas → `CanScrollDown` false (documents the contract) |
| `AutoSizeCanvas_LogicalSizeFollowsLayoutBounds` | AutoSize buffer follows the slot; logical size matches, no desync |

Full suite: **3539 passing**, `dotnet build` clean.
