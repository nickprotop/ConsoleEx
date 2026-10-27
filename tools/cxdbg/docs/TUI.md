# cxdbg attach tui

A debug IDE for a running SharpConsoleUI application.

```
cxdbg attach -s <socket>          # the tui
cxdbg attach shell -s <socket>    # a cxdbg> prompt instead
```

The tree is the target's **desktop**, with its windows under it and their
controls under them. Beside it, two tabs saying what a thing is and what it
looks like. Below, four tabs saying why it is that way. Every boundary is a
draggable splitter.

Every screenshot here is real: cxdbg running headless against the DemoApp's
Chart Showcase, photographed over cxdbg's own diagnostics socket. cxdbg
debugging cxdbg.

## the tree, and what a control is

```
pid:1821373  frame:433                                                              1 2:pane  F1-F4:dock  [ ]:row  f:freeze  q:q
┌────────────────────────────────┬─────────┬─────────┬─────────────────────────────┐ ║ 1 details │ 2 screen ────────────────────
│control                         │      got│    wants│layout                       │ ║MarkupControl#1189baa
├────────────────────────────────┼─────────┼─────────┼─────────────────────────────┤ ║╭─ size ─────────────────────────────────╮
│[-] desktop                     │   120x40│         │ModernGrayTheme              │▲║│ got         26 x 3                     │
│├─ [-] Chart Showcase           │   116x40│   active│DockRootLayout               │█║╰────────────────────────────────────────╯
││  └─ [-] BodyMarker            │   114x38│         │ScrollLayout                 ││║╭─ where ────────────────────────────────╮
││     └─ [-] NavigationView     │   114x38│         │VerticalStackLayout          ││║│ in window   0,0                        │
││        └─ [-] HorizontalGridCo│   114x38│         │GridLayout                   ││║│ on screen   0,0                        │
││           ├─ [-] ColumnContain│    26x38│    26x13│VerticalStackLayout          ││║│ layout      -                          │
││           │  ├─     MarkupCont│     26x3│         │                             ││║╰────────────────────────────────────────╯
││           │  └─ [-] Scrollable│    26x35│    26x10│ScrollLayout                 ││║╭─ reach ────────────────────────────────╮
││           │     ├─     MarkupC│     26x1│     11x1│                             ││║│ visible     true                       │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║│ enabled     yes                        │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║│ focusable   no                         │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║╰────────────────────────────────────────╯
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║╭─ state ────────────────────────────────╮
││           │     ├─     MarkupC│     26x1│     15x1│                             ││║│ text        [bold]  ChartCon           │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║╰────────────────────────────────────────╯
├──────────────────────────────────────────────────────────────────────────────────┤│║
│ Row 7/130  / filter                                                              ││║
└──────────────────────────────────────────────────────────────────────────────────┘▼║
═════════════════════════════════════════════════════════════════════════════════════╬══════════════════════════════════════════
 1 size │ 2 focus │ 3 cells │ 4 events ─────────────────────────────────────────────────────────────────────────────────────────
MarkupControl   wants 26x3   offered 26x38
┌────────────────────────────────────────────────────┬──────────────────────────────────────────────────┬────────────┬─────────┐
│container                                           │layout                                            │offered     │decided  │
├────────────────────────────────────────────────────┼──────────────────────────────────────────────────┼────────────┼─────────┤
│-                                                   │DockRootLayout                                    │114 x 38    │         │
│BodyMarker                                          │ScrollLayout                                      │114 x 38    │         │
│NavigationView                                      │VerticalStackLayout                               │114 x 38    │         │
│HorizontalGridControl                               │GridLayout                                        │114 x 38    │         │
│ColumnContainer                                     │VerticalStackLayout                               │26 x 38     │size     │
└────────────────────────────────────────────────────┴──────────────────────────────────────────────────┴────────────┴─────────┘
```

**`wants 26x13` appears only where it differs from what the control got**, so
the lines that disagree stand out instead of being buried among the ones that
got what they asked for. A control with a zero extent says `invisible`:
present in the tree, reachable by focus, painting nothing.

The tree is a real tree table, so it clicks, expands, and filters with `/` -
keeping the ancestors of a match, so filtering to `Scroll` still shows where it
lives. Selection survives the polls, because the rows are diffed rather than
rebuilt.

The details pane answers in sections, and what it says depends on what you
picked: a **desktop** has a theme and panel rows, a **window** has a state and
a root layout and what the user may do with it, a **control** has a size, a
place, and whatever it reports about itself in the library's shared vocabulary.

## focus

