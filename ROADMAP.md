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

## Next

- **Slider / RangeControl** — horizontal and vertical value sliders
- **StatusBarControl** — per-window status bar (distinct from the system-level status bars)
- **Instant input response** — replace polling-based input loop with event-driven wake for zero-latency keypress handling

## Later

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
