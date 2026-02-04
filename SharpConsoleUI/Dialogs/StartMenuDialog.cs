using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Models;
using SharpConsoleUI.Windows;
using Spectre.Console;
using Ctl = SharpConsoleUI.Builders.Controls;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides Start menu dialog functionality.
/// </summary>
public static class StartMenuDialog
{
	// Debounce: track last invocation time to prevent double-trigger from mouse events
	private static DateTime _lastInvocation = DateTime.MinValue;
	private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(200);

	/// <summary>
	/// Shows the Start menu with system actions, plugins, user actions, and windows.
	/// If the Start menu is already open, it will be closed (toggle behavior).
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	public static void Show(ConsoleWindowSystem windowSystem)
	{
		// Debounce: Ignore rapid repeated calls (e.g., from Button1Pressed + Button1Clicked)
		var now = DateTime.Now;
		if (now - _lastInvocation < DebounceInterval)
		{
			return;
		}
		_lastInvocation = now;

		// Check if a start menu overlay is already open
		var existingOverlay = windowSystem.Windows.Values
			.OfType<OverlayWindow>()
			.FirstOrDefault();

		if (existingOverlay != null)
		{
			// Start menu already open - close it (toggle behavior)
			existingOverlay.Dismiss();
			return;
		}

		var theme = windowSystem.Theme;

		// Determine menu position based on status bar location
		var startButtonLocation = windowSystem.Options.StatusBar.StartButtonLocation;

		// Create full-screen overlay with dimmed background
		var overlay = new OverlayWindow(windowSystem);
		overlay.SetOverlayBackground(Color.Grey11);  // Dark dimmed background
		overlay.SetDismissOnClickOutside(true);

		// Create vertical menu
		var menuBuilder = Ctl.Menu()
			.Vertical()
			.WithName("StartMenu")
			.Sticky()
			.WithDropdownColors(
				Color.Grey15,
				Color.Grey93,
				Color.DarkBlue,
				Color.White
			);

		// Build menu structure
		BuildSystemCategory(menuBuilder, windowSystem, overlay);
		BuildPluginsCategory(menuBuilder, windowSystem, overlay);
		BuildUserActionsCategory(menuBuilder, windowSystem, overlay);
		BuildWindowsCategory(menuBuilder, windowSystem, overlay);

		// Always add Exit at top level
		menuBuilder.AddSeparator();
		menuBuilder.AddItem("Exit Application", () =>
		{
			overlay.Close(force: true);
			windowSystem.Shutdown(0);
		});

		var menu = menuBuilder.Build();

		// Position menu using DOM layout (Margin and StickyPosition)
		menu.Margin = new Controls.Margin(2, 2, 0, 0); // Left=2, Top=2 offset
		menu.StickyPosition = startButtonLocation == Configuration.StatusBarLocation.Bottom
			? Controls.StickyPosition.Bottom
			: Controls.StickyPosition.Top;

		// Add menu to overlay
		overlay.AddControl(menu);

		// Focus the menu
		menu.SetFocus(true, FocusReason.Programmatic);

		// Add overlay to window system and activate it
		windowSystem.AddWindow(overlay);
		windowSystem.SetActiveWindow(overlay);
	}

	private static void BuildSystemCategory(MenuBuilder menuBuilder, ConsoleWindowSystem windowSystem, OverlayWindow overlay)
	{
		if (!windowSystem.Options.StatusBar.ShowSystemMenuCategory)
			return;

		menuBuilder.AddItem("System", systemMenu =>
		{
			systemMenu.AddItem("Change Theme...", () =>
			{
				overlay.CloseAndInvalidate();
				ThemeSelectorDialog.Show(windowSystem);
			});

			systemMenu.AddItem("Settings...", () =>
			{
				overlay.CloseAndInvalidate();
				SettingsDialog.Show(windowSystem);
			});

			systemMenu.AddItem("About...", () =>
			{
				overlay.CloseAndInvalidate();
				AboutDialog.Show(windowSystem);
			});

			// Performance submenu
			systemMenu.AddItem("Performance", perfMenu =>
			{
				var metricsEnabled = windowSystem.Performance.IsPerformanceMetricsEnabled;
				perfMenu.AddItem(
					$"Toggle Metrics {(metricsEnabled ? "[ON]" : "[OFF]")}",
					() =>
					{
						windowSystem.Performance.SetPerformanceMetrics(!windowSystem.Performance.IsPerformanceMetricsEnabled);
						overlay.CloseAndInvalidate();
					}
				);

				var frameLimitingEnabled = windowSystem.Performance.IsFrameRateLimitingEnabled;
				perfMenu.AddItem(
					$"Toggle Frame Limiting {(frameLimitingEnabled ? "[ON]" : "[OFF]")}",
					() =>
					{
						windowSystem.Performance.SetFrameRateLimiting(!windowSystem.Performance.IsFrameRateLimitingEnabled);
						overlay.CloseAndInvalidate();
					}
				);

				perfMenu.AddItem("Set Target FPS...", () =>
				{
					overlay.CloseAndInvalidate();
					PerformanceDialog.Show(windowSystem);
				});
			});
		});
	}

