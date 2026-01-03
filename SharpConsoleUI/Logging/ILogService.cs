// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace SharpConsoleUI.Logging;

/// <summary>
/// Library-managed logging service that handles all internal logging.
/// Users can subscribe to log events or access the log buffer directly.
/// </summary>
public interface ILogService
{
    #region Core Logging Methods

    /// <summary>
    /// Logs a message at the specified level
    /// </summary>
    /// <param name="level">The log level</param>
    /// <param name="message">The message to log</param>
    /// <param name="category">Optional category for grouping</param>
    void Log(LogLevel level, string message, string? category = null);

    /// <summary>
    /// Logs a trace message (most verbose)
    /// </summary>
    void LogTrace(string message, string? category = null);

    /// <summary>
    /// Logs a debug message
    /// </summary>
    void LogDebug(string message, string? category = null);

    /// <summary>
    /// Logs an informational message
    /// </summary>
    void LogInfo(string message, string? category = null);

    /// <summary>
    /// Logs a warning message
    /// </summary>
    void LogWarning(string message, string? category = null);

    /// <summary>
    /// Logs an error message with optional exception
    /// </summary>
    void LogError(string message, Exception? exception = null, string? category = null);

    /// <summary>
    /// Logs a critical error message with optional exception
    /// </summary>
    void LogCritical(string message, Exception? exception = null, string? category = null);

    #endregion

    #region Buffer Access

    /// <summary>
    /// Gets the most recent log entries
    /// </summary>
    /// <param name="count">Maximum number of entries to return</param>
    /// <returns>Read-only list of log entries, newest first</returns>
    IReadOnlyList<LogEntry> GetRecentLogs(int count = 100);

    /// <summary>
    /// Gets all log entries currently in the buffer
    /// </summary>
    /// <returns>Read-only list of all buffered log entries</returns>
    IReadOnlyList<LogEntry> GetAllLogs();

    /// <summary>
    /// Clears all log entries from the buffer
    /// </summary>
    void ClearLogs();

    /// <summary>
    /// Gets the current number of log entries in the buffer
    /// </summary>
    int Count { get; }

    #endregion

    #region Configuration

    /// <summary>
    /// Gets or sets the minimum log level. Messages below this level are ignored.
    /// Default: Warning
    /// </summary>
    LogLevel MinimumLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of log entries to retain in the buffer.
    /// Default: 1000
    /// </summary>
    int MaxBufferSize { get; set; }

    /// <summary>
    /// Gets or sets whether logging is enabled
    /// </summary>
    bool IsEnabled { get; set; }

    #endregion

    #region File Logging

    /// <summary>
    /// Enables file logging to the specified path.
    /// The file will be created if it doesn't exist.
    /// Parent directories will be created if needed.
    /// </summary>
    /// <param name="filePath">Path to the log file</param>
    /// <param name="append">If true, appends to existing file; if false, overwrites</param>
    void EnableFileLogging(string filePath, bool append = true);

    /// <summary>
    /// Disables file logging and closes the log file
    /// </summary>
    void DisableFileLogging();

    /// <summary>
    /// Gets whether file logging is currently enabled
    /// </summary>
    bool IsFileLoggingEnabled { get; }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a new log entry is added (after passing the minimum level filter)
    /// </summary>
    event EventHandler<LogEntry>? LogAdded;

    /// <summary>
    /// Raised when the log buffer is cleared
    /// </summary>
    event EventHandler? LogsCleared;

    #endregion
}
