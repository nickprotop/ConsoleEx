# Pluggable window root layout — grid as the default, sticky as grid placement

**Date:** 2026-10-24
**Status:** Experiment. Design approved for implementation **in this clone only**.
**Location:** `~/source/ConsoleEx-rootlayout`, branch `experiment/pluggable-root`,
cloned from `~/source/ConsoleEx` at `07003029`. Its `origin` is the local repo, not
GitHub — nothing here can reach adopters by accident.
**Supersedes:** nothing. Extends the arc of `b310f0d5` (ScrollablePanel becomes a
layout-tree participant) and `d2f186ed` (one scrollbar engine).

## How this experiment runs

This is a **throwaway clone**. The library is freely modifiable: public API may be
cut, `WindowContentLayout` may be deleted outright, and the window scroll surface
(`Window.ScrollOffset`, `IsScrollable`, `TotalLines`, `GetVisibleHeightForControl`
— ~69 internal call sites between them) is explicitly in scope for removal if that
makes the model cleaner.

The no-breaking-changes rule does **not** bind inside the clone. It binds on what
gets applied back. Those are deliberately separate decisions: the experiment's job
is to find the clean model, and a compatibility layer designed before the model is
known is a guess.

**Applying back is a later decision requiring its own approval.** The experiment
produces evidence and a recommendation, not a merge-ready branch.

### Opportunity scope

Rewriting the window root is a rare opening. Changes that could never justify
their own release — because they touch core layout, or add public surface for its
own sake — become nearly free while that code is already open. This experiment
deliberately takes a few of them:

- **`DockPanelControl`** as well as a dock root. A new layout control is hard to
  justify alone; alongside the layout it needs anyway, it is a wrapper.
- **Horizontal border scrollbars.** The frame has only ever drawn a vertical
  thumb. Adding the horizontal case in isolation means touching `BorderRenderer`
  for one feature; doing it while scroll ownership is moving anyway is the same
  edit.
- **Retiring the four documented `StickyPosition` workarounds** in `Flows`. They
  are only removable once a panel can honour sticky.

The discipline that keeps this from becoming a rewrite: each item must be
**enabled by** the root change, not merely adjacent to it. Dead-code removal, for
instance, is explicitly **excluded** — `Core/ScrollState.cs` (198 lines, zero
consumers) and the four unused methods in `Helpers/ScrollingHelper.cs` are real
findings, but they are public API whose removal needs an adopter-wide search, and
bundling that into an experiment about layout would muddy both. They are tracked
as separate follow-up work in the main repo.

## Why

