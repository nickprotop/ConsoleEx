// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Performance;
using SharpConsoleUI.Themes;
using System.Drawing;

namespace SharpConsoleUI.Rendering
{
	/// <summary>
	/// Coordinates all rendering operations for the console window system.
	/// Handles window rendering, status bar display, caching, and performance metrics.
	/// Extracted from ConsoleWindowSystem as part of Phase 1.2 refactoring.
	/// </summary>
	public class RenderCoordinator
	{
		// Dependencies (injected via constructor)
		private readonly IConsoleDriver _consoleDriver;
		private readonly Renderer _renderer;
		private readonly WindowStateService _windowStateService;
		#pragma warning disable CS0612, CS0618 // Type or member is obsolete
		private readonly StatusBarStateService _statusBarStateService;
	#pragma warning restore CS0612, CS0618
		private readonly ILogService _logService;
		private readonly ConsoleWindowSystem _windowSystemContext;
		private readonly Func<ConsoleWindowSystemOptions> _getOptions;
		private readonly PerformanceTracker _performanceTracker;

		// Performance optimization: cached collections to avoid allocations in hot paths
		private readonly HashSet<Window> _windowsToRender = new HashSet<Window>();
		private readonly List<Window> _sortedWindows = new List<Window>();
		private readonly Dictionary<string, bool> _coverageCache = new Dictionary<string, bool>();
		private readonly List<Rectangle> _pendingDesktopClears = new List<Rectangle>();

		// Coverage cache invalidation: only recompute when window layout changes
		private long _lastCoverageLayoutHash;

		// Desktop rendering flag - forces render even when no windows are dirty
		// Used when desktop background changes (e.g., after closing last window)
		private bool _desktopNeedsRender = false;

		/// <summary>
		/// Gets or sets whether the desktop needs to render on the next frame.
		/// This forces UpdateDisplay() to run even when no windows are dirty.
		/// Automatically cleared after rendering.
		/// </summary>
		public bool DesktopNeedsRender
		{
			get => _desktopNeedsRender;
			set => _desktopNeedsRender = value;
		}

		// Status bar caching
		private string? _cachedBottomStatus;
		private string? _cachedTopStatus;
		private string? _cachedTaskBar;
		private int _taskBarWindowCount;
		private int _taskBarStateHash;

		// Panel visibility tracking (used to skip rendering hidden panels)
		private bool _lastTopPanelVisible;
		private bool _lastBottomPanelVisible;

		// Render lock for thread safety
		private readonly object _renderLock = new object();

		// Track windows needing region update
		private readonly HashSet<Window> _windowsNeedingRegionUpdate = new();

		// Pooled collections to avoid per-frame allocations
		private readonly List<Rectangle> _clearsCopyPool = new List<Rectangle>();
		private readonly List<Window> _overlappingClearsPool = new List<Window>();
		private readonly List<Window> _topLevelWindowsPool = new List<Window>();

		/// <summary>
		/// Initializes a new instance of the RenderCoordinator class.
		/// </summary>
		/// <param name="consoleDriver">Console driver for low-level I/O.</param>
		/// <param name="renderer">Renderer for window and content rendering.</param>
		/// <param name="windowStateService">Service managing window state and Z-order.</param>
		/// <param name="statusBarStateService">Service managing status bar state and Start menu.</param>
		/// <param name="logService">Service for debug logging.</param>
		/// <param name="windowSystemContext">Context providing access to window system properties.</param>
		/// <param name="getOptions">Getter for current configuration options (allows runtime changes).</param>
		/// <param name="performanceTracker">Performance metrics tracker.</param>
#pragma warning disable CS0612, CS0618 // Type or member is obsolete
		public RenderCoordinator(
			IConsoleDriver consoleDriver,
			Renderer renderer,
			WindowStateService windowStateService,
			StatusBarStateService statusBarStateService,
			ILogService logService,
			ConsoleWindowSystem windowSystemContext,
			Func<ConsoleWindowSystemOptions> getOptions,
			PerformanceTracker performanceTracker)
#pragma warning restore CS0612, CS0618
		{
			_consoleDriver = consoleDriver ?? throw new ArgumentNullException(nameof(consoleDriver));
			_renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
			_windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
			_statusBarStateService = statusBarStateService ?? throw new ArgumentNullException(nameof(statusBarStateService));
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_windowSystemContext = windowSystemContext ?? throw new ArgumentNullException(nameof(windowSystemContext));
			_getOptions = getOptions ?? throw new ArgumentNullException(nameof(getOptions));
			_performanceTracker = performanceTracker ?? throw new ArgumentNullException(nameof(performanceTracker));
		}

		#region Public Properties

		/// <summary>
		/// Gets the top status bar bounds for mouse hit testing.
		/// Returns empty rectangle — panels handle bounds internally.
		/// </summary>
		public Rectangle TopStatusBarBounds => Rectangle.Empty;

		/// <summary>
		/// Gets the bottom status bar bounds for mouse hit testing.
		/// Returns empty rectangle — panels handle bounds internally.
		/// </summary>
		public Rectangle BottomStatusBarBounds => Rectangle.Empty;

		/// <summary>
		/// Gets the start button bounds for mouse hit testing.
		/// Returns empty rectangle — panels handle bounds internally.
		/// </summary>
		public Rectangle StartButtonBounds => Rectangle.Empty;

