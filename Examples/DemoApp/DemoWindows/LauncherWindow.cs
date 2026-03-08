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
        "",
        "[dim]Press Enter to launch a demo.[/]"
    };

    public static Window Create(ConsoleWindowSystem ws)
    {
        // Build controls first
        var demoTree = Controls.Tree()
            .WithGuide(TreeGuide.Line)
            .WithHighlightColors(Color.White, Color.Blue)
            .Build();

        BuildDemoTree(demoTree);

        var detailPane = Controls.Markup()
            .AddLines(DetailPlaceholder.ToArray())
            .WithMargin(1, 1, 1, 1)
            .Build();

        // Update detail pane when tree selection changes
        demoTree.SelectedNodeChanged += (sender, args) =>
        {
            if (args.Node != null)
            {
                var info = GetDemoInfo(args.Node.Text);
                if (info != null)
                    detailPane.SetContent(info);
            }
        };

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
            .Centered()
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
        controls.AddChild("Nerd Fonts");
        controls.AddChild("Markup Syntax");

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
            "Nerd Fonts" => NerdFontWindow.Create(ws),
            "Markup Syntax" => MarkupSyntaxWindow.Create(ws),
            "Graphs & Charts" => GraphsWindow.Create(ws),
            "Gradients" => GradientDemoWindow.Create(ws),
            "Animations" => AnimationDemoWindow.Create(ws),
            "Image Rendering" => ImageDemoWindow.Create(ws),
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "  - NerdFontHelper, Icons",
                "",
                "[green]\\[Enter] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
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
                "",
                "[green][[Enter]] Launch Demo[/]"
            },
            "Welcome Banner" => new List<string>
            {
                "[bold cyan]Welcome Banner[/]",
                "",
                "FIGlet ASCII art banner with project info.",
                "",
                "[dim]Controls used:[/]",
                "  - FigleControl, MarkupControl",
                "",
                "[green][[Enter]] Launch Demo[/]"
            },
            _ => null
        };
    }
}
