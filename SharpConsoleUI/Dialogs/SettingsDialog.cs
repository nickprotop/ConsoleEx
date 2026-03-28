using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs.Settings;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides a unified settings dialog with NavigationView-based grouped sections.
/// </summary>
public static class SettingsDialog
{
	/// <summary>
	/// Shows the settings dialog for configuring application preferences.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	public static void Show(ConsoleWindowSystem windowSystem, Window? parentWindow = null)
	{
		var gradient = ColorGradient.FromColors(
			new Color(10, 15, 40),
			new Color(25, 40, 80),
			new Color(15, 20, 50));

		var navBuilder = Ctl.NavigationView()
			.WithNavWidth(28)
			.WithPaneHeader("[bold rgb(120,180,255)]  ⚙  Settings[/]")
			.WithSelectedColors(Color.White, new Color(40, 80, 160))
			.WithSelectionIndicator('▸')
			.WithPaneDisplayMode(NavigationViewDisplayMode.Auto)
			.WithExpandedThreshold(80)
			.WithCompactThreshold(50)
			.WithCompactPaneWidth(6)
			.WithAnimateTransitions(true)
			.WithContentBorder(BorderStyle.Rounded)
			.WithContentBorderColor(new Color(60, 80, 120))
			.WithContentBackground(new Color(20, 25, 45))
			.WithContentPadding(1, 0, 1, 0)
			.WithContentHeader(true)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill);

		// Built-in groups
		navBuilder.AddHeader("Appearance", new Color(120, 180, 255), h =>
		{
			h.AddItem("Theme", icon: "◐", subtitle: "Select visual theme",
				content: panel => ThemePage.Build(panel, windowSystem));
			h.AddItem("Status Bar", icon: "▬", subtitle: "Configure status bar",
				content: panel => StatusBarPage.Build(panel, windowSystem));
		});

		navBuilder.AddHeader("Performance", new Color(252, 152, 103), h =>
		{
			h.AddItem("Rendering", icon: "▦", subtitle: "Display and rendering options",
				content: panel => RenderingPage.Build(panel, windowSystem));
			h.AddItem("Animations", icon: "◎", subtitle: "Animation behavior",
				content: panel => AnimationsPage.Build(panel, windowSystem));
		});

		navBuilder.AddHeader("Logging", new Color(255, 97, 136), h =>
		{
			h.AddItem("Log Settings", icon: "▤", subtitle: "Log level and output",
				content: panel => LogSettingsPage.Build(panel, windowSystem));
		});

		navBuilder.AddHeader("Info", new Color(171, 157, 242), h =>
		{
			h.AddItem("System", icon: "⊞", subtitle: "Runtime and system information",
				content: panel => InfoPage.Build(panel, windowSystem));
		});

		// Custom registered groups
		foreach (var group in windowSystem.SettingsRegistrationService.Groups)
		{
			var capturedGroup = group;
			navBuilder.AddHeader(capturedGroup.Name, capturedGroup.AccentColor, h =>
			{
				foreach (var page in capturedGroup.Pages)
				{
					var capturedPage = page;
					h.AddItem(capturedPage.Name, icon: capturedPage.Icon,
						subtitle: capturedPage.Subtitle,
						content: panel => capturedPage.ContentFactory(panel));
				}
			});
		}

		var nav = navBuilder.Build();

		var builder = new WindowBuilder(windowSystem)
			.WithTitle("⚙ Settings")
			.Centered()
			.WithSize(90, 28)
			.AsModal()
			.Resizable(false)
			.Minimizable(false)
			.Maximizable(false)
			.Movable(true)
			.WithBackgroundGradient(gradient, GradientDirection.DiagonalDown)
			.AddControl(nav);

		if (parentWindow != null)
			builder.WithParent(parentWindow);

		var modal = builder.Build();

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