		#endregion

		#region Public Methods

		/// <summary>
		/// Gets the height occupied by the top status bar (0 or 1).
		/// Accounts for panels, status text, and performance metrics.
		/// </summary>
		public int GetTopStatusHeight()
		{
			var panelService = _windowSystemContext.PanelStateService;
			var topPanel = panelService.TopPanel;
			if (topPanel != null)
				return topPanel.Height;
			return panelService.ShowTopPanel && (!string.IsNullOrEmpty(panelService.TopStatus) || _getOptions().EnablePerformanceMetrics) ? 1 : 0;
		}

		/// <summary>
		/// Gets the height occupied by the bottom status bar (0 or 1).
		/// Accounts for panels, status text, and Start button.
		/// </summary>
		public int GetBottomStatusHeight()
		{
			var panelService = _windowSystemContext.PanelStateService;
			var bottomPanel = panelService.BottomPanel;
			if (bottomPanel != null)
				return bottomPanel.Height;
			bool hasContent = !string.IsNullOrEmpty(panelService.BottomStatus) || _getOptions().StatusBar.ShowTaskBar;
			bool hasStartButton = _getOptions().StatusBar.ShowStartButton && _getOptions().StatusBar.StartButtonLocation == Configuration.StatusBarLocation.Bottom;
			return panelService.ShowBottomPanel && (hasContent || hasStartButton) ? 1 : 0;
		}

		/// <summary>
		/// Returns true if status bar content has changed since last render.
		/// Compares current state against cached values without triggering a render.
		/// </summary>
		public bool IsStatusBarDirty() => _windowSystemContext.PanelStateService.IsDirty;

		/// <summary>
		/// Invalidates all status bar caches, forcing them to be rebuilt on next render.
		/// Call this when window state, titles, or themes change.
		/// </summary>
		public void InvalidateStatusCache()
		{
			_cachedBottomStatus = null;
			_cachedTopStatus = null;
			_cachedTaskBar = null;

			// Also mark panels dirty so they re-render
			var panelService = _windowSystemContext.PanelStateService;
			panelService.TopPanel?.MarkDirty();
			panelService.BottomPanel?.MarkDirty();
			panelService.MarkDirty();
		}

		/// <summary>
		/// Restores screen regions that were covered by portal control bounds.
		/// Called by DesktopPortalService when portals are removed.
		/// </summary>
		/// <param name="portal">The portal being removed (provides bounds and control bounds).</param>
		public void RestorePortalRegions(Core.DesktopPortal portal)
		{
			if (portal.ControlBounds.Count == 0)
				return;

			foreach (var bounds in portal.ControlBounds)
			{
				var screenRect = new Rectangle(
					portal.BufferOrigin.X + bounds.X,
					portal.BufferOrigin.Y + bounds.Y,
					bounds.Width,
					bounds.Height);
				RestoreScreenRegion(screenRect, belowPortal: portal);
			}
		}

		/// <summary>
		/// Invalidates all windows and status bars, forcing a complete redraw.
		/// Call this after theme changes, status bar visibility changes, or other global UI updates.
		/// </summary>
		public void InvalidateAllWindows()
		{
			InvalidateStatusCache();
			foreach (var w in _windowSystemContext.Windows.Values)
			{
				w.Invalidate(true);
			}
		}

		/// <summary>
		/// Updates the status bar bounds based on current screen size and configuration.
		/// No-op — panels handle bounds internally.
		/// </summary>
		public void UpdateStatusBarBounds()
		{
			// No-op — panels handle bounds internally
		}

		/// <summary>
		/// Adds a desktop area to be cleared on the next render.
		/// Used for atomic clearing of old window positions.
		/// </summary>
		/// <param name="rect">The rectangle to clear.</param>
		public void AddPendingDesktopClear(Rectangle rect)
		{
			_pendingDesktopClears.Add(rect);
		}

		/// <summary>
		/// Adds a window to the set of windows needing region update.
		/// </summary>
		/// <param name="window">The window needing a region update.</param>
		public void AddWindowNeedingRegionUpdate(Window window)
		{
			_windowsNeedingRegionUpdate.Add(window);
		}

