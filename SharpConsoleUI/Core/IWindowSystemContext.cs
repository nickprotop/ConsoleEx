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
		/// Handles start menu keyboard shortcut (Alt+key or configured shortcut).
		/// </summary>
		/// <param name="key">The key pressed.</param>
		/// <returns>True if the shortcut was handled; false otherwise.</returns>
		bool HandleStartMenuShortcut(ConsoleKeyInfo key);

		/// <summary>
		/// Handles status bar mouse click (e.g., start button).
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <returns>True if the click was handled; false otherwise.</returns>
		bool HandleStatusBarMouseClick(int x, int y);

		/// <summary>
		/// Handles Alt+1-9 window selection by index.
		/// </summary>
		/// <param name="key">The key pressed.</param>
		/// <returns>True if the input was handled; false otherwise.</returns>
		bool HandleAltInput(ConsoleKeyInfo key);


		/// <summary>
		/// Activates the next non-minimized window after the specified window is minimized.
		/// </summary>
		/// <param name="minimizedWindow">The window that was just minimized.</param>
		void ActivateNextNonMinimizedWindow(Window minimizedWindow);

		/// <summary>
		/// Deactivates the current active window (e.g., when clicking on empty desktop).
		/// </summary>
		void DeactivateCurrentWindow();

		/// <summary>
		/// Translates absolute screen coordinates to window-relative coordinates.
		/// </summary>
		/// <param name="window">The window to translate coordinates relative to.</param>
		/// <param name="point">The point in absolute screen coordinates.</param>
		/// <returns>The point in window-relative coordinates.</returns>
		Point TranslateToRelative(Window window, Point? point);

		/// <summary>
		/// Finds the topmost window at the specified point.
		/// </summary>
		/// <param name="point">The point in absolute screen coordinates.</param>
		/// <returns>The topmost window at the point, or null if no window is at that point.</returns>
		Window? GetWindowAtPoint(Point point);

		/// <summary>
		/// Moves the specified window to a new position.
		/// </summary>
		void MoveWindowTo(Window window, int newLeft, int newTop);

		/// <summary>
		/// Moves the specified window by a relative delta.
		/// </summary>
		void MoveWindowBy(Window window, int deltaX, int deltaY);

		/// <summary>
		/// Resizes the specified window to a new size and position.
		/// </summary>
		void ResizeWindowTo(Window window, int newLeft, int newTop, int newWidth, int newHeight);

		/// <summary>
		/// Resizes the specified window by a relative delta.
		/// </summary>
		void ResizeWindowBy(Window window, int deltaWidth, int deltaHeight);

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
		/// Gets or sets the top status bar text.
		/// </summary>
		string? TopStatus { get; set; }

		/// <summary>
		/// Gets or sets the bottom status bar text.
		/// </summary>
		string? BottomStatus { get; set; }

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
