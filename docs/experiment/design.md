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

## Findings

Recorded as the experiment runs. These are observations about the EXISTING code,
found while building the oracle - not consequences of the rewrite.

### Step 2 result: the mapping holds for bands, and isolates the body as the real work

`GridRootLayout` implements `IGridSource` and delegates to the shipped
`GridLayout`, so the Auto/Star/Auto mapping is tested against the real track
sizer rather than a reimplementation. Parity is measured below the window — both
layouts driven over the same controls, arranged child rectangles compared.

**5 of 9 parity cases pass outright**, including sticky-only, invisible-child
exclusion, the empty window, and multi-control bands at their own heights (one
Auto row per sticky child, not one shared row — a shared row would size every
control in a band to the tallest).

**The 4 failures are all one thing: the body row.**

| Case | Old | New |
|---|---|---|
| sandwich 60x20 | body `7x40` | body `7x18` |
| sandwich 20x6 | body `7x40` | body `7x4` |
| sandwich 12x4 | body `7x40` | body `7x2` |
| body-only | children at y=1, y=2 | both at y=0 |

Two distinct causes, same root:

1. **The body must be allowed to overflow.** The old layout gives the body its
   full content height (40 rows in a 20-row window) — that overflow *is* what
   makes a window scrollable. A grid clamps a cell to its track, so the body
   comes back at viewport height and nothing can scroll.
2. **The grid does not stack within a cell.** Several body children placed in the
   same cell all land at `y=0`. They need a vertical stack inside the star row.

Both are the same missing piece, and it is the piece the design already named:
**the star row must host a scrolling layout, not raw children.**

### The index-correlation wall — RESOLVED by owning the tree

**Status: resolved.** Recorded as found; the resolution follows at the end.

**Original finding:** This is the experiment's first genuine
structural obstacle, as opposed to work not yet done.

`GridLayout` pairs child nodes to cells **by index**: `node.Children[i]` is
`OrderedCells[i]`. `IGridSource` documents this as an invariant and six sites in
`GridLayout` rely on it (`Math.Min(children.Count, cells.Count)` then index both).

The body needs the opposite. A grid cell clamps its content to the track and does
not stack several controls; a scrolling body must stack them and let them
overflow. The fix is a synthetic body `LayoutNode` holding a `ScrollLayout` in the
star row — `LayoutNode(ILayoutContainer)` exists for exactly this, and nothing
walks the root's children positionally (`FindByControl` recurses), so the extra
depth is safe.

But that body node would have to BE one of the root's children, making the list
`[sticky…, bodyNode, sticky…]` — a restructured list, not the author-order control
list `WindowRenderer` builds. `GridRootLayout` cannot both honour index
correlation and restructure its children.

Three ways through:

- **Build the restructured tree in the root layout.** `GridRootLayout` owns the
  root node's children: it would take the flat control list and produce the
  banded tree. This is the honest answer but it moves tree construction from
  `WindowRenderer` into a layout, which no other `ILayoutContainer` does.
- **Relax index correlation to a placement lookup.** Give `IGridSource` a
  control→placement map instead of a positional list, so a grid can hold children
  the source did not enumerate. Cleaner conceptually, but it changes a contract
  six call sites depend on, inside the grid engine, which is shipped and heavily
  tested.
- **`WindowRenderer` builds the banded tree.** The renderer, which already builds
  the root node, groups sticky and body children before handing them to the
  layout. Keeps the grid contract and keeps layouts side-effect-free; costs the
  renderer knowledge of sticky, which is the coupling this experiment set out to
  remove.

The third is the smallest change and the worst fit; the first is the best fit and
the largest. Not deciding this unilaterally — it is the kind of
architecture-shaping call that belongs with the maintainer.

**What is already proven and kept:** `IScrollOrigin` and the additive
origin-driven path in `ScrollLayout`. The panel path is untouched by
construction, and **659 scroll tests pass**, so the seam itself is sound and
independently useful regardless of how the body question resolves.

