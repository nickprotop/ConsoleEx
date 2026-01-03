// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Reason for a scroll change
	/// </summary>
	public enum ScrollChangeReason
	{
		/// <summary>Scroll changed programmatically</summary>
		Programmatic,
		/// <summary>Scroll changed via keyboard navigation</summary>
		Keyboard,
		/// <summary>Scroll changed via mouse wheel</summary>
		MouseWheel,
		/// <summary>Scroll changed via scrollbar drag</summary>
		Scrollbar,
		/// <summary>Scroll changed to bring item into view</summary>
		ScrollIntoView,
		/// <summary>Scroll changed due to content size change</summary>
		ContentChange
	}

	/// <summary>
	/// Immutable record representing scroll state for a scrollable element
	/// </summary>
	public record ScrollState
	{
		/// <summary>
		/// Vertical scroll offset (0 = top)
		/// </summary>
		public int VerticalOffset { get; init; }

		/// <summary>
		/// Horizontal scroll offset (0 = left)
		/// </summary>
		public int HorizontalOffset { get; init; }

		/// <summary>
		/// Total content height
		/// </summary>
		public int ContentHeight { get; init; }

		/// <summary>
		/// Total content width
		/// </summary>
		public int ContentWidth { get; init; }

		/// <summary>
		/// Visible viewport height
		/// </summary>
		public int ViewportHeight { get; init; }

		/// <summary>
		/// Visible viewport width
		/// </summary>
		public int ViewportWidth { get; init; }

		/// <summary>
		/// Timestamp when this state was created
		/// </summary>
		public DateTime UpdateTime { get; init; }

		/// <summary>
		/// Creates a new scroll state
		/// </summary>
		public ScrollState()
		{
			UpdateTime = DateTime.UtcNow;
		}

		/// <summary>
		/// Empty scroll state (at origin)
		/// </summary>
		public static readonly ScrollState Empty = new();

		/// <summary>
		/// Gets scroll position as a Point
		/// </summary>
		public Point Position => new(HorizontalOffset, VerticalOffset);

		/// <summary>
		/// Gets whether vertical scrolling is possible
		/// </summary>
		public bool CanScrollVertically => ContentHeight > ViewportHeight;

		/// <summary>
		/// Gets whether horizontal scrolling is possible
		/// </summary>
		public bool CanScrollHorizontally => ContentWidth > ViewportWidth;

		/// <summary>
		/// Gets the maximum vertical scroll offset
		/// </summary>
		public int MaxVerticalOffset => Math.Max(0, ContentHeight - ViewportHeight);

		/// <summary>
		/// Gets the maximum horizontal scroll offset
		/// </summary>
		public int MaxHorizontalOffset => Math.Max(0, ContentWidth - ViewportWidth);

		/// <summary>
		/// Gets vertical scroll progress (0.0 to 1.0)
		/// </summary>
		public double VerticalProgress => MaxVerticalOffset > 0 ? (double)VerticalOffset / MaxVerticalOffset : 0;

		/// <summary>
		/// Gets horizontal scroll progress (0.0 to 1.0)
		/// </summary>
		public double HorizontalProgress => MaxHorizontalOffset > 0 ? (double)HorizontalOffset / MaxHorizontalOffset : 0;

		/// <summary>
		/// Returns true if the specified row is visible
		/// </summary>
		public bool IsRowVisible(int row)
		{
			return row >= VerticalOffset && row < VerticalOffset + ViewportHeight;
		}

		/// <summary>
		/// Returns true if the specified column is visible
		/// </summary>
		public bool IsColumnVisible(int column)
		{
			return column >= HorizontalOffset && column < HorizontalOffset + ViewportWidth;
		}
	}

	/// <summary>
	/// Event arguments for scroll changes
	/// </summary>
	public class ScrollChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The scrollable object whose scroll changed
		/// </summary>
		public object Scrollable { get; }

		/// <summary>
		/// The previous scroll state
		/// </summary>
		public ScrollState PreviousState { get; }

		/// <summary>
		/// The new scroll state
		/// </summary>
		public ScrollState NewState { get; }

		/// <summary>
		/// The reason for the scroll change
		/// </summary>
		public ScrollChangeReason Reason { get; }

		/// <summary>
		/// Whether the vertical offset changed
		/// </summary>
		public bool VerticalOffsetChanged => PreviousState.VerticalOffset != NewState.VerticalOffset;

		/// <summary>
		/// Whether the horizontal offset changed
		/// </summary>
		public bool HorizontalOffsetChanged => PreviousState.HorizontalOffset != NewState.HorizontalOffset;

		/// <summary>
		/// The change in vertical offset
		/// </summary>
		public int VerticalDelta => NewState.VerticalOffset - PreviousState.VerticalOffset;

		/// <summary>
		/// The change in horizontal offset
		/// </summary>
		public int HorizontalDelta => NewState.HorizontalOffset - PreviousState.HorizontalOffset;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScrollChangedEventArgs"/> class.
		/// </summary>
		/// <param name="scrollable">The scrollable object whose scroll position changed.</param>
		/// <param name="previousState">The previous scroll state before the change.</param>
		/// <param name="newState">The new scroll state after the change.</param>
		/// <param name="reason">The reason for the scroll change.</param>
		public ScrollChangedEventArgs(object scrollable, ScrollState previousState, ScrollState newState, ScrollChangeReason reason)
		{
			Scrollable = scrollable;
			PreviousState = previousState;
			NewState = newState;
			Reason = reason;
		}
	}
}
