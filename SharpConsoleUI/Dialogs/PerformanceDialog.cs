using SharpConsoleUI.Builders;
using SharpConsoleUI.Layout;
using Ctl = SharpConsoleUI.Builders.Controls;

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
		var perf = windowSystem.Performance;

		var builder = new WindowBuilder(windowSystem)
			.WithTitle("Performance Settings")
			.Centered()
			.WithSize(60, 16)
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

		// Checkboxes
		modal.AddControl(Ctl.Checkbox("Show Performance Metrics")
			.Checked(perf.IsPerformanceMetricsEnabled)
			.WithMargin(1, 0, 1, 0)
			.OnCheckedChanged((sender, isChecked) =>
			{
				perf.SetPerformanceMetrics(isChecked);
			})
			.Build());

		modal.AddControl(Ctl.Checkbox("Enable Frame Rate Limiting")
			.Checked(perf.IsFrameRateLimitingEnabled)
			.WithMargin(1, 0, 1, 0)
			.OnCheckedChanged((sender, isChecked) =>
			{
				perf.SetFrameRateLimiting(isChecked);
			})
			.Build());

		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.Build());

		// FPS dropdown
		var fpsOptions = new[] { 30, 60, 120, 144 };
		var currentFPSIdx = Array.IndexOf(fpsOptions, perf.TargetFPS);
		if (currentFPSIdx < 0) currentFPSIdx = 1;

		modal.AddControl(Ctl.Markup()
			.AddLine("[grey70]Target FPS[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0)
			.Build());

		modal.AddControl(Ctl.Dropdown("Target FPS")
			.AddItem("30 FPS", "30")
			.AddItem("60 FPS", "60")
			.AddItem("120 FPS", "120")
			.AddItem("144 FPS", "144")
			.SelectedIndex(currentFPSIdx)
			.WithMargin(1, 0, 1, 0)
			.OnSelectedValueChanged((sender, value) =>
			{
				if (value != null && int.TryParse(value, out var fps))
					perf.SetTargetFPS(fps);
			})
			.Build());

		// Bottom separator
		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.StickyBottom()
			.Build());

		// Footer
		modal.AddControl(Ctl.Markup()
			.AddLine("[grey70]Escape: Close[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.StickyBottom()
			.Build());

		// Handle Escape key
		modal.KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				modal.Close();
				e.Handled = true;
			}
		};

		windowSystem.AddWindow(modal);
		windowSystem.SetActiveWindow(modal);
	}
}
