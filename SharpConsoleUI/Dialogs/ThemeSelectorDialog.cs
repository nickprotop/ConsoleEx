// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Themes;
using Spectre.Console;
using Ctl = SharpConsoleUI.Builders.Controls;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

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
		var themes = ThemeRegistry.GetAvailableThemes();
		var currentThemeName = windowSystem.Theme.Name;
		var logService = windowSystem.LogService;
		var theme = windowSystem.Theme;

		// Log available themes
		logService?.Log(LogLevel.Information, "Theme",
			$"Available themes ({themes.Count}): {string.Join(", ", themes.Select(t => t.Name))}");
		logService?.Log(LogLevel.Information, "Theme",
			$"Current theme: {currentThemeName}");

		// Create modal window using WindowBuilder
		var builder = new WindowBuilder(windowSystem)
			.WithTitle("Theme Selector")
			.Centered()
			.WithSize(65, 20)
			.AsModal()
			.Resizable(true)
			.Minimizable(false)
			.Maximizable(true)
			.Movable(true)
			.WithColors(theme.ModalBackgroundColor, theme.WindowForegroundColor);

		if (parentWindow != null)
			builder.WithParent(parentWindow);

		var modal = builder.Build();

		// Header with title and instructions
		modal.AddControl(Ctl.Markup()
			.AddLine("[cyan1 bold]Available Themes[/]")
			.AddLine("[grey50]Select a theme to apply, or press Escape to cancel[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0)
			.Build());

		// Separator
		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.Build());

		// Theme list
		var themeList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithColors(theme.ModalBackgroundColor, theme.WindowForegroundColor)
			.WithFocusedColors(theme.ModalBackgroundColor, theme.WindowForegroundColor)
			.WithHighlightColors(Color.Grey35, Color.White)
			.SimpleMode()
			.WithDoubleClickActivation(true)
			.Build();

		// Populate theme list
		foreach (var themeInfo in themes)
		{
			var isCurrentTheme = themeInfo.Name == currentThemeName;
			var label = isCurrentTheme
				? $"[white bold]{themeInfo.Name}[/] [cyan1](current)[/] [grey50]{themeInfo.Description}[/]"
				: $"[white]{themeInfo.Name}[/] [grey50]{themeInfo.Description}[/]";

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
			.WithColor(Color.Grey23)
			.StickyBottom()
			.Build());

		// Footer with instructions
		modal.AddControl(Ctl.Markup()
			.AddLine("[grey70]Enter/Double-click: Apply Theme  •  Escape: Cancel[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.StickyBottom()
			.Build());

		// Handle double-click activation
		themeList.ItemActivated += (sender, item) =>
		{
			if (item?.Tag is string themeName)
			{
				logService?.Log(LogLevel.Information, "Theme",
					$"Theme selected via double-click: {themeName}");
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
					logService?.Log(LogLevel.Information, "Theme",
						$"Theme selected via Enter: {themeName}");
					windowSystem.ThemeStateService.SwitchTheme(themeName);
					modal.Close();
				}
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				// Cancel with Escape key
				logService?.Log(LogLevel.Debug, "Theme",
					"Theme selector cancelled");
				modal.Close();
				e.Handled = true;
			}
		};

		// Add modal to window system and activate it
		windowSystem.AddWindow(modal);
		windowSystem.SetActiveWindow(modal);

		// Focus the list after modal is active
		themeList.SetFocus(true, FocusReason.Programmatic);
	}
}
