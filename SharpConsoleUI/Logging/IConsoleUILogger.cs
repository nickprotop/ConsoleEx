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
/// Specialized logger interface for SharpConsoleUI with structured logging support
/// </summary>
public interface IConsoleUILogger
{
    /// <summary>
    /// Logs a window operation (creation, activation, closing)
    /// </summary>
    /// <param name="operation">The window operation</param>
    /// <param name="windowId">The window identifier</param>
    /// <param name="details">Additional details</param>
    void LogWindowOperation(string operation, string windowId, object? details = null);

    /// <summary>
    /// Logs a rendering operation
    /// </summary>
    /// <param name="operation">The render operation</param>
    /// <param name="duration">Duration in milliseconds</param>
    /// <param name="details">Additional details</param>
    void LogRenderOperation(string operation, long duration, object? details = null);

    /// <summary>
    /// Logs an input event
    /// </summary>
    /// <param name="eventType">The input event type</param>
    /// <param name="details">Event details</param>
    void LogInputEvent(string eventType, object? details = null);

    /// <summary>
    /// Logs a plugin operation
    /// </summary>
    /// <param name="operation">The plugin operation</param>
    /// <param name="pluginName">The plugin name</param>
    /// <param name="details">Additional details</param>
    void LogPluginOperation(string operation, string pluginName, object? details = null);

    /// <summary>
    /// Logs a performance metric
    /// </summary>
    /// <param name="metricName">The metric name</param>
    /// <param name="value">The metric value</param>
    /// <param name="unit">The unit of measurement</param>
    void LogPerformanceMetric(string metricName, double value, string unit = "ms");

    /// <summary>
    /// Logs an error with context
    /// </summary>
    /// <param name="exception">The exception</param>
    /// <param name="context">Additional context</param>
    void LogError(Exception exception, object? context = null);
}

/// <summary>
/// Default implementation of IConsoleUILogger that wraps Microsoft.Extensions.Logging
/// </summary>
public sealed class ConsoleUILogger : IConsoleUILogger
{
    private readonly ILogger<ConsoleUILogger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleUILogger"/> class
    /// </summary>
    /// <param name="logger">The underlying logger</param>
    public ConsoleUILogger(ILogger<ConsoleUILogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void LogWindowOperation(string operation, string windowId, object? details = null)
    {
        _logger.LogInformation("Window {Operation} for {WindowId} with details {Details}",
            operation, windowId, details);
    }

    /// <inheritdoc />
    public void LogRenderOperation(string operation, long duration, object? details = null)
    {
        _logger.LogDebug("Render {Operation} completed in {Duration}ms with details {Details}",
            operation, duration, details);
    }

    /// <inheritdoc />
    public void LogInputEvent(string eventType, object? details = null)
    {
        _logger.LogDebug("Input event {EventType} with details {Details}",
            eventType, details);
    }

    /// <inheritdoc />
    public void LogPluginOperation(string operation, string pluginName, object? details = null)
    {
        _logger.LogInformation("Plugin {Operation} for {PluginName} with details {Details}",
            operation, pluginName, details);
    }

    /// <inheritdoc />
    public void LogPerformanceMetric(string metricName, double value, string unit = "ms")
    {
        _logger.LogInformation("Performance metric {MetricName}: {Value} {Unit}",
            metricName, value, unit);
    }

    /// <inheritdoc />
    public void LogError(Exception exception, object? context = null)
    {
        _logger.LogError(exception, "Error occurred with context {Context}", context);
    }
}

/// <summary>
/// Static logger factory for SharpConsoleUI
/// </summary>
public static class ConsoleUILoggerFactory
{
    private static ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Sets the logger factory to use for SharpConsoleUI logging
    /// </summary>
    /// <param name="loggerFactory">The logger factory</param>
    public static void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Creates a logger for the specified type
    /// </summary>
    /// <typeparam name="T">The type to create a logger for</typeparam>
    /// <returns>A logger instance</returns>
    public static ILogger<T> CreateLogger<T>() where T : class
    {
        if (_loggerFactory == null)
        {
            throw new InvalidOperationException("Logger factory has not been set. Call SetLoggerFactory first.");
        }

        return _loggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a SharpConsoleUI logger
    /// </summary>
    /// <returns>A SharpConsoleUI logger instance</returns>
    public static IConsoleUILogger CreateConsoleUILogger()
    {
        var logger = CreateLogger<ConsoleUILogger>();
        return new ConsoleUILogger(logger);
    }
}