An external evaluator ([CodewinBzn/WinASM65's
`docs/console-ui-evaluation.md`](https://github.com/CodewinBzn/WinASM65/blob/master/docs/console-ui-evaluation.md),
comparing us against Terminal.Gui v2) named two layout gaps as reasons to choose
the other toolkit:

- **No flex allocator.** Partly wrong — flex exists (`WindowContentLayout.IsFlexHeight`,
  line 292). What is missing is *weight*: the division is
  `availableViewportHeight / fillChildCount`, strictly equal shares. Two Fill
  children are always 50/50; 2:1 is unreachable.
- **No Left/Right docking.** Correct and total. `StickyPosition` is
  `{ None, Top, Bottom }` — there is no horizontal edge concept at all.

A third gap is ours, not theirs, and is the stronger motivation: **sticky only
works at the window root.** `ScrollablePanelControl` ignores `StickyPosition`
entirely. Four separate comments in `Flows/PrimitiveStepContents.cs` and
`Flows/IFlowStepContent.cs` document the workaround ("a ScrollablePanel does NOT
honour StickyPosition, so the bands must be window children") rather than fixing
it. Documented workarounds are a smell.

### The real diagnosis

`WindowContentLayout` is the last self-scrolling container in the codebase.

`b310f0d5` (2026-06-14) moved `ScrollablePanelControl` off its private layout
engine onto `ScrollLayout : ILayoutContainer, IRegionClippingLayout`, explicitly
to "eliminate the dual layout engine — the structural root of recurring cursor /
hit-test / content-height desyncs". `d2f186ed` (2026-09-26) did the same for
scrollbars: three implementations became one, and "where the three disagreed,
ScrollablePanel's model won, because it was consistently the most correct".
`ede5465f` (2026-10-23) deleted `ScrollableLayoutNode`, the last orphan.

`WindowContentLayout` never made that trip. It was added in `6b88f111`
(2025-02-20) — **commit #54 of 1766**, nine days into the project, before
`ILayoutContainer` existed — and has seen **7 commits in 20 months**. It still
fuses two responsibilities the rest of the engine separates: a vertical stack
with sticky bands, and a scroll viewport.

And the window root is the only node in the tree that cannot choose its layout.
`WindowRenderer.cs:208` hardcodes `new WindowContentLayout()`. Everything below
the root dispatches polymorphically through `LayoutNodeFactory`, which already
returns six different `ILayoutContainer` implementations by control type.

So this is not "add docking and weighted flex". It is: **make the root
polymorphic like the rest of the engine, and the two gaps close as
consequences.** A grid root gives Left/Right docking (columns) and weighted flex
(`Star(2)`/`Star(1)`) using machinery that already ships and is already tested.

## Goals

1. The window root layout is replaceable (`Window.RootLayout`).
2. `GridLayout` becomes the default root; `StickyPosition` is reinterpreted as
   grid placement rather than bespoke band arithmetic.
3. `DockLayout` exists as a root option, giving Left/Right/Top/Bottom docking.
4. **Every existing application behaves identically with no source change.**
5. `ScrollablePanelControl` honours `StickyPosition`, retiring the four
   documented workarounds.

### Non-goals

- Removing, renaming or deprecating `StickyPosition`. It stays public, with the
  same three values and the same meaning, indefinitely. 132 call sites across 30
  builders inside this library depend on it, plus unknown adopter usage.
- Absolute positioning. Out of scope and explicitly unwanted — it would ruin the
  core render philosophy.
- Changing `GridLayout`, `GridTrackSizer` or `ScrollLayout` semantics.

## Success criterion

The experiment succeeds when **sticky is no longer a separate layout concept** —
it is grid placement — **and no existing app can tell.** Capability (docking,
weighted flex) is necessary but not sufficient: a version that adds docking while
leaving `WindowContentLayout` in place as the default fails this bar, because the
unification is the point.

## Design

### The mapping

`StickyPosition` already describes an `Auto / Star / Auto` row partition.
`WindowContentLayout` computes it by hand (`ArrangeChildren`, lines 165–281):
sum the sticky-top heights, sum the sticky-bottom heights, give the remainder to
the middle, lay the middle out at `-ScrollOffset`. That is exactly what a grid
with `Auto`, `Star(1)`, `Auto` rows does.

| `StickyPosition` | Grid placement |
|---|---|
| `Top` | consecutive `Auto` rows at the top, in child order |
| `None` | the single `Star(1)` row — the scrolling body |
| `Bottom` | consecutive `Auto` rows at the bottom, in child order |

Because the vocabulary already matches, the default root can be a grid without
any change to what `.StickyTop()` means to a caller.

### Root layout selection

`WindowRenderer.RebuildDOMTree` (line 208) stops hardcoding the layout:

```csharp
_rootLayout = _window.RootLayout ?? WindowRootLayout.Default();
_rootNode = new LayoutNode(null, _rootLayout);
```

`Window.RootLayout` is a new nullable property; null means the default. The
builder exposes `WithRootLayout(...)`. Omitting it is today's behaviour.

### Where window scrolling lives

This is the crux, and the one place "nothing changes" needs proving rather than
asserting.

Today the root both stacks and scrolls: `WindowContentLayout` owns
`ScrollOffset`, `MaxScrollOffset`, `ViewportHeight`, `ScrollableContentHeight`,
`CanScroll`, and `WindowRenderer` preserves `ScrollOffset` across rebuilds
(line 207).

Under a grid root, the **star row hosts the scrolling body** — which is
`ScrollLayout`'s existing job. Window scrolling becomes "the root's body row is a
scrolling layout", the same answer `b310f0d5` gave for SPC. The default root is
therefore a composition:

```
GridLayout (root)
├── Auto row(s)    ← sticky-top children
├── Star row       ← ScrollLayout, holding every StickyPosition.None child
└── Auto row(s)    ← sticky-bottom children
```

For an explicitly chosen `Root.Grid(...)` or `Root.Dock()`, there is **no
window-level scrolling**: the root fills the window exactly and scrolling belongs
to whatever `ScrollablePanelControl` sits in a cell. This matches WinUI, and it
is the one genuine behavioural fork — but it applies only to windows that opt in
to a root they could not have had before, so no existing app reaches it.

#### Coupling audit

Verified against the current tree:

- `_windowContentLayout.` has **exactly one** external reader:
  `WindowRenderer.cs:122` (the `ScrollOffset` setter).
- `StickyTopHeight`, `StickyBottomHeight`, `ScrollableTop`, `ScrollableBottom`
  have **zero** readers outside `WindowContentLayout.cs`. They exist to serve
  `GetPaintClipRect` (line 388), which is already behind the
  `IRegionClippingLayout` interface that `ScrollLayout` also implements.

The coupling is far thinner than the file's age suggests. The public window
scroll API (`Window.ScrollOffset` and friends) must keep its exact signatures and
semantics for a default-root window; it is backed by the body `ScrollLayout`
instead of the fused layout.

### Default sizing of the grid root

The default root grid is **horizontally stretch, vertically fill**: it occupies
the window's content rectangle exactly, rather than sizing to its children.

This is what `WindowContentLayout` effectively does today — it arranges against
`finalRect.Height` and treats the window's content box as the viewport — so
stating it as the grid root's default sizing preserves current behaviour rather
than changing it. It also makes the common cases degenerate correctly: a single
`Star` row with no sticky children is a body that fills the window, which is
exactly today's scroll viewport.

An explicitly chosen `Root.Grid(...)` or `Root.Dock()` uses the same default, for
the same reason: a root that shrink-wrapped its children would leave unpainted
window area and make docking meaningless.

### Border-delegated scrollbars

Today the window frame draws the scroll indicator itself:
`Windows/BorderRenderer.cs` renders a thumb into the right border
(`DrawScrollbar`, line 124) and, for frameless windows, over a reserved last
content column (`DrawFramelessScrollbar`, line 89). Both read `_window.ScrollOffset`
and `_window.IsScrollable`. It is **vertical only** — there is no horizontal
equivalent anywhere in the frame.

A grid root should be able to delegate a cell's scrollbars to the window border
the same way, on either axis:

```csharp
.WithRootLayout(Root.Grid(g => g
    .Columns(GridLength.Auto, GridLength.Star(1))
    .Rows(GridLength.Star(1))
    .DelegateScrollbarsToBorder(row: 0, col: 1, Axis.Vertical | Axis.Horizontal)))
```

The delegated cell's scrolling content drives the border thumb instead of
painting its own bar inside the cell, reclaiming a column (and, for the
horizontal case, a row) of content space. Mouse interaction on the border thumb
routes to that cell's scroller.

Two consequences worth stating up front:

- **Horizontal border scrollbars are new surface in `BorderRenderer`**, not a
  reuse. The bottom border currently draws no thumb, and the frameless path
  reserves a column but no row. This is additive work, not a rewiring.
- **At most one cell per axis may delegate.** The border is a single track; two
  cells delegating vertically would fight over the same thumb. The builder should
  reject the second registration rather than silently letting the last win.

This also generalises what the window does today: the default root delegating its
star row's vertical scrollbar to the border **is** the current window scrollbar,
expressed in the new model rather than special-cased.

### DockLayout

A new `DockLayout : ILayoutContainer`, modelled on WPF's `DockPanel`:

- `Dock` enum: `Left`, `Top`, `Right`, `Bottom`.
- Children claim edges in declaration order, each shrinking the remaining
  rectangle.
- The last child with no dock fills the remainder (`LastChildFill`, default true).

Placement for a dock root is expressed at add time, not as a control property —
`.Docked(Dock.Left)` on the builder. This keeps `IWindowControl` free of a second
positioning property; `StickyPosition` stays the only one, and it stays meaningful
only under the default root.

### DockPanelControl — docking below the root

`DockLayout` is also exposed as an ordinary control, `DockPanelControl`
(`Controls.DockPanel(...)`), not only as a root layout.

This matters because **a grid does not and cannot "gain docking".** They are
different allocation algorithms, not one feature the other can absorb:

- A **grid** places children into a track structure declared up front. The cell
  exists before the child does; `Place(ctrl, row: 0, col: 1)` is a coordinate.
  Overlap is legal — two children may share a cell.
- A **dock** has no tracks. Each child *consumes* an edge of a shrinking
  rectangle, in declaration order. A 30-wide left dock means every later child
  sees a rectangle 30 columns narrower. Order is semantic; there are no
  coordinates.

Fusing them would require a docked grid child to answer "which track did you just
consume?", and the answer is none. They stay siblings under `ILayoutContainer`,
exactly as `ScrollLayout`, `TabLayout` and `GridLayout` already coexist.

Exposing the layout as a control gives the practical capability without the
fusion: any grid cell may *contain* a dock panel, and any dock region may contain
a grid. The cost is one control wrapper over a layout that must exist anyway.

Worth doing now rather than later. Adding a new layout *control* to a shipped
library is a public-surface addition that has to earn its place on its own; doing
it while the layout is being written anyway is nearly free, and retrofitting it
afterwards means revisiting the same code twice.

### Nesting

The root layout governs the window's **direct** children only. Any child may be a
container control — `GridControl`, `DockPanelControl`, a scroller — at any depth,
in either direction. The IDE shape (menu/tree/status docked around a splittable
two-pane grid) is a dock root with a `Controls.Grid(...)` fill child; the inverse
(a grid root with a docked panel in one cell) composes just as well.

One root layout per window; ordinary controls below.

## Authoring surface

Unchanged (compiles and renders identically):

```csharp
new WindowBuilder(system)
    .WithTitle("Dashboard")
    .AddControl(Controls.Markup("[bold]Header[/]").StickyTop().Build())
    .AddControl(Controls.ScrollablePanel().WithAutoScroll().Build())
    .AddControl(Controls.StatusBar().StickyBottom().Build())
    .BuildAndShow();
```

Newly possible — docking:

```csharp
new WindowBuilder(system)
    .WithRootLayout(Root.Dock())
    .AddControl(Controls.Markup("menu").Docked(Dock.Top).Build())
    .AddControl(Controls.Tree().Docked(Dock.Left).WithWidth(30).Build())
    .AddControl(Controls.StatusBar().Docked(Dock.Bottom).Build())
    .AddControl(editorGrid)                       // fills the remainder
    .BuildAndShow();
```

Newly possible — weighted flex with a root splitter:

```csharp
new WindowBuilder(system)
    .WithRootLayout(Root.Grid(g => g
        .Columns(GridLength.Star(2), GridLength.Star(1))
        .ColumnSplitterAfter(0)))
    .AddControl(editor.At(row: 0, col: 0))
    .AddControl(inspector.At(row: 0, col: 1))
    .BuildAndShow();
```

## Risk

The capability work is additive and low-risk. The risk is concentrated entirely
in **reimplementing sticky on grid placement while preserving behaviour**. This
is a behaviour-preserving rewrite of the oldest, least-churned, most-depended-on
layout code in the repository.

Specific hazards, each with a named precedent:

| Hazard | Precedent |
|---|---|
| Scroll offset silently reset by a measure pass | `b310f0d5` hit exactly this; fixed by asserting the offset **survives** a later measure/arrange |
| Fill children mis-sized when the extent differs from the painted viewport | the SPC-in-grid-cell scroll-cap bug (`7b03160b`) — every isolated test passed |
| Paint bleed across sticky bands | `GetPaintClipRect` must produce byte-identical clips |
| Focus traversal order changing | sticky children currently arrange in a separate pass from body children |
| Hit-testing against sticky bands | `WindowEventDispatcher` reads `StickyPosition` at lines 618 and 997 |
| Border scrollbar thumb drifting from the body's real offset | `BorderRenderer` reads `_window.ScrollOffset` directly; under the new model that value is owned by the body `ScrollLayout`, and the two must not diverge |

### Verification

- The 132 internal sticky call sites are the first harness: if sticky drifts, this
  library's own controls break loudly.
- The local consumer build gate must be green — **including CxSql**, which is
  structurally excluded by its `global.json` SDK pin and must be run via the
  documented `mv global.json global.json.off` workaround. It is the heaviest API
  user; a gate that omits it proves little.
- A "real thing" test per CLAUDE.md: real container nesting, boundary-stressing
  sizes (narrow/short windows, where allotted extent differs from painted
  viewport), driven through the real input path, asserting state **survives** a
  re-render.
- Live tmux smoke-drive of DemoApp — mandatory for layout changes, per the
  ScrollLayout post-mortem.
- Golden-render comparison: capture a set of DemoApp windows before and after,
  assert the character buffers are identical.

## Order of work in the clone

Not shipping phases — the clone ships nothing. This is the order that keeps the
experiment answerable if it is abandoned halfway.

**1. Golden-buffer harness, before any change.** Capture character buffers for a
set of DemoApp windows and the sticky-heavy `Flows` screens, at several sizes
including narrow and short. This is the oracle; without it "byte-identical" is an
opinion. Built first, against unmodified code, so it is known good.

**2. `GridRootLayout` behind the existing default.** Implement the
`Auto/Star/Auto` mapping and run it against the harness *without* making it the
default — the grid root and `WindowContentLayout` both laying out the same tree,
buffers compared. This answers the central question before anything is cut.

**3. Swap the default, delete `WindowContentLayout`.** Only once step 2 matches.
Window scrolling moves to the body `ScrollLayout`; the public scroll surface is
cut or reshaped as the model demands.

**4. `DockLayout`, `DockPanelControl`, and border-delegated scrollbars.**
Additive capability, built after the answer is in because none of it proves
anything about the risky part and would otherwise delay that answer. The control
wrapper lands with the layout, not after it.

**5. `ScrollablePanelControl` honours `StickyPosition`.** Retire the four `Flows`
workarounds and check whether the comments describing them can be deleted.

Steps 1–3 are the experiment. Steps 4–5 are what the experiment unlocks, built
here to confirm they actually fall out as claimed rather than being assumed.

## Exit criteria

**Success** — all of:
- Golden buffers identical for every captured window, at every captured size.
- Full suite green in the clone (currently 6922 tests).
- Live tmux smoke-drive of DemoApp shows no visible regression.
- A written account of what the public scroll API had to become.

**Failure** — any of:
- Sticky cannot be reproduced byte-identically and the divergence is not
  explainable as a pre-existing bug.
- Preserving window scrolling requires `GridRootLayout` to grow special cases
  until it is `WindowContentLayout` with a grid-shaped wrapper.
- The cut to the public scroll API is so wide that applying it back could only
  ever be a major version.

Failure is a real outcome and is cheap here: delete the clone, keep the writeup.
The finding that "sticky cannot be expressed as grid placement" would itself be
worth having, and would settle the question for good.

## Applying back

Out of scope for this spec, and requires its own approval. The experiment's
deliverable is a recommendation with evidence, in three parts:

1. Whether the unification works at all.
2. What it costs in public API.
3. Whether that cost can be hidden behind a compatibility layer (non-breaking,
   mergeable now) or cannot (a 3.0 conversation).

If the answer to 3 is "hidden cheaply", the work transfers by cherry-picking
**from** the main repo — never by pushing out of this clone, which is blocked by
a `pre-push` hook and a disabled push URL.

## Open questions

1. **Does any adopter read `Window.ScrollOffset` / `CanScroll` directly?** The
   internal coupling is one call site, but external usage is unaudited. Worth a
   `gh search code` sweep using the corrected query syntax — needed before
   applying back, not before cutting it here.
2. **Does `ScrollablePanelControl` honouring `StickyPosition` change existing
   rendering?** Today a sticky child inside an SPC is silently treated as
   `None`. Making it work is a fix, but it is also a behaviour change for any app
   that set sticky inside a panel and adapted to it being ignored. Needs a
   survey of the four `Flows` workarounds before assuming it is purely additive.
3. **Should `Root.Grid()` windows expose a scroll API at all?** Leaving
   `Window.ScrollOffset` present but inert is a trap; throwing is hostile.
   Leaning toward documenting it as meaningless for non-default roots.
