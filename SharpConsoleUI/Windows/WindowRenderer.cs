// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Windows
{
	/// <summary>
	/// Coordinates window rendering operations for the DOM-based layout system.
	/// Extracted from Window class as part of Phase 3.2 refactoring.
	///
	/// Responsibilities:
	/// - DOM tree building and management
	/// - Three-stage layout (Measure, Arrange, Paint)
	/// - CharacterBuffer management
	/// - Visible region clipping
	/// - Hit testing
	/// </summary>
	public class WindowRenderer
	{
		private readonly Window _window;
		private readonly ILogService? _logService;

		// DOM state (owned by Window but managed here)
		private LayoutNode? _rootNode;
		private WindowContentLayout? _windowContentLayout;
		private CharacterBuffer? _buffer;
		private readonly Dictionary<IWindowControl, LayoutNode> _controlToNodeMap = new();

		/// <summary>
		/// Initializes a new instance of the WindowRenderer class.
		/// </summary>
		/// <param name="window">The window instance this renderer serves</param>
		/// <param name="logService">Optional log service for diagnostic logging</param>
		public WindowRenderer(
			Window window,
			ILogService? logService)
		{
			_window = window;
			_logService = logService;
		}

		#region Public Properties

		/// <summary>
		/// Gets the root layout node of the DOM tree.
		/// </summary>
		public LayoutNode? RootLayoutNode => _rootNode;

		/// <summary>
		/// Gets or sets the scroll offset for the window content.
		/// </summary>
		public int ScrollOffset
		{
			get => _windowContentLayout?.ScrollOffset ?? 0;
			set
			{
				if (_windowContentLayout != null)
				{
					_windowContentLayout.ScrollOffset = value;
				}
			}
		}

		/// <summary>
		/// Gets the total height of scrollable content.
		/// </summary>
		public int ScrollableContentHeight => _windowContentLayout?.ScrollableContentHeight ?? 0;

		/// <summary>
		/// Gets the maximum scroll offset.
		/// </summary>
		public int MaxScrollOffset => _windowContentLayout?.MaxScrollOffset ?? 0;

		/// <summary>
		/// Scrolls by the specified delta.
		/// </summary>
		public void ScrollBy(int delta)
		{
			_windowContentLayout?.ScrollBy(delta);
		}

		/// <summary>
		/// Scrolls to the top of the content.
		/// </summary>
		public void ScrollToTop()
		{
			_windowContentLayout?.ScrollToTop();
		}

		/// <summary>
		/// Scrolls to the bottom of the content.
		/// </summary>
		public void ScrollToBottom()
		{
			_windowContentLayout?.ScrollToBottom();
		}

		/// <summary>
		/// Scrolls up by one page.
		/// </summary>
		public void PageUp()
		{
			_windowContentLayout?.PageUp();
		}

		/// <summary>
		/// Scrolls down by one page.
		/// </summary>
		public void PageDown()
		{
			_windowContentLayout?.PageDown();
		}

		#endregion

		#region DOM Tree Building

		/// <summary>
		/// Rebuilds the complete DOM tree from the control list.
		/// </summary>
		/// <param name="controls">The window's control list</param>
		/// <param name="contentWidth">Available content width</param>
		/// <param name="contentHeight">Available content height</param>
		public void RebuildDOMTree(IReadOnlyList<IWindowControl> controls, int contentWidth, int contentHeight)
		{
			_logService?.LogDebug($"Rebuilding DOM tree for window '{_window.Title}' ({controls.Count} controls)", "Renderer");

			// Create the character buffer if needed
			if (_buffer == null || _buffer.Width != contentWidth || _buffer.Height != contentHeight)
			{
				_buffer = new CharacterBuffer(contentWidth, contentHeight);
			}

			// Create root node with WindowContentLayout, preserving scroll offset
			int previousScrollOffset = _windowContentLayout?.ScrollOffset ?? 0;
			_windowContentLayout = new WindowContentLayout { ScrollOffset = previousScrollOffset };
			_rootNode = new LayoutNode(null, _windowContentLayout);
			_controlToNodeMap.Clear();

			// Add ALL controls as children (not just visible ones)
			// The node's IsVisible property will control whether it participates in layout
			foreach (var control in controls)
			{
				var node = CreateLayoutNode(control);
				node.IsVisible = control.Visible; // Set visibility on the node
				_rootNode.AddChild(node);
				_controlToNodeMap[control] = node;
			}

			// Perform initial layout
			PerformDOMLayout(contentWidth, contentHeight);
		}

		/// <summary>
		/// Creates a LayoutNode for a control, handling container controls recursively.
		/// </summary>
		private LayoutNode CreateLayoutNode(IWindowControl control)
		{
			ILayoutContainer? layout = null;
			IEnumerable<IWindowControl>? children = null;

			// Determine layout type and get children based on control type
			if (control is Controls.ColumnContainer columnContainer)
			{
				layout = new VerticalStackLayout();
				children = columnContainer.Contents;
			}
			else if (control is Controls.HorizontalGridControl horizontalGrid)
			{
				layout = new HorizontalLayout();
				// Build ordered list of columns and splitters
				var orderedChildren = new List<IWindowControl>();
				for (int i = 0; i < horizontalGrid.Columns.Count; i++)
				{
					orderedChildren.Add(horizontalGrid.Columns[i]);
					// Add splitter after this column if one exists
					var splitter = horizontalGrid.Splitters.FirstOrDefault(s => horizontalGrid.GetSplitterLeftColumnIndex(s) == i);
					if (splitter != null)
					{
						orderedChildren.Add(splitter);
					}
				}
				children = orderedChildren;
			}
			else if (control is Controls.ScrollablePanelControl)
			{
				// ScrollablePanelControl is a self-painting container that manages its own children's
				// rendering with scroll offsets. Do NOT add children to DOM tree - the panel's PaintDOM
				// handles all child painting. Adding children here would cause double-painting.
				layout = null;
				children = null;
			}

			var node = new LayoutNode(control, layout);

			// Handle container controls with children
			// Create nodes for ALL children and set their visibility
			if (children != null)
			{
				foreach (var child in children)
				{
					var childNode = CreateLayoutNode(child);
					childNode.IsVisible = child.Visible; // Set visibility on the node
					node.AddChild(childNode);
					_controlToNodeMap[child] = childNode;
				}
			}

			return node;
		}

		#endregion

		#region Layout Operations

		/// <summary>
		/// Performs the measure and arrange passes on the DOM tree.
		/// </summary>
		public void PerformDOMLayout(int contentWidth, int contentHeight)
		{
			if (_rootNode == null) return;

			_logService?.LogTrace($"Performing layout for window '{_window.Title}' ({contentWidth}x{contentHeight})", "Renderer");

			// Sync node visibility with control visibility before layout
			SyncNodeVisibility();

			// Measure pass - use Loose constraints so children can measure smaller
			var constraints = LayoutConstraints.Loose(contentWidth, contentHeight);
			_rootNode.Measure(constraints);

			// Arrange pass
			_rootNode.Arrange(new LayoutRect(0, 0, contentWidth, contentHeight));
		}

		/// <summary>
		/// Syncs all layout node properties with their corresponding control properties.
		/// This includes visibility and explicit width (important for dynamic sizing like splitters).
		/// </summary>
		private void SyncNodeVisibility()
		{
			foreach (var pair in _controlToNodeMap)
			{
				// Sync visibility
				pair.Value.IsVisible = pair.Key.Visible;

				// Sync explicit width (critical for splitter-resized columns!)
				pair.Value.ExplicitWidth = pair.Key.Width;
			}
		}

		/// <summary>
		/// Invalidates the DOM layout, triggering a re-measure and re-arrange.
		/// </summary>
		public void InvalidateDOMLayout()
		{
			_rootNode?.InvalidateMeasure();
		}

		/// <summary>
		/// Invalidates the entire DOM tree, forcing a complete rebuild on next render.
		/// Called when controls are added, removed, or the window structure changes.
		/// </summary>
		public void InvalidateDOM()
		{
			_rootNode = null;
			_controlToNodeMap.Clear();
		}

		#endregion

		#region Painting Operations

		/// <summary>
		/// Paints the DOM tree to the character buffer.
		/// </summary>
		/// <param name="clipRect">The clipping rectangle in window-space coordinates. Only content within this rect will be painted.</param>
		/// <param name="backgroundColor">Window background color</param>
		public void PaintDOM(LayoutRect clipRect, Color backgroundColor)
		{
			if (_rootNode == null || _buffer == null) return;

			// Clear buffer (could optimize to only clear clipRect region, but full clear is simpler)
			_buffer.Clear(backgroundColor);

			// Paint the tree with the provided clip rect
			_rootNode.Paint(_buffer, clipRect);
		}

		/// <summary>
		/// Converts the character buffer to a list of ANSI-formatted strings.
		/// </summary>
		/// <param name="foregroundColor">Default foreground color</param>
		/// <param name="backgroundColor">Default background color</param>
		/// <returns>List of ANSI-formatted strings</returns>
		public List<string> BufferToLines(Color foregroundColor, Color backgroundColor)
		{
			if (_buffer == null)
				return new List<string>();

			return _buffer.ToLines(foregroundColor, backgroundColor);
		}

		#endregion

		#region Hit Testing

		/// <summary>
		/// Performs hit testing to find the control at the specified position.
		/// </summary>
		/// <param name="x">X coordinate relative to window content area.</param>
		/// <param name="y">Y coordinate relative to window content area.</param>
		/// <returns>The control at the specified position, or null if none found.</returns>
		public IWindowControl? HitTestDOM(int x, int y)
		{
			if (_rootNode == null) return null;

			var hitNode = _rootNode.HitTest(x, y);
			return hitNode?.Control;
		}

		#endregion

		#region Visible Region Processing

		/// <summary>
		/// Converts screen-space visible regions to a window-space clipping rectangle.
		/// This optimization prevents painting occluded areas that are covered by overlapping windows.
		/// </summary>
		/// <param name="visibleRegions">Screen-space rectangles representing visible portions of the window.</param>
		/// <param name="windowLeft">Window left position</param>
		/// <param name="windowTop">Window top position</param>
		/// <param name="windowWidth">Window width</param>
		/// <param name="windowHeight">Window height</param>
		/// <param name="showTitle">Whether the window shows a title bar</param>
		/// <returns>A window-space LayoutRect representing the bounding box of all visible regions, or empty rect if nothing is visible.</returns>
		public LayoutRect ConvertVisibleRegionsToClipRect(
			List<Rectangle> visibleRegions,
			int windowLeft,
			int windowTop,
			int windowWidth,
			int windowHeight,
			bool showTitle)
		{
			if (visibleRegions == null || !visibleRegions.Any())
			{
				// No visible regions - return empty clipRect
				return new LayoutRect(0, 0, 0, 0);
			}

			// Convert screen-space rectangles to window-space coordinates
			// Screen coords: absolute positions on console
			// Window coords: relative to window content area (0,0 = top-left of content, excluding border)

			int windowContentLeft = windowLeft + 1;  // +1 for left border
			int windowContentTop = windowTop + (showTitle ? 2 : 1);  // +1 or +2 for border/title

			// Find bounding box of all visible regions in window space
			int minX = int.MaxValue;
			int minY = int.MaxValue;
			int maxX = int.MinValue;
			int maxY = int.MinValue;

			int contentWidth = windowWidth - 2;  // Available content width
			int contentHeight = windowHeight - 2;  // Available content height

			foreach (var region in visibleRegions)
			{
				// Convert to window-relative coordinates
				int relLeft = region.Left - windowContentLeft;
				int relTop = region.Top - windowContentTop;
				int relRight = relLeft + region.Width;
				int relBottom = relTop + region.Height;

				// Clamp to window content bounds
				relLeft = Math.Max(0, Math.Min(relLeft, contentWidth));
				relTop = Math.Max(0, Math.Min(relTop, contentHeight));
				relRight = Math.Max(0, Math.Min(relRight, contentWidth));
				relBottom = Math.Max(0, Math.Min(relBottom, contentHeight));

				// Skip if region has no area after clamping
				if (relLeft < relRight && relTop < relBottom)
				{
					minX = Math.Min(minX, relLeft);
					minY = Math.Min(minY, relTop);
					maxX = Math.Max(maxX, relRight);
					maxY = Math.Max(maxY, relBottom);
				}
			}

			if (minX == int.MaxValue)
			{
				// No valid regions after conversion
				return new LayoutRect(0, 0, 0, 0);
			}

			return new LayoutRect(minX, minY, maxX - minX, maxY - minY);
		}

		#endregion

		#region Complete Rendering Pipeline

		/// <summary>
		/// Rebuilds the content cache using DOM-based layout.
		/// This is the main entry point for the complete rendering pipeline.
		/// </summary>
		/// <param name="controls">The window's control list</param>
		/// <param name="availableWidth">Available content width</param>
		/// <param name="availableHeight">Available content height</param>
		/// <param name="visibleRegions">Optional screen-space visible regions for clipping optimization</param>
		/// <param name="windowLeft">Window left position</param>
		/// <param name="windowTop">Window top position</param>
		/// <param name="showTitle">Whether the window shows a title bar</param>
		/// <param name="foregroundColor">Window foreground color</param>
		/// <param name="backgroundColor">Window background color</param>
		/// <returns>List of rendered lines as ANSI strings</returns>
		public List<string> RebuildContentCacheDOM(
			IReadOnlyList<IWindowControl> controls,
			int availableWidth,
			int availableHeight,
			List<Rectangle>? visibleRegions,
			int windowLeft,
			int windowTop,
			bool showTitle,
			Color foregroundColor,
			Color backgroundColor)
		{
			// Ensure DOM tree exists
			if (_rootNode == null)
			{
				RebuildDOMTree(controls, availableWidth, availableHeight);
			}

			// Ensure buffer is sized correctly - invalidate measure if size changed (text wrapping may change)
			if (_buffer == null || _buffer.Width != availableWidth || _buffer.Height != availableHeight)
			{
				_buffer = new CharacterBuffer(availableWidth, availableHeight);
				_rootNode?.InvalidateMeasure(); // Size changed, need to re-measure (text wrapping)
			}

			// Always perform layout (arrange pass uses scroll offset)
			PerformDOMLayout(availableWidth, availableHeight);

			// Calculate clip rect from visible regions (optimization to avoid painting occluded areas)
			LayoutRect clipRect;
			if (visibleRegions != null && visibleRegions.Any())
			{
				clipRect = ConvertVisibleRegionsToClipRect(
					visibleRegions,
					windowLeft,
					windowTop,
					availableWidth + 2, // Convert back to window width (content + borders)
					availableHeight + 2,
					showTitle);

				// If no visible area, skip painting entirely
				if (clipRect.Width == 0 || clipRect.Height == 0)
				{
					return new List<string>();
				}
			}
			else
			{
				// No visible regions provided - paint entire window (fallback for non-optimized calls)
				clipRect = new LayoutRect(0, 0, availableWidth, availableHeight);
			}

			// Paint to buffer with clip rect
			PaintDOM(clipRect, backgroundColor);

			// Convert buffer to lines for compatibility with existing render system
			return BufferToLines(foregroundColor, backgroundColor);
		}

		#endregion

		#region Node Lookup

		/// <summary>
		/// Gets the layout node for a specific control.
		/// </summary>
		/// <param name="control">The control to look up</param>
		/// <returns>The layout node for the control, or null if not found</returns>
		public LayoutNode? GetLayoutNode(IWindowControl control)
		{
			_controlToNodeMap.TryGetValue(control, out var node);
			return node;
		}

		/// <summary>
		/// Creates a portal node for rendering external content within a control's bounds.
		/// </summary>
		/// <param name="ownerControl">The control that owns the portal</param>
		/// <param name="portalContent">The content to render in the portal</param>
		/// <returns>The created portal node, or null if the owner control has no layout node</returns>
		public LayoutNode? CreatePortal(IWindowControl ownerControl, IWindowControl portalContent)
		{
			if (!_controlToNodeMap.TryGetValue(ownerControl, out var ownerNode))
				return null;

			var portalNode = new LayoutNode(portalContent);
			portalNode.IsVisible = true;
			ownerNode.AddPortalChild(portalNode);  // FIXED: Use AddPortalChild, not AddChild
			_controlToNodeMap[portalContent] = portalNode;

			return portalNode;
		}

		/// <summary>
		/// Removes a portal node from the DOM tree.
		/// </summary>
		/// <param name="ownerControl">The control that owns the portal</param>
		/// <param name="portalNode">The portal node to remove</param>
		public void RemovePortal(IWindowControl ownerControl, LayoutNode portalNode)
		{
			if (!_controlToNodeMap.TryGetValue(ownerControl, out var ownerNode))
				return;

			ownerNode.RemovePortalChild(portalNode);  // FIXED: Use RemovePortalChild, not RemoveChild

			// Remove from map
			var portalControl = portalNode.Control;
			if (portalControl != null)
			{
				_controlToNodeMap.Remove(portalControl);
			}
		}

		#endregion
	}
}
