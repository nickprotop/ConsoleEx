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
                .AddItem("WinUI Layout", subtitle: "WinUI-inspired settings layout", content: MakeInfoPanel("WinUI Layout")))
            .AddHeader("Controls", Color.Green, header => header
                .AddItem("Interactive Demo", subtitle: "Real-time key press handling", content: MakeInfoPanel("Interactive Demo"))
                .AddItem("Dropdown", subtitle: "Cascading dropdowns", content: MakeInfoPanel("Dropdown"))
                .AddItem("List View", subtitle: "NuGet-style package browser", content: MakeInfoPanel("List View"))
                .AddItem("Table", subtitle: "Interactive employee directory", content: MakeInfoPanel("Table"))
                .AddItem("DataGrid", subtitle: "Virtual DataGrid with 10K rows", content: MakeInfoPanel("DataGrid"))
                .AddItem("Nerd Fonts", subtitle: "NerdFont icon showcase", content: MakeInfoPanel("Nerd Fonts"))
                .AddItem("Markup Syntax", subtitle: "Rich markup system demo", content: MakeInfoPanel("Markup Syntax"))
                .AddItem("International & Emoji", subtitle: "Unicode & emoji support", content: MakeInfoPanel("International & Emoji"))
                .AddItem("Data Binding", subtitle: "MVVM data binding", content: MakeInfoPanel("Data Binding")))
            .AddHeader("Data Visualization", Color.Yellow, header => header
                .AddItem("Graphs & Charts", subtitle: "Live sparklines & bar graphs", content: MakeInfoPanel("Graphs & Charts")))
            .AddHeader("Rendering", Color.Orange1, header => header
                .AddItem("Gradients", subtitle: "Gradient text & backgrounds", content: MakeInfoPanel("Gradients"))
                .AddItem("Animations", subtitle: "Window animations & easing", content: MakeInfoPanel("Animations"))
                .AddItem("Image Rendering", subtitle: "Pixel art with half-blocks", content: MakeInfoPanel("Image Rendering"))
                .AddItem("Image Viewer", subtitle: "Load & display image files", content: MakeInfoPanel("Image Viewer")))
            .AddHeader("Utilities", Color.Magenta1, header => header
                .AddItem("Built-in Dialogs", subtitle: "File pickers & system dialogs", content: MakeInfoPanel("Built-in Dialogs"))
                .AddItem("Digital Clock", subtitle: "FIGlet-rendered clock", content: MakeInfoPanel("Digital Clock"))
                .AddItem("Log Viewer", subtitle: "Real-time log display", content: MakeInfoPanel("Log Viewer"))
                .AddItem("Notifications", subtitle: "Notification system demo", content: MakeInfoPanel("Notifications"))
                .AddItem("System Info", subtitle: "OS & runtime details", content: MakeInfoPanel("System Info"))
                .AddItem("Terminal", subtitle: "PTY-backed terminal emulator", content: MakeInfoPanel("Terminal"))
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

        var gradient = ColorGradient.FromColors(
            new Color(15, 25, 60),
            new Color(5, 5, 15));

        return new WindowBuilder(ws)
            .WithTitle("SharpConsoleUI Demo")
            .WithSize(90, 30)
            .AtPosition(0, 0)
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
            .AddControl(nav)
            .BuildAndShow();
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
            "Interactive Demo" => InteractiveWindow.Create(ws),
            "Dropdown" => DropdownWindow.Create(ws),
            "List View" => ListViewWindow.Create(ws),
            "Table" => TableDemoWindow.Create(ws),
            "DataGrid" => DataGridWindow.Create(ws),
            "Nerd Fonts" => NerdFontWindow.Create(ws),
            "Markup Syntax" => MarkupSyntaxWindow.Create(ws),
            "International & Emoji" => InternationalWindow.Create(ws),
            "Data Binding" => DataBindingWindow.Create(ws),
            "Graphs & Charts" => GraphsWindow.Create(ws),
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
            "Welcome Banner" => WelcomeWindow.Create(ws),
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
            "Welcome Banner" => new List<string>
            {
                "[bold cyan]Welcome Banner[/]",
                "",
                "FIGlet ASCII art banner with project info.",
                "",
                "[dim]Controls used:[/]",
                "  - FigleControl, MarkupControl",
            },
            _ => null
        };
    }
}
