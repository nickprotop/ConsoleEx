// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

public partial class TableControl
{
	#region Sorting Properties

	/// <summary>
	/// Gets or sets whether sorting is enabled. When enabled, clicking a header column
	/// cycles through Ascending → Descending → None.
	/// </summary>
	public bool SortingEnabled
	{
		get => _sortingEnabled;
		set
		{
			_sortingEnabled = value;
			OnPropertyChanged();
			if (!value) ClearSort();
			Invalidate(Invalidation.Relayout);
		}
	}

	/// <summary>
	/// Gets the current sort column index, or -1 if no sort is applied.
	/// </summary>
	public int SortColumnIndex => _sortColumnIndex;

	/// <summary>
	/// Gets the current sort direction.
	/// </summary>
	public SortDirection CurrentSortDirection => _sortDirection;

	#endregion

	#region Sorting Methods

	/// <summary>
	/// Sorts the table by the specified column. Cycles: None → Ascending → Descending → None.
	/// </summary>
	public void SortByColumn(int columnIndex)
	{
		if (!_sortingEnabled) return;

		// Check if the column is sortable
		if (_dataSource != null)
		{
			if (!_dataSource.CanSort(columnIndex)) return;
		}
		else
		{
			lock (_tableLock)
			{
				if (columnIndex < 0 || columnIndex >= _columns.Count) return;
				if (!_columns[columnIndex].IsSortable) return;
			}
		}

		// The selection is kept by row, so it follows its rows to wherever the sort puts them.
		var selection = CaptureSelection();

		// Cycle sort direction
		if (_sortColumnIndex == columnIndex)
		{
			_sortDirection = _sortDirection switch
			{
				SortDirection.Ascending => SortDirection.Descending,
				SortDirection.Descending => SortDirection.None,
				_ => SortDirection.Ascending
			};
		}
		else
		{
			_sortColumnIndex = columnIndex;
			_sortDirection = SortDirection.Ascending;
		}

		if (_sortDirection == SortDirection.None)
			_sortColumnIndex = -1;

		// A data source sorts itself unless the table is filtering it client-side; there it owns the
		// map and sorts the matches the same way it sorts its own rows.
		if (_dataSource != null && !HasClientFilterMap)
		{
			if (_sortDirection != SortDirection.None)
				_dataSource.Sort(_sortColumnIndex, _sortDirection);
		}
		else
		{
			RebuildDisplayMap();
		}

		RestoreSelection(selection);

		InvalidateColumnWidths();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Clears any active sort, restoring original order.
	/// </summary>
	public void ClearSort()
	{
		_sortColumnIndex = -1;
		_sortDirection = SortDirection.None;
		if (_dataSource == null || HasClientFilterMap)
			RebuildDisplayMap();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Recomputes the display map from the table's whole state, filter and sort together, except
	/// where the data source owns the answer.
	/// </summary>
	/// <remarks>
	/// THE ONE WAY THE MAP IS REBUILT, replacing a sort routine and a filter routine that each
	/// rebuilt their own map and left the other stale. A data source's answer to a filter is never
	/// recomputed from a client-side scan, and without a client-side filter a source is read by
	/// identity, having sorted itself through <see cref="ITableDataSource.Sort"/>.
	/// </remarks>
	private void RebuildDisplayMap()
	{
		if (_dataSource != null)
		{
			if (_rowView.FromSource) return;
			if (_activeFilter == null)
			{
				_rowView.Clear();
				return;
			}
		}

		RecomputeDisplayMap();
	}

	/// <summary>
	/// The comparison every sort of in-memory rows uses: the column's
	/// <see cref="TableColumn.CustomRowComparer"/>, else its <see cref="TableColumn.CustomComparer"/>
	/// over the raw cell text, else ordinal ignore-case over the text the markup displays, with the
	/// direction applied. Rows that compare equal keep their data order. Callers hold
	/// <see cref="_tableLock"/> while it runs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// ONE COMPARISON, because there used to be two. Sorting a filtered table went through a
	/// second copy that knew nothing of <see cref="TableColumn.CustomRowComparer"/>, so the same
	/// header click ordered the same rows differently depending on whether a filter was active.
	/// </para>
	/// <para>
	/// The data-index tie-break makes the sort stable, which <see cref="Array.Sort{T}(T[], Comparison{T})"/>
	/// is not: past sixteen rows it stops being an insertion sort, and rows with equal keys came out
	/// in whatever order its partitioning left them. The tie-break is applied after the direction,
	/// so equal keys read in data order whichever way the column is sorted.
	/// </para>
	/// <para>
	/// THE DEFAULT COMPARES WHAT THE USER READS. Comparing the raw cell sorted <c>[red]Apple[/]</c>
	/// by the word "red", after "Banana". Filter matching and the data-source sort already strip
	/// markup; the default sort now does too. Each row is stripped once and the text kept until the
	/// row or its cells change, rather than stripped per comparison. A
	/// <see cref="TableColumn.CustomComparer"/> still receives the raw text, as it always has, so any
	/// comparer written against markup keeps working.
	/// </para>
	/// </remarks>
	private Comparison<int> CreateRowComparison(int col, SortDirection direction)
	{
		Comparison<TableRow>? customRowComparer = null;
		IComparer<string>? customComparer = null;
		if (col >= 0 && col < _columns.Count)
		{
			customRowComparer = _columns[col].CustomRowComparer;
			customComparer = _columns[col].CustomComparer;
		}

		List<string?>? displayedText = customRowComparer == null && customComparer == null
			? _rowView.GetSortTextCache(col, _rows.Count)
			: null;

		string RawCell(int row) => col < _rows[row].Cells.Count ? _rows[row].Cells[col] : string.Empty;

		string DisplayedCell(int row) => displayedText![row] ??= MarkupParser.Remove(RawCell(row));

		return (a, b) =>
		{
			int result;
			if (customRowComparer != null)
				result = customRowComparer(_rows[a], _rows[b]);
			else if (customComparer != null)
				result = customComparer.Compare(RawCell(a), RawCell(b));
			else
				result = string.Compare(DisplayedCell(a), DisplayedCell(b), StringComparison.OrdinalIgnoreCase);

			if (result == 0) return a.CompareTo(b);
			return direction == SortDirection.Descending ? -result : result;
		};
	}

	#endregion
}
