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
/// Represents a single log entry in the library's internal log buffer
/// </summary>
/// <param name="Timestamp">When the log entry was created</param>
/// <param name="Level">The severity level of the log entry</param>
/// <param name="Message">The log message</param>
/// <param name="Category">Optional category for grouping related logs</param>
/// <param name="Exception">Optional exception associated with this log entry</param>
public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    string? Category = null,
    Exception? Exception = null
)
{
    /// <summary>
    /// Returns a formatted string representation of this log entry
    /// </summary>
    public override string ToString()
    {
        var categoryPart = string.IsNullOrEmpty(Category) ? "" : $"[{Category}] ";
        var exceptionPart = Exception != null ? $" | {Exception.GetType().Name}: {Exception.Message}" : "";
        return $"[{Timestamp:HH:mm:ss}] [{Level}] {categoryPart}{Message}{exceptionPart}";
    }

    /// <summary>
    /// Returns a markup-formatted string for display in Spectre.Console controls
    /// </summary>
    public string ToMarkup()
    {
        var levelColor = Level switch
        {
            LogLevel.Trace => "dim",
            LogLevel.Debug => "grey",
            LogLevel.Information => "white",
            LogLevel.Warning => "yellow",
            LogLevel.Error => "red",
            LogLevel.Critical => "red bold",
            _ => "white"
        };

        var categoryPart = string.IsNullOrEmpty(Category) ? "" : $"[dim][[{Category}]][/] ";
        var exceptionPart = Exception != null ? $" [red]| {Exception.GetType().Name}[/]" : "";
        var escapedMessage = Message.Replace("[", "[[").Replace("]", "]]");

        return $"[dim]{Timestamp:HH:mm:ss}[/] [{levelColor}]{Level,-11}[/] {categoryPart}{escapedMessage}{exceptionPart}";
    }
}
