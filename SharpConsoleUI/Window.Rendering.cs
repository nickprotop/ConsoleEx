// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

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
				if (PendingWork != FrameWork.None)
				{
					// FramelessLayoutWidth reserves the scrollbar column ONLY for an overflowing frameless
					// window; the reservation is derived from TotalLines (measured at full width on the prior
					// build), so it is width-stable and cannot oscillate. Non-frameless windows are unaffected.
					var availableWidth = FramelessLayoutWidth(TotalLines);
					var availableHeight = ContentHeight;
					RebuildContentBufferOnly(availableWidth, availableHeight, visibleRegions);

					// No visible regions yet (off-screen): keep a rebuild pending so the window re-runs when it
					// later gets a viewport. Re-raise at Repaint — the off-screen Measure already ran and its
					// DesiredSize is viewport-independent, so the eventual on-screen frame reuses it (Measure
					// skipped); a genuine size change is caught by the buffer-resize guard's NeedsMeasure backstop.
					bool isInRenderingPipeline = visibleRegions != null && visibleRegions.Count > 0;
					if (!isInRenderingPipeline)
					{
						// Off-screen: no viewport yet. Re-raise at Repaint so the window re-runs once it gets a
						// viewport. PendingWork is the only dirty signal; setting it here is the whole retry.
						Request(Invalidation.Repaint);
					}
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

			var availableHeight = ContentHeight;
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
		/// Creates this window's root layout - the layout that arranges the window's own controls.
		/// Null, the default, uses the application default
		/// (<see cref="Windows.WindowRenderer.RootLayoutFactory"/>, a
		/// <see cref="Layout.DockRootLayout"/> unless changed).
		/// </summary>
		/// <remarks>
		/// <para>
		/// Setting this gives one window a different layout model from the rest of the
		/// application: a docked shell around a grid body, a plain stack, or anything else
		/// implementing <see cref="Layout.IWindowRootLayout"/>. Other windows are unaffected.
		/// </para>
		/// <para>
		/// This is a factory rather than an instance because the renderer rebuilds its root
		/// whenever the DOM is rebuilt; a shared instance would carry one window's banding and
		/// scroll position into the next rebuild. Assigning applies immediately.
		/// </para>
		/// <example>
		/// <code>
		/// window.Root = () => new DockRootLayout();   // this window only
		/// window.Root = null;                          // back to the application default
		/// </code>
		/// </example>
		/// </remarks>
		public Func<Layout.IWindowRootLayout>? Root
		{
			get => _renderer?.WindowRootLayoutFactory;
			set
			{
				if (_renderer == null || _renderer.WindowRootLayoutFactory == value)
					return;

				_renderer.WindowRootLayoutFactory = value;

				// The root is built during the DOM rebuild, so a new one only takes effect once
				// the tree is rebuilt. Doing that here means assignment is enough - a caller
				// should not have to know that the root is a rebuild-time decision.
				ForceRebuildLayout();
			}
		}

		#region Border-delegated scrollbars

		private Layout.IBorderScrollSource? _borderVerticalSource;
		private Layout.IBorderScrollSource? _borderHorizontalSource;

		// What the source's own scrollbar was before delegation took it over, so releasing the
		// axis puts it back rather than leaving the region permanently bar-less.
		private bool _borderVerticalRestore;
		private bool _borderHorizontalRestore;

		/// <summary>
		/// Hands a region's scrollbar to the window border, so it draws in the frame instead of
		/// inside the region - reclaiming a column (vertical) or row (horizontal) of content.
		/// </summary>
		/// <param name="source">
		/// The region driving the border. Pass <see langword="null"/> to release the axis back to
		/// the window's own scrolling body.
		/// </param>
		/// <param name="axis">The axis or axes to delegate.</param>
		/// <exception cref="InvalidOperationException">
		/// An axis is already delegated to a different source. The border is a single track per
		/// axis: two regions sharing one would fight over the thumb, so the second registration
		/// is refused rather than silently winning.
		/// </exception>
		/// <remarks>
		/// This generalises what the frame has always done. The window's scrolling body drawing a
		/// thumb in the right border IS this relationship, previously hard-wired; delegating is
		/// the same arrangement made available to any region that can report a position.
		/// </remarks>
		public void DelegateScrollbarToBorder(Layout.IBorderScrollSource? source, Layout.ScrollAxis axis)
		{
			if (axis == Layout.ScrollAxis.None)
				return;

			if (axis.HasFlag(Layout.ScrollAxis.Vertical))
			{
				var previous = _borderVerticalSource;
				Claim(ref _borderVerticalSource, source, Layout.ScrollAxis.Vertical);
				Handover(previous, source, ref _borderVerticalRestore);
			}

			if (axis.HasFlag(Layout.ScrollAxis.Horizontal))
			{
				var previous = _borderHorizontalSource;
				Claim(ref _borderHorizontalSource, source, Layout.ScrollAxis.Horizontal);
				Handover(previous, source, ref _borderHorizontalRestore);
			}

			Invalidate(Invalidation.Repaint);

			// Taking the border over hides the region's own bar; handing it back restores it.
			// Delegating without this draws two scrollbars and reclaims none of the space the
			// caller delegated in order to reclaim.
			static void Handover(Layout.IBorderScrollSource? previous, Layout.IBorderScrollSource? source,
				ref bool restore)
			{
				if (ReferenceEquals(previous, source))
					return;

				if (source == null)
				{
					if (previous is Layout.PanelBorderScrollSource releasing)
						releasing.RestoreOwnScrollbar(restore);
					restore = false;
					return;
				}

				if (source is Layout.PanelBorderScrollSource claiming)
					restore = claiming.SuppressOwnScrollbar();
			}

			static void Claim(ref Layout.IBorderScrollSource? slot, Layout.IBorderScrollSource? source,
				Layout.ScrollAxis axis)
			{
				if (source != null && slot != null && !ReferenceEquals(slot, source))
				{
					throw new InvalidOperationException(
						$"The {axis} border scrollbar is already delegated to another region. " +
						"Release it first by passing null for that axis.");
				}

				slot = source;
			}
		}

		/// <summary>
		/// The region driving the border scrollbar on <paramref name="axis"/>, or
		/// <see langword="null"/> when the window's own body drives it.
		/// </summary>
		/// <param name="axis">A single axis; <see cref="Layout.ScrollAxis.Both"/> is not a query.</param>
		/// <returns>The delegated source, or null.</returns>
		public Layout.IBorderScrollSource? BorderScrollSource(Layout.ScrollAxis axis) => axis switch
		{
			Layout.ScrollAxis.Vertical => _borderVerticalSource,
			Layout.ScrollAxis.Horizontal => _borderHorizontalSource,
			_ => null
		};

		/// <summary>
		/// Whether the bottom border is currently showing a horizontal scrollbar, which happens
		/// only when a region has delegated that axis AND has something to scroll.
		/// </summary>
		internal bool HorizontalBorderScrollbarVisible
		{
			get
			{
				var source = _borderHorizontalSource;
				return source != null && source.Extent > source.Viewport && source.Viewport > 0;
			}
		}

		#endregion

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
			Request(Invalidation.Relayout);
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

				var availableHeight = ContentHeight;
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
			if (container is IColumnGridOwner grid)
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
				var size = paintable.MeasureDOM(new LayoutConstraints(0, ContentWidth, 0, ContentHeight));
				return size.Height;
			}

			// Last resort
			return control.GetLogicalContentSize().Height;
		}

		// Check if a container control contains the target control
		private bool ContainsControl(IWindowControl container, IWindowControl target)
		{
			if (container is IColumnGridOwner grid)
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