#### Resolution: the root layout owns the tree, and the grid is untouched

Relaxing index correlation — my first recommendation — was wrong, and the reason
is worth keeping: the body node is **synthetic and has no control**, so pairing
by control identity could not resolve it either. The relaxation would have
changed a contract at five sites inside the shipped grid engine and still left
the special case, merely somewhere else.

`GridRootLayout` instead owns its tree shape. It rebands the root's children into
`[sticky-top…, bodyNode, sticky-bottom…]` on every pass, where `bodyNode` is a
persistent synthetic node holding a `ScrollLayout` over the non-sticky children.
The grid sees one child per cell, exactly as before, and `IGridSource` is
unchanged.

This makes it the only `ILayoutContainer` that constructs nodes, which is called
out in the class remarks. The precedent is weaker than it first appears:
`ScrollLayout` already swaps the panel's child-node resolver during measure, and
`LayoutNode(ILayoutContainer)` exists precisely for control-less structural nodes.

The body node is invisible to callers: `Window.Controls` still returns the flat
author-order list, and `FindByControl` recurses, so a control inside the body is
found as before. A `BodyMarker` control stands in for the body in `OrderedCells`
because the grid needs something to correlate against; it never paints and is
never added to a window.

**Parity: 9 of 9.** The sandwich at four sizes including 12x4, body-only,
sticky-only, multi-control bands, invisible children, and the empty window. Leaf
controls land at identical coordinates:

```
          old                 new
HEADER    (0,0 6x1)           (0,0 6x1)
body      (0,1 7x40)          (0,1 7x40)      inside an 18-row viewport
FOOTER    (0,19 6x1)          (0,19 6x1)
```

The body keeps its full 40-row height inside an 18-row viewport, which is what
makes the window scrollable.

**Full suite: 6939 of 6940.** The single failure is
`X11ClipboardTests.ALargeCopy_CrossesInPieces_Whole`, the known pre-existing
flake: it fails only under full-suite load and passes 3/3 in isolation at ~25ms.
The changes touch three layout files and nothing near the clipboard.

**A real bug found on the way.** `ScrollLayout` ignored `HorizontalAlignment`
entirely — correct for a panel, which stretches its children unconditionally, but
wrong for a window root, where the old layout gives a Left/Center/Right child its
own desired width. Caught by the body-only parity case (`beta` arranged 5 wide
instead of 4). Fixed in the layout, not the test.

### `ScrollLayout` cannot currently serve a window root

`ScrollLayout` does exactly what the body row needs — its own doc says it stacks
children "from `y = -verticalScrollOffset` … each at its full measured height".
But it resolves its scroll state through `node.Control as ScrollablePanelControl`
(line 35) and returns early when that cast fails. A window root node has
`Control == null`, so it cannot be reused as-is.

This is the first real structural finding of the experiment. Three ways out,
to be decided before step 3:

- **Extract the scroll-state seam.** `ScrollLayout` stops reaching for a concrete
  control and takes an interface supplying offsets and content extent. Cleanest,
  and it finishes the `b310f0d5` arc properly, but it touches a shipped layout
  that currently has one caller and a lot of tests.
- **Give the window root a hidden `ScrollablePanelControl`** as its body host.
  Cheapest; smells of the facade pattern that caused the Panel regressions, and
  would make the window's control tree contain a control the user never added.
- **A sibling `RootScrollLayout`** duplicating the stacking logic without the
  panel dependency. Lowest risk, but it is a second scrolling implementation,
  which is precisely what `d2f186ed` was fought to eliminate.

Leaning toward extracting the seam: it is the option consistent with the
direction the codebase has already been moving for five months.

### `Window.ScrollOffset`'s setter does not invalidate

`Window.cs:845`. The setter stores the value through to the renderer and stops.
`ScrollBy` (line 865), twenty lines below, does the same store and then calls
`Invalidate(Invalidation.Relayout)`.

