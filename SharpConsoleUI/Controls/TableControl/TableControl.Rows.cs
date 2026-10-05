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
		AppendRows(new[] { new TableRow(cells) });
	}

	/// <summary>
	/// Adds a row to the table.
	/// </summary>
	public void AddRow(TableRow row)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add rows when DataSource is set.");
		AppendRows(new[] { row });
	}

	/// <summary>
	/// Adds multiple rows to the table.
	/// </summary>
	public void AddRows(IEnumerable<TableRow> rows)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add rows when DataSource is set.");

		var rowList = rows.ToList();
		if (rowList.Count == 0) return;

		AppendRows(rowList);
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

		InsertRowsCore(index, new[] { row });
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

		InsertRowsCore(index, rowList);
	}

	/// <summary>
	/// Removes the row at the specified index.
	/// </summary>
	public void RemoveRow(int index)
	{
		lock (_tableLock)
		{
			if (index < 0 || index >= _rows.Count)
				return;
		}

		RemoveRowsCore(index, 1);
	}

	/// <summary>
	/// Clears all rows.
	/// </summary>
	public void ClearRows()
	{
		_selectedRowIndex = -1;
		_selectedColumnIndex = -1;
		_hoveredRowIndex = -1;
		_scrollOffset = 0;
		_horizontalScrollOffset = 0;
		_selectedRowIndices.Clear();
		_filterMode = FilterMode.None;
		_filterBuffer = string.Empty;
		_activeFilter = null;
		_rowView.Clear();
		SetDataCore(Array.Empty<TableRow>());
		SelectedRowChanged?.Invoke(this, -1);
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
		SetDataCore(rows as IReadOnlyList<TableRow> ?? rows.ToList());
	}

	#endregion

	#region Row Changes

	/// <summary>Appends rows after the last data row.</summary>
	private void AppendRows(IReadOnlyList<TableRow> rows)
	{
		int index;
		lock (_tableLock) { index = _rows.Count; }
		InsertRowsCore(index, rows);
	}

	/// <summary>
	/// Inserts rows at a data index, keeping the selection on the rows it was on.
	/// </summary>
	/// <param name="index">The data index to insert at, clamped to the data row count.</param>
	/// <param name="rows">The rows to insert, in order.</param>
	private void InsertRowsCore(int index, IReadOnlyList<TableRow> rows)
	{
		var selection = CaptureSelection();

		lock (_tableLock)
		{
			index = Math.Clamp(index, 0, _rows.Count);
			foreach (var row in rows) row.Owner = this;
			_rows.InsertRange(index, rows);
		}

		selection.ShiftForInsert(index, rows.Count);
		_rowView.Clear();
		RestoreSelection(selection);
		InvalidateAfterRowChange();
	}

	/// <summary>
	/// Removes a range of data rows, keeping the selection on the rows that remain.
	/// </summary>
	/// <param name="index">The first data index to remove; the range is already validated.</param>
	/// <param name="count">How many rows to remove.</param>
	private void RemoveRowsCore(int index, int count)
	{
		var selection = CaptureSelection();

		lock (_tableLock)
		{
			for (int i = index; i < index + count; i++)
				_rows[i].Owner = null;
			_rows.RemoveRange(index, count);
		}

		selection.ShiftForRemove(index, count);
		_rowView.Clear();
		RestoreSelection(selection);
		InvalidateAfterRowChange();
	}

	/// <summary>
	/// Replaces every row, keeping the selection on the rows the new set still contains.
	/// </summary>
	/// <param name="rows">The new rows, copied; the caller keeps its list.</param>
	private void SetDataCore(IReadOnlyList<TableRow> rows)
	{
		var selection = CaptureSelection();
		List<TableRow> oldRows;

		lock (_tableLock)
		{
			oldRows = _rows;
			foreach (var oldRow in oldRows) oldRow.Owner = null;
			_rows = new List<TableRow>(rows);
			foreach (var row in _rows) row.Owner = this;
		}

		selection.RemapByIdentity(oldRows, rows);
		// Drops a sort map but keeps a filter map, as SetData always has; both go stale here.
		if (!_rowView.IsFilterMapActive)
			_rowView.Clear();
		RestoreSelection(selection);
		InvalidateAfterRowChange();
	}

	/// <summary>The width and layout invalidation every change to the rows needs.</summary>
	private void InvalidateAfterRowChange()
	{
		InvalidateColumnWidths();
		_measurementCache.InvalidateCache();
		Invalidate(Invalidation.Relayout);
	}

	#endregion

	#region Selection Across Row Changes

	/// <summary>Stands for a selected row that a change removed from the data.</summary>
	private const int RemovedRow = -2;

	/// <summary>
	/// Where the selection was before the rows changed, by data row rather than display position,
	/// so it can be put back on the same rows wherever they are displayed afterwards.
	/// </summary>
	/// <remarks>
	/// Data indices are adjusted by the change itself — an insert shifts the indices after it, a
	/// removal marks the removed ones <see cref="RemovedRow"/> — rather than by looking the rows up
	/// again, which a table holding the same <see cref="TableRow"/> twice could not do. Only
	/// <see cref="RemapByIdentity"/> looks rows up, because replacing every row leaves no
	/// arithmetic to follow.
	/// </remarks>
	private struct RowSelection
	{
		/// <summary>The cursor's data row, -1 for none, or <see cref="RemovedRow"/>.</summary>
		internal int Cursor;

		/// <summary>The cursor's display position before the change.</summary>
		internal int CursorPosition;

		/// <summary>Whether the cursor was on screen before the change.</summary>
		internal bool CursorWasVisible;

		/// <summary>The range anchor's data row, -1 for none, or <see cref="RemovedRow"/>.</summary>
		internal int Anchor;

		/// <summary>The multi-selected data rows.</summary>
		internal int[] Selected;

		/// <summary>
		/// False in data-source mode, which has no rows to follow: its selection stays numeric.
		/// </summary>
		internal bool IsTracked;

		/// <summary>Follows the selected rows past <paramref name="count"/> rows inserted at <paramref name="index"/>.</summary>
		internal void ShiftForInsert(int index, int count)
		{
			int Shift(int dataIndex) => dataIndex >= index ? dataIndex + count : dataIndex;

			Remap(Shift);
		}

		/// <summary>Follows the selected rows past <paramref name="count"/> rows removed at <paramref name="index"/>.</summary>
		internal void ShiftForRemove(int index, int count)
		{
			int Shift(int dataIndex)
			{
				if (dataIndex < index) return dataIndex;
				return dataIndex < index + count ? RemovedRow : dataIndex - count;
			}

			Remap(Shift);
		}

		/// <summary>Finds the selected rows again after every row was replaced, by reference.</summary>
		/// <remarks>
		/// Reference identity, not <see cref="object.Equals(object)"/>: a <see cref="TableRow"/>
		/// subclass may define equality, and two equal rows are still two rows.
		/// </remarks>
		internal void RemapByIdentity(IReadOnlyList<TableRow> oldRows, IReadOnlyList<TableRow> newRows)
		{
			if (!IsTracked) return;

			var newIndex = new Dictionary<TableRow, int>(newRows.Count, ReferenceEqualityComparer.Instance);
			for (int i = 0; i < newRows.Count; i++)
				newIndex.TryAdd(newRows[i], i);

			int Find(int dataIndex)
			{
				if (dataIndex < 0) return dataIndex;
				return dataIndex < oldRows.Count && newIndex.TryGetValue(oldRows[dataIndex], out int found)
					? found
					: RemovedRow;
			}

			Remap(Find);
		}

		private void Remap(Func<int, int> map)
		{
			if (!IsTracked) return;

			if (Cursor >= 0) Cursor = map(Cursor);
			if (Anchor >= 0) Anchor = map(Anchor);
			for (int i = 0; i < Selected.Length; i++)
			{
				if (Selected[i] >= 0)
					Selected[i] = map(Selected[i]);
			}
		}
	}

	/// <summary>Records the selection by data row, before the rows change.</summary>
	private RowSelection CaptureSelection()
	{
		if (_dataSource != null)
			return new RowSelection { Selected = Array.Empty<int>() };

		int rowCount = RowCount;
		int DataRowAt(int position) => position >= 0 && position < rowCount ? MapDisplayToData(position) : -1;

		var selected = new List<int>(_selectedRowIndices.Count);
		foreach (int position in _selectedRowIndices)
		{
			if (position >= 0 && position < rowCount)
				selected.Add(MapDisplayToData(position));
		}

		return new RowSelection
		{
			Cursor = DataRowAt(_selectedRowIndex),
			CursorPosition = _selectedRowIndex,
			CursorWasVisible = _selectedRowIndex >= _scrollOffset && _selectedRowIndex < _scrollOffset + GetVisibleRowCount(),
			Anchor = DataRowAt(_selectionAnchorRowIndex),
			Selected = selected.ToArray(),
			IsTracked = true,
		};
	}

	/// <summary>
	/// Puts the selection back on its rows after they changed, and says so only where it changed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A cursor whose row is still displayed follows it to its new position. One whose row was
	/// removed lands on the row now at its old position, clamped to the rows there are, which is
	/// what removing the selected row has always done.
	/// </para>
	/// <para>
	/// EVENTS ONLY WHEN THE ROW CHANGES. A cursor that merely moves because rows were inserted or
	/// removed above it raises property-changed for <see cref="SelectedRowIndex"/>, so a binding
	/// stays right, but not <see cref="SelectedRowChanged"/> or <see cref="SelectedRowItemChanged"/>:
	/// the selected row is the same row. When the row does change, an edit in progress is cancelled
	/// first, so <see cref="CellEditCancelled"/> still names the cell that was being edited.
	/// </para>
	/// <para>
	/// The scroll offset is kept, clamped to the rows there are; a cursor that was on screen is
	/// brought back on screen, so rows arriving elsewhere neither scroll the view nor lose the
	/// cursor.
	/// </para>
	/// </remarks>
	private void RestoreSelection(RowSelection before)
	{
		if (!before.IsTracked) return;

		int rowCount = RowCount;
		int oldPosition = _selectedRowIndex;
		bool rowChanged = false;

		if (before.Cursor != -1)
		{
			int position = before.Cursor >= 0 ? MapDataToDisplay(before.Cursor) : -1;
			if (position < 0 || position >= rowCount)
			{
				position = rowCount == 0 ? -1 : Math.Clamp(before.CursorPosition, 0, rowCount - 1);
				rowChanged = true;
			}

			if (rowChanged && _isEditing)
				CancelEdit();
			_selectedRowIndex = position;
		}

		if (before.Selected.Length > 0 || _selectedRowIndices.Count > 0)
		{
			var restored = new HashSet<int>();
			foreach (int dataIndex in before.Selected)
			{
				int position = dataIndex >= 0 ? MapDataToDisplay(dataIndex) : -1;
				if (position >= 0 && position < rowCount)
					restored.Add(position);
			}

			bool countChanged = restored.Count != _selectedRowIndices.Count;
			_selectedRowIndices = restored;
			if (countChanged)
				Core.AsyncEvent.Raise(MultiSelectionChanged, MultiSelectionChangedAsync, this, _selectedRowIndices.Count, Container?.GetConsoleWindowSystem?.LogService);
		}

		if (before.Anchor != -1)
		{
			int anchor = before.Anchor >= 0 ? MapDataToDisplay(before.Anchor) : -1;
			_selectionAnchorRowIndex = anchor >= 0 && anchor < rowCount ? anchor : _selectedRowIndex;
		}

		_scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, rowCount - GetVisibleRowCount()));
		if (before.CursorWasVisible)
			EnsureSelectedRowVisible();

		if (rowChanged)
			RaiseSelectedRowChanged();
		else if (_selectedRowIndex != oldPosition)
			OnPropertyChanged(nameof(SelectedRowIndex));
	}

	#endregion
}
