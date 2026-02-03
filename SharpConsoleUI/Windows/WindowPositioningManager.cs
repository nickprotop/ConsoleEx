// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Themes;
using System.Drawing;

namespace SharpConsoleUI.Windows
{
	/// <summary>
	/// Manages window positioning and resizing operations.
	/// Handles window movement, resizing, bounds validation, and desktop clearing.
	/// Extracted from ConsoleWindowSystem as part of Phase 2.2 refactoring.
	/// </summary>
	public class WindowPositioningManager
	{
		private readonly Renderer _renderer;
		private readonly RenderCoordinator _renderCoordinator;
		private readonly Func<IWindowSystemContext> _getWindowSystem;

		/// <summary>
		/// Initializes a new instance of the WindowPositioningManager class.
		/// </summary>
		/// <param name="renderer">Renderer for desktop clearing and region rendering.</param>
		/// <param name="renderCoordinator">Render coordinator for pending desktop clears.</param>
		/// <param name="getWindowSystem">Function to get window system context (lazy to avoid circular dependency).</param>
		public WindowPositioningManager(
			Renderer renderer,
			RenderCoordinator renderCoordinator,
			Func<IWindowSystemContext> getWindowSystem)
		{
			_renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
			_renderCoordinator = renderCoordinator ?? throw new ArgumentNullException(nameof(renderCoordinator));
			_getWindowSystem = getWindowSystem ?? throw new ArgumentNullException(nameof(getWindowSystem));
		}

		/// <summary>
		/// Moves a window to a specific position.
		/// </summary>
		/// <param name="window">The window to move.</param>
		/// <param name="newLeft">The new left coordinate.</param>
		/// <param name="newTop">The new top coordinate.</param>
		public void MoveWindowTo(Window window, int newLeft, int newTop)
		{
			if (window == null) return;

			// Only update if position actually changed
			if (newLeft == window.Left && newTop == window.Top)
				return;

			var oldBounds = new Rectangle(window.Left, window.Top, window.Width, window.Height);

			_renderCoordinator.AddPendingDesktopClear(oldBounds);
			window.SetPosition(new Point(newLeft, newTop));
			window.Invalidate(true);

			// Invalidate windows that were underneath (now exposed)
			InvalidateExposedRegions(window, oldBounds);
		}

		/// <summary>
		/// Moves a window by a relative delta.
		/// </summary>
		/// <param name="window">The window to move.</param>
		/// <param name="deltaX">The horizontal movement delta.</param>
		/// <param name="deltaY">The vertical movement delta.</param>
		public void MoveWindowBy(Window window, int deltaX, int deltaY)
		{
			if (window == null) return;

			var context = _getWindowSystem();

			int newLeft = window.Left + deltaX;
			int newTop = window.Top + deltaY;

			// Constrain to desktop bounds
			newLeft = Math.Max(0, Math.Min(newLeft, context.DesktopDimensions.Width - window.Width));
			newTop = Math.Max(0, Math.Min(newTop, context.DesktopDimensions.Height - window.Height));

			MoveWindowTo(window, newLeft, newTop);
		}

		/// <summary>
		/// Resizes a window to a new size and position.
		/// </summary>
		/// <param name="window">The window to resize.</param>
		/// <param name="newLeft">The new left coordinate.</param>
		/// <param name="newTop">The new top coordinate.</param>
		/// <param name="newWidth">The new width.</param>
		/// <param name="newHeight">The new height.</param>
		public void ResizeWindowTo(Window window, int newLeft, int newTop, int newWidth, int newHeight)
		{
			if (window == null) return;

			var context = _getWindowSystem();

			// Store the current window bounds before resizing
			var oldBounds = new Rectangle(window.Left, window.Top, window.Width, window.Height);

			// Constrain to minimum/maximum sizes and desktop bounds
			newWidth = Math.Max(10, newWidth); // Minimum width
			newHeight = Math.Max(3, newHeight); // Minimum height

			// Constrain to desktop bounds
			newLeft = Math.Max(0, Math.Min(newLeft, context.DesktopDimensions.Width - newWidth));
			newTop = Math.Max(0, Math.Min(newTop, context.DesktopDimensions.Height - newHeight));

			// Ensure the window doesn't resize beyond desktop bounds
			newWidth = Math.Min(newWidth, context.DesktopDimensions.Width - newLeft);
			newHeight = Math.Min(newHeight, context.DesktopDimensions.Height - newTop);

			// Only update if position or size actually changed
			if (newLeft != window.Left || newTop != window.Top ||
				newWidth != window.Width || newHeight != window.Height)
			{
				// Add old bounds to pending clears
				_renderCoordinator.AddPendingDesktopClear(oldBounds);

				// Apply the new position and size
				window.SetPosition(new Point(newLeft, newTop));
				window.SetSize(newWidth, newHeight);
				window.Invalidate(true);

				// Invalidate windows that were underneath (now exposed)
				InvalidateExposedRegions(window, oldBounds);
			}
		}