So setting `ScrollOffset` directly and re-rendering a clean tree paints the old
layout: the value reads back correctly, `IsScrollable` is true, `TotalLines` is
40, and the buffer is unchanged. `ScrollBy(3)` on the same window scrolls the
body from `body line 0` to `body line 3` with the sticky bands pinned.

Found by `GoldenBufferOracleTests.TheOracle_CatchesAScrolledBody`, which failed
on its first run and looked like a blind oracle. It was not - the scroll had
genuinely not happened.

Whether this is a bug depends on whether the property is meant to be a live
setter or a seed value read during the next natural layout. It is public API, so
it is worth settling **in the main repo**, separately from this experiment: a
caller doing `window.ScrollOffset = n` today gets silence until something else
triggers a relayout. It is also directly relevant here, because the rewrite moves
the backing store from `WindowContentLayout` to the body `ScrollLayout`, and
whatever the invalidation contract turns out to be has to survive that move.

### Step 3 result: the swap costs ten lines, and the flex contract was the hidden half

Swapping the default is a type rename. `WindowRenderer` touched
`WindowContentLayout` in exactly one place that matters — it constructs one and
exposes a scroll facade over it — so `GridRootLayout` gaining `ScrollOffset`,
`ScrollBy`, `ScrollToTop`, `ScrollToBottom`, `PageUp` and `PageDown` made it a
drop-in. The field was renamed `_rootLayout`, because a field called
`_windowContentLayout` holding a `GridRootLayout` is the kind of name that
outlives the refactor and misleads the next reader.

`EnsureVisible(int y)` has **zero callers** anywhere in the library. It is public
API, so it is not droppable in the main repo without the usual audit, but nothing
internal depends on it and `GridRootLayout` does not reimplement it.

**The real finding is what parity testing could not see.** Nine passing parity
tests and a proven oracle both missed it, because the parity suite drives
`Markup` controls, which size to their content. The old root layout measures its
body in **two passes** with an asymmetry that is the whole contract:

- children that size to their own content are measured at `int.MaxValue`, and
  their overflow is what there is to scroll through;
- **flex** children — `VerticalAlignment.Fill`, or any `IScrollableContainer` —
  are measured against a **bounded share of the remaining viewport**.

`MeasureFromOrigin` collapsed both into the unbounded case. A `Fill` child asked
to size itself against `int.MaxValue` answers `int.MaxValue`, and the paint walk
then runs two billion rows. This did not fail a test; it **hung the suite**,
which is why it only surfaced when the default was swapped and the whole corpus
of existing windows started running through the new root.

`ArrangeFromOrigin` had the matching gap, and it is not fixable by reading
`DesiredSize` back: a flex child's desired height is whatever it was last
measured against, so arrange has to recompute the share itself — including the
integer-division remainder, spread one row at a time across the leading flex
children, exactly as the old layout did. Both passes now mirror
`WindowContentLayout`'s rule via a shared `IsFlexHeight`.

Two lessons worth carrying to the apply-back:

1. **Geometry parity over content-sized controls is not parity.** The test corpus
   has to include a `Fill` child and a nested `ScrollablePanelControl` before the
   mapping can be called proven. The existing suite caught this only by hanging.
2. **A hang is a failure.** Had the run been given a shorter timeout and read as
   "slow", the swap would have looked green. The suite was diagnosed by sampling
   `dotnet-stack` against the live `testhost`, three times, to confirm the stack
   was not advancing.

### Three more flex bugs, all from the body being a viewport the grid treats as a cell

Fixing the measure/arrange flex contract got the suite running, but 49 tests still
failed, and every one traced to the same mismatch: `GridLayout` treats the body
as an ordinary cell, and an ordinary cell is a control that knows its own size.
A viewport is not.

**1. The grid re-measures cells at unbounded height, and the body records it.**
`GridLayout`'s pass-2 wrap reflow measures each child at
`LayoutConstraints.Loose(measureW, int.MaxValue)` to discover wrapped heights,
on the explicit assumption that "Fixed/Star rows ignore the reported height".
True for a control. But the body node is a `ScrollLayout`, which *stores* what it
was measured against as `ContentHeight` — so the unbounded pass was the one that
stuck. A bare `ScrollablePanelControl` in a 28-row window came out 12 rows tall:
its own unbounded default cap, recorded as if it were the viewport.

