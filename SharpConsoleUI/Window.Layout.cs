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
using Color = Spectre.Console.Color;

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
			_renderer?.RemovePortal(ownerControl, portalNode);
			Invalidate(false);
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
		/// Rebuilds the content cache using DOM-based layout.
		/// Converts the CharacterBuffer output to line-based format for compatibility.
		/// </summary>
		private void RebuildContentCacheDOM(int availableWidth, int availableHeight, List<Rectangle>? visibleRegions = null)
		{
			if (_renderer == null)
			{
				_cachedContent = new List<string>();
				_invalidated = false;
				return;
			}

			// Delegate to renderer for complete rendering pipeline
			_cachedContent = _renderer.RebuildContentCacheDOM(
				_controls,
				availableWidth,
				availableHeight,
				visibleRegions,
				Left,
				Top,
				ShowTitle,
				ForegroundColor,
				BackgroundColor);

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
