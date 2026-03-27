using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Controls.StartMenu;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using System.Drawing;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides Start menu dialog functionality using a borderless window.
/// The Start menu is a non-movable, non-resizable borderless window that
/// hosts standard controls and closes on deactivation.
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

		// Toggle: close existing start menu window if open
		var existing = windowSystem.StatusBarStateService.StartMenuWindow;
		if (existing != null)
		{
			existing.Close(force: true);
			windowSystem.StatusBarStateService.StartMenuWindow = null;
			return;
		}

		var menuOpts = windowSystem.Options.StatusBar.StartMenuConfig;
		var showIcons = menuOpts.ShowIcons;

		// Resolve dropdown colors from theme
		var theme = windowSystem.Theme;
		var dropBg = theme?.MenuDropdownBackgroundColor ?? Color.Grey15;
		var dropFg = theme?.MenuDropdownForegroundColor ?? Color.Grey93;
		var dropHiBg = theme?.MenuDropdownHighlightBackgroundColor ?? Color.DarkBlue;
		var dropHiFg = theme?.MenuDropdownHighlightForegroundColor ?? Color.White;

		// Build header markup — use configured app name/version or library defaults
		var appName = menuOpts.AppName ?? "SharpConsoleUI";
		var libVersion = typeof(ConsoleWindowSystem).Assembly.GetName().Version;
		var appVersion = menuOpts.AppVersion
			?? (libVersion != null ? $"{libVersion.Major}.{libVersion.Minor}.{libVersion.Build}" : "0.0.1");
		var headerLabel = new MarkupControl(
			StartMenuStyleHelper.FormatAppHeaderLines(appName, appVersion, showIcons, menuOpts.HeaderIcon));
		headerLabel.Margin = new Margin(1, 0, 1, 0);

		// Build category menu
		var categoryMenu = BuildCategoryMenu(windowSystem, menuOpts, dropBg, dropFg, dropHiBg, dropHiFg);

		// Build header rule separator
		var headerRule = new RuleControl { BorderStyle = BorderStyle.Single };

		// Build column structure
		var controls = new List<IWindowControl>();
		int menuWidth;
		int menuHeight;

		if (menuOpts.Layout == StartMenuLayout.TwoColumn)
		{
			var grid = new HorizontalGridControl();
			var leftColumn = new ColumnContainer(grid);
			leftColumn.AddContent(headerLabel);
			leftColumn.AddContent(headerRule);
			leftColumn.AddContent(categoryMenu);
			grid.AddColumn(leftColumn);

			// Right column
			var rightColumn = new ColumnContainer(grid);

			if (menuOpts.ShowWindowList)
			{
				var windowListHeader = new RuleControl
				{
					Title = "Windows",
					BorderStyle = BorderStyle.Single
				};

				var windowList = new ListControl();
				windowList.MaxVisibleItems = ControlDefaults.StartMenuMaxVisibleWindows;
				PopulateWindowList(windowSystem, windowList);

				windowList.ItemActivated += (_, item) =>
				{
					if (item.Tag is Window targetWindow)
					{
						CloseStartMenu(windowSystem);
						windowSystem.SetActiveWindow(targetWindow);
						if (targetWindow.State == WindowState.Minimized)
							targetWindow.State = WindowState.Normal;
					}
				};

				var infoSeparator = new RuleControl { BorderStyle = BorderStyle.Single };

				var themeName = windowSystem.Theme?.Name ?? "Default";
				var windowCount = windowSystem.Windows.Values.Count(w => w.ParentWindow == null && w.ShowInTaskbar);
				var pluginCount = windowSystem.PluginStateService.CurrentState.LoadedPluginCount;
				var infoStrip = new MarkupControl(
					StartMenuStyleHelper.FormatInfoStripLines(themeName, windowCount, pluginCount));
				infoStrip.Margin = new Margin(1, 0, 1, 0);

				rightColumn.AddContent(windowListHeader);
				rightColumn.AddContent(windowList);
				rightColumn.AddContent(infoSeparator);
				rightColumn.AddContent(infoStrip);
			}

			grid.AddColumn(rightColumn);

			// Add visual separator between columns
			var splitter = new SplitterControl { IsEnabled = false };
			grid.AddSplitterAfter(leftColumn, splitter);

			grid.HorizontalAlignment = HorizontalAlignment.Stretch;
			grid.VerticalAlignment = VerticalAlignment.Fill;

			// Compute column widths
			int leftWidth = ComputeLeftColumnWidth(categoryMenu);
			int rightWidth = ComputeRightColumnWidth(windowSystem);
			leftColumn.Width = leftWidth;
			rightColumn.Width = rightWidth;

			controls.Add(grid);
			// Compute width from explicit column widths (GetLogicalContentSize ignores them)
			int splitterWidth = 1;
			int borderOverhead = 2; // left + right window border
			menuWidth = leftWidth + splitterWidth + rightWidth + borderOverhead;
			menuHeight = grid.GetLogicalContentSize().Height + borderOverhead;
		}
		else
		{
			// Single column: add controls directly to the window
			controls.Add(headerLabel);
			controls.Add(headerRule);
			controls.Add(categoryMenu);

			var menuSize = categoryMenu.GetLogicalContentSize();
			int borderOverhead = 2; // left + right window border
			menuWidth = Math.Max(menuSize.Width + 2, ControlDefaults.StartMenuMinLeftColumnWidth) + borderOverhead;
			menuHeight = headerLabel.GetLogicalContentSize().Height
				+ 1 // rule
				+ menuSize.Height
				+ borderOverhead;
		}

		// Compute position relative to start button
		var startButtonLocation = windowSystem.Options.StatusBar.StartButtonLocation;
		var startBounds = windowSystem.StatusBarStateService.StartButtonBounds;
		var desktopUpperLeft = windowSystem.DesktopUpperLeft;
		var desktopBottomRight = windowSystem.DesktopBottomRight;

		int availableWidth = desktopBottomRight.X + 1 - desktopUpperLeft.X;
		int availableHeight = desktopBottomRight.Y + 1 - desktopUpperLeft.Y;
		var screenBounds = new Rectangle(0, 0, availableWidth, availableHeight);

		// Add border overhead for the window (2 for borders even though borderless,
		// the window still occupies the content area)
		var placement = startButtonLocation == StatusBarLocation.Bottom
			? PortalPlacement.AboveOrBelow
			: PortalPlacement.BelowOrAbove;

		var anchorY = startButtonLocation == StatusBarLocation.Bottom
			? availableHeight
			: 0;
		var anchorInDesktop = new Rectangle(
			startBounds.X, anchorY,
			startBounds.Width, 1);

		var posResult = PortalPositioner.Calculate(new PortalPositionRequest(
			Anchor: anchorInDesktop,
			ContentSize: new Size(menuWidth, menuHeight),
			ScreenBounds: screenBounds,
			Placement: placement));

		// Convert to absolute screen coordinates
		int windowX = desktopUpperLeft.X + posResult.Bounds.X;
		int windowY = desktopUpperLeft.Y + posResult.Bounds.Y;

		// Build the start menu window
		var builder = new WindowBuilder(windowSystem)
			.WithName("StartMenu")
			.WithBorderStyle(BorderStyle.Rounded)
			.HideTitle()
			.HideTitleButtons()
			.Movable(false)
			.Resizable(false)
			.WithAlwaysOnTop()
			.ShowInTaskbar(false)
			.WithBounds(windowX, windowY, posResult.Bounds.Width, posResult.Bounds.Height)
			.WithBackgroundColor(dropBg)
			.WithForegroundColor(dropFg);

		if (menuOpts.BackgroundGradient != null)
			builder.WithBackgroundGradient(menuOpts.BackgroundGradient.Gradient, menuOpts.BackgroundGradient.Direction);

		foreach (var control in controls)
			builder.AddControl(control);

		builder
			.WithCloseOnDeactivate()
			.OnClosed((s, e) =>
			{
				windowSystem.StatusBarStateService.StartMenuWindow = null;
			});

		// Register BEFORE BuildAndShow so OnDeactivated can find it
		var window = builder.Build();
		windowSystem.StatusBarStateService.StartMenuWindow = window;
		windowSystem.AddWindow(window, activateWindow: true);
	}

	#region Menu Building

	private static MenuControl BuildCategoryMenu(
		ConsoleWindowSystem windowSystem,
		StartMenuOptions menuOpts,
		Color dropBg, Color dropFg, Color dropHiBg, Color dropHiFg)
	{
		var showIcons = menuOpts.ShowIcons;
		var menuBuilder = Ctl.Menu()
			.Vertical()
			.WithName("StartMenuCategories")
			.Sticky()
			.WithDropdownColors(dropBg, dropFg, dropHiBg, dropHiFg);

		// Quick actions at top level
		if (menuOpts.ShowSystemCategory)
			BuildQuickActions(windowSystem, menuBuilder);

		menuBuilder.AddSeparator();

		// Category submenus
		if (menuOpts.Layout == StartMenuLayout.SingleColumn && menuOpts.ShowWindowList)
		{
			BuildWindowsCategoryMenu(windowSystem, menuBuilder, showIcons);
		}

		if (menuOpts.ShowSystemCategory)
			BuildSystemCategory(windowSystem, menuBuilder, showIcons);
		BuildPluginsCategory(windowSystem, menuBuilder, showIcons);
		BuildUserActionsCategory(windowSystem, menuBuilder);

		menuBuilder.AddSeparator();

		// Exit action
		menuBuilder.AddItem(StartMenuStyleHelper.FormatExitRow(showIcons), () =>
		{
			CloseStartMenu(windowSystem);
			windowSystem.DesktopPortalService.DismissAllPortals();
			windowSystem.Shutdown(0);
		});

		return menuBuilder.Build();
	}

	private static void BuildQuickActions(
		ConsoleWindowSystem windowSystem, MenuBuilder menuBuilder)
	{

		menuBuilder.AddItem("Change Theme...", () =>
		{
			CloseStartMenu(windowSystem);
			ThemeSelectorDialog.Show(windowSystem);
		});

		menuBuilder.AddItem("Settings...", () =>
		{
			CloseStartMenu(windowSystem);
			SettingsDialog.Show(windowSystem);
		});

		menuBuilder.AddItem("About...", () =>
		{
			CloseStartMenu(windowSystem);
			AboutDialog.Show(windowSystem);
		});
	}

	private static void BuildSystemCategory(
		ConsoleWindowSystem windowSystem, MenuBuilder menuBuilder, bool showIcons)
	{
		var headerText = StartMenuStyleHelper.FormatCategoryHeader("System", false, null);
		menuBuilder.AddItem(headerText, systemMenu =>
		{
			systemMenu.AddItem("Performance", perfMenu =>
			{
				var metricsEnabled = windowSystem.Performance.IsPerformanceMetricsEnabled;
				perfMenu.AddItem(
					$"Toggle Metrics {(metricsEnabled ? "[ON]" : "[OFF]")}",
					() =>
					{
						windowSystem.Performance.SetPerformanceMetrics(!windowSystem.Performance.IsPerformanceMetricsEnabled);
						CloseStartMenu(windowSystem);
					});

				var frameLimitingEnabled = windowSystem.Performance.IsFrameRateLimitingEnabled;
				perfMenu.AddItem(
					$"Toggle Frame Limiting {(frameLimitingEnabled ? "[ON]" : "[OFF]")}",
					() =>
					{
						windowSystem.Performance.SetFrameRateLimiting(!windowSystem.Performance.IsFrameRateLimitingEnabled);
						CloseStartMenu(windowSystem);
					});

				perfMenu.AddItem("Set Target FPS...", () =>
				{
					CloseStartMenu(windowSystem);
					PerformanceDialog.Show(windowSystem);
				});
			});
		});
	}

	private static void BuildPluginsCategory(
		ConsoleWindowSystem windowSystem, MenuBuilder menuBuilder, bool showIcons)
	{
		var pluginState = windowSystem.PluginStateService.CurrentState;
		if (pluginState.LoadedPluginCount == 0)
			return;

		var hasAnyContent = false;
		var pluginItems = new List<(string Name, Action<MenuItemBuilder> Configure)>();

		foreach (var plugin in windowSystem.PluginStateService.LoadedPlugins)
		{
			var windows = plugin.GetWindows();
			var actionProviders = plugin.GetActionProviders();
			var allActions = actionProviders
				.SelectMany(p => p.GetAvailableActions().Select(a => (Provider: p, Action: a)))
				.ToList();

			if (windows.Count == 0 && allActions.Count == 0)
				continue;

			hasAnyContent = true;

			pluginItems.Add((plugin.Info.Name, pluginMenu =>
			{
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
						CloseStartMenu(windowSystem);
					});
				}

				if (windows.Count > 0 && allActions.Count > 0)
					pluginMenu.AddSeparator();

				foreach (var (provider, action) in allActions)
				{
					var providerName = provider.ProviderName;
					var actionName = action.Name;
					pluginMenu.AddItem(actionName, () =>
					{
						windowSystem.PluginStateService.ExecutePluginAction(
							providerName, actionName, context: null);
						CloseStartMenu(windowSystem);
					});
				}
			}));
		}

		if (!hasAnyContent)
			return;

		var headerText = StartMenuStyleHelper.FormatCategoryHeader("Plugins", false, null);
		menuBuilder.AddItem(headerText, pluginsMenu =>
		{
			foreach (var (name, configure) in pluginItems)
			{
				pluginsMenu.AddItem(name, configure);
			}
		});
	}

	private static void BuildUserActionsCategory(
		ConsoleWindowSystem windowSystem, MenuBuilder menuBuilder)
	{
		var userActions = windowSystem.StatusBarStateService.GetStartMenuActions();
		if (userActions.Count == 0)
			return;

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
					foreach (var action in actions)
					{
						var callback = action.Callback;
						userMenu.AddItem(action.Name, () =>
						{
							callback();
							CloseStartMenu(windowSystem);
						});
					}
				}
				else
				{
					userMenu.AddItem(group.Key, subMenu =>
					{
						foreach (var action in actions)
						{
							var callback = action.Callback;
							subMenu.AddItem(action.Name, () =>
							{
								callback();
								CloseStartMenu(windowSystem);
							});
						}
					});
				}
			}
		});
	}

	private static void BuildWindowsCategoryMenu(
		ConsoleWindowSystem windowSystem, MenuBuilder menuBuilder, bool showIcons)
	{
		var topLevelWindows = windowSystem.Windows.Values
			.Where(w => w.ParentWindow == null && w.ShowInTaskbar)
			.ToList();

		if (topLevelWindows.Count == 0)
			return;

		var headerText = StartMenuStyleHelper.FormatCategoryHeader("Windows", false, null);
		menuBuilder.AddItem(headerText, windowsMenu =>
		{
			for (int i = 0; i < topLevelWindows.Count; i++)
			{
				var window = topLevelWindows[i];
				var shortcut = i < 9 ? $"Alt-{i + 1}" : "";
				var minIndicator = window.State == WindowState.Minimized ? "[dim]" : "";
				var minEnd = window.State == WindowState.Minimized ? "[/]" : "";
				var text = $"{minIndicator}{window.Title}{minEnd}";

				var targetWindow = window;
				windowsMenu.AddItem(text, shortcut, () =>
				{
					CloseStartMenu(windowSystem);
					windowSystem.SetActiveWindow(targetWindow);
					if (targetWindow.State == WindowState.Minimized)
						targetWindow.State = WindowState.Normal;
				});
			}
		});
	}

	#endregion

	#region Right Column Helpers

	private static void PopulateWindowList(
		ConsoleWindowSystem windowSystem, ListControl windowList)
	{
		var topLevelWindows = windowSystem.Windows.Values
			.Where(w => w.ParentWindow == null && w.ShowInTaskbar)
			.ToList();

		var activeWindow = windowSystem.WindowStateService.ActiveWindow;

		for (int i = 0; i < Math.Min(topLevelWindows.Count, ControlDefaults.StartMenuMaxVisibleWindows); i++)
		{
			var window = topLevelWindows[i];
			var isMinimized = window.State == WindowState.Minimized;
			var isActive = ReferenceEquals(window, activeWindow);
			var text = StartMenuStyleHelper.FormatWindowItem(window.Title, i, isMinimized, isActive);

			windowList.AddItem(new ListItem(text) { Tag = window });
		}
	}

	#endregion

	#region Sizing

	private static int ComputeLeftColumnWidth(MenuControl categoryMenu)
	{
		var menuSize = categoryMenu.GetLogicalContentSize();
		return Math.Clamp(
			menuSize.Width + 2,
			ControlDefaults.StartMenuMinLeftColumnWidth,
			ControlDefaults.StartMenuMaxLeftColumnWidth);
	}

	private static int ComputeRightColumnWidth(
		ConsoleWindowSystem windowSystem)
	{
		int maxItemWidth = 0;
		var topLevelWindows = windowSystem.Windows.Values
			.Where(w => w.ParentWindow == null && w.ShowInTaskbar)
			.ToList();

		for (int i = 0; i < Math.Min(topLevelWindows.Count, ControlDefaults.StartMenuMaxVisibleWindows); i++)
		{
			var window = topLevelWindows[i];
			var formatted = StartMenuStyleHelper.FormatWindowItem(
				window.Title, i,
				window.State == WindowState.Minimized,
				false);
			var displayWidth = MarkupParser.StripLength(formatted);
			maxItemWidth = Math.Max(maxItemWidth, displayWidth);
		}

		int contentWidth = maxItemWidth + 2;
		return Math.Clamp(
			contentWidth,
			ControlDefaults.StartMenuMinRightColumnWidth,
			ControlDefaults.StartMenuMaxRightColumnWidth);
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Closes the start menu window if it is open.
	/// </summary>
	private static void CloseStartMenu(ConsoleWindowSystem windowSystem)
	{
		var smw = windowSystem.StatusBarStateService.StartMenuWindow;
		if (smw != null)
		{
			smw.Close(force: true);
			windowSystem.StatusBarStateService.StartMenuWindow = null;
		}
		windowSystem.DesktopPortalService.DismissAllPortals();
	}

	#endregion
}
