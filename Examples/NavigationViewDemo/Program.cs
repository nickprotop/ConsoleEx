using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
namespace NavigationViewDemo;

internal class Program
{
    static int Main(string[] args)
    {
        if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

        try
        {
            var windowSystem = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer));

            windowSystem.StatusBarStateService.ShowTopStatus = false;
            windowSystem.StatusBarStateService.ShowBottomStatus = false;

            CreateMainWindow(windowSystem);

            windowSystem.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            ExceptionFormatter.WriteException(ex);
            return 1;
        }
    }

    private static void CreateMainWindow(ConsoleWindowSystem ws)
    {
        var gradient = ColorGradient.FromColors(
            new Color(10, 15, 40),
            new Color(25, 40, 80),
            new Color(15, 20, 50));

        var nav = Controls.NavigationView()
            .WithNavWidth(28)
            .WithPaneHeader("[bold rgb(120,180,255)]  ◆  SharpConsoleUI[/]")
            .WithSelectedColors(Color.White, new Color(40, 80, 160))
            .WithContentBorder(BorderStyle.Rounded)
            .WithContentBorderColor(new Color(60, 80, 120))
            .WithContentBackground(new Color(20, 25, 45))
            .WithContentPadding(1, 0, 1, 0)
            .WithPaneDisplayMode(NavigationViewDisplayMode.Auto)
            .WithExpandedThreshold(80)
            .WithCompactThreshold(50)
            .AddHeader("Overview", new Color(100, 180, 255), h =>
            {
                h.AddItem("Dashboard", icon: "◈", subtitle: "System overview and status",
                    content: BuildDashboardPage);
                h.AddItem("Getting Started", icon: "▶", subtitle: "Quick start guide",
                    content: BuildGettingStartedPage);
            })
            .AddHeader("Controls", new Color(120, 220, 160), h =>
            {
                h.AddItem("Buttons & Inputs", icon: "◉", subtitle: "Interactive input controls",
                    content: BuildButtonsPage);
                h.AddItem("Lists & Trees", icon: "≡", subtitle: "Data display controls",
                    content: BuildListsPage);
                h.AddItem("Data Visualization", icon: "▥", subtitle: "Charts, graphs, and progress",
                    content: BuildDataVizPage);
                h.AddItem("Layout", icon: "⊞", subtitle: "Containers and layout controls",
                    content: BuildLayoutPage);
            })
            .AddHeader("Theming", new Color(220, 160, 100), h =>
            {
                h.AddItem("Colors & Gradients", icon: "◆", subtitle: "Color system and gradients",
                    content: BuildColorsPage);
                h.AddItem("Typography", icon: "A", subtitle: "Markup and text styling",
                    content: BuildTypographyPage);
            })
            .AddHeader("System", new Color(180, 140, 220), h =>
            {
                h.AddItem("About", icon: "ℹ", subtitle: "Application information",
                    content: BuildAboutPage);
            })
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        Window? window = null;
        window = new WindowBuilder(ws)
            .WithTitle("NavigationView Demo")
            .Maximized()
            .WithBackgroundGradient(gradient, GradientDirection.DiagonalDown)
            .WithColors(Color.White, Color.Black)
            .WithBorderStyle(BorderStyle.None)
            .HideTitle()
            .HideTitleButtons()
            .Movable(false)
            .Resizable(false)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow(window!);
                    e.Handled = true;
                }
            })
            .AddControl(nav)
            .BuildAndShow();
    }

    #region Dashboard Page

    private static void BuildDashboardPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(100,180,255)]System Dashboard[/]")
            .AddEmptyLine()
            .AddLine("Welcome to the [bold]SharpConsoleUI[/] NavigationView demo.")
            .AddLine("This full-screen application showcases the NavigationView")
            .AddLine("control with gradient backgrounds and rich content pages.")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("System Status")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.ProgressBar()
            .WithHeader("CPU Usage")
            .WithPercentage(42)
            .WithFilledColor(new Color(80, 160, 255))
            .WithUnfilledColor(new Color(40, 50, 70))
            .Stretch()
            .ShowPercentage()
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.ProgressBar()
            .WithHeader("Memory")
            .WithPercentage(67)
            .WithFilledColor(new Color(120, 220, 160))
            .WithUnfilledColor(new Color(40, 50, 70))
            .Stretch()
            .ShowPercentage()
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.ProgressBar()
            .WithHeader("Disk I/O")
            .WithPercentage(23)
            .WithFilledColor(new Color(220, 160, 100))
            .WithUnfilledColor(new Color(40, 50, 70))
            .Stretch()
            .ShowPercentage()
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("[dim]Last updated: just now[/]")
            .Build());
    }

    #endregion

    #region Getting Started Page

    private static void BuildGettingStartedPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(100,180,255)]Getting Started[/]")
            .AddEmptyLine()
            .AddLine("[bold]1. Create a window system[/]")
            .AddLine("[dim]   var ws = new ConsoleWindowSystem(driver);[/]")
            .AddEmptyLine()
            .AddLine("[bold]2. Build a window[/]")
            .AddLine("[dim]   new WindowBuilder(ws)[/]")
            .AddLine("[dim]       .WithTitle(\"My App\")[/]")
            .AddLine("[dim]       .Maximized()[/]")
            .AddLine("[dim]       .BuildAndShow();[/]")
            .AddEmptyLine()
            .AddLine("[bold]3. Add a NavigationView[/]")
            .AddLine("[dim]   Controls.NavigationView()[/]")
            .AddLine("[dim]       .WithNavWidth(28)[/]")
            .AddLine("[dim]       .AddHeader(\"Section\", h => {[/]")
            .AddLine("[dim]           h.AddItem(\"Page\", content: p => { ... });[/]")
            .AddLine("[dim]       })[/]")
            .AddLine("[dim]       .Build();[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Keyboard Navigation")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("  [bold rgb(120,180,255)]Tab[/]          Switch between nav and content panes")
            .AddLine("  [bold rgb(120,180,255)]Up/Down[/]      Navigate items in the nav pane")
            .AddLine("  [bold rgb(120,180,255)]Enter[/]        Select / expand or collapse headers")
            .AddLine("  [bold rgb(120,180,255)]Left[/]         Collapse parent header")
            .AddLine("  [bold rgb(120,180,255)]Right[/]        Expand header / move to content")
            .AddLine("  [bold rgb(120,180,255)]Home/End[/]     Jump to first/last item")
            .AddLine("  [bold rgb(120,180,255)]Esc[/]          Quit the application")
            .Build());
    }

    #endregion

    #region Buttons & Inputs Page

    private static void BuildButtonsPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(120,220,160)]Buttons & Input Controls[/]")
            .AddEmptyLine()
            .AddLine("Interactive controls for user input and actions.")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Buttons")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.Button("Primary Action")
            .WithWidth(24)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.Button("Secondary")
            .WithWidth(24)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Checkboxes")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.Checkbox("Enable dark mode")
            .Checked(true)
            .Build());

        panel.AddControl(Controls.Checkbox("Show line numbers")
            .Checked(true)
            .Build());

        panel.AddControl(Controls.Checkbox("Word wrap")
            .Build());

        panel.AddControl(Controls.Checkbox("Auto-indent")
            .Checked(true)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Text Input")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.Prompt("Search: ")
            .Build());
    }

    #endregion

    #region Lists & Trees Page

    private static void BuildListsPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(120,220,160)]Lists & Trees[/]")
            .AddEmptyLine()
            .AddLine("Controls for displaying collections and hierarchies.")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("List Control")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.List("Project Files")
            .AddItems(
                "Program.cs",
                "App.xaml.cs",
                "MainWindow.cs",
                "Settings.json",
                "README.md",
                "LICENSE")
            .MaxVisibleItems(8)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Tree Control")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        var tree = Controls.Tree()
            .WithHeight(10)
            .Build();

        var root = tree.AddRootNode("Solution");
        var src = root.AddChild("src");
        src.AddChild("Controls");
        src.AddChild("Builders");
        src.AddChild("Helpers");
        src.AddChild("Themes");

        var tests = root.AddChild("tests");
        tests.AddChild("Unit");
        tests.AddChild("Integration");

        var docs = root.AddChild("docs");
        docs.AddChild("guides");
        docs.AddChild("api");

        panel.AddControl(tree);
    }

    #endregion

    #region Data Visualization Page

    private static void BuildDataVizPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(120,220,160)]Data Visualization[/]")
            .AddEmptyLine()
            .AddLine("Charts, progress bars, and sparklines for data display.")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Progress Bars")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.ProgressBar()
            .WithHeader("Build")
            .WithPercentage(100)
            .WithFilledColor(new Color(80, 200, 120))
            .WithUnfilledColor(new Color(40, 50, 70))
            .Stretch()
            .ShowPercentage()
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.ProgressBar()
            .WithHeader("Tests")
            .WithPercentage(85)
            .WithFilledColor(new Color(100, 180, 255))
            .WithUnfilledColor(new Color(40, 50, 70))
            .Stretch()
            .ShowPercentage()
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.ProgressBar()
            .WithHeader("Coverage")
            .WithPercentage(73)
            .WithFilledColor(new Color(220, 180, 60))
            .WithUnfilledColor(new Color(40, 50, 70))
            .Stretch()
            .ShowPercentage()
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Sparkline")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.Sparkline()
            .WithTitle("Network Traffic (Mbps)")
            .WithData(new double[] { 12, 45, 28, 67, 34, 89, 56, 23, 78, 45, 62, 38, 91, 55, 42, 70, 33, 85, 48, 60 })
            .WithBarColor(new Color(100, 180, 255))
            .WithBackgroundColor(new Color(40, 50, 70))
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Bar Graphs")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.BarGraph()
            .WithLabel("Monday")
            .WithValue(45)
            .WithMaxValue(100)
            .WithFilledColor(new Color(80, 160, 255))
            .WithUnfilledColor(new Color(40, 50, 70))
            .ShowLabel()
            .ShowValue()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build());

        panel.AddControl(Controls.BarGraph()
            .WithLabel("Tuesday")
            .WithValue(72)
            .WithMaxValue(100)
            .WithFilledColor(new Color(120, 220, 160))
            .WithUnfilledColor(new Color(40, 50, 70))
            .ShowLabel()
            .ShowValue()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build());

        panel.AddControl(Controls.BarGraph()
            .WithLabel("Wednesday")
            .WithValue(58)
            .WithMaxValue(100)
            .WithFilledColor(new Color(220, 180, 60))
            .WithUnfilledColor(new Color(40, 50, 70))
            .ShowLabel()
            .ShowValue()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build());

        panel.AddControl(Controls.BarGraph()
            .WithLabel("Thursday")
            .WithValue(91)
            .WithMaxValue(100)
            .WithFilledColor(new Color(220, 120, 80))
            .WithUnfilledColor(new Color(40, 50, 70))
            .ShowLabel()
            .ShowValue()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build());

        panel.AddControl(Controls.BarGraph()
            .WithLabel("Friday")
            .WithValue(36)
            .WithMaxValue(100)
            .WithFilledColor(new Color(180, 140, 220))
            .WithUnfilledColor(new Color(40, 50, 70))
            .ShowLabel()
            .ShowValue()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build());
    }

    #endregion

    #region Layout Page

    private static void BuildLayoutPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(120,220,160)]Layout Controls[/]")
            .AddEmptyLine()
            .AddLine("Containers and layout primitives for building complex UIs.")
            .AddEmptyLine()
            .AddLine("[bold]Available containers:[/]")
            .AddEmptyLine()
            .AddLine("  [bold rgb(120,180,255)]HorizontalGridControl[/]")
            .AddLine("  Multi-column layout with flexible or fixed widths.")
            .AddLine("  Supports SplitterControl for resizable columns.")
            .AddEmptyLine()
            .AddLine("  [bold rgb(120,180,255)]ScrollablePanelControl[/]")
            .AddLine("  Scrollable container with optional border and padding.")
            .AddLine("  Supports keyboard and mouse scroll.")
            .AddEmptyLine()
            .AddLine("  [bold rgb(120,180,255)]PanelControl[/]")
            .AddLine("  Bordered container for grouping related content.")
            .AddEmptyLine()
            .AddLine("  [bold rgb(120,180,255)]TabControl[/]")
            .AddLine("  Tabbed container with switchable content areas.")
            .AddEmptyLine()
            .AddLine("  [bold rgb(120,180,255)]NavigationView[/]")
            .AddLine("  WinUI-inspired nav pane with content switching.")
            .AddLine("  (This is what you're looking at right now!)")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Alignment Options")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("  [bold]Horizontal:[/] Left, Center, Right, Stretch")
            .AddLine("  [bold]Vertical:[/]   Top, Center, Bottom, Fill")
            .AddLine("  [bold]Sticky:[/]     Top, Bottom (stays in view while scrolling)")
            .Build());
    }

    #endregion

    #region Colors & Gradients Page

    private static void BuildColorsPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(220,160,100)]Colors & Gradients[/]")
            .AddEmptyLine()
            .AddLine("SharpConsoleUI supports full 24-bit RGB color and gradient backgrounds.")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Color Palette")
            .WithColor(new Color(160, 120, 60))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("  [on rgb(220,60,60)]  Red     [/]  [on rgb(220,140,40)]  Orange  [/]  [on rgb(220,200,40)]  Yellow  [/]")
            .AddLine("  [on rgb(60,180,60)]  Green   [/]  [on rgb(40,140,220)]  Blue    [/]  [on rgb(140,60,220)]  Purple  [/]")
            .AddLine("  [on rgb(220,80,160)]  Pink    [/]  [on rgb(60,200,200)]  Cyan    [/]  [on rgb(160,160,160)]  Gray    [/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Gradient Directions")
            .WithColor(new Color(160, 120, 60))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("  [bold]Horizontal[/]    Left to right")
            .AddLine("  [bold]Vertical[/]      Top to bottom")
            .AddLine("  [bold]DiagonalDown[/]  Top-left to bottom-right")
            .AddLine("  [bold]DiagonalUp[/]    Bottom-left to top-right")
            .AddEmptyLine()
            .AddLine("[bold]Predefined gradients:[/]")
            .AddEmptyLine()
            .AddLine("  [rgb(0,0,255)]■■■[/][rgb(0,128,255)]■■■[/][rgb(0,255,255)]■■■[/]  [dim]cool[/]")
            .AddLine("  [rgb(255,255,0)]■■■[/][rgb(255,165,0)]■■■[/][rgb(255,0,0)]■■■[/]  [dim]warm[/]")
            .AddLine("  [rgb(0,0,255)]■■[/][rgb(0,255,0)]■■[/][rgb(255,255,0)]■■[/][rgb(255,0,0)]■■[/]  [dim]spectrum[/]")
            .AddLine("  [rgb(30,30,30)]■■■[/][rgb(128,128,128)]■■■[/][rgb(255,255,255)]■■■[/]  [dim]grayscale[/]")
            .AddEmptyLine()
            .AddLine("[dim]This window uses a DiagonalDown gradient background.[/]")
            .Build());
    }

    #endregion

    #region Typography Page

    private static void BuildTypographyPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(220,160,100)]Typography & Markup[/]")
            .AddEmptyLine()
            .AddLine("Rich text formatting using Spectre-compatible markup syntax.")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Text Styles")
            .WithColor(new Color(160, 120, 60))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("  [bold]Bold text[/]")
            .AddLine("  [dim]Dimmed text[/]")
            .AddLine("  [italic]Italic text[/]")
            .AddLine("  [underline]Underlined text[/]")
            .AddLine("  [strikethrough]Strikethrough text[/]")
            .AddLine("  [bold italic underline]Combined styles[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Colors")
            .WithColor(new Color(160, 120, 60))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("  [red]Red[/] [green]Green[/] [blue]Blue[/] [yellow]Yellow[/] [cyan1]Cyan[/] [magenta1]Magenta[/]")
            .AddLine("  [rgb(255,128,0)]Custom RGB (255,128,0)[/]")
            .AddLine("  [bold white on rgb(60,60,180)] Background colors [/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Markup Syntax")
            .WithColor(new Color(160, 120, 60))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("[dim]  Syntax:    [bold]\\[style]text\\[/][/][/]")
            .AddEmptyLine()
            .AddLine("[dim]  Examples:[/]")
            .AddLine("[dim]    \\[bold]Bold\\[/]              →  [/][bold]Bold[/]")
            .AddLine("[dim]    \\[red]Red text\\[/]          →  [/][red]Red text[/]")
            .AddLine("[dim]    \\[bold cyan1]Combined\\[/]    →  [/][bold cyan1]Combined[/]")
            .AddLine("[dim]    \\[on blue]Background\\[/]    →  [/][on blue]Background[/]")
            .Build());
    }

    #endregion

    #region About Page

    private static void BuildAboutPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(180,140,220)]About[/]")
            .AddEmptyLine()
            .AddLine("[bold]SharpConsoleUI[/]")
            .AddLine("[dim]A modern .NET console windowing system[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Features")
            .WithColor(new Color(120, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("  ◆ Full 24-bit RGB color and gradient backgrounds")
            .AddLine("  ◆ Fluent builder patterns for window and control creation")
            .AddLine("  ◆ Rich set of controls: lists, trees, tables, charts")
            .AddLine("  ◆ NavigationView with WinUI-inspired layout")
            .AddLine("  ◆ Async/await patterns throughout")
            .AddLine("  ◆ Plugin architecture for extensibility")
            .AddLine("  ◆ Mouse support with click, drag, and scroll")
            .AddLine("  ◆ Unicode-aware rendering with wide character support")
            .AddLine("  ◆ Double-buffered rendering with dirty region tracking")
            .AddLine("  ◆ Theming system with customizable styles")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("System Info")
            .WithColor(new Color(120, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine($"  [dim]Runtime:[/]  {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}")
            .AddLine($"  [dim]OS:[/]       {System.Runtime.InteropServices.RuntimeInformation.OSDescription}")
            .AddLine($"  [dim]License:[/]  MIT")
            .AddLine($"  [dim]Author:[/]   Nikolaos Protopapas")
            .Build());
    }

    #endregion
}
