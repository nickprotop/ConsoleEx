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
		#region DOM-Based Layout System

		/// <summary>
		/// Gets the root layout node for the window's DOM tree.
		/// Used internally for layout traversal (e.g., collecting control bounds for overlay rendering).
		/// </summary>
		internal LayoutNode? GetRootLayoutNode() => _renderer?.RootLayoutNode;

		/// <summary>
		/// Gets the LayoutNode associated with a control.
		/// </summary>
		/// <param name="control">The control to look up.</param>
		/// <returns>The LayoutNode for the control, or null if not found.</returns>
		public LayoutNode? GetLayoutNode(IWindowControl control)
		{
			return _renderer?.GetLayoutNode(control);
		}

		/// <summary>
		/// Creates a portal overlay for the specified control.
		/// Portal content renders on top of all normal content with no parent clipping.
		/// Portals are useful for dropdowns, tooltips, context menus, and other overlay content.
		/// </summary>
		/// <param name="ownerControl">The control creating the portal.</param>
		/// <param name="portalContent">The content to render as an overlay.</param>
		/// <returns>The portal LayoutNode for later removal, or null if owner not found.</returns>
		public LayoutNode? CreatePortal(IWindowControl ownerControl, IWindowControl portalContent)
		{
			if (UseDesktopPortals && _windowSystem != null)
			{
				return CreateDesktopPortal(ownerControl, portalContent);
			}

			var portalNode = _renderer?.CreatePortal(ownerControl, portalContent);
			if (portalNode != null)
			{
				Invalidate(false);
			}
			return portalNode;
		}

		/// <summary>
		/// Removes a portal overlay created by CreatePortal().
		/// </summary>
		/// <param name="ownerControl">The control that owns the portal.</param>
		/// <param name="portalNode">The portal LayoutNode returned by CreatePortal().</param>
		public void RemovePortal(IWindowControl ownerControl, LayoutNode portalNode)
		{
			if (UseDesktopPortals && _windowSystem != null)
			{
				RemoveDesktopPortal(ownerControl, portalNode);
				return;
			}

			_renderer?.RemovePortal(ownerControl, portalNode);
			Invalidate(false);
		}

		// Desktop portal tracking: maps LayoutNode → DesktopPortal for cleanup
		private readonly Dictionary<LayoutNode, Core.DesktopPortal> _desktopPortalMap = new();

		private LayoutNode? CreateDesktopPortal(IWindowControl ownerControl, IWindowControl portalContent)
		{
			// Get portal bounds in window-content-relative coordinates
			System.Drawing.Rectangle portalBounds;
			if (portalContent is IHasPortalBounds positioned)
			{
				portalBounds = positioned.GetPortalBounds();
			}
			else
			{
				var size = portalContent.GetLogicalContentSize();
				portalBounds = new System.Drawing.Rectangle(0, 0, size.Width, size.Height);
			}

			// The content (e.g., MenuPortalContent) paints using dropdown.Bounds which are
			// in window-content-relative coordinates. To make those coordinates valid in the
			// desktop portal buffer, we use a buffer that covers the full window content area
			// with BufferOrigin at the window's screen content origin.
			int contentOriginX = Left + 1; // +1 for border
			int contentOriginY = Top + 1;
			int contentWidth = Width - 2;
			int contentHeight = Height - 2;

			// Screen-absolute bounds of the portal content
			int screenX = contentOriginX + portalBounds.X;
			int screenY = contentOriginY + portalBounds.Y;
			var screenBounds = new System.Drawing.Rectangle(
				screenX, screenY, portalBounds.Width, portalBounds.Height);

			// Use the full desktop as the buffer so submenus can extend in any direction.
			// BufferOrigin maps buffer (0,0) to the window's content origin, so
			// window-content-relative coordinates (used by dropdown.Bounds) work directly.
			var desktopDims = _windowSystem!.DesktopDimensions;
			var bufferSize = new System.Drawing.Size(desktopDims.Width, desktopDims.Height);
			var bufferOrigin = new System.Drawing.Point(contentOriginX, contentOriginY);

			var desktopPortal = _windowSystem.DesktopPortalService.CreatePortal(
				new Core.DesktopPortalOptions(
					Content: portalContent,
					Bounds: screenBounds,
					DismissOnClickOutside: false,
					ConsumeClickOnDismiss: false,
					DimBackground: false,
					Owner: ownerControl,
					BufferSize: bufferSize,
					BufferOrigin: bufferOrigin));

			_desktopPortalMap[desktopPortal.RootNode] = desktopPortal;
			return desktopPortal.RootNode;
		}

		private void RemoveDesktopPortal(IWindowControl ownerControl, LayoutNode portalNode)
		{
			if (_desktopPortalMap.TryGetValue(portalNode, out var desktopPortal))
			{
				_desktopPortalMap.Remove(portalNode);
				_windowSystem!.DesktopPortalService.RemovePortal(desktopPortal);
			}
		}

		/// <summary>
		/// Removes all desktop portals created by this window. Called on window close.
		/// </summary>
		internal void CleanupDesktopPortals()
		{
			if (_desktopPortalMap.Count == 0) return;

			foreach (var desktopPortal in _desktopPortalMap.Values.ToList())
			{
				_windowSystem?.DesktopPortalService.RemovePortal(desktopPortal);
			}
			_desktopPortalMap.Clear();
		}

		/// <summary>
		/// Dismisses all portals that have DismissOnOutsideClick enabled.
		/// Called on window deactivation and can be called programmatically.
		/// </summary>
		internal void DismissAutoClosePortals()
		{
			var root = RootLayoutNode;
			if (root == null) return;

			var toDismiss = new List<LayoutNode>();

			root.Visit(node =>
			{
				foreach (var portal in node.PortalChildren)
				{
					if (portal.Control is Layout.IHasPortalBounds hasPortalBounds
						&& hasPortalBounds.DismissOnOutsideClick)
					{
						toDismiss.Add(portal);
					}
				}
			});

			foreach (var portal in toDismiss)
			{
				if (portal.Control is Controls.PortalContentBase portalContent)
					portalContent.RaiseDismissRequested();
				if (portal.Control != null)
					RemovePortal(portal.Control, portal);
			}
		}

		/// <summary>
		/// Gets the root layout node for this window.
		/// </summary>
		public LayoutNode? RootLayoutNode => _renderer?.RootLayoutNode;

		/// <summary>
		/// Gets whether DOM-based layout is enabled.
		/// DOM layout is always enabled and is the only rendering path.
		/// </summary>
		[Obsolete("DOM layout is now always enabled. This property will be removed.")]
		public bool UseDOMLayout => true;

		/// <summary>
		/// Rebuilds the DOM tree from the current controls.
		/// </summary>
		internal void RebuildDOMTree()
		{
			var contentWidth = Width - 2;
			var contentHeight = Height - 2;
			_renderer?.RebuildDOMTree(_controls, contentWidth, contentHeight);
		}


		/// <summary>
		/// Performs the measure and arrange passes on the DOM tree.
		/// </summary>
		private void PerformDOMLayout()
		{
			var contentWidth = Width - 2;
			var contentHeight = Height - 2;
			_renderer?.PerformDOMLayout(contentWidth, contentHeight);
		}

		/// <summary>
		/// Paints the DOM tree to the character buffer.
		/// </summary>
		/// <param name="clipRect">The clipping rectangle in window-space coordinates. Only content within this rect will be painted.</param>
		private void PaintDOM(LayoutRect clipRect)
		{
			_renderer?.PaintDOM(clipRect, BackgroundColor);
		}

		/// <summary>
		/// Invalidates the DOM layout, triggering a re-measure and re-arrange.
		/// </summary>
		private void InvalidateDOMLayout()
		{
			_renderer?.InvalidateDOMLayout();
		}


		/// <summary>
		/// Rebuilds the content buffer (DOM + paint) without converting to ANSI strings.
		/// </summary>
		private void RebuildContentBufferOnly(int availableWidth, int availableHeight, List<Rectangle>? visibleRegions = null)
		{
			if (_renderer == null)
			{
				_invalidated = false;
				return;
			}

			_renderer.RebuildContentBuffer(
				_controls,
				availableWidth,
				availableHeight,
				visibleRegions,
				Left,
				Top,
				ShowTitle,
				BackgroundColor);

			PostRebuildCleanup();
		}

		private void PostRebuildCleanup()
		{
			// Clear sticky tracking (DOM handles sticky internally)
			_topStickyLines.Clear();
			_topStickyHeight = 0;
			_bottomStickyLines.Clear();
			_bottomStickyHeight = 0;
			_controlPositions.Clear();

			_invalidated = false;
		}

		#endregion
	}
}
