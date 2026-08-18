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
- Control-authoring on-ramp with the measure/paint contract reconciled to source

## Next

- **Instant input response** — replace the polling-based input loop with event-driven wake for zero-latency keypress handling
- **Consolidate focus tracking** — unify visual focus (`control.HasFocus`) and coordinator routing into a single source of truth; currently two independent writes that can drift and cause key routing to target the wrong control
- **Granular invalidation** — `SetProperty` always issues a full `Relayout`; let appearance-only properties settle for a `Repaint` so a color change stops costing a measure pass

## Later

- **Scroll-to-cursor for nested editors** — when a content-sized `MultilineEditControl` (one that does not scroll internally) is taller than its host `ScrollablePanel`, moving the cursor toward the editor's end hides the terminal cursor instead of scrolling the panel to follow it
- **WrapPanel** — responsive wrap-by-width layout for toolbars, tag lists and narrow-window form fields
- ListControl data virtualization — virtual data source for 100K+ item lists, matching TableControl
- NumericSpinner — increment/decrement with arrow keys
- ColorPicker — color selection dialog
- More composites — command palette, property inspector, diff viewer

## Future

- **Web terminal backend** — run your TUI in a browser via WebSocket
- **SSH remote session driver** — dedicated driver for remote sessions
- **Native plugin ABI** — real `.dll`/`.so` plugin loading for services and themes, beyond today's host-compiled types
- **Plugin ecosystem** — community-contributed controls and themes

---

Have a feature request? [Open a discussion](https://github.com/nickprotop/ConsoleEx/discussions/categories/ideas) or [create an issue](https://github.com/nickprotop/ConsoleEx/issues/new).
