// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SpectreColor = Spectre.Console.Color;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Models;

/// <summary>
/// Immutable window bounds information
/// </summary>
/// <param name="X">The X coordinate</param>
/// <param name="Y">The Y coordinate</param>
/// <param name="Width">The width</param>
/// <param name="Height">The height</param>
public sealed record WindowBounds(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// Gets the left coordinate
    /// </summary>
    public int Left => X;

    /// <summary>
    /// Gets the top coordinate
    /// </summary>
    public int Top => Y;

    /// <summary>
    /// Gets the right coordinate
    /// </summary>
    public int Right => X + Width - 1;

    /// <summary>
    /// Gets the bottom coordinate
    /// </summary>
    public int Bottom => Y + Height - 1;

    /// <summary>
    /// Gets the center point
    /// </summary>
    public Point Center => new(X + Width / 2, Y + Height / 2);

    /// <summary>
    /// Gets the size
    /// </summary>
    public Size Size => new(Width, Height);

    /// <summary>
    /// Gets the location
    /// </summary>
    public Point Location => new(X, Y);

    /// <summary>
    /// Converts to a Rectangle
    /// </summary>
    /// <returns>A Rectangle representation</returns>
    public Rectangle ToRectangle() => new(X, Y, Width, Height);

    /// <summary>
    /// Creates a new WindowBounds with the specified position
    /// </summary>
    /// <param name="x">The new X coordinate</param>
    /// <param name="y">The new Y coordinate</param>
    /// <returns>A new WindowBounds instance</returns>
    public WindowBounds WithPosition(int x, int y) => this with { X = x, Y = y };

    /// <summary>
    /// Creates a new WindowBounds with the specified size
    /// </summary>
    /// <param name="width">The new width</param>
    /// <param name="height">The new height</param>
    /// <returns>A new WindowBounds instance</returns>
    public WindowBounds WithSize(int width, int height) => this with { Width = width, Height = height };

    /// <summary>
    /// Creates a new WindowBounds offset by the specified amount
    /// </summary>
    /// <param name="deltaX">The X offset</param>
    /// <param name="deltaY">The Y offset</param>
    /// <returns>A new WindowBounds instance</returns>
    public WindowBounds Offset(int deltaX, int deltaY) => this with { X = X + deltaX, Y = Y + deltaY };

    /// <summary>
    /// Determines if this bounds contains the specified point
    /// </summary>
    /// <param name="x">The X coordinate</param>
    /// <param name="y">The Y coordinate</param>
    /// <returns>True if the point is within these bounds</returns>
    public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;

    /// <summary>
    /// Determines if this bounds intersects with another bounds
    /// </summary>
    /// <param name="other">The other bounds</param>
    /// <returns>True if the bounds intersect</returns>
    public bool IntersectsWith(WindowBounds other) =>
        X < other.X + other.Width &&
        X + Width > other.X &&
        Y < other.Y + other.Height &&
        Y + Height > other.Y;

    /// <summary>
    /// Creates a WindowBounds from a Rectangle
    /// </summary>
    /// <param name="rectangle">The rectangle</param>
    /// <returns>A WindowBounds instance</returns>
    public static WindowBounds FromRectangle(Rectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}

/// <summary>
/// Immutable render context information
/// </summary>
/// <param name="Region">The region being rendered</param>
/// <param name="BackgroundColor">The background color</param>
/// <param name="ForegroundColor">The foreground color</param>
/// <param name="ClipBounds">The clipping bounds</param>
/// <param name="IsActive">Whether the context is for an active element</param>
/// <param name="Theme">The theme being used</param>
public sealed record RenderContext(
    WindowBounds Region,
    SpectreColor BackgroundColor,
    SpectreColor ForegroundColor,
    WindowBounds? ClipBounds = null,
    bool IsActive = false,
    string? Theme = null
)
{
    /// <summary>
    /// Creates a new RenderContext with the specified region
    /// </summary>
    /// <param name="region">The new region</param>
    /// <returns>A new RenderContext instance</returns>
    public RenderContext WithRegion(WindowBounds region) => this with { Region = region };

    /// <summary>
    /// Creates a new RenderContext with the specified colors
    /// </summary>
    /// <param name="backgroundColor">The background color</param>
    /// <param name="foregroundColor">The foreground color</param>
    /// <returns>A new RenderContext instance</returns>
    public RenderContext WithColors(SpectreColor backgroundColor, SpectreColor foregroundColor) =>
        this with { BackgroundColor = backgroundColor, ForegroundColor = foregroundColor };

    /// <summary>
    /// Creates a new RenderContext with the specified active state
    /// </summary>
    /// <param name="isActive">The active state</param>
    /// <returns>A new RenderContext instance</returns>
    public RenderContext WithActiveState(bool isActive) => this with { IsActive = isActive };

    /// <summary>
    /// Creates a new RenderContext with clipping bounds
    /// </summary>
    /// <param name="clipBounds">The clipping bounds</param>
    /// <returns>A new RenderContext instance</returns>
    public RenderContext WithClipping(WindowBounds clipBounds) => this with { ClipBounds = clipBounds };

    /// <summary>
    /// Gets the effective render region (considering clipping)
    /// </summary>
    public WindowBounds EffectiveRegion => ClipBounds?.IntersectsWith(Region) == true ?
        GetIntersection(Region, ClipBounds) : Region;

    private static WindowBounds GetIntersection(WindowBounds a, WindowBounds b)
    {
        var left = Math.Max(a.Left, b.Left);
        var top = Math.Max(a.Top, b.Top);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);

        if (left >= right || top >= bottom)
            return new WindowBounds(0, 0, 0, 0);

        return new WindowBounds(left, top, right - left + 1, bottom - top + 1);
    }
}

