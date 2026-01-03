// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;

namespace SharpConsoleUI.Builders;

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
