using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class WinUIDemoWindow
{
    private const int WindowWidth = 80;
    private const int WindowHeight = 32;

    public static Window Create(ConsoleWindowSystem ws)
    {
        var gradient = ColorGradient.FromColors(
            new Color(15, 25, 60),
            new Color(5, 5, 15));

        var nav = Controls.NavigationView()
            .WithNavWidth(26)
            .WithPaneHeader("[bold white]  ⚙  Settings[/]")
            .WithContentBorder(BorderStyle.Rounded)
            .WithContentBorderColor(Color.Grey37)
            .WithContentBackground(new Color(30, 30, 40))
            .WithContentPadding(1, 0, 1, 0)
            .AddItem("Home", subtitle: "Configure your preferences", content: panel =>
            {
                panel.AddControl(Controls.Markup()
                    .AddLine("[bold cyan]Welcome[/]")
                    .AddEmptyLine()
                    .AddLine("This demo showcases a WinUI-inspired layout")
                    .AddLine("using SharpConsoleUI controls.")
                    .AddEmptyLine()
                    .AddLine("[dim]Features demonstrated:[/]")
                    .AddLine("  • Gradient background transparency")
                    .AddLine("  • ScrollablePanel with rounded border")
                    .AddLine("  • Left navigation with selection state")
                    .AddLine("  • Dynamic content switching")
                    .AddEmptyLine()
                    .AddLine("[dim]Click a nav item on the left to switch sections.[/]")
                    .Build());
            })
            .AddItem("Settings", subtitle: "General application settings", content: panel =>
            {
                panel.AddControl(Controls.Markup()
                    .AddLine("[bold cyan]General Settings[/]")
                    .AddEmptyLine()
                    .Build());
                panel.AddControl(Controls.Checkbox("Enable notifications")
                    .Checked(true)
                    .Build());
                panel.AddControl(Controls.Checkbox("Auto-save on exit")
                    .Checked(true)
                    .Build());
                panel.AddControl(Controls.Checkbox("Show status bar")
                    .Build());
                panel.AddControl(Controls.Checkbox("Enable telemetry")
                    .Build());
                panel.AddControl(Controls.Markup()
                    .AddEmptyLine()
                    .AddLine("[bold cyan]Advanced[/]")
                    .AddEmptyLine()
                    .Build());
                panel.AddControl(Controls.Checkbox("Developer mode")
                    .Build());
                panel.AddControl(Controls.Checkbox("Verbose logging")
                    .Build());
                panel.AddControl(Controls.Checkbox("Experimental features")
                    .Build());
            })
            .AddItem("Appearance", subtitle: "Customize the look and feel", content: panel =>
            {
                panel.AddControl(Controls.Markup()
                    .AddLine("[bold cyan]Theme[/]")
                    .AddEmptyLine()
                    .AddLine("[dim]Current theme:[/] [bold]Dark[/]")
                    .AddEmptyLine()
                    .AddLine("[bold cyan]Colors[/]")
                    .AddEmptyLine()
                    .AddLine($"  [on rgb(60,60,180)]  Accent  [/]  [on rgb(40,120,40)]  Success [/]  [on rgb(180,60,60)]  Danger  [/]")
                    .AddEmptyLine()
                    .AddLine("[bold cyan]Font Size[/]")
                    .AddEmptyLine()
                    .Build());
                panel.AddControl(Controls.ProgressBar()
                    .WithHeader("Scale")
                    .WithPercentage(50)
                    .WithFilledColor(Color.Cyan1)
                    .WithUnfilledColor(Color.Grey23)
                    .Stretch()
                    .ShowPercentage()
                    .Build());
                panel.AddControl(Controls.Markup()
                    .AddEmptyLine()
                    .AddLine("[dim]Drag the slider to adjust UI scale.[/]")
                    .Build());
            })
            .AddItem("Privacy", subtitle: "Manage your privacy options", content: panel =>
            {
                panel.AddControl(Controls.Markup()
                    .AddLine("[bold cyan]Privacy Settings[/]")
                    .AddEmptyLine()
                    .Build());
                panel.AddControl(Controls.Checkbox("Share usage data")
                    .Build());
                panel.AddControl(Controls.Checkbox("Allow personalized content")
                    .Checked(true)
                    .Build());
                panel.AddControl(Controls.Checkbox("Location services")
                    .Build());
                panel.AddControl(Controls.Markup()
                    .AddEmptyLine()
                    .AddLine("[bold cyan]Data Management[/]")
                    .AddEmptyLine()
                    .AddLine("[dim]Clear browsing data, cookies, and cache.[/]")
                    .AddEmptyLine()
                    .Build());
                panel.AddControl(Controls.Button("Clear All Data")
                    .WithWidth(20)
                    .Build());
            })
            .AddItem("About", subtitle: "Application information", content: panel =>
            {
                panel.AddControl(Controls.Markup()
                    .AddLine("[bold cyan]About[/]")
                    .AddEmptyLine()
                    .AddLine("[bold]SharpConsoleUI[/]")
                    .AddLine("[dim]Version 1.0.0[/]")
                    .AddEmptyLine()
                    .AddLine("A modern TUI framework for .NET with rich")
                    .AddLine("controls, gradient rendering, and Unicode support.")
                    .AddEmptyLine()
                    .AddLine("[dim]License:[/] MIT")
                    .AddLine("[dim]Author:[/]  Nikolaos Protopapas")
                    .AddEmptyLine()
                    .AddLine("[bold cyan]System[/]")
                    .AddEmptyLine()
                    .AddLine($"[dim]Runtime:[/]  {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}")
                    .AddLine($"[dim]OS:[/]       {System.Runtime.InteropServices.RuntimeInformation.OSDescription}")
                    .Build());
            })
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        Window? window = null;
        window = new WindowBuilder(ws)
            .WithTitle("WinUI Layout")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
            .WithColors(Color.White, Color.Black)
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

        return window;
    }
}
