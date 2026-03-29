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
	/// Manages panel state, Start menu actions, and panel visibility.
	/// Primary state service for all panel and start menu functionality,
	/// replacing StatusBarStateService as the canonical owner.
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

		// Legacy status text (backward compat)
		private string _topStatus = "";
		private string _bottomStatus = "";

		// Visibility
		private bool _showTopPanel = true;
		private bool _showBottomPanel = true;

		// Dirty tracking
		private bool _isDirty;

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
		/// Gets whether any panel property has changed since the last render.
		/// </summary>
		public bool IsDirty => _isDirty;

		/// <summary>
		/// Clears the dirty flag. Called by RenderCoordinator after rendering panels.
		/// </summary>
		public void ClearDirty() => _isDirty = false;

		/// <summary>
		/// Marks panels as dirty, forcing a re-render on the next frame.
		/// </summary>
		public void MarkDirty() => _isDirty = true;

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
					_isDirty = true;
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
					_isDirty = true;
					if (_bottomPanel != null)
						_bottomPanel.Visible = value;
					var ws = _getWindowSystem();
					ws.Render.InvalidateAllWindows();
				}
			}
		}

		#endregion

		#region Legacy Text Support

		/// <summary>
		/// Gets or sets the text displayed in the top status area.
		/// Updates the legacyTopStatus StatusTextElement if present, or stores for legacy rendering.
		/// </summary>
		public string TopStatus
		{
			get => _topStatus;
			set
			{
				var newValue = value ?? "";
				if (_topStatus != newValue)
				{
					_topStatus = newValue;
					_isDirty = true;
				}
			}
		}

		/// <summary>
		/// Gets or sets the text displayed in the bottom status area.
		/// Updates the legacyBottomStatus StatusTextElement if present, or stores for legacy rendering.
		/// </summary>
		public string BottomStatus
		{
			get => _bottomStatus;
			set
			{
				var newValue = value ?? "";
				if (_bottomStatus != newValue)
				{
					_bottomStatus = newValue;
					_isDirty = true;
				}
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
		/// Initializes panels from configuration options or legacy StatusBarOptions.
		/// Moved from ConsoleWindowSystem.InitializePanels().
		/// </summary>
		/// <param name="options">The window system configuration options.</param>
		public void InitializePanels(ConsoleWindowSystemOptions options)
		{
			var ws = _getWindowSystem();

			// Sync visibility from legacy StatusBarOptions
			var statusBar = options.StatusBar;
			_showTopPanel = statusBar.ShowTopStatus;
			_showBottomPanel = statusBar.ShowBottomStatus;

			if (options.TopPanelConfig != null)
			{
				var builder = options.TopPanelConfig(new Panel.PanelBuilder());
				_topPanel = builder.Build();
				_topPanel.WindowSystem = ws;
				_topPanel.Visible = _showTopPanel;
			}

			if (options.BottomPanelConfig != null)
			{
				var builder = options.BottomPanelConfig(new Panel.PanelBuilder());
				_bottomPanel = builder.Build();
				_bottomPanel.WindowSystem = ws;
				_bottomPanel.Visible = _showBottomPanel;
			}

			// Legacy fallback: if no panel builders but StatusBarOptions has legacy config
			if (options.TopPanelConfig == null && options.BottomPanelConfig == null)
			{

				// Build top panel from legacy config
				if (statusBar.ShowTopStatus)
				{
					var topBuilder = new Panel.PanelBuilder();
					var topStatusElement = new Panel.StatusTextElement(_topStatus ?? string.Empty, "legacyTopStatus");
					topBuilder.Left(topStatusElement);

					if (options.EnablePerformanceMetrics)
					{
						topBuilder.Right(new Panel.PerformanceElement("legacyPerf"));
					}

					if (statusBar.ShowStartButton && statusBar.StartButtonLocation == StatusBarLocation.Top)
					{
						var startElement = new Panel.StartMenuElement("legacyStartTop") { Text = statusBar.StartButtonText };
						if (statusBar.StartButtonPosition == StartButtonPosition.Left)
							topBuilder.Left(startElement);
						else
							topBuilder.Right(startElement);
					}

					_topPanel = topBuilder.Build();
					_topPanel.WindowSystem = ws;
				}

				// Build bottom panel from legacy config
				if (statusBar.ShowBottomStatus)
				{
					var bottomBuilder = new Panel.PanelBuilder();
					bool hasContent = false;

					if (statusBar.ShowStartButton && statusBar.StartButtonLocation == StatusBarLocation.Bottom)
					{
						var startElement = new Panel.StartMenuElement("legacyStartBottom") { Text = statusBar.StartButtonText };
						if (statusBar.StartButtonPosition == StartButtonPosition.Left)
							bottomBuilder.Left(startElement);
						else
							bottomBuilder.Right(startElement);
						hasContent = true;
					}

					if (statusBar.ShowTaskBar)
					{
						bottomBuilder.Center(new Panel.TaskBarElement("legacyTaskbar"));
						hasContent = true;
					}

					if (hasContent)
					{
						_bottomPanel = bottomBuilder.Build();
						_bottomPanel.WindowSystem = ws;
					}
				}
			}
		}

		#endregion
	}
}
