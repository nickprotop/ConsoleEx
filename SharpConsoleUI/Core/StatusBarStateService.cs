// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Models;
using System.Drawing;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Manages status bar state, Start menu actions, and status bar bounds.
	/// Centralized state service for all status bar and start menu functionality.
	/// </summary>
	[Obsolete("Use PanelStateService instead. This type will be removed in a future version.")]
	public class StatusBarStateService
	{
		private readonly PanelStateService _panelStateService;
		private readonly ILogService _logService;
		private readonly Func<ConsoleWindowSystem> _getWindowSystem;

		/// <summary>
		/// Initializes a new instance of the StatusBarStateService class.
		/// </summary>
		/// <param name="panelStateService">The panel state service to delegate to.</param>
		/// <param name="logService">Service for debug logging.</param>
		/// <param name="getWindowSystem">Function to get the window system (lazy to avoid circular dependency).</param>
		public StatusBarStateService(PanelStateService panelStateService, ILogService logService, Func<ConsoleWindowSystem> getWindowSystem)
		{
			_panelStateService = panelStateService ?? throw new ArgumentNullException(nameof(panelStateService));
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_getWindowSystem = getWindowSystem ?? throw new ArgumentNullException(nameof(getWindowSystem));
		}

		/// <summary>
		/// Gets whether any status bar property has changed since the last render.
		/// </summary>
		public bool IsDirty => _panelStateService.IsDirty;

		/// <summary>
		/// Clears the dirty flag. Called by RenderCoordinator after rendering status bars.
		/// </summary>
		public void ClearDirty() => _panelStateService.ClearDirty();

		#region Properties

		/// <summary>
		/// Gets or sets the text displayed in the top status bar.
		/// </summary>
		public string TopStatus
		{
			get => _panelStateService.TopStatus;
			set => _panelStateService.TopStatus = value;
		}

		/// <summary>
		/// Gets or sets the text displayed in the bottom status bar.
		/// </summary>
		public string BottomStatus
		{
			get => _panelStateService.BottomStatus;
			set => _panelStateService.BottomStatus = value;
		}

		/// <summary>
		/// Gets or sets whether the top status bar is visible.
		/// Changing this affects desktop dimensions and triggers window invalidation.
		/// </summary>
		public bool ShowTopStatus
		{
			get => _panelStateService.ShowTopPanel;
			set => _panelStateService.ShowTopPanel = value;
		}

		/// <summary>
		/// Gets or sets whether the bottom status bar is visible.
		/// Changing this affects desktop dimensions and triggers window invalidation.
		/// </summary>
		public bool ShowBottomStatus
		{
			get => _panelStateService.ShowBottomPanel;
			set => _panelStateService.ShowBottomPanel = value;
		}

		/// <summary>
		/// Gets the top status bar bounds for mouse hit testing.
		/// Returns empty rectangle — panels handle bounds internally.
		/// </summary>
		public Rectangle TopStatusBarBounds => Rectangle.Empty;

		/// <summary>
		/// Gets the bottom status bar bounds for mouse hit testing.
		/// Returns empty rectangle — panels handle bounds internally.
		/// </summary>
		public Rectangle BottomStatusBarBounds => Rectangle.Empty;

		/// <summary>
		/// Gets the start button bounds for mouse hit testing.
		/// Returns empty rectangle — panels handle bounds internally.
		/// </summary>
		public Rectangle StartButtonBounds => Rectangle.Empty;

		#endregion

		#region Status Bar Height Calculation

		/// <summary>
		/// Gets the height occupied by the top status bar (0 or 1).
		/// Delegates to panel height if available.
		/// </summary>
		/// <param name="showTopStatus">Whether the top status bar is enabled.</param>
		/// <param name="enablePerformanceMetrics">Whether performance metrics are enabled.</param>
		/// <returns>Height in rows (0 or 1).</returns>
		public int GetTopStatusHeight(bool showTopStatus, bool enablePerformanceMetrics)
		{
			var topPanel = _panelStateService.TopPanel;
			if (topPanel != null)
				return topPanel.Height;
			return showTopStatus && (!string.IsNullOrEmpty(_panelStateService.TopStatus) || enablePerformanceMetrics) ? 1 : 0;
		}

		/// <summary>
		/// Gets the height occupied by the bottom status bar (0 or 1).
		/// Delegates to panel height if available.
		/// </summary>
		/// <param name="showBottomStatus">Whether the bottom status bar is enabled.</param>
		/// <param name="showTaskBar">Whether the task bar (window list) is enabled.</param>
		/// <param name="showStartButton">Whether the start button is enabled.</param>
		/// <param name="startButtonLocation">Location of the start button.</param>
		/// <returns>Height in rows (0 or 1).</returns>
		public int GetBottomStatusHeight(bool showBottomStatus, bool showTaskBar, bool showStartButton, StatusBarLocation startButtonLocation)
		{
			var bottomPanel = _panelStateService.BottomPanel;
			if (bottomPanel != null)
				return bottomPanel.Height;

			bool hasContent = !string.IsNullOrEmpty(_panelStateService.BottomStatus) || showTaskBar;
			bool hasStartButton = showStartButton && startButtonLocation == StatusBarLocation.Bottom;
			return showBottomStatus && (hasContent || hasStartButton) ? 1 : 0;
		}

		#endregion

		#region Status Bar Bounds Management

		/// <summary>
		/// Updates the status bar bounds. No-op — panels handle bounds internally.
		/// </summary>
		/// <param name="screenWidth">Current screen width.</param>
		/// <param name="screenHeight">Current screen height.</param>
		/// <param name="showTopStatus">Whether the top status bar is enabled.</param>
		/// <param name="showBottomStatus">Whether the bottom status bar is enabled.</param>
		/// <param name="options">Status bar configuration options.</param>
		public void UpdateStatusBarBounds(int screenWidth, int screenHeight, bool showTopStatus, bool showBottomStatus, StatusBarOptions options)
		{
			// No-op — panels handle bounds internally
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
			_panelStateService.RegisterStartMenuAction(name, callback, category, order);
		}

		/// <summary>
		/// Removes an action from the Start menu by name.
		/// </summary>
		/// <param name="name">Name of the action to remove.</param>
		public void UnregisterStartMenuAction(string name)
		{
			_panelStateService.UnregisterStartMenuAction(name);
		}

		/// <summary>
		/// Gets all registered Start menu actions.
		/// </summary>
		/// <returns>Read-only list of actions.</returns>
		public IReadOnlyList<StartMenuAction> GetStartMenuActions() => _panelStateService.GetStartMenuActions();

		#endregion

		#region Start Menu Display

		/// <summary>
		/// Gets or sets the currently open Start menu window, if any.
		/// Used for toggle behavior — if non-null, the Start menu is open.
		/// </summary>
		internal Window? StartMenuWindow
		{
			get => _panelStateService.StartMenuWindow;
			set => _panelStateService.StartMenuWindow = value;
		}

		/// <summary>
		/// Shows the Start menu dialog.
		/// </summary>
		public void ShowStartMenu()
		{
			_panelStateService.ShowStartMenu();
		}

		#endregion

		#region Mouse Click Handling

		/// <summary>
		/// Optional handler invoked when the top status bar area is clicked.
		/// Receives the raw screen X coordinate of the click.
		/// </summary>
		public Action<int>? TopStatusClickHandler { get; set; }

		/// <summary>
		/// Optional handler invoked when the bottom status bar area (excluding the Start button) is clicked.
		/// Receives the raw screen X coordinate of the click.
		/// </summary>
		public Action<int>? BottomStatusClickHandler { get; set; }

		/// <summary>
		/// Handles status bar mouse click. Delegates to panels if available, otherwise returns false.
		/// </summary>
		/// <param name="x">X coordinate of click.</param>
		/// <param name="y">Y coordinate of click.</param>
		/// <returns>True if the click was handled; false otherwise.</returns>
		public bool HandleStatusBarClick(int x, int y)
		{
			// Panels handle all clicks now — this is a no-op fallback
			return false;
		}

		#endregion
	}
}
