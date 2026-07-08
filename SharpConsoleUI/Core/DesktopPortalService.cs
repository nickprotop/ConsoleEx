// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;

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

		// Portal state is mutated from BOTH the UI/render thread (CreatePortal/RemovePortal via the key
		// loop; HasPortals/AnyPortalDirty reads from the render loop) AND the driver's mouse thread
		// (click-outside → DismissAllPortals; HitTest). Without synchronization those unsynchronized
		// List/Stack accesses race and can corrupt state — e.g. leaving HasPortals stuck true, after
		// which every key and click is swallowed by the portal input branch while rendering continues.
		// This lock serialises all access to _portals/_savedActiveWindows. Callbacks that re-enter the
		// window system (OnDismiss, SetActiveWindow, RestorePortalRegions) run OUTSIDE the lock.
		private readonly object _portalLock = new();

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
		public bool HasPortals { get { lock (_portalLock) { return _portals.Count > 0; } } }

		/// <summary>
		/// Gets the topmost portal (highest ZOrder), or null if none.
		/// </summary>
		public DesktopPortal? TopPortal { get { lock (_portalLock) { return _portals.Count > 0 ? _portals[_portals.Count - 1] : null; } } }

		/// <summary>
		/// Gets a snapshot of all open portals, ordered by ZOrder.
		/// </summary>
		public IReadOnlyList<DesktopPortal> Portals { get { lock (_portalLock) { return _portals.ToList(); } } }

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

			// Read the active window outside the lock (it re-enters the window system).
			var activeWindow = _windowSystem.ActiveWindow;

			DesktopPortal portal;
			lock (_portalLock)
			{
				_savedActiveWindows.Push(activeWindow);
				portal = new DesktopPortal(options, _nextZOrder++, _windowSystem);
				_portals.Add(portal);
			}

			_logService.LogDebug($"Desktop portal created: {portal.Id}", category: "Portal");
			return portal;
		}

		/// <summary>
		/// Removes a specific portal and cleans up.
		/// </summary>
		/// <param name="portal">The portal to remove.</param>
		public void RemovePortal(DesktopPortal portal)
		{
			// Mutate the shared collections under the lock; decide what focus to restore. Re-entrant
			// callbacks (OnDismiss, RestorePortalRegions, SetActiveWindow) run OUTSIDE the lock.
			Window? windowToActivate = null;
			lock (_portalLock)
			{
				if (!_portals.Remove(portal))
					return;

				if (_savedActiveWindows.Count > 0)
				{
					var savedWindow = _savedActiveWindows.Pop();
					// Only restore if no more portals remain.
					if (_portals.Count == 0 && savedWindow != null)
						windowToActivate = savedWindow;
				}
			}

			_logService.LogDebug($"Removing desktop portal: {portal.Id}", category: "Portal");

			portal.OnDismiss?.Invoke();
			portal.Content.Container = null;
			_windowSystem.Render.RestorePortalRegions(portal);

			if (windowToActivate != null)
				_windowSystem.SetActiveWindow(windowToActivate);

			_windowSystem.Render.DesktopNeedsRender = true;
		}

		/// <summary>
		/// Dismisses all open portals and restores the original active window.
		/// </summary>
		public void DismissAllPortals()
		{
			// Snapshot + clear the shared collections under the lock; run re-entrant callbacks outside it.
			Window? originalWindow = null;
			List<DesktopPortal> portalsCopy;
			lock (_portalLock)
			{
				if (_portals.Count == 0)
					return;

				// Drain to the bottom-most saved window (the one before any portals were opened).
				while (_savedActiveWindows.Count > 0)
					originalWindow = _savedActiveWindows.Pop();

				portalsCopy = _portals.ToList();
				_portals.Clear();
			}

			_logService.LogDebug($"Dismissing all desktop portals ({portalsCopy.Count})", category: "Portal");

			foreach (var portal in portalsCopy)
			{
				portal.OnDismiss?.Invoke();
				portal.Content.Container = null;
				_windowSystem.Render.RestorePortalRegions(portal);
			}

			if (originalWindow != null)
				_windowSystem.SetActiveWindow(originalWindow);

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
			lock (_portalLock)
			{
				for (int i = 0; i < _portals.Count; i++)
				{
					if (_portals[i].IsDirty)
						return true;
				}
				return false;
			}
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
			// Snapshot under the lock (this runs on the mouse thread while the UI thread may mutate).
			DesktopPortal[] snapshot;
			lock (_portalLock)
			{
				snapshot = _portals.ToArray();
			}

			// Check in reverse order (topmost first).
			for (int i = snapshot.Length - 1; i >= 0; i--)
			{
				var portal = snapshot[i];
				// Check against control bounds, not full portal bounds
				foreach (var bounds in portal.ControlBounds)
				{
					int screenX = portal.BufferOrigin.X + bounds.X;
					int screenY = portal.BufferOrigin.Y + bounds.Y;
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
