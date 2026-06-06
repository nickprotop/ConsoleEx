// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// SharpConsoleUI/Core/RecoveredEventArgs.cs
namespace SharpConsoleUI.Core;

/// <summary>
/// Raised on the UI thread when the main loop recovers after a stall. Safe to touch the UI.
/// </summary>
public sealed class RecoveredEventArgs : EventArgs
{
	/// <summary>Total time the main loop was stalled before it recovered.</summary>
	public TimeSpan WasStalledFor { get; }
	/// <summary>UTC timestamp when recovery was detected.</summary>
	public DateTime TimestampUtc { get; }

	/// <summary>
	/// When true (default = WatchdogOptions.FullRefreshOnRecovery), the system performs a full-screen
	/// repaint after the event returns to clear any stray terminal output. Set false to skip it.
	/// </summary>
	public bool FullRefresh { get; set; }

	/// <summary>Initializes a new instance of the <see cref="RecoveredEventArgs"/> class.</summary>
	public RecoveredEventArgs(TimeSpan wasStalledFor, DateTime timestampUtc, bool fullRefresh)
	{
		WasStalledFor = wasStalledFor;
		TimestampUtc = timestampUtc;
		FullRefresh = fullRefresh;
	}
}
