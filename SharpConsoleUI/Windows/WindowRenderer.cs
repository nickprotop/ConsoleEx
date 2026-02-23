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

		#region Public Events

		/// <summary>
		/// Delegate for buffer painting events.
		/// </summary>
		/// <param name="buffer">The character buffer to paint to.</param>
		/// <param name="dirtyRegion">The region being painted (or full bounds if entire buffer).</param>
		/// <param name="clipRect">The clipping rectangle used during paint.</param>
		public delegate void BufferPaintDelegate(
			CharacterBuffer buffer,
			LayoutRect dirtyRegion,
			LayoutRect clipRect);

		/// <summary>
		/// Raised BEFORE painting controls to the buffer.
		/// </summary>
		/// <remarks>
		/// This event allows painting backgrounds, game graphics, or other content
		/// that should appear BEHIND the controls. Controls will paint on top.
		///
		/// Example use cases:
		/// - Game rendering (fractals, animations, sprites)
		/// - Custom backgrounds
		/// - Gradients or patterns behind UI
		/// </remarks>
		public event BufferPaintDelegate? PreBufferPaint;

		/// <summary>
		/// Raised AFTER painting controls to the buffer but before converting to ANSI strings.
		/// </summary>
		/// <remarks>
		/// This event allows custom effects, transitions, filters, or compositor-style
		/// manipulations on the rendered buffer. The buffer can be safely modified here.
		/// Content painted here will appear ON TOP of controls.
		///
		/// Example use cases:
		/// - Fade in/out transitions
		/// - Blur effects for modal backgrounds
		/// - Glow effects around focused controls
		/// - Custom overlays and effects
		/// </remarks>
		public event BufferPaintDelegate? PostBufferPaint;

		#endregion

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

		/// <summary>
		/// Gets the current character buffer for this window.
		/// </summary>
		/// <remarks>
		/// CAUTION: Direct buffer manipulation should only be done via PostBufferPaint event
		/// to avoid race conditions. Reading is safe at any time.
		/// </remarks>
		public CharacterBuffer? Buffer => _buffer;

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
			// Special case: ScrollablePanel children registered but NOT added to DOM tree
			if (control is Controls.ScrollablePanelControl scrollablePanel)
			{
				var node = new LayoutNode(control, null);
				foreach (var child in scrollablePanel.Children)
				{
					var childNode = CreateLayoutNode(child);
					_controlToNodeMap[child] = childNode;
				}
				return node;
			}

			// Use shared factory for all other control types
			var subtree = LayoutNodeFactory.CreateSubtree(control);

			// Register all nodes in the subtree in our control-to-node map
			RegisterSubtreeInMap(subtree);

			return subtree;
		}

		/// <summary>
		/// Recursively registers all nodes in a subtree in the control-to-node map.
		/// </summary>
		private void RegisterSubtreeInMap(LayoutNode node)
		{
			if (node.Control != null)
				_controlToNodeMap[node.Control] = node;
			foreach (var child in node.Children)
				RegisterSubtreeInMap(child);
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

			PaintDOMWithoutClear(clipRect);
		}

		/// <summary>
		/// Paints the DOM tree to the character buffer WITHOUT clearing first.
		/// Used internally when PreBufferPaint has already painted content to preserve.
		/// </summary>
		/// <param name="clipRect">The clipping rectangle in window-space coordinates.</param>
		private void PaintDOMWithoutClear(LayoutRect clipRect)
		{
			if (_rootNode == null || _buffer == null) return;

			// Paint the tree with the provided clip rect
			_rootNode.Paint(_buffer, clipRect);

			// Diagnostics: Capture CharacterBuffer snapshot after painting
			var diagnostics = _window._windowSystem?.RenderingDiagnostics;
			if (diagnostics?.IsEnabled == true && diagnostics.EnabledLayers.HasFlag(Configuration.DiagnosticsLayers.CharacterBuffer))
			{
				diagnostics.CaptureCharacterBuffer(_buffer);
			}
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

			// Connect diagnostics to CharacterBuffer if needed
			var diagnostics = _window._windowSystem?.RenderingDiagnostics;
			if (diagnostics != null && _buffer.Diagnostics == null)
			{
				_buffer.Diagnostics = diagnostics;
			}

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
			int windowContentTop = windowTop + 1;  // +1 for border (title is inline with border)

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

			// Clear buffer first (before any painting)
			_buffer.Clear(backgroundColor);

			// Fire pre-paint event for background rendering (games, custom backgrounds)
			if (PreBufferPaint != null)
			{
				var dirtyRegion = new LayoutRect(0, 0, _buffer.Width, _buffer.Height);
				PreBufferPaint.Invoke(_buffer, dirtyRegion, clipRect);
			}

			// Paint controls to buffer with clip rect (on top of pre-paint content)
			PaintDOMWithoutClear(clipRect);

			// Fire post-paint event for custom effects (e.g., transitions, filters)
			if (PostBufferPaint != null && _buffer != null)
			{
				var dirtyRegion = new LayoutRect(0, 0, _buffer.Width, _buffer.Height);
				PostBufferPaint.Invoke(_buffer, dirtyRegion, clipRect);
			}

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
		/// Updates the absolute bounds for a control managed by a self-painting container.
		/// Used by ScrollablePanelControl to register child bounds during PaintDOM.
		/// </summary>
		internal void UpdateChildBounds(IWindowControl child, LayoutRect bounds)
		{
			if (!_controlToNodeMap.TryGetValue(child, out var node))
			{
				// Child not yet registered (e.g., added after DOM build) — create a placeholder node
				node = new LayoutNode(child);
				_controlToNodeMap[child] = node;
			}
			node.SetAbsoluteBounds(bounds);
		}

		/// <summary>
		/// Creates a portal node for rendering external content within a control's bounds.
		/// </summary>
		/// <param name="ownerControl">The control that owns the portal</param>
		/// <param name="portalContent">The content to render in the portal</param>
		/// <returns>The created portal node, or null if the owner control has no layout node</returns>
		public LayoutNode? CreatePortal(IWindowControl ownerControl, IWindowControl portalContent)
		{
			// Note: ownerControl may be nested inside another control (e.g., dropdown inside toolbar)
			// and not directly registered in _controlToNodeMap. We don't actually need the owner node
			// for portal creation - the portal bounds come from the portal content itself.
			// So we allow portal creation even for nested controls.

			var portalNode = new LayoutNode(portalContent);
			portalNode.IsVisible = true; // Ensure portal is visible

			// Measure the portal to get its size
			var contentWidth = _window.Width - 2;
			var contentHeight = _window.Height - 2;
			var constraints = LayoutConstraints.Loose(contentWidth, contentHeight);
			portalNode.Measure(constraints);

			// Get the portal's desired position
			// IHasPortalBounds allows any external portal content to supply its own bounds.
			// The existing specific-type checks are kept for backward compatibility.
			Rectangle portalBounds;
			if (portalContent is IHasPortalBounds positioned)
			{
				portalBounds = positioned.GetPortalBounds();
			}
			else if (portalContent is MenuPortalContent menuPortal)
			{
				portalBounds = menuPortal.GetPortalBounds();
			}
			else if (portalContent is Controls.DropdownPortalContent dropdownPortal)
			{
				portalBounds = dropdownPortal.GetPortalBounds();
			}
			else
			{
				// Fallback: position at (0,0) with measured size
				portalBounds = new Rectangle(0, 0, portalNode.DesiredSize.Width, portalNode.DesiredSize.Height);
			}

			var portalRect = new LayoutRect(portalBounds.X, portalBounds.Y, portalBounds.Width, portalBounds.Height);

			// Arrange the portal at its absolute position
			portalNode.Arrange(portalRect);

			// CRITICAL: Add portal to ROOT node, not owner node
			// This ensures portals paint AFTER all regular content
			if (_rootNode != null)
			{
				_rootNode.AddPortalChild(portalNode);
				_controlToNodeMap[portalContent] = portalNode;
			}

			return portalNode;
		}

		/// <summary>
		/// Removes a portal node from the DOM tree.
		/// </summary>
		/// <param name="ownerControl">The control that owns the portal</param>
		/// <param name="portalNode">The portal node to remove</param>
		public void RemovePortal(IWindowControl ownerControl, LayoutNode portalNode)
		{
			// Remove from root node (where it was added in CreatePortal)
			if (_rootNode != null)
			{
				var removed = _rootNode.RemovePortalChild(portalNode);
				if (portalNode.Control != null)
					_controlToNodeMap.Remove(portalNode.Control);
			}
		}

		#endregion
	}
}
