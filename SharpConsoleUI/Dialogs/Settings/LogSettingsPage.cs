using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Logging;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class LogSettingsPage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
    {
        var logService = windowSystem.LogService;

        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(255,97,136)]Logging[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Log Level")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var levels = new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
        var currentIdx = Array.IndexOf(levels, logService.MinimumLevel);
        if (currentIdx < 0) currentIdx = 3;

        panel.AddControl(Ctl.Dropdown("Minimum Level")
            .AddItem("Trace", "0")
            .AddItem("Debug", "1")
            .AddItem("Information", "2")
            .AddItem("Warning", "3")
            .AddItem("Error", "4")
            .AddItem("Critical", "5")
            .SelectedIndex(currentIdx)
            .OnSelectedValueChanged((sender, value) =>
            {
                if (value != null && int.TryParse(value, out var level))
                    logService.MinimumLevel = (LogLevel)level;
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("File Output")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var fileLoggingEnabled = logService.IsFileLoggingEnabled;
        panel.AddControl(Ctl.Markup()
            .AddLine($"[bold]File Logging:[/] {(fileLoggingEnabled ? "[green]Enabled[/]" : "[dim]Disabled[/]")}")
            .AddLine("[dim]Set via SHARPCONSOLEUI_DEBUG_LOG environment variable[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Buffer")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddLine($"[bold]Buffer Size:[/] {logService.MaxBufferSize}")
            .AddLine($"[bold]Entries:[/] {logService.Count}")
            .Build());
    }
}
