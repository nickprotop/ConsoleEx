// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Represents a size with width and height dimensions.
	/// Immutable value type for layout calculations.
	/// Named LayoutSize to avoid conflicts with System.Drawing.Size during transition.
	/// </summary>
	public readonly record struct LayoutSize(int Width, int Height)
	{
		/// <summary>
		/// Gets a size with zero width and height.
		/// </summary>
		public static LayoutSize Zero => new(0, 0);

		/// <summary>
		/// Gets a size with maximum possible dimensions.
		/// </summary>
		public static LayoutSize Infinite => new(int.MaxValue, int.MaxValue);

		/// <summary>
		/// Gets whether this size has zero area.
		/// </summary>
		public bool IsEmpty => Width <= 0 || Height <= 0;

		/// <summary>
		/// Returns a new size with the width constrained to the specified maximum.
		/// </summary>
		public LayoutSize WithMaxWidth(int maxWidth) => new(Math.Min(Width, maxWidth), Height);

		/// <summary>
		/// Returns a new size with the height constrained to the specified maximum.
		/// </summary>
		public LayoutSize WithMaxHeight(int maxHeight) => new(Width, Math.Min(Height, maxHeight));

		/// <summary>
		/// Returns a new size with both dimensions constrained to the specified maximums.
		/// </summary>
		public LayoutSize Constrain(int maxWidth, int maxHeight) =>
			new(Math.Min(Width, maxWidth), Math.Min(Height, maxHeight));

		/// <summary>
		/// Returns a new size with both dimensions constrained to the specified size.
		/// </summary>
		public LayoutSize Constrain(LayoutSize max) => Constrain(max.Width, max.Height);

		/// <summary>
		/// Returns a new size expanded to at least the specified minimums.
		/// </summary>
		public LayoutSize Expand(int minWidth, int minHeight) =>
			new(Math.Max(Width, minWidth), Math.Max(Height, minHeight));

		/// <summary>
		/// Returns a new size expanded to at least the specified size.
		/// </summary>
		public LayoutSize Expand(LayoutSize min) => Expand(min.Width, min.Height);

		/// <summary>
		/// Returns a new size clamped between minimum and maximum bounds.
		/// </summary>
		public LayoutSize Clamp(LayoutSize min, LayoutSize max) =>
			new(
				Math.Clamp(Width, min.Width, max.Width),
				Math.Clamp(Height, min.Height, max.Height)
			);

		/// <summary>
		/// Converts to System.Drawing.Size.
		/// </summary>
		public System.Drawing.Size ToDrawingSize() => new(Width, Height);

		/// <summary>
		/// Creates from System.Drawing.Size.
		/// </summary>
		public static LayoutSize FromDrawingSize(System.Drawing.Size size) =>
			new(size.Width, size.Height);

		/// <summary>Returns a string representation of this size.</summary>
		public override string ToString() => $"LayoutSize({Width}, {Height})";
	}
}