		/// <summary>
		/// Resizes a window by a relative delta.
		/// </summary>
		/// <param name="window">The window to resize.</param>
		/// <param name="deltaWidth">The width change delta.</param>
		/// <param name="deltaHeight">The height change delta.</param>
		public void ResizeWindowBy(Window window, int deltaWidth, int deltaHeight)
		{
			if (window == null) return;

			int newWidth = window.Width + deltaWidth;
			int newHeight = window.Height + deltaHeight;

			ResizeWindowTo(window, window.Left, window.Top, newWidth, newHeight);
		}

		/// <summary>
		/// Performs a move or resize operation with desktop clearing and window invalidation.
		/// Used for keyboard-based window operations.
		/// </summary>
		/// <param name="window">The window to move or resize.</param>
		/// <param name="windowTopologyAction">The type of operation (Move or Resize).</param>
		/// <param name="direction">The direction of the operation.</param>
		public void MoveOrResizeOperation(Window? window, WindowTopologyAction windowTopologyAction, Direction direction)
		{
			if (window == null) return;

			var context = _getWindowSystem();
			var theme = context.Theme;

			// Store the current window bounds before any operation
			var oldBounds = new Rectangle(window.Left, window.Top, window.Width, window.Height);

			// FIRST: Clear the old window position completely (same as mouse operations)
			_renderer.FillRect(window.Left, window.Top, window.Width, window.Height,
				theme.DesktopBackgroundChar, theme.DesktopBackgroundColor, theme.DesktopForegroundColor);

			// Redraw the necessary regions that were underneath the window
			foreach (var w in context.Windows.Values.OrderBy(w => w.ZIndex))
			{
				// Skip minimized windows - they're invisible
				if (w.State == WindowState.Minimized)
					continue;

				if (w != window && GeometryHelpers.DoesRectangleOverlapWindow(oldBounds, w))
				{
					// Redraw the parts of underlying windows that were covered
					var intersection = GeometryHelpers.GetRectangleIntersection(oldBounds,
						new Rectangle(w.Left, w.Top, w.Width, w.Height));

					if (!intersection.IsEmpty)
					{
						_renderer.RenderRegion(w, intersection);
					}
				}
			}

			// FINALLY: Invalidate the window which will cause it to redraw at its new position
			// (The actual position/size change happens in the calling HandleMoveInput method)
			window.Invalidate(false);
		}

		#region Private Helper Methods

		/// <summary>
		/// Invalidates windows that were underneath the moved window and are now exposed.
		/// </summary>
		/// <param name="movedWindow">The window that was moved.</param>
		/// <param name="oldBounds">The old bounds of the moved window.</param>
		private void InvalidateExposedRegions(Window movedWindow, Rectangle oldBounds)
		{
			// BRUTE FORCE FIX: Instead of trying to render tiny exposed regions (which causes blanks),
			// just invalidate all windows that were underneath the old position.
			// They'll render normally with proper visibleRegions in the next UpdateDisplay.

			var context = _getWindowSystem();

			foreach (var window in context.Windows.Values)
			{
				if (window == movedWindow)
					continue; // Skip the window that moved

				if (window.ZIndex >= movedWindow.ZIndex)
					continue; // Only invalidate windows that were underneath

				// Check if this window overlaps with the OLD position
				if (GeometryHelpers.DoesRectangleOverlapWindow(oldBounds, window))
				{
					window.Invalidate(true);
				}
			}
		}


		#endregion
	}
}
