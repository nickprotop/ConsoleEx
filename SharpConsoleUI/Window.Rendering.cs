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
		/// Renders the window content and returns the visible lines.
		/// </summary>
		/// <param name="visibleRegions">Optional list of screen-space rectangles representing visible portions of the window.
		/// If provided, only these regions will be painted (optimization to avoid painting occluded areas).</param>
		/// <returns>A list of rendered content lines visible within the window viewport.</returns>
		public List<string> RenderAndGetVisibleContent(List<Rectangle>? visibleRegions = null)
		{
			// Return empty list if window is minimized
			if (_state == WindowState.Minimized)
			{
				return new List<string>();
			}

			lock (_lock)
			{
				// Only recalculate content if it's been invalidated
				if (_invalidated)
				{
					// Layout will be updated lazily on next event
					RebuildContentCache(visibleRegions);

					// Check if visibleRegions is null or empty (window not in rendering pipeline yet)
					bool isInRenderingPipeline = visibleRegions != null && visibleRegions.Count > 0;

					// If we rendered without visible regions (during window creation),
					// keep _invalidated=true so we re-render when actually in the pipeline
					if (!isInRenderingPipeline)
					{
						_invalidated = true;
					}
				}

				return BuildVisibleContent();
			}
		}

		private void RebuildContentCache(List<Rectangle>? visibleRegions = null)
		{
			var availableWidth = Width - 2; // Account for borders
			var availableHeight = Height - 2; // Account for borders

			// Always use DOM-based layout
			RebuildContentCacheDOM(availableWidth, availableHeight, visibleRegions);
		}

		private List<string> BuildVisibleContent()
		{
			var availableHeight = Height - 2; // Account for borders

			// DOM mode: _cachedContent already contains the viewport-sized content
			var result = _cachedContent?.Take(availableHeight).ToList() ?? new List<string>();
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
		/// Gets or sets the cached top border string (exposed for Renderer.cs access).
		/// </summary>
		internal string? _cachedTopBorder
		{
			get => _borderRenderer?._cachedTopBorder;
			set { if (_borderRenderer != null) _borderRenderer._cachedTopBorder = value; }
		}

		/// <summary>
		/// Gets or sets the cached bottom border string (exposed for Renderer.cs access).
		/// </summary>
		internal string? _cachedBottomBorder
		{
			get => _borderRenderer?._cachedBottomBorder;
			set { if (_borderRenderer != null) _borderRenderer._cachedBottomBorder = value; }
		}

		/// <summary>
		/// Gets or sets the cached vertical border string (exposed for Renderer.cs access).
		/// </summary>
		internal string? _cachedVerticalBorder
		{
			get => _borderRenderer?._cachedVerticalBorder;
			set { if (_borderRenderer != null) _borderRenderer._cachedVerticalBorder = value; }
		}

		/// <summary>
		/// Gets or sets the cached border width (exposed for Renderer.cs access).
		/// </summary>
		internal int _cachedBorderWidth
		{
			get => _borderRenderer?._cachedBorderWidth ?? -1;
			set { if (_borderRenderer != null) _borderRenderer._cachedBorderWidth = value; }
		}

		/// <summary>
		/// Gets or sets the cached border active state (exposed for Renderer.cs access).
		/// </summary>
		internal bool _cachedBorderIsActive
		{
			get => _borderRenderer?._cachedBorderIsActive ?? false;
			set { if (_borderRenderer != null) _borderRenderer._cachedBorderIsActive = value; }
		}

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
