using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Controls.StartMenu;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using System.Drawing;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Provides Start menu dialog functionality using a NavigationView-based panel.
/// The Start menu is a non-movable, non-resizable window with rounded border that
/// hosts a NavigationView for category switching and closes on deactivation.
/// </summary>
public static class StartMenuDialog
{
	// Debounce: track last invocation time to prevent double-trigger from mouse events
	private static DateTime _lastInvocation = DateTime.MinValue;
	private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(ControlDefaults.DefaultDebounceMs);

	// Resolved colors for the current start menu instance (set during Show, used by content builders)
	private static Color _dropBg, _dropFg, _dropHiBg, _dropHiFg;

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
		var existing = windowSystem.PanelStateService.StartMenuWindow;
		if (existing != null)
		{
			existing.Close(force: true);
			windowSystem.PanelStateService.StartMenuWindow = null;
			return;
		}

		var menuOpts = windowSystem.Options.StatusBar.StartMenuConfig;
		var showIcons = menuOpts.ShowIcons;

		// Resolve colors: explicit option -> theme -> default
		var theme = windowSystem.Theme;
		var dropBg = ColorResolver.ResolveStartMenuBackground(menuOpts.BackgroundColor, theme);
		var dropFg = ColorResolver.ResolveStartMenuForeground(menuOpts.ForegroundColor, theme);
		var dropHiBg = ColorResolver.ResolveStartMenuHighlightBackground(menuOpts.HighlightBackgroundColor, theme);
		var dropHiFg = ColorResolver.ResolveStartMenuHighlightForeground(menuOpts.HighlightForegroundColor, theme);

		// Store for content builders
		_dropBg = dropBg;
		_dropFg = dropFg;
		_dropHiBg = dropHiBg;
		_dropHiFg = dropHiFg;

		// Build header markup
		var appName = menuOpts.AppName ?? "SharpConsoleUI";
		var libVersion = typeof(ConsoleWindowSystem).Assembly.GetName().Version;
		var appVersion = menuOpts.AppVersion
			?? (libVersion != null ? $"{libVersion.Major}.{libVersion.Minor}.{libVersion.Build}" : "0.0.1");
		var headerLabel = new MarkupControl(
			StartMenuStyleHelper.FormatAppHeaderLines(appName, appVersion, showIcons, menuOpts.HeaderIcon));
		headerLabel.Margin = new Margin(1, 0, 1, 0);

		// Build header rule separator
		var headerRule = new RuleControl { BorderStyle = BorderStyle.Single };

		// Build NavigationView
		var navView = new NavigationView();
		navView.ShowContentHeader = false;
		navView.ContentBorderStyle = BorderStyle.None;
		navView.ContentBackgroundColor = Color.Transparent;
		navView.ContentPadding = new Padding(1, 0, 1, 0);

		if (menuOpts.SidebarStyle == StartMenuSidebarStyle.IconRail)
		{
			navView.PaneDisplayMode = NavigationViewDisplayMode.Compact;
			navView.CompactPaneWidth = ControlDefaults.StartMenuCompactPaneWidth;
		}
		else
		{
			navView.PaneDisplayMode = NavigationViewDisplayMode.Expanded;
			navView.NavPaneWidth = ControlDefaults.StartMenuExpandedPaneWidth;
		}

		BuildSidebarItems(navView, windowSystem, menuOpts);
		navView.SelectedIndex = 0;
		navView.VerticalAlignment = VerticalAlignment.Fill;

		// Build bottom bar: exit (left) | extensions | info (right) — single line
		var bottomSeparator = new RuleControl
		{
			BorderStyle = BorderStyle.Single,
			VerticalAlignment = VerticalAlignment.Bottom
		};

		var exitIcon = showIcons ? $"{ControlDefaults.StartMenuExitIcon} " : "";
		var exitButton = new ButtonControl
		{
			Text = $"{exitIcon}Exit",
			ButtonBorder = ButtonBorderStyle.None
		};
		exitButton.Click += (_, _) =>
		{
			CloseStartMenu(windowSystem);
			windowSystem.DesktopPortalService.DismissAllPortals();
			windowSystem.Shutdown(0);
		};

