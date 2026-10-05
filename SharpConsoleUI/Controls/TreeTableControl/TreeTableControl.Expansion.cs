// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;

namespace SharpConsoleUI.Controls;

public partial class TreeTableControl
{
	#region Fields

	// Expansions applied but not yet announced: RowExpansionChanged is raised once the outermost batch
	// has ended and what they show is displayed.
	private List<TreeTableRowExpansionEventArgs> _pendingExpansionChanges = new();

	#endregion

	#region Events

	/// <summary>
	/// Occurs before a row is expanded or collapsed, by any means: the methods, setting
	/// <see cref="TreeTableRow.IsExpanded"/>, a key or a click. Set
	/// <see cref="TreeTableRowExpansionChangingEventArgs.Cancel"/> to keep the row as it is.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The place to load children on demand: a row with <see cref="TreeTableRow.HasUnrealizedChildren"/>
	/// shows an expander before it has children, and children added from this handler are displayed
	/// together with the expansion, in the same recompute.
	/// </para>
	/// <para>
	/// Synchronous only, since the answer is needed before the change, and raised on the UI thread,
	/// never while <see cref="TableControl.SyncRoot"/> is held. A bulk change such as
	/// <see cref="ExpandAll"/> raises it for each row that would change, and a cancelled row is left
	/// out while the others change.
	/// </para>
	/// </remarks>
	public event EventHandler<TreeTableRowExpansionChangingEventArgs>? RowExpansionChanging;

	/// <summary>
	/// Occurs after a row was expanded or collapsed, by any means, once the rows displayed reflect it.
	/// </summary>
	/// <remarks>
	/// Raised after the selection events the change caused, and in a <see cref="BatchUpdate"/> when
	/// the outermost batch ends. A bulk change raises it once for each row that changed.
	/// </remarks>
	public event EventHandler<TreeTableRowExpansionEventArgs>? RowExpansionChanged;

	/// <summary>Async counterpart of <see cref="RowExpansionChanged"/>.</summary>
	public event Core.AsyncEventHandler<TreeTableRowExpansionEventArgs>? RowExpansionChangedAsync;

	#endregion

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

	/// <summary>Sets several rows' state in what is displayed, with one recompute.</summary>
	private bool SetExpanded(IReadOnlyList<TreeTableRow> rows, bool isExpanded)
		=> ChangeExpansion(rows, isExpanded, ownState: false);

	/// <summary>
	/// Sets a row's lasting state, from <see cref="TreeTableRow.IsExpanded"/>. While filtered it shows
	/// wherever the row was not opened or closed during the filter.
	/// </summary>
	internal void SetPersistentExpansion(TreeTableRow row, bool isExpanded)
		=> ChangeExpansion(new[] { row }, isExpanded, ownState: true);

	/// <summary>
	/// Expands or collapses rows, the one path every change takes: asks
	/// <see cref="RowExpansionChanging"/> for each row that would change, applies the changes not
	/// cancelled in one batch, and announces them through <see cref="RowExpansionChanged"/> once they
	/// are displayed.
	/// </summary>
	/// <param name="rows">The rows to change.</param>
	/// <param name="isExpanded">Whether they are to be open.</param>
	/// <param name="ownState">
	/// True to set the rows' own <see cref="TreeTableRow.IsExpanded"/>; false to set what is
	/// displayed, which while filtered is the filtered view only.
	/// </param>
	private bool ChangeExpansion(IReadOnlyList<TreeTableRow> rows, bool isExpanded, bool ownState)
	{
		ThrowIfDataSource();
		bool changed = false;

		bool IsAlready(TreeTableRow row) => ownState ? row.IsExpanded == isExpanded : IsOpenInView(row) == isExpanded;

		BatchUpdate(() =>
		{
			foreach (var row in rows)
			{
				bool isFilteredView;
				lock (SyncRoot)
				{
					if (row.Table != this || IsAlready(row)) continue;
					isFilteredView = !ownState && _togglesFilter != null;
				}

				var changing = new TreeTableRowExpansionChangingEventArgs(row, isExpanded, isFilteredView);
				RowExpansionChanging?.Invoke(this, changing);
				if (changing.Cancel) continue;

				lock (SyncRoot)
				{
					// The handler may have removed the row, or changed it itself.
					if (row.Table != this || IsAlready(row)) continue;

					if (isFilteredView)
						_filterToggles[row] = isExpanded;
					else
						row.SetExpandedState(isExpanded);
				}

				_pendingExpansionChanges.Add(new TreeTableRowExpansionEventArgs(row, isExpanded, isFilteredView));
				changed = true;
			}

			if (changed)
				RefreshView();
		});

		return changed;
	}

	/// <summary>
	/// Raises <see cref="RowExpansionChanged"/> for the expansions applied, once the outermost batch
	/// has ended and they are displayed.
	/// </summary>
	private void RaisePendingExpansionChanges()
	{
		if (_pendingExpansionChanges.Count == 0) return;

		var changes = _pendingExpansionChanges;
		_pendingExpansionChanges = new List<TreeTableRowExpansionEventArgs>();
		foreach (var change in changes)
			Core.AsyncEvent.Raise(RowExpansionChanged, RowExpansionChangedAsync, this, change, Container?.GetConsoleWindowSystem?.LogService);
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
