// SharpConsoleUI/Configuration/WatchdogOptions.cs
namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration for the main-loop watchdog (liveness monitoring, unresponsive notification, recovery).
/// </summary>
public record WatchdogOptions(
	bool Enabled                 = true,
	int  StaleThresholdMs        = SystemDefaults.WatchdogStaleThresholdMs,
	int  UnresponsiveThresholdMs = SystemDefaults.WatchdogUnresponsiveThresholdMs,
	int  PollIntervalMs          = SystemDefaults.WatchdogPollIntervalMs,
	bool ShowUnresponsiveBanner  = true,
	bool FullRefreshOnRecovery   = true
);
