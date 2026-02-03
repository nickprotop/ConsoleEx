// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Performance
{
	/// <summary>
	/// Tracks and reports performance metrics for the console window system.
	/// Consolidates frame timing, FPS calculation, and dirty region tracking.
	/// Extracted from ConsoleWindowSystem and RenderCoordinator as part of Phase 1.3 refactoring.
	/// </summary>
	public class PerformanceTracker
	{
		// Timing
		private DateTime _lastFrameTime = DateTime.UtcNow;
		private double _currentFrameTimeMs;

		// Metrics
		private int _currentWindowCount;
		private int _currentDirtyCount;
		private int _currentDirtyChars;
		private int _displayedDirtyChars;

		// Display update throttling
		private int _metricsUpdateCounter = 0;
		private const int MetricsUpdateInterval = 15; // Update display every 15 frames (~250ms at 60fps)

		// DirtyChars hold for visibility
		private DateTime _lastDirtyCharsChange = DateTime.UtcNow;
		private const int DirtyCharsHoldTimeMs = 1000; // Hold last value for 1 second

		/// <summary>
		/// Initializes a new instance of the PerformanceTracker class.
		/// </summary>
		public PerformanceTracker()
		{
		}

		#region Public Properties

		/// <summary>
		/// Gets the most recent frame time in milliseconds.
		/// </summary>
		public double CurrentFrameTimeMs => _currentFrameTimeMs;

		/// <summary>
		/// Gets the current frames per second (FPS).
		/// </summary>
		public double CurrentFPS => _currentFrameTimeMs > 0 ? 1000.0 / _currentFrameTimeMs : 0;

		/// <summary>
		/// Gets the displayed dirty character count (held for visibility).
		/// </summary>
		public int CurrentDirtyChars => _displayedDirtyChars;

		/// <summary>
		/// Gets the current window count.
		/// </summary>
		public int CurrentWindowCount => _currentWindowCount;

		/// <summary>
		/// Gets the current number of dirty windows.
		/// </summary>
		public int CurrentDirtyCount => _currentDirtyCount;

		#endregion

		#region Public Methods

		/// <summary>
		/// Records frame timing and calculates elapsed time since last frame.
		/// Call this at the start of each frame.
		/// </summary>
		/// <returns>True if metrics display should be updated this frame.</returns>
		public bool BeginFrame()
		{
			var now = DateTime.UtcNow;
			_currentFrameTimeMs = (now - _lastFrameTime).TotalMilliseconds;
			_lastFrameTime = now;

			// Check if metrics display needs updating (throttled to every N frames)
			if (++_metricsUpdateCounter >= MetricsUpdateInterval)
			{
				_metricsUpdateCounter = 0;
				return true; // Signal that display should update
			}

			return false;
		}

		/// <summary>
		/// Updates metrics snapshot with current window state.
		/// Call this after BeginFrame and before rendering.
		/// </summary>
		/// <param name="windowCount">Total number of windows.</param>
		/// <param name="dirtyCount">Number of dirty windows needing redraw.</param>
		public void UpdateMetrics(int windowCount, int dirtyCount)
		{
			_currentWindowCount = windowCount;
			_currentDirtyCount = dirtyCount;
		}

		/// <summary>
		/// Sets the number of dirty characters rendered in this frame.
		/// Call this after rendering windows but before rendering status bars.
		/// Implements hold logic to keep the value visible for a minimum duration.
		/// </summary>
		/// <param name="dirtyChars">Number of characters changed this frame.</param>
		public void SetDirtyChars(int dirtyChars)
		{
			_currentDirtyChars = dirtyChars;

			// DirtyChars hold logic: preserve last non-zero value for visibility
			if (_currentDirtyChars != _displayedDirtyChars)
			{
				// Value changed - update immediately
				_displayedDirtyChars = _currentDirtyChars;
				_lastDirtyCharsChange = DateTime.UtcNow;
			}
			else if (_currentDirtyChars == 0)
			{
				// Value is 0 - check if hold time expired
				var elapsed = (DateTime.UtcNow - _lastDirtyCharsChange).TotalMilliseconds;
				if (elapsed >= DirtyCharsHoldTimeMs)
				{
					_displayedDirtyChars = 0; // Reset to 0 after hold period
				}
				// else: preserve last non-zero value
			}
		}

		/// <summary>
		/// Formats performance metrics as Spectre markup string for display.
		/// Returns empty string if no frame time has been recorded yet.
		/// </summary>
		/// <returns>Formatted metrics string with frame time, FPS, window count, dirty count, and dirty chars.</returns>
		public string FormatMetrics()
		{
			if (_currentFrameTimeMs <= 0)
				return string.Empty;

			// Format: " | Frame:16ms Win:3 Dirty:1 DirtyChars:234"
			return $" [dim]|[/] " +
				   $"[dim]Frame:{_currentFrameTimeMs:F0}ms[/] " +
				   $"[dim]Win:{_currentWindowCount}[/] " +
				   $"[dim]Dirty:{_currentDirtyCount}[/] " +
				   $"[dim]DirtyChars:{_displayedDirtyChars}[/]";
		}

		#endregion
	}
}