		/// <summary>
		/// Main rendering orchestrator.
		/// Renders all dirty windows, status bars, and flushes to console.
		/// </summary>
		public void UpdateDisplay()
		{
			// Begin new frame for diagnostics tracking
			_windowSystemContext.RenderingDiagnostics?.BeginFrame();

			lock (_renderLock)
			{
			// ATOMIC DESKTOP CLEARING: Clear old window positions before rendering
			// FIX: Calculate visible regions to avoid overwriting windows below (prevents empty regions bug)
			if (_pendingDesktopClears.Count > 0)
			{
				// Copy list to avoid race condition (mouse events can add during iteration)
				_clearsCopyPool.Clear();
				_clearsCopyPool.AddRange(_pendingDesktopClears);
				_pendingDesktopClears.Clear();

				foreach (var clearRect in _clearsCopyPool)
				{
					// Find all visible windows that overlap with clear area
					_overlappingClearsPool.Clear();
					foreach (var w in _windowSystemContext.Windows.Values)
					{
						if (w.State != WindowState.Minimized &&
						    GeometryHelpers.DoesRectangleIntersect(clearRect,
						        new Rectangle(w.Left, w.Top, w.Width, w.Height)))
						{
							_overlappingClearsPool.Add(w);
						}
					}
					var overlappingWindows = _overlappingClearsPool;

					// Calculate visible regions (areas NOT covered by windows)
					var visibleRegions = _windowSystemContext.VisibleRegions
						.CalculateVisibleRegions(clearRect, overlappingWindows);

					// Only clear visible regions (never overwrite windows!)
					foreach (var region in visibleRegions)
					{
						_renderer.FillRect(region.Left, region.Top, region.Width, region.Height,
							_windowSystemContext.Theme.DesktopBackgroundChar,
							_windowSystemContext.Theme.DesktopBackgroundColor,
							_windowSystemContext.Theme.DesktopForegroundColor);
					}
				}
			}

				// RENDERING ORDER:
				// 1. Windows first (so we can measure their dirty chars)
				// 2. Capture dirty chars (after windows, before TopStatus)
				// 3. TopStatus (with captured metrics, doesn't pollute measurement)
				// 4. BottomStatus
				// 5. Flush

				RenderWindows();

				// Render desktop portals on top of windows (before status bars)
				RenderDesktopPortals();

				// CRITICAL: Capture dirty chars AFTER windows rendered, BEFORE TopStatus
				// This measures window rendering work without including TopStatus itself
				if (_getOptions().EnablePerformanceMetrics)
				{
					_performanceTracker.SetDirtyChars(_consoleDriver.GetDirtyCharacterCount());
				}

				// Render panels or fall back to legacy status bar rendering
				var topPanel = _windowSystemContext.TopPanel;
				var bottomPanel = _windowSystemContext.BottomPanel;

				if (topPanel != null || bottomPanel != null)
				{
					// Sync legacy StatusTextElement from StatusBarStateService (backward compat)
					SyncLegacyPanelElements(topPanel);

					RenderPanel(topPanel, 0,
						_windowSystemContext.Theme.TopBarForegroundColor,
						_windowSystemContext.Theme.TopBarBackgroundColor,
						ref _lastTopPanelVisible);
					RenderPanel(bottomPanel, _consoleDriver.ScreenSize.Height - 1,
						_windowSystemContext.Theme.BottomBarForegroundColor,
						_windowSystemContext.Theme.BottomBarBackgroundColor,
						ref _lastBottomPanelVisible);
				}
				else
				{
					RenderTopStatus();
					RenderBottomStatus();
				}

				// Clear status bar dirty flag after rendering
				_windowSystemContext.PanelStateService.ClearDirty();

				// Update status bar bounds for mouse click detection
				UpdateStatusBarBounds();

				// Clear the region update set for next frame
				_windowsNeedingRegionUpdate.Clear();
				_consoleDriver.Flush();

				// Clear desktop render flag now that we've rendered
				// (always clear it, as it was used to trigger this render if no windows were dirty)
				_desktopNeedsRender = false;
			}
		}

		#endregion

		#region Private Helper Methods

