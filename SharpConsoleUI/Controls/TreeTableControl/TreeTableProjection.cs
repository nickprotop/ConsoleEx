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
		IsExpanded = new bool[count];
		IsLastSibling = new bool[count];
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

	/// <summary>Whether each row shows an expander: it has children to show, or children not loaded yet.</summary>
	internal bool[] HasChildren { get; }

	/// <summary>Whether each row is open in this view.</summary>
	internal bool[] IsExpanded { get; }

	/// <summary>Whether each displayed row is the last of the siblings displayed with it.</summary>
	internal bool[] IsLastSibling { get; }

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
	/// <param name="isOpen">Whether a row that has children shows them.</param>
	internal static TreeTableProjection Compute(TreeTableShape shape, Func<int, bool> isOpen)
	{
		var view = new TreeTableProjection(shape);

		for (int row = 0; row < shape.Count; row++)
		{
			view.HasChildren[row] = shape.Children[row].Length > 0
				|| shape.Rows[row] is TreeTableRow { HasUnrealizedChildren: true };
			view.IsExpanded[row] = view.HasChildren[row] && isOpen(row);
		}

		var displayRows = new List<int>(shape.Count);

		void Show(int[] siblings)
		{
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
}
