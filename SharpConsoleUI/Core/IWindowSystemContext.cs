// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Models;
using SharpConsoleUI.Themes;
using SharpConsoleUI.Windows;
using System.Drawing;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Interface for window system context operations.
	/// Provides access to window management operations without exposing the entire ConsoleWindowSystem.
	/// Used by coordinator classes (InputCoordinator, RenderCoordinator, etc.) to break circular dependencies.
	/// </summary>
	public interface IWindowSystemContext
	{
		/// <summary>
		/// Gets the currently active window, or null if no window is active.
		/// </summary>
		Window? ActiveWindow { get; }

		/// <summary>
		/// Gets the desktop dimensions (content area excluding status bars).
		/// </summary>
		Size DesktopDimensions { get; }

		/// <summary>
		/// Cycles to the next active window (Ctrl+T handler).
		/// </summary>
		void CycleActiveWindow();

		/// <summary>
		/// Adds a window to the window system.
		/// </summary>
		/// <param name="window">The window to add.</param>
		/// <param name="activateWindow">Whether to activate the window after adding.</param>
		/// <returns>The added window.</returns>
		Window AddWindow(Window window, bool activateWindow = true);

		/// <summary>
		/// Sets the specified window as the active window.
		/// </summary>
		/// <param name="window">The window to activate.</param>
		void SetActiveWindow(Window window);

		/// <summary>
		/// Closes the specified window.
		/// </summary>
		/// <param name="window">The window to close.</param>
		/// <returns>True if the window was closed; false if the close was cancelled.</returns>
		bool CloseWindow(Window window);

		/// <summary>
		/// Requests the window system to exit with the specified exit code.
		/// </summary>
		/// <param name="exitCode">The exit code to return.</param>
		void RequestExit(int exitCode);

	/// <summary>
	/// Invalidates all windows and status bars after status bar visibility changes.
	/// </summary>
	void InvalidateAllWindows();

		/// <summary>
		/// Finds the topmost window at the specified point.
		/// </summary>
		/// <param name="point">The point in absolute screen coordinates.</param>
		/// <returns>The topmost window at the point, or null if no window is at that point.</returns>
		Window? GetWindowAtPoint(Point point);

		/// <summary>
		/// Gets the window state service for window lifecycle and state management.
		/// </summary>
		WindowStateService WindowStateService { get; }

		/// <summary>
		/// Gets the status bar state service for status bar and Start menu management.
		/// </summary>
		StatusBarStateService StatusBarStateService { get; }

		/// <summary>
		/// Gets the window positioning manager for move and resize operations.
		/// </summary>
		WindowPositioningManager Positioning { get; }

		/// <summary>
		/// Gets the current theme.
		/// </summary>
		ITheme Theme { get; }

		/// <summary>
		/// Gets the desktop upper left coordinate.
		/// </summary>
		Point DesktopUpperLeft { get; }

		/// <summary>
		/// Gets the desktop bottom right coordinate.
		/// </summary>
		Point DesktopBottomRight { get; }


		/// <summary>
		/// Gets the collection of all windows indexed by GUID.
		/// </summary>
		IReadOnlyDictionary<string, Window> Windows { get; }

		/// <summary>
		/// Gets or sets the console window system options.
		/// </summary>
		ConsoleWindowSystemOptions Options { get; set; }
	}
}
