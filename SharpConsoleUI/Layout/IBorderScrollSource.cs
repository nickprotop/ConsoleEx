// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout;

/// <summary>The axis a scrollbar runs along.</summary>
[Flags]
public enum ScrollAxis
{
	/// <summary>Neither axis.</summary>
	None = 0,

	/// <summary>The vertical axis, drawn in the right border.</summary>
	Vertical = 1,

	/// <summary>The horizontal axis, drawn in the bottom border.</summary>
	Horizontal = 2,

	/// <summary>Both axes.</summary>
	Both = Vertical | Horizontal
}

/// <summary>
/// A scrolling region that can drive a window-border scrollbar instead of painting its own.
/// </summary>
/// <remarks>
/// <para>
/// The window frame has always drawn a vertical thumb for the window's own scrolling body. This
/// interface is that relationship named: the border asks a source for its position and extent,
/// and any region able to answer can take the border over, reclaiming the column (or row) it
/// would otherwise spend on a scrollbar of its own.
/// </para>
/// <para>
/// Values are in the source's own units - rows for the vertical axis, columns for the horizontal.
/// A source with nothing to scroll reports an <see cref="Extent"/> no greater than its
/// <see cref="Viewport"/>, and the border draws plain frame instead of a track.
/// </para>
/// </remarks>
public interface IBorderScrollSource
{
	/// <summary>How far the region is scrolled, in its own units.</summary>
	int Offset { get; }

	/// <summary>The full size of the content being scrolled through.</summary>
	int Extent { get; }

	/// <summary>The size of the visible window onto that content.</summary>
	int Viewport { get; }

	/// <summary>Scrolls to <paramref name="offset"/>, clamped by the implementation.</summary>
	/// <param name="offset">The offset to scroll to.</param>
	void ScrollTo(int offset);
}
