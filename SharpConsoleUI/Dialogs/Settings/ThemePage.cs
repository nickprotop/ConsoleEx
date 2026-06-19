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
using SharpConsoleUI.Themes;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class ThemePage
{
	public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
	{
		var theme = windowSystem.Theme;
		var ruleColor = DialogColors.Rule(theme);
		var currentTheme = windowSystem.ThemeStateService.CurrentTheme?.Name ?? "Unknown";

		panel.AddControl(Ctl.Markup()
			.AddLine($"[bold {DialogColors.Section(theme, DialogSection.Appearance).ToMarkup()}]Theme[/]")
			.AddEmptyLine()
			.Build());

		panel.AddControl(Ctl.RuleBuilder()
			.WithTitle("Current Theme")
			.WithColor(ruleColor)
			.Build());

		panel.AddControl(Ctl.Markup()
			.AddLine($"[bold]Active:[/] [cyan1]{currentTheme}[/]")
			.AddLine($"[bold]Available:[/] {windowSystem.ThemeRegistryService.Count}")
			.AddEmptyLine()
			.Build());

		panel.AddControl(Ctl.Button("Change Theme...")
			.OnClick((sender, btn, window) =>
			{
				ThemeSelectorDialog.Show(windowSystem, window);
			})
			.WithMargin(0, 1, 0, 0)
			.Build());
	}
}
