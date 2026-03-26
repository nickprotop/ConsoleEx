// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using System.Drawing;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Manages desktop-level portals — lightweight render overlays that appear above all windows.
	/// Portals render after windows in the normal render pass and never interfere with window rendering.
	/// </summary>
	public class DesktopPortalService
	{
		private readonly ILogService _logService;
		private readonly ConsoleWindowSystem _windowSystem;
		private readonly List<DesktopPortal> _portals = new();
		private readonly Stack<Window?> _savedActiveWindows = new();
		private int _nextZOrder;
		private bool _needsCleanupFrame;

		/// <summary>
		/// Initializes a new instance of the DesktopPortalService class.
		/// </summary>
		/// <param name="logService">Service for debug logging.</param>
		/// <param name="windowSystem">The window system this service belongs to.</param>
		public DesktopPortalService(ILogService logService, ConsoleWindowSystem windowSystem)
		{
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
		}

		#region Properties

		/// <summary>
		/// Gets whether any desktop portals are currently open.
		/// </summary>
		public bool HasPortals => _portals.Count > 0;

		/// <summary>
		/// Gets whether a cleanup frame is needed (portals were just removed).
		/// Cleared after the cleanup frame runs.
		/// </summary>
		public bool NeedsCleanupFrame
		{
			get => _needsCleanupFrame;
			internal set => _needsCleanupFrame = value;
		}

		/// <summary>
		/// Gets the topmost portal (highest ZOrder), or null if none.
		/// </summary>
		public DesktopPortal? TopPortal => _portals.Count > 0 ? _portals[_portals.Count - 1] : null;

		/// <summary>
		/// Gets a read-only list of all open portals, ordered by ZOrder.
		/// </summary>
		public IReadOnlyList<DesktopPortal> Portals => _portals.AsReadOnly();

		#endregion

		#region Portal Lifecycle

		/// <summary>
		/// Creates and shows a new desktop portal.
		/// </summary>
		/// <param name="options">Portal configuration options.</param>
		/// <returns>The created portal.</returns>
		public DesktopPortal CreatePortal(DesktopPortalOptions options)
		{
			_logService.LogDebug($"Creating desktop portal (DismissOnClick={options.DismissOnClickOutside}, Dim={options.DimBackground})", category: "Portal");

			// Save current active window for focus restoration
			_savedActiveWindows.Push(_windowSystem.ActiveWindow);

			var portal = new DesktopPortal(options, _nextZOrder++, _windowSystem);
			_portals.Add(portal);

			_logService.LogDebug($"Desktop portal created: {portal.Id}", category: "Portal");
			return portal;
		}

		/// <summary>
		/// Removes a specific portal and cleans up.
		/// </summary>
		/// <param name="portal">The portal to remove.</param>
		public void RemovePortal(DesktopPortal portal)
		{
			if (!_portals.Remove(portal))
				return;

			_logService.LogDebug($"Removing desktop portal: {portal.Id}", category: "Portal");

			// Invoke dismiss callback
			portal.OnDismiss?.Invoke();

			// Disconnect content from invalidation chain
			portal.Content.Container = null;

			// Invalidate all windows — next frame repaints everything fresh
			foreach (var window in _windowSystem.Windows.Values)
			{
				window.IsDirty = true;
			}

			// Restore focus
			if (_savedActiveWindows.Count > 0)
			{
				var savedWindow = _savedActiveWindows.Pop();

				// Only restore if no more portals remain
				if (_portals.Count == 0 && savedWindow != null)
				{
					_windowSystem.SetActiveWindow(savedWindow);
				}
			}

			_needsCleanupFrame = true;
			_windowSystem.Render.DesktopNeedsRender = true;
		}

		/// <summary>
		/// Dismisses all open portals and restores the original active window.
		/// </summary>
		public void DismissAllPortals()
		{
			if (_portals.Count == 0)
				return;

			_logService.LogDebug($"Dismissing all desktop portals ({_portals.Count})", category: "Portal");

			// Find the bottom-most saved window (the one before any portals were opened)
			Window? originalWindow = null;
			while (_savedActiveWindows.Count > 0)
			{
				originalWindow = _savedActiveWindows.Pop();
			}

			// Remove all portals
			var portalsCopy = _portals.ToList();
			_portals.Clear();

			foreach (var portal in portalsCopy)
			{
				portal.OnDismiss?.Invoke();
				portal.Content.Container = null;
			}

			// Invalidate all windows — next frame repaints everything fresh
			foreach (var window in _windowSystem.Windows.Values)
			{
				window.IsDirty = true;
			}

			// Restore original active window
			if (originalWindow != null)
			{
				_windowSystem.SetActiveWindow(originalWindow);
			}

			_needsCleanupFrame = true;
			_windowSystem.Render.DesktopNeedsRender = true;
		}

		#endregion

		#region Dirty Tracking

		/// <summary>
		/// Returns true if any portal needs re-rendering.
		/// Checked by the main loop's shouldRender gate.
		/// </summary>
		public bool AnyPortalDirty()
		{
			for (int i = 0; i < _portals.Count; i++)
			{
				if (_portals[i].IsDirty)
					return true;
			}
			return false;
		}

		#endregion

		#region Hit Testing

		/// <summary>
		/// Tests if a screen point hits any portal. Returns the topmost hit portal, or null.
		/// </summary>
		/// <param name="point">Screen-space point.</param>
		/// <returns>The topmost portal containing the point, or null.</returns>
		public DesktopPortal? HitTest(Point point)
		{
			// Check in reverse order (topmost first)
			for (int i = _portals.Count - 1; i >= 0; i--)
			{
				var portal = _portals[i];
				// Check against control bounds, not full portal bounds
				foreach (var bounds in portal.ControlBounds)
				{
					int screenX = portal.Bounds.X + bounds.X;
					int screenY = portal.Bounds.Y + bounds.Y;
					var screenRect = new Rectangle(screenX, screenY, bounds.Width, bounds.Height);
					if (screenRect.Contains(point))
						return portal;
				}
			}
			return null;
		}

		#endregion
	}
}