```
pid:1821373  frame:473                                                              1 2:pane  F1-F4:dock  [ ]:row  f:freeze  q:q
┌────────────────────────────────┬─────────┬─────────┬─────────────────────────────┐ ║ 1 details │ 2 screen ────────────────────
│control                         │      got│    wants│layout                       │ ║MarkupControl#1189baa
├────────────────────────────────┼─────────┼─────────┼─────────────────────────────┤ ║╭─ size ─────────────────────────────────╮
│[-] desktop                     │   120x40│         │ModernGrayTheme              │▲║│ got         26 x 3                     │
│├─ [-] Chart Showcase           │   116x40│   active│DockRootLayout               │█║╰────────────────────────────────────────╯
││  └─ [-] BodyMarker            │   114x38│         │ScrollLayout                 ││║╭─ where ────────────────────────────────╮
││     └─ [-] NavigationView     │   114x38│         │VerticalStackLayout          ││║│ in window   0,0                        │
││        └─ [-] HorizontalGridCo│   114x38│         │GridLayout                   ││║│ on screen   0,0                        │
││           ├─ [-] ColumnContain│    26x38│    26x13│VerticalStackLayout          ││║│ layout      -                          │
││           │  ├─     MarkupCont│     26x3│         │                             ││║╰────────────────────────────────────────╯
││           │  └─ [-] Scrollable│    26x35│    26x10│ScrollLayout                 ││║╭─ reach ────────────────────────────────╮
││           │     ├─     MarkupC│     26x1│     11x1│                             ││║│ visible     true                       │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║│ enabled     yes                        │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║│ focusable   no                         │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║╰────────────────────────────────────────╯
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║╭─ state ────────────────────────────────╮
││           │     ├─     MarkupC│     26x1│     15x1│                             ││║│ text        [bold]  ChartCon           │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║╰────────────────────────────────────────╯
├──────────────────────────────────────────────────────────────────────────────────┤│║
│ Row 7/130  / filter                                                              ││║
└──────────────────────────────────────────────────────────────────────────────────┘▼║
═════════════════════════════════════════════════════════════════════════════════════╬══════════════════════════════════════════
 1 size │ 2 focus │ 3 cells │ 4 events ─────────────────────────────────────────────────────────────────────────────────────────
1 stops   the keyboard is on ScrollablePanelControl
tab  control                     size
1    ScrollablePanelControl      26 x 35   it has the keyboard
```

`reachable` gives the Tab order and no geometry; the tree gives geometry and no
order. Joined, they answer the question people actually ask of a focus list:
which of these can be seen. A stop that is reachable by Tab and paints nothing
is a real bug and invisible in either source alone.

The DemoApp's `DropdownControl` shows the other case: a Tab stop that is not in
the window's tree at all, because it lives in a portal.

## cells

```
pid:1821373  frame:702                                                              1 2:pane  F1-F4:dock  [ ]:row  f:freeze  q:q
┌────────────────────────────────┬─────────┬─────────┬─────────────────────────────┐ ║ 1 details │ 2 screen ────────────────────
│control                         │      got│    wants│layout                       │ ║MarkupControl#1189baa
├────────────────────────────────┼─────────┼─────────┼─────────────────────────────┤ ║╭─ size ─────────────────────────────────╮
│[-] desktop                     │   120x40│         │ModernGrayTheme              │▲║│ got         26 x 3                     │
│├─ [-] Chart Showcase           │   116x40│   active│DockRootLayout               │█║╰────────────────────────────────────────╯
││  └─ [-] BodyMarker            │   114x38│         │ScrollLayout                 ││║╭─ where ────────────────────────────────╮
││     └─ [-] NavigationView     │   114x38│         │VerticalStackLayout          ││║│ in window   0,0                        │
││        └─ [-] HorizontalGridCo│   114x38│         │GridLayout                   ││║│ on screen   0,0                        │
││           ├─ [-] ColumnContain│    26x38│    26x13│VerticalStackLayout          ││║│ layout      -                          │
││           │  ├─     MarkupCont│     26x3│         │                             ││║╰────────────────────────────────────────╯
││           │  └─ [-] Scrollable│    26x35│    26x10│ScrollLayout                 ││║╭─ reach ────────────────────────────────╮
││           │     ├─     MarkupC│     26x1│     11x1│                             ││║│ visible     true                       │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║│ enabled     yes                        │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║│ focusable   no                         │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║╰────────────────────────────────────────╯
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║╭─ state ────────────────────────────────╮
││           │     ├─     MarkupC│     26x1│     15x1│                             ││║│ text        [bold]  ChartCon           │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║╰────────────────────────────────────────╯
├──────────────────────────────────────────────────────────────────────────────────┤│║
│ Row 7/130  / filter                                                              ││║
└──────────────────────────────────────────────────────────────────────────────────┘▼║
═════════════════════════════════════════════════════════════════════════════════════╬══════════════════════════════════════════
 1 size │ 2 focus │ 3 cells │ 4 events ─────────────────────────────────────────────────────────────────────────────────────────
row 5   114 columns, 114 glyphs   [ ] moves the row
┌─────┬───────┬──────┬──────┬─────────────────────────────────────────────────────────────────────────────────────────────────┐
│col  │glyph  │width │marks │style                                                                                            │
├─────┼───────┼──────┼──────┼─────────────────────────────────────────────────────────────────────────────────────────────────┤
│0    │space  │1     │      │                                                                                                 │▲
│1    │space  │1     │      │                                                                                                 │█
│2    │space  │1     │      │                                                                                                 ││
│3    │space  │1     │      │                                                                                                 ││
│4    │space  │1     │      │                                                                                                 ││
└─────┴───────┴──────┴──────┴─────────────────────────────────────────────────────────────────────────────────────────────────┘▼
```

