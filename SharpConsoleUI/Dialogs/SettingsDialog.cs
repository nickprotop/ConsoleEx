using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides a unified settings dialog with tabs for Appearance, Performance, and About.
/// </summary>
public static class SettingsDialog
{
	/// <summary>
	/// Shows a tabbed settings dialog for configuring application preferences.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	public static void Show(ConsoleWindowSystem windowSystem, Window? parentWindow = null)
	{
		var theme = windowSystem.Theme;

		var builder = new WindowBuilder(windowSystem)
			.WithTitle("Settings")
			.Centered()
			.WithSize(75, 24)
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

		// Tabbed content
		var tabs = Ctl.TabControl()
			.AddTab("Appearance", BuildAppearanceTab(windowSystem))
			.AddTab("Performance", BuildPerformanceTab(windowSystem))
			.AddTab("About", BuildAboutTab(windowSystem))
			.Fill()
			.Build();

		modal.AddControl(tabs);

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

	private static IWindowControl BuildAppearanceTab(ConsoleWindowSystem windowSystem)
	{
		var themes = ThemeRegistry.GetAvailableThemes();
		var currentThemeName = windowSystem.Theme.Name;

		var themeList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithDoubleClickActivation(true)
			.Build();

		foreach (var themeInfo in themes)
		{
			var isCurrent = themeInfo.Name == currentThemeName;
			var label = isCurrent
				? $"[white bold]{themeInfo.Name}[/] [cyan1](current)[/] [grey50]{themeInfo.Description}[/]"
				: $"[white]{themeInfo.Name}[/] [grey50]{themeInfo.Description}[/]";

			themeList.AddItem(new ListItem(label) { Tag = themeInfo.Name });
		}

		var currentIdx = themes.ToList().FindIndex(t => t.Name == currentThemeName);
		if (currentIdx >= 0)
			themeList.SelectedIndex = currentIdx;

		// Apply theme on Enter or double-click
		themeList.ItemActivated += (sender, item) =>
		{
			if (item?.Tag is string themeName)
				windowSystem.ThemeStateService.SwitchTheme(themeName);
		};

		var panel = Ctl.ScrollablePanel()
			.AddControl(Ctl.Markup()
				.AddLine("[grey70]Select a theme to apply[/]")
				.WithAlignment(HorizontalAlignment.Left)
				.WithMargin(1, 0, 1, 0)
				.Build())
			.AddControl(themeList)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		return panel;
	}

	private static IWindowControl BuildPerformanceTab(ConsoleWindowSystem windowSystem)
	{
		var perf = windowSystem.Performance;

		var metricsCheckbox = Ctl.Checkbox("Show Performance Metrics")
			.Checked(perf.IsPerformanceMetricsEnabled)
			.WithMargin(1, 0, 1, 0)
			.OnCheckedChanged((sender, isChecked) =>
			{
				perf.SetPerformanceMetrics(isChecked);
			})
			.Build();

		var frameLimitCheckbox = Ctl.Checkbox("Enable Frame Rate Limiting")
			.Checked(perf.IsFrameRateLimitingEnabled)
			.WithMargin(1, 0, 1, 0)
			.OnCheckedChanged((sender, isChecked) =>
			{
				perf.SetFrameRateLimiting(isChecked);
			})
			.Build();

		// Build FPS dropdown
		var fpsOptions = new[] { 30, 60, 120, 144 };
		var currentFPS = perf.TargetFPS;
		var currentFPSIdx = Array.IndexOf(fpsOptions, currentFPS);
		if (currentFPSIdx < 0) currentFPSIdx = 1; // default to 60

		var fpsDropdown = Ctl.Dropdown("Target FPS")
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
			.Build();

		var panel = Ctl.ScrollablePanel()
			.AddControl(metricsCheckbox)
			.AddControl(frameLimitCheckbox)
			.AddControl(Ctl.RuleBuilder()
				.WithColor(Color.Grey23)
				.Build())
			.AddControl(Ctl.Markup()
				.AddLine("[grey70]Target FPS[/]")
				.WithAlignment(HorizontalAlignment.Left)
				.WithMargin(1, 0, 1, 0)
				.Build())
			.AddControl(fpsDropdown)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		return panel;
	}

	private static IWindowControl BuildAboutTab(ConsoleWindowSystem windowSystem)
	{
		var pluginState = windowSystem.PluginStateService.CurrentState;

		var markupBuilder = Ctl.Markup()
			.AddLine("[cyan1 bold]SharpConsoleUI - Console Window System[/]")
			.AddLine("[grey50]Version 2.0.0[/]")
			.AddLine("")
			.AddLine("[white bold]Description:[/]")
			.AddLine("[grey70]A modern .NET console windowing system with dependency injection,[/]")
			.AddLine("[grey70]fluent builders, async/await patterns, and plugin architecture.[/]")
			.AddLine("")
			.AddLine("[white bold]Author:[/] [cyan1]Nikolaos Protopapas[/]")
			.AddLine("[white bold]Email:[/] [grey70]nikolaos.protopapas@gmail.com[/]")
			.AddLine("[white bold]License:[/] [green]MIT[/]")
			.AddLine("")
			.AddLine("[white bold]Core Features:[/]")
			.AddLine("[grey70]• Double-buffered rendering with dirty region tracking[/]")
			.AddLine("[grey70]• Modern async/await and fluent builder patterns[/]")
			.AddLine("[grey70]• Rich control library with Spectre.Console integration[/]")
			.AddLine("[grey70]• Plugin system with reflection-free patterns[/]")
			.AddLine("[grey70]• Configurable frame rate limiting and performance metrics[/]")
			.AddLine("")
			.AddLine($"[white bold]Loaded Plugins:[/] [yellow]{pluginState.LoadedPluginCount}[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0);

		if (pluginState.LoadedPluginCount > 0)
		{
			markupBuilder.AddLine("");
			foreach (var name in pluginState.PluginNames)
				markupBuilder.AddLine($"[grey70]  • {name}[/]");
		}

		var panel = Ctl.ScrollablePanel()
			.AddControl(markupBuilder.Build())
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		return panel;
	}
}
