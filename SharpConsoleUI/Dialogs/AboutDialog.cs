using SharpConsoleUI.Builders;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Ctl = SharpConsoleUI.Builders.Controls;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides about dialog functionality.
/// </summary>
public static class AboutDialog
{
	/// <summary>
	/// Shows a dialog with application information.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	public static void Show(ConsoleWindowSystem windowSystem, Window? parentWindow = null)
	{
		var theme = windowSystem.Theme;
		var pluginState = windowSystem.PluginStateService.CurrentState;

		// Create modal window
		var builder = new WindowBuilder(windowSystem)
			.WithTitle("About SharpConsoleUI")
			.Centered()
			.WithSize(70, 25)
			.AsModal()
			.Resizable(false)
			.Minimizable(false)
			.Maximizable(false)
			.Movable(true)
			.WithColors(theme.ModalBackgroundColor, theme.WindowForegroundColor);

		if (parentWindow != null)
			builder.WithParent(parentWindow);

		var modal = builder.Build();

		// Header
		modal.AddControl(Ctl.Markup()
			.AddLine("[cyan1 bold]SharpConsoleUI - Console Window System[/]")
			.AddLine("[grey50]Version 2.0.0[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(1, 0, 1, 0)
			.Build());

		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.Build());

		// Application information
		modal.AddControl(Ctl.Markup()
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
			.WithMargin(2, 0, 1, 0)
			.Build());

		// Display loaded plugin names if any
		if (pluginState.LoadedPluginCount > 0)
		{
			var pluginLines = pluginState.PluginNames.Select(name => $"[grey70]  • {name}[/]").ToArray();
			modal.AddControl(Ctl.Markup()
				.AddLines(pluginLines)
				.WithAlignment(HorizontalAlignment.Left)
				.WithMargin(2, 0, 1, 0)
				.Build());
		}

		// Bottom separator
		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(Color.Grey23)
			.StickyBottom()
			.Build());

		// Footer
		modal.AddControl(Ctl.Markup()
			.AddLine("[grey70]Press Escape or Enter to close[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.StickyBottom()
			.Build());

		// Handle Escape and Enter keys
		modal.KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Escape || e.KeyInfo.Key == ConsoleKey.Enter)
			{
				modal.Close();
				e.Handled = true;
			}
		};

		// Add modal to window system and activate it
		windowSystem.AddWindow(modal);
		windowSystem.SetActiveWindow(modal);
	}
}
