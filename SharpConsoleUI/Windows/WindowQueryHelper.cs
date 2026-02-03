// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using System.Drawing;

namespace SharpConsoleUI.Windows
{
	/// <summary>
	/// Provides static helper methods for querying and finding windows.
	/// Extracted from ConsoleWindowSystem as part of Phase 3.3 refactoring.
	/// </summary>
	public static class WindowQueryHelper
	{
		/// <summary>
		/// Finds the deepest modal child window with the highest Z-index.
		/// </summary>
		/// <param name="window">The parent window to search for modal children.</param>
		/// <param name="context">The window system context.</param>
		/// <returns>The deepest modal child, or null if none found.</returns>
		public static Window? FindDeepestModalChild(Window window, IWindowSystemContext context)
		{
			// Get all direct modal children of the window, ordered by Z-index (highest first)
			var modalChildren = context.Windows.Values
				.Where(w => w.ParentWindow == window && w.Mode == WindowMode.Modal)
				.OrderByDescending(w => w.ZIndex)
				.ToList();

			// If no direct modal children, return null
			if (modalChildren.Count == 0)
			{
				return null;
			}

			// Take the highest Z-index modal child
			Window highestModalChild = modalChildren.First();

			// Check if this modal child itself has modal children
			Window? deeperModalChild = FindDeepestModalChild(highestModalChild, context);

			// If deeper modal child found, return it, otherwise return the highest modal child
			return deeperModalChild ?? highestModalChild;
		}

		/// <summary>
		/// Finds the appropriate window to activate based on modality rules.
		/// Handles active modal children and recursive modal hierarchies.
		/// </summary>
		/// <param name="targetWindow">The window to activate.</param>
		/// <param name="context">The window system context.</param>
		/// <param name="flashWindow">Action to flash a window for user feedback.</param>
		/// <returns>The window that should actually be activated.</returns>
		public static Window FindWindowToActivate(Window targetWindow, IWindowSystemContext context, Action<Window> flashWindow)
		{
			// First, check if there's already an active modal child - prioritize it
			var activeModalChild = context.Windows.Values
				.Where(w => w.ParentWindow == targetWindow && w.Mode == WindowMode.Modal && w.GetIsActive())
				.FirstOrDefault();

			if (activeModalChild != null)
			{
				// Found an already active modal child, prioritize it
				flashWindow(activeModalChild);
				return FindWindowToActivate(activeModalChild, context, flashWindow); // Recursively check if this active modal has active modal children
			}

			// No already active modal child, check for any modal children
			var modalChild = FindDeepestModalChild(targetWindow, context);
			if (modalChild != null)
			{
				// Found a modal child, activate it instead
				flashWindow(modalChild);
				return modalChild;
			}

			// No modal children, return the target window itself
			return targetWindow;
		}

		/// <summary>
		/// Finds the topmost window at the specified point.
		/// Considers parent-child relationships to avoid returning windows covered by their modal children.
		/// </summary>
		/// <param name="point">The point in absolute screen coordinates.</param>
		/// <param name="context">The window system context.</param>
		/// <returns>The topmost window at the point, or null if none found.</returns>
		public static Window? GetWindowAtPoint(Point point, IWindowSystemContext context)
		{
			List<Window> windows = context.Windows.Values
				.Where(window =>
					point.X >= window.Left &&
					point.X < window.Left + window.Width &&
					point.Y - context.DesktopUpperLeft.Y >= window.Top &&
					point.Y - context.DesktopUpperLeft.Y < window.Top + window.Height)
				.OrderBy(window => window.ZIndex).ToList();

			// Iterate from topmost (highest ZIndex) to bottom
			// Return the first window that doesn't have a child at this point
			for (int i = windows.Count - 1; i >= 0; i--)
			{
				var window = windows[i];

				// Check if any higher-ZIndex window in the list is a child of this window
				bool hasChildAtPoint = false;
				for (int j = i + 1; j < windows.Count; j++)
				{
					var higherWindow = windows[j];

					// Check if this higher window is a modal child of current window
					if (higherWindow.Mode == WindowMode.Modal && higherWindow.ParentWindow == window)
					{
						hasChildAtPoint = true;
						break;
					}
				}

				if (!hasChildAtPoint)
				{
					return window;
				}
			}

			return null;
		}

		/// <summary>
		/// Checks if a window is a child (or descendant) of another window.
		/// </summary>
		/// <param name="potentialChild">The window to check.</param>
		/// <param name="potentialParent">The potential parent window.</param>
		/// <returns>True if potentialChild is a descendant of potentialParent; false otherwise.</returns>
		public static bool IsChildWindow(Window potentialChild, Window potentialParent)
		{
			Window? current = potentialChild;
			while (current?.ParentWindow != null)
			{
				if (current.ParentWindow.Equals(potentialParent))
					return true;
				current = current.ParentWindow;
			}
			return false;
		}
	}
}
