# cxdbg attach tui

`cxdbg attach tui -s <socket>` enters a live view on a running SharpConsoleUI
application.

Tree on the left, two tabs on the right answering *what is this control*, a
docked strip below answering *why*. Every boundary is a draggable splitter, so
the panes are yours to size rather than mine to guess.

Every screenshot here is the real thing: cxdbg running headless against the
DemoApp's Chart Showcase, photographed over cxdbg's own diagnostics socket.
cxdbg debugging cxdbg.

## details + size

A control selected. The right pane says what it is; the dock says why it is
that size.

```
cxdbg  pid 1701435  frame 715   up/down select   1 2 pane   F1 dock   f freeze   q quit
Chart Showcase                                                               ▲║ 1 details │ 2 screen ─────────────────
  (root)  114x38                                                             █║MarkupControl#3385da1
    BodyMarker  114x38                                                       █║
      NavigationView  114x38                                                 █║wants       24 x 1
        HorizontalGridControl  114x38                                        │║got         26 x 1
          ColumnContainer  26x38  wants 26x13                                │║layout      -
            MarkupControl  26x3                                              │║visible     true
            ScrollablePanelControl  26x35  wants 26x10                       │║
              MarkupControl  26x1  wants 11x1                                │║enabled     true
              MarkupControl  26x1  wants 24x1                                │║focusable   false
              MarkupControl  26x1  wants 24x1                                │║text        [bold rgb(255,255,255) on
              MarkupControl  26x1  wants 24x1                                │║rgb(88,88,88)]    ▸ Line & Area      …
              MarkupControl  26x1  wants 24x1                                │║
              MarkupControl  26x1  wants 15x1                                │║focus is on ScrollablePanelControl
              MarkupControl  26x1  wants 24x1                                │║
              MarkupControl  26x1  wants 24x1                                │║
              MarkupControl  26x1  wants 24x1                                │║
              MarkupControl  26x1  wants 14x1                                │║
              MarkupControl  26x1  wants 24x1                                │║
              MarkupControl  26x1  wants 24x1                                │║
              MarkupControl  26x1  wants 24x1                                │║
              MarkupControl  26x1  wants 10x1                                │║
              MarkupControl  26x1  wants 24x1                                ▼║
══════════════════════════════════════════════════════════════════════════════╬═══════════════════════════════════════
 1 size │ 3 cells ────────────────────────────────────────────────────────────────────────────────────────────────────
MarkupControl   wants 26x3   offered 26x38
  DockRootLayout           offered  114 x 38
  BodyMarker               offered  114 x 38
  NavigationView           offered  114 x 38
  HorizontalGridControl    offered  114 x 38
  ColumnContainer          offered   26 x 38     decided the size
```

**`wants 26x13` on a tree line.** Shown only where desired and arranged
differ, so the lines that disagree stand out instead of being buried among the
ones that got what they asked for. A control with a zero extent says
`invisible` - present in the tree, reachable by focus, painting nothing.

**The details pane reads the control's own state** in the shared vocabulary.
Those names come from the library, so every control answers in the same words.

**The size dock names the container that decided.** `measure` marks the
deciding cascade link with `setsWidth`/`setsHeight`; cxdbg prints what it is
told rather than working it out from the numbers.

## screen

The same frame, right tab switched to the target's own screen.

```
cxdbg  pid 1701435  frame 739   up/down select   1 2 pane   F1 dock   f freeze   q quit
Chart Showcase                                                               ▲║ 1 details │ 2 screen ─────────────────
  (root)  114x38                                                             █║selection at 0,4 spanning 26x1        ▲
    BodyMarker  114x38                                                       █║                                      █
      NavigationView  114x38                                                 █║  ChartControl            Line & Area █
        HorizontalGridControl  114x38                                        │║                          The two cur █
          ColumnContainer  26x38  wants 26x13                                │║  [-] Plots                A line joi █
            MarkupControl  26x3                                              │║    ▸ Line & Area                     █
            ScrollablePanelControl  26x35  wants 26x10                       │║      Bar & Scatter        Line       █
              MarkupControl  26x1  wants 11x1                                │║      Gauges               100        █
              MarkupControl  26x1  wants 24x1                                │║      Combo                           █
              MarkupControl  26x1  wants 24x1                                │║  [-] Rendering                       █
              MarkupControl  26x1  wants 24x1                                │║      Render Modes                  ▀ █
              MarkupControl  26x1  wants 24x1                                │║      Gradients                  ▀▀▀  █
              MarkupControl  26x1  wants 15x1                                │║      Thresholds                      │
              MarkupControl  26x1  wants 24x1                                │║  [-] Heatmaps                 ▀      │
              MarkupControl  26x1  wants 24x1                                │║      Heatmap                         │
              MarkupControl  26x1  wants 24x1                                │║      Sparse Grid                     │
              MarkupControl  26x1  wants 14x1                                │║      Colour Scale           0        │
              MarkupControl  26x1  wants 24x1                                │║  [-] Axes                     0      │
              MarkupControl  26x1  wants 24x1                                │║      Ticks & Labels       ────────── │
              MarkupControl  26x1  wants 24x1                                │║      Custom Ticks         Area       │
              MarkupControl  26x1  wants 10x1                                │║      Two Scales           100        │
              MarkupControl  26x1  wants 24x1                                ▼║  [-] Composition                     ▼
══════════════════════════════════════════════════════════════════════════════╬═══════════════════════════════════════
 1 size │ 3 cells ────────────────────────────────────────────────────────────────────────────────────────────────────
MarkupControl   wants 26x3   offered 26x38
  DockRootLayout           offered  114 x 38
  BodyMarker               offered  114 x 38
  NavigationView           offered  114 x 38
  HorizontalGridControl    offered  114 x 38
  ColumnContainer          offered   26 x 38     decided the size
```