	private static void BuildPluginsCategory(MenuBuilder menuBuilder, ConsoleWindowSystem windowSystem, OverlayWindow overlay)
	{
		var pluginState = windowSystem.PluginStateService.CurrentState;
		if (pluginState.LoadedPluginCount == 0)
			return;

		var hasAnyContent = false;

		// Temporary list to collect plugin menu items
		var pluginItems = new List<(string Name, Action<MenuItemBuilder> Configure)>();

		foreach (var plugin in windowSystem.PluginStateService.LoadedPlugins)
		{
			var windows = plugin.GetWindows();
			var actionProviders = plugin.GetActionProviders();

			// Collect all actions from providers
			var allActions = actionProviders
				.SelectMany(p => p.GetAvailableActions().Select(a => (Provider: p, Action: a)))
				.ToList();

			// Skip plugins with no windows or actions
			if (windows.Count == 0 && allActions.Count == 0)
				continue;

			hasAnyContent = true;

			pluginItems.Add((plugin.Info.Name, pluginMenu =>
			{
				// Add plugin windows
				foreach (var window in windows)
				{
					var windowName = window.Name;
					pluginMenu.AddItem(windowName, () =>
					{
						var w = windowSystem.PluginStateService.CreateWindow(windowName);
						if (w != null)
						{
							windowSystem.AddWindow(w);
							windowSystem.SetActiveWindow(w);
						}
						overlay.CloseAndInvalidate();
					});
				}

				// Add separator if both windows and actions exist
				if (windows.Count > 0 && allActions.Count > 0)
					pluginMenu.AddSeparator();

				// Add plugin actions
				foreach (var (provider, action) in allActions)
				{
					var providerName = provider.ProviderName;
					var actionName = action.Name;
					pluginMenu.AddItem(actionName, () =>
					{
						windowSystem.PluginStateService.ExecutePluginAction(
							providerName,
							actionName,
							context: null
						);
						overlay.CloseAndInvalidate();
					});
				}
			}));
		}

		if (!hasAnyContent)
			return;

		// Add plugins category with all plugin items
		menuBuilder.AddItem("Plugins", pluginsMenu =>
		{
			foreach (var (name, configure) in pluginItems)
			{
				pluginsMenu.AddItem(name, configure);
			}
		});
	}

	private static void BuildUserActionsCategory(MenuBuilder menuBuilder, ConsoleWindowSystem windowSystem, OverlayWindow overlay)
	{
		var userActions = windowSystem.GetStartMenuActions();
		if (userActions.Count == 0)
			return;

		// Group by category
		var grouped = userActions
			.GroupBy(a => a.Category ?? "User Actions")
			.OrderBy(g => g.Key);

		menuBuilder.AddItem("User Actions", userMenu =>
		{
			foreach (var group in grouped)
			{
				var actions = group.OrderBy(a => a.Order).ToList();

				if (group.Key == "User Actions")
				{
					// Add directly without subcategory
					foreach (var action in actions)
					{
						var callback = action.Callback;
						userMenu.AddItem(action.Name, () =>
						{
							callback();
							overlay.CloseAndInvalidate();
						});
					}
				}
				else
				{
					// Add as subcategory
					userMenu.AddItem(group.Key, subMenu =>
					{
						foreach (var action in actions)
						{
							var callback = action.Callback;
							subMenu.AddItem(action.Name, () =>
							{
								callback();
								overlay.CloseAndInvalidate();
							});
						}
					});
				}
			}
		});
	}

	private static void BuildWindowsCategory(MenuBuilder menuBuilder, ConsoleWindowSystem windowSystem, OverlayWindow overlay)
	{
		if (!windowSystem.Options.StatusBar.ShowWindowListInMenu)
			return;

		var topLevelWindows = windowSystem.Windows.Values
			.Where(w => w.ParentWindow == null && !(w is OverlayWindow))  // Exclude overlay windows
			.ToList();

		if (topLevelWindows.Count == 0)
			return;

		menuBuilder.AddItem("Windows", windowsMenu =>
		{
			for (int i = 0; i < topLevelWindows.Count; i++)
			{
				var window = topLevelWindows[i];
				var shortcut = i < 9 ? $"Alt-{i + 1}" : "";
				var minIndicator = window.State == WindowState.Minimized ? "[dim]" : "";
				var minEnd = window.State == WindowState.Minimized ? "[/]" : "";

				var text = string.IsNullOrEmpty(shortcut)
					? $"{minIndicator}{window.Title}{minEnd}"
					: $"{minIndicator}{window.Title}{minEnd}";

				// Capture window in local variable for closure
				var targetWindow = window;
				windowsMenu.AddItem(text, shortcut, () =>
				{
					// Close overlay first, then activate window
					overlay.CloseAndInvalidate();
					windowSystem.SetActiveWindow(targetWindow);
					if (targetWindow.State == WindowState.Minimized)
						targetWindow.State = WindowState.Normal;
				});
			}
		});
	}
}
