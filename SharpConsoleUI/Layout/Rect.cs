// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Represents a rectangle with position and size.
	/// Immutable value type for layout calculations and hit testing.
	/// Named LayoutRect to avoid conflicts with System.Drawing.Rectangle during transition.
	/// </summary>
	public readonly record struct LayoutRect(int X, int Y, int Width, int Height)
	{
		/// <summary>
		/// Gets an empty rectangle at the origin.
		/// </summary>
		public static LayoutRect Empty => new(0, 0, 0, 0);

		/// <summary>
		/// Gets whether this rectangle has zero or negative area.
		/// </summary>
		public bool IsEmpty => Width <= 0 || Height <= 0;

		/// <summary>
		/// Gets the right edge X coordinate (exclusive).
		/// </summary>
		public int Right => X + Width;

		/// <summary>
		/// Gets the bottom edge Y coordinate (exclusive).
		/// </summary>
		public int Bottom => Y + Height;

		/// <summary>
		/// Gets the size of this rectangle.
		/// </summary>
		public LayoutSize Size => new(Width, Height);

		/// <summary>
		/// Gets the top-left position of this rectangle as X, Y coordinates.
		/// </summary>
		public (int X, int Y) Location => (X, Y);

		/// <summary>
		/// Gets the center point of this rectangle.
		/// </summary>
		public (int X, int Y) Center => (X + Width / 2, Y + Height / 2);

		/// <summary>
		/// Creates a rectangle from position and size.
		/// </summary>
		public static LayoutRect FromPositionAndSize(int x, int y, LayoutSize size) =>
			new(x, y, size.Width, size.Height);

		/// <summary>
		/// Determines whether this rectangle contains the specified point.
		/// </summary>
		public bool Contains(int x, int y) =>
			x >= X && x < Right && y >= Y && y < Bottom;

		/// <summary>
		/// Determines whether this rectangle contains the specified point.
		/// </summary>
		public bool Contains(Point point) => Contains(point.X, point.Y);

		/// <summary>
		/// Determines whether this rectangle fully contains another rectangle.
		/// </summary>
		public bool Contains(LayoutRect other) =>
			other.X >= X && other.Right <= Right &&
			other.Y >= Y && other.Bottom <= Bottom;

		/// <summary>
		/// Determines whether this rectangle intersects with another rectangle.
		/// </summary>
		public bool IntersectsWith(LayoutRect other) =>
			X < other.Right && Right > other.X &&
			Y < other.Bottom && Bottom > other.Y;

		/// <summary>
		/// Returns the intersection of this rectangle with another, or Empty if they don't intersect.
		/// </summary>
		public LayoutRect Intersect(LayoutRect other)
		{
			int x = Math.Max(X, other.X);
			int y = Math.Max(Y, other.Y);
			int right = Math.Min(Right, other.Right);
			int bottom = Math.Min(Bottom, other.Bottom);

			if (right > x && bottom > y)
				return new LayoutRect(x, y, right - x, bottom - y);

			return Empty;
		}

		/// <summary>
		/// Returns the smallest rectangle that contains both this and another rectangle.
		/// </summary>
		public LayoutRect Union(LayoutRect other)
		{
			if (IsEmpty) return other;
			if (other.IsEmpty) return this;

			int x = Math.Min(X, other.X);
			int y = Math.Min(Y, other.Y);
			int right = Math.Max(Right, other.Right);
			int bottom = Math.Max(Bottom, other.Bottom);

			return new LayoutRect(x, y, right - x, bottom - y);
		}

		/// <summary>
		/// Returns a new rectangle offset by the specified amounts.
		/// </summary>
		public LayoutRect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

		/// <summary>
		/// Returns a new rectangle with the position set to the specified coordinates.
		/// </summary>
		public LayoutRect WithPosition(int x, int y) => new(x, y, Width, Height);

		/// <summary>
		/// Returns a new rectangle with the size set to the specified dimensions.
		/// </summary>
		public LayoutRect WithSize(int width, int height) => new(X, Y, width, height);

		/// <summary>
		/// Returns a new rectangle with the size set to the specified size.
		/// </summary>
		public LayoutRect WithSize(LayoutSize size) => new(X, Y, size.Width, size.Height);

		/// <summary>
		/// Returns a new rectangle inflated by the specified amounts on all sides.
		/// </summary>
		public LayoutRect Inflate(int horizontal, int vertical) =>
			new(X - horizontal, Y - vertical, Width + horizontal * 2, Height + vertical * 2);

		/// <summary>
		/// Returns a new rectangle deflated by the specified amounts on all sides.
		/// </summary>
		public LayoutRect Deflate(int horizontal, int vertical) =>
			new(X + horizontal, Y + vertical,
				Math.Max(0, Width - horizontal * 2),
				Math.Max(0, Height - vertical * 2));

		/// <summary>
		/// Converts to System.Drawing.Rectangle.
		/// </summary>
		public Rectangle ToRectangle() => new(X, Y, Width, Height);

		/// <summary>
		/// Creates from System.Drawing.Rectangle.
		/// </summary>
		public static LayoutRect FromRectangle(Rectangle rect) =>
			new(rect.X, rect.Y, rect.Width, rect.Height);

		/// <summary>Returns a string representation of this rectangle.</summary>
		public override string ToString() => $"LayoutRect({X}, {Y}, {Width}, {Height})";
	}
}
