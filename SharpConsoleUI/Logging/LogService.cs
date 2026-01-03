// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace SharpConsoleUI.Logging;

/// <summary>
/// Library-managed logging service with circular buffer storage.
/// Thread-safe implementation suitable for multi-threaded console applications.
/// Supports optional file logging via environment variable or code configuration.
/// </summary>
public sealed class LogService : ILogService, IDisposable
{
    private const string DebugLogEnvVar = "SHARPCONSOLEUI_DEBUG_LOG";

    private readonly ConcurrentQueue<LogEntry> _buffer = new();
    private readonly object _lock = new();
    private readonly object _fileLock = new();
    private int _maxBufferSize = 1000;
    private LogLevel _minimumLevel = LogLevel.Warning;
    private bool _isEnabled = true;

    // File logging fields
    private StreamWriter? _fileWriter;
    private string? _logFilePath;

    /// <summary>
    /// Creates a new LogService instance.
    /// Automatically enables file logging if SHARPCONSOLEUI_DEBUG_LOG environment variable is set.
    /// </summary>
    public LogService()
    {
        // Check for environment variable to auto-enable file logging
        var envLogPath = Environment.GetEnvironmentVariable(DebugLogEnvVar);
        if (!string.IsNullOrWhiteSpace(envLogPath))
        {
            try
            {
                EnableFileLogging(envLogPath);
            }
            catch
            {
                // Silently ignore - don't crash if we can't create log file
            }
        }
    }

    /// <inheritdoc />
    public LogLevel MinimumLevel
    {
        get => _minimumLevel;
        set => _minimumLevel = value;
    }

    /// <inheritdoc />
    public int MaxBufferSize
    {
        get => _maxBufferSize;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Buffer size must be at least 1");

            _maxBufferSize = value;
            TrimBuffer();
        }
    }

    /// <inheritdoc />
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <inheritdoc />
    public int Count => _buffer.Count;

    /// <inheritdoc />
    public event EventHandler<LogEntry>? LogAdded;

    /// <inheritdoc />
    public event EventHandler? LogsCleared;

    /// <inheritdoc />
    public void Log(LogLevel level, string message, string? category = null)
    {
        if (!_isEnabled || level < _minimumLevel)
            return;

        var entry = new LogEntry(DateTime.Now, level, message, category);
        AddEntry(entry);
    }

    /// <inheritdoc />
    public void LogTrace(string message, string? category = null)
        => Log(LogLevel.Trace, message, category);

    /// <inheritdoc />
    public void LogDebug(string message, string? category = null)
        => Log(LogLevel.Debug, message, category);

    /// <inheritdoc />
    public void LogInfo(string message, string? category = null)
        => Log(LogLevel.Information, message, category);

    /// <inheritdoc />
    public void LogWarning(string message, string? category = null)
        => Log(LogLevel.Warning, message, category);

    /// <inheritdoc />
    public void LogError(string message, Exception? exception = null, string? category = null)
    {
        if (!_isEnabled || LogLevel.Error < _minimumLevel)
            return;

        var entry = new LogEntry(DateTime.Now, LogLevel.Error, message, category, exception);
        AddEntry(entry);
    }

    /// <inheritdoc />
    public void LogCritical(string message, Exception? exception = null, string? category = null)
    {
        if (!_isEnabled || LogLevel.Critical < _minimumLevel)
            return;

        var entry = new LogEntry(DateTime.Now, LogLevel.Critical, message, category, exception);
        AddEntry(entry);
    }

    /// <inheritdoc />
    public IReadOnlyList<LogEntry> GetRecentLogs(int count = 100)
    {
        var entries = _buffer.ToArray();
        return entries
            .Skip(Math.Max(0, entries.Length - count))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<LogEntry> GetAllLogs()
    {
        return _buffer.ToArray().ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public void ClearLogs()
    {
        lock (_lock)
        {
            while (_buffer.TryDequeue(out _)) { }
        }
        LogsCleared?.Invoke(this, EventArgs.Empty);
    }

    private void AddEntry(LogEntry entry)
    {
        _buffer.Enqueue(entry);
        TrimBuffer();

        // Write to file if enabled
        if (_fileWriter != null)
        {
            WriteToFile(entry);
        }

        // Raise event (fire and forget, don't let subscriber exceptions affect logging)
        try
        {
            LogAdded?.Invoke(this, entry);
        }
        catch
        {
            // Swallow exceptions from event handlers to prevent logging from breaking
        }
    }

    private void TrimBuffer()
    {
        while (_buffer.Count > _maxBufferSize && _buffer.TryDequeue(out _))
        {
            // Keep removing until we're within size limits
        }
    }

    #region File Logging

    /// <inheritdoc />
    public bool IsFileLoggingEnabled => _fileWriter != null;

    /// <inheritdoc />
    public void EnableFileLogging(string filePath, bool append = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        lock (_fileLock)
        {
            // Close any existing file first
            DisableFileLoggingInternal();

            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _logFilePath = filePath;
            _fileWriter = new StreamWriter(filePath, append) { AutoFlush = true };

            // Log that file logging started
            WriteToFile(new LogEntry(DateTime.Now, LogLevel.Information,
                $"File logging started: {filePath}", "LogService"));
        }
    }

    /// <inheritdoc />
    public void DisableFileLogging()
    {
        lock (_fileLock)
        {
            DisableFileLoggingInternal();
        }
    }

    private void DisableFileLoggingInternal()
    {
        if (_fileWriter != null)
        {
            try
            {
                _fileWriter.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
            _fileWriter = null;
            _logFilePath = null;
        }
    }

    private void WriteToFile(LogEntry entry)
    {
        if (_fileWriter == null) return;

        lock (_fileLock)
        {
            if (_fileWriter == null) return;

            try
            {
                var line = FormatLogLine(entry);
                _fileWriter.WriteLine(line);

                if (entry.Exception != null)
                {
                    // Indent exception details
                    var exceptionLines = entry.Exception.ToString().Split('\n');
                    foreach (var exLine in exceptionLines)
                    {
                        _fileWriter.WriteLine($"    {exLine.TrimEnd()}");
                    }
                }
            }
            catch
            {
                // Silently ignore file write errors to prevent logging from breaking
            }
        }
    }

    private static string FormatLogLine(LogEntry entry)
    {
        var level = entry.Level.ToString().ToUpper().PadRight(5);
        var category = (entry.Category ?? "General").PadRight(12);
        return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{category}] {entry.Message}";
    }

    /// <summary>
    /// Disposes resources used by the log service
    /// </summary>
    public void Dispose()
    {
        DisableFileLogging();
    }

    #endregion
}