/// <summary>
/// Immutable input event information
/// </summary>
/// <param name="Timestamp">When the event occurred</param>
/// <param name="Source">The source of the event</param>
/// <param name="WindowId">The target window ID</param>
/// <param name="Position">The position (for mouse events)</param>
public abstract record InputEvent(
    DateTime Timestamp,
    string? Source = null,
    string? WindowId = null,
    Point? Position = null
);

/// <summary>
/// Immutable keyboard input event
/// </summary>
/// <param name="Timestamp">When the event occurred</param>
/// <param name="KeyInfo">The key information</param>
/// <param name="Source">The source of the event</param>
/// <param name="WindowId">The target window ID</param>
public sealed record KeyboardInputEvent(
    DateTime Timestamp,
    ConsoleKeyInfo KeyInfo,
    string? Source = null,
    string? WindowId = null
) : InputEvent(Timestamp, Source, WindowId);

/// <summary>
/// Immutable mouse input event
/// </summary>
/// <param name="Timestamp">When the event occurred</param>
/// <param name="Position">The mouse position</param>
/// <param name="Buttons">The mouse buttons</param>
/// <param name="Source">The source of the event</param>
/// <param name="WindowId">The target window ID</param>
public sealed record MouseInputEvent(
    DateTime Timestamp,
    Point Position,
    MouseFlags Buttons,
    string? Source = null,
    string? WindowId = null
) : InputEvent(Timestamp, Source, WindowId, Position)
{
    // Override to provide non-nullable Position for mouse events
    public new Point Position { get; } = Position;
};

