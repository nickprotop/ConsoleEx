using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Models;
using Ctl = SharpConsoleUI.Builders.Controls;
using System.Drawing;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides Start menu dialog functionality using desktop portals.
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

		// Check if a start menu portal is already open — toggle behavior
		var existing = windowSystem.DesktopPortalService.Portals
			.FirstOrDefault(p => p.Owner is MenuControl m && m.Name == "StartMenu");

		if (existing != null)
		{
			windowSystem.DesktopPortalService.RemovePortal(existing);
			return;
		}

		// Determine menu position based on status bar location
		var startButtonLocation = windowSystem.Options.StatusBar.StartButtonLocation;

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
		BuildSystemCategory(menuBuilder, windowSystem);
		BuildPluginsCategory(menuBuilder, windowSystem);
		BuildUserActionsCategory(menuBuilder, windowSystem);
		BuildWindowsCategory(menuBuilder, windowSystem);

		// Always add Exit at top level
		menuBuilder.AddSeparator();
		menuBuilder.AddItem("Exit Application", () =>
		{
			windowSystem.DesktopPortalService.DismissAllPortals();
			windowSystem.Shutdown(0);
		});

		var menu = menuBuilder.Build();

		// Position menu using PortalPositioner
		var startBounds = windowSystem.StatusBarStateService.StartButtonBounds;
		var desktopDims = windowSystem.DesktopDimensions;
		var desktopUpperLeft = windowSystem.DesktopUpperLeft;
		var desktopBottomRight = windowSystem.DesktopBottomRight;

		// Use DesktopBottomRight as the authoritative reference for available space
		// (DesktopDimensions.Height can be off by 1 from the actual clipping boundary)
		int availableWidth = desktopBottomRight.X + 1 - desktopUpperLeft.X;
		int availableHeight = desktopBottomRight.Y + 1 - desktopUpperLeft.Y;
		var screenBounds = new Rectangle(0, 0, availableWidth, availableHeight);

		var menuSize = menu.GetLogicalContentSize();
		var placement = startButtonLocation == Configuration.StatusBarLocation.Bottom
			? PortalPlacement.AboveOrBelow
			: PortalPlacement.BelowOrAbove;

		// Anchor at the desktop edge (start button is on the status bar, outside desktop)
		var anchorY = startButtonLocation == Configuration.StatusBarLocation.Bottom
			? availableHeight  // Bottom edge of desktop
			: 0;               // Top edge of desktop
		var anchorInDesktop = new Rectangle(
			startBounds.X, anchorY,
			startBounds.Width, 1);

		var posResult = PortalPositioner.Calculate(new PortalPositionRequest(
			Anchor: anchorInDesktop,
			ContentSize: new Size(menuSize.Width, menuSize.Height),
			ScreenBounds: screenBounds,
			Placement: placement));

		// Portal Bounds = menu's screen position and size
		// BufferSize = full desktop (so submenu portals have room to render beyond menu bounds)
		var portalBounds = new Rectangle(
			desktopUpperLeft.X + posResult.Bounds.X,
			desktopUpperLeft.Y + posResult.Bounds.Y,
			posResult.Bounds.Width, posResult.Bounds.Height);

		windowSystem.DesktopPortalService.CreatePortal(new Core.DesktopPortalOptions(
			Content: menu,
			Bounds: portalBounds,
			DismissOnClickOutside: true,
			ConsumeClickOnDismiss: true,
			DimBackground: false,
			Owner: menu,
			BufferSize: new Size(desktopDims.Width, desktopDims.Height),
			BufferOrigin: new Point(desktopUpperLeft.X, desktopUpperLeft.Y)));
	}

	private static void BuildSystemCategory(MenuBuilder menuBuilder, ConsoleWindowSystem windowSystem)
	{
		if (!windowSystem.Options.StatusBar.ShowSystemMenuCategory)
			return;

		menuBuilder.AddItem("System", systemMenu =>
		{
			systemMenu.AddItem("Change Theme...", () =>
			{
				windowSystem.DesktopPortalService.DismissAllPortals();
				ThemeSelectorDialog.Show(windowSystem);
			});

			systemMenu.AddItem("Settings...", () =>
			{
				windowSystem.DesktopPortalService.DismissAllPortals();
				SettingsDialog.Show(windowSystem);
			});

			systemMenu.AddItem("About...", () =>
			{
				windowSystem.DesktopPortalService.DismissAllPortals();
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
						windowSystem.DesktopPortalService.DismissAllPortals();
					}
				);

				var frameLimitingEnabled = windowSystem.Performance.IsFrameRateLimitingEnabled;
				perfMenu.AddItem(
					$"Toggle Frame Limiting {(frameLimitingEnabled ? "[ON]" : "[OFF]")}",
					() =>
					{
						windowSystem.Performance.SetFrameRateLimiting(!windowSystem.Performance.IsFrameRateLimitingEnabled);
						windowSystem.DesktopPortalService.DismissAllPortals();
					}
				);

				perfMenu.AddItem("Set Target FPS...", () =>
				{
					windowSystem.DesktopPortalService.DismissAllPortals();
					PerformanceDialog.Show(windowSystem);
				});
			});
		});
	}

	private static void BuildPluginsCategory(MenuBuilder menuBuilder, ConsoleWindowSystem windowSystem)
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
						windowSystem.DesktopPortalService.DismissAllPortals();
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
						windowSystem.DesktopPortalService.DismissAllPortals();
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

	private static void BuildUserActionsCategory(MenuBuilder menuBuilder, ConsoleWindowSystem windowSystem)
	{
		var userActions = windowSystem.StatusBarStateService.GetStartMenuActions();
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
							windowSystem.DesktopPortalService.DismissAllPortals();
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
								windowSystem.DesktopPortalService.DismissAllPortals();
							});
						}
					});
				}
			}
		});
	}

	private static void BuildWindowsCategory(MenuBuilder menuBuilder, ConsoleWindowSystem windowSystem)
	{
		if (!windowSystem.Options.StatusBar.ShowWindowListInMenu)
			return;

		var topLevelWindows = windowSystem.Windows.Values
			.Where(w => w.ParentWindow == null)
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
					windowSystem.DesktopPortalService.DismissAllPortals();
					windowSystem.SetActiveWindow(targetWindow);
					if (targetWindow.State == WindowState.Minimized)
						targetWindow.State = WindowState.Normal;
				});
			}
		});
	}
}