		// Info text (right-aligned via spacer)
		var themeName = windowSystem.Theme?.Name ?? "Default";
		var windowCount = windowSystem.Windows.Values.Count(w => w.ParentWindow == null && w.ShowInTaskbar);
		var pluginCount = windowSystem.PluginStateService.CurrentState.LoadedPluginCount;
		var windowLabel = windowCount == 1 ? "W" : "W";
		var infoText = $"[dim]{themeName} | {windowLabel}:{windowCount} P:{pluginCount}[/]";

		// Append custom info items
		foreach (var extraItem in menuOpts.InfoStripItems)
			infoText += $" | {extraItem}";

		var infoLabel = new MarkupControl(new List<string> { infoText });

		// Bottom toolbar: exit left, spacer, info right
		var bottomBar = new ToolbarControl
		{
			VerticalAlignment = VerticalAlignment.Bottom,
			ShowAboveLine = false,
			ShowBelowLine = false
		};
		bottomBar.AddItem(exitButton);
		bottomBar.AddItem(new SeparatorControl());

		// Add any custom bottom bar items from extensions
		foreach (var barItem in menuOpts.BottomBarItems)
			bottomBar.AddItem(barItem);

		bottomBar.AddItem(infoLabel);

		// Compute size explicitly — NavigationView.GetLogicalContentSize() is unreliable
		// before content is laid out, so compute from sidebar + content widths directly.
		int borderOverhead = 2; // left + right window border
		int sidebarWidth = menuOpts.SidebarStyle == StartMenuSidebarStyle.IconRail
			? ControlDefaults.StartMenuCompactPaneWidth
			: ControlDefaults.StartMenuExpandedPaneWidth;
		int splitterWidth = 1;
		int menuWidth = sidebarWidth + splitterWidth + ControlDefaults.StartMenuContentPanelWidth + borderOverhead;
		int menuHeight = Math.Min(
			headerLabel.GetLogicalContentSize().Height
				+ 1 // header rule
				+ ControlDefaults.StartMenuMaxHeight
				+ 1 // bottom separator
				+ 1 // bottom bar (single line)
				+ borderOverhead,
			windowSystem.DesktopDimensions.Height);

		var startButtonLocation = windowSystem.Options.StatusBar.StartButtonLocation;
		var desktopUpperLeft = windowSystem.DesktopUpperLeft;
		var desktopBottomRight = windowSystem.DesktopBottomRight;

		// Compute start button bounds from config for positioning anchor
		int screenWidth = desktopBottomRight.X + 1;
		int startBtnWidth = MarkupParser.StripLength(windowSystem.Options.StatusBar.StartButtonText) + 1;
		int startBtnX = windowSystem.Options.StatusBar.StartButtonPosition == StartButtonPosition.Left
			? 0
			: screenWidth - startBtnWidth;
		var startBounds = new System.Drawing.Rectangle(startBtnX, 0, startBtnWidth, 1);

		int availableWidth = desktopBottomRight.X + 1 - desktopUpperLeft.X;
		int availableHeight = desktopBottomRight.Y + 1 - desktopUpperLeft.Y;
		var screenBounds = new Rectangle(0, 0, availableWidth, availableHeight);

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
			ContentSize: new System.Drawing.Size(menuWidth, menuHeight),
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

		builder
			.AddControl(headerLabel)
			.AddControl(headerRule)
			.AddControl(navView)
			.AddControl(bottomSeparator)
			.AddControl(bottomBar)
			.WithCloseOnDeactivate();

		// Subscribe to options changes — close and reopen when config changes
		EventHandler? optionsHandler = null;
		optionsHandler = (_, _) =>
		{
			menuOpts.OptionsChanged -= optionsHandler;
			CloseStartMenu(windowSystem);
			_lastInvocation = DateTime.MinValue;
			Show(windowSystem);
		};
		menuOpts.OptionsChanged += optionsHandler;

		builder.OnClosed((s, e) =>
		{
			menuOpts.OptionsChanged -= optionsHandler;
			windowSystem.PanelStateService.StartMenuWindow = null;
		});

