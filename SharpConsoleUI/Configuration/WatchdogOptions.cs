// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// SharpConsoleUI/Configuration/WatchdogOptions.cs
namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration for the main-loop watchdog (liveness monitoring, unresponsive notification, recovery).
/// </summary>
public record WatchdogOptions(
	bool Enabled = true,
	int StaleThresholdMs = SystemDefaults.WatchdogStaleThresholdMs,
	int UnresponsiveThresholdMs = SystemDefaults.WatchdogUnresponsiveThresholdMs,
	int PollIntervalMs = SystemDefaults.WatchdogPollIntervalMs,
	bool ShowUnresponsiveBanner = true,
	bool FullRefreshOnRecovery = true,

	// Whether the emergency exit may terminate the PROCESS (Environment.Exit) when the user asks to
	// force-quit a wedged main loop. True suits a standalone terminal application, where the process
	// is the app and a hung UI leaves no other way out.
	//
	// Set false when the library is embedded in a host that owns the process — a test runner, a
	// service, an IDE extension, a GUI app with a console pane. Terminating the host over one wedged
	// loop is never the embedder's choice to lose: the loop is stopped and the driver restored, but
	// the process lives.
	bool AllowProcessExit = true
);
