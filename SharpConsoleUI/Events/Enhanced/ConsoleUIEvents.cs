// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Events.Enhanced;

/// <summary>
/// Base class for all SharpConsoleUI events
/// </summary>
public abstract record ConsoleUIEvent
{
    /// <summary>
    /// Gets the timestamp when the event was created
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether the event should be cancelled
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Gets or sets whether the event has been handled
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Gets the source component that raised the event
    /// </summary>
    public object? Source { get; init; }
}

/// <summary>
/// Event raised when a window is about to be created
/// </summary>
/// <param name="WindowId">The window identifier</param>
/// <param name="Title">The window title</param>
/// <param name="Bounds">The window bounds</param>
public sealed record WindowCreatingEvent(
    string WindowId,
    string? Title,
    Rectangle Bounds
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a window has been created
/// </summary>
/// <param name="WindowId">The window identifier</param>
/// <param name="Window">The created window</param>
public sealed record WindowCreatedEvent(
    string WindowId,
    Window Window
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a window is about to be closed
/// </summary>
/// <param name="WindowId">The window identifier</param>
/// <param name="Reason">The close reason</param>
public sealed record WindowClosingEvent(
    string WindowId,
    string Reason
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a window has been closed
/// </summary>
/// <param name="WindowId">The window identifier</param>
/// <param name="Reason">The close reason</param>
public sealed record WindowClosedEvent(
    string WindowId,
    string Reason
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a window becomes active
/// </summary>
/// <param name="WindowId">The window identifier</param>
/// <param name="PreviousActiveWindowId">The previously active window ID</param>
public sealed record WindowActivatedEvent(
    string WindowId,
    string? PreviousActiveWindowId
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a window becomes inactive
/// </summary>
/// <param name="WindowId">The window identifier</param>
/// <param name="NextActiveWindowId">The next active window ID</param>
public sealed record WindowDeactivatedEvent(
    string WindowId,
    string? NextActiveWindowId
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a window is resized
/// </summary>
/// <param name="WindowId">The window identifier</param>
/// <param name="OldSize">The old size</param>
/// <param name="NewSize">The new size</param>
public sealed record WindowResizedEvent(
    string WindowId,
    Size OldSize,
    Size NewSize
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a window is moved
/// </summary>
/// <param name="WindowId">The window identifier</param>
/// <param name="OldPosition">The old position</param>
/// <param name="NewPosition">The new position</param>
public sealed record WindowMovedEvent(
    string WindowId,
    Point OldPosition,
    Point NewPosition
) : ConsoleUIEvent;

/// <summary>
/// Event raised when rendering begins
/// </summary>
/// <param name="RenderRegion">The region being rendered</param>
public sealed record RenderStartedEvent(
    Rectangle RenderRegion
) : ConsoleUIEvent;

/// <summary>
/// Event raised when rendering completes
/// </summary>
/// <param name="RenderRegion">The region that was rendered</param>
/// <param name="Duration">The render duration</param>
public sealed record RenderCompletedEvent(
    Rectangle RenderRegion,
    TimeSpan Duration
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a key is pressed
/// </summary>
/// <param name="KeyInfo">The key information</param>
/// <param name="TargetWindowId">The target window ID</param>
public sealed record KeyPressedEvent(
    ConsoleKeyInfo KeyInfo,
    string? TargetWindowId
) : ConsoleUIEvent;

/// <summary>
/// Event raised when mouse input occurs
/// </summary>
/// <param name="Position">The mouse position</param>
/// <param name="Buttons">The mouse button flags</param>
/// <param name="TargetWindowId">The target window ID</param>
public sealed record MouseInputEvent(
    Point Position,
    MouseFlags Buttons,
    string? TargetWindowId
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a control gets focus
/// </summary>
/// <param name="ControlId">The control identifier</param>
/// <param name="ControlType">The control type</param>
/// <param name="WindowId">The parent window ID</param>
public sealed record ControlFocusedEvent(
    string ControlId,
    Type ControlType,
    string WindowId
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a control loses focus
/// </summary>
/// <param name="ControlId">The control identifier</param>
/// <param name="ControlType">The control type</param>
/// <param name="WindowId">The parent window ID</param>
public sealed record ControlUnfocusedEvent(
    string ControlId,
    Type ControlType,
    string WindowId
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a plugin is loaded
/// </summary>
/// <param name="PluginName">The plugin name</param>
/// <param name="PluginVersion">The plugin version</param>
/// <param name="LoadDuration">The load duration</param>
public sealed record PluginLoadedEvent(
    string PluginName,
    string PluginVersion,
    TimeSpan LoadDuration
) : ConsoleUIEvent;

/// <summary>
/// Event raised when a plugin fails to load
/// </summary>
/// <param name="PluginName">The plugin name</param>
/// <param name="Error">The error that occurred</param>
public sealed record PluginLoadFailedEvent(
    string PluginName,
    Exception Error
) : ConsoleUIEvent;

/// <summary>
/// Event raised when the theme changes
/// </summary>
/// <param name="OldThemeName">The old theme name</param>
/// <param name="NewThemeName">The new theme name</param>
public sealed record ThemeChangedEvent(
    string? OldThemeName,
    string NewThemeName
) : ConsoleUIEvent;

/// <summary>
/// Event raised when configuration changes
/// </summary>
/// <param name="SectionName">The configuration section that changed</param>
/// <param name="ChangeType">The type of change</param>
public sealed record ConfigurationChangedEvent(
    string SectionName,
    string ChangeType
) : ConsoleUIEvent;

/// <summary>
/// Event raised when an error occurs
/// </summary>
/// <param name="Error">The error that occurred</param>
/// <param name="Context">Additional context</param>
public sealed record ErrorOccurredEvent(
    Exception Error,
    string? Context
) : ConsoleUIEvent;

/// <summary>
/// Event raised for performance metrics
/// </summary>
/// <param name="MetricName">The metric name</param>
/// <param name="Value">The metric value</param>
/// <param name="Unit">The unit of measurement</param>
/// <param name="Tags">Additional tags</param>
public sealed record PerformanceMetricEvent(
    string MetricName,
    double Value,
    string Unit,
    Dictionary<string, string>? Tags = null
) : ConsoleUIEvent;