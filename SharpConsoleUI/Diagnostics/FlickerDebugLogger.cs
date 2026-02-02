// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace SharpConsoleUI.Diagnostics
{
	/// <summary>
	/// Thread-safe debug logger for tracking rendering performance and flicker issues.
	/// Writes to file to avoid interfering with console UI rendering.
	/// </summary>
	public sealed class FlickerDebugLogger : IDisposable
	{
		private static readonly Lazy<FlickerDebugLogger> _instance = new(() => new FlickerDebugLogger());
		private static readonly string DefaultLogPath = "/tmp/sharpconsoleui-flicker-debug.log";

		private readonly ConcurrentQueue<string> _logBuffer = new();
		private readonly object _writeLock = new();
		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
		private readonly Timer? _flushTimer;

		private StreamWriter? _writer;
		private string _logFilePath;
		private DebugLevel _enabledLevels = DebugLevel.None;
		private int _scopeDepth = 0;
		private bool _disposed = false;

		/// <summary>
		/// Gets the singleton instance of the logger.
		/// </summary>
		public static FlickerDebugLogger Instance => _instance.Value;

		/// <summary>
		/// Gets whether debug logging is enabled.
		/// </summary>
		public bool IsEnabled => _enabledLevels != DebugLevel.None;

		private FlickerDebugLogger()
		{
			_logFilePath = Environment.GetEnvironmentVariable("SHARPCONSOLEUI_DEBUG_LOG") ?? DefaultLogPath;

			// Check if debug logging is enabled via environment variable
			var debugEnabled = Environment.GetEnvironmentVariable("SHARPCONSOLEUI_FLICKER_DEBUG");
			if (debugEnabled == "1" || debugEnabled?.ToLowerInvariant() == "true")
			{
				// Parse debug level from environment
				var debugLevelStr = Environment.GetEnvironmentVariable("SHARPCONSOLEUI_FLICKER_DEBUG_LEVEL") ?? "All";
				if (debugLevelStr.Equals("All", StringComparison.OrdinalIgnoreCase))
				{
					_enabledLevels = DebugLevel.All;
				}
				else
				{
					// Parse comma-separated list of levels
					_enabledLevels = DebugLevel.None;
					var levels = debugLevelStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
					foreach (var level in levels)
					{
						if (Enum.TryParse<DebugLevel>(level.Trim(), true, out var parsedLevel))
						{
							_enabledLevels |= parsedLevel;
						}
					}
				}

				InitializeLogFile();

				// Auto-flush every 100ms
				_flushTimer = new Timer(_ => Flush(), null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
			}
		}

		/// <summary>
		/// Enables debug logging to the specified file.
		/// </summary>
		public void Enable(string logFilePath, DebugLevel levels = DebugLevel.All)
		{
			lock (_writeLock)
			{
				_logFilePath = logFilePath;
				_enabledLevels = levels;
				InitializeLogFile();
			}
		}

		/// <summary>
		/// Disables debug logging.
		/// </summary>
		public void Disable()
		{
			lock (_writeLock)
			{
				Flush();
				_writer?.Dispose();
				_writer = null;
				_enabledLevels = DebugLevel.None;
			}
		}

		/// <summary>
		/// Sets which debug levels are enabled.
		/// </summary>
		public void SetLevels(DebugLevel levels)
		{
			_enabledLevels = levels;
		}

		/// <summary>
		/// Logs a debug message if the specified level is enabled.
		/// </summary>
		public void Log(DebugLevel level, string category, string message)
		{
			if (!IsEnabled || (_enabledLevels & level) == 0)
				return;

			var timestamp = _stopwatch.Elapsed.TotalMilliseconds;
			var indent = new string(' ', _scopeDepth * 2);
			var logEntry = $"[{timestamp:F3}ms] [DEPTH:{_scopeDepth}] [{level}] {category}: {indent}{message}";

			_logBuffer.Enqueue(logEntry);

			// Auto-flush if buffer gets large (buffered writes for performance)
			if (_logBuffer.Count > 1000)
			{
				Flush();
			}
		}

		/// <summary>
		/// Begins a logging scope (increases indent depth).
		/// </summary>
		public void BeginScope(string operation)
		{
			Log(DebugLevel.FRAME, "Scope", $"BEGIN: {operation}");
			Interlocked.Increment(ref _scopeDepth);
		}

		/// <summary>
		/// Ends a logging scope (decreases indent depth).
		/// </summary>
		public void EndScope(string operation, double elapsedMs = 0)
		{
			Interlocked.Decrement(ref _scopeDepth);
			if (_scopeDepth < 0) _scopeDepth = 0;

			var message = elapsedMs > 0
				? $"END: {operation} ({elapsedMs:F2}ms)"
				: $"END: {operation}";

			Log(DebugLevel.FRAME, "Scope", message);
		}

		/// <summary>
		/// Flushes buffered log entries to disk.
		/// </summary>
		public void Flush()
		{
			if (_writer == null || _logBuffer.IsEmpty)
				return;

			lock (_writeLock)
			{
				while (_logBuffer.TryDequeue(out var entry))
				{
					_writer.WriteLine(entry);
				}
				_writer.Flush();
			}
		}

		private void InitializeLogFile()
		{
			try
			{
				// Close existing writer
				_writer?.Dispose();

				// Create/overwrite log file
				var directory = Path.GetDirectoryName(_logFilePath);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				_writer = new StreamWriter(_logFilePath, append: false, Encoding.UTF8)
				{
					AutoFlush = false // We control flushing for performance
				};

				// Write header
				_writer.WriteLine($"=== SharpConsoleUI Flicker Debug Log ===");
				_writer.WriteLine($"=== Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
				_writer.WriteLine($"=== Enabled Levels: {_enabledLevels} ===");
				_writer.WriteLine();
				_writer.Flush();
			}
			catch (Exception ex)
			{
				// Can't log to console (would corrupt UI), so just disable logging
				_enabledLevels = DebugLevel.None;
				_writer = null;
				System.Diagnostics.Debug.WriteLine($"FlickerDebugLogger: Failed to initialize log file: {ex.Message}");
			}
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;
			_flushTimer?.Dispose();

			lock (_writeLock)
			{
				Flush();
				_writer?.Dispose();
				_writer = null;
			}
		}
	}
}
