// -----------------------------------------------------------------------
// cxtop - ntop/btop-inspired live dashboard
// Demonstrates full-screen window with Spectre renderables and SharpConsoleUI controls
// Modernized with AgentStudio aesthetics and simplified UX
// -----------------------------------------------------------------------

using cxtop.Configuration;
using cxtop.Dashboard;
using cxtop.Stats;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;

namespace cxtop;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var config = ConsoleTopConfig.Default;
            var stats = SystemStatsFactory.Create();

            var windowSystem = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer),
                options: new ConsoleWindowSystemOptions(
                    ShowTopPanel: false,
                    ShowBottomPanel: false));

            windowSystem.PanelStateService.TopStatus =
                $"ConsoleTop - System Monitor ({SystemStatsFactory.GetPlatformName()})";

            Console.CancelKeyPress += (sender, e) =>
            {
                windowSystem.LogService.LogInfo("Ctrl+C received, shutting down...");
                e.Cancel = true;
                windowSystem.Shutdown(0);
            };

            var dashboard = new DashboardWindow(windowSystem, stats, config);
            dashboard.Create();

            windowSystem.LogService.LogInfo("Starting cxtop");
            await Task.Run(() => windowSystem.Run());
            windowSystem.LogService.LogInfo("cxtop stopped");
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