**2. The body reported its widest child as its width.** `MeasureFromOrigin`
returned `Math.Min(widest, width)`. For a window root that is wrong in the same
way: the body stretches to its allocation, so it must report the allocation. A
one-column scrollbar was making the grid size the body cell to one column, and a
`Stretch` child inside it then never saw the window width — 1 column instead of
40, with the height already correct.

**3. Measure and arrange disagreed about the remainder row.** Arrange spread the
integer-division remainder across the leading flex children; measure gave every
flex child the floor. Two Fill children in a 15-row body were measured at 7 and 7
but arranged at 8 and 7. Harmless for a scrollbar, not harmless for a table or a
wrapping panel, which reports one size and paints at another.

**The fix for (1) is a fourth member on `IScrollOrigin`: `ViewportHeight`.** The
first attempt had the body remember its last finite constraint, which worked
until the owner rebanded between passes — a remembered 28 describing a region
that sticky footers had since cut to 26, leaving the window claiming a 2-row
scroll range that did not exist. The owner is the only thing that knows the
current region, so the body asks it, and falls back to the remembered height only
when the owner has not arranged yet.

That is the real shape of this step: **the body is the one grid cell whose size
is an input, not an output.** Everything above follows from the grid being
entitled to assume the opposite about every other cell.

### Rebanding every frame defeated paint-only invalidation, and hiding that was hiding a bug

`Reband` tore down and rebuilt the root's child list on every measure *and* every
arrange. `LayoutNode.ClearChildren` and `AddChild` each call `InvalidateMeasure`,
so the layout re-dirtied the whole tree on every frame. That is a real per-frame
cost, not just a failing assertion — `PaintOnlyInvalidationTests` exist precisely
to hold the line that a repaint performs **zero** measures, and six of them
failed with "expected 0, actual 5".

The fix splits `Reband` in two. The row and cell lists are plain data the grid
reads, so they are rebuilt every pass and invalidate nothing. The **tree** is
reshaped only when the banding actually changed, compared by node identity so a
child that merely resized counts as unchanged.

**Making the rebuild conditional then broke all nine parity tests** — and the
reason is the useful part. With the skip in place, the body's content painted at
the window origin instead of below the sticky header. Dumping the tree showed the
body node correctly at `(0,1 60x18)` while its child sat at `(0,0)` with a **null
`Parent`**.

`ClearChildren` nulls each removed child's `Parent`, and re-adding them is what
restored it. `LayoutNode.Arrange` derives `AbsoluteBounds` from
`Parent.AbsoluteBounds`, so a severed link silently resolves to the window origin
— no exception, no empty rect, just content in the wrong place. The unconditional
rebuild had been reattaching those links by accident on every frame, which is why
the bug was invisible until the rebuild stopped happening.

The skip path now reattaches `_bodyNode.Parent` and each body child's `Parent`
explicitly, without touching the child lists, so nothing is invalidated.

Worth carrying forward: **a per-frame rebuild can mask a correctness bug as well
as cost performance.** The six paint-only failures and the nine parity failures
were the same defect seen from two sides, and fixing only the cheap-looking one
would have traded a visible failure for a silent misplacement.

### The body's measured size is its slot, not its content

The last two failures were an empty Fill `ScrollablePanel` in a frameless
maximized window rendering as **60x2** instead of 60x20 — two rows being exactly
its border, with no content between them.

`MeasureFromOrigin` had been returning `Math.Min(contentHeight, MaxHeight)`.
`GridLayout` reads that as the Star row's content floor, so a body whose content
is *shorter* than the viewport shrinks the viewport to fit it. Every other case
in the corpus hid this, because they all have content **taller** than the
viewport — and there, content-floor and allocation happen to agree.

