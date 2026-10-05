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
	#region Public Methods - Row Management

	/// <summary>
	/// Adds a row with the specified cells.
	/// </summary>
	public void AddRow(params string[] cells)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add rows when DataSource is set.");
		lock (_tableLock) { _rows.Add(new TableRow(cells) { Owner = this }); }
		_rowView.Clear();
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Adds a row to the table.
	/// </summary>
	public void AddRow(TableRow row)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add rows when DataSource is set.");
		lock (_tableLock) { row.Owner = this; _rows.Add(row); }
		_rowView.Clear();
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Adds multiple rows to the table.
	/// </summary>
	public void AddRows(IEnumerable<TableRow> rows)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add rows when DataSource is set.");
		lock (_tableLock)
		{
			foreach (var row in rows)
			{
				row.Owner = this;
				_rows.Add(row);
			}
		}
		_rowView.Clear();
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Inserts a row with the specified cells at the given index.
	/// Index is clamped to [0, RowCount].
	/// </summary>
	/// <param name="index">The zero-based index at which to insert.</param>
	/// <param name="cells">The cell values for the new row.</param>
	public void InsertRow(int index, params string[] cells)
	{
		InsertRow(index, new TableRow(cells));
	}

	/// <summary>
	/// Inserts a row at the given index.
	/// Index is clamped to [0, RowCount].
	/// </summary>
	/// <param name="index">The zero-based index at which to insert.</param>
	/// <param name="row">The row to insert.</param>
	public void InsertRow(int index, TableRow row)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot insert rows when DataSource is set.");

		lock (_tableLock)
		{
			index = Math.Clamp(index, 0, _rows.Count);
			row.Owner = this;
			_rows.Insert(index, row);
		}

		AdjustSelectionAfterInsert(index, 1);
		_rowView.Clear();
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Inserts multiple rows starting at the given index.
	/// Index is clamped to [0, RowCount].
	/// </summary>
	/// <param name="index">The zero-based index at which to begin inserting.</param>
	/// <param name="rows">The rows to insert.</param>
	public void InsertRows(int index, IEnumerable<TableRow> rows)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot insert rows when DataSource is set.");

		var rowList = rows.ToList();
		if (rowList.Count == 0) return;

		lock (_tableLock)
		{
			index = Math.Clamp(index, 0, _rows.Count);
			foreach (var row in rowList) row.Owner = this;
			_rows.InsertRange(index, rowList);
		}

		AdjustSelectionAfterInsert(index, rowList.Count);
		_rowView.Clear();
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Shifts selection indices forward after rows are inserted.
	/// </summary>
	private void AdjustSelectionAfterInsert(int insertIndex, int count)
	{
		if (_selectedRowIndex >= insertIndex)
		{
			_selectedRowIndex += count;
		}

		if (_selectedRowIndices.Count > 0)
		{
			var adjusted = new HashSet<int>();
			foreach (var idx in _selectedRowIndices)
			{
				adjusted.Add(idx >= insertIndex ? idx + count : idx);
			}
			_selectedRowIndices = adjusted;
		}
	}

	/// <summary>
	/// Removes the row at the specified index.
	/// </summary>
	public void RemoveRow(int index)
	{
		lock (_tableLock)
		{
			if (index >= 0 && index < _rows.Count)
			{
				_rows[index].Owner = null;
				_rows.RemoveAt(index);
			}
			else
				return;
		}

		// Adjust selection
		if (_selectedRowIndex >= 0)
		{
			int rowCount;
			lock (_tableLock) { rowCount = _rows.Count; }
			if (_selectedRowIndex == index)
			{
				_selectedRowIndex = rowCount > 0 ? Math.Min(_selectedRowIndex, rowCount - 1) : -1;
				SelectedRowChanged?.Invoke(this, _selectedRowIndex);
			}
			else if (_selectedRowIndex > index)
			{
				_selectedRowIndex--;
			}
		}

		_rowView.Clear();
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Clears all rows.
	/// </summary>
	public void ClearRows()
	{
		lock (_tableLock)
		{
			foreach (var row in _rows) row.Owner = null;
			_rows.Clear();
		}
		_selectedRowIndex = -1;
		_selectedColumnIndex = -1;
		_hoveredRowIndex = -1;
		_scrollOffset = 0;
		_horizontalScrollOffset = 0;
		_selectedRowIndices.Clear();
		_rowView.Clear();
		_filterMode = FilterMode.None;
		_filterBuffer = string.Empty;
		_activeFilter = null;
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		SelectedRowChanged?.Invoke(this, -1);
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Updates a cell value.
	/// </summary>
	public void UpdateCell(int row, int column, string value)
	{
		lock (_tableLock)
		{
			if (row >= 0 && row < _rows.Count && column >= 0 && column < _rows[row].Cells.Count)
				_rows[row].Cells[column] = value;
			else
				return;
		}
		InvalidateColumnWidths();
		_measurementCache.InvalidateCachedEntry(value);
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>
	/// Gets a cell value.
	/// </summary>
	public string GetCell(int row, int column)
	{
		if (_dataSource != null)
			return _dataSource.GetCellValue(row, column);

		lock (_tableLock)
		{
			if (row >= 0 && row < _rows.Count && column >= 0 && column < _rows[row].Cells.Count)
				return _rows[row].Cells[column];
			return string.Empty;
		}
	}

	/// <summary>
	/// Gets a row.
	/// </summary>
	public TableRow GetRow(int index)
	{
		lock (_tableLock)
		{
			if (index >= 0 && index < _rows.Count)
				return _rows[index];
			throw new ArgumentOutOfRangeException(nameof(index));
		}
	}

	/// <summary>
	/// Gets the caller's tag object for a row, or null when there is none.
	/// </summary>
	/// <remarks>
	/// Reads <see cref="ITableDataSource.GetRowTag"/> when a data source is attached and
	/// <see cref="TableRow.Tag"/> otherwise, so a row's identity can be recovered the same way in
	/// both modes. <see cref="GetRow"/> cannot serve the data-source case: it reads the
	/// <c>_rows</c> list, which is empty whenever a source is attached, and throws.
	/// </remarks>
	/// <param name="index">The row index. Out-of-range indices return null rather than throwing.</param>
	/// <returns>The row's tag, or null.</returns>
	public object? GetRowTagAt(int index)
	{
		if (index < 0) return null;

		if (_dataSource != null)
			return index < _dataSource.RowCount ? _dataSource.GetRowTag(index) : null;

		lock (_tableLock)
		{
			return index < _rows.Count ? _rows[index].Tag : null;
		}
	}

	/// <summary>
	/// Sets all rows at once.
	/// </summary>
	public void SetData(IEnumerable<TableRow> rows)
	{
		lock (_tableLock)
		{
			foreach (var oldRow in _rows) oldRow.Owner = null;
			_rows = new List<TableRow>(rows);
			foreach (var row in _rows) row.Owner = this;
		}
		// Drops a sort map but keeps a filter map, as SetData always has; both go stale here.
		if (!_rowView.IsFilterMapActive)
			_rowView.Clear();
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	#endregion
}
