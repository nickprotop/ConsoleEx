// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Specifies whether a control should stick to the top or bottom of its container during scrolling.
	/// </summary>
	public enum StickyPosition
	{
		/// <summary>The control scrolls normally with other content.</summary>
		None,
		/// <summary>The control remains fixed at the top of the visible area.</summary>
		Top,
		/// <summary>The control remains fixed at the bottom of the visible area.</summary>
		Bottom
	}

	/// <summary>
	/// Represents a UI control that can be displayed within a window or container.
	/// </summary>
	public interface IWindowControl : IDisposable
	{
		/// <summary>Gets the actual rendered width of the control, or null if not yet rendered.</summary>
		int? ActualWidth { get; }

		/// <summary>Gets or sets the horizontal alignment of the control within its container.</summary>
		HorizontalAlignment HorizontalAlignment { get; set; }

		/// <summary>Gets or sets the vertical alignment of the control within its container.</summary>
		VerticalAlignment VerticalAlignment { get; set; }

		/// <summary>Gets or sets the parent container that hosts this control.</summary>
		IContainer? Container { get; set; }

		/// <summary>Gets or sets the margin (spacing) around the control.</summary>
		Margin Margin { get; set; }

		/// <summary>Gets or sets whether this control should stick to the top or bottom during scrolling.</summary>
		StickyPosition StickyPosition { get; set; }

		/// <summary>Gets or sets the unique name identifier for this control, used for lookup.</summary>
		string? Name { get; set; }

		/// <summary>Gets or sets an arbitrary object value that can be used to store custom data.</summary>
		object? Tag { get; set; }

		/// <summary>Gets or sets whether this control is visible.</summary>
		bool Visible { get; set; }

		/// <summary>Gets or sets the explicit width of the control, or null for automatic sizing.</summary>
		int? Width { get; set; }

		/// <summary>
		/// Gets the logical size of the control's content without rendering.
		/// </summary>
		/// <returns>The size representing the content's natural dimensions.</returns>
		Size GetLogicalContentSize();

		/// <summary>
		/// Marks this control as needing to be re-rendered.
		/// </summary>
		void Invalidate();

	}

	/// <summary>
	/// Represents spacing around a control's content.
	/// </summary>
	public struct Margin
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Margin"/> struct.
		/// </summary>
		/// <param name="left">The left margin in characters.</param>
		/// <param name="top">The top margin in lines.</param>
		/// <param name="right">The right margin in characters.</param>
		/// <param name="bottom">The bottom margin in lines.</param>
		public Margin(int left, int top, int right, int bottom)
		{
			Left = left;
			Top = top;
			Right = right;
			Bottom = bottom;
		}

		/// <summary>Gets or sets the bottom margin in lines.</summary>
		public int Bottom { get; set; }

		/// <summary>Gets or sets the left margin in characters.</summary>
		public int Left { get; set; }

		/// <summary>Gets or sets the right margin in characters.</summary>
		public int Right { get; set; }

		/// <summary>Gets or sets the top margin in lines.</summary>
		public int Top { get; set; }
	}

	/// <summary>
	/// Optional interface for controls that participate in layout negotiation.
	/// Controls implementing this interface can express their sizing requirements
	/// and receive notifications when their allocated space changes.
	/// </summary>
	public interface ILayoutAware
	{
		/// <summary>
		/// Gets the control's layout requirements.
		/// Called during the measure phase before painting.
		/// </summary>
		LayoutRequirements GetLayoutRequirements();

		/// <summary>
		/// Notifies the control of its layout allocation.
		/// Called after layout calculation, before painting.
		/// </summary>
		void OnLayoutAllocated(LayoutAllocation allocation);
	}

	/// <summary>
	/// Interface for controls that can provide logical cursor positions.
	/// </summary>
	public interface ILogicalCursorProvider
	{
		/// <summary>
		/// Gets the logical cursor position within the control's content coordinate system.
		/// This should be the raw position without any visual adjustments for margins, scrolling, etc.
		/// </summary>
		/// <returns>Logical cursor position or null if no cursor.</returns>
		Point? GetLogicalCursorPosition();

		/// <summary>
		/// Sets the logical cursor position within the control's content coordinate system.
		/// </summary>
		void SetLogicalCursorPosition(Point position);
	}
}