		/// <summary>
		/// Restores a screen-space rectangle to its correct visual state by blitting
		/// desktop background, existing window buffers, and lower portal buffers.
		/// No windows are re-rendered — only cached buffers are used.
		/// </summary>
		/// <param name="screenRect">Screen-space rectangle to restore.</param>
		/// <param name="belowPortal">If set, only re-blit portals with ZOrder below this portal.</param>
		private void RestoreScreenRegion(Rectangle screenRect, Core.DesktopPortal? belowPortal = null)
		{
			var desktopUpperLeft = _windowSystemContext.DesktopUpperLeft;
			var desktopBottomRight = _windowSystemContext.DesktopBottomRight;
			var theme = _windowSystemContext.Theme;

			// Clip to desktop area
			int clipLeft = Math.Max(screenRect.Left, desktopUpperLeft.X);
			int clipTop = Math.Max(screenRect.Top, desktopUpperLeft.Y);
			int clipRight = Math.Min(screenRect.Right, desktopBottomRight.X + 1);
			int clipBottom = Math.Min(screenRect.Bottom, desktopBottomRight.Y + 1);

			int clippedWidth = clipRight - clipLeft;
			int clippedHeight = clipBottom - clipTop;
			if (clippedWidth <= 0 || clippedHeight <= 0)
				return;

			var clippedRect = new Rectangle(clipLeft, clipTop, clippedWidth, clippedHeight);

			// 1. Fill with desktop background
			// FillRect takes desktop-relative coords and adds DesktopUpperLeft.Y internally
			_renderer.FillRect(
				clipLeft, clipTop - desktopUpperLeft.Y,
				clippedWidth, clippedHeight,
				theme.DesktopBackgroundChar, theme.DesktopBackgroundColor, theme.DesktopForegroundColor);

			// 2. Re-blit overlapping windows from cached buffers (z-order ascending)
			_sortedWindows.Clear();
			_sortedWindows.AddRange(_windowSystemContext.Windows.Values);
			_sortedWindows.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

			foreach (var window in _sortedWindows)
			{
				if (window.State == WindowState.Minimized)
					continue;

				var windowScreenRect = new Rectangle(
					window.Left, window.Top + desktopUpperLeft.Y,
					window.Width, window.Height);

				if (!windowScreenRect.IntersectsWith(clippedRect))
					continue;

				// Re-blit borders for the intersection
				var intersection = Rectangle.Intersect(windowScreenRect, clippedRect);
				if (window.BorderRenderer != null)
				{
					window.BorderRenderer.RenderBorders(new List<Rectangle> { intersection });
				}

				// Re-blit content from cached buffer
				var buffer = window.ContentBuffer;
				if (buffer == null)
					continue;

				// Content area is inside the window borders (Left+1, Top+1) to (Left+Width-1, Top+Height-1)
				int contentScreenLeft = window.Left + 1;
				int contentScreenTop = window.Top + desktopUpperLeft.Y + 1;
				int contentScreenRight = window.Left + window.Width - 1;
				int contentScreenBottom = window.Top + desktopUpperLeft.Y + window.Height - 1;

				// Intersect content area with the region to restore
				int srcLeft = Math.Max(contentScreenLeft, clippedRect.Left);
				int srcTop = Math.Max(contentScreenTop, clippedRect.Top);
				int srcRight = Math.Min(contentScreenRight, clippedRect.Right);
				int srcBottom = Math.Min(contentScreenBottom, clippedRect.Bottom);

				int width = srcRight - srcLeft;
				int height = srcBottom - srcTop;
				if (width <= 0 || height <= 0)
					continue;

				int bufX = srcLeft - contentScreenLeft;
				int bufY = srcTop - contentScreenTop;

				for (int row = 0; row < height; row++)
				{
					if (bufY + row >= buffer.Height)
						break;
					_consoleDriver.WriteBufferRegion(
						srcLeft, srcTop + row,
						buffer,
						bufX, bufY + row,
						width,
						window.BackgroundColor);
				}
			}

			// 3. Re-blit lower portal control regions that intersect
			var portalService = _windowSystemContext.DesktopPortalService;
			foreach (var portal in portalService.Portals)
			{
				if (belowPortal != null && portal.ZOrder >= belowPortal.ZOrder)
					continue;

				if (portal.Buffer == null || portal.ControlBounds.Count == 0)
					continue;

				foreach (var bounds in portal.ControlBounds)
				{
					int portalScreenX = portal.BufferOrigin.X + bounds.X;
					int portalScreenY = portal.BufferOrigin.Y + bounds.Y;
					var portalScreenRect = new Rectangle(portalScreenX, portalScreenY, bounds.Width, bounds.Height);

					if (!portalScreenRect.IntersectsWith(clippedRect))
						continue;

					var inter = Rectangle.Intersect(portalScreenRect, clippedRect);
					int pSrcX = bounds.X + (inter.Left - portalScreenX);
					int pSrcY = bounds.Y + (inter.Top - portalScreenY);

					for (int row = 0; row < inter.Height; row++)
					{
						_consoleDriver.WriteBufferRegion(
							inter.Left, inter.Top + row,
							portal.Buffer,
							pSrcX, pSrcY + row,
							inter.Width,
							theme.DesktopBackgroundColor);
					}
				}
			}
		}

		/// <summary>
		/// Renders all desktop portals. Portals paint their control regions on top of window content.
		/// Only regions where controls exist are rendered — areas without controls are transparent.
		/// When control bounds change (submenu open/close), delta regions are restored from cached buffers.
		/// </summary>
		private void RenderDesktopPortals()
		{
			var portalService = _windowSystemContext.DesktopPortalService;
			if (!portalService.HasPortals)
				return;

			foreach (var portal in portalService.Portals)
			{
				if (!portal.IsDirty && portal.Buffer != null)
				{
					// Portal is clean — but we still need to repaint its control regions
					// in case underlying windows were re-rendered and overwrote portal pixels
					WritePortalControlRegions(portal);
					continue;
				}

				// Swap control bounds lists to avoid allocation:
				// PreviousControlBounds gets the current bounds, ControlBounds gets recycled
				var previousBounds = portal.ControlBounds;
				portal.ControlBounds = portal.PreviousControlBounds;
				portal.ControlBounds.Clear();
				portal.PreviousControlBounds = previousBounds;

				// Rebuild the portal's content buffer
				var bufSize = portal.BufferSize;
				if (bufSize.Width <= 0 || bufSize.Height <= 0)
					continue;

				// Create or reuse buffer (uses BufferSize which may be larger than Bounds)
				if (portal.Buffer == null || portal.Buffer.Width != bufSize.Width || portal.Buffer.Height != bufSize.Height)
				{
					portal.Buffer = new Layout.CharacterBuffer(bufSize.Width, bufSize.Height, _windowSystemContext.Theme.DesktopBackgroundColor);
				}
				else
				{
					portal.Buffer.Clear(_windowSystemContext.Theme.DesktopBackgroundColor);
				}

				// Re-measure and arrange the DOM
				// Arrange at portal bounds so root control fills the declared area
				var constraints = Layout.LayoutConstraints.Loose(bufSize.Width, bufSize.Height);
				portal.RootNode.Measure(constraints);
				int rootOffX = portal.Bounds.X - portal.BufferOrigin.X;
				int rootOffY = portal.Bounds.Y - portal.BufferOrigin.Y;
				portal.RootNode.Arrange(new Layout.LayoutRect(rootOffX, rootOffY, portal.Bounds.Width, portal.Bounds.Height));

				// Paint DOM to buffer (clip to full buffer so submenus can render beyond content bounds)
				var clipRect = new Layout.LayoutRect(0, 0, bufSize.Width, bufSize.Height);
				portal.RootNode.Paint(portal.Buffer, clipRect);

				// Collect control bounds for selective rendering
				portal.ControlBounds.Clear();
				portal.RootNode.Visit(node =>
				{
					if (node.Control != null && !node.AbsoluteBounds.IsEmpty)
					{
						portal.ControlBounds.Add(node.AbsoluteBounds);
					}
				});

				// If DimBackground, dim cells outside control bounds
				if (portal.DimBackground)
				{
					RenderPortalDimming(portal);
				}

				// Restore delta regions: areas in previous bounds no longer covered by new bounds
				RestorePortalDelta(portal, previousBounds);

				// Write only control-bound regions to the console driver
				WritePortalControlRegions(portal);

				portal.IsDirty = false;
			}
		}

