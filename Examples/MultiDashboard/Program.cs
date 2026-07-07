using MultiDashboard.Windows;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Panel;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Themes;

namespace MultiDashboard;

class Program
{
    private static ConsoleWindowSystem? _windowSystem;

    // Window names — the keys the framework's WindowStateService uses to track each window. We do NOT
    // hold windows in static fields; the window state manager is the single source of truth for which
    // windows are open, so there are no stale references to go wrong when the user closes a window.
    private const string NameControlCenter = "controlcenter";
    private const string NameMetrics = "metrics";
    private const string NameMarkets = "markets";
    private const string NameLogStream = "logstream";

    static async Task<int> Main(string[] args)
    {
        try
        {
            // 1. Initialize the window system with framing top/bottom status bars that showcase the
            // built-in panel elements: a Start menu + a TaskBar window pager (bottom) and a live Clock
            // (top-right).
            _windowSystem = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer),
                options: new ConsoleWindowSystemOptions(
                    TopPanelConfig: panel => panel
                        .Left(Elements.StatusText("[bold]Ocean Control Center[/]"))
                        .Left(Elements.Separator())
                        .Left(Elements.StatusText("[dim]F1: Control Center  ·  Ctrl+Space: Start[/]"))
                        .Right(Elements.Clock().WithFormat("HH:mm:ss")),
                    BottomPanelConfig: panel => panel
                        .Left(Elements.StartMenu()
                            .WithName("startmenu")
                            .WithText("☰ Start")
                            .WithShortcutKey(ConsoleKey.Spacebar, ConsoleModifiers.Control)
                            .WithOptions(new StartMenuOptions
                            {
                                AppName = "Multi-Dashboard",
                                SidebarStyle = StartMenuSidebarStyle.IconLabel,
                                ShowWindowList = true
                            }))
                        .Center(Elements.TaskBar())
                        .Right(Elements.StatusText("[dim]Ctrl+Q quit[/]"))
                )
            );

            // 2. Ocean palette theme — a deep-ocean look derived from two seed colors.
            _windowSystem.ThemeRegistryService.RegisterTheme(
                "MultiDashboardOcean",
                "Deep ocean palette for the dashboard showcase",
                () => Theme.FromPalette(new Palette
                {
                    Primary = Color.FromHex("#2DD4BF"),      // cyan-teal accent
                    Background = Color.FromHex("#0B1F2A")     // deep ocean surface
                }));
            _windowSystem.ThemeStateService.SwitchTheme("MultiDashboardOcean");

            // 3. Ocean-Dots desktop background — vertical deep-blue gradient with a dot pattern.
            _windowSystem.DesktopBackground = new DesktopBackgroundConfig
            {
                Gradient = new GradientBackground(
                    ColorGradient.FromColors(new Color(30, 90, 160), new Color(5, 15, 35)),
                    GradientDirection.Vertical),
                Pattern = DesktopPatterns.Dots
            };

            // 4. Graceful shutdown on Ctrl+C.
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _windowSystem?.Shutdown(0);
            };

            // 5. Auto-open all dashboard windows so the multi-window desktop is populated immediately
            // (this is what makes it a *Multi*-Dashboard). Each is a normal, movable window on the Ocean
            // desktop; the Control Center opens last and stays active/on top.
            ToggleMetrics();
            ToggleMarkets();
            ToggleLogStream();
            OpenControlCenter();

            // 6. Register app-shell wiring: a global F1 shortcut and Start-menu actions that open/focus
            // each window — so every window is closable yet always reopenable. Showcases the global
            // shortcut system + the Start menu as an app launcher.
            _windowSystem.RegisterGlobalShortcut(ConsoleModifiers.None, ConsoleKey.F1, OpenControlCenter);

            var startMenu = _windowSystem.BottomPanel?.FindElement<StartMenuElement>("startmenu");
            if (startMenu != null)
            {
                startMenu.RegisterAction("Control Center", OpenControlCenter, category: "Dashboards", order: 0);
                startMenu.RegisterAction("Metrics", ToggleMetrics, category: "Dashboards", order: 1);
                startMenu.RegisterAction("Markets", ToggleMarkets, category: "Dashboards", order: 2);
                startMenu.RegisterAction("Log Stream", ToggleLogStream, category: "Dashboards", order: 3);
            }

            // 7. Run the application.
            await Task.Run(() => _windowSystem.Run());

            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            ExceptionFormatter.WriteException(ex);
            return 1;
        }
    }

    /// <summary>
    /// Opens (creates) a window if the window state manager has none by that name, otherwise closes the
    /// open one — a toggle keyed off the manager, not a cached reference. The factory builds the wrapper,
    /// whose <c>Show()</c> registers the window; the manager then owns its lifetime.
    /// </summary>
    private static void ToggleByName(string name, Func<IDashboardWindow> factory)
    {
        try
        {
            var existing = _windowSystem!.WindowStateService.FindWindowByName(name);
            if (existing != null)
                _windowSystem.CloseWindow(existing);
            else
                factory().Show();
        }
        catch (Exception ex)
        {
            _windowSystem?.LogService.LogError($"Error toggling window '{name}'", ex, "Program");
        }
    }

    /// <summary>
    /// Opens the Control Center if the manager has none, or focuses the open one (focus-or-recreate).
    /// Bound to the global F1 shortcut and the Start-menu action, so the hub is always reachable even
    /// after the user closes it. No cached reference — liveness comes from the window state manager.
    /// </summary>
    public static void OpenControlCenter()
    {
        var existing = _windowSystem!.WindowStateService.FindWindowByName(NameControlCenter);
        if (existing != null)
        {
            _windowSystem.SetActiveWindow(existing);
            return;
        }

        var cc = new ControlCenterWindow(_windowSystem);
        cc.Show();
        if (cc.Window != null)
            _windowSystem.SetActiveWindow(cc.Window);
    }

    /// <summary>Toggles the real Metrics dashboard window (live Sparkline / BarGraph / LineGraph).</summary>
    public static void ToggleMetrics() =>
        ToggleByName(NameMetrics, () => new MetricsWindow(_windowSystem!));

    /// <summary>Toggles the real Markets dashboard window (multi-series LineGraph + live ITableDataSource grid).</summary>
    public static void ToggleMarkets() =>
        ToggleByName(NameMarkets, () => new MarketsWindow(_windowSystem!));

    /// <summary>Toggles the live Log Stream dashboard window.</summary>
    public static void ToggleLogStream() =>
        ToggleByName(NameLogStream, () => new LogStreamWindow(_windowSystem!));
}
