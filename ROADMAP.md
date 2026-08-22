# Roadmap

SharpConsoleUI is actively maintained and driven by real-world usage in production apps ([ServerHub](https://github.com/nickprotop/ServerHub), [LazyNuGet](https://github.com/nickprotop/lazynuget), [LazyDotIDE](https://github.com/nickprotop/lazydotide), [cxshell](https://github.com/nickprotop/cxshell)).

## Recently Shipped

**Composite controls** — the library has crossed from inventing primitives to composing them:

- ChatTranscriptControl — agent/chat transcript composing scrolling, collapsible messages and markup: per-message actions and status footer, role-tinted message rail, collapsed-message preview, per-message compact footer, selectable/copyable messages, `SetExpanded`/`SetHeader`
- FormControl — labeled-input form composing GridControl with the input controls, plus FormXml for loading a form from a declarative XML descriptor
- LogViewerControl — virtualized, framework-native log console with sticky-bottom auto-follow that detaches when the user scrolls up
- NavigationView — nav-pane shell with selectable items and keyboard routing
- WizardControl — multi-step wizard with per-step validation
- PromptControl — single-line input with history, optional wrapping, placeholder measuring and configurable Enter behavior

**Layout and input:**

- GridControl — WinUI-style 2D layout: Fixed/Auto/Star rows & columns, row/column spans, CSS-style gaps, per-cell styling via `grid[r,c]`, AutoFlow, content wrapping, full focus/cursor host conformance, ColorRole theming
- Grid splitters — column and row splitters with WinUI-style resize and Tab focus
- GridBackedHGrid — grid-backed drop-in for HorizontalGridControl, retiring the forked HGC/Grid layout engine
- ScrollLayout — `ScrollablePanelControl` is a real layout-tree participant (`ScrollLayout : ILayoutContainer, IRegionClippingLayout`) instead of a self-painting container. Eliminates the dual layout engine and the off-viewport stale-`ActualY` desyncs at the source; behavior-preserving, ~4× faster measure and ~24× fewer allocations on the layout path
- FlowControl — sequential flow layout
- MouseGestureCapture&lt;TRegion&gt; — sub-region gesture ownership
- Opt-in UI-affine window thread (`WithWindowThreadOnUI`)

**Controls and theming:**

- RadioControl&lt;T&gt; / RadioGroup&lt;T&gt; — generic single-select with a coordination object
- TableControl DataGrid — virtual data source, sorting, filtering, inline editing, compound filter expressions, opt-in AutoScroll
- Control Roles + nullable theme foundation — `ColorRole` on controls with a role resolver, so a control inherits its theme slot instead of hardcoding colors
- CanvasControl, ImageControl, VideoControl, LineGraphControl, SliderControl / RangeSliderControl, DatePicker, TimePicker, StatusBarControl, HorizontalSplitterControl, BarGraph animated transitions, Sparkline X-axis
- Markup engine — inline `[markdown]`, gradients with animation, per-line `[fillwidth]`, opt-in source copy, incremental append parsing

**Platform:**

- MVVM data binding — `INotifyPropertyChanged` on all controls, `Bind()` / `BindTwoWay()`
- Project templates — `dotnet new tui-app`, `tui-dashboard`, `tui-multiwindow`
- .NET 8.0 + 9.0 multi-targeting, SourceLink and symbol packages
- Compositor effects — PreBufferPaint/PostBufferPaint hooks
- Unified focus management — a single `FocusManager` per window owns `FocusedControl` and the focus path, replacing the old FocusCoordinator/FocusStateService pair and the per-control `_hasFocus` fields. `IFocusScope` lets containers (ScrollablePanel, grids, NavigationView, Toolbar) define their own Tab order
- Control-authoring on-ramp with the measure/paint contract reconciled to source

## Next

Ordered by effect per unit of effort — cheapest broad wins first, the deepest and
most narrowly-scoped work last.

- **Granular invalidation** — the `Repaint` tier already exists and the layout path already honors it, but all 476 `SetProperty` setters hardcode `Relayout`, so a color change still costs a full measure pass. Remaining work is per-property classification (~166 are colour-only and provably layout-neutral), not engine work; over-invalidating stays correct, so the failure mode is safe in one direction
- **Instant input response** — `InputLoop` spins on `Console.KeyAvailable` with a 10ms sleep, so every keystroke pays up to 10ms before anything sees it. Replace that one loop with a blocking read plus a wake. Contained to a single method, but it is the paste and ANSI-sequence parsing path, so it needs care on both Unix and Windows
- **A centred window larger than the desktop opens blank** — `WindowBuilder.Centered()` computes a negative position, and nothing shrinks an oversized window to fit
- **Alt+1-9 does not select a window when none is active** — the chord is handled inside the branch that routes keys to the active window, so it is dropped when there is nothing to route to
- **Ctrl+Q does not force-quit when the stall is inside a terminal write** — the watchdog banner promises it, but on Windows the input thread blocks on the same `_consoleLock` the render path holds, so the key never reaches the queue; the fix is narrowing that lock, next to the frozen compositor
- **Native plugin ABI** — a real C-ABI plugin boundary that loads `.dll`/`.so` plugins at runtime, scoped to services and themes. Today's `LoadPlugin<T>` is `new T()` on a host-compiled type, so plugins cannot ship independently of the host. The largest piece of work here and the one no adopter is currently blocked on

## Later

- **Scroll-to-cursor for nested editors** — when a content-sized `MultilineEditControl` (one that does not scroll internally) is taller than its host `ScrollablePanel`, moving the cursor toward the editor's end hides the terminal cursor instead of scrolling the panel to follow it
- **WrapPanel** — responsive wrap-by-width layout for toolbars, tag lists and narrow-window form fields
- **Portal child viewport slices** — `GetVisibleHeightForControl` hands a portal's hosted child the whole inner area, so a list stacked with siblings (prompt, rules, status) believes it owns their rows too. Every portal-based command palette in the family hand-patches this with the same `h - 2 - 5` arithmetic. Fixing it shifts layout for existing portal consumers, so it needs its own tests and a deliberate landing
- **Interactive desktop portals** — `PortalContentContainer` does not implement `IInteractiveControl`, so `InputCoordinator`'s desktop-portal key path skips it entirely and every interactive portal hand-rolls key delivery through the host window's `PreviewKeyPressed`. The old assumption — a portal is a passive overlay that may hold nothing interactive — stopped being true once portals hosted prompts and lists. The container can answer this itself: it already has `GetFocusableChildren()`, so `ProcessKey` can delegate to the focused child and return `false` when there are none, which `InputCoordinator` already bubbles to global shortcuts, the exit key, then Escape-to-close. Affects the desktop-portal path only (`UseDesktopPortals`, default false), so it changes key delivery for existing desktop portals and needs its own tests
- ListControl data virtualization — virtual data source for 100K+ item lists, matching TableControl
- NumericSpinner — increment/decrement with arrow keys
- ColorPicker — color selection dialog
- **CommandPaletteControl** — the palette five family apps each copied from lazydotide and drifted (~700 duplicated lines). Portal-hosted, generic over the item type, with a real scored subsequence matcher and match highlighting — which none of the five actually has today. Design: `docs/superpowers/specs/2026-08-18-command-palette-control-design.md`
- More composites — property inspector, diff viewer

## Future

- **Cell-stream transport** — one idea with two front ends, currently listed as two: a **web
  terminal backend** (run your TUI in a browser over WebSocket) and an **SSH remote session
  driver**. Both mean the same thing: stop serialising the UI to ANSI and have the far end parse
  it back, and instead send the cells themselves — the compositor's `CharacterBuffer` diff — with
  input events returning on the same channel. The dirty-region tracking that already exists *is*
  the diff. Prior art: vtm's DirectVT does exactly this over stdin/stdout and reports beating a
  classic SSH connection. The real prize is not speed: when both ends are ours, terminal
  capability probing stops mattering — wide-char widths, ZWJ ligation, Kitty support are all known
  rather than guessed. Costs: a client to ship and version, and a wire format owned forever. It is
  an additional driver, never a replacement — plain SSH into a stock terminal must keep working
- **Plugin ecosystem** — community-contributed controls and themes

---

Have a feature request? [Open a discussion](https://github.com/nickprotop/ConsoleEx/discussions/categories/ideas) or [create an issue](https://github.com/nickprotop/ConsoleEx/issues/new).