		/// <summary>
		/// Restores screen regions that were covered by old portal control bounds
		/// but are no longer covered by the new control bounds.
		/// </summary>
		private void RestorePortalDelta(Core.DesktopPortal portal, List<Layout.LayoutRect> previousBounds)
		{
			if (previousBounds.Count == 0)
				return;

			foreach (var prevRect in previousBounds)
			{
				// Check if this previous rect is fully covered by any new control bound
				bool covered = false;
				foreach (var newRect in portal.ControlBounds)
				{
					if (newRect.X <= prevRect.X && newRect.Y <= prevRect.Y &&
						newRect.X + newRect.Width >= prevRect.X + prevRect.Width &&
						newRect.Y + newRect.Height >= prevRect.Y + prevRect.Height)
					{
						covered = true;
						break;
					}
				}

				if (!covered)
				{
					// Convert to screen space and restore
					var screenRect = new Rectangle(
						portal.BufferOrigin.X + prevRect.X,
						portal.BufferOrigin.Y + prevRect.Y,
						prevRect.Width,
						prevRect.Height);
					RestoreScreenRegion(screenRect, belowPortal: portal);
				}
			}
		}

		/// <summary>
		/// Writes a portal's control regions to the console driver.
		/// Only regions where controls exist are written — areas without controls are transparent.
		/// </summary>
		private void WritePortalControlRegions(Core.DesktopPortal portal)
		{
			if (portal.Buffer == null || portal.ControlBounds.Count == 0)
				return;

			var desktopUpperLeft = _windowSystemContext.DesktopUpperLeft;
			var desktopBottomRight = _windowSystemContext.DesktopBottomRight;

			foreach (var bounds in portal.ControlBounds)
			{
				int screenX = portal.BufferOrigin.X + bounds.X;
				int screenY = portal.BufferOrigin.Y + bounds.Y;

				// Clip to desktop area
				int clipLeft = Math.Max(screenX, desktopUpperLeft.X);
				int clipTop = Math.Max(screenY, desktopUpperLeft.Y);
				int clipRight = Math.Min(screenX + bounds.Width, desktopBottomRight.X + 1);
				int clipBottom = Math.Min(screenY + bounds.Height, desktopBottomRight.Y + 1);

				int clippedWidth = clipRight - clipLeft;
				int clippedHeight = clipBottom - clipTop;

				if (clippedWidth <= 0 || clippedHeight <= 0)
					continue;

				// Source offset in buffer
				int baseSrcX = bounds.X + (clipLeft - screenX);
				int baseSrcY = bounds.Y + (clipTop - screenY);

				// WriteBufferRegion writes one row at a time — loop over each row
				for (int row = 0; row < clippedHeight; row++)
				{
					_consoleDriver.WriteBufferRegion(
						clipLeft, clipTop + row,
						portal.Buffer,
						baseSrcX, baseSrcY + row,
						clippedWidth,
						_windowSystemContext.Theme.DesktopBackgroundColor);
				}
			}
		}

		/// <summary>
		/// Dims the screen area behind a portal (outside control bounds).
		/// Reads existing cells and writes them back with reduced brightness.
		/// </summary>
		private void RenderPortalDimming(Core.DesktopPortal portal)
		{
			// Dimming is a visual effect that darkens cells not covered by portal controls.
			// For now, we skip this — it requires reading back from the console driver
			// which not all drivers support. This can be implemented as a follow-up.
			// The portal still renders correctly without dimming.
		}

		/// <summary>
		/// Renders a panel at the specified screen row, or clears the row if the panel just became hidden.
		/// </summary>
		/// <param name="panel">The panel to render (may be null or hidden).</param>
		/// <param name="y">Screen row.</param>
		/// <param name="themeFg">Theme foreground color.</param>
		/// <param name="themeBg">Theme background color.</param>
		/// <param name="lastVisible">Tracked visibility state — updated by this method.</param>
		private void RenderPanel(Panel.Panel? panel, int y, Color themeFg, Color themeBg, ref bool lastVisible)
		{
			bool currentlyVisible = panel != null && panel.Visible;
			lastVisible = currentlyVisible;

			if (!currentlyVisible)
				return;

			// Always mark taskbar panels dirty when panel state is dirty (window changes)
			if (_windowSystemContext.PanelStateService.IsDirty)
				panel!.MarkDirty();

			if (!panel!.IsDirty)
				return;

			var buffer = new Layout.CharacterBuffer(_consoleDriver.ScreenSize.Width, 1, themeBg);
			panel.Render(buffer, 0, _consoleDriver.ScreenSize.Width, themeFg, themeBg);
			_consoleDriver.WriteBufferRegion(0, y, buffer, 0, 0, buffer.Width, themeBg);
		}

