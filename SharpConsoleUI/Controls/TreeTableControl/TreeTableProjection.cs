// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// One computation of what a <see cref="TreeTableControl"/> displays: the rows, in order, and what
/// each row looks like there — whether it shows an expander, whether it is open, whether it is the
/// last of the siblings displayed.
/// </summary>
/// <remarks>
/// <para>
/// Computed from a <see cref="TreeTableShape"/> every time the table recomputes its display rows,
/// and never changed afterwards. Painting reads only this, so a row's guides and expander always
/// agree with the display rows the table is painting, whatever the live hierarchy is doing.
/// </para>
/// <para>
/// Everything is computed for every row by data index, in a few linear passes; the markup drawn for
/// a row is built from it only when the row is painted, and kept until the next computation.
/// </para>
/// </remarks>
internal sealed class TreeTableProjection
{
	private TreeTableProjection(TreeTableShape shape)
	{
		int count = shape.Count;
		Shape = shape;
		Displayed = new bool[count];
		HasChildren = new bool[count];
		IsOpen = new bool[count];
		IsExpanded = new bool[count];
		IsLastSibling = new bool[count];
		HasMatchingDescendant = new bool[count];
		GuideMarkup = new string?[count];
		ExpanderMarkup = new string?[count];
		PrefixMarkup = new string?[count];
	}

	/// <summary>The hierarchy this was computed from.</summary>
	internal TreeTableShape Shape { get; }

	/// <summary>The data rows displayed, in display order.</summary>
	internal int[] DisplayRows { get; private set; } = Array.Empty<int>();

	/// <summary>Whether each row is displayed.</summary>
	internal bool[] Displayed { get; }

	/// <summary>
	/// Whether each row shows an expander: it has children to show — children the filter passes, while
	/// filtered — or children not loaded yet.
	/// </summary>
	internal bool[] HasChildren { get; }

	/// <summary>Whether each row is open in this view, whether or not it has children to show.</summary>
	internal bool[] IsOpen { get; }

	/// <summary>Whether each row is open and shows children: <see cref="IsOpen"/> where there are any.</summary>
	internal bool[] IsExpanded { get; }

	/// <summary>Whether each displayed row is the last of the siblings displayed with it.</summary>
	internal bool[] IsLastSibling { get; }

	/// <summary>While filtered, whether a row has a descendant that matches the filter.</summary>
	internal bool[] HasMatchingDescendant { get; }

	/// <summary>Whether some displayed row shows an expander, so that rows without one leave its width blank.</summary>
	internal bool AnyExpandable { get; private set; }

	/// <summary>Each row's guide markup, filled the first time the row is painted.</summary>
	internal string?[] GuideMarkup { get; }

	/// <summary>Each row's expander markup, filled the first time the row is painted.</summary>
	internal string?[] ExpanderMarkup { get; }

	/// <summary>Each row's guide and expander together, as drawn in front of the value.</summary>
	internal string?[] PrefixMarkup { get; }

	/// <summary>Drops the markup built so far, after a setting that changes how rows are drawn.</summary>
	internal void ForgetMarkup()
	{
		Array.Clear(GuideMarkup);
		Array.Clear(ExpanderMarkup);
		Array.Clear(PrefixMarkup);
	}

	/// <summary>
	/// Computes what is displayed: the roots, then under each open row its children, in sibling order.
	/// </summary>
	/// <param name="shape">The hierarchy.</param>
	/// <param name="request">How rows open, in what order siblings show, and what the filter passes.</param>
	/// <remarks>
	/// <para>
	/// FILTERING A HIERARCHY. A matching row is shown together with every row above it, since a match
	/// out of context means little, and those rows are open in the filtered view whatever their own
	/// state, so the match is not hidden behind a collapsed parent. Rows inside collapsed parents are
	/// searched like any other. A match's children that do not match themselves stay hidden unless
	/// the request asks for whole subtrees under matches.
	/// </para>
	/// <para>
	/// In a filtered view a row is open as it was toggled during this filter, else open if a
	/// descendant matches, else as its own state says; the toggles belong to the filter, so clearing
	/// it brings the hierarchy back exactly as it was.
	/// </para>
	/// </remarks>
	internal static TreeTableProjection Compute(TreeTableShape shape, in TreeTableViewRequest request)
	{
		var view = new TreeTableProjection(shape);
		int count = shape.Count;
		var matches = request.Matches;
		bool[]? passes = null;

		if (matches != null)
		{
			// Depth-first order puts every descendant after its ancestor, so walking backwards sees
			// a row's whole subtree before the row, and walking forwards sees its ancestors first.
			var subtreeMatches = new bool[count];
			for (int row = count - 1; row >= 0; row--)
			{
				foreach (int child in shape.Children[row])
				{
					if (subtreeMatches[child])
						view.HasMatchingDescendant[row] = true;
				}
				subtreeMatches[row] = matches[row] || view.HasMatchingDescendant[row];
			}

			var underMatch = new bool[count];
			passes = new bool[count];
			for (int row = 0; row < count; row++)
			{
				int parent = shape.Parents[row];
				underMatch[row] = parent >= 0 && (matches[parent] || underMatch[parent]);
				passes[row] = subtreeMatches[row] || (request.ShowSubtreesOfMatches && underMatch[row]);
			}
		}

		Predicate<int>? isPassed = passes == null ? null : row => passes[row];

		for (int row = 0; row < count; row++)
		{
			if (isPassed == null)
			{
				view.HasChildren[row] = shape.Children[row].Length > 0
					|| shape.Rows[row] is TreeTableRow { HasUnrealizedChildren: true };
				view.IsOpen[row] = request.IsOpen(row);
			}
			else
			{
				view.HasChildren[row] = Array.Exists(shape.Children[row], isPassed);
				view.IsOpen[row] = request.ToggleInView?.Invoke(row)
					?? (view.HasMatchingDescendant[row] || request.IsOpen(row));
			}

			view.IsExpanded[row] = view.HasChildren[row] && view.IsOpen[row];
		}

		var displayRows = new List<int>(count);
		var siblingOrder = request.SiblingOrder;

		int[] Ordered(int[] siblings)
		{
			var shown = isPassed == null ? siblings : Array.FindAll(siblings, isPassed);
			if (siblingOrder == null || shown.Length < 2) return shown;

			var ordered = ReferenceEquals(shown, siblings) ? (int[])siblings.Clone() : shown;
			Array.Sort(ordered, siblingOrder);
			return ordered;
		}

		void Show(int[] unordered)
		{
			var siblings = Ordered(unordered);
			for (int i = 0; i < siblings.Length; i++)
			{
				int row = siblings[i];
				displayRows.Add(row);
				view.Displayed[row] = true;
				view.IsLastSibling[row] = i == siblings.Length - 1;
				if (view.HasChildren[row])
					view.AnyExpandable = true;
				if (view.IsExpanded[row])
					Show(shape.Children[row]);
			}
		}

		Show(shape.Roots);
		view.DisplayRows = displayRows.ToArray();
		return view;
	}

