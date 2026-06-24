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
		/// Gets the screen position (window-relative cell) of the logical cursor of the given control, as it
		/// is actually painted — accumulating every layout offset (container nesting such as a TabControl's
		/// header, scroll offsets) and the window's own frame/title inset. Use this to anchor overlays (e.g. a
		/// completion popup) directly at the caret, rather than deriving a position from a control's
		/// <c>ActualX</c>/<c>ActualY</c> (which are in window-CONTENT coordinates and omit the window inset and
		/// some intermediate chrome).
		/// </summary>
		/// <param name="control">A control that provides a logical cursor (implements <c>ILogicalCursorProvider</c>).</param>
		/// <returns>The window-relative screen cell of the cursor, or null if the control has no cursor /
		/// is scrolled out of view / is not laid out.</returns>
		public System.Drawing.Point? GetCursorScreenPosition(IWindowControl control)
			=> _layoutManager.TranslateLogicalCursorToWindow(control);

		/// <summary>
		/// Gets the window-CONTENT-relative cell (origin at the content area, excluding the window's
		/// frame/title inset) of the logical cursor of the given control, as it is actually painted —
		/// accumulating every layout offset (container nesting such as a TabControl's header, scroll
		/// offsets). This is the coordinate space that <see cref="CreatePortal"/> arranges portal content
		/// in, so it is the correct anchor for a completion popup or similar overlay. Prefer this over
		/// <see cref="GetCursorScreenPosition"/> when feeding a portal's positioner: the window-relative
		/// variant includes the inset, which the portal layout would then add a second time.
		/// </summary>
		/// <param name="control">A control that provides a logical cursor (implements <c>ILogicalCursorProvider</c>).</param>
		/// <returns>The content-relative cell of the cursor, or null if the control has no cursor /
		/// is scrolled out of view / is not laid out.</returns>
		public System.Drawing.Point? GetCursorContentPosition(IWindowControl control)
			=> _layoutManager.TranslateLogicalCursorToContent(control);

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
				Invalidate(Invalidation.Relayout);
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
			Invalidate(Invalidation.Relayout);
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
			int contentOriginX = ContentOrigin.X;
			int contentOriginY = ContentOrigin.Y;
			int contentWidth = ContentWidth;
			int contentHeight = ContentHeight;

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
			// FramelessLayoutWidth reserves the scrollbar column ONLY for an overflowing frameless
			// window; the reservation is derived from TotalLines (measured at full width on the prior
			// build), so it is width-stable and cannot oscillate. Non-frameless windows are unaffected.
			var contentWidth = FramelessLayoutWidth(TotalLines);
			var contentHeight = ContentHeight;
			_renderer?.RebuildDOMTree(_controls, contentWidth, contentHeight);
		}


		/// <summary>
		/// Performs the measure and arrange passes on the DOM tree.
		/// </summary>
		private void PerformDOMLayout()
		{
			// FramelessLayoutWidth reserves the scrollbar column ONLY for an overflowing frameless
			// window; the reservation is derived from TotalLines (measured at full width on the prior
			// build), so it is width-stable and cannot oscillate. Non-frameless windows are unaffected.
			var contentWidth = FramelessLayoutWidth(TotalLines);
			var contentHeight = ContentHeight;
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
				// No renderer: do NOT consume the pending work — leave PendingWork set so the next tick (once a
				// renderer exists) re-enters and rebuilds. Do NOT clear PendingWork here.
				return;
			}

			// Consume = atomic snapshot-and-clear of the frame intent, BEFORE any work. This is the ONLY
			// consume point; the accumulator resets to None here and is re-raised by any later Request.
			var work = (FrameWork)Interlocked.Exchange(ref _pendingWork, (int)FrameWork.None);
			LastFrameRequests = Interlocked.Exchange(ref _requestsThisFrame, 0);

			// A None snapshot means nothing was pending (e.g. an unconditional rebuild call); still paint at
			// Repaint fidelity so the buffer stays correct, but never re-measure for free.
			if (work == FrameWork.None)
				work = FrameWork.Repaint;

			_renderer.RebuildContentBuffer(
				_controls,
				availableWidth,
				availableHeight,
				visibleRegions,
				Left,
				Top,
				ShowTitle,
				BackgroundColor,
				work);

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
		}

		#endregion
	}
}
