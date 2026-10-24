// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout;

/// <summary>
/// The contract a window's root layout must satisfy: it lays out the window's children, clips
/// them, and owns the window's vertical scroll position.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam the experiment is named for. Before it, <see cref="Windows.WindowRenderer"/>
/// constructed one concrete layout and reached into it by type, which made the window root the
/// last non-polymorphic node in an otherwise polymorphic engine - everything below it dispatches
/// through <see cref="LayoutNodeFactory"/>.
/// </para>
/// <para>
/// Scrolling is part of the contract rather than bolted beside it because a root that bands its
/// children is the only thing that knows which band scrolls. A root with no scrolling region is
/// free to report a zero range and ignore the verbs.
/// </para>
/// </remarks>
public interface IWindowRootLayout : ILayoutContainer, IRegionClippingLayout
{
	/// <summary>Rows the scrolling region is scrolled down by.</summary>
	int ScrollOffset { get; set; }

	/// <summary>The full height of the scrolling region's content.</summary>
	int ScrollableContentHeight { get; }

	/// <summary>The largest valid <see cref="ScrollOffset"/>.</summary>
	int MaxScrollOffset { get; }

	/// <summary>Scrolls by <paramref name="delta"/> rows, clamped to the valid range.</summary>
	void ScrollBy(int delta);

	/// <summary>Scrolls to the first row.</summary>
	void ScrollToTop();

	/// <summary>Scrolls to the last row.</summary>
	void ScrollToBottom();

	/// <summary>Scrolls up by one viewport height.</summary>
	void PageUp();

	/// <summary>Scrolls down by one viewport height.</summary>
	void PageDown();
}