		/// <summary>
		/// Syncs legacy StatusTextElement from StatusBarStateService for backward compatibility.
		/// </summary>
		private void SyncLegacyPanelElements(Panel.Panel? topPanel)
		{
			if (topPanel?.FindElement<Panel.StatusTextElement>("legacyTopStatus") is { } el)
			{
				var currentText = _windowSystemContext.PanelStateService.TopStatus ?? string.Empty;
				if (el.Text != currentText)
					el.Text = currentText;
			}
		}

		/// <summary>
		/// Returns true if the top status bar should be rendered.
		/// </summary>
		private bool ShouldRenderTopStatus()
		{
			var panelService = _windowSystemContext.PanelStateService;
			return panelService.ShowTopPanel && (!string.IsNullOrEmpty(panelService.TopStatus) || _getOptions().EnablePerformanceMetrics);
		}

		/// <summary>
		/// Returns true if the bottom status bar should be rendered.
		/// </summary>
		private bool ShouldRenderBottomStatus()
		{
			var panelService = _windowSystemContext.PanelStateService;
			// Render if we have status text OR if task bar (window list) is enabled
			bool hasContent = !string.IsNullOrEmpty(panelService.BottomStatus) || _getOptions().StatusBar.ShowTaskBar;
			bool hasStartButton = _getOptions().StatusBar.ShowStartButton &&
								  _getOptions().StatusBar.StartButtonLocation == Configuration.StatusBarLocation.Bottom;

			return panelService.ShowBottomPanel && (hasContent || hasStartButton);
		}

		/// <summary>
		/// Builds the start button markup string.
		/// </summary>
		private string BuildStartButton()
		{
			if (!_getOptions().StatusBar.ShowStartButton)
				return string.Empty;

			var text = _getOptions().StatusBar.StartButtonText;
			return $"[bold cyan]{text}[/] ";
		}

		/// <summary>
		/// Formats the performance metrics string for display.
		/// </summary>
		/// <summary>
		/// Computes a hash of the task bar state to detect changes.
		/// </summary>
		private int ComputeTaskBarStateHash(List<Window> windows)
		{
			int hash = 0;
			foreach (var w in windows)
			{
				hash ^= w.Title.GetHashCode();
				hash ^= w.State.GetHashCode();
				hash ^= w.GetIsActive().GetHashCode();
			}
			return hash;
		}

		/// <summary>
		/// Checks if a window is completely covered by other windows (with caching).
		/// </summary>
		private bool IsCompletelyCovered(Window window)
		{
			// Check cache first
			if (_coverageCache.TryGetValue(window.Guid, out bool cached))
				return cached;

			// Calculate coverage
			bool result = CalculateIsCompletelyCovered(window);
			_coverageCache[window.Guid] = result;
			return result;
		}

