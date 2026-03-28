using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;
using DemoApp.DemoWindows;
using Color = SharpConsoleUI.Color;

namespace DemoApp;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

        try
        {
            var options = new ConsoleWindowSystemOptions(
                StatusBarOptions: new StatusBarOptions(
                    ShowStartButton: true,
                    StartMenu: new StartMenuOptions
                    {
                        AppName = "SharpConsoleUI Demo",
                        SidebarStyle = StartMenuSidebarStyle.IconLabel,
                        BackgroundGradient = new GradientBackground(
                            ColorGradient.FromColors(new Color(25, 25, 60), new Color(15, 15, 35)),
                            GradientDirection.Vertical)
                    })
            );
            var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer), options: options);
            using var disposables = new DisposableManager();

            windowSystem.StatusBarStateService.TopStatus = "SharpConsoleUI Demo | Ctrl+T: Theme Selector";

            // Register a sample custom settings group
            windowSystem.RegisterSettingsGroup("Demo", new Color(255, 200, 100), group =>
            {
                group.AddPage("About Demo", icon: "★", subtitle: "Demo application info",
                    content: panel =>
                    {
                        panel.AddControl(Controls.Markup()
                            .AddLine("[bold rgb(255,200,100)]Demo Application[/]")
                            .AddEmptyLine()
                            .AddLine("[dim]This is a sample custom settings page[/]")
                            .AddLine("[dim]registered via the extensibility API.[/]")
                            .Build());
                    });
            });

            LauncherWindow.Create(windowSystem);

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
}
