using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class LauncherWindow
{
    private static readonly List<string> DetailPlaceholder = new()
    {
        "[bold]Welcome to SharpConsoleUI[/]",
        "",
        "Select a demo from the tree to see its description.",
    };

    public static Window Create(ConsoleWindowSystem ws)
    {
        // Build controls first
        var demoTree = Controls.Tree()
            .WithGuide(TreeGuide.Line)
            .WithHighlightColors(Color.White, Color.Blue)
            .Build();

        BuildDemoTree(demoTree);

        var detailMarkup = Controls.Markup()
            .AddLines(DetailPlaceholder.ToArray())
            .WithMargin(1, 1, 1, 1)
            .Build();

        var launchButton = Controls.Button()
            .WithText("  Launch Demo  ")
            .WithMargin(1, 1, 0, 0)
            .WithBorder(ButtonBorderStyle.Rounded)
            .Build();

        launchButton.Visible = false;

        var detailPane = new ScrollablePanelControl();
        detailPane.AddControl(detailMarkup);
        detailPane.AddControl(launchButton);

        // Update detail pane when tree selection changes
        demoTree.SelectedNodeChanged += (sender, args) =>
        {
            if (args.Node != null)
            {
                var info = GetDemoInfo(args.Node.Text);
                if (info != null)
                {
                    detailMarkup.SetContent(info);
                    launchButton.Visible = true;
                }
                else
                {
                    launchButton.Visible = false;
                }
            }
        };

        // Launch demo on button click
        launchButton.Click += (sender, btn) => LaunchSelectedDemo(ws, demoTree);

        // Launch demo on double-click / Enter
        demoTree.NodeActivated += (sender, args) =>
        {
            if (args.Node != null)
                LaunchSelectedDemo(ws, demoTree);
        };

        var grid = Controls.HorizontalGrid()
            .Column(col => col.Width(30).Add(demoTree))
            .Column(col => col.Add(detailPane))
            .WithSplitterAfter(0)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("SharpConsoleUI Demo")
            .WithSize(90, 30)
            .AtPosition(0, 0)
            .AddControl(grid)
            .BuildAndShow();
    }

    private static void BuildDemoTree(TreeControl tree)
    {
        var layout = tree.AddRootNode("Layout & Windows");
        layout.TextColor = Color.Cyan1;
        layout.IsExpanded = true;
        layout.AddChild("IDE Layout");
        layout.AddChild("File Explorer");
        layout.AddChild("Multi-Tab Demo");

        var controls = tree.AddRootNode("Controls");
        controls.TextColor = Color.Green;
        controls.IsExpanded = true;
        controls.AddChild("Interactive Demo");
        controls.AddChild("Dropdown");
        controls.AddChild("List View");
        controls.AddChild("Table");
        controls.AddChild("DataGrid");
        controls.AddChild("Nerd Fonts");
        controls.AddChild("Markup Syntax");
        controls.AddChild("Data Binding");

        var dataViz = tree.AddRootNode("Data Visualization");
        dataViz.TextColor = Color.Yellow;
        dataViz.IsExpanded = true;
        dataViz.AddChild("Graphs & Charts");

        var rendering = tree.AddRootNode("Rendering");
        rendering.TextColor = Color.Orange1;
        rendering.IsExpanded = true;
        rendering.AddChild("Gradients");
        rendering.AddChild("Animations");
        rendering.AddChild("Image Rendering");
        rendering.AddChild("Image Viewer");

        var utilities = tree.AddRootNode("Utilities");
        utilities.TextColor = Color.Magenta1;
        utilities.IsExpanded = true;
        utilities.AddChild("Built-in Dialogs");
        utilities.AddChild("Digital Clock");
        utilities.AddChild("Log Viewer");
        utilities.AddChild("Notifications");
        utilities.AddChild("System Info");
        utilities.AddChild("Terminal");
        utilities.AddChild("Welcome Banner");
    }

    private static void LaunchSelectedDemo(ConsoleWindowSystem ws, TreeControl tree)
    {
        var node = tree.SelectedNode;
        if (node == null || node.Children.Count > 0) return;

        _ = node.Text switch
        {
            "IDE Layout" => IdeLayoutWindow.Create(ws),
            "File Explorer" => FileExplorerWindow.Create(ws),
            "Multi-Tab Demo" => TabDemoWindow.Create(ws),
            "Interactive Demo" => InteractiveWindow.Create(ws),
            "Dropdown" => DropdownWindow.Create(ws),
            "List View" => ListViewWindow.Create(ws),
            "Table" => TableDemoWindow.Create(ws),
            "DataGrid" => DataGridWindow.Create(ws),
            "Nerd Fonts" => NerdFontWindow.Create(ws),
            "Markup Syntax" => MarkupSyntaxWindow.Create(ws),
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
                "[bold cyan]Dropdown Demo[/]",
                "",
                "Demonstrates dropdown controls with searchable",
                "selection from a list of items.",
                "",
                "[dim]Controls used:[/]",
                "  - DropdownControl",
            },
            "List View" => new List<string>
            {
                "[bold cyan]List View[/]",
                "",
                "A scrollable list with diverse item types,",
                "selection, and keyboard navigation.",
                "",
                "[dim]Controls used:[/]",
                "  - ListControl",
            },
            "Table" => new List<string>
            {
                "[bold cyan]Table Demo[/]",
                "",
                "Displays tabular data with columns, rows,",
                "and rounded borders.",
                "",
                "[dim]Controls used:[/]",
                "  - TableControl",
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