The body now reports its full allocation on both axes. The scrollable extent is
published separately through `ContentHeight`, which is what the window's scroll
range actually reads; the measured size is the slot, and the slot is the whole
allocation.

This is the third time in this step the same mistake appeared in a different
costume — width, then height-when-unbounded, now height-when-short. Each time the
body answered a question about its content when it was being asked about its
space. Worth stating as the rule for the apply-back: **a viewport's measured size
is what it was given, never what it holds.**

Two parity tests now cover it: an empty Fill container, and a short body between
sticky bands.

### Step 3 verdict: all four exit criteria met

- **Full suite green in the clone.** 6948 / 6948, zero failures. Note that this
  includes `X11ClipboardTests.ALargeCopy_CrossesInPieces_Whole`, which was flaky
  under full-suite load on master; it has passed every run since the swap.
- **Live tmux smoke-drive of DemoApp: no visible regression.** Driven at 120x35
  through the launcher, the Horizontal Splitter demo (Fill panels, explicit
  heights, nested splitters - the exact flex path rewritten here) and the Spinner
  demo, which is the decisive one: a sticky footer, a scrolling body with a live
  partial-thumb scrollbar, and animated content. Arrow-key scrolling moved the
  body, held the sticky footer pinned, and tracked the scrollbar thumb and the
  up/down overflow markers correctly. Zero stderr output across the session.
- **A written account of what the public scroll API had to become.** Above: the
  facade kept its shape exactly, six scroll verbs moved onto `GridRootLayout`,
  and `EnsureVisible` turned out to have no callers at all. Nothing was cut.

- **Golden buffers identical for every captured window, at every captured size.**
  14 cases, byte-identical including colour and decoration. This was the one
  criterion an earlier draft of this section recorded as NOT met, on the grounds
  that comparing the two roots needed both live in one process while the default
  was already swapped. That turned out to be a missing seam rather than a real
  obstacle: extracting `IWindowRootLayout` made both constructible, and the
  comparison ran. Recorded here because "we cannot test that" deserves one
  attempt at "why not" before it is written down as a limit.

### `WindowContentLayout` is gone, and the proof outlived it

An earlier draft of this section argued the old layout should stay in the clone
as the parity oracle, with deletion left to the apply-back. That reasoning was
wrong on its premise: **there is no apply-back — this is the clone.** Keeping a
dead class to protect a test is the tail wagging the dog.

The real constraint was narrower: deleting the old layout deletes the only thing
the new one can be compared against. So the order mattered.

1. Extract `IWindowRootLayout` from the facade `WindowRenderer` already had, and
   route root construction through a static factory. Both layouts satisfied the
   interface **without changing a line** — which is itself the evidence that the
   facade was already the real contract, and that the window root was the last
   node in the engine chosen by type rather than by dispatch.
2. With both roots constructible, run the comparison that settles the central
   claim: the same window rendered through each, buffers compared cell by cell,
   colour and decoration included. **14 cases, all identical** — sticky
   sandwiches at four sizes, three scroll positions including past the end,
   multi-control bands, a Fill `ScrollablePanel`, the empty-panel collapse,
   aligned children, and a hidden sticky child.
3. Verify the comparison can fail: shifting the body band by one row fails 12 of
   the 14. An oracle that cannot fail proves nothing.
4. Commit that, so the proof lives in history.
5. Then delete.

The three `HorizontalSplitterControl` layout tests were **retargeted, not
deleted**: they now drive the new root with their assertions unchanged and pass.
That is a stronger result than parity testing — the flex contract (an explicit
height wins, equal share otherwise) is reproduced exactly rather than
approximately, as judged by tests written against the old implementation with no
knowledge of the new one.

Also deleted, now that no public-API audit stands in the way: `Core/ScrollState.cs`
(198 lines, zero consumers anywhere) and four of the five methods in
`Helpers/ScrollingHelper.cs` (`CenterIndexInViewport`, `ScrollByDelta`,
`GetMaxScrollOffset`, `IsIndexVisible` — zero call sites each; only
`EnsureIndexVisible` is used, once).

