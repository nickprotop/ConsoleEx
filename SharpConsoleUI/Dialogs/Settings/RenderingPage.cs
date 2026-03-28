using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class RenderingPage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
    {
        var perf = windowSystem.Performance;
        var driverType = windowSystem.ConsoleDriver.GetType().Name;

        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(252,152,103)]Rendering[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Display Driver")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddLine($"[bold]Driver:[/] {driverType}")
            .AddLine("[bold]Render Mode:[/] Buffer")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Performance")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Ctl.Checkbox("Show performance metrics")
            .Checked(perf.IsPerformanceMetricsEnabled)
            .OnCheckedChanged((sender, isChecked) =>
            {
                perf.SetPerformanceMetrics(isChecked);
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.Checkbox("Enable frame rate limiting")
            .Checked(perf.IsFrameRateLimitingEnabled)
            .OnCheckedChanged((sender, isChecked) =>
            {
                perf.SetFrameRateLimiting(isChecked);
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Frame Rate")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var fpsOptions = new[] { 30, 60, 120, 144 };
        var currentFPSIdx = Array.IndexOf(fpsOptions, perf.TargetFPS);
        if (currentFPSIdx < 0) currentFPSIdx = 1;

        panel.AddControl(Ctl.Dropdown("Target FPS")
            .AddItem("30 FPS", "30")
            .AddItem("60 FPS", "60")
            .AddItem("120 FPS", "120")
            .AddItem("144 FPS", "144")
            .SelectedIndex(currentFPSIdx)
            .OnSelectedValueChanged((sender, value) =>
            {
                if (value != null && int.TryParse(value, out var fps))
                    perf.SetTargetFPS(fps);
            })
            .WithMargin(0, 1, 0, 0)
            .Build());
    }
}
