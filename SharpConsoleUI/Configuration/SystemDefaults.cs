// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Configuration
{
	/// <summary>
	/// Centralized default values and constants for ConsoleWindowSystem behavior.
	/// Extracted from magic numbers in ConsoleWindowSystem.cs.
	/// </summary>
	public static class SystemDefaults
	{
		// Main loop timing
		/// <summary>
		/// Default idle time between main loop iterations in milliseconds (default: 10ms)
		/// </summary>
		public const int DefaultIdleTimeMs = 10;

		/// <summary>
		/// Minimum sleep duration for input state service (default: 10ms)
		/// </summary>
		public const int MinSleepDurationMs = 10;

		/// <summary>
		/// Maximum sleep duration for input state service (default: 100ms)
		/// </summary>
		public const int MaxSleepDurationMs = 100;

		// Watchdog timings
		/// <summary>No heartbeat for this long → log + scan for emergency exit (default: 2000ms).</summary>
		public const int WatchdogStaleThresholdMs = 2000;

		/// <summary>No heartbeat for this long → raise Unresponsive + optional banner (default: 5000ms).</summary>
		public const int WatchdogUnresponsiveThresholdMs = 5000;

		/// <summary>Watchdog timer tick cadence in milliseconds (default: 500ms).</summary>
		public const int WatchdogPollIntervalMs = 500;

		/// <summary>
		/// While draining the UI-action queue, pulse the watchdog heartbeat at most this often (default:
		/// 250ms) so a long-but-productive drain (e.g. a flood of enqueued frames) is reported as alive
		/// rather than falsely flagged stale. Well under <see cref="WatchdogStaleThresholdMs"/> so the
		/// loop stays live; large enough that a single action blocking longer than the stale threshold
		/// still trips the watchdog (the pulse only fires BETWEEN actions).
		/// </summary>
		public const int UiDrainHeartbeatIntervalMs = 250;

		/// <summary>
		/// Debounce time before allowing portal dismiss after creation (default: 200ms).
		/// Prevents the same click that opens a portal from immediately dismissing it.
		/// </summary>
		public const int PortalDismissDebounceMs = 200;

		/// <summary>
		/// Fast loop idle time when rendering without frame rate limit (default: 10ms)
		/// </summary>
		public const int FastLoopIdleMs = 10;

		// Window switching
		/// <summary>
		/// First Alt+Number window switch key (default: Alt+1)
		/// </summary>
		public const ConsoleKey FirstWindowSwitchKey = ConsoleKey.D1;

		/// <summary>
		/// Last Alt+Number window switch key (default: Alt+9)
		/// </summary>
		public const ConsoleKey LastWindowSwitchKey = ConsoleKey.D9;
	}
}