	/// <summary>
	/// The nearest ancestor of a row that is displayed, or -1 when none is.
	/// </summary>
	internal int FindDisplayedAncestor(int row)
	{
		for (int parent = Shape.Parents[row]; parent >= 0; parent = Shape.Parents[parent])
		{
			if (Displayed[parent])
				return parent;
		}
		return -1;
	}

	/// <summary>
	/// The row to select in <paramref name="after"/> once a change removed the selected row from the
	/// hierarchy <paramref name="before"/> displayed: its nearest next sibling still displayed, else
	/// its nearest previous one, else its nearest ancestor; -1 when there is none.
	/// </summary>
	/// <remarks>
	/// Siblings are taken in the order <paramref name="before"/> displayed them, so in a sorted view
	/// the cursor goes to the row that was shown below. When the row went together with some of its
	/// ancestors, the siblings are those of the topmost row removed.
	/// </remarks>
	internal static int FindStandInForRemoved(TreeTableProjection before, TreeTableProjection after, TableRow row)
	{
		var shape = before.Shape;
		int gone = shape.IndexOf(row);
		if (gone < 0) return -1;

		bool IsInAfter(int oldRow) => after.Shape.IndexOf(shape.Rows[oldRow]) >= 0;

		int DisplayedInAfter(int oldRow)
		{
			int index = after.Shape.IndexOf(shape.Rows[oldRow]);
			return index >= 0 && after.Displayed[index] ? index : -1;
		}

		while (shape.Parents[gone] >= 0 && !IsInAfter(shape.Parents[gone]))
			gone = shape.Parents[gone];

		// A removed selected row was displayed, and so were the rows it went with. Its displayed
		// siblings are the rows at its depth in the stretch of display rows its parent covers.
		int position = Array.IndexOf(before.DisplayRows, gone);
		int depth = shape.Depths[gone];
		for (int p = position + 1; position >= 0 && p < before.DisplayRows.Length; p++)
		{
			int sibling = before.DisplayRows[p];
			if (shape.Depths[sibling] < depth) break;

			int standIn = shape.Depths[sibling] == depth ? DisplayedInAfter(sibling) : -1;
			if (standIn >= 0) return standIn;
		}
		for (int p = position - 1; p >= 0; p--)
		{
			int sibling = before.DisplayRows[p];
			if (shape.Depths[sibling] < depth) break;

			int standIn = shape.Depths[sibling] == depth ? DisplayedInAfter(sibling) : -1;
			if (standIn >= 0) return standIn;
		}

		for (int ancestor = shape.Parents[gone]; ancestor >= 0; ancestor = shape.Parents[ancestor])
		{
			int standIn = DisplayedInAfter(ancestor);
			if (standIn >= 0) return standIn;
		}
		return -1;
	}
}

/// <summary>
/// What a <see cref="TreeTableProjection"/> is computed from, besides the hierarchy.
/// </summary>
/// <param name="IsOpen">A row's own, lasting expansion state.</param>
/// <param name="SiblingOrder">The order to show siblings in, ties already broken, or null for the order they were added in.</param>
/// <param name="Matches">Whether each row matches the filter, or null when nothing is filtered.</param>
/// <param name="ShowSubtreesOfMatches">Whether the rows under a match show although they do not match.</param>
/// <param name="ToggleInView">How a row was toggled during this filter, or null where it was not.</param>
internal readonly record struct TreeTableViewRequest(
	Func<int, bool> IsOpen,
	Comparison<int>? SiblingOrder,
	bool[]? Matches,
	bool ShowSubtreesOfMatches,
	Func<int, bool?>? ToggleInView);
