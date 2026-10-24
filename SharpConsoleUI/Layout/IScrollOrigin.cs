// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout;

/// <summary>
/// The scroll state a <see cref="ScrollLayout"/> needs from whatever owns it. Lets the layout
/// serve a window root, which has no <c>ScrollablePanelControl</c> to read offsets from.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately narrow. <see cref="ScrollLayout"/> reaches into nine members of
/// <c>ScrollablePanelControl</c>, but only these are about <em>scrolling</em>; the rest
/// (<c>SetChildNodeResolver</c>, <c>ResolveContentMetrics</c>, <c>ComputeFillMetrics</c>,
/// <c>MeasureDOM</c>, <c>TotalContentWidthInternal</c>) are the panel reusing its own self-paint
/// and Fill helpers. A window root has no outer box distinct from the window, no scrollbar
/// chrome inset and no Fill-helper legacy, so lifting those too would put panel implementation
/// detail behind an interface that cannot honestly implement it.
/// </para>
/// <para>
/// The layout keeps its existing panel path untouched: when the node's control IS a panel,
/// nothing about this interface applies and behaviour is byte-identical. This is the second,
/// additive path.
/// </para>
/// </remarks>
internal interface IScrollOrigin
{
	/// <summary>Rows scrolled down. Children are stacked from <c>-VerticalScrollOffset</c>.</summary>
	int VerticalScrollOffset { get; }

	/// <summary>Columns scrolled right.</summary>
	int HorizontalScrollOffset { get; }

	/// <summary>
	/// Whether children lay out at their full content width (so horizontal overflow exists to
	/// scroll through) rather than being constrained to the visible width.
	/// </summary>
	bool HorizontalScrollEnabled { get; }

	/// <summary>
	/// The widest child content, used only when <see cref="HorizontalScrollEnabled"/> is true.
	/// Zero is a valid answer and means "no horizontal overflow".
	/// </summary>
	int TotalContentWidth { get; }
}
