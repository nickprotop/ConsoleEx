// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Rendering;
using Spectre.Console;

namespace SharpConsoleUI.Windows
{
	/// <summary>
	/// Manages window lifecycle operations including creation, activation, closing, and flashing.
	/// Extracted from ConsoleWindowSystem as part of Phase 2.1 refactoring.
	/// </summary>
	public class WindowLifecycleManager
	{
		private readonly ILogService _logService;
		private readonly WindowStateService _windowStateService;
		private readonly ModalStateService _modalStateService;
		private readonly FocusStateService _focusStateService;
		private readonly Renderer _renderer;
		private readonly IConsoleDriver _consoleDriver;
		private readonly Func<IWindowSystemContext> _getWindowSystem;

		// Track windows currently being flashed to prevent concurrent flashes
		private readonly HashSet<Window> _flashingWindows = new();

		/// <summary>
		/// Initializes a new instance of the WindowLifecycleManager class.
		/// </summary>
		/// <param name="logService">Logging service for debug output.</param>
		/// <param name="windowStateService">Service for window registration and state management.</param>
		/// <param name="modalStateService">Service for modal window management.</param>
		/// <param name="focusStateService">Service for focus management.</param>
		/// <param name="renderer">Renderer for screen clearing operations.</param>
		/// <param name="consoleDriver">Console driver for screen size access.</param>
		/// <param name="getWindowSystem">Function to get window system context (lazy to avoid circular dependency).</param>
		public WindowLifecycleManager(
			ILogService logService,
			WindowStateService windowStateService,
			ModalStateService modalStateService,
			FocusStateService focusStateService,
			Renderer renderer,
			IConsoleDriver consoleDriver,
			Func<IWindowSystemContext> getWindowSystem)
		{
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
			_modalStateService = modalStateService ?? throw new ArgumentNullException(nameof(modalStateService));
			_focusStateService = focusStateService ?? throw new ArgumentNullException(nameof(focusStateService));
			_renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
			_consoleDriver = consoleDriver ?? throw new ArgumentNullException(nameof(consoleDriver));
			_getWindowSystem = getWindowSystem ?? throw new ArgumentNullException(nameof(getWindowSystem));
		}

		/// <summary>
		/// Adds a window to the window system and optionally activates it.
		/// </summary>
		/// <param name="window">The window to add.</param>
		/// <param name="activateWindow">Whether to activate the window after adding. Defaults to true.</param>
		/// <returns>The added window.</returns>
		public Window AddWindow(Window window, bool activateWindow = true)
		{
			var context = _getWindowSystem();

			_logService.LogDebug($"Adding window: {window.Title} (GUID: {window.Guid})", "Window");

			// Delegate to window state service for window registration
			// The service handles ZIndex assignment and adding to collection
			_windowStateService.RegisterWindow(window, activate: false);

			// Register modal windows with the modal state service
			if (window.Mode == WindowMode.Modal)
			{
				_modalStateService.PushModal(window, window.ParentWindow);
				_logService.LogDebug($"Modal window pushed: {window.Title}", "Modal");
			}

			// Activate the window if needed (through SetActiveWindow for modal logic)
			if (context.ActiveWindow == null || activateWindow)
			{
				context.SetActiveWindow(window);
			}

			window.WindowIsAdded();

			_logService.LogDebug($"Window added successfully: {window.Title}", "Window");
			return window;
		}

		/// <summary>
		/// Closes a modal window and optionally activates its parent window.
		/// </summary>
		/// <param name="modalWindow">The modal window to close. If null or not a modal window, the method returns without action.</param>
		public void CloseModalWindow(Window? modalWindow)
		{
			if (modalWindow == null || modalWindow.Mode != WindowMode.Modal)
				return;

			var context = _getWindowSystem();

			_logService.LogDebug($"Closing modal window: {modalWindow.Title}", "Modal");

			// Store the parent window before closing
			Window? parentWindow = modalWindow.ParentWindow;

			// Close the modal window
			if (CloseWindow(modalWindow))
			{
				// If we have a parent window, ensure it becomes active
				if (parentWindow != null && context.Windows.ContainsKey(parentWindow.Guid))
				{
					context.SetActiveWindow(parentWindow);
				}
			}
		}

