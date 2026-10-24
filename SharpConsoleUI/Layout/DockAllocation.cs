// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout;

/// <summary>
/// The dock allocation rule: the order in which docked children consume the edges of a
/// shrinking rectangle.
/// </summary>
/// <remarks>
/// <para>
/// Its own type because it has two callers that are otherwise unrelated -
/// <see cref="DockRootLayout"/> (the window root) and <see cref="GridLayout"/> (a grid's docked
/// children, allocated outside its tracks) - and because it was briefly written twice. The
/// copies drifted, and the drift cost a real bug: a third caller reaching for it while stacking
/// downward, rather than consuming a shrinking rectangle, had its far-edge run reversed twice.
/// One rule, one implementation, and a documented contract about which traversal it is for.
/// </para>
/// </remarks>
internal static class DockAllocation
{
	/// <summary>
	/// Orders children for allocation: same-edge runs on a far edge are reversed, everything
	/// else keeps declaration order.
	/// </summary>
	/// <param name="children">The children to order, in declaration order.</param>
	/// <param name="skip">
	/// An optional child to leave out. <see cref="DockRootLayout"/> passes its synthetic body
	/// node, which is not a docked child and is allocated separately from whatever is left.
	/// </param>
	/// <returns>The children in the order the edges should be consumed.</returns>
	/// <remarks>
	/// <para>
	/// <b>This order is for consuming a shrinking rectangle.</b> Each child in turn takes a
	/// strip off the rectangle the previous ones left. A caller that instead stacks children
	/// downward from a computed start is performing the OPPOSITE traversal and must NOT use
	/// this - applying the reversal there reverses a second time and puts a help line above the
	/// rule declared before it. <c>ScrollLayout</c>'s sticky bands are exactly that case and
	/// deliberately do not call this.
	/// </para>
	/// <para>
	/// <b>Why far edges reverse.</b> Under the strict WPF rule each bottom child consumes the
	/// bottom edge in turn, so declaring a rule and then a help line puts the help line ABOVE
	/// the rule - correct by the algorithm, backwards to anyone writing a footer top to bottom.
	/// WPF users know to declare footers in reverse; the 90 existing <c>StickyBottom</c> call
	/// sites in this repository do not, and <c>ImageViewerWindow</c> is exactly this shape.
	/// Reversing the consumption order for the far edges makes author order read the way the
	/// screen reads, and makes <see cref="Dock.Bottom"/> a drop-in for
	/// <c>StickyPosition.Bottom</c>.
	/// </para>
	/// <para>
	/// <b>Reversal applies WITHIN a run only.</b> A run of Bottom children is reversed among
	/// themselves and stays where it was declared relative to the Left and Top docks. Moving
	/// every far-edge child to the end instead would make declaration order meaningless across
	/// axes: a status bar declared before a sidebar would still be processed after it, and so
	/// would stop spanning the full width.
	/// </para>
	/// </remarks>
	internal static List<LayoutNode> Order(IReadOnlyList<LayoutNode> children,
		LayoutNode? skip = null)
	{
		var ordered = new List<LayoutNode>(children.Count);
		var run = new List<LayoutNode>();
		Dock runEdge = Dock.None;

		void Flush()
		{
			for (int i = run.Count - 1; i >= 0; i--)
				ordered.Add(run[i]);
			run.Clear();
			runEdge = Dock.None;
		}

		foreach (var child in children)
		{
			if (skip != null && ReferenceEquals(child, skip))
				continue;

			var d = child.Control?.Dock ?? Dock.None;
			bool isFar = d == Dock.Bottom || d == Dock.Right;

			if (isFar && d == runEdge)
			{
				run.Add(child);
				continue;
			}

			Flush();

			if (isFar)
			{
				runEdge = d;
				run.Add(child);
			}
			else
			{
				ordered.Add(child);
			}
		}

		Flush();
		return ordered;
	}
}
