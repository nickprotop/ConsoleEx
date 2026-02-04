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
using System.Drawing;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Manages status bar state, Start menu actions, and status bar bounds.
	/// Centralized state service for all status bar and start menu functionality.
	/// </summary>
	public class StatusBarStateService
	{
		private readonly ILogService _logService;
		private readonly Func<IWindowSystemContext> _getWindowSystem;
		private readonly List<StartMenuAction> _startMenuActions = new();

		// Status bar state
		private string _topStatus = "";
		private string _bottomStatus = "";
	private bool _showTopStatus = true;
	private bool _showBottomStatus = true;

		// Status bar bounds (updated during rendering)
		private Rectangle _topStatusBarBounds = Rectangle.Empty;
		private Rectangle _bottomStatusBarBounds = Rectangle.Empty;
		private Rectangle _startButtonBounds = Rectangle.Empty;

		/// <summary>
		/// Initializes a new instance of the StatusBarStateService class.
		/// </summary>
		/// <param name="logService">Service for debug logging.</param>
		/// <param name="getWindowSystem">Function to get the window system (lazy to avoid circular dependency).</param>
		public StatusBarStateService(ILogService logService, Func<IWindowSystemContext> getWindowSystem)
		{
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_getWindowSystem = getWindowSystem ?? throw new ArgumentNullException(nameof(getWindowSystem));
		}

		#region Properties

		/// <summary>
		/// Gets or sets the text displayed in the top status bar.
		/// </summary>
		public string TopStatus
		{
			get => _topStatus;
			set => _topStatus = value ?? "";
		}

		/// <summary>
		/// Gets or sets the text displayed in the bottom status bar.
		/// </summary>
		public string BottomStatus
		{
			get => _bottomStatus;
			set => _bottomStatus = value ?? "";
		}

	/// <summary>
	/// Gets or sets whether the top status bar is visible.
	/// Changing this affects desktop dimensions and triggers window invalidation.
	/// </summary>
	public bool ShowTopStatus
	{
		get => _showTopStatus;
		set
		{
			if (_showTopStatus != value)
			{
				_showTopStatus = value;
				_getWindowSystem().Render.InvalidateAllWindows();
			}
		}
	}

	/// <summary>
	/// Gets or sets whether the bottom status bar is visible.
	/// Changing this affects desktop dimensions and triggers window invalidation.
	/// </summary>
	public bool ShowBottomStatus
	{
		get => _showBottomStatus;
		set
		{
			if (_showBottomStatus != value)
			{
				_showBottomStatus = value;
				_getWindowSystem().Render.InvalidateAllWindows();
			}
		}
	}

		/// <summary>
		/// Gets the top status bar bounds for mouse hit testing.
		/// </summary>
		public Rectangle TopStatusBarBounds => _topStatusBarBounds;

		/// <summary>
		/// Gets the bottom status bar bounds for mouse hit testing.
		/// </summary>
		public Rectangle BottomStatusBarBounds => _bottomStatusBarBounds;

		/// <summary>
		/// Gets the start button bounds for mouse hit testing.
		/// </summary>
		public Rectangle StartButtonBounds => _startButtonBounds;

		#endregion

		#region Status Bar Height Calculation

		/// <summary>
		/// Gets the height occupied by the top status bar (0 or 1).
		/// Accounts for both status text and performance metrics.
		/// </summary>
		/// <param name="showTopStatus">Whether the top status bar is enabled.</param>
		/// <param name="enablePerformanceMetrics">Whether performance metrics are enabled.</param>
		/// <returns>Height in rows (0 or 1).</returns>
		public int GetTopStatusHeight(bool showTopStatus, bool enablePerformanceMetrics)
		{
			return showTopStatus && (!string.IsNullOrEmpty(_topStatus) || enablePerformanceMetrics) ? 1 : 0;
		}

		/// <summary>
		/// Gets the height occupied by the bottom status bar (0 or 1).
		/// Accounts for both status text and Start button.
		/// </summary>
		/// <param name="showBottomStatus">Whether the bottom status bar is enabled.</param>
		/// <param name="showTaskBar">Whether the task bar (window list) is enabled.</param>
		/// <param name="showStartButton">Whether the start button is enabled.</param>
		/// <param name="startButtonLocation">Location of the start button.</param>
		/// <returns>Height in rows (0 or 1).</returns>
		public int GetBottomStatusHeight(bool showBottomStatus, bool showTaskBar, bool showStartButton, StatusBarLocation startButtonLocation)
		{
			bool hasContent = !string.IsNullOrEmpty(_bottomStatus) || showTaskBar;
			bool hasStartButton = showStartButton && startButtonLocation == StatusBarLocation.Bottom;

			return showBottomStatus && (hasContent || hasStartButton) ? 1 : 0;
		}

		#endregion

		#region Status Bar Bounds Management

		/// <summary>
		/// Updates the status bar bounds based on current screen size and configuration.
		/// Call this after screen resizes or configuration changes.
		/// </summary>
		/// <param name="screenWidth">Current screen width.</param>
		/// <param name="screenHeight">Current screen height.</param>
		/// <param name="showTopStatus">Whether the top status bar is enabled.</param>
		/// <param name="showBottomStatus">Whether the bottom status bar is enabled.</param>
		/// <param name="options">Status bar configuration options.</param>
		public void UpdateStatusBarBounds(int screenWidth, int screenHeight, bool showTopStatus, bool showBottomStatus, StatusBarOptions options)
		{
			if (showTopStatus)
				_topStatusBarBounds = new Rectangle(0, 0, screenWidth, 1);

			if (showBottomStatus)
				_bottomStatusBarBounds = new Rectangle(0, screenHeight - 1, screenWidth, 1);

			if (options.ShowStartButton)
			{
				int y = options.StartButtonLocation == StatusBarLocation.Top
					? 0
					: (screenHeight - 1);

				int x;
				int width = Helpers.AnsiConsoleHelper.StripSpectreLength(options.StartButtonText) + 1;

				if (options.StartButtonPosition == StartButtonPosition.Left)
				{
					x = 0;
				}
				else
				{
					x = screenWidth - width;
				}

				_startButtonBounds = new Rectangle(x, y, width, 1);
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
		/// Shows the Start menu dialog.
		/// </summary>
		public void ShowStartMenu()
		{
			_logService.LogDebug("Showing Start menu", category: "StartMenu");
			var windowSystem = _getWindowSystem();

			// Access window system as ConsoleWindowSystem for dialog
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

		#region Mouse Click Handling

		/// <summary>
		/// Handles status bar mouse click (e.g., start button).
		/// </summary>
		/// <param name="x">X coordinate of click.</param>
		/// <param name="y">Y coordinate of click.</param>
		/// <returns>True if the click was handled; false otherwise.</returns>
		public bool HandleStatusBarClick(int x, int y)
		{
			// Check if click is on Start button
			if (_startButtonBounds.Contains(x, y))
			{
				_logService.LogDebug($"Start button clicked at ({x}, {y})", category: "StartMenu");
				ShowStartMenu();
				return true;
			}

			return false;
		}

		#endregion
	}
}