		/// <summary>
		/// Closes a window and removes it from the window system.
		/// </summary>
		/// <param name="window">The window to close. If null or not in the system, returns false.</param>
		/// <param name="activateParent">Whether to activate the parent window after closing. Defaults to true.</param>
		/// <param name="force">If true, forces the window to close even if IsClosable is false or OnClosing cancels.</param>
		/// <returns>True if the window was closed successfully; false otherwise.</returns>
		public bool CloseWindow(Window? window, bool activateParent = true, bool force = false)
		{
			if (window == null) return false;

			var context = _getWindowSystem();
			if (!context.Windows.ContainsKey(window.Guid)) return false;

			_logService.LogDebug($"Closing window: {window.Title} (GUID: {window.Guid}, Force: {force})", "Window");

			// STEP 1: Check if close is allowed BEFORE any state changes
			// This fires OnClosing and respects IsClosable (unless forced)
			if (!window.TryClose(force))
			{
				_logService.LogDebug($"Window close cancelled by OnClosing handler: {window.Title}", "Window");
				return false;
			}

			// STEP 2: Close is allowed - now safe to remove from system
			Window? parentWindow = window.ParentWindow;
			bool wasActive = (window == context.ActiveWindow);

			// Unregister modal window from modal state service
			if (window.Mode == WindowMode.Modal)
			{
				_modalStateService.PopModal(window);
			}

			// Clear focus state for this window
			_focusStateService.ClearFocus(window);

			// Remove from window collection via service
			// This prevents race condition where render thread tries to render disposed controls
			_windowStateService.UnregisterWindow(window);

			// Activate the next window (UnregisterWindow updates state but doesn't call SetIsActive)
			if (wasActive)
			{
				Window? targetWindow = null;

				if (activateParent && parentWindow != null && context.Windows.ContainsKey(parentWindow.Guid))
				{
					// Let ModalStateService resolve correct target (may redirect to modal child)
					// This prevents the "black hole" bug when closing a modal that has other modals stacked
					targetWindow = _modalStateService.GetEffectiveActivationTarget(parentWindow);
				}
				else if (context.Windows.Count > 0)
				{
					// Activate window with highest Z-Index
					int maxZIndex = context.Windows.Values.Max(w => w.ZIndex);
					var nextWindow = context.Windows.Values.FirstOrDefault(w => w.ZIndex == maxZIndex);

					if (nextWindow != null)
					{
						// Use GetEffectiveActivationTarget to handle modal redirection
						targetWindow = _modalStateService.GetEffectiveActivationTarget(nextWindow);
					}
				}

				if (targetWindow != null)
				{
					context.SetActiveWindow(targetWindow);
				}
			}

			// STEP 3: Complete the close (fire OnClosed, dispose controls)
			window.CompleteClose();

			// Redraw the screen
			var theme = context.Theme;
			_renderer.FillRect(0, 0, _consoleDriver.ScreenSize.Width, _consoleDriver.ScreenSize.Height,
							  theme.DesktopBackgroundChar, theme.DesktopBackgroundColor, theme.DesktopForegroundColor);
			foreach (var w in context.Windows.Values)
			{
				w.Invalidate(true);
			}

			return true;
		}

		/// <summary>
		/// Flashes a window to draw user attention by briefly changing its background color.
		/// </summary>
		/// <param name="window">The window to flash. If null, the method returns without action.</param>
		/// <param name="flashCount">The number of times to flash. Defaults to 1.</param>
		/// <param name="flashDuration">The duration of each flash in milliseconds. Defaults to 150.</param>
		/// <param name="flashBackgroundColor">The background color to use for flashing. If null, uses the theme's ModalFlashColor.</param>
		public void FlashWindow(Window? window, int flashCount = 1, int flashDuration = 150, Color? flashBackgroundColor = null)
		{
			if (window == null) return;

			var context = _getWindowSystem();

			// Prevent multiple concurrent flashes on the same window
			lock (_flashingWindows)
			{
				if (_flashingWindows.Contains(window)) return;
				_flashingWindows.Add(window);
			}

			var originalBackgroundColor = window.BackgroundColor;
			var flashColor = flashBackgroundColor ?? context.Theme.ModalFlashColor;

			// Use ThreadPool with synchronous sleep for reliable timing
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					for (int i = 0; i < flashCount; i++)
					{
						window.BackgroundColor = flashColor;
						window.Invalidate(true);
						Thread.Sleep(flashDuration);

						window.BackgroundColor = originalBackgroundColor;
						window.Invalidate(true);

						// Only delay between flashes, not after the last one
						if (i < flashCount - 1)
						{
							Thread.Sleep(flashDuration);
						}
					}
				}
				finally
				{
					// Always remove from tracking to allow future flashes
					lock (_flashingWindows)
					{
						_flashingWindows.Remove(window);
					}
				}
			});
		}

		/// <summary>
		/// Activates the next non-minimized window after a window is minimized.
		/// </summary>
		/// <param name="minimizedWindow">The window that was just minimized.</param>
		public void ActivateNextNonMinimizedWindow(Window minimizedWindow)
		{
			if (minimizedWindow == null) return;

			var context = _getWindowSystem();

			// Find the next non-minimized window to activate
			var nextWindow = context.Windows.Values
				.Where(w => w != minimizedWindow && w.State != WindowState.Minimized)
				.OrderByDescending(w => w.ZIndex)
				.FirstOrDefault();

			if (nextWindow != null)
			{
				context.SetActiveWindow(nextWindow);
			}
		}

		/// <summary>
		/// Deactivates the current active window (e.g., when clicking on empty desktop).
		/// </summary>
		public void DeactivateCurrentWindow()
		{
			var context = _getWindowSystem();

			if (context.ActiveWindow != null)
			{
				context.ActiveWindow.SetIsActive(false);
				context.ActiveWindow.Invalidate(true);
			}
		}
	}
}
