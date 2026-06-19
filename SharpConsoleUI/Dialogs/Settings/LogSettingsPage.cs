// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Logging;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class LogSettingsPage
{
	public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
	{
		var theme = windowSystem.Theme;
		var ruleColor = DialogColors.Rule(theme);
		var logService = windowSystem.LogService;

		panel.AddControl(Ctl.Markup()
			.AddLine($"[bold {DialogColors.Section(theme, DialogSection.Logging).ToMarkup()}]Logging[/]")
			.AddEmptyLine()
			.Build());

		panel.AddControl(Ctl.RuleBuilder()
			.WithTitle("Log Level")
			.WithColor(ruleColor)
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
			.WithColor(ruleColor)
			.Build());

		var fileLoggingEnabled = logService.IsFileLoggingEnabled;
		panel.AddControl(Ctl.Markup()
			.AddLine($"[bold]File Logging:[/] {(fileLoggingEnabled ? "[green]Enabled[/]" : "[dim]Disabled[/]")}")
			.AddLine("[dim]Set via SHARPCONSOLEUI_DEBUG_LOG environment variable[/]")
			.AddEmptyLine()
			.Build());

		panel.AddControl(Ctl.RuleBuilder()
			.WithTitle("Buffer")
			.WithColor(ruleColor)
			.Build());

		panel.AddControl(Ctl.Markup()
			.AddLine($"[bold]Buffer Size:[/] {logService.MaxBufferSize}")
			.AddLine($"[bold]Entries:[/] {logService.Count}")
			.Build());
	}
}
