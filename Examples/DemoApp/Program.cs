using DemoApp.DemoWindows;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Panel;
using SharpConsoleUI.Rendering;
using Color = SharpConsoleUI.Color;

namespace DemoApp;

public class Program
{
	/// <summary>Whether a flag appears in the command line.</summary>
	private static bool HasFlag(string[] args, string flag) =>
		Array.Exists(args, a => string.Equals(a, flag, StringComparison.Ordinal));

	/// <summary>Columns for a headless run, from <c>--size WxH</c>.</summary>
	private static int ScreenWidth(string[] args) => ScreenSize(args).Width;

	/// <summary>Rows for a headless run, from <c>--size WxH</c>.</summary>
	private static int ScreenHeight(string[] args) => ScreenSize(args).Height;

	/// <summary>
	/// The screen a headless run uses: <c>--size 120x40</c>, or a readable default.
	/// </summary>
	private static (int Width, int Height) ScreenSize(string[] args)
	{
		for (int i = 0; i < args.Length - 1; i++)
		{
			if (args[i] != "--size") continue;

			string[] parts = args[i + 1].Split('x', 'X');
			if (parts.Length == 2
				&& int.TryParse(parts[0], out int w)
				&& int.TryParse(parts[1], out int h)
				&& w > 0 && h > 0)
			{
				return (w, h);
			}
		}

		return (120, 40);
	}

	static async Task<int> Main(string[] args)
	{
		if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

		try
		{
			var options = CreateOptions();

			// --headless runs with no terminal at all, which is how a debugger or
			// a CI job drives this app: the diagnostics socket still serves, and
			// the screen is read over it rather than painted anywhere. Size comes
			// from --size WxH, since there is no terminal to ask.
			IConsoleDriver driver = HasFlag(args, "--headless")
				? new HeadlessConsoleDriver(ScreenWidth(args), ScreenHeight(args))
				: new NetConsoleDriver(RenderMode.Buffer);

			var windowSystem = new ConsoleWindowSystem(driver, options: options);

			Configure(windowSystem, args);

			windowSystem.Run();
			return 0;
		}
		catch (Exception ex)
		{
			// Console.Clear() itself throws when stdout is redirected, which would replace the real
			// exception with an unrelated IOException and hide why the app actually failed.
			try { Console.Clear(); } catch { /* not a console — the message below still matters */ }
			ExceptionFormatter.WriteException(ex);
			return 1;
		}
	}

	/// <summary>
	/// The panel layout this demo runs with: a status bar, a Start menu, a taskbar, a clock.
	/// </summary>
	/// <returns>Options ready to hand to a <see cref="ConsoleWindowSystem"/>.</returns>
	/// <remarks>
	/// Separate from <see cref="Configure"/> because panels are fixed at construction while
	/// everything else is set afterwards. Public so another host can present this same
	/// application - the WebHost example runs it headless and serves it to a browser.
	/// </remarks>
	public static ConsoleWindowSystemOptions CreateOptions()
	{
		return new ConsoleWindowSystemOptions(
				TopPanelConfig: panel => panel
					.Left(Elements.StatusText("[bold]SharpConsoleUI Demo[/]"))
					.Left(Elements.Separator())
					.Left(Elements.StatusText("[dim]Ctrl+L: Launcher[/]"))
					.Right(Elements.Performance()),
				BottomPanelConfig: panel => panel
					.Left(Elements.StartMenu()
						.WithText("\u2630 Start")
						.WithOptions(new StartMenuOptions
						{
							AppName = "SharpConsoleUI Demo",
							SidebarStyle = StartMenuSidebarStyle.IconLabel
							// Background left unset so the Start menu follows the active theme (a hardcoded
							// dark gradient here rendered as an unreadable dark popup on light themes).
						}))
					.Center(Elements.TaskBar())
					.Right(Elements.Clock().WithFormat("HH:mm:ss"))
			);
	}

	/// <summary>
	/// Builds the demo into a window system: background, settings, launcher, shortcuts.
	/// </summary>
	/// <param name="windowSystem">The system to configure.</param>
	/// <param name="args">Command-line arguments, for the capture helpers below.</param>
	public static void Configure(ConsoleWindowSystem windowSystem, string[] args)
	{
			// Ocean Dots — the same configuration the Desktop Background demo's "Ocean Dots" button
			// applies, made the default so the app opens on it.
			windowSystem.DesktopBackground = new DesktopBackgroundConfig
			{
				Gradient = new GradientBackground(
					ColorGradient.FromColors(new Color(10, 40, 90), new Color(5, 10, 25)),
					GradientDirection.Vertical),
				Pattern = DesktopPatterns.Dots
			};

			// Register a sample custom settings group
			windowSystem.RegisterSettingsGroup("Demo", new Color(255, 200, 100), group =>
			{
				group.AddPage("About Demo", icon: "★", subtitle: "Demo application info",
					content: panel =>
					{
						panel.AddControl(Controls.Markup()
							.AddLine("[bold rgb(255,200,100)]Demo Application[/]")
							.AddEmptyLine()
							.AddLine("[dim]This is a sample custom settings page[/]")
							.AddLine("[dim]registered via the extensibility API.[/]")
							.Build());
					});
			});

			LauncherWindow.Create(windowSystem);

			// Command-line helpers for headless/automated capture:
			//   --theme <Name>   switch to a registered theme before showing the UI
			//   --demo "<Name>"  open a specific demo window by its launcher name on startup
			for (int i = 0; i < args.Length - 1; i++)
			{
				if (args[i] == "--theme")
					windowSystem.ThemeStateService.SwitchTheme(args[i + 1]);
				else if (args[i] == "--demo")
					LauncherWindow.LaunchByName(windowSystem, args[i + 1]);
			}

			// Shared logic: open/activate/recreate launcher
			void OpenLauncher()
			{
				var launcher = windowSystem.Windows.Values
					.FirstOrDefault(w => w.Title == "SharpConsoleUI Demo");

				if (launcher != null)
				{
					if (launcher.State == WindowState.Minimized)
						launcher.Restore();
					windowSystem.SetActiveWindow(launcher);
				}
				else
				{
					LauncherWindow.Create(windowSystem);
				}
			}

			// Ctrl+L global shortcut
			windowSystem.RegisterGlobalShortcut(ConsoleModifiers.Control, ConsoleKey.L, OpenLauncher);

			// Start menu action
			var startMenu = windowSystem.BottomPanel!.FindElement<StartMenuElement>("startmenu")!;
			startMenu.RegisterAction("Launcher", OpenLauncher, order: 0);
	}
}
