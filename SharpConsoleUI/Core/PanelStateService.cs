// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Models;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Manages panel state, start menu actions, and panel visibility.
	/// </summary>
	public class PanelStateService
	{
		private readonly ILogService _logService;
		private readonly Func<ConsoleWindowSystem> _getWindowSystem;
		private readonly List<StartMenuAction> _startMenuActions = new();

		// Start menu window tracking
		private Window? _startMenuWindow;

		// Panel references
		private Panel.Panel? _topPanel;
		private Panel.Panel? _bottomPanel;

		// Visibility
		private bool _showTopPanel = true;
		private bool _showBottomPanel = true;

		// Start menu configuration
		private StartMenuOptions _startMenuOptions = new();
		private ConsoleKey _startMenuShortcutKey = ConsoleKey.Spacebar;
		private ConsoleModifiers _startMenuShortcutModifiers = ConsoleModifiers.Control;

		/// <summary>
		/// Initializes a new instance of the PanelStateService class.
		/// </summary>
		/// <param name="logService">Service for debug logging.</param>
		/// <param name="getWindowSystem">Function to get the window system (lazy to avoid circular dependency).</param>
		public PanelStateService(ILogService logService, Func<ConsoleWindowSystem> getWindowSystem)
		{
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_getWindowSystem = getWindowSystem ?? throw new ArgumentNullException(nameof(getWindowSystem));
		}

		#region Panel Ownership

		/// <summary>
		/// Gets the top panel (desktop bar) if configured, or null.
		/// </summary>
		public Panel.Panel? TopPanel => _topPanel;

		/// <summary>
		/// Gets the bottom panel (desktop bar) if configured, or null.
		/// </summary>
		public Panel.Panel? BottomPanel => _bottomPanel;

		/// <summary>
		/// Gets whether any panel needs to be redrawn.
		/// </summary>
		public bool IsDirty =>
			(_topPanel?.IsDirty ?? false)
			|| (_bottomPanel?.IsDirty ?? false);

		/// <summary>
		/// Gets the start menu options configuration.
		/// </summary>
		public StartMenuOptions StartMenuOptions => _startMenuOptions;

		/// <summary>
		/// Gets the shortcut key for toggling the start menu.
		/// </summary>
		public ConsoleKey StartMenuShortcutKey => _startMenuShortcutKey;

		/// <summary>
		/// Gets the shortcut modifier keys for toggling the start menu.
		/// </summary>
		public ConsoleModifiers StartMenuShortcutModifiers => _startMenuShortcutModifiers;

		/// <summary>
		/// Gets whether any panel contains a StartMenuElement.
		/// </summary>
		public bool HasStartMenu =>
			(_topPanel?.HasElement<Panel.StartMenuElement>() ?? false) ||
			(_bottomPanel?.HasElement<Panel.StartMenuElement>() ?? false);

		/// <summary>
		/// Marks both panels as dirty, forcing a re-render on the next frame.
		/// </summary>
		public void MarkDirty()
		{
			_topPanel?.MarkDirty();
			_bottomPanel?.MarkDirty();
		}

		#endregion

		#region Visibility

		/// <summary>
		/// Gets or sets whether the top panel is visible.
		/// Changing this affects desktop dimensions and triggers window invalidation.
		/// </summary>
		public bool ShowTopPanel
		{
			get => _showTopPanel;
			set
			{
				if (_showTopPanel != value)
				{
					_showTopPanel = value;
					if (_topPanel != null)
						_topPanel.Visible = value;
					var ws = _getWindowSystem();
					ws.Render.InvalidateAllWindows();
				}
			}
		}

		/// <summary>
		/// Gets or sets whether the bottom panel is visible.
		/// Changing this affects desktop dimensions and triggers window invalidation.
		/// </summary>
		public bool ShowBottomPanel
		{
			get => _showBottomPanel;
			set
			{
				if (_showBottomPanel != value)
				{
					_showBottomPanel = value;
					if (_bottomPanel != null)
						_bottomPanel.Visible = value;
					var ws = _getWindowSystem();
					ws.Render.InvalidateAllWindows();
				}
			}
		}

		#endregion

		#region Status Text (convenience)

		/// <summary>
		/// Sets the text of the first StatusTextElement in the top panel.
		/// Convenience shorthand — equivalent to finding the element and setting .Text directly.
		/// </summary>
		public string TopStatus
		{
			set
			{
				if (_topPanel?.FindElement<Panel.StatusTextElement>("statustext") is { } el)
					el.Text = value ?? "";
			}
		}

		/// <summary>
		/// Sets the text of the first StatusTextElement in the bottom panel.
		/// Convenience shorthand — equivalent to finding the element and setting .Text directly.
		/// </summary>
		public string BottomStatus
		{
			set
			{
				if (_bottomPanel?.FindElement<Panel.StatusTextElement>("statustext") is { } el)
					el.Text = value ?? "";
			}
		}

		#endregion

		#region Start Menu Actions

		/// <summary>
		/// Registers a new action in the Start menu.
		/// </summary>
		/// <param name="name">Display name of the action.</param>
		/// <param name="callback">Callback to execute when action is selected.</param>
		/// <param name="category">Optional category for grouping actions.</param>
		/// <param name="order">Display order (lower values appear first).</param>
		public void RegisterStartMenuAction(string name, Action callback, string? category = null, int order = 0)
		{
			_logService.LogDebug($"Registering Start menu action: {name}", category: "StartMenu");
			var action = new StartMenuAction(name, callback, category, order);
			_startMenuActions.Add(action);
		}

		/// <summary>
		/// Removes an action from the Start menu by name.
		/// </summary>
		/// <param name="name">Name of the action to remove.</param>
		public void UnregisterStartMenuAction(string name)
		{
			_logService.LogDebug($"Unregistering Start menu action: {name}", category: "StartMenu");
			_startMenuActions.RemoveAll(a => a.Name == name);
		}

		/// <summary>
		/// Gets all registered Start menu actions.
		/// </summary>
		/// <returns>Read-only list of actions.</returns>
		public IReadOnlyList<StartMenuAction> GetStartMenuActions() => _startMenuActions.AsReadOnly();

		#endregion

		#region Start Menu Display

		/// <summary>
		/// Gets or sets the currently open Start menu window, if any.
		/// Used for toggle behavior — if non-null, the Start menu is open.
		/// </summary>
		internal Window? StartMenuWindow
		{
			get => _startMenuWindow;
			set => _startMenuWindow = value;
		}

		/// <summary>
		/// Gets the screen bounds and panel location of the start menu element.
		/// Returns null if no start menu element exists in any panel.
		/// </summary>
		internal (System.Drawing.Rectangle bounds, bool isBottom)? GetStartMenuBounds()
		{
			var ws = _getWindowSystem();
			var screenHeight = ws.DesktopDimensions.Height
				+ (_topPanel?.Height ?? 0)
				+ (_bottomPanel?.Height ?? 0);

			if (_bottomPanel != null)
			{
				var b = _bottomPanel.GetElementBounds<Panel.StartMenuElement>();
				if (b.HasValue)
					return (new System.Drawing.Rectangle(b.Value.x, screenHeight - 1, b.Value.width, 1), true);
			}
			if (_topPanel != null)
			{
				var b = _topPanel.GetElementBounds<Panel.StartMenuElement>();
				if (b.HasValue)
					return (new System.Drawing.Rectangle(b.Value.x, 0, b.Value.width, 1), false);
			}
			return null;
		}

		/// <summary>
		/// Shows the Start menu dialog.
		/// </summary>
		public void ShowStartMenu()
		{
			_logService.LogDebug("Showing Start menu", category: "StartMenu");
			var windowSystem = _getWindowSystem();

			if (windowSystem is ConsoleWindowSystem consoleWindowSystem)
			{
				StartMenuDialog.Show(consoleWindowSystem);
			}
			else
			{
				_logService.LogWarning("Cannot show Start menu: window system is not ConsoleWindowSystem", category: "StartMenu");
			}
		}

		#endregion

		#region Panel Initialization

		/// <summary>
		/// Initializes panels from configuration options.
		/// User-supplied config replaces the default panel entirely.
		/// When no config is provided, sensible defaults are created.
		/// </summary>
		/// <param name="options">The window system configuration options.</param>
		public void InitializePanels(ConsoleWindowSystemOptions options)
		{
			var ws = _getWindowSystem();

			// Apply start menu config
			if (options.StartMenu != null)
				_startMenuOptions = options.StartMenu;
			_startMenuShortcutKey = options.StartMenuShortcutKey;
			_startMenuShortcutModifiers = options.StartMenuShortcutModifiers;

			// Top panel: user config or default (status text + clock)
			if (options.TopPanelConfig != null)
			{
				var builder = options.TopPanelConfig(new Panel.PanelBuilder());
				_topPanel = builder.Build();
			}
			else
			{
				var appName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "SharpConsoleUI";
				_topPanel = new Panel.PanelBuilder()
					.Left(new Panel.StatusTextElement($"[bold]{appName}[/]"))
					.Right(new Panel.ClockElement { Format = "HH:mm:ss" })
					.Build();
			}
			_topPanel.WindowSystem = ws;

			// Bottom panel: user config or default (status text + task bar)
			if (options.BottomPanelConfig != null)
			{
				var builder = options.BottomPanelConfig(new Panel.PanelBuilder());
				_bottomPanel = builder.Build();
			}
			else
			{
				_bottomPanel = new Panel.PanelBuilder()
					.Left(new Panel.StatusTextElement(""))
					.Center(new Panel.TaskBarElement())
					.Build();
			}
			_bottomPanel.WindowSystem = ws;

			// Apply initial visibility from options
			_showTopPanel = options.ShowTopPanel;
			_showBottomPanel = options.ShowBottomPanel;
			_topPanel.Visible = _showTopPanel;
			_bottomPanel.Visible = _showBottomPanel;
		}

		#endregion
	}
}
