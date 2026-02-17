using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Ctl = SharpConsoleUI.Builders.Controls;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides performance configuration dialog functionality.
/// </summary>
public static class PerformanceDialog
{
	/// <summary>
	/// Shows a dialog for configuring performance settings.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	public static void Show(ConsoleWindowSystem windowSystem, Window? parentWindow = null)
	{
		var theme = windowSystem.Theme;

		// Create modal window
		var builder = new WindowBuilder(windowSystem)
			.WithTitle("Performance Settings")
			.Centered()
			.WithSize(70, 18)
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
			.AddLine("[cyan1 bold]Performance Configuration[/]")
			.AddLine("[grey50]Configure rendering and performance settings[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0)
			.Build());

		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.Build());

		// Options list
		var optionsList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithColors(theme.WindowForegroundColor, theme.ModalBackgroundColor)
			.WithFocusedColors(theme.WindowForegroundColor, theme.ModalBackgroundColor)
			.WithHighlightColors(Color.White, Color.Grey35)
			.SimpleMode()
			.WithDoubleClickActivation(true)
			.Build();

		// Build option items
		var metricsEnabled = windowSystem.Performance.IsPerformanceMetricsEnabled;
		var frameLimitingEnabled = windowSystem.Performance.IsFrameRateLimitingEnabled;
		var targetFPS = windowSystem.Performance.TargetFPS;

		optionsList.AddItem(new ListItem(
			$"Performance Metrics Display: {(metricsEnabled ? "[green]Enabled[/]" : "[red]Disabled[/]")}")
		{ Tag = "toggle-metrics" });

		optionsList.AddItem(new ListItem(
			$"Frame Rate Limiting: {(frameLimitingEnabled ? "[green]Enabled[/]" : "[red]Disabled[/]")}")
		{ Tag = "toggle-limiting" });

		optionsList.AddItem(new ListItem($"Target FPS: [yellow]{targetFPS}[/]")
		{ Tag = "set-fps" });

		modal.AddControl(optionsList);

		// Bottom separator
		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.StickyBottom()
			.Build());

		// Footer
		modal.AddControl(Ctl.Markup()
			.AddLine("[grey70]Enter/Double-click: Toggle/Set  •  Escape: Close[/]")
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
				case "toggle-metrics":
					windowSystem.Performance.SetPerformanceMetrics(!windowSystem.Performance.IsPerformanceMetricsEnabled);
					break;

				case "toggle-limiting":
					windowSystem.Performance.SetFrameRateLimiting(!windowSystem.Performance.IsFrameRateLimitingEnabled);
					break;

				case "set-fps":
					ShowFPSDialog(windowSystem, modal);
					return;
			}

			// Refresh the list to show updated values
			modal.Close();
			Show(windowSystem, parentWindow);
		}

		// Handle double-click
		optionsList.ItemActivated += (sender, item) => HandleSelection(item);

		// Handle Enter and Escape keys
		modal.KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Enter)
			{
				HandleSelection(optionsList.SelectedItem);
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
		optionsList.SetFocus(true, FocusReason.Programmatic);
	}

	private static void ShowFPSDialog(ConsoleWindowSystem windowSystem, Window parentWindow)
	{
		var theme = windowSystem.Theme;

		// Create FPS selection modal
		var builder = new WindowBuilder(windowSystem)
			.WithTitle("Set Target FPS")
			.Centered()
			.WithSize(50, 15)
			.AsModal()
			.WithParent(parentWindow)
			.Resizable(false)
			.Minimizable(false)
			.Maximizable(false)
			.Movable(true)
			.WithColors(theme.WindowForegroundColor, theme.ModalBackgroundColor);

		var fpsModal = builder.Build();

		// Header
		fpsModal.AddControl(Ctl.Markup()
			.AddLine("[cyan1 bold]Select Target FPS[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(1, 0, 1, 0)
			.Build());

		fpsModal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.Build());

		// FPS options
		var fpsList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithColors(theme.WindowForegroundColor, theme.ModalBackgroundColor)
			.WithFocusedColors(theme.WindowForegroundColor, theme.ModalBackgroundColor)
			.WithHighlightColors(Color.White, Color.Grey35)
			.SimpleMode()
			.WithDoubleClickActivation(true)
			.Build();

		var fpsOptions = new[] { 30, 60, 120, 144 };
		var currentFPS = windowSystem.Performance.TargetFPS;

		foreach (var fps in fpsOptions)
		{
			var label = fps == currentFPS
				? $"{fps} FPS [cyan1](current)[/]"
				: $"{fps} FPS";
			fpsList.AddItem(new ListItem(label) { Tag = fps });
		}

		// Set initial selection to current FPS
		var currentIdx = Array.IndexOf(fpsOptions, currentFPS);
		if (currentIdx >= 0)
			fpsList.SelectedIndex = currentIdx;

		fpsModal.AddControl(fpsList);

		// Bottom separator
		fpsModal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.StickyBottom()
			.Build());

		// Footer
		fpsModal.AddControl(Ctl.Markup()
			.AddLine("[grey70]Enter/Double-click: Select  •  Escape: Cancel[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.StickyBottom()
			.Build());

		// Handle selection
		void SelectFPS(ListItem? item)
		{
			if (item?.Tag is int fps)
			{
				windowSystem.Performance.SetTargetFPS(fps);
				fpsModal.Close();
				parentWindow.Close();
				Show(windowSystem);
			}
		}

		fpsList.ItemActivated += (sender, item) => SelectFPS(item);

		fpsModal.KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Enter)
			{
				SelectFPS(fpsList.SelectedItem);
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				fpsModal.Close();
				e.Handled = true;
			}
		};

		// Add modal to window system and activate it
		windowSystem.AddWindow(fpsModal);
		windowSystem.SetActiveWindow(fpsModal);
		fpsList.SetFocus(true, FocusReason.Programmatic);
	}
}
