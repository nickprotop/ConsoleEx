// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Represents the complete bounds and coordinate information for a control within a window
	/// </summary>
	public class ControlBounds
	{
		/// <summary>
		/// The control these bounds apply to
		/// </summary>
		public Controls.IWindowControl Control { get; }

		/// <summary>
		/// The window containing this control
		/// </summary>
		public Window ParentWindow { get; }

		/// <summary>
		/// Total area allocated to the control within the window content area (including margins)
		/// </summary>
		public Rectangle WindowContentBounds { get; set; }

		/// <summary>
		/// The actual control rendering area (excluding margins, padding)
		/// </summary>
		public Rectangle ControlContentBounds { get; set; }

		/// <summary>
		/// The viewport size available for control content (considering scrollbars)
		/// </summary>
		public Size ViewportSize { get; set; }

		/// <summary>
		/// Current scroll offset within the control content
		/// </summary>
		public Point ScrollOffset { get; set; }

		/// <summary>
		/// Whether the control is currently visible within the window viewport
		/// </summary>
		public bool IsVisible { get; set; }

		/// <summary>
		/// Whether the control supports internal scrolling
		/// </summary>
		public bool HasInternalScrolling { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ControlBounds"/> class.
		/// </summary>
		/// <param name="control">The control these bounds apply to.</param>
		/// <param name="parentWindow">The window containing this control.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> or <paramref name="parentWindow"/> is null.</exception>
		public ControlBounds(Controls.IWindowControl control, Window parentWindow)
		{
			Control = control ?? throw new ArgumentNullException(nameof(control));
			ParentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
			WindowContentBounds = Rectangle.Empty;
			ControlContentBounds = Rectangle.Empty;
			ViewportSize = Size.Empty;
			ScrollOffset = Point.Empty;
			IsVisible = true;
			HasInternalScrolling = false;
		}

		/// <summary>
		/// Converts a position from control-local coordinates to window content coordinates
		/// </summary>
		public Point ControlToWindowContent(Point controlPosition)
		{
			return new Point(
				ControlContentBounds.X + controlPosition.X,
				ControlContentBounds.Y + controlPosition.Y
			);
		}

		/// <summary>
		/// Converts a position from window content coordinates to control-local coordinates
		/// </summary>
		public Point WindowContentToControl(Point windowContentPosition)
		{
			return new Point(
				windowContentPosition.X - ControlContentBounds.X,
				windowContentPosition.Y - ControlContentBounds.Y
			);
		}

		/// <summary>
		/// Converts a control-local position to window coordinates (including borders)
		/// </summary>
		public Point ControlToWindow(Point controlPosition)
		{
			var windowContentPos = ControlToWindowContent(controlPosition);
			return new Point(
				windowContentPos.X + 1, // Add window border
				windowContentPos.Y + 1  // Add window border
			);
		}

		/// <summary>
		/// Converts a window coordinate (including borders) to control-local coordinates
		/// </summary>
		public Point WindowToControl(Point windowPosition)
		{
			var contentPos = new Point(
				windowPosition.X - 1, // Remove window border
				windowPosition.Y - 1  // Remove window border
			);
			return WindowContentToControl(contentPos);
		}

		/// <summary>
		/// Checks if a control-local position is within the visible viewport
		/// </summary>
		public bool IsPositionVisible(Point controlPosition)
		{
			return controlPosition.X >= ScrollOffset.X &&
				   controlPosition.X < ScrollOffset.X + ViewportSize.Width &&
				   controlPosition.Y >= ScrollOffset.Y &&
				   controlPosition.Y < ScrollOffset.Y + ViewportSize.Height;
		}

		/// <summary>
		/// Converts a control-local position to viewport-relative coordinates
		/// </summary>
		public Point ControlToViewport(Point controlPosition)
		{
			return new Point(
				controlPosition.X - ScrollOffset.X,
				controlPosition.Y - ScrollOffset.Y
			);
		}

		/// <summary>
		/// Gets the visible portion of the control within the window
		/// </summary>
		public Rectangle GetVisibleContentBounds()
		{
			var intersection = Rectangle.Intersect(
				ControlContentBounds,
				new Rectangle(0, 0, ParentWindow.Width - 2, ParentWindow.Height - 2)
			);
			
			return intersection;
		}
	}

	/// <summary>
	/// Manages layout calculations and coordinate translations for all controls in a window
	/// </summary>
	public class WindowLayoutManager
	{
		private readonly Window _window;
		private readonly Dictionary<Controls.IWindowControl, ControlBounds> _controlBounds = new();

		/// <summary>
		/// Initializes a new instance of the <see cref="WindowLayoutManager"/> class.
		/// </summary>
		/// <param name="window">The window to manage layout for.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="window"/> is null.</exception>
		public WindowLayoutManager(Window window)
		{
			_window = window ?? throw new ArgumentNullException(nameof(window));
		}

		/// <summary>
		/// Calculates and updates the layout for all controls in the window
		/// </summary>
		public void UpdateLayout(int availableWidth, int availableHeight)
		{
			// Don't clear existing bounds, just update them
			// This allows us to maintain ControlBounds objects across layout updates
		}

		/// <summary>
		/// Gets or creates bounds for a control
		/// </summary>
		public ControlBounds GetOrCreateControlBounds(Controls.IWindowControl control)
		{
			if (!_controlBounds.TryGetValue(control, out var bounds))
			{
				bounds = new ControlBounds(control, _window);
				_controlBounds[control] = bounds;
			}
			return bounds;
		}

		/// <summary>
		/// Gets the bounds information for a specific control
		/// </summary>
		public ControlBounds? GetControlBounds(Controls.IWindowControl control)
		{
			return _controlBounds.TryGetValue(control, out var bounds) ? bounds : null;
		}

		/// <summary>
		/// Translates a control's logical cursor position to window coordinates
		/// by walking up the parent container hierarchy and accumulating offsets
		/// </summary>
		public Point? TranslateLogicalCursorToWindow(Controls.IWindowControl control)
		{
			if (control is not Controls.ILogicalCursorProvider cursorProvider)
				return null;

			var logicalPosition = cursorProvider.GetLogicalCursorPosition();
			if (logicalPosition == null)
				return null;

			// Get control's bounds (which are already absolute window-content coordinates from DOM)
			var bounds = GetOrCreateControlBounds(control);
			var contentBounds = bounds.ControlContentBounds;

			// For nested controls, ControlContentBounds is never populated (only top-level controls get it).
			// Fall back to the DOM node's AbsoluteBounds which tracks all controls including nested ones.
			if (contentBounds.Width == 0 && contentBounds.Height == 0)
			{
				var node = _window._renderer?.GetLayoutNode(control);
				if (node == null) return null;
				var ab = node.AbsoluteBounds;
				contentBounds = new Rectangle(ab.X, ab.Y, ab.Width, ab.Height);
			}

			// ControlContentBounds are already absolute window-content coordinates (from DOM rendering)
			// Just add the logical position to the bounds, then add window border offset
			var windowContentPosition = new Point(
				contentBounds.X + logicalPosition.Value.X,
				contentBounds.Y + logicalPosition.Value.Y
			);

			// Add window border offset (+1, +1)
			var finalPosition = new Point(windowContentPosition.X + 1, windowContentPosition.Y + 1);

			return finalPosition;
		}

		/// <summary>
		/// Finds the control at a specific window coordinate
		/// </summary>
		public (Controls.IWindowControl? control, Point localPosition) GetControlAtWindowPosition(Point windowPosition)
		{
			foreach (var kvp in _controlBounds)
			{
				var bounds = kvp.Value;
				var localPos = bounds.WindowToControl(windowPosition);
				
				// Check if position is within control bounds
				if (localPos.X >= 0 && localPos.X < bounds.ViewportSize.Width &&
					localPos.Y >= 0 && localPos.Y < bounds.ViewportSize.Height)
				{
					// Adjust for scroll offset to get content-local position
					var contentPos = new Point(
						localPos.X + bounds.ScrollOffset.X,
						localPos.Y + bounds.ScrollOffset.Y
					);

					return (kvp.Key, contentPos);
				}
			}

			return (null, Point.Empty);
		}
	}
}