// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Ctl = SharpConsoleUI.Builders.Controls;
namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides theme selector dialog functionality.
/// </summary>
public static class ThemeSelectorDialog
{
	/// <summary>
	/// Shows a dialog for selecting and applying a theme.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	public static void Show(ConsoleWindowSystem windowSystem, Window? parentWindow = null)
	{
		var themes = windowSystem.ThemeRegistryService.GetAvailableThemes();
		var currentThemeName = windowSystem.Theme.Name;
		var logService = windowSystem.LogService;

		// Theme-derived markup colors so the dialog reads on any theme (a hardcoded [white]/[cyan1] would
		// be invisible on a light theme). Accent for highlights, window foreground for body, a dimmed
		// foreground for secondary text, and DialogColors.Rule for the separators.
		var theme = windowSystem.Theme;
		string accent = DialogColors.Accent(theme).ToMarkup();
		string fg = theme.WindowForegroundColor.ToMarkup();
		string dim = theme.WindowForegroundColor.Mix(theme.WindowBackgroundColor, 0.45).ToMarkup();
		var ruleColor = DialogColors.Rule(theme);

		// Log available themes
		logService?.LogInfo(
			$"Available themes ({themes.Count}): {string.Join(", ", themes.Select(t => t.Name))}", "Theme");
		logService?.LogInfo($"Current theme: {currentThemeName}", "Theme");

		// Create modal window using WindowBuilder (no explicit colors - use theme defaults)
		var builder = new WindowBuilder(windowSystem)
			.WithTitle("Theme Selector")
			.Centered()
			.WithSize(65, 20)
			.AsModal()
			.Resizable(true)
			.Minimizable(false)
			.Maximizable(true)
			.Movable(true);

		if (parentWindow != null)
			builder.WithParent(parentWindow);

		var modal = builder.Build();

		// Header with title and instructions
		modal.AddControl(Ctl.Markup()
			.AddLine($"[{accent} bold]Available Themes[/]")
			.AddLine($"[{dim}]Select a theme to apply, or press Escape to cancel[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0)
			.Build());

		// Separator
		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(ruleColor)
			.Build());

		// Theme list (no explicit colors - inherit from theme)
		var themeList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithDoubleClickActivation(true)
			.Build();

		// Populate theme list
		foreach (var themeInfo in themes)
		{
			var isCurrentTheme = themeInfo.Name == currentThemeName;
			var label = isCurrentTheme
				? $"[{fg} bold]{themeInfo.Name}[/] [{accent}](current)[/] [{dim}]{themeInfo.Description}[/]"
				: $"[{fg}]{themeInfo.Name}[/] [{dim}]{themeInfo.Description}[/]";

			themeList.AddItem(new ListItem(label) { Tag = themeInfo.Name });
		}

		// Set initial selection to current theme
		var currentIdx = themes.ToList().FindIndex(t => t.Name == currentThemeName);
		if (currentIdx >= 0)
		{
			themeList.SelectedIndex = currentIdx;
		}

		modal.AddControl(themeList);

		// Bottom separator
		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(ruleColor)
			.StickyBottom()
			.Build());

		// Footer with instructions
		modal.AddControl(Ctl.Markup()
			.AddLine($"[{dim}]Enter/Double-click: Apply Theme  •  Escape: Cancel[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.StickyBottom()
			.Build());

		// Handle double-click activation
		themeList.ItemActivated += (sender, item) =>
		{
			if (item?.Tag is string themeName)
			{
				logService?.LogInfo($"Theme selected via double-click: {themeName}", "Theme");
				windowSystem.ThemeStateService.SwitchTheme(themeName);
				modal.Close();
			}
		};

		// Handle Enter and Escape keys
		modal.KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Enter)
			{
				// Apply selected theme with Enter key
				var selectedItem = themeList.SelectedItem;
				if (selectedItem?.Tag is string themeName)
				{
					logService?.LogInfo($"Theme selected via Enter: {themeName}", "Theme");
					windowSystem.ThemeStateService.SwitchTheme(themeName);
					modal.Close();
				}
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				// Cancel with Escape key
				logService?.LogDebug("Theme selector cancelled", "Theme");
				modal.Close();
				e.Handled = true;
			}
		};

		// Add modal to window system and activate it
		windowSystem.AddWindow(modal);
		windowSystem.SetActiveWindow(modal);

		// Focus the list after modal is active
		themeList.GetParentWindow()?.FocusManager.SetFocus(themeList, FocusReason.Programmatic);
	}
}