What is in each column of one row of the screen, which text cannot carry: the
text formats drop the continuation cell of a wide glyph and fold combining
marks into the character before them. `[` and `]` move the row.

## the screen, and clicking it

```
pid:1821373  frame:734                                                              1 2:pane  F1-F4:dock  [ ]:row  f:freeze  q:q
┌────────────────────────────────┬─────────┬─────────┬─────────────────────────────┐ ║ 1 details │ 2 screen ────────────────────
│control                         │      got│    wants│layout                       │ ║selection at 0,0 spanning 26x3           ▲
├────────────────────────────────┼─────────┼─────────┼─────────────────────────────┤ ║                                         █
│[-] desktop                     │   120x40│         │ModernGrayTheme              │▲║  ChartControl            Line & Area    █
│├─ [-] Chart Showcase           │   116x40│   active│DockRootLayout               │█║                          The two curve  █
││  └─ [-] BodyMarker            │   114x38│         │ScrollLayout                 ││║  [-] Plots                A line joins  █
││     └─ [-] NavigationView     │   114x38│         │VerticalStackLayout          ││║    ▸ Line & Area                        █
││        └─ [-] HorizontalGridCo│   114x38│         │GridLayout                   ││║      Bar & Scatter        Line          █
││           ├─ [-] ColumnContain│    26x38│    26x13│VerticalStackLayout          ││║      Gauges               100           █
││           │  ├─     MarkupCont│     26x3│         │                             ││║      Combo                              █
││           │  └─ [-] Scrollable│    26x35│    26x10│ScrollLayout                 ││║  [-] Rendering                          █
││           │     ├─     MarkupC│     26x1│     11x1│                             ││║      Render Modes                  ▀▀▀▀ │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║      Gradients                  ▀▀▀     │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║      Thresholds                         │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║  [-] Heatmaps                 ▀         │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║      Heatmap                            │
││           │     ├─     MarkupC│     26x1│     15x1│                             ││║      Sparse Grid                        │
││           │     ├─     MarkupC│     26x1│     24x1│                             ││║      Colour Scale           0           │
├──────────────────────────────────────────────────────────────────────────────────┤│║  [-] Axes                     0         │
│ Row 7/130  / filter                                                              ││║      Ticks & Labels       ───────────── │
└──────────────────────────────────────────────────────────────────────────────────┘▼║      Custom Ticks         Area          ▼
═════════════════════════════════════════════════════════════════════════════════════╬══════════════════════════════════════════
 1 size │ 2 focus │ 3 cells │ 4 events ─────────────────────────────────────────────────────────────────────────────────────────
row 5   114 columns, 114 glyphs   [ ] moves the row
┌─────┬───────┬──────┬──────┬─────────────────────────────────────────────────────────────────────────────────────────────────┐
│col  │glyph  │width │marks │style                                                                                            │
├─────┼───────┼──────┼──────┼─────────────────────────────────────────────────────────────────────────────────────────────────┤
│0    │space  │1     │      │                                                                                                 │▲
│1    │space  │1     │      │                                                                                                 │█
│2    │space  │1     │      │                                                                                                 ││
│3    │space  │1     │      │                                                                                                 ││
│4    │space  │1     │      │                                                                                                 ││
└─────┴───────┴──────┴──────┴─────────────────────────────────────────────────────────────────────────────────────────────────┘▼
```

The application's own screen, repainted from its cells - `snapshot
{"format":"cells"}` straight into a `SurfaceViewControl`, with no text format
in between to lose anything or to need a second parser.

It follows the selection: the desktop shows the **composed desktop**, with the
system's panels and every window's border; anything inside a window shows that
window alone.

**And it is interactive.** A click in the picture is dispatched to the
application at that point, through its own hit-test routing, so it responds
exactly as it would to someone sitting in front of it - and cxdbg selects the
control painting there.

## keys

| | |
|---|---|
| click / arrows | select in the tree |
| click a guide | expand or collapse |
| `/` | filter the tree |
| `1` `2` | right pane: details, screen |
| `F1`-`F4` | dock: size, focus, cells, events |
| `[` `]` | the row the cells dock reads |
| `Tab` | move between panes and splitters |
| `f` | freeze - the target carries on, only the looking stops |
| `q` | quit |

The status bar's right-hand items are clickable as well as readable.

## what it costs the target

`frame` leads every poll: it is cheap and unmarshalled, and when the counter
has not moved nothing has changed, so the expensive questions are skipped.

`measure` runs on selection change only, never on the poll. It forces a
relayout, so polling it would make the debugger the cause of the churn its own
views report. `events` is asked for only while its tab is showing.

Freeze stops the polling. The application carries on; only the looking stops,
which is what the held frame counter is telling you.
