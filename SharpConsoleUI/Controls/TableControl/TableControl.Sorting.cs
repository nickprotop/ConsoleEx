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
				SortSource();
			else
				ClearSourceSort();
		}
		else
		{
			// Under a client-side filter the matches are read in the source's order, so an order the
			// source still holds from before the filter goes first, then the map is rebuilt over it.
			if (_sortDirection == SortDirection.None)
				ClearSourceSort();
			RebuildDisplayMap();
		}

		RestoreSelection(selection);

		InvalidateColumnWidths();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Clears any active sort, restoring original order.
	/// </summary>
	/// <remarks>
	/// A data source that sorted itself is asked to restore its own order through
	/// <see cref="ITableDataSource.ClearSort"/>.
	/// </remarks>
	public void ClearSort()
	{
		var selection = CaptureSelection();

		_sortColumnIndex = -1;
		_sortDirection = SortDirection.None;
		ClearSourceSort();
		if (_dataSource == null || HasClientFilterMap)
			RebuildDisplayMap();

		RestoreSelection(selection);
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>Asks the data source to sort itself the way the table is sorted.</summary>
	private void SortSource()
	{
		_dataSource!.Sort(_sortColumnIndex, _sortDirection);
		_sourceSorted = true;
	}

	/// <summary>
	/// Asks the data source to drop the order it sorted itself into, if it did, so its rows do not
	/// keep a sort the header no longer shows.
	/// </summary>
	private void ClearSourceSort()
	{
		if (!_sourceSorted || _dataSource == null) return;

		_sourceSorted = false;
		_dataSource.ClearSort();
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

	#endregion

	#region Comparing Rows

	/// <summary>
	/// Compares two data rows by a column exactly as the table's own sort does.
	/// </summary>
	/// <param name="dataIndexA">The first data row.</param>
	/// <param name="dataIndexB">The second data row.</param>
	/// <param name="columnIndex">The column to compare by.</param>
	/// <param name="direction">
	/// The direction to apply; <see cref="SortDirection.Descending"/> negates the result, anything else
	/// leaves it ascending.
	/// </param>
	/// <returns>Negative, zero or positive, as <see cref="IComparer{T}.Compare"/>.</returns>
	/// <remarks>
	/// <para>
	/// THE TABLE'S RULES, NOT A COPY OF THEM. For in-memory rows: the column's
	/// <see cref="TableColumn.CustomRowComparer"/>, else its <see cref="TableColumn.CustomComparer"/>
	/// over the raw cell text, else ordinal ignore-case over the text the markup displays. For a data
	/// source: the displayed text of its cells. A derived table that orders rows its own way — siblings
	/// within a parent, say — calls this rather than re-implementing the rules, so a column's comparer
	/// means the same thing in both.
	/// </para>
	/// <para>
	/// No tie-break: rows that compare equal return zero, so a caller can apply its own order to them.
	/// <see cref="SortRowIndices"/> is the stable sort built on it. Takes <see cref="SyncRoot"/> for the
	/// duration of the comparison, so it is safe to call while the rows are being painted.
	/// </para>
	/// </remarks>
	protected int CompareRows(int dataIndexA, int dataIndexB, int columnIndex, SortDirection direction)
	{
		int result;
		if (_dataSource != null)
		{
			result = CompareSourceRows(dataIndexA, dataIndexB, columnIndex);
		}
		else
		{
			lock (_tableLock) { result = CompareOwnRows(dataIndexA, dataIndexB, columnIndex); }
		}

		return direction == SortDirection.Descending ? -result : result;
	}

	/// <summary>
	/// Sorts data row indices in place by a column, as the table's own sort orders them, keeping rows
	/// that compare equal in data order.
	/// </summary>
	/// <param name="dataIndices">The data rows to sort, in any order; sorted in place.</param>
	/// <param name="columnIndex">The column to sort by.</param>
	/// <param name="direction">The direction; see <see cref="CompareRows"/>.</param>
	/// <remarks>
	/// <para>
	/// A span, so a caller sorting several groups — the children of each parent in turn — can sort each
	/// slice of one buffer without copying it. The lock is taken once for the whole sort rather than
	/// once per comparison.
	/// </para>
	/// <para>
	/// STABLE, which <see cref="Array.Sort{T}(T[], Comparison{T})"/> on its own is not: past sixteen
	/// elements it stops being an insertion sort, and equal keys came out in whatever order its
	/// partitioning left them. Ties are broken on the data index after the direction is applied, so
	/// equal keys read in data order whichever way the column is sorted.
	/// </para>
	/// </remarks>
	protected void SortRowIndices(Span<int> dataIndices, int columnIndex, SortDirection direction)
	{
		if (_dataSource != null)
		{
			dataIndices.Sort(CreateRowComparison(columnIndex, direction));
			return;
		}

		lock (_tableLock)
		{
			dataIndices.Sort(CreateRowComparison(columnIndex, direction));
		}
	}

	/// <summary>
	/// The total order every sort uses: <see cref="CompareRows"/>'s rules, the direction applied, ties
	/// broken on the data index. For in-memory rows, callers hold <see cref="_tableLock"/> while it runs.
	/// </summary>
	/// <remarks>
	/// ONE COMPARISON, because there used to be two. Sorting a filtered table went through a second
	/// copy that knew nothing of <see cref="TableColumn.CustomRowComparer"/>, so the same header click
	/// ordered the same rows differently depending on whether a filter was active.
	/// </remarks>
	private Comparison<int> CreateRowComparison(int col, SortDirection direction) => (a, b) =>
	{
		int result = _dataSource != null ? CompareSourceRows(a, b, col) : CompareOwnRows(a, b, col);
		if (result == 0) return a.CompareTo(b);
		return direction == SortDirection.Descending ? -result : result;
	};

	/// <summary>Compares two in-memory rows by a column, ascending. Callers hold <see cref="_tableLock"/>.</summary>
	/// <remarks>
	/// THE DEFAULT COMPARES WHAT THE USER READS. Comparing the raw cell sorted <c>[red]Apple[/]</c> by
	/// the word "red", after "Banana". Filter matching and the data-source sort already strip markup;
	/// the default sort does too. Each row is stripped once and the text kept until the row or its
	/// cells change, rather than stripped per comparison. A <see cref="TableColumn.CustomComparer"/>
	/// still receives the raw text, as it always has, so any comparer written against markup keeps
	/// working.
	/// </remarks>
	private int CompareOwnRows(int a, int b, int col)
	{
		var column = col >= 0 && col < _columns.Count ? _columns[col] : null;
		if (column?.CustomRowComparer is { } rowComparer)
			return rowComparer(_rows[a], _rows[b]);
		if (column?.CustomComparer is { } comparer)
			return comparer.Compare(GetOwnCellText(a, col), GetOwnCellText(b, col));

		var displayedText = _rowView.GetSortTextCache(col, _rows.Count);
		string textA = displayedText[a] ??= MarkupParser.Remove(GetOwnCellText(a, col));
		string textB = displayedText[b] ??= MarkupParser.Remove(GetOwnCellText(b, col));
		return string.Compare(textA, textB, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>Compares two data-source rows by the text their cells display, ascending.</summary>
	private int CompareSourceRows(int a, int b, int col)
		=> string.Compare(MarkupParser.Remove(_dataSource!.GetCellValue(a, col)),
			MarkupParser.Remove(_dataSource.GetCellValue(b, col)), StringComparison.OrdinalIgnoreCase);

	/// <summary>An in-memory row's raw cell text, or empty past its last cell. Callers hold the lock.</summary>
	private string GetOwnCellText(int row, int col)
		=> col < _rows[row].Cells.Count ? _rows[row].Cells[col] : string.Empty;

	#endregion
}
