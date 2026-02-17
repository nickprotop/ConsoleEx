using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Ctl = SharpConsoleUI.Builders.Controls;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides settings configuration dialog functionality.
/// </summary>
public static class SettingsDialog
{
	/// <summary>
	/// Shows a dialog for configuring general settings.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	public static void Show(ConsoleWindowSystem windowSystem, Window? parentWindow = null)
	{
		var theme = windowSystem.Theme;

		// Create modal window
		var builder = new WindowBuilder(windowSystem)
			.WithTitle("Settings")
			.Centered()
			.WithSize(70, 16)
			.AsModal()
			.Resizable(false)
			.Minimizable(false)
			.Maximizable(false)
			.Movable(true)
			.WithColors(theme.WindowForegroundColor, theme.ModalBackgroundColor);

		if (parentWindow != null)
			builder.WithParent(parentWindow);

		var modal = builder.Build();

		// Header
		modal.AddControl(Ctl.Markup()
			.AddLine("[cyan1 bold]Application Settings[/]")
			.AddLine("[grey50]Configure application preferences[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0)
			.Build());

		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.Build());

		// Settings list
		var settingsList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithColors(theme.WindowForegroundColor, theme.ModalBackgroundColor)
			.WithFocusedColors(theme.WindowForegroundColor, theme.ModalBackgroundColor)
			.WithHighlightColors(Color.White, Color.Grey35)
			.SimpleMode()
			.WithDoubleClickActivation(true)
			.Build();

		// Add setting items
		settingsList.AddItem(new ListItem("Change Theme...") { Tag = "theme" });
		settingsList.AddItem(new ListItem("Performance Settings...") { Tag = "performance" });
		settingsList.AddItem(new ListItem("About...") { Tag = "about" });

		modal.AddControl(settingsList);

		// Bottom separator
		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.StickyBottom()
			.Build());

		// Footer
		modal.AddControl(Ctl.Markup()
			.AddLine("[grey70]Enter/Double-click: Open  •  Escape: Close[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.StickyBottom()
			.Build());

		// Handle selection
		void HandleSelection(ListItem? item)
		{
			if (item?.Tag is not string action)
				return;

			switch (action)
			{
				case "theme":
					modal.Close();
					ThemeSelectorDialog.Show(windowSystem, parentWindow);
					break;

				case "performance":
					modal.Close();
					PerformanceDialog.Show(windowSystem, parentWindow);
					break;

				case "about":
					modal.Close();
					AboutDialog.Show(windowSystem, parentWindow);
					break;
			}
		}

		// Handle double-click
		settingsList.ItemActivated += (sender, item) => HandleSelection(item);

		// Handle Enter and Escape keys
		modal.KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Enter)
			{
				HandleSelection(settingsList.SelectedItem);
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				modal.Close();
				e.Handled = true;
			}
		};

		// Add modal to window system and activate it
		windowSystem.AddWindow(modal);
		windowSystem.SetActiveWindow(modal);

		// Focus the list
		settingsList.SetFocus(true, FocusReason.Programmatic);
	}
}
