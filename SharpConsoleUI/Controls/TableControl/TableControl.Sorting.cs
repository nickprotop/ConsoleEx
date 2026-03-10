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
			Container?.Invalidate(true);
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
			_sortIndexMap = null;
			// If filter is active, recompute without sort
			if (_filterIndexMap != null && _activeFilter != null)
				RecomputeDisplayMap();
		}
		else
		{
			// If filter is active, recompute combined map
			if (_filterIndexMap != null && _activeFilter != null)
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
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Clears any active sort, restoring original order.
	/// </summary>
	public void ClearSort()
	{
		_sortColumnIndex = -1;
		_sortDirection = SortDirection.None;
		_sortIndexMap = null;
		// If filter is active, recompute without sort
		if (_filterIndexMap != null && _activeFilter != null)
			RecomputeDisplayMap();
		Container?.Invalidate(true);
	}

	private void ApplySort()
	{
		if (_dataSource != null)
		{
			// Delegate sorting to the data source
			_dataSource.Sort(_sortColumnIndex, _sortDirection);
			_sortIndexMap = null;
			return;
		}

		lock (_tableLock)
		{
			int rowCount = _rows.Count;
			if (rowCount == 0)
			{
				_sortIndexMap = null;
				return;
			}

			// Build index map
			var indices = Enumerable.Range(0, rowCount).ToArray();

			// Get custom comparer if available
			IComparer<string>? customComparer = null;
			if (_sortColumnIndex >= 0 && _sortColumnIndex < _columns.Count)
				customComparer = _columns[_sortColumnIndex].CustomComparer;

			int col = _sortColumnIndex;
			Array.Sort(indices, (a, b) =>
			{
				string valA = col < _rows[a].Cells.Count ? _rows[a].Cells[col] : string.Empty;
				string valB = col < _rows[b].Cells.Count ? _rows[b].Cells[col] : string.Empty;

				int result;
				if (customComparer != null)
					result = customComparer.Compare(valA, valB);
				else
					result = string.Compare(valA, valB, StringComparison.OrdinalIgnoreCase);

				return _sortDirection == SortDirection.Descending ? -result : result;
			});

			_sortIndexMap = indices;
		}
	}

	#endregion
}
