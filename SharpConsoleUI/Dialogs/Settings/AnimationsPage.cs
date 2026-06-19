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
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs.Settings;

internal static class AnimationsPage
{
	public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
	{
		var theme = windowSystem.Theme;
		var ruleColor = DialogColors.Rule(theme);
		var animationCount = windowSystem.Animations.ActiveCount;

		panel.AddControl(Ctl.Markup()
			.AddLine($"[bold {DialogColors.Section(theme, DialogSection.Performance).ToMarkup()}]Animations[/]")
			.AddEmptyLine()
			.Build());

		panel.AddControl(Ctl.RuleBuilder()
			.WithTitle("Settings")
			.WithColor(ruleColor)
			.Build());

		panel.AddControl(Ctl.Checkbox("Enable animations")
			.Checked(windowSystem.Animations.IsEnabled)
			.OnCheckedChanged((sender, isChecked) =>
			{
				windowSystem.Animations.IsEnabled = isChecked;
			})
			.WithMargin(0, 1, 0, 0)
			.Build());

		panel.AddControl(Ctl.RuleBuilder()
			.WithTitle("Status")
			.WithColor(ruleColor)
			.Build());

		panel.AddControl(Ctl.Markup()
			.AddLine($"[bold]Active Animations:[/] {animationCount}")
			.AddEmptyLine()
			.AddLine("[dim]Animations include window transitions,[/]")
			.AddLine("[dim]navigation pane resizing, and control effects.[/]")
			.Build());
	}
}