		// Register BEFORE BuildAndShow so OnDeactivated can find it
		var window = builder.Build();
		windowSystem.PanelStateService.StartMenuWindow = window;
		windowSystem.AddWindow(window, activateWindow: true);
	}

	#region Sidebar Building

	private static void BuildSidebarItems(
		NavigationView navView, ConsoleWindowSystem windowSystem, StartMenuOptions menuOpts)
	{
		bool useIcons = menuOpts.SidebarStyle != StartMenuSidebarStyle.TextLabel;

		// "All" category — always present (order 0)
		var allItem = navView.AddItem("All", icon: useIcons ? ControlDefaults.StartMenuAllIcon : null);
		navView.SetItemContent(allItem, panel => BuildAllContent(panel, windowSystem, menuOpts));

		// "Windows" category — conditional (order 10)
		if (menuOpts.ShowWindowList)
		{
			var topLevelWindows = windowSystem.Windows.Values
				.Where(w => w.ParentWindow == null && w.ShowInTaskbar)
				.ToList();

			if (topLevelWindows.Count > 0)
			{
				var windowsItem = navView.AddItem("Windows", icon: useIcons ? ControlDefaults.StartMenuWindowsIcon : null);
				navView.SetItemContent(windowsItem, panel => BuildWindowsContent(panel, windowSystem, menuOpts));
			}
		}

		// "System" category — conditional (order 20)
		if (menuOpts.ShowSystemCategory)
		{
			var systemItem = navView.AddItem("System", icon: useIcons ? ControlDefaults.StartMenuSystemIcon : null);
			navView.SetItemContent(systemItem, panel => BuildSystemContent(panel, windowSystem, menuOpts));
		}

		// "Plugins" category — conditional (order 30)
		var pluginState = windowSystem.PluginStateService.CurrentState;
		if (pluginState.LoadedPluginCount > 0)
		{
			var pluginsItem = navView.AddItem("Plugins", icon: useIcons ? ControlDefaults.StartMenuPluginsIcon : null);
			navView.SetItemContent(pluginsItem, panel => BuildPluginsContent(panel, windowSystem, menuOpts));
		}

		// "Actions" category — conditional (order 40)
		var userActions = windowSystem.PanelStateService.GetStartMenuActions();
		if (userActions.Count > 0)
		{
			var actionsItem = navView.AddItem("Actions", icon: useIcons ? ControlDefaults.StartMenuActionsIcon : null);
			navView.SetItemContent(actionsItem, panel => BuildActionsContent(panel, windowSystem));
		}

		// Custom categories sorted by Order
		foreach (var cat in menuOpts.Categories.OrderBy(c => c.Order))
		{
			var customItem = navView.AddItem(cat.Name, icon: useIcons ? cat.Icon : null);
			navView.SetItemContent(customItem, panel => cat.ContentFactory?.Invoke(panel));
		}
	}

	#endregion

	#region Content Builders

	private static void BuildAllContent(
		ScrollablePanelControl panel, ConsoleWindowSystem windowSystem, StartMenuOptions menuOpts)
	{
		var allLists = new List<ListControl>();

		// Quick actions (system)
		if (menuOpts.ShowSystemCategory)
		{
			var quickActions = new ListControl();
			quickActions.AddItem(new ListItem("Change Theme..."));
			quickActions.AddItem(new ListItem("Settings..."));
			quickActions.AddItem(new ListItem("About..."));
			quickActions.ItemActivated += (_, item) =>
			{
				CloseStartMenu(windowSystem);
				switch (item.Text)
				{
					case "Change Theme...":
						ThemeSelectorDialog.Show(windowSystem);
						break;
					case "Settings...":
						SettingsDialog.Show(windowSystem);
						break;
					case "About...":
						AboutDialog.Show(windowSystem);
						break;
				}
			};
			StyleListControl(quickActions, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
			panel.AddControl(quickActions);
			allLists.Add(quickActions);
		}

		// Plugins
		var pluginState = windowSystem.PluginStateService.CurrentState;
		if (pluginState.LoadedPluginCount > 0)
		{
			var pluginsRule = new RuleControl { Title = "Plugins", BorderStyle = BorderStyle.Single };
			panel.AddControl(pluginsRule);

			var pluginList = new ListControl();
			AddPluginItems(pluginList, windowSystem);
			StyleListControl(pluginList, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
			panel.AddControl(pluginList);
			allLists.Add(pluginList);
		}

		// Windows
		if (menuOpts.ShowWindowList)
		{
			var topLevelWindows = windowSystem.Windows.Values
				.Where(w => w.ParentWindow == null && w.ShowInTaskbar)
				.ToList();

			if (topLevelWindows.Count > 0)
			{
				var windowsRule = new RuleControl { Title = "Windows", BorderStyle = BorderStyle.Single };
				panel.AddControl(windowsRule);

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
				StyleListControl(windowList, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
				panel.AddControl(windowList);
				allLists.Add(windowList);
			}
		}

		// User actions
		var userActions = windowSystem.PanelStateService.GetStartMenuActions();
		if (userActions.Count > 0)
		{
			var userRule = new RuleControl { Title = "User Actions", BorderStyle = BorderStyle.Single };
			panel.AddControl(userRule);

			var userList = new ListControl();
			foreach (var action in userActions.OrderBy(a => a.Order))
			{
				var callback = action.Callback;
				var listItem = new ListItem(action.Name) { Tag = callback };
				userList.AddItem(listItem);
			}
			userList.ItemActivated += (_, item) =>
			{
				if (item.Tag is Action callback)
				{
					CloseStartMenu(windowSystem);
					callback();
				}
			};
			StyleListControl(userList, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
			panel.AddControl(userList);
			allLists.Add(userList);
		}

		// Ensure only one list shows a selection at a time
		if (allLists.Count > 1)
			SetupExclusiveSelection(allLists.ToArray());
	}

	private static void BuildWindowsContent(
		ScrollablePanelControl panel, ConsoleWindowSystem windowSystem, StartMenuOptions menuOpts)
	{
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

		StyleListControl(windowList, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
		panel.AddControl(windowList);

	}

	private static void BuildSystemContent(
		ScrollablePanelControl panel, ConsoleWindowSystem windowSystem, StartMenuOptions menuOpts)
	{
		var allLists = new List<ListControl>();

		// Quick actions
		var actionsList = new ListControl();
		actionsList.AddItem(new ListItem("Change Theme..."));
		actionsList.AddItem(new ListItem("Settings..."));
		actionsList.ItemActivated += (_, item) =>
		{
			CloseStartMenu(windowSystem);
			switch (item.Text)
			{
				case "Change Theme...":
					ThemeSelectorDialog.Show(windowSystem);
					break;
				case "Settings...":
					SettingsDialog.Show(windowSystem);
					break;
			}
		};
		StyleListControl(actionsList, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
		panel.AddControl(actionsList);
		allLists.Add(actionsList);

		// Performance section
		var perfRule = new RuleControl { Title = "Performance", BorderStyle = BorderStyle.Single };
		panel.AddControl(perfRule);

		var perfList = new ListControl();
		var metricsEnabled = windowSystem.Performance.IsPerformanceMetricsEnabled;
		var frameLimitingEnabled = windowSystem.Performance.IsFrameRateLimitingEnabled;

		perfList.AddItem(new ListItem($"Toggle Metrics [{(metricsEnabled ? "ON" : "OFF")}]"));
		perfList.AddItem(new ListItem($"Toggle Frame Limiting [{(frameLimitingEnabled ? "ON" : "OFF")}]"));
		perfList.AddItem(new ListItem("Set Target FPS..."));
		perfList.ItemActivated += (_, item) =>
		{
			CloseStartMenu(windowSystem);
			if (item.Text.StartsWith("Toggle Metrics"))
			{
				windowSystem.Performance.SetPerformanceMetrics(!windowSystem.Performance.IsPerformanceMetricsEnabled);
			}
			else if (item.Text.StartsWith("Toggle Frame Limiting"))
			{
				windowSystem.Performance.SetFrameRateLimiting(!windowSystem.Performance.IsFrameRateLimitingEnabled);
			}
			else if (item.Text == "Set Target FPS...")
			{
				PerformanceDialog.Show(windowSystem);
			}
		};
		StyleListControl(perfList, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
		panel.AddControl(perfList);
		allLists.Add(perfList);

		// Info section
		var infoRule = new RuleControl { Title = "Info", BorderStyle = BorderStyle.Single };
		panel.AddControl(infoRule);

		var infoList = new ListControl();
		infoList.AddItem(new ListItem("About..."));
		infoList.ItemActivated += (_, _) =>
		{
			CloseStartMenu(windowSystem);
			AboutDialog.Show(windowSystem);
		};
		StyleListControl(infoList, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
		panel.AddControl(infoList);
		allLists.Add(infoList);

		// Ensure only one list shows a selection at a time
		if (allLists.Count > 1)
			SetupExclusiveSelection(allLists.ToArray());
	}

	private static void BuildPluginsContent(
		ScrollablePanelControl panel, ConsoleWindowSystem windowSystem, StartMenuOptions menuOpts)
	{
		var pluginList = new ListControl();
		AddPluginItems(pluginList, windowSystem);
		StyleListControl(pluginList, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
		panel.AddControl(pluginList);
	}

	private static void BuildActionsContent(
		ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
	{
		var userActions = windowSystem.PanelStateService.GetStartMenuActions();
		var actionList = new ListControl();
		foreach (var action in userActions.OrderBy(a => a.Order))
		{
			var callback = action.Callback;
			actionList.AddItem(new ListItem(action.Name) { Tag = callback });
		}
		actionList.ItemActivated += (_, item) =>
		{
			if (item.Tag is Action callback)
			{
				CloseStartMenu(windowSystem);
				callback();
			}
		};
		StyleListControl(actionList, _dropBg, _dropFg, _dropHiBg, _dropHiFg);
		panel.AddControl(actionList);
	}

	#endregion

	#region Shared Helpers

	/// <summary>
	/// Applies start menu styling to a ListControl — transparent background,
	/// theme-matched highlight colors, and proper margin for breathing room.
	/// </summary>
	private static void StyleListControl(ListControl list, Color dropBg, Color dropFg, Color dropHiBg, Color dropHiFg)
	{
		list.BackgroundColor = Color.Transparent;
		list.FocusedBackgroundColor = Color.Transparent;
		list.ForegroundColor = dropFg;
		list.FocusedForegroundColor = dropFg;
		list.HighlightBackgroundColor = dropHiBg;
		list.HighlightForegroundColor = dropHiFg;
		list.Margin = new Margin(0, 0, 0, 1); // bottom margin for spacing between sections
	}

	/// <summary>
	/// Wires up exclusive selection across sibling ListControls — when one list
	/// gains a valid selection, all others in the group have their selection cleared.
	/// </summary>
	private static void SetupExclusiveSelection(params ListControl[] lists)
	{
		foreach (var list in lists)
		{
			var capturedList = list;
			capturedList.SelectedIndexChanged += (_, index) =>
			{
				if (index < 0) return;
				foreach (var sibling in lists)
				{
					if (!ReferenceEquals(sibling, capturedList) && sibling.SelectedIndex >= 0)
						sibling.SelectedIndex = -1;
				}
			};
		}
	}

	private static void AddPluginItems(ListControl pluginList, ConsoleWindowSystem windowSystem)
	{
		foreach (var plugin in windowSystem.PluginStateService.LoadedPlugins)
		{
			var windows = plugin.GetWindows();
			var actionProviders = plugin.GetActionProviders();
			var allActions = actionProviders
				.SelectMany(p => p.GetAvailableActions().Select(a => (Provider: p, Action: a)))
				.ToList();

			if (windows.Count == 0 && allActions.Count == 0)
				continue;

			foreach (var window in windows)
			{
				var windowName = window.Name;
				pluginList.AddItem(new ListItem(windowName) { Tag = ("PluginWindow", windowName) });
			}

			foreach (var (provider, action) in allActions)
			{
				var providerName = provider.ProviderName;
				var actionName = action.Name;
				pluginList.AddItem(new ListItem(actionName) { Tag = ("PluginAction", providerName, actionName) });
			}
		}

		pluginList.ItemActivated += (_, item) =>
		{
			if (item.Tag is (string type, string name) && type == "PluginWindow")
			{
				var w = windowSystem.PluginStateService.CreateWindow(name);
				if (w != null)
				{
					windowSystem.AddWindow(w);
					windowSystem.SetActiveWindow(w);
				}
				CloseStartMenu(windowSystem);
			}
			else if (item.Tag is (string actionType, string providerName, string actionName) && actionType == "PluginAction")
			{
				windowSystem.PluginStateService.ExecutePluginAction(
					providerName, actionName, context: null);
				CloseStartMenu(windowSystem);
			}
		};
	}

	private static void PopulateWindowList(ConsoleWindowSystem windowSystem, ListControl windowList)
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

	#region Helpers

	/// <summary>
	/// Closes the start menu window if it is open.
	/// </summary>
	private static void CloseStartMenu(ConsoleWindowSystem windowSystem)
	{
		var smw = windowSystem.PanelStateService.StartMenuWindow;
		if (smw != null)
		{
			smw.Close(force: true);
			windowSystem.PanelStateService.StartMenuWindow = null;
		}
	}

	#endregion
}
