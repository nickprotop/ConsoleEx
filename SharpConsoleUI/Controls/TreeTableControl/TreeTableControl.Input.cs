// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

public partial class TreeTableControl
{
	#region Keys

	/// <summary>
	/// Opens and closes rows from the keyboard, as <see cref="TreeControl"/> does: Right opens a row,
	/// or moves to its first child once it is open; Left closes it, or moves to its parent once it is
	/// closed; Space toggles it; <c>+</c> opens it, <c>-</c> closes it and <c>*</c> opens it and every
	/// row under it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The table keeps every key it already gives a meaning to. With
	/// <see cref="TableControl.CellNavigationEnabled"/> on, Left and Right move between cells; with
	/// <see cref="TableControl.MultiSelectEnabled"/> on, Space selects rows. Keys held with Ctrl or
	/// Alt are left alone, so an application's shortcuts keep working, and a key that means nothing
	/// for the selected row — Space on a row without children, say — goes on to the table.
	/// </para>
	/// <para>Override to add keys of your own, and call the base for these.</para>
	/// </remarks>
	protected override bool TryHandleKey(ConsoleKeyInfo key)
	{
		if (base.TryHandleKey(key)) return true;
		if (DataSource != null || (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0)
			return false;

		var view = _view;
		int position = SelectedRowIndex;
		int dataIndex = position >= 0 ? GetDataRowIndex(position) : -1;
		if (view == null || dataIndex < 0 || dataIndex >= view.Shape.Count || view.Shape.Rows[dataIndex] is not TreeTableRow row)
			return false;

		bool expandable = view.HasChildren[dataIndex];
		bool open = view.IsOpen[dataIndex];

		switch (key.Key)
		{
			case ConsoleKey.RightArrow when !CellNavigationEnabled:
				if (!expandable) return false;
				if (!open)
				{
					Expand(row);
					return true;
				}

				// Depth-first, so a row's first displayed child is the row displayed right after it.
				int next = position + 1 < RowCount ? GetDataRowIndex(position + 1) : -1;
				if (next >= 0 && view.Shape.Parents[next] == dataIndex)
					SelectedRowIndex = position + 1;
				return true;

			case ConsoleKey.LeftArrow when !CellNavigationEnabled:
				if (expandable && open)
				{
					Collapse(row);
					return true;
				}

				int parent = view.Shape.Parents[dataIndex];
				if (parent < 0) return false;
				SelectedRowIndex = GetDisplayRowIndex(parent);
				return true;

			case ConsoleKey.Spacebar when !MultiSelectEnabled:
				if (!expandable) return false;
				Toggle(row);
				return true;
		}

		if (!expandable) return false;
		if (key.Key == ConsoleKey.Add || key.KeyChar == '+')
			Expand(row);
		else if (key.Key == ConsoleKey.Subtract || key.KeyChar == '-')
			Collapse(row);
		else if (key.Key == ConsoleKey.Multiply || key.KeyChar == '*')
			ExpandSubtree(row);
		else
			return false;
		return true;
	}

	#endregion

	#region Clicks

	/// <summary>
	/// Toggles a row when its expander is clicked. Every other click is the table's: selecting,
	/// sorting by a header, double-clicking to activate.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The expander's span is measured from what <see cref="GetGuideMarkup"/> and
	/// <see cref="GetExpanderMarkup"/> return for the row, so an override that draws them wider or
	/// narrower keeps working with the mouse; it counts from the tree column's left edge as laid out,
	/// so horizontal scrolling and a checkbox column do not move it.
	/// </para>
	/// <para>
	/// The click is taken: two quick clicks on an expander toggle the row twice rather than
	/// activating it. Override to take clicks of your own, and call the base for this one.
	/// </para>
	/// </remarks>
	protected override bool TryHandleClick(TableHitTestResult hit, int clickCount, MouseEventArgs args)
	{
		if (base.TryHandleClick(hit, clickCount, args)) return true;
		if (DataSource != null || hit.Zone != TableHitZone.Cell || hit.ColumnIndex != _treeColumnIndex)
			return false;

		var view = _view;
		int dataIndex = hit.DataRowIndex;
		if (view == null || dataIndex < 0 || dataIndex >= view.Shape.Count || !view.HasChildren[dataIndex]
			|| view.Shape.Rows[dataIndex] is not TreeTableRow row)
			return false;

		// Built when the row was painted; built here if it was not.
		view.PrefixMarkup[dataIndex] ??= BuildPrefixMarkup(view, dataIndex);
		int expanderStart = MarkupParser.StripLength(view.GuideMarkup[dataIndex] ?? string.Empty);
		int expanderEnd = expanderStart + MarkupParser.StripLength(view.ExpanderMarkup[dataIndex] ?? string.Empty);
		if (hit.CellOffset < expanderStart || hit.CellOffset >= expanderEnd)
			return false;

		SelectedRowIndex = hit.DisplayRowIndex;
		Toggle(row);
		return true;
	}

	#endregion
}