This is the application's real screen, repainted from its cells, with one line
saying where the selection sits in it - `selection paints nothing` when the
rectangle is empty.

**Cells out, cells in.** `snapshot {"format":"cells"}` hands over one entry per
column and `SurfaceViewControl` paints them. Nothing is serialised as text in
between, so no second parser can drift from the first and a wide glyph still
occupies the two columns it occupied in the application.

The protocol also offers `{"format":"markup"}`, equally lossless, for anything
that wants text it can keep or replay - a file, a log, a golden test - rather
than a pane repainting several times a second.

## cells

`F3`, and `[` `]` to move the row. The question text cannot answer, in the
place you are already looking.

```
cxdbg  pid 1701435  frame 1009   up/down select   1 2 pane   F1 dock   f freeze   q quit
Chart Showcase                                                               ▲║ 1 details │ 2 screen ─────────────────
  (root)  114x38                                                             █║selection at 0,4 spanning 26x1        ▲
    BodyMarker  114x38                                                       █║                                      █
      NavigationView  114x38                                                 █║  ChartControl            Line & Area █
        HorizontalGridControl  114x38                                        │║                          The two cur █
          ColumnContainer  26x38  wants 26x13                                │║  [-] Plots                A line joi █
            MarkupControl  26x3                                              │║    ▸ Line & Area                     █
            ScrollablePanelControl  26x35  wants 26x10                       │║      Bar & Scatter        Line       █
              MarkupControl  26x1  wants 11x1                                │║      Gauges               100        █
              MarkupControl  26x1  wants 24x1                                │║      Combo                           █
              MarkupControl  26x1  wants 24x1                                │║  [-] Rendering                       █
              MarkupControl  26x1  wants 24x1                                │║      Render Modes                  ▀ █
              MarkupControl  26x1  wants 24x1                                │║      Gradients                  ▀▀▀  █
              MarkupControl  26x1  wants 15x1                                │║      Thresholds                      │
              MarkupControl  26x1  wants 24x1                                │║  [-] Heatmaps                 ▀      │
              MarkupControl  26x1  wants 24x1                                │║      Heatmap                         │
              MarkupControl  26x1  wants 24x1                                │║      Sparse Grid                     │
              MarkupControl  26x1  wants 14x1                                │║      Colour Scale           0        │
              MarkupControl  26x1  wants 24x1                                │║  [-] Axes                     0      │
              MarkupControl  26x1  wants 24x1                                │║      Ticks & Labels       ────────── │
              MarkupControl  26x1  wants 24x1                                │║      Custom Ticks         Area       │
              MarkupControl  26x1  wants 10x1                                │║      Two Scales           100        │
              MarkupControl  26x1  wants 24x1                                ▼║  [-] Composition                     ▼
══════════════════════════════════════════════════════════════════════════════╬═══════════════════════════════════════
 1 size │ 3 cells ────────────────────────────────────────────────────────────────────────────────────────────────────
row 7   114 columns   [ ] moves the row                                                                              ▲
                                                                                                                     █
    0   ' '   width 1                                                                                                │
    1   ' '   width 1                                                                                                │
    2   ' '   width 1                                                                                                │
    3   ' '   width 1                                                                                                │
    4   ' '   width 1                                                                                                │
    5   ' '   width 1                                                                                                │
    6   C   width 1                                                                                                  │
    7   o   width 1                                                                                                  ▼
```

A continuation cell says so rather than repeating the glyph before it, which is
the whole reason to look here. Combining marks are counted, not folded away.
Read from the cells the screen pane is already holding, so the dock costs no
extra round trip and always agrees with the picture above it.

## keys

| | |
|---|---|
| up / down | move the tree selection; the tree scrolls to follow |
| 1 / 2 | right pane tab |
| F1 / F3 | dock tab |
| [ / ] | the row the cells dock reads |
| Tab | move between panes and splitters |
| f | freeze - the target carries on, only the looking stops |
| q | quit |

Digits for the right pane, function keys for the dock. Two tab strips on one
screen is this layout's real risk, so they take visibly different classes of
key rather than sharing one and leaving you to guess which moved.

## What it costs the target

`frame` leads every poll. It is cheap and unmarshalled, and when the counter has
not moved nothing has changed, so the expensive questions are skipped entirely.

`measure` runs on selection change only, never on the poll. It forces a
relayout, so polling it would make the debugger the cause of the churn its own
invalidation view reports.

Freeze stops the polling. The application carries on; only the looking stops,
which is what the held frame counter in the header is telling you.
