using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace ProjectName;

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
            .WithPaneHeader("[bold rgb(120,180,255)]  ◆  PROJECT_TITLE[/]")
            .WithSelectedColors(Color.White, new Color(40, 80, 160))
            .WithContentBorder(BorderStyle.Rounded)
            .WithContentBorderColor(new Color(60, 80, 120))
            .WithContentBackground(new Color(20, 25, 45))
            .WithContentPadding(1, 0, 1, 0)
            .AddHeader("Main", new Color(100, 180, 255), h =>
            {
                h.AddItem("Home", icon: "◈", subtitle: "Welcome and overview",
                    content: BuildHomePage);
                h.AddItem("Settings", icon: "⚙", subtitle: "Application preferences",
                    content: BuildSettingsPage);
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
            .WithTitle("PROJECT_TITLE")
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
                    ws.Stop();
                    e.Handled = true;
                }
            })
            .AddControl(nav)
            .BuildAndShow();
    }

    private static void BuildHomePage(ScrollablePanelControl panel)
    {
        panel.AddControl(new MarkupControl(
            "[bold rgb(100,180,255)]Welcome to PROJECT_TITLE[/]\n\n" +
            "This is your new SharpConsoleUI application.\n" +
            "Use the navigation panel to explore different sections.\n\n" +
            "[dim]Press Esc to quit.[/]"));

        panel.AddControl(new MarkupControl("\n[bold]Progress[/]"));

        var progress1 = Controls.ProgressBar()
            .WithLabel("Setup")
            .WithPercentage(100)
            .WithBarColor(new Color(100, 180, 255))
            .Build();
        panel.AddControl(progress1);

        var progress2 = Controls.ProgressBar()
            .WithLabel("Configuration")
            .WithPercentage(60)
            .WithBarColor(new Color(120, 220, 160))
            .Build();
        panel.AddControl(progress2);

        var progress3 = Controls.ProgressBar()
            .WithLabel("Customization")
            .WithPercentage(25)
            .WithBarColor(new Color(220, 160, 100))
            .Build();
        panel.AddControl(progress3);
    }

    private static void BuildSettingsPage(ScrollablePanelControl panel)
    {
        panel.AddControl(new MarkupControl(
            "[bold rgb(100,180,255)]Settings[/]\n"));

        var darkMode = Controls.Checkbox()
            .WithLabel("Dark mode")
            .WithChecked(true)
            .Build();
        panel.AddControl(darkMode);

        var notifications = Controls.Checkbox()
            .WithLabel("Enable notifications")
            .WithChecked(true)
            .Build();
        panel.AddControl(notifications);

        var autoSave = Controls.Checkbox()
            .WithLabel("Auto-save on exit")
            .Build();
        panel.AddControl(autoSave);

        panel.AddControl(new MarkupControl("\n[dim]Toggle options with Space or Enter.[/]"));
    }

    private static void BuildAboutPage(ScrollablePanelControl panel)
    {
        var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

        panel.AddControl(new MarkupControl(
            $"[bold rgb(180,140,220)]About PROJECT_TITLE[/]\n\n" +
            $"[bold]Version:[/] 1.0.0\n" +
            $"[bold]Runtime:[/] {runtime}\n" +
            $"[bold]OS:[/] {os}\n\n" +
            $"Built with [bold rgb(100,180,255)]SharpConsoleUI[/]"));
    }
}
