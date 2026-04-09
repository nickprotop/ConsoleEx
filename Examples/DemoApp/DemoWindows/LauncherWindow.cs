using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

public static class LauncherWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var darkGradient = new GradientBackground(
            ColorGradient.FromColors(new Color(15, 25, 60), new Color(5, 5, 15)),
            GradientDirection.Vertical);

        var lightGradient = new GradientBackground(
            ColorGradient.FromColors(new Color(180, 200, 230), new Color(220, 225, 240)),
            GradientDirection.Vertical);

        var nav = Controls.NavigationView()
            .WithNavWidth(30)
            .WithPaneHeader("[bold white]  SharpConsoleUI[/]")
            .WithContentBorder(BorderStyle.Rounded)
            .WithContentBorderColor(Color.Grey37)
            .WithContentBackground(new Color(30, 30, 40))
            .WithContentPadding(1, 0, 1, 0)
            .AddHeader("Layout & Windows", Color.Cyan1, header => header
                .AddItem("Border Styles", subtitle: "Explore window border styles", content: MakeInfoPanel("Border Styles"))
                .AddItem("IDE Layout", subtitle: "IDE-like application UI", content: MakeInfoPanel("IDE Layout"))
                .AddItem("File Explorer", subtitle: "Filesystem browser", content: MakeInfoPanel("File Explorer"))
                .AddItem("Multi-Tab Demo", subtitle: "TabControl with multiple tabs", content: MakeInfoPanel("Multi-Tab Demo"))
                .AddItem("WinUI Layout", subtitle: "WinUI-inspired settings layout", content: MakeInfoPanel("WinUI Layout"))
                .AddItem("Horizontal Splitter", subtitle: "Drag-to-resize horizontal bars", content: MakeInfoPanel("Horizontal Splitter"))
                .AddItem("Status Bar", subtitle: "Clickable status bar with zones", content: MakeInfoPanel("Status Bar"))
                .AddItem("Toolbar", subtitle: "Multi-height toolbar with auto-sizing", content: MakeInfoPanel("Toolbar")))
            .AddHeader("Controls", Color.Green, header => header
                .AddItem("Interactive Demo", subtitle: "Real-time key press handling", content: MakeInfoPanel("Interactive Demo"))
                .AddItem("Dropdown", subtitle: "Cascading dropdowns", content: MakeInfoPanel("Dropdown"))
                .AddItem("List View", subtitle: "NuGet-style package browser", content: MakeInfoPanel("List View"))
                .AddItem("Table", subtitle: "Interactive employee directory", content: MakeInfoPanel("Table"))
                .AddItem("DataGrid", subtitle: "Virtual DataGrid with 10K rows", content: MakeInfoPanel("DataGrid"))
                .AddItem("Nerd Fonts", subtitle: "NerdFont icon showcase", content: MakeInfoPanel("Nerd Fonts"))
                .AddItem("Markup Syntax", subtitle: "Rich markup system demo", content: MakeInfoPanel("Markup Syntax"))
                .AddItem("International & Emoji", subtitle: "Unicode & emoji support", content: MakeInfoPanel("International & Emoji"))
                .AddItem("Data Binding", subtitle: "MVVM data binding", content: MakeInfoPanel("Data Binding"))
                .AddItem("Date & Time", subtitle: "DatePicker and TimePicker controls", content: MakeInfoPanel("Date & Time"))
                .AddItem("Slider", subtitle: "Value and range slider controls", content: MakeInfoPanel("Slider"))
                .AddItem("HTML Rendering", subtitle: "Lynx-inspired HTML renderer", content: MakeInfoPanel("HTML Rendering")))
            .AddHeader("Data Visualization", Color.Yellow, header => header
                .AddItem("Graphs & Charts", subtitle: "Live sparklines & bar graphs", content: MakeInfoPanel("Graphs & Charts"))
                .AddItem("System Monitor", subtitle: "Real-time system dashboard", content: MakeInfoPanel("System Monitor")))
            .AddHeader("Rendering", Color.Orange1, header => header
                .AddItem("Container Backgrounds", subtitle: "Background color & gradient propagation", content: MakeInfoPanel("Container Backgrounds"))
                .AddItem("Gradients", subtitle: "Gradient text & backgrounds", content: MakeInfoPanel("Gradients"))
                .AddItem("Animations", subtitle: "Window animations & easing", content: MakeInfoPanel("Animations"))
                .AddItem("Image Rendering", subtitle: "Pixel art with half-blocks", content: MakeInfoPanel("Image Rendering"))
                .AddItem("Image Viewer", subtitle: "Load & display image files", content: MakeInfoPanel("Image Viewer"))
                .AddItem("Alpha Blending", subtitle: "Compositing, glass panels, live blend preview",
                    content: MakeInfoPanel("Alpha Blending"))
                .AddItem("Canvas Animations", subtitle: "Starfield, plasma & geometry canvases",
                    content: MakeCanvasInfoPanel()))
            .AddHeader("System", new Color(180, 140, 255), header => header
                .AddItem("Panel Config", subtitle: "Desktop panel configuration", content: MakeInfoPanel("Panel Config"))
                .AddItem("Desktop Background", subtitle: "Desktop background configuration", content: MakeInfoPanel("Desktop Background")))
            .AddHeader("Utilities", Color.Magenta1, header => header
                .AddItem("Built-in Dialogs", subtitle: "File pickers & system dialogs", content: MakeInfoPanel("Built-in Dialogs"))
                .AddItem("Digital Clock", subtitle: "FIGlet-rendered clock", content: MakeInfoPanel("Digital Clock"))
                .AddItem("Log Viewer", subtitle: "Real-time log display", content: MakeInfoPanel("Log Viewer"))
                .AddItem("Notifications", subtitle: "Notification system demo", content: MakeInfoPanel("Notifications"))
                .AddItem("System Info", subtitle: "OS & runtime details", content: MakeInfoPanel("System Info"))
                .AddItem("Terminal", subtitle: "PTY-backed terminal emulator", content: MakeInfoPanel("Terminal"))
                .AddItem("Video Player", subtitle: "Terminal video playback with half-block rendering",
                    content: MakeInfoPanel("Video Player"))
                .AddItem("Welcome Banner", subtitle: "FIGlet ASCII art banner", content: MakeInfoPanel("Welcome Banner")))
            .OnSelectedItemChanged((sender, args) =>
            {
                // No additional action needed — content factories handle the detail pane
            })
            .WithAlignment(HorizontalAlignment.Stretch)
            .Fill()
            .Build();

        // Launch demo on Enter/Space
        nav.ItemInvoked += (sender, args) =>
        {
            if (args.NewItem != null)
                LaunchDemo(ws, args.NewItem.Text);
        };

        // Theme switcher dropdown in content toolbar
        var themeDropdown = new DropdownControl("Theme:", new[] { "Dark", "Light" });
        themeDropdown.SelectedIndex = 0;
        nav.AddContentToolbarItem(themeDropdown);

        Window? win = null;
        themeDropdown.SelectedIndexChanged += (_, idx) =>
        {
            if (win != null)
                win.BackgroundGradient = idx == 0 ? darkGradient : lightGradient;
        };

        var window = new WindowBuilder(ws)
            .WithTitle("SharpConsoleUI Demo")
            .WithSize(90, 30)
            .AtPosition(0, 0)
            .WithBackgroundGradient(darkGradient.Gradient, darkGradient.Direction)
            .AddControl(nav)
            .BuildAndShow();

        win = window;
        return window;
    }

    private static Window OpenCanvasWindows(ConsoleWindowSystem ws)
    {
        CanvasDemoWindow.CreateStarfieldWindow(ws);
        CanvasDemoWindow.CreatePlasmaWindow(ws);
        return CanvasDemoWindow.CreateGeometryWindow(ws);
    }

    private static Action<ScrollablePanelControl> MakeCanvasInfoPanel()
    {
        return panel =>
        {
            var info = GetDemoInfo("Canvas Animations");
            if (info != null)
            {
                panel.AddControl(Controls.Markup()
                    .AddLines(info.ToArray())
                    .WithMargin(1, 1, 1, 1)
                    .Build());
            }

            var toolbar = Controls.Toolbar()
                .WithSpacing(1)
                .WithContentPadding(1, 0, 1, 0)
                .Build();

            var starfieldBtn = Controls.Button()
                .WithText("  Starfield  ")
                .WithBorder(ButtonBorderStyle.Rounded)
                .Build();

            var plasmaBtn = Controls.Button()
                .WithText("  Plasma  ")
                .WithBorder(ButtonBorderStyle.Rounded)
                .Build();

            var geometryBtn = Controls.Button()
                .WithText("  Geometry  ")
                .WithBorder(ButtonBorderStyle.Rounded)
                .Build();

            starfieldBtn.Click += (_, _) =>
            {
                var ws = panel.Container?.GetConsoleWindowSystem;
                if (ws != null)
                    CanvasDemoWindow.CreateStarfieldWindow(ws);
            };

            plasmaBtn.Click += (_, _) =>
            {
                var ws = panel.Container?.GetConsoleWindowSystem;
                if (ws != null)
                    CanvasDemoWindow.CreatePlasmaWindow(ws);
            };

            geometryBtn.Click += (_, _) =>
            {
                var ws = panel.Container?.GetConsoleWindowSystem;
                if (ws != null)
                    CanvasDemoWindow.CreateGeometryWindow(ws);
            };

            toolbar.AddItem(starfieldBtn);
            toolbar.AddItem(plasmaBtn);
            toolbar.AddItem(geometryBtn);

            panel.AddControl(toolbar);
        };
    }

    private static Action<ScrollablePanelControl> MakeInfoPanel(string demoName)
    {
        return panel =>
        {
            var info = GetDemoInfo(demoName);
            if (info != null)
            {
                panel.AddControl(Controls.Markup()
                    .AddLines(info.ToArray())
                    .WithMargin(1, 1, 1, 1)
                    .Build());
            }

            var launchButton = Controls.Button()
                .WithText("  Launch Demo  ")
                .WithMargin(1, 1, 0, 0)
                .WithBorder(ButtonBorderStyle.Rounded)
                .Build();

            launchButton.Click += (_, _) =>
            {
                var ws = panel.Container?.GetConsoleWindowSystem;
                if (ws != null)
                    LaunchDemo(ws, demoName);
            };

            panel.AddControl(launchButton);
        };
    }

    private static void LaunchDemo(ConsoleWindowSystem ws, string demoName)
    {
        _ = demoName switch
        {
            "Border Styles" => BorderStyleWindow.Create(ws),
            "IDE Layout" => IdeLayoutWindow.Create(ws),
            "File Explorer" => FileExplorerWindow.Create(ws),
            "Multi-Tab Demo" => TabDemoWindow.Create(ws),
            "WinUI Layout" => WinUIDemoWindow.Create(ws),
            "Horizontal Splitter" => HorizontalSplitterDemoWindow.Create(ws),
            "Status Bar" => StatusBarDemoWindow.Create(ws),
            "Interactive Demo" => InteractiveWindow.Create(ws),
            "Dropdown" => DropdownWindow.Create(ws),
            "List View" => ListViewWindow.Create(ws),
            "Table" => TableDemoWindow.Create(ws),
            "DataGrid" => DataGridWindow.Create(ws),
            "Nerd Fonts" => NerdFontWindow.Create(ws),
            "Markup Syntax" => MarkupSyntaxWindow.Create(ws),
            "International & Emoji" => InternationalWindow.Create(ws),
            "Data Binding" => DataBindingWindow.Create(ws),
            "Date & Time" => DateTimeDemo.Create(ws),
            "Slider" => SliderDemoWindow.Create(ws),
            "Graphs & Charts" => GraphsWindow.Create(ws),
                "System Monitor" => SystemMonitorWindow.Create(ws),
            "Container Backgrounds" => ContainerBgDemoWindow.Create(ws),
                "Gradients" => GradientDemoWindow.Create(ws),
            "Animations" => AnimationDemoWindow.Create(ws),
            "Image Rendering" => ImageDemoWindow.Create(ws),
            "Image Viewer" => ImageViewerWindow.Create(ws),
            "Built-in Dialogs" => DialogsWindow.Create(ws),
            "Digital Clock" => ClockWindow.Create(ws),
            "Log Viewer" => LogViewerWindow.Create(ws),
            "Notifications" => NotificationsWindow.Create(ws),
            "System Info" => SystemInfoWindow.Create(ws),
            "Terminal" => TerminalWindow.Create(ws),
            "Toolbar" => ToolbarDemoWindow.Create(ws),
            "Welcome Banner" => WelcomeWindow.Create(ws),
            "Alpha Blending" => AlphaBlendingDemoWindow.Create(ws),
            "Panel Config" => PanelConfigWindow.Create(ws),
            "Desktop Background" => DesktopBackgroundWindow.Create(ws),
            "Canvas Animations" => OpenCanvasWindows(ws),
            "Video Player" => VideoDemoWindow.Create(ws),
            "HTML Rendering" => HtmlDemoWindow.Create(ws),
            _ => (Window?)null
        };
    }

    private static List<string>? GetDemoInfo(string demoName)
    {
        return demoName switch
        {
            "Border Styles" => new List<string>
            {
                "[bold cyan]Border Styles[/]",
                "",
                "Explore all four window border styles: DoubleLine,",
                "Single, Rounded, and None (borderless). Spawn",
                "windows with each style and toggle live.",
                "",
                "[dim]Features:[/]",
                "  - All 4 BorderStyle options demonstrated",
                "  - Spawned windows show each style",
                "  - Live cycling between styles on a window",
                "  - Active vs inactive border differences",
                "",
                "[dim]Controls used:[/]",
                "  - ButtonControl, MarkupControl",
                "  - WindowBuilder border API",
            },
            "IDE Layout" => new List<string>
            {
                "[bold cyan]IDE Layout[/]",
                "",
                "A complete IDE-like application UI with menu bar,",
                "toolbar, project explorer, tabbed editor, and status bar.",
                "",
                "[dim]Controls used:[/]",
                "  - MenuControl, ToolbarControl",
                "  - TreeControl, MultilineEditControl",
                "  - SplitterControl, HorizontalGridControl",
                "  - ButtonControl, MarkupControl",
            },
            "File Explorer" => new List<string>
            {
                "[bold cyan]File Explorer[/]",
                "",
                "Browse the filesystem with a tree view for directories",
                "and a list view for files with icons and sizes.",
                "",
                "[dim]Controls used:[/]",
                "  - TreeControl, ListControl",
                "  - SplitterControl, HorizontalGridControl",
            },
            "Multi-Tab Demo" => new List<string>
            {
                "[bold cyan]Multi-Tab Demo[/]",
                "",
                "Demonstrates the TabControl with multiple tabs",
                "containing different types of content.",
                "",
                "[dim]Controls used:[/]",
                "  - TabControl, MarkupControl",
                "  - ScrollablePanelControl",
            },
            "WinUI Layout" => new List<string>
            {
                "[bold cyan]WinUI Layout[/]",
                "",
                "Windows 11 WinUI-inspired settings layout with",
                "gradient background, transparent nav panel, and",
                "bordered scrollable content area.",
                "",
                "[dim]Features:[/]",
                "  - Gradient background shows through nav & header",
                "  - ScrollablePanel with rounded border",
                "  - Left nav with selection highlighting",
                "  - Dynamic content switching per section",
                "",
                "[dim]Controls used:[/]",
                "  - ScrollablePanelControl (border + padding)",
                "  - HorizontalGridControl, MarkupControl",
                "  - CheckboxControl, ButtonControl",
            },
            "Horizontal Splitter" => new List<string>
            {
                "[bold cyan]Horizontal Splitter[/]",
                "",
                "Drag-to-resize horizontal bar between vertically",
                "stacked controls. Supports mouse drag and keyboard.",
                "",
                "[dim]Features:[/]",
                "  - Mouse drag to resize",
                "  - Keyboard: Up/Down arrows, Shift for 5-row jump",
                "  - Smart resize with Fill and explicit heights",
                "  - Min height clamping (default: 3 rows)",
                "",
                "[dim]Controls used:[/]",
                "  - HorizontalSplitterControl",
                "  - ScrollablePanelControl, HorizontalGridControl",
            },
            "Status Bar" => new List<string>
            {
                "[bold cyan]Status Bar[/]",
                "",
                "Dedicated StatusBarControl with left/center/right",
                "alignment zones, clickable shortcut+label items,",
                "separator items, and dynamic content updates.",
                "",
                "[dim]Features:[/]",
                "  - Three alignment zones (left, center, right)",
                "  - Shortcut key hints with accent color",
                "  - Markup support for labels",
                "  - Click handling with events",
                "  - Dynamic add/remove/update items",
                "  - BatchUpdate for bulk changes",
                "",
                "[dim]Controls used:[/]",
                "  - StatusBarControl",
                "  - ToolbarControl, ButtonControl, MarkupControl",
            },
            "Toolbar" => new List<string>
            {
                "[bold cyan]Toolbar Demo[/]",
                "",
                "ToolbarControl with auto-height support for",
                "multi-row items like bordered buttons. Shows",
                "plain, bordered, and mixed-height toolbars.",
                "",
                "[dim]Features:[/]",
                "  - Auto-height from tallest item per row",
                "  - Bordered buttons (3-row) in toolbar",
                "  - Mixed-height items with vertical alignment",
                "  - Sticky top toolbar positioning",
                "",
                "[dim]Controls used:[/]",
                "  - ToolbarControl",
                "  - ButtonControl, MarkupControl",
            },
            "Interactive Demo" => new List<string>
            {
                "[bold cyan]Interactive Demo[/]",
                "",
                "Shows real-time key press event handling.",
                "Press any key to see its details displayed.",
                "",
                "[dim]Controls used:[/]",
                "  - MarkupControl",
            },
            "Dropdown" => new List<string>
            {
                "[bold cyan]Meal Planner[/]",
                "",
                "Five cascading dropdowns with per-item colors",
                "and icons, live summary panel, and contextual",
                "meal suggestions based on cuisine and diet.",
                "",
                "[dim]Features:[/]",
                "  - 5 dropdowns with different configurations",
                "  - Per-item colors and emoji icons",
                "  - Live summary updates on every change",
                "  - Contextual meal suggestions lookup",
                "  - Type-ahead search, split-panel layout",
            },
            "List View" => new List<string>
            {
                "[bold cyan]Package Manager[/]",
                "",
                "NuGet-style package browser with checkbox mode,",
                "type-ahead search, custom formatting, hover",
                "highlighting, and a detail panel.",
                "",
                "[dim]Features:[/]",
                "  - Checkbox mode (Space to toggle)",
                "  - Type-ahead search (just type)",
                "  - Hover highlighting, auto-focus highlight",
                "  - Detail panel with package info",
                "  - Ctrl+A / Ctrl+D: select/deselect all",
                "  - Scrollbar with auto visibility",
            },
            "Table" => new List<string>
            {
                "[bold cyan]Employee Directory[/]",
                "",
                "Interactive table with 25 employees across 6",
                "departments. Sorting, filtering, inline editing,",
                "cell navigation, and column resize.",
                "",
                "[dim]Features:[/]",
                "  - Click header to sort columns",
                "  - / to filter with fuzzy matching",
                "  - F2 to inline edit Name/Title/Dept",
                "  - Tab/arrows for cell navigation",
                "  - Drag column borders to resize",
                "  - Markup-colored status column",
            },
            "DataGrid" => new List<string>
            {
                "[bold cyan]Interactive DataGrid[/]",
                "",
                "Full-featured DataGrid with 10,000 virtual rows,",
                "ITableDataSource, sorting, inline editing,",
                "cell navigation, column resize, and scrollbar drag.",
                "",
                "[dim]Features:[/]",
                "  - Virtual rendering (only visible rows)",
                "  - Click header to sort, F2 to edit",
                "  - Tab/arrows for cell navigation",
                "  - Drag column borders to resize",
                "  - Smooth scrollbar dragging",
            },
            "Nerd Fonts" => new List<string>
            {
                "[bold cyan]Nerd Font Showcase[/]",
                "",
                "Displays NerdFont icon families with auto-detection",
                "and ASCII fallback support.",
                "",
                "[dim]Controls used:[/]",
                "  - MarkupControl, HorizontalGridControl",
                "  - NerdFontHelper, Icons"
            },
            "Markup Syntax" => new List<string>
            {
                "[bold cyan]Markup Syntax Showcase[/]",
                "",
                "Demonstrates the rich markup system with colors,",
                "RGB/hex support, text decorations, backgrounds,",
                "nested tags, gradients, and escaping.",
                "",
                "[dim]Controls used:[/]",
                "  - MarkupControl, ScrollablePanelControl",
                "  - RuleControl",
            },
            "International & Emoji" => new List<string>
            {
                "[bold cyan]International & Emoji Showcase[/]",
                "",
                "Demonstrates Unicode support including CJK wide",
                "characters, surrogate pairs (U+10000+), emoji,",
                "world scripts, and accented Latin text.",
                "",
                "[dim]Features:[/]",
                "  - CJK ideographs (Chinese, Japanese, Korean)",
                "  - Surrogate pair characters (CJK Ext B, math, music)",
                "  - Wide and narrow emoji with markup styling",
                "  - Arabic, Hebrew, Devanagari, Thai, Georgian, etc.",
                "  - Flag emoji (regional indicator sequences)",
                "  - Gradient background + gradient text markup",
                "",
                "[dim]Controls used:[/]",
                "  - MarkupControl, ScrollablePanelControl",
                "  - RuleControl",
            },
            "Data Binding" => new List<string>
            {
                "[bold cyan]MVVM Data Binding[/]",
                "",
                "Demonstrates INotifyPropertyChanged-based data",
                "binding between a ViewModel and UI controls.",
                "",
                "[dim]Features:[/]",
                "  - One-way binding (VM to Control)",
                "  - One-way with converter (formatting)",
                "  - Two-way binding (VM and Control in sync)",
                "  - Live async updates through VM properties",
                "",
                "[dim]Controls used:[/]",
                "  - ProgressBarControl, MarkupControl",
                "  - CheckboxControl",
            },
            "Date & Time" => new List<string>
            {
                "[bold cyan]Date & Time[/]",
                "",
                "DatePicker and TimePicker controls with multiple",
                "format and culture configurations, constraints,",
                "and a combined event scheduling example.",
                "",
                "[dim]Features:[/]",
                "  - ISO, US, and European date formats",
                "  - 12h and 24h time formats with seconds",
                "  - Culture-aware formatting (en-US, de-DE)",
                "  - Min/max date and time constraints",
                "  - Combined date+time event scheduling",
                "  - Live status panel showing all values",
                "",
                "[dim]Controls used:[/]",
                "  - DatePickerControl, TimePickerControl",
                "  - MarkupControl, RuleControl",
                "  - HorizontalGridControl, ScrollablePanelControl",
            },
            "Graphs & Charts" => new List<string>
            {
                "[bold cyan]Graphs & Charts[/]",
                "",
                "Live sparklines, bar graphs, and progress bars",
                "with animated real-time updates.",
                "",
                "[dim]Controls used:[/]",
                "  - SparklineControl (Block, Braille, Bidirectional)",
                "  - BarGraphControl, ProgressBarControl",
            },
            "System Monitor" => new List<string>
            {
                "[bold cyan]System Monitor[/]",
                "",
                "Real-time system monitoring dashboard with multi-",
                "frequency updates. Simulates CPU, memory, disk, and",
                "network metrics with smooth transitions.",
                "",
                "[dim]Features:[/]",
                "  - Bar graphs with gradient color coding",
                "  - Block, Braille, and Bidirectional sparklines",
                "  - Live activity log with threshold alerts",
                "  - Status panel with system overview",
                "  - Multi-rate updates (100ms to 700ms)",
                "",
                "[dim]Controls used:[/]",
                "  - SparklineControl, BarGraphControl",
                "  - PanelControl, ListControl",
                "  - HorizontalGridControl, MarkupControl",
            },
            "Container Backgrounds" => new List<string>
            {
                "[bold cyan]Container Background Demo[/]",
                "",
                "Showcases how background colors and gradients",
                "propagate through nested containers.",
                "",
                "[dim]Cases demonstrated:[/]",
                "  - Controls with no bg (gradient preserved)",
                "  - Controls with explicit bg (gradient blocked)",
                "  - Grid with explicit bg → children inherit",
                "  - ScrollPanel ↔ Grid nesting (both directions)",
                "  - Nested containers with mixed bg settings",
                "",
                "[dim]Controls used:[/]",
                "  - PanelControl, ScrollablePanelControl",
                "  - HorizontalGridControl, MarkupControl",
                "  - RuleControl",
            },
            "Gradients" => new List<string>
            {
                "[bold cyan]Gradient Demo[/]",
                "",
                "Showcases gradient text markup and gradient",
                "window backgrounds with direction cycling.",
                "",
                "[dim]Features:[/]",
                "  - Gradient markup: spectrum, warm, cool, custom",
                "  - Background gradient with direction toggle",
                "  - Decorative gradient bars",
            },
            "Animations" => new List<string>
            {
                "[bold cyan]Animation Demo[/]",
                "",
                "Shows window animations including fade and slide",
                "transitions with selectable easing functions.",
                "",
                "[dim]Features:[/]",
                "  - Fade in/out animations",
                "  - Slide from all four directions",
                "  - 6 easing functions (Linear to Elastic)",
            },
            "Image Rendering" => new List<string>
            {
                "[bold cyan]Image Rendering Demo[/]",
                "",
                "Displays programmatic pixel art using half-block",
                "characters for 2-pixel-per-cell resolution.",
                "",
                "[dim]Features:[/]",
                "  - Rainbow bars, checkerboard, shapes",
                "  - Scale modes: Fit, Fill, Stretch, None",
                "  - ImageControl with PixelBuffer source",
            },
            "Image Viewer" => new List<string>
            {
                "[bold cyan]Image Viewer[/]",
                "",
                "Load and display real image files (PNG, JPEG, BMP,",
                "GIF, WebP, TIFF) with half-block Unicode rendering.",
                "",
                "[dim]Features:[/]",
                "  - File picker for loading images",
                "  - Four scale modes: Fit, Fill, Stretch, None",
                "  - Keyboard shortcuts: Ctrl+O, S, Esc",
                "  - Resizable window with live rescaling",
                "",
                "[dim]Powered by SixLabors.ImageSharp[/]",
            },
            "Built-in Dialogs" => new List<string>
            {
                "[bold cyan]Built-in Dialogs[/]",
                "",
                "Showcases all built-in dialog types: file pickers,",
                "folder picker, save dialog, about, settings,",
                "performance, and theme selector.",
                "",
                "[dim]Controls used:[/]",
                "  - ButtonControl, MarkupControl",
                "  - FileDialogs, AboutDialog, SettingsDialog",
                "  - PerformanceDialog, ThemeSelectorDialog",
            },
            "Digital Clock" => new List<string>
            {
                "[bold cyan]Digital Clock[/]",
                "",
                "A FIGlet-rendered digital clock that updates",
                "every second using async window thread.",
                "",
                "[dim]Controls used:[/]",
                "  - FigleControl",
            },
            "Log Viewer" => new List<string>
            {
                "[bold cyan]Log Viewer[/]",
                "",
                "Real-time log display with simulated entries",
                "and severity-based coloring.",
                "",
                "[dim]Controls used:[/]",
                "  - LogViewerControl",
            },
            "Notifications" => new List<string>
            {
                "[bold cyan]Notifications[/]",
                "",
                "Demonstrates the notification system with all",
                "severity levels: info, success, warning, danger.",
                "Includes modal, persistent, and multiline variants.",
                "",
                "[dim]Controls used:[/]",
                "  - ButtonControl, MarkupControl",
                "  - NotificationStateService",
            },
            "System Info" => new List<string>
            {
                "[bold cyan]System Information[/]",
                "",
                "Displays OS, runtime, memory, and processor",
                "details in a formatted view.",
                "",
                "[dim]Controls used:[/]",
                "  - MarkupControl",
            },
            "Terminal" => new List<string>
            {
                "[bold cyan]Terminal[/]",
                "",
                "A PTY-backed terminal emulator running",
                "the system shell.",
                "",
                "[dim]Controls used:[/]",
                "  - TerminalControl (PTY)",
            },
            "Slider" => new List<string>
            {
                "[bold cyan]Slider Controls[/]",
                "",
                "Single-value sliders and dual-thumb range sliders",
                "with keyboard and mouse interaction.",
                "",
                "[dim]Features:[/]",
                "  - Horizontal and vertical orientations",
                "  - Step and large step increments",
                "  - Value labels and min/max labels",
                "  - Mouse drag and click-to-jump",
                "  - RangeSlider with MinRange constraint",
                "  - Tab to switch active thumb (range)",
                "",
                "[dim]Controls used:[/]",
                "  - SliderControl, RangeSliderControl",
                "  - MarkupControl, HorizontalGridControl",
            },
            "Welcome Banner" => new List<string>
            {
                "[bold cyan]Welcome Banner[/]",
                "",
                "FIGlet ASCII art banner with project info.",
                "",
                "[dim]Controls used:[/]",
                "  - FigleControl, MarkupControl",
            },
            "Canvas Animations" => new List<string>
            {
                "[bold cyan]Canvas Animations[/]",
                "",
                "Three animated CanvasControl windows showcasing",
                "real-time drawing with BeginPaint/EndPaint and",
                "async animation loops.",
                "",
                "[dim]Starfield:[/] Parallax star layers scrolling left",
                "  with click-to-burst particle effects.",
                "[dim]Plasma:[/] Animated color plasma with sine-wave",
                "  patterns and click-to-add ripple distortions.",
                "[dim]Geometry:[/] Rotating polygons, orbiting triangles,",
                "  pulsing circles, arcs, and click-to-expand rings.",
                "",
                "[dim]Features:[/]",
                "  - CanvasControl with auto-size to fill window",
                "  - ~25 fps animation via WithAsyncWindowThread",
                "  - Mouse click interactions per canvas",
                "  - HSV color cycling and gradients",
                "  - Window resize auto-adapts canvas",
                "",
                "[dim]Controls used:[/]",
                "  - CanvasControl (SharpConsoleUI.Drawing)",
            },
            "Alpha Blending" => new List<string>
            {
                "[bold cyan]Alpha Blending[/]",
                "",
                "Per-cell alpha compositing across five interactive zones.",
                "Drag a slider to preview Color.Blend() live, watch a panel",
                "pulse via sin-wave alpha, and toggle animated gradient direction.",
                "",
                "[dim]Features:[/]",
                "  - Alpha ladder (8 opacity levels)",
                "  - Fade-to-transparent gradient strip",
                "  - Glass panels at 25/50/75/100% alpha",
                "  - Live Color.Blend() compositor with slider",
                "  - Animated pulse panel (sin-wave alpha)",
                "  - Animated background gradient (checkbox-controlled)",
            },
            "Panel Config" => new List<string>
            {
                "[bold cyan]Panel Configuration[/]",
                "",
                "Interactive control center for the desktop panel system.",
                "Toggle panels, add/remove elements at runtime, and",
                "customize colors and element settings.",
                "",
                "[dim]Features:[/]",
                "  - Show/hide top and bottom panels",
                "  - Add/remove clock, status text, separators",
                "  - Color presets for both panels",
                "  - Clock format and update interval",
                "  - Performance metrics toggle & options",
                "  - Task bar display configuration",
                "",
                "[dim]Panel elements:[/]",
                "  - StatusText, Separator, StartMenu",
                "  - TaskBar, Clock, Performance, Custom",
            },
            "Desktop Background" => new List<string>
            {
                "[bold cyan]Desktop Background[/]",
                "",
                "Configure the desktop background with solid colors,",
                "gradients, repeating patterns, animated effects,",
                "and combined gradient+pattern presets.",
                "",
                "[dim]Features:[/]",
                "  - Reset to theme default solid color",
                "  - 6 gradient presets (vertical, horizontal, diagonal)",
                "  - 11 pattern presets (checkerboard, dots, shading, etc.)",
                "  - 3 animated effects (color cycling, pulse, drifting)",
                "  - 5 combined gradient+pattern presets",
                "",
                "[dim]Uses:[/]",
                "  - DesktopBackgroundConfig, DesktopPatterns",
                "  - DesktopEffects, GradientBackground",
            },
            "HTML Rendering" => new List<string>
            {
                "[bold cyan]HTML Rendering[/]",
                "",
                "Renders HTML content directly in the terminal,",
                "inspired by the Lynx text browser.",
                "",
                "[dim]Features:[/]",
                "  - Full HTML parsing via AngleSharp",
                "  - CSS style support (inline + style blocks)",
                "  - Text formatting (bold, italic, underline)",
                "  - Links with click/hover events",
                "  - Tables with box-drawing borders",
                "  - Lists (ordered + unordered, nested)",
                "  - Blockquotes, code blocks, headings",
                "  - CSS Grid layout",
                "  - Async URL loading",
                "  - Scrollable content",
                "",
                "[dim]Controls used:[/]",
                "  - HtmlControl, HtmlBuilder",
            },
            "Video Player" => new List<string>
            {
                "[bold cyan]Video Player[/]",
                "",
                "Plays video files in the terminal using three",
                "rendering modes. Requires FFmpeg on PATH.",
                "",
                "[dim]Render Modes:[/]",
                "  - [white]Half-Block:[/] 2 pixels/cell, best color",
                "  - [white]ASCII:[/] Brightness-to-density characters",
                "  - [white]Braille:[/] 2x4 dots/cell, highest resolution",
                "",
                "[dim]Controls:[/]",
                "  Space — Play / Pause",
                "  M — Cycle render mode",
                "  L — Toggle looping",
                "  Esc — Stop playback",
                "",
                "[dim]Formats:[/] MP4, MKV, AVI, WebM, MOV, FLV, WMV",
                "",
                "[dim]Requires:[/] FFmpeg installed and on system PATH",
            },
            _ => null
        };
    }
}
