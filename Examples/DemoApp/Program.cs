using SharpConsoleUI;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using DemoApp.DemoWindows;

namespace DemoApp;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

        try
        {
            var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
            using var disposables = new DisposableManager();

            windowSystem.StatusBarStateService.TopStatus = "SharpConsoleUI Demo | Ctrl+P: Command Palette | Ctrl+T: Theme Selector";

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
