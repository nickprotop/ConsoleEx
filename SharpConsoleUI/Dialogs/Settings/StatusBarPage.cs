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

internal static class StatusBarPage
{
	public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
	{
		var theme = windowSystem.Theme;
		var ruleColor = DialogColors.Rule(theme);
		var panelService = windowSystem.PanelStateService;

		panel.AddControl(Ctl.Markup()
			.AddLine($"[bold {DialogColors.Section(theme, DialogSection.Appearance).ToMarkup()}]Panels[/]")
			.AddEmptyLine()
			.Build());

		panel.AddControl(Ctl.RuleBuilder()
			.WithTitle("Visibility")
			.WithColor(ruleColor)
			.Build());

		// Read initial state from panel service
		bool topVisible = panelService.TopPanel?.Visible ?? panelService.ShowTopPanel;
		bool bottomVisible = panelService.BottomPanel?.Visible ?? panelService.ShowBottomPanel;

		panel.AddControl(Ctl.Checkbox("Show top panel")
			.Checked(topVisible)
			.OnCheckedChanged((sender, isChecked) =>
			{
				panelService.ShowTopPanel = isChecked;
			})
			.WithMargin(0, 1, 0, 0)
			.Build());

		panel.AddControl(Ctl.Checkbox("Show bottom panel")
			.Checked(bottomVisible)
			.OnCheckedChanged((sender, isChecked) =>
			{
				panelService.ShowBottomPanel = isChecked;
			})
			.WithMargin(0, 1, 0, 0)
			.Build());
	}
}
