// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace SharpConsoleUI.Core;

/// <summary>
/// Monitors the main loop heartbeat. When the main loop stalls (rendering deadlock,
/// slow layout, blocked callback), the watchdog provides an emergency exit path
/// by scanning the input queue for Ctrl+Q and force-exiting.
/// If the stall resolves on its own, the watchdog triggers a recovery callback
/// to force a full screen repaint (the ANSI banner is written outside the
/// CharacterBuffer, so diff rendering won't clear it automatically).
/// The input thread is independent (keys enqueue even when the main loop is stuck).
/// </summary>
internal sealed class MainLoopWatchdog : IDisposable
{
	private readonly int _staleThresholdMs;
	private readonly int _unresponsiveThresholdMs;
	private readonly int _pollIntervalMs;
	private long _lastHeartbeatTicks;
	private Timer? _timer;
	private volatile bool _disposed;
	private volatile bool _bannerShown;
	private volatile bool _staleLogged;
	private volatile bool _wasStale;                 // crossed stale threshold since last recovery

	// Dependencies injected via Start()
	private Func<bool>? _scanForEmergencyExit;
	private Action? _onForceExit;
	private Action? _onRecovery;
	private Func<TimeSpan, bool>? _onUnresponsive;   // returns whether to show the banner
	private Action<TimeSpan>? _onRecovered;
	private Action<string>? _logWarning;

	/// <summary>
	/// Creates a watchdog with configurable thresholds.
	/// </summary>
	/// <param name="staleThresholdMs">Milliseconds without heartbeat before scanning for emergency exit (default 2000).</param>
	/// <param name="unresponsiveThresholdMs">Milliseconds without heartbeat before raising Unresponsive / showing the banner (default 5000).</param>
	/// <param name="pollIntervalMs">Watchdog timer tick cadence in milliseconds (default 500).</param>
	public MainLoopWatchdog(int staleThresholdMs = 2000, int unresponsiveThresholdMs = 5000, int pollIntervalMs = 500)
	{
		_staleThresholdMs = staleThresholdMs;
		_unresponsiveThresholdMs = unresponsiveThresholdMs;
		_pollIntervalMs = pollIntervalMs;
		_lastHeartbeatTicks = Stopwatch.GetTimestamp();
	}

	/// <summary>
	/// True when the main loop heartbeat has gone stale beyond the threshold.
	/// </summary>
	public bool IsStale
	{
		get
		{
			var elapsed = Stopwatch.GetElapsedTime(Volatile.Read(ref _lastHeartbeatTicks));
			return elapsed.TotalMilliseconds > _staleThresholdMs;
		}
	}

	/// <summary>
	/// Attaches a logging callback for watchdog events.
	/// </summary>
	public MainLoopWatchdog WithLogging(Action<string> logWarning)
	{
		_logWarning = logWarning;
		return this;
	}

	/// <summary>
	/// Called by the main loop each iteration to signal liveness.
	/// If the loop went stale since the last recovery, triggers recovery (full repaint)
	/// and raises the Recovered callback with the duration of the stall.
	/// </summary>
	public void Heartbeat()
	{
		var now = Stopwatch.GetTimestamp();
		var stalledFor = Stopwatch.GetElapsedTime(Volatile.Read(ref _lastHeartbeatTicks));
		Volatile.Write(ref _lastHeartbeatTicks, now);

		if (_staleLogged)
		{
			_staleLogged = false;
			_logWarning?.Invoke("Main loop recovered from stall");
		}
		// Recover if the watchdog flagged a stall, or if this heartbeat itself arrived
		// after the stale threshold (covers stalls the timer hasn't observed yet).
		if (_wasStale || stalledFor.TotalMilliseconds > _staleThresholdMs)
		{
			_wasStale = false;
			_bannerShown = false;
			_onRecovery?.Invoke();            // existing: full repaint hook
			_onRecovered?.Invoke(stalledFor); // new: raise Recovered event
		}
	}

	/// <summary>
	/// Starts the watchdog timer. Call after the main loop begins.
	/// </summary>
	/// <param name="scanForEmergencyExit">Callback that scans the input queue for Ctrl+Q.
	/// Returns true if an emergency exit key was found and consumed.</param>
	/// <param name="onForceExit">Callback invoked to force-exit the application.</param>
	/// <param name="onRecovery">Callback invoked when the main loop recovers after going stale.
	/// Should trigger a full screen repaint to clear any ghost banner.</param>
	/// <param name="onUnresponsive">Optional callback invoked when the unresponsive threshold is
	/// crossed. Returns whether the watchdog should write the terminal banner.</param>
	/// <param name="onRecovered">Optional callback invoked on recovery with the stall duration.</param>
	public void Start(
		Func<bool> scanForEmergencyExit,
		Action onForceExit,
		Action onRecovery,
		Func<TimeSpan, bool>? onUnresponsive = null,
		Action<TimeSpan>? onRecovered = null)
	{
		_scanForEmergencyExit = scanForEmergencyExit;
		_onForceExit = onForceExit;
		_onRecovery = onRecovery;
		_onUnresponsive = onUnresponsive;
		_onRecovered = onRecovered;
		_timer = new Timer(OnTick, null, _pollIntervalMs, _pollIntervalMs);
	}

	private void OnTick(object? state)
	{
		if (_disposed) return;

		var elapsed = Stopwatch.GetElapsedTime(Volatile.Read(ref _lastHeartbeatTicks));

		if (elapsed.TotalMilliseconds > _staleThresholdMs && !_staleLogged)
		{
			_staleLogged = true;
			_wasStale = true;
			_logWarning?.Invoke($"Main loop stale \u2014 no heartbeat for {elapsed.TotalMilliseconds:F0}ms");
		}

		if (elapsed.TotalMilliseconds > _unresponsiveThresholdMs && !_bannerShown)
		{
			_bannerShown = true;
			bool showBanner = _onUnresponsive?.Invoke(elapsed) ?? true;
			if (showBanner)
				WriteBanner();
		}

		if (elapsed.TotalMilliseconds > _staleThresholdMs)
		{
			if (_scanForEmergencyExit?.Invoke() == true)
			{
				_onForceExit?.Invoke();
			}
		}
	}

	/// <summary>
	/// Writes an "unresponsive" banner directly to the terminal using ANSI escapes,
	/// bypassing the CharacterBuffer. This works even when the render pipeline is deadlocked.
	/// </summary>
	private static void WriteBanner()
	{
		try
		{
			// Save cursor, move to row 1 col 1, white on red background, restore cursor
			Console.Error.Write("\x1b[s\x1b[1;1H\x1b[41;97m UI UNRESPONSIVE \u2014 Press Ctrl+Q to force quit \x1b[0m\x1b[u");
			Console.Error.Flush();
		}
		catch
		{
			// Best effort — terminal may be in a bad state
		}
	}

	public void Dispose()
	{
		_disposed = true;
		_timer?.Dispose();
		_timer = null;
	}
}
