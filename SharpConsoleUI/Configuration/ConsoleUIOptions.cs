// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration options for SharpConsoleUI
/// </summary>
public sealed record ConsoleUIOptions
{
    /// <summary>
    /// Gets or sets the render mode
    /// </summary>
    public RenderMode RenderMode { get; init; } = RenderMode.Buffer;

    /// <summary>
    /// Gets or sets the idle time in milliseconds
    /// </summary>
    [Range(1, 10000)]
    public int IdleTimeMs { get; init; } = 10;

    /// <summary>
    /// Gets or sets whether to show the task bar
    /// </summary>
    public bool ShowTaskBar { get; init; } = true;

    /// <summary>
    /// Gets or sets the top status bar text
    /// </summary>
    public string? TopStatus { get; init; }

    /// <summary>
    /// Gets or sets the bottom status bar text
    /// </summary>
    public string? BottomStatus { get; init; }

    /// <summary>
    /// Gets or sets whether to enable plugin support
    /// </summary>
    public bool EnablePlugins { get; init; } = false;

    /// <summary>
    /// Gets or sets the plugin directory path
    /// </summary>
    public string? PluginDirectory { get; init; } = "plugins";

    /// <summary>
    /// Gets or sets performance monitoring options
    /// </summary>
    public PerformanceOptions Performance { get; init; } = new();

    /// <summary>
    /// Gets or sets input handling options
    /// </summary>
    public InputOptions Input { get; init; } = new();
}

/// <summary>
/// Performance monitoring options
/// </summary>
public sealed record PerformanceOptions
{
    /// <summary>
    /// Gets or sets whether to enable performance monitoring
    /// </summary>
    public bool EnableMonitoring { get; init; } = false;

    /// <summary>
    /// Gets or sets the performance sampling interval in milliseconds
    /// </summary>
    [Range(100, 60000)]
    public int SamplingIntervalMs { get; init; } = 1000;

    /// <summary>
    /// Gets or sets whether to log slow operations
    /// </summary>
    public bool LogSlowOperations { get; init; } = true;

    /// <summary>
    /// Gets or sets the slow operation threshold in milliseconds
    /// </summary>
    [Range(1, 10000)]
    public int SlowOperationThresholdMs { get; init; } = 100;
}

/// <summary>
/// Input handling options
/// </summary>
public sealed record InputOptions
{
    /// <summary>
    /// Gets or sets the double-click detection time in milliseconds
    /// </summary>
    [Range(100, 2000)]
    public int DoubleClickTimeMs { get; init; } = 500;

    /// <summary>
    /// Gets or sets the key repeat delay in milliseconds
    /// </summary>
    [Range(100, 2000)]
    public int KeyRepeatDelayMs { get; init; } = 500;

    /// <summary>
    /// Gets or sets the key repeat rate in milliseconds
    /// </summary>
    [Range(10, 500)]
    public int KeyRepeatRateMs { get; init; } = 50;

    /// <summary>
    /// Gets or sets whether to enable mouse input
    /// </summary>
    public bool EnableMouseInput { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to enable keyboard shortcuts
    /// </summary>
    public bool EnableKeyboardShortcuts { get; init; } = true;
}