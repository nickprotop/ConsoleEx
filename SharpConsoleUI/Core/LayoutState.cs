// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Reason for a layout change.
	/// </summary>
	public enum LayoutChangeReason
	{
		/// <summary>Layout changed programmatically.</summary>
		Programmatic,
		/// <summary>Layout changed due to window resize.</summary>
		WindowResize,
		/// <summary>Layout changed due to container resize.</summary>
		ContainerResize,
		/// <summary>Layout changed due to sibling control change.</summary>
		SiblingChange,
		/// <summary>Layout changed due to content size change.</summary>
		ContentSizeChange,
		/// <summary>Layout changed due to requirements change.</summary>
		RequirementsChange,
		/// <summary>Initial layout calculation.</summary>
		Initial
	}

	/// <summary>
	/// Controls express sizing and alignment needs via this record.
	/// Single source of truth for all layout-related information.
	/// </summary>
	public record LayoutRequirements
	{
		/// <summary>Fixed width (null = flexible, uses available space)</summary>
		public int? Width { get; init; }

		/// <summary>Minimum width constraint (null = no minimum, defaults to 1 during layout)</summary>
		public int? MinWidth { get; init; }

		/// <summary>Maximum width constraint (null = unlimited)</summary>
		public int? MaxWidth { get; init; }

		/// <summary>Fixed height (null = flexible)</summary>
		public int? Height { get; init; }

		/// <summary>Minimum height constraint (null = no minimum)</summary>
		public int? MinHeight { get; init; }

		/// <summary>Maximum height constraint (null = unlimited)</summary>
		public int? MaxHeight { get; init; }

		/// <summary>Horizontal alignment within allocated space</summary>
		public Alignment HorizontalAlignment { get; init; } = Alignment.Left;

		/// <summary>Vertical alignment within allocated space</summary>
		public Alignment VerticalAlignment { get; init; } = Alignment.Left;

		/// <summary>Flex factor for proportional sizing when Width is null (1.0 = equal share)</summary>
		public double FlexFactor { get; init; } = 1.0;

		/// <summary>
		/// Default layout requirements with no fixed dimensions and left alignment.
		/// </summary>
		public static readonly LayoutRequirements Default = new();

		/// <summary>Creates fixed-width requirements</summary>
		public static LayoutRequirements Fixed(int width, Alignment alignment = Alignment.Left) => new()
		{
			Width = width,
			HorizontalAlignment = alignment
		};

		/// <summary>Creates fixed-size requirements</summary>
		public static LayoutRequirements Fixed(int width, int height, Alignment horizontalAlignment = Alignment.Left, Alignment verticalAlignment = Alignment.Left) => new()
		{
			Width = width,
			Height = height,
			HorizontalAlignment = horizontalAlignment,
			VerticalAlignment = verticalAlignment
		};

		/// <summary>Creates flexible requirements with optional constraints</summary>
		public static LayoutRequirements Flexible(int? minWidth = null, int? maxWidth = null, Alignment alignment = Alignment.Left) => new()
		{
			MinWidth = minWidth,
			MaxWidth = maxWidth,
			HorizontalAlignment = alignment
		};

		/// <summary>Creates stretch requirements (expands to fill available space)</summary>
		public static LayoutRequirements Stretch() => new()
		{
			HorizontalAlignment = Alignment.Stretch
		};

		/// <summary>Effective minimum width (defaults to 1 if null)</summary>
		public int EffectiveMinWidth => MinWidth ?? 1;

		/// <summary>Effective maximum width (defaults to int.MaxValue if null)</summary>
		public int EffectiveMaxWidth => MaxWidth ?? int.MaxValue;

		/// <summary>Effective minimum height (defaults to 1 if null)</summary>
		public int EffectiveMinHeight => MinHeight ?? 1;

		/// <summary>Effective maximum height (defaults to int.MaxValue if null)</summary>
		public int EffectiveMaxHeight => MaxHeight ?? int.MaxValue;

		/// <summary>True if this has a fixed width (Width is set)</summary>
		public bool IsFixed => Width.HasValue;

		/// <summary>True if this should expand to fill available space</summary>
		public bool IsStretch => HorizontalAlignment == Alignment.Stretch;

		/// <summary>
		/// Clamps a width value to the min/max constraints
		/// </summary>
		public int ClampWidth(int width) => Math.Max(EffectiveMinWidth, Math.Min(EffectiveMaxWidth, width));

		/// <summary>
		/// Clamps a height value to the min/max constraints
		/// </summary>
		public int ClampHeight(int height) => Math.Max(EffectiveMinHeight, Math.Min(EffectiveMaxHeight, height));
	}

	/// <summary>
	/// Represents the allocated space for a control after layout calculation
	/// </summary>
	public record LayoutAllocation
	{
		/// <summary>Width allocated to the control</summary>
		public int AllocatedWidth { get; init; }

		/// <summary>Height allocated to the control</summary>
		public int AllocatedHeight { get; init; }

		/// <summary>True if the allocation is below the control's minimum requirements</summary>
		public bool IsBelowMinimum { get; init; }

		/// <summary>When this allocation was made</summary>
		public DateTime UpdateTime { get; init; } = DateTime.UtcNow;

		/// <summary>
		/// Empty layout allocation with no allocated space.
		/// </summary>
		public static readonly LayoutAllocation Empty = new();
	}

	/// <summary>
	/// Complete layout state for a control, tracking requirements, allocation, and actual dimensions
	/// </summary>
	public record LayoutState
	{
		/// <summary>The control's layout requirements</summary>
		public LayoutRequirements Requirements { get; init; } = LayoutRequirements.Default;

		/// <summary>The space allocated to the control</summary>
		public LayoutAllocation Allocation { get; init; } = LayoutAllocation.Empty;

		/// <summary>Space offered by container</summary>
		public int? AvailableWidth { get; init; }

		/// <summary>Space offered by container</summary>
		public int? AvailableHeight { get; init; }

		/// <summary>What was actually rendered (read-only result)</summary>
		public int? ActualWidth { get; init; }

		/// <summary>What was actually rendered (read-only result)</summary>
		public int? ActualHeight { get; init; }

		/// <summary>When this state was last updated</summary>
		public DateTime UpdateTime { get; init; } = DateTime.UtcNow;

		/// <summary>
		/// Empty layout state with default requirements and no allocation.
		/// </summary>
		public static readonly LayoutState Empty = new();

		/// <summary>
		/// Returns true if the available space has changed from the current state
		/// </summary>
		public bool HasSpaceChanged(int? newWidth, int? newHeight) =>
			AvailableWidth != newWidth || AvailableHeight != newHeight;

		/// <summary>
		/// Returns true if a re-render is needed based on the new available space
		/// </summary>
		public bool NeedsRerender(int? newWidth, int? newHeight)
		{
			// If space changed, need re-render
			if (HasSpaceChanged(newWidth, newHeight))
				return true;

			// If we have a fixed width requirement but actual doesn't match, need re-render
			if (Requirements.Width.HasValue && ActualWidth != Requirements.Width)
				return true;

			return false;
		}
	}
}
