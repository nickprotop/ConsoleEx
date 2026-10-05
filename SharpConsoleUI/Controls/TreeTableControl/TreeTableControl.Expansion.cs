// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

public partial class TreeTableControl
{
	#region Expanding and Collapsing

	/// <summary>Opens a row, showing the rows nested under it.</summary>
	/// <param name="row">A row in this table.</param>
	/// <returns>True when the row was closed and is now open.</returns>
	public bool Expand(TreeTableRow row) => SetExpanded(row, true);

	/// <summary>Closes a row, hiding the rows nested under it.</summary>
	/// <param name="row">A row in this table.</param>
	/// <returns>True when the row was open and is now closed.</returns>
	public bool Collapse(TreeTableRow row) => SetExpanded(row, false);

	/// <summary>Opens a closed row, or closes an open one.</summary>
	/// <param name="row">A row in this table.</param>
	/// <returns>True when the row changed.</returns>
	public bool Toggle(TreeTableRow row) => SetExpanded(row, !IsRowExpanded(row));

	/// <summary>Opens a row and every row nested under it.</summary>
	/// <param name="row">A row in this table.</param>
	/// <returns>True when any of them changed.</returns>
	public bool ExpandSubtree(TreeTableRow row)
	{
		ThrowIfNotHere(row);
		var rows = new List<TreeTableRow>();
		lock (SyncRoot) { CollectSubtree(row, rows); }
		return SetExpanded(rows, true);
	}

	/// <summary>Opens every row.</summary>
	public void ExpandAll() => SetExpanded(CollectAll(), true);

	/// <summary>Closes every row.</summary>
	public void CollapseAll() => SetExpanded(CollectAll(), false);

	/// <summary>Whether a row is open in what is displayed.</summary>
	/// <param name="row">A row in this table.</param>
	/// <remarks>
	/// Without a filter, the row's own <see cref="TreeTableRow.IsExpanded"/>. While filtered, the
	/// filtered view's: as the row was opened or closed during this filter, else open if a row under
	/// it matches, else its own state. Expanding and collapsing while filtered changes only the
	/// filtered view, so clearing the filter brings the hierarchy back as it was.
	/// </remarks>
	public bool IsRowExpanded(TreeTableRow row)
	{
		ThrowIfNotHere(row);
		lock (SyncRoot) { return IsOpenInView(row); }
	}

	/// <summary>
	/// Makes a row displayed and brings it into view: opens every row it is nested under, and
	/// scrolls to it.
	/// </summary>
	/// <param name="row">A row in this table.</param>
	/// <returns>False when the row is not in this table.</returns>
	public bool EnsureRowVisible(TableRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		ThrowIfDataSource();
		if (!Contains(row)) return false;

		var ancestors = new List<TreeTableRow>();
		lock (SyncRoot)
		{
			for (var parent = GetParentRow(row); parent != null; parent = parent.Parent)
				ancestors.Add(parent);
		}
		SetExpanded(ancestors, true);

		int position = GetDisplayPosition(row);
		if (position < 0) return false;

		int visibleRows = Math.Max(1, GetVisibleRowCount());
		if (position < ScrollOffset)
			ScrollOffset = position;
		else if (position >= ScrollOffset + visibleRows)
			ScrollOffset = position - visibleRows + 1;
		return true;
	}

	/// <summary>
	/// Selects a row, opening the rows it is nested under and scrolling it into view.
	/// </summary>
	/// <param name="row">A row in this table.</param>
	/// <returns>False when the row is not in this table.</returns>
	public bool SelectRow(TableRow row)
	{
		if (!EnsureRowVisible(row)) return false;

		SelectedRowIndex = GetDisplayPosition(row);
		return true;
	}

	/// <summary>Sets whether a row is open in what is displayed: the one path every change takes.</summary>
	private bool SetExpanded(TreeTableRow row, bool isExpanded)
	{
		ThrowIfNotHere(row);
		return SetExpanded(new[] { row }, isExpanded);
	}

	/// <summary>Sets several rows' state at once, with one recompute.</summary>
	private bool SetExpanded(IReadOnlyList<TreeTableRow> rows, bool isExpanded)
	{
		ThrowIfDataSource();
		bool changed = false;
		lock (SyncRoot)
		{
			foreach (var row in rows)
			{
				if (IsOpenInView(row) == isExpanded) continue;

				if (_togglesFilter != null)
					_filterToggles[row] = isExpanded;
				else
					row.SetExpandedState(isExpanded);
				changed = true;
			}
		}

		if (changed)
			RefreshView();
		return changed;
	}

	/// <summary>
	/// Sets a row's lasting state, from <see cref="TreeTableRow.IsExpanded"/>. While filtered it shows
	/// wherever the row was not opened or closed during the filter.
	/// </summary>
	internal void SetPersistentExpansion(TreeTableRow row, bool isExpanded)
	{
		ThrowIfDataSource();
		lock (SyncRoot) { row.SetExpandedState(isExpanded); }
		RefreshView();
	}

	/// <summary>Whether a row is open in the view now displayed. Callers hold <see cref="TableControl.SyncRoot"/>.</summary>
	private bool IsOpenInView(TreeTableRow row)
	{
		if (_togglesFilter == null) return row.IsExpanded;
		if (_filterToggles.TryGetValue(row, out bool toggled)) return toggled;

		var view = _view;
		int dataIndex = view?.Shape.IndexOf(row) ?? -1;
		return (dataIndex >= 0 && view!.HasMatchingDescendant[dataIndex]) || row.IsExpanded;
	}

	/// <summary>The display position of a row in this table, or -1 when it is not displayed.</summary>
	private int GetDisplayPosition(TableRow row)
	{
		int dataIndex;
		lock (SyncRoot) { dataIndex = _shape.IndexOf(row); }
		return dataIndex < 0 ? -1 : GetDisplayRowIndex(dataIndex);
	}

	/// <summary>Every row that has children, or children not loaded yet.</summary>
	private List<TreeTableRow> CollectAll()
	{
		var rows = new List<TreeTableRow>();
		lock (SyncRoot)
		{
			foreach (var root in _roots)
			{
				if (root is TreeTableRow treeRoot)
					CollectSubtree(treeRoot, rows);
			}
		}
		return rows;
	}

	private static void CollectSubtree(TreeTableRow row, List<TreeTableRow> rows)
	{
		if (row.ChildList.Count > 0 || row.HasUnrealizedChildren)
			rows.Add(row);
		foreach (var child in row.ChildList)
			CollectSubtree(child, rows);
	}

	/// <summary>Throws unless a row is in this table.</summary>
	private void ThrowIfNotHere(TreeTableRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		if (row.Table != this)
			throw new InvalidOperationException("The row is not in this table.");
	}

	#endregion
}
