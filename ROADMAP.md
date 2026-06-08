# Roadmap

SharpConsoleUI is actively maintained and driven by real-world usage in production apps ([ServerHub](https://github.com/nickprotop/ServerHub), [LazyNuGet](https://github.com/nickprotop/lazynuget), [LazyDotIDE](https://github.com/nickprotop/lazydotide)).

## Recently Shipped

- MVVM data binding — `INotifyPropertyChanged` on all controls, `Bind()` / `BindTwoWay()` API
- CanvasControl — retained and immediate mode drawing with full graphics API
- ImageControl — load and display PNG/JPEG/BMP/GIF/WebP/TIFF files in the terminal
- TableControl DataGrid — virtual data source, sorting, filtering, inline editing, compound filter expressions
- Gradient text and backgrounds with animation framework
- Project templates — `dotnet new tui-app`, `tui-dashboard`, `tui-multiwindow`
- .NET 8.0 + 9.0 multi-targeting
- SourceLink and symbol packages for debugging
- Compositor effects — PreBufferPaint/PostBufferPaint hooks for custom rendering
- DatePicker — locale-aware date selection with segmented editing and calendar popup
- TimePicker — locale-aware time selection with 12h/24h modes, optional seconds, AM/PM
- HorizontalSplitterControl — drag-to-resize horizontal bar between vertically stacked controls, mouse and keyboard support, auto-hide on invisible neighbors
- StatusBarControl — per-window status bar with left/center/right zones, clickable shortcut+label items, markup support, optional above line, theme integration
- LineGraphControl — multi-series line graphs with braille (2×4 pixel grid) and ASCII rendering modes, color gradients, Y-axis labels, live data updates
- SliderControl / RangeSliderControl — horizontal and vertical value sliders with keyboard, mouse drag, step/large-step, min/max labels, and dual-thumb range selection with MinRange enforcement
- VideoControl — terminal video playback via FFmpeg with half-block, ASCII and braille render modes, overlay status bar, dynamic resize, looping

## Next

- **Instant input response** — replace polling-based input loop with event-driven wake for zero-latency keypress handling
- **Consolidate focus tracking** — unify visual focus (`control.HasFocus`) and coordinator routing (`FocusCoord._focusPath`) into a single source of truth; currently two independent writes that can drift and cause key routing to target the wrong control

## Later

- **ScrollablePanel layout unification (`ScrollLayout : ILayoutContainer`)** — `ScrollablePanelControl` is currently a self-painting container: its children live outside the window's persistent layout tree, so it re-derives each child's measure/arrange/Fill slot in several code paths that must agree but historically drifted (cursor, hit-test, and content-height desyncs). Make its children real `LayoutNode`s via a dedicated `ScrollLayout` so the engine measures/arranges/caches them once, leaving the panel to own only the scroll offset and clip. Eliminates the off-viewport stale-`ActualY` problem at the source. Must stay behavior-preserving (external NuGet users); the existing 210-test SPC suite is the safety net.
- **Scroll-to-cursor for nested editors** — when a content-sized `MultilineEditControl` (one that does not scroll internally) is taller than its host `ScrollablePanel`, moving the cursor toward the editor's end currently hides the terminal cursor instead of scrolling the panel to follow it. Add panel auto-scroll that tracks a focused nested editor's cursor row.
- ListControl data virtualization — virtual data source for 100K+ item lists
- RadioButtonGroup — exclusive selection from a set
- NumericSpinner — increment/decrement with arrow keys
- ColorPicker — color selection dialog
- Accordion / CollapsiblePanel
- Custom control authoring SDK — `ControlBase` abstract class and guide for third-party control development

## Future

- **Web terminal backend** — run your TUI in a browser via WebSocket
- **SSH remote session driver** — dedicated driver for remote sessions
- **Plugin ecosystem** — community-contributed controls and themes

---

Have a feature request? [Open a discussion](https://github.com/nickprotop/ConsoleEx/discussions/categories/ideas) or [create an issue](https://github.com/nickprotop/ConsoleEx/issues/new).
