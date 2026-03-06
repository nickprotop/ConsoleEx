// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI
{
	public partial class Window
	{
		/// <summary>
		/// Ensures the window's content buffer is up to date by rebuilding DOM + painting
		/// if the window has been invalidated. Does NOT convert to ANSI strings.
		/// Used by the optimized cell-level rendering path in <see cref="Renderer"/>.
		/// </summary>
		/// <param name="visibleRegions">Optional list of screen-space rectangles for clipping optimization.</param>
		/// <returns>The content buffer, or null if the window is minimized.</returns>
		internal CharacterBuffer? EnsureContentReady(List<Rectangle>? visibleRegions = null)
		{
			if (_state == WindowState.Minimized)
				return null;

			lock (_lock)
			{
				if (_invalidated)
				{
					var availableWidth = Width - 2;
					var availableHeight = Height - 2;
					RebuildContentBufferOnly(availableWidth, availableHeight, visibleRegions);

					bool isInRenderingPipeline = visibleRegions != null && visibleRegions.Count > 0;
					if (!isInRenderingPipeline)
						_invalidated = true;
				}

				return _renderer?.Buffer;
			}
		}

		/// <summary>
		/// Gets the current content buffer without triggering a rebuild.
		/// Returns null if no buffer has been created yet.
		/// </summary>
		internal CharacterBuffer? ContentBuffer => _renderer?.Buffer;

		/// <summary>
		/// Renders the window content and returns ANSI-formatted lines.
		/// This rebuilds the buffer via <see cref="EnsureContentReady"/> and then
		/// serializes to ANSI strings. Prefer <see cref="EnsureContentReady"/> +
		/// direct buffer access for rendering (avoids ANSI round-trip).
		/// </summary>
		/// <param name="visibleRegions">Optional list of screen-space rectangles representing visible portions of the window.</param>
		/// <returns>A list of rendered content lines visible within the window viewport.</returns>
		public List<string> RenderAndGetVisibleContent(List<Rectangle>? visibleRegions = null)
		{
			var buffer = EnsureContentReady(visibleRegions);
			if (buffer == null)
				return new List<string>();

			var availableHeight = Height - 2;
			var lines = buffer.ToLines(ForegroundColor, BackgroundColor);
			var result = lines.Take(availableHeight).ToList();
			while (result.Count < availableHeight)
			{
				result.Add(string.Empty);
			}
			return result;
		}

		/// <summary>
		/// Gets the BorderRenderer instance for this window.
		/// </summary>
		internal Windows.BorderRenderer? BorderRenderer => _borderRenderer;

		/// <summary>
		/// Gets the WindowEventDispatcher instance for this window.
		/// </summary>
		internal Windows.WindowEventDispatcher? EventDispatcher => _eventDispatcher;

		/// <summary>
		/// Forces a complete rebuild of the DOM tree. Use this when the control hierarchy changes
		/// structurally (e.g., adding/removing columns in a grid) rather than just property changes.
		/// This is more expensive than Invalidate() but necessary for structural changes.
		/// </summary>
		public void ForceRebuildLayout()
		{
			lock (_lock)
			{
				_renderer?.InvalidateDOM(); // Force rebuild on next render
			}
			IsDirty = true;
			_invalidated = true;
		}

		/// <summary>
		/// Gets the actual visible height for a control within the window viewport.
		/// Accounts for window scrolling and clipping.
		/// </summary>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			lock (_lock)
			{
				// For sticky controls, they're always fully visible
				if (control.StickyPosition == StickyPosition.Top || control.StickyPosition == StickyPosition.Bottom)
				{
					// Return the control's rendered height
					var bounds = _layoutManager.GetControlBounds(control);
					return bounds?.ControlContentBounds.Height;
				}

				var availableHeight = Height - 2;
				var scrollableAreaHeight = availableHeight - _topStickyHeight - _bottomStickyHeight;

				// Try to find position for direct children
				if (_controlPositions.TryGetValue(control, out var position))
				{
					return CalculateVisibleHeight(position.StartLine, position.LineCount, scrollableAreaHeight);
				}

				// For nested controls, find the parent container that IS tracked
				// and calculate the nested control's actual position
				foreach (var kvp in _controlPositions)
				{
					var (nestedOffset, nestedHeight) = FindNestedControlPosition(kvp.Key, control);
					if (nestedHeight > 0)
					{
						// Calculate actual position: parent start + nested offset
						int actualStart = kvp.Value.StartLine + nestedOffset;
						return CalculateVisibleHeight(actualStart, nestedHeight, scrollableAreaHeight);
					}
				}

				return null;
			}
		}

		// Find a nested control's position within a parent container
		// Returns (offsetWithinParent, controlHeight) or (-1, 0) if not found
		private (int Offset, int Height) FindNestedControlPosition(IWindowControl container, IWindowControl target)
		{
			if (container is HorizontalGridControl grid)
			{
				// For HorizontalGridControl, controls are in columns side by side
				// We need to find which column contains the control and its vertical offset
				foreach (var column in grid.Columns)
				{
					var result = FindNestedControlPosition(column, target);
					if (result.Height > 0)
						return result;
				}
			}
			else if (container is ColumnContainer column)
			{
				int offset = 0;
				foreach (var child in column.Contents.Where(c => c.Visible))
				{
					if (child == target)
					{
						// Found the target - get its size using DOM
						var size = GetControlHeight(child);
						return (offset, size);
					}

					// Check if target is nested deeper
					var nestedResult = FindNestedControlPosition(child, target);
					if (nestedResult.Height > 0)
					{
						return (offset + nestedResult.Offset, nestedResult.Height);
					}

					// Add this child's height to the offset
					offset += GetControlHeight(child);
				}
			}
			return (-1, 0);
		}

		// Get control height using DOM or MeasureDOM
		private int GetControlHeight(IWindowControl control)
		{
			// Try to get from DOM node first
			var node = _renderer?.GetLayoutNode(control);
			if (node != null)
			{
				return node.AbsoluteBounds.Height;
			}

			// Fallback to MeasureDOM
			if (control is IDOMPaintable paintable)
			{
				var size = paintable.MeasureDOM(new LayoutConstraints(0, Width - 2, 0, Height - 2));
				return size.Height;
			}

			// Last resort
			return control.GetLogicalContentSize().Height;
		}

		// Check if a container control contains the target control
		private bool ContainsControl(IWindowControl container, IWindowControl target)
		{
			if (container is HorizontalGridControl grid)
			{
				foreach (var column in grid.Columns)
				{
					foreach (var child in column.Contents)
					{
						if (child == target)
							return true;
						if (ContainsControl(child, target))
							return true;
					}
				}
			}
			else if (container is ColumnContainer column)
			{
				foreach (var child in column.Contents)
				{
					if (child == target)
						return true;
					if (ContainsControl(child, target))
						return true;
				}
			}
			return false;
		}

		// Calculate visible height given control position and viewport
		private int CalculateVisibleHeight(int controlStart, int controlHeight, int viewportHeight)
		{
			int controlEnd = controlStart + controlHeight;
			int viewportTop = _scrollOffset;
			int viewportBottom = _scrollOffset + viewportHeight;

			int visibleTop = Math.Max(controlStart, viewportTop);
			int visibleBottom = Math.Min(controlEnd, viewportBottom);

			int visibleHeight = visibleBottom - visibleTop;
			return visibleHeight > 0 ? visibleHeight : 0;
		}
	}
}