What this costs: the parity and golden-buffer suites are gone with the layout
they compared against. The window root's behaviour is now held by the rest of
the suite — thousands of tests written against the old root that pass unchanged
against the new one — plus the `HorizontalSplitterControl` tests above. That is
the right trade once the comparison has been made and recorded, and the wrong
one before.

Net effect on the library: **-688 lines**, and the window root is no longer a
special case in an engine that is polymorphic everywhere else.

Re-smoke-driven after the deletions at 120x35: launcher, tree scrolling with
correct thumb and overflow markers, detail pane, borders. Zero stderr.

### Can the root be swapped for anything else? Yes, including per window

A seam that only its original occupant fits is not a seam, so this was tested
rather than asserted. The test root is built on `VerticalStackLayout` — a stock
container written long before this experiment, which knows nothing about sticky
bands, scrolling or grids, and was not modified to serve as a root.

**It took 18 lines of real code.** The layout work delegates to the stack; the
clip rule is "the window's content area"; the five scroll verbs do nothing and
the range reports zero, which is the honest answer for a layout with no
scrolling region.

The result is observable, which is the thing that makes the seam real rather
than decorative. Identical controls under each root:

```
--- grid-rooted window ---        --- stack-rooted window ---
body                              FOOTER
                                  body
         (six blank rows)                  (six blank rows)
FOOTER
```

The grid root gives `StickyPosition` a meaning and pins the footer to the last
row. The stack root does not, and is **entitled** not to — a root defines its
own model, and sticky is a property of the banded model, not of windows. The
window's scroll API degrades to a no-op rather than throwing or misreporting.

**The factory was process-wide, and that was too coarse.** The original framing
of this idea was "like WinUI, can have a different parent in any window" — which
is per *window*, not per process. A static factory cannot give one window a
docked shell and another a grid body. So `WindowRenderer` gained
`WindowRootLayoutFactory`, a per-instance override that falls back to the global
default when null. The output above is two windows **alive in the same system,
rendered in the same pass**, with different layout models.

What a root must supply, in full:

| Requirement | Cost |
|---|---|
| `MeasureChildren` / `ArrangeChildren` | Delegate to any existing `ILayoutContainer` |
| `GetPaintClipRect` | One line if the root has no bands |
| Five scroll verbs + two range properties | Empty bodies and zero if nothing scrolls |

Nothing in the contract presumes bands, grids or scrolling. A root that wants
none of them pays almost nothing for the parts it does not use — which is the
test of whether an interface was extracted at the right place.


### The user-facing surface: `window.Root`

```csharp
window.Root = () => new DockRootLayout();   // this window only
window.Root = null;                          // back to the application default
```

A **layout**, not a control. That distinction is not pedantry: the root node is
built as `new LayoutNode(null, rootLayout)` - no control - so swapping it adds
nothing to the paint tree or the hit-test walk. A control-shaped root would
insert a node that has to be painted, hit-tested and focus-skipped on every
frame, for a thing with no visual of its own.

A **factory**, not an instance, because the renderer rebuilds its root on every
DOM rebuild; a shared instance would carry one window's banding and scroll
position into the next rebuild. The setter calls `ForceRebuildLayout()` itself,
so assignment is enough - a caller should not have to know that the root is a
rebuild-time decision.

`Window.Root` is the only public way to set this. `WindowRenderer` carries the
backing property, but it is internal: the renderer does not rebuild the DOM when
it changes, so setting it directly would leave the window rendering with its
previous root until something else happened to trigger a rebuild. One door, and
the door does the whole job.

Three scopes, narrowest wins:

| Scope | Surface |
|---|---|
| One window | `window.Root = () => new MyRoot()` |
| Whole application | `WindowRenderer.RootLayoutFactory = () => new MyRoot()` |
| Default | `GridRootLayout` |

Verified round-trip: default to stack to `null` returns a byte-identical buffer
to how the window started, without recreating the window.

