// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

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

		// Track the currently selected row's data index to preserve selection
		int selectedDataIndex = _selectedRowIndex >= 0 ? MapDisplayToData(_selectedRowIndex) : -1;
		object? selectedTag = null;
		if (selectedDataIndex >= 0 && _dataSource == null)
		{
			lock (_tableLock)
			{
				if (selectedDataIndex < _rows.Count)
					selectedTag = _rows[selectedDataIndex].Tag;
			}
		}

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
		{
			_sortColumnIndex = -1;
			_rowView.SetSortMap(null);
			// If filter is active, recompute without sort
			if (HasClientFilterMap)
				RecomputeDisplayMap();
		}
		else
		{
			// If filter is active, recompute combined map
			if (HasClientFilterMap)
				RecomputeDisplayMap();
			else
				ApplySort();
		}

		// Restore selection by tag
		if (selectedTag != null && _dataSource == null)
		{
			lock (_tableLock)
			{
				for (int i = 0; i < _rows.Count; i++)
				{
					if (ReferenceEquals(_rows[i].Tag, selectedTag))
					{
						_selectedRowIndex = MapDataToDisplay(i);
						break;
					}
				}
			}
		}

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
		_rowView.SetSortMap(null);
		// If filter is active, recompute without sort
		if (HasClientFilterMap)
			RecomputeDisplayMap();
		Invalidate(Invalidation.Relayout);
	}

	private void ApplySort()
	{
		if (_dataSource != null)
		{
			// Delegate sorting to the data source
			_dataSource.Sort(_sortColumnIndex, _sortDirection);
			_rowView.SetSortMap(null);
			return;
		}

		lock (_tableLock)
		{
			int rowCount = _rows.Count;
			if (rowCount == 0)
			{
				_rowView.SetSortMap(null);
				return;
			}

			// Build index map
			var indices = Enumerable.Range(0, rowCount).ToArray();
			Array.Sort(indices, CreateRowComparison(_sortColumnIndex, _sortDirection));

			_rowView.SetSortMap(indices);
		}
	}

	/// <summary>
	/// The comparison every sort of in-memory rows uses: the column's
	/// <see cref="TableColumn.CustomRowComparer"/>, else its <see cref="TableColumn.CustomComparer"/>
	/// over the raw cell text, else ordinal ignore-case, with the direction applied. Callers hold
	/// <see cref="_tableLock"/> while it runs.
	/// </summary>
	/// <remarks>
	/// ONE COMPARISON, because there used to be two. Sorting a filtered table went through a
	/// second copy that knew nothing of <see cref="TableColumn.CustomRowComparer"/>, so the same
	/// header click ordered the same rows differently depending on whether a filter was active.
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

		return (a, b) =>
		{
			int result;
			if (customRowComparer != null)
				result = customRowComparer(_rows[a], _rows[b]);
			else
			{
				string valA = col < _rows[a].Cells.Count ? _rows[a].Cells[col] : string.Empty;
				string valB = col < _rows[b].Cells.Count ? _rows[b].Cells[col] : string.Empty;
				result = customComparer != null
					? customComparer.Compare(valA, valB)
					: string.Compare(valA, valB, StringComparison.OrdinalIgnoreCase);
			}

			return direction == SortDirection.Descending ? -result : result;
		};
	}

	#endregion
}
