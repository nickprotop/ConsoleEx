// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// SharpConsoleUI/Core/UnresponsiveEventArgs.cs
namespace SharpConsoleUI.Core;

/// <summary>
/// Raised when the main loop has not heartbeat past the unresponsive threshold.
/// IMPORTANT: raised on the watchdog timer thread (the UI thread is stuck). Handlers must be
/// thread-safe and must NOT touch the UI — log, set a flag, or write raw ANSI only.
/// </summary>
public sealed class UnresponsiveEventArgs : EventArgs
{
	/// <summary>How long the main loop had gone without a heartbeat when the stall was detected.</summary>
	public TimeSpan StalledFor { get; }
	/// <summary>The main-loop phase executing when the stall was detected.</summary>
	public MainLoopPhase Phase { get; }
	/// <summary>Best-effort label of the user callback that was executing, or null if unknown.</summary>
	public string? BlockedIn { get; }
	/// <summary>UTC timestamp when the stall was detected.</summary>
	public DateTime TimestampUtc { get; }

	/// <summary>
	/// When true (default = WatchdogOptions.ShowUnresponsiveBanner), the built-in ANSI banner is
	/// written after the event returns. Set false to suppress it and own the display yourself.
	/// </summary>
	public bool ShowBanner { get; set; }

	/// <summary>Initializes a new instance of the <see cref="UnresponsiveEventArgs"/> class.</summary>
	public UnresponsiveEventArgs(TimeSpan stalledFor, MainLoopPhase phase, string? blockedIn, DateTime timestampUtc, bool showBanner)
	{
		StalledFor = stalledFor;
		Phase = phase;
		BlockedIn = blockedIn;
		TimestampUtc = timestampUtc;
		ShowBanner = showBanner;
	}
}
