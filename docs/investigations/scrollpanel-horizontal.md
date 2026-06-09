# Investigation: ScrollablePanelControl horizontal scrolling (scrollbar + keyboard)

**Status:** Root-caused and **fixed**
**Date:** 2026-06-09
**Symptom:** A `CanvasControl` larger than the viewport (`AutoSize=false`, `IsEnabled=false`),
sole child of a `ScrollablePanelControl` with both scroll modes = `Scroll` and the scrollbar
enabled, scrolls neither horizontally nor vertically and shows no horizontal scrollbar — even
though `CanScrollRight=True`.
**Tests:** `SharpConsoleUI.Tests/Controls/ScrollablePanelHorizontalTests.cs`

---

## Bug A — no horizontal scrollbar (and horizontal offset never applied)

### Trace: the scrollbar guards were vertical-only
Every scrollbar decision was gated on the **vertical** axis only:

```
_showScrollbar && _verticalScrollMode == ScrollMode.Scroll && _contentHeight > _viewportHeight
```

Occurrences (before the fix):
- `ScrollablePanelControl.Rendering.cs:104` (viewport reservation), `:260` (draw call), `:496`
  (`CalculateContentHeight` narrow re-measure)
- `ScrollablePanelControl.Scrolling.cs:33`
- `ScrollablePanelControl.Mouse.cs:46, 71, 211, 319`
- `ScrollablePanelControl.cs:428`

There was **no horizontal equivalent**: no `DrawHorizontalScrollbar`, no bottom-row reservation,
no `_contentWidth > _viewportWidth` scrollbar guard. Confirmed by grep — the only horizontal code
was the *offset* logic (`ScrollHorizontalBy`, `_horizontalScrollOffset`, `CanScrollLeft/Right`).

### Second half of Bug A: the offset was never painted
`PaintDOM` applied `currentY = -_verticalScrollOffset` to the child Y, but the child **X** was
fixed at `contentOriginX` — `_horizontalScrollOffset` was never subtracted. Worse, each child was
measured with `MaxWidth = contentWidth` (the viewport width), so a 240-wide canvas with
`HorizontalAlignment.Stretch` was *clamped to the viewport* at measure time — there was no
overflow to scroll through. So horizontal scrolling did nothing visually even when the offset
changed.

---

## Bug B — keyboard scrolling for a non-focusable child

### Focus owner
`ScrollablePanelControl.CanReceiveFocus` returns true when `NeedsScrolling()` is true
(`ScrollablePanelControl.Input.cs:325-339`), and `NeedsScrolling()` already covers the horizontal
axis (`Scrolling.cs:207-219`). So a panel whose only child is non-focusable **is** focusable, and
`GetInitialFocus` returning null (no focusable children) makes `FocusManager.SetFocus(panel)` fall
through to focus the panel itself. Headless repro confirmed the panel auto-focuses and
`HasFocus=True`.

So the panel *does* receive `ProcessKey`. The actual gaps were in which keys did horizontal work:
- **Left/Right arrows** already scrolled horizontally (`Input.cs:194-208`) — these worked.
- **Home/End/PageUp/PageDown** were **vertical-only** (`Input.cs:177-192`): when horizontal was the
  overflow axis they did a vertical scroll (a no-op when there is no vertical overflow), so the
  horizontal offset never moved on those keys.

Conclusion: the framework requirement holds — the panel must be focusable (it is, via
`NeedsScrolling`); a non-focusable child is fine and recommended for a pure-render canvas. The fix
is to make the paging/jump keys horizontal-aware.

---

## Fixes (all in `ScrollablePanelControl`)

1. **Single source of truth for scrollbar visibility** — `NeedsVerticalScrollbar` /
   `NeedsHorizontalScrollbar` (`ScrollablePanelControl.cs`), resolving the mutual dependency (a
   vertical bar steals `VerticalScrollbarColumns=2`; a horizontal bar steals
   `HorizontalScrollbarRows=1`). Public `HasVerticalScrollbar` / `HasHorizontalScrollbar` expose
   them.
2. **Reserve the bottom row** — `PaintDOM` resolves the H-scrollbar first (width-based), reduces
   `_viewportHeight` by one row, then measures `_contentHeight` against the reduced viewport so
   Fill children fill the *content* area exactly (no spurious vertical overflow from the scrollbar
   row). The vertical bar keeps its existing 2-column reservation in `contentWidth`.
3. **Apply the horizontal offset** — children are measured/arranged at
   `max(contentWidth, _contentWidth)` when horizontal scrolling is on, positioned at
   `contentOriginX - _horizontalScrollOffset`, and clipped to the content viewport (anchored at
   `contentOriginX`, so scrolled-off-left content is hidden).
4. **Draw the H-scrollbar** — `DrawHorizontalScrollbar` mirrors `DrawVerticalScrollbar`: track
   (`─`), thumb (`█`) sized from `trackWidth / _contentWidth`, positioned from
   `_horizontalScrollOffset`, end arrows `◄`/`►`. Drawn on the reserved bottom row; the vertical
   bar occupies the right column, so the corner cell is not double-drawn (the H-bar spans only the
   content width, which already excludes the vertical bar's columns).
5. **Horizontal-aware paging/jump keys** — Home/End/PageUp/PageDown act vertically when
   `VerticalIsScrollable`, else fall back to horizontal when `HorizontalIsScrollable`
   (`ScrollablePanelControl.Input.cs`). Added `ScrollHorizontalTo` (mirrors `ScrollVerticalTo`).

Vertical scrolling and the focusable-child case are unchanged: the vertical guards now read
`NeedsVerticalScrollbar` (same predicate), the H-row is reserved only when horizontal actually
overflows, and the child-first key delegation in `ProcessKey` is untouched.

---

## Consumer requirement

- The **panel** must be able to hold focus to receive scroll keys — it is, automatically, when it
  needs scrolling (`NeedsScrolling` → `CanReceiveFocus`). No extra setup needed.
- A **non-focusable child** (`IsEnabled=false`) is fine and recommended for pure-render canvases —
  the panel owns scrolling.
- To scroll a canvas, set `HorizontalScrollMode=Scroll` / `VerticalScrollMode=Scroll`,
  `ShowScrollbar=true`, and give the canvas an explicit size with `AutoSize=false`. (A
  `VerticalAlignment.Fill` canvas fills the viewport and won't drive vertical scroll — see
  `canvas-in-scrollpanel.md`.)

---

## Tests

| Test | Asserts |
|------|---------|
| `PanelWithNonFocusableChild_IsFocusable_AndAutoFocuses` | child non-focusable; panel focusable + holds focus |
| `RightArrow_ScrollsHorizontally` | Right increases `HorizontalScrollOffset` |
| `End_ScrollsToFarRight_WhenHorizontalIsTheOverflowAxis` | End jumps to `ContentWidth - ViewportWidth` (horizontal) |
| `RightArrow_ClampsAtMaxOffset` | offset clamps; `CanScrollRight` false at the end |
| `HorizontalScrollbar_IsRendered_OnBottomRow` | `HasHorizontalScrollbar` true |
| `HorizontalScrollbar_GlyphsAppearInRenderedBuffer` | `◄ … ►` + track/thumb glyphs present in the rendered buffer |
| `NoHorizontalScrollbar_WhenContentFitsWidth` | no H-scrollbar when content fits |

Full suite: **3546 passing**, `dotnet build` clean.
