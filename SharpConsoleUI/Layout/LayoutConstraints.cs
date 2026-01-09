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
	/// Represents layout constraints for measuring controls.
	/// Defines minimum and maximum width/height bounds.
	/// </summary>
	public readonly record struct LayoutConstraints(
		int MinWidth,
		int MaxWidth,
		int MinHeight,
		int MaxHeight
	)
	{
		/// <summary>
		/// Gets unbounded constraints (0 to MaxValue).
		/// </summary>
		public static LayoutConstraints Unbounded =>
			new(0, int.MaxValue, 0, int.MaxValue);

		/// <summary>
		/// Gets zero-size constraints.
		/// </summary>
		public static LayoutConstraints Zero =>
			new(0, 0, 0, 0);

		/// <summary>
		/// Creates constraints with fixed width and height.
		/// </summary>
		public static LayoutConstraints Fixed(int width, int height) =>
			new(width, width, height, height);

		/// <summary>
		/// Creates constraints with fixed size.
		/// </summary>
		public static LayoutConstraints Fixed(LayoutSize size) =>
			Fixed(size.Width, size.Height);

		/// <summary>
		/// Creates constraints with maximum bounds but no minimum.
		/// </summary>
		public static LayoutConstraints Loose(int maxWidth, int maxHeight) =>
			new(0, maxWidth, 0, maxHeight);

		/// <summary>
		/// Creates constraints with maximum bounds but no minimum.
		/// </summary>
		public static LayoutConstraints Loose(LayoutSize maxSize) =>
			Loose(maxSize.Width, maxSize.Height);

		/// <summary>
		/// Creates tight constraints where min equals max.
		/// </summary>
		public static LayoutConstraints Tight(int width, int height) =>
			new(width, width, height, height);

		/// <summary>
		/// Creates tight constraints where min equals max.
		/// </summary>
		public static LayoutConstraints Tight(LayoutSize size) =>
			Tight(size.Width, size.Height);

		/// <summary>
		/// Gets whether the width is tightly constrained (min == max).
		/// </summary>
		public bool HasTightWidth => MinWidth == MaxWidth;

		/// <summary>
		/// Gets whether the height is tightly constrained (min == max).
		/// </summary>
		public bool HasTightHeight => MinHeight == MaxHeight;

		/// <summary>
		/// Gets whether both dimensions are tightly constrained.
		/// </summary>
		public bool IsTight => HasTightWidth && HasTightHeight;

		/// <summary>
		/// Gets the maximum size from these constraints.
		/// </summary>
		public LayoutSize MaxSize => new(MaxWidth, MaxHeight);

		/// <summary>
		/// Gets the minimum size from these constraints.
		/// </summary>
		public LayoutSize MinSize => new(MinWidth, MinHeight);

		/// <summary>
		/// Returns constraints with a new maximum width.
		/// </summary>
		public LayoutConstraints WithMaxWidth(int maxWidth) =>
			this with { MaxWidth = Math.Max(MinWidth, maxWidth) };

		/// <summary>
		/// Returns constraints with a new maximum height.
		/// </summary>
		public LayoutConstraints WithMaxHeight(int maxHeight) =>
			this with { MaxHeight = Math.Max(MinHeight, maxHeight) };

		/// <summary>
		/// Returns constraints with a new minimum width.
		/// </summary>
		public LayoutConstraints WithMinWidth(int minWidth) =>
			this with { MinWidth = Math.Min(minWidth, MaxWidth) };

		/// <summary>
		/// Returns constraints with a new minimum height.
		/// </summary>
		public LayoutConstraints WithMinHeight(int minHeight) =>
			this with { MinHeight = Math.Min(minHeight, MaxHeight) };

		/// <summary>
		/// Returns constraints with the maximum height reduced by the specified amount.
		/// </summary>
		public LayoutConstraints SubtractHeight(int height) =>
			this with { MaxHeight = Math.Max(0, MaxHeight - height) };

		/// <summary>
		/// Returns constraints with the maximum width reduced by the specified amount.
		/// </summary>
		public LayoutConstraints SubtractWidth(int width) =>
			this with { MaxWidth = Math.Max(0, MaxWidth - width) };

		/// <summary>
		/// Constrains a size to fit within these bounds.
		/// </summary>
		public LayoutSize Constrain(LayoutSize size) =>
			new(
				Math.Clamp(size.Width, MinWidth, MaxWidth),
				Math.Clamp(size.Height, MinHeight, MaxHeight)
			);

		/// <summary>
		/// Constrains dimensions to fit within these bounds.
		/// </summary>
		public LayoutSize Constrain(int width, int height) =>
			Constrain(new LayoutSize(width, height));

		/// <summary>
		/// Returns whether a size satisfies these constraints.
		/// </summary>
		public bool IsSatisfiedBy(LayoutSize size) =>
			size.Width >= MinWidth && size.Width <= MaxWidth &&
			size.Height >= MinHeight && size.Height <= MaxHeight;

		/// <summary>
		/// Returns constraints that are the intersection of this and another.
		/// </summary>
		public LayoutConstraints Intersect(LayoutConstraints other) =>
			new(
				Math.Max(MinWidth, other.MinWidth),
				Math.Min(MaxWidth, other.MaxWidth),
				Math.Max(MinHeight, other.MinHeight),
				Math.Min(MaxHeight, other.MaxHeight)
			);

		public override string ToString() =>
			$"Constraints(W: {MinWidth}-{(MaxWidth == int.MaxValue ? "∞" : MaxWidth)}, H: {MinHeight}-{(MaxHeight == int.MaxValue ? "∞" : MaxHeight)})";
	}
}
