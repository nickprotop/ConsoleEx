// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Ctl = SharpConsoleUI.Builders.Controls;

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

		// Theme-derived markup colors so the text reads on any theme (hardcoded [white]/[grey] would be
		// invisible on a light theme): accent for highlights, window foreground for labels, a dimmed
		// foreground for body, and DialogColors.Rule for separators.
		string accent = DialogColors.Accent(theme).ToMarkup();
		string fg = theme.WindowForegroundColor.ToMarkup();
		string dim = theme.WindowForegroundColor.Mix(theme.WindowBackgroundColor, 0.40).ToMarkup();
		string success = (theme.SuccessColor ?? Color.Green).ToMarkup();
		string warn = (theme.WarningColor ?? Color.Orange1).ToMarkup();
		var ruleColor = DialogColors.Rule(theme);

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
			.WithColors(theme.WindowForegroundColor, theme.ModalBackgroundColor ?? theme.WindowBackgroundColor);

		if (parentWindow != null)
			builder.WithParent(parentWindow);

		var modal = builder.Build();

		// Header
		modal.AddControl(Ctl.Markup()
			.AddLine($"[{accent} bold]SharpConsoleUI - Console Window System[/]")
			.AddLine($"[{dim}]Version 2.0.0[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(1, 0, 1, 0)
			.Build());

		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(ruleColor)
			.Build());

		// Application information
		modal.AddControl(Ctl.Markup()
			.AddLine($"[{fg} bold]Description:[/]")
			.AddLine($"[{dim}]A modern .NET console windowing system with dependency injection,[/]")
			.AddLine($"[{dim}]fluent builders, async/await patterns, and plugin architecture.[/]")
			.AddLine("")
			.AddLine($"[{fg} bold]Author:[/] [{accent}]Nikolaos Protopapas[/]")
			.AddLine($"[{fg} bold]Email:[/] [{dim}]nikolaos.protopapas@gmail.com[/]")
			.AddLine($"[{fg} bold]License:[/] [{success}]MIT[/]")
			.AddLine("")
			.AddLine($"[{fg} bold]Core Features:[/]")
			.AddLine($"[{dim}]• Double-buffered rendering with dirty region tracking[/]")
			.AddLine($"[{dim}]• Modern async/await and fluent builder patterns[/]")
			.AddLine($"[{dim}]• Rich control library with Spectre.Console integration[/]")
			.AddLine($"[{dim}]• Plugin system with reflection-free patterns[/]")
			.AddLine($"[{dim}]• Configurable frame rate limiting and performance metrics[/]")
			.AddLine("")
			.AddLine($"[{fg} bold]Loaded Plugins:[/] [{warn}]{pluginState.LoadedPluginCount}[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(2, 0, 1, 0)
			.Build());

		// Display loaded plugin names if any
		if (pluginState.LoadedPluginCount > 0)
		{
			var pluginLines = pluginState.PluginNames.Select(name => $"[{dim}]  • {name}[/]").ToArray();
			modal.AddControl(Ctl.Markup()
				.AddLines(pluginLines)
				.WithAlignment(HorizontalAlignment.Left)
				.WithMargin(2, 0, 1, 0)
				.Build());
		}

		// Bottom separator
		modal.AddControl(Ctl.RuleBuilder()
			.WithColor(ruleColor)
			.StickyBottom()
			.Build());

		// Footer
		modal.AddControl(Ctl.Markup()
			.AddLine($"[{dim}]Press Escape or Enter to close[/]")
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