/// <summary>
/// Immutable control state information
/// </summary>
/// <param name="Id">The control ID</param>
/// <param name="Type">The control type</param>
/// <param name="IsVisible">Whether the control is visible</param>
/// <param name="IsEnabled">Whether the control is enabled</param>
/// <param name="HasFocus">Whether the control has focus</param>
/// <param name="Bounds">The control bounds</param>
/// <param name="Properties">Additional properties</param>
public sealed record ControlState(
    string Id,
    Type Type,
    bool IsVisible = true,
    bool IsEnabled = true,
    bool HasFocus = false,
    WindowBounds? Bounds = null,
    IReadOnlyDictionary<string, object>? Properties = null
)
{
    /// <summary>
    /// Creates a new ControlState with the specified visibility
    /// </summary>
    /// <param name="isVisible">The visibility state</param>
    /// <returns>A new ControlState instance</returns>
    public ControlState WithVisibility(bool isVisible) => this with { IsVisible = isVisible };

    /// <summary>
    /// Creates a new ControlState with the specified enabled state
    /// </summary>
    /// <param name="isEnabled">The enabled state</param>
    /// <returns>A new ControlState instance</returns>
    public ControlState WithEnabledState(bool isEnabled) => this with { IsEnabled = isEnabled };

    /// <summary>
    /// Creates a new ControlState with the specified focus state
    /// </summary>
    /// <param name="hasFocus">The focus state</param>
    /// <returns>A new ControlState instance</returns>
    public ControlState WithFocusState(bool hasFocus) => this with { HasFocus = hasFocus };

    /// <summary>
    /// Creates a new ControlState with the specified bounds
    /// </summary>
    /// <param name="bounds">The bounds</param>
    /// <returns>A new ControlState instance</returns>
    public ControlState WithBounds(WindowBounds bounds) => this with { Bounds = bounds };

    /// <summary>
    /// Creates a new ControlState with additional properties
    /// </summary>
    /// <param name="key">The property key</param>
    /// <param name="value">The property value</param>
    /// <returns>A new ControlState instance</returns>
    public ControlState WithProperty(string key, object value)
    {
        var newProperties = Properties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();
        newProperties[key] = value;
        return this with { Properties = newProperties };
    }
}

/// <summary>
/// Immutable performance measurement
/// </summary>
/// <param name="Name">The measurement name</param>
/// <param name="Value">The measured value</param>
/// <param name="Unit">The unit of measurement</param>
/// <param name="Timestamp">When the measurement was taken</param>
/// <param name="Tags">Additional tags</param>
public sealed record PerformanceMeasurement(
    string Name,
    double Value,
    string Unit,
    DateTime Timestamp,
    IReadOnlyDictionary<string, string>? Tags = null
)
{
    /// <summary>
    /// Creates a new measurement with additional tags
    /// </summary>
    /// <param name="key">The tag key</param>
    /// <param name="value">The tag value</param>
    /// <returns>A new PerformanceMeasurement instance</returns>
    public PerformanceMeasurement WithTag(string key, string value)
    {
        var newTags = Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>();
        newTags[key] = value;
        return this with { Tags = newTags };
    }

    /// <summary>
    /// Creates a timing measurement
    /// </summary>
    /// <param name="name">The measurement name</param>
    /// <param name="duration">The duration</param>
    /// <param name="tags">Additional tags</param>
    /// <returns>A new PerformanceMeasurement instance</returns>
    public static PerformanceMeasurement Timing(string name, TimeSpan duration, IReadOnlyDictionary<string, string>? tags = null) =>
        new(name, duration.TotalMilliseconds, "ms", DateTime.UtcNow, tags);

    /// <summary>
    /// Creates a counter measurement
    /// </summary>
    /// <param name="name">The measurement name</param>
    /// <param name="count">The count value</param>
    /// <param name="tags">Additional tags</param>
    /// <returns>A new PerformanceMeasurement instance</returns>
    public static PerformanceMeasurement Counter(string name, long count, IReadOnlyDictionary<string, string>? tags = null) =>
        new(name, count, "count", DateTime.UtcNow, tags);

    /// <summary>
    /// Creates a memory measurement
    /// </summary>
    /// <param name="name">The measurement name</param>
    /// <param name="bytes">The memory size in bytes</param>
    /// <param name="tags">Additional tags</param>
    /// <returns>A new PerformanceMeasurement instance</returns>
    public static PerformanceMeasurement Memory(string name, long bytes, IReadOnlyDictionary<string, string>? tags = null) =>
        new(name, bytes, "bytes", DateTime.UtcNow, tags);
}