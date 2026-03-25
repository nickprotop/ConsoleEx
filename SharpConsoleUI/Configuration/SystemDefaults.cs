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
