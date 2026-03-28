using System.Runtime.InteropServices;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class InfoPage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
    {
        var libVersion = typeof(ConsoleWindowSystem).Assembly.GetName().Version;
        var versionStr = libVersion != null
            ? $"{libVersion.Major}.{libVersion.Minor}.{libVersion.Build}"
            : "0.0.1";

        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(171,157,242)]System Information[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Version")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddLine($"[bold]SharpConsoleUI:[/] {versionStr}")
            .AddLine($"[bold].NET:[/] {RuntimeInformation.FrameworkDescription}")
            .AddLine($"[bold]OS:[/] {RuntimeInformation.OSDescription}")
            .AddLine($"[bold]Architecture:[/] {RuntimeInformation.OSArchitecture}")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Console")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var driver = windowSystem.ConsoleDriver;
        var screenSize = driver.ScreenSize;
        panel.AddControl(Ctl.Markup()
            .AddLine($"[bold]Terminal Size:[/] {screenSize.Width}x{screenSize.Height}")
            .AddLine($"[bold]Driver:[/] {driver.GetType().Name}")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Windows")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var windowCount = windowSystem.Windows.Count;
        var activeWindow = windowSystem.WindowStateService.ActiveWindow?.Title ?? "None";
        var modalCount = windowSystem.ModalStateService.HasModals ? "Yes" : "No";

        panel.AddControl(Ctl.Markup()
            .AddLine($"[bold]Total Windows:[/] {windowCount}")
            .AddLine($"[bold]Active Window:[/] {activeWindow}")
            .AddLine($"[bold]Modals Active:[/] {modalCount}")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Plugins")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var pluginState = windowSystem.PluginStateService.CurrentState;
        var markupBuilder = Ctl.Markup()
            .AddLine($"[bold]Loaded:[/] {pluginState.LoadedPluginCount}");

        if (pluginState.LoadedPluginCount > 0)
        {
            foreach (var name in pluginState.PluginNames)
                markupBuilder.AddLine($"  [dim]• {name}[/]");
        }
        markupBuilder.AddEmptyLine();
        panel.AddControl(markupBuilder.Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Performance")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var perf = windowSystem.Performance;
        panel.AddControl(Ctl.Markup()
            .AddLine($"[bold]Current FPS:[/] {perf.CurrentFPS:F1}")
            .AddLine($"[bold]Frame Time:[/] {perf.CurrentFrameTimeMs:F1}ms")
            .AddLine($"[bold]Target FPS:[/] {perf.TargetFPS}")
            .AddLine($"[bold]Frame Limiting:[/] {(perf.IsFrameRateLimitingEnabled ? "On" : "Off")}")
            .Build());
    }
}