		/// <summary>
		/// Calculates if a window is completely covered by other windows.
		/// </summary>
		private bool CalculateIsCompletelyCovered(Window window)
		{
			foreach (var otherWindow in _windowSystemContext.Windows.Values)
			{
				if (otherWindow != window && _renderer.IsOverlapping(window, otherWindow) && otherWindow.ZIndex > window.ZIndex)
				{
					if (otherWindow.Left <= window.Left && otherWindow.Top <= window.Top &&
						otherWindow.Left + otherWindow.Width >= window.Left + window.Width &&
						otherWindow.Top + otherWindow.Height >= window.Top + window.Height)
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Computes a hash of window layout state (positions, sizes, z-order, visibility).
		/// Used to detect when coverage cache needs invalidation.
		/// </summary>
		private long ComputeWindowLayoutHash()
		{
			unchecked
			{
				long hash = 17;
				foreach (var window in _windowSystemContext.Windows.Values)
				{
					hash = hash * 31 + window.Left;
					hash = hash * 31 + window.Top;
					hash = hash * 31 + window.Width;
					hash = hash * 31 + window.Height;
					hash = hash * 31 + window.ZIndex;
					hash = hash * 31 + (int)window.State;
				}
				hash = hash * 31 + _windowSystemContext.Windows.Count;
				return hash;
			}
		}

		/// <summary>
		/// Renders all dirty windows in proper Z-order.
		/// </summary>
		private void RenderWindows()
		{
			// Reuse cached HashSet to avoid allocation
			_windowsToRender.Clear();

			// Only clear coverage cache when window layout actually changed
			long layoutHash = ComputeWindowLayoutHash();
			if (layoutHash != _lastCoverageLayoutHash)
			{
				_coverageCache.Clear();
				_lastCoverageLayoutHash = layoutHash;
			}

			// Build sorted window list for rendering (avoid LINQ allocations)
			_sortedWindows.Clear();
			_sortedWindows.AddRange(_windowSystemContext.Windows.Values);
			_sortedWindows.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

			// Identify dirty windows and only overlapping windows with higher Z-index
			// This prevents unnecessary redraws of windows below the dirty window
			foreach (var window in _windowSystemContext.Windows.Values)
			{
				// Skip minimized windows - they're invisible
				if (window.State == WindowState.Minimized)
					continue;

				// Skip windows with invalid dimensions (can happen during rapid resize)
				if (window.Width <= 0 || window.Height <= 0)
					continue;

				if (!window.IsDirty)
					continue;

				if (IsCompletelyCovered(window))
					continue;

				_windowsToRender.Add(window);

				// OPTIMIZATION: Don't add overlapping windows to render list
				// VisibleRegions.CalculateVisibleRegions() already clips each window's rendering
				// to exclude areas covered by higher Z-index windows, so overlapping windows
				// don't need re-rendering when a window beneath them changes.
			}

			// Safety: Always ensure the window being dragged is in the render list.
			// This prevents the dragging window from going invisible if an edge case
			// causes it to be skipped (e.g., marked clean prematurely or covered check race).
			var dragState = _windowStateService.CurrentDrag;
			if (dragState != null && dragState.Window != null &&
			    dragState.Window.State != WindowState.Minimized &&
			    dragState.Window.Width > 0 && dragState.Window.Height > 0 &&
			    !_windowsToRender.Contains(dragState.Window))
			{
				dragState.Window.Invalidate(true);
				_windowsToRender.Add(dragState.Window);
			}

			// PASS 1: Render normal (non-AlwaysOnTop) windows based on their ZIndex (no LINQ)
			for (int i = 0; i < _sortedWindows.Count; i++)
			{
				var window = _sortedWindows[i];
				if (window.AlwaysOnTop) continue;

				if (window != _windowSystemContext.ActiveWindow && _windowsToRender.Contains(window))
				{
					// Skip windows with invalid dimensions
					if (window.Width > 0 && window.Height > 0)
					{
						_renderer.RenderWindow(window);
					}
				}
			}

			// Check if any of the overlapping windows is overlapping the active window
			if (_windowSystemContext.ActiveWindow != null && !_windowSystemContext.ActiveWindow.AlwaysOnTop)
			{
				if (_windowsToRender.Contains(_windowSystemContext.ActiveWindow))
				{
					// Skip windows with invalid dimensions
					if (_windowSystemContext.ActiveWindow.Width > 0 && _windowSystemContext.ActiveWindow.Height > 0)
					{
						_renderer.RenderWindow(_windowSystemContext.ActiveWindow);
					}
				}
				else
				{
					var overlappingWindows = _renderer.GetOverlappingWindows(_windowSystemContext.ActiveWindow);

					foreach (var overlappingWindow in overlappingWindows)
					{
						// Only render active window if overlapping window is ABOVE it (higher Z-index)
						// Windows below the active window can't affect its visible pixels
						if (overlappingWindow.ZIndex > _windowSystemContext.ActiveWindow.ZIndex &&
							_windowsToRender.Contains(overlappingWindow))
						{
							// Skip windows with invalid dimensions
							if (_windowSystemContext.ActiveWindow.Width > 0 && _windowSystemContext.ActiveWindow.Height > 0)
							{
								_renderer.RenderWindow(_windowSystemContext.ActiveWindow);
								break;  // Only need to render once
							}
						}
					}
				}
			}

			// PASS 2: Render AlwaysOnTop windows (always last, on top of everything) (no LINQ)
			// IMPORTANT: AlwaysOnTop windows render whenever ANY window is dirty to ensure they stay on top
			bool anyWindowsDirty = _windowsToRender.Count > 0;
			for (int i = 0; i < _sortedWindows.Count; i++)
			{
				var window = _sortedWindows[i];
				if (!window.AlwaysOnTop) continue;
				if (window.State == WindowState.Minimized) continue;

				// AlwaysOnTop windows render if: they're dirty, OR any other window is dirty (to stay on top)
				if (window.IsDirty || anyWindowsDirty)
				{
					// Skip windows with invalid dimensions
					if (window.Width > 0 && window.Height > 0)
					{
						_renderer.RenderWindow(window);
					}
				}
			}
		}

		/// <summary>
		/// Renders the top status bar with optional performance metrics.
		/// </summary>
		private void RenderTopStatus()
		{
			if (!ShouldRenderTopStatus())
				return;

			// Build complete TopStatus with metrics appended
			var baseStatus = _windowSystemContext.PanelStateService.TopStatus ?? string.Empty;
			var metricsString = _getOptions().EnablePerformanceMetrics
				? _performanceTracker.FormatMetrics()
				: string.Empty;
			var completeTopStatus = baseStatus + metricsString;

			// Build start button if configured for top
			var startButton = string.Empty;
			if (_getOptions().StatusBar.ShowStartButton &&
				_getOptions().StatusBar.StartButtonLocation == Configuration.StatusBarLocation.Top)
			{
				startButton = BuildStartButton();
			}

			string topRow;
			if (_getOptions().StatusBar.StartButtonPosition == Configuration.StartButtonPosition.Left)
			{
				topRow = $"{startButton}{completeTopStatus}";
			}
			else
			{
				// Right position - add start button at the end
				var contentLength = Parsing.MarkupParser.StripLength(completeTopStatus);
				var startButtonLength = Parsing.MarkupParser.StripLength(startButton);
				var availableSpace = _consoleDriver.ScreenSize.Width - startButtonLength;

				var content = completeTopStatus;
				if (contentLength > availableSpace)
				{
					content = Parsing.MarkupParser.Truncate(content, availableSpace);
				}

				content += new string(' ', availableSpace - Parsing.MarkupParser.StripLength(content));
				topRow = $"{content}{startButton}";
			}

			// Cache includes start button for proper invalidation
			if (topRow != _cachedTopStatus)
			{
				var effectiveLength = Parsing.MarkupParser.StripLength(topRow);
				var paddedTopRow = topRow.PadRight(_consoleDriver.ScreenSize.Width + (topRow.Length - effectiveLength));
				var statusBuffer = MarkupLineToBuffer(paddedTopRow, _consoleDriver.ScreenSize.Width,
					_windowSystemContext.Theme.TopBarForegroundColor, _windowSystemContext.Theme.TopBarBackgroundColor);
				_consoleDriver.WriteBufferRegion(0, 0, statusBuffer, 0, 0, statusBuffer.Width,
					_windowSystemContext.Theme.TopBarBackgroundColor);

				_cachedTopStatus = topRow;
			}
		}

		/// <summary>
		/// Renders the bottom status bar with task bar and optional start button.
		/// </summary>
		private void RenderBottomStatus()
		{
			if (!ShouldRenderBottomStatus())
				return;

			// Filter out sub-windows and overlay windows from the bottom status bar
			_topLevelWindowsPool.Clear();
			foreach (var w in _windowSystemContext.Windows.Values)
			{
				if (w.ParentWindow == null && w.ShowInTaskbar)
					_topLevelWindowsPool.Add(w);
			}
			_topLevelWindowsPool.Sort((a, b) => a.CreationOrder.CompareTo(b.CreationOrder));

			// Check if task bar cache is valid
			string taskBar;
			if (_getOptions().StatusBar.ShowTaskBar)
			{
				int stateHash = ComputeTaskBarStateHash(_topLevelWindowsPool);
				if (_cachedTaskBar != null &&
					_taskBarWindowCount == _topLevelWindowsPool.Count &&
					_taskBarStateHash == stateHash)
				{
					// Use cached task bar
					taskBar = _cachedTaskBar;
				}
				else
				{
					// Rebuild task bar
					var parts = new string[_topLevelWindowsPool.Count];
					for (int i = 0; i < _topLevelWindowsPool.Count; i++)
					{
						var w = _topLevelWindowsPool[i];
						var minIndicator = w.State == WindowState.Minimized ? "[dim]" : "";
						var minEnd = w.State == WindowState.Minimized ? "[/]" : "";
						parts[i] = $"[bold]Alt-{i + 1}[/] {minIndicator}{StringHelper.TrimWithEllipsis(w.Title, 15, 7)}{minEnd}";
					}
					taskBar = $"{string.Join(" | ", parts)} | ";

					// Update cache
					_cachedTaskBar = taskBar;
					_taskBarWindowCount = _topLevelWindowsPool.Count;
					_taskBarStateHash = stateHash;
				}
			}
			else
			{
				taskBar = string.Empty;
			}

			// Build start button if configured for bottom
			var startButton = string.Empty;
			if (_getOptions().StatusBar.ShowStartButton &&
				_getOptions().StatusBar.StartButtonLocation == Configuration.StatusBarLocation.Bottom)
			{
				startButton = BuildStartButton();
			}

			string bottomRow;
			if (_getOptions().StatusBar.StartButtonPosition == Configuration.StartButtonPosition.Left)
			{
				bottomRow = $"{startButton}{taskBar}{_windowSystemContext.PanelStateService.BottomStatus}";
			}
			else
			{
				// Right position - add start button at the end
				var content = $"{taskBar}{_windowSystemContext.PanelStateService.BottomStatus}";
				var contentLength = Parsing.MarkupParser.StripLength(content);
				var startButtonLength = Parsing.MarkupParser.StripLength(startButton);
				var availableSpace = _consoleDriver.ScreenSize.Width - startButtonLength;

				if (contentLength > availableSpace)
				{
					content = Parsing.MarkupParser.Truncate(content, availableSpace);
				}

				content += new string(' ', availableSpace - Parsing.MarkupParser.StripLength(content));
				bottomRow = $"{content}{startButton}";
			}

			// Display the list of window titles in the bottom row
			if (Parsing.MarkupParser.StripLength(bottomRow) > _consoleDriver.ScreenSize.Width)
			{
				bottomRow = Parsing.MarkupParser.Truncate(bottomRow, _consoleDriver.ScreenSize.Width);
			}

			bottomRow += new string(' ', _consoleDriver.ScreenSize.Width - Parsing.MarkupParser.StripLength(bottomRow));

			if (_cachedBottomStatus != bottomRow)
			{
				var statusBuffer = MarkupLineToBuffer(bottomRow, _consoleDriver.ScreenSize.Width,
					_windowSystemContext.Theme.BottomBarForegroundColor, _windowSystemContext.Theme.BottomBarBackgroundColor);
				_consoleDriver.WriteBufferRegion(0, _consoleDriver.ScreenSize.Height - 1,
					statusBuffer, 0, 0, statusBuffer.Width, _windowSystemContext.Theme.BottomBarBackgroundColor);

				_cachedBottomStatus = bottomRow;
			}
		}

		/// <summary>
		/// Converts a markup string to a CharacterBuffer using MarkupParser.
		/// </summary>
		private static Layout.CharacterBuffer MarkupLineToBuffer(string markup, int width,
			Color defaultFg, Color defaultBg)
		{
			var buffer = new Layout.CharacterBuffer(width, 1, defaultBg);
			buffer.WriteCells(0, 0, Parsing.MarkupParser.Parse(markup, defaultFg, defaultBg));
			return buffer;
		}

		#endregion
	}
}