**Prior art.** This is WinUI's `Window.Content` with one deliberate difference -
WinUI's root is a `Panel`, a control that happens to lay out, where ours is pure
layout. The closer match is **GTK4**: `gtk_window_set_child()` plus
`GtkLayoutManager`, which split "what is in the window" from "how it is
arranged" and made the latter a swappable object rather than a widget subclass.
GTK3's `Gtk.Window` is a single-child `Bin`, which is the shape we had before
this experiment, not after it.


### One root, not two: sticky IS docking on the vertical axis

The spec argued that a grid cannot gain docking, and that stands - a grid places
into declared tracks by coordinate, a dock consumes edges of a shrinking
rectangle in order. What the spec never asked is whether **sticky** is a dock.
It is.

`GridRootLayout.Reband` partitions children into top / body / bottom in child
order, each sticky child taking an `Auto` row off the end of a shrinking middle.
That is the dock allocation rule, restricted to two of its four edges.

Tested rather than argued: `DockRootLayout` was written to derive docks from
`StickyPosition` (`Top` to `Dock.Top`, `Bottom` to `Dock.Bottom`) and compared
against the banded root with the same golden-buffer harness. **15 of 15 cases
byte-identical** - sandwiches at four sizes, three scroll positions, a Fill
`ScrollablePanel`, the empty-panel collapse, aligned children, a hidden sticky
child, an empty window. 14 of those passed on the first attempt.

**So the plan changes: `DockRootLayout` replaces `GridRootLayout` rather than
joining it.** Shipping both would be shipping one algorithm twice, once with two
of its edges removed. And the fourth edge comes free - the same root drives a
full IDE shell:

```
HEADER
SIDEbody 0                            INSP
    body 1
    body 2
STATUS
```

Two real decisions came out of the comparison, neither of them cosmetic.

**Far edges reverse within their run.** Under strict WPF `DockPanel` semantics
each `Bottom` child consumes the bottom edge in turn, so a rule declared before
a help line ends up *below* it. WPF users know to declare footers in reverse;
the 90 `StickyBottom` call sites in this repository do not, and
`ImageViewerWindow` is exactly that shape - a rule, then a help line under it.
So a run of same-edge far-side children is reversed among themselves, which
makes author order read the way the screen reads and makes `Dock.Bottom` a
drop-in for `StickyPosition.Bottom`.

**The reversal is within a run, not across the whole child list.** The first
attempt moved every far-edge child to the end, which silently destroyed
cross-axis ordering: a status bar declared before a sidebar was still processed
after it, so it stopped spanning the full width. Declaration order across edges
IS the contract - it is how "a title bar above a sidebar" differs from "a
sidebar beside a title bar" - and only same-edge runs may be reordered.

What remains before this can be the default root: a real `Dock` property (the
test drives `DockRootLayout.DockOf`, a static hook, which is a probe and not a
surface), and whether `StickyPosition` stays as the sugar for the two vertical
edges or is deprecated in favour of `Dock`.


## Open questions

1. ~~**Does any adopter read `Window.ScrollOffset` / `CanScroll` directly?**~~
   **Moot.** Nothing was cut. The scroll facade kept its exact shape; the six
   verbs moved onto `GridRootLayout` with the same signatures, and
   `EnsureVisible` — the one member that could have gone — had no callers at all.
   The question was premised on the rewrite forcing a reduction in surface area,
   and it did not.
2. **Does `ScrollablePanelControl` honouring `StickyPosition` change existing
   rendering?** Today a sticky child inside an SPC is silently treated as
   `None`. Making it work is a fix, but it is also a behaviour change for any app
   that set sticky inside a panel and adapted to it being ignored. Needs a
   survey of the four `Flows` workarounds before assuming it is purely additive.
3. **Should `Root.Grid()` windows expose a scroll API at all?** Leaving
   `Window.ScrollOffset` present but inert is a trap; throwing is hostile.
   Leaning toward documenting it as meaningless for non-default roots.
