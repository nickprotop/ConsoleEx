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
		ThrowIfComputingDisplayRows();
		AppendRows(new[] { CreateRow(cells) });
	}

	/// <summary>
	/// Adds a row to the table.
	/// </summary>
	public void AddRow(TableRow row)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot add rows when DataSource is set.");
		ThrowIfComputingDisplayRows();
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

		ThrowIfComputingDisplayRows();
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
		InsertRow(index, CreateRow(cells));
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

		ThrowIfComputingDisplayRows();
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

		ThrowIfComputingDisplayRows();
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

		ThrowIfComputingDisplayRows();
		RemoveRowsCore(index, 1);
	}

	/// <summary>
	/// Clears all rows.
	/// </summary>
	public void ClearRows()
	{
		ThrowIfComputingDisplayRows();

		_selectedRowIndex = -1;
		_selectedColumnIndex = -1;
		_hoveredRowIndex = -1;
		_scrollOffset = 0;
		_horizontalScrollOffset = 0;
		_selectedRowIndices.Clear();
		_filterMode = FilterMode.None;
		_filterBuffer = string.Empty;
		_activeFilter = null;
		SetDataCore(Array.Empty<TableRow>());
		SelectedRowChanged?.Invoke(this, -1);
	}


	/// <summary>
	/// Updates a cell value.
	/// </summary>
	public void UpdateCell(int row, int column, string value)
	{
		TableRow target;
		lock (_tableLock)
		{
			if (row >= 0 && row < _rows.Count && column >= 0 && column < _rows[row].Cells.Count)
				target = _rows[row];
			else
				return;
		}

		// Outside the lock: the change notifies OnRowContentChanged, which is never called with it held.
		target.Cells[column] = value;
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
		ThrowIfComputingDisplayRows();
		SetDataCore(rows as IReadOnlyList<TableRow> ?? rows.ToList());
	}

	#endregion

	#region Rows For Derived Tables

	/// <summary>
	/// The lock that guards the table's rows and columns.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The table paints on the render thread from a snapshot taken under this lock, and every change
	/// to its rows takes it. A derived table keeping its own structure alongside the rows — a parent
	/// for each row, say — guards that structure with the same lock, so a paint never sees the rows
	/// and the structure out of step. A second lock of its own would invite the two to be taken in
	/// opposite orders.
	/// </para>
	/// <para>
	/// Hold it briefly. The table's hooks are never called with it held, and the public members that
	/// raise events should not be called while holding it either, or a handler running on another
	/// thread could deadlock against a paint.
	/// </para>
	/// </remarks>
	protected object SyncRoot => _tableLock;

	/// <summary>
	/// The number of data rows: every in-memory row, or the data source's row count, whatever is
	/// displayed.
	/// </summary>
	/// <remarks>
	/// <see cref="RowCount"/> counts DISPLAYED rows. The two differ whenever a filter is active or a
	/// derived table hides rows, and data indices — <see cref="GetRow"/>, the cell accessors, the
	/// display map — run from zero to this count, not to <see cref="RowCount"/>.
	/// </remarks>
	protected int DataRowCount
	{
		get
		{
			if (_dataSource != null) return _dataSource.RowCount;
			lock (_tableLock) { return _rows.Count; }
		}
	}

	/// <summary>
	/// The data row displayed at a display position, or -1 when there is no such position.
	/// </summary>
	/// <param name="displayIndex">A display position, as <see cref="SelectedRowIndex"/> counts them.</param>
	/// <remarks>
	/// Unlike the table's internal mapping, a position past the displayed rows is -1 rather than
	/// being taken for a data index: that fallback is how the rows a filter excluded were once
	/// painted underneath its matches.
	/// </remarks>
	protected int GetDataRowIndex(int displayIndex)
		=> displayIndex >= 0 && displayIndex < RowCount ? MapDisplayToData(displayIndex) : -1;

	/// <summary>
	/// The display position of a data row, or -1 when it is not displayed — filtered out, hidden by a
	/// derived table, or not a row at all.
	/// </summary>
	/// <param name="dataIndex">A data row, as <see cref="GetRow"/> counts them.</param>
	/// <remarks>O(1): answered from an inverse of the display map, built once per map.</remarks>
	protected int GetDisplayRowIndex(int dataIndex)
	{
		if (dataIndex < 0 || dataIndex >= DataRowCount) return -1;

		int position = MapDataToDisplay(dataIndex);
		return position < RowCount ? position : -1;
	}

	/// <summary>
	/// Recomputes which rows are displayed, keeping the selection on its rows, after something only
	/// the derived table knows about changed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The table recomputes its display rows itself after every change it can see: rows added,
	/// removed or replaced, a sort, a filter. A derived table whose <see cref="ComputeDisplayRows"/>
	/// depends on state of its own — whether a parent is expanded, say — calls this when that state
	/// changes, and gets the same treatment as a sort: the cursor, the multi-selection and the range
	/// anchor follow their rows, a cursor whose row is no longer displayed goes to the row
	/// <see cref="ResolveHiddenSelectedRow"/> names, a cursor that was on screen stays on screen, and
	/// the column widths and layout are refreshed.
	/// </para>
	/// <para>
	/// Calling it from <see cref="ComputeDisplayRows"/> or <see cref="ResolveHiddenSelectedRow"/>
	/// throws <see cref="InvalidOperationException"/>: it would recompute the rows while they are
	/// being computed.
	/// </para>
	/// </remarks>
	protected void RefreshDisplayRows()
	{
		ThrowIfComputingDisplayRows();

		var selection = CaptureSelection();
		RebuildDisplayMap();
		RestoreSelection(selection);
		InvalidateAfterRowChange();
	}

	/// <summary>
	/// Names the row the cursor should move to when its own row is still in the table but no longer
	/// displayed.
	/// </summary>
	/// <param name="dataIndex">The data row the cursor was on, now hidden.</param>
	/// <returns>
	/// A displayed data row to select instead, or -1 to let the table choose: the row now at the
	/// cursor's old position.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The table only ever hides a selected row through a filter, and a filter starts again from its
	/// first match, so the default has nothing better to offer and returns -1. A derived table that
	/// hides rows itself knows a better answer: collapsing a parent hides its selected child, and the
	/// parent is where the cursor belongs.
	/// </para>
	/// <para>
	/// Called on the UI thread, never while <see cref="SyncRoot"/> is held, and only for the table's
	/// own rows. The selection events follow, since the selected row changes. An answer that is not
	/// displayed is ignored. Changing the rows from here throws <see cref="InvalidOperationException"/>.
	/// </para>
	/// </remarks>
	protected virtual int ResolveHiddenSelectedRow(int dataIndex) => -1;

	/// <summary>
	/// Names the row the cursor should move to when its own row was removed from the table.
	/// </summary>
	/// <param name="row">The row the cursor was on, no longer in the table.</param>
	/// <returns>
	/// A displayed data row to select instead, counted in the rows as they are now, or -1 to let the
	/// table choose: the row now at the cursor's old position.
	/// </returns>
	/// <remarks>
	/// <para>
	/// In a flat list the row that takes the removed row's place is the natural next stop, so the
	/// default returns -1 and keeps what removing the selected row has always done. A derived table
	/// whose rows have a structure knows better: in a tree, the row now below a removed subtree can
	/// belong to another parent, and the next sibling, or the parent, is where the cursor belongs.
	/// </para>
	/// <para>
	/// Called after the rows changed and the display rows were recomputed, on the UI thread, never
	/// while <see cref="SyncRoot"/> is held, and only when the cursor's own row was removed. The
	/// selection events follow, since the selected row changes. An answer that is not displayed is
	/// ignored. Changing the rows from here throws <see cref="InvalidOperationException"/>.
	/// </para>
	/// </remarks>
	protected virtual int ResolveRemovedSelectedRow(TableRow row) => -1;

	/// <summary>
	/// Asks <see cref="ResolveHiddenSelectedRow"/> or <see cref="ResolveRemovedSelectedRow"/> for the
	/// row standing in for the cursor's, with the rows locked against change.
	/// </summary>
	private int ResolveStandInRow(in RowSelection before)
	{
		_displayRowsHookDepth++;
		try
		{
			if (before.Cursor >= 0)
				return ResolveHiddenSelectedRow(before.Cursor);
			return before.CursorRow != null ? ResolveRemovedSelectedRow(before.CursorRow) : -1;
		}
		finally
		{
			_displayRowsHookDepth--;
		}
	}

	/// <summary>
	/// Refuses to change the rows, or recompute which are displayed, while the display rows are being
	/// computed: a hook doing so would recurse into itself.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Called from <see cref="ComputeDisplayRows"/>, <see cref="ResolveHiddenSelectedRow"/> or
	/// <see cref="ResolveRemovedSelectedRow"/>, or from anything they call.
	/// </exception>
	/// <remarks>
	/// The table checks this itself before it changes its rows. A derived table that keeps a structure
	/// of its own beside the rows calls it before changing that structure, so a hook that tries to is
	/// refused before anything has changed, rather than after the structure and the rows have parted.
	/// </remarks>
	protected void ThrowIfComputingDisplayRows()
	{
		if (_displayRowsHookDepth > 0)
			throw new InvalidOperationException(
				"The rows cannot be changed, or the displayed rows recomputed, from ComputeDisplayRows, ResolveHiddenSelectedRow or ResolveRemovedSelectedRow.");
	}

	#endregion

	#region Row Changes

	// EVERY CHANGE TO THE ROWS GOES THROUGH THREE METHODS. Each public mutator — the AddRow, AddRows,
	// InsertRow and InsertRows overloads, RemoveRow, ClearRows and SetData — validates its arguments
	// and calls InsertRowsCore, RemoveRowsCore or SetDataCore, the way Collection<T>.InsertItem and
	// RemoveItem work. A derived table that keeps a structure of its own alongside the rows overrides
	// these to keep it in step, and calls the base to make the change, whichever public member was
	// used and also from its own API.

	/// <summary>
	/// Creates the row for the overloads that take cell text: <see cref="AddRow(string[])"/> and
	/// <see cref="InsertRow(int, string[])"/>.
	/// </summary>
	/// <param name="cells">The row's cell values.</param>
	/// <returns>A new, unattached row.</returns>
	/// <remarks>
	/// The one way a derived table's rows created from text get its own row type, so that a table
	/// whose rows carry more than cells can still be filled through the inherited overloads.
	/// </remarks>
	protected virtual TableRow CreateRow(string[] cells) => new TableRow(cells);

	/// <summary>Appends rows after the last data row.</summary>
	private void AppendRows(IReadOnlyList<TableRow> rows)
	{
		int index;
		lock (_tableLock) { index = _rows.Count; }
		InsertRowsCore(index, rows);
	}

	/// <summary>
	/// Inserts rows at a data index, keeping the sort, the filter and the selection.
	/// </summary>
	/// <param name="index">The data index to insert at; clamped to the data row count.</param>
	/// <param name="rows">The rows to insert, in order; never empty.</param>
	/// <remarks>
	/// One complete change: the selection is recorded by row, the rows are inserted under
	/// <see cref="SyncRoot"/>, the displayed rows are recomputed, the selection is put back on its
	/// rows, and the layout is refreshed. Called by every add and insert overload. Throws
	/// <see cref="InvalidOperationException"/> while a data source is set, or when called from
	/// <see cref="ComputeDisplayRows"/>.
	/// </remarks>
	protected virtual void InsertRowsCore(int index, IReadOnlyList<TableRow> rows)
	{
		if (_dataSource != null)
			throw new InvalidOperationException("Cannot insert rows when DataSource is set.");
		ThrowIfComputingDisplayRows();

		var selection = CaptureSelection();

		lock (_tableLock)
		{
			index = Math.Clamp(index, 0, _rows.Count);
			foreach (var row in rows) row.Owner = this;
			_rows.InsertRange(index, rows);
			_rowView.RecordInsert(index, rows.Count);
		}

		selection.ShiftForInsert(index, rows.Count);
		RebuildDisplayMap();
		RestoreSelection(selection);
		InvalidateAfterRowChange();
	}

	/// <summary>
	/// Removes a range of data rows, keeping the sort, the filter and the selection.
	/// </summary>
	/// <param name="index">The first data index to remove.</param>
	/// <param name="count">How many rows to remove.</param>
	/// <remarks>
	/// One complete change, as <see cref="InsertRowsCore"/> is. Called by <see cref="RemoveRow(int)"/>.
	/// A range that does not lie within the data rows, as it is when the rows changed after the caller
	/// checked them, removes nothing, as <see cref="RemoveRow(int)"/> does with an index out of range.
	/// A selected row that is removed hands the cursor to the row <see cref="ResolveRemovedSelectedRow"/>
	/// names, by default the row now in its place.
	/// </remarks>
	protected virtual void RemoveRowsCore(int index, int count)
	{
		ThrowIfComputingDisplayRows();

		var selection = CaptureSelection();

		lock (_tableLock)
		{
			// Checked again under the lock that removes: the caller checked under an earlier one.
			if (index < 0 || count <= 0 || index > _rows.Count - count)
				return;

			for (int i = index; i < index + count; i++)
				_rows[i].Owner = null;
			_rows.RemoveRange(index, count);
			_rowView.RecordRemove(index, count);
		}

		selection.ShiftForRemove(index, count);
		RebuildDisplayMap();
		RestoreSelection(selection);
		InvalidateAfterRowChange();
	}

	/// <summary>
	/// Replaces every row, applying the sort and the filter to the new set and keeping the
	/// selection on the rows it still contains.
	/// </summary>
	/// <param name="rows">The new rows, copied; the caller keeps its list.</param>
	/// <remarks>
	/// One complete change, as <see cref="InsertRowsCore"/> is. Called by <see cref="SetData"/>, and by
	/// <see cref="ClearRows"/> with no rows after it has reset the selection, the scroll and the filter.
	/// Rows that stay are found again by reference.
	/// </remarks>
	protected virtual void SetDataCore(IReadOnlyList<TableRow> rows)
	{
		ThrowIfComputingDisplayRows();

		var selection = CaptureSelection();
		List<TableRow> oldRows;

		lock (_tableLock)
		{
			oldRows = _rows;
			foreach (var oldRow in oldRows) oldRow.Owner = null;
			_rows = new List<TableRow>(rows);
			foreach (var row in _rows) row.Owner = this;
			_rowView.RecordReset();
		}

		selection.RemapByIdentity(oldRows, rows);
		// A data source's rows are not these; its map is left as the source made it.
		if (_dataSource == null)
			RebuildDisplayMap();
		RestoreSelection(selection);
		InvalidateAfterRowChange();
	}

	/// <summary>The width and layout invalidation every change to the rows needs.</summary>
	private void InvalidateAfterRowChange()
	{
		InvalidateColumnWidths();
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

		/// <summary>The cursor's row, kept to name it once a change has removed it.</summary>
		internal TableRow? CursorRow;

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
			int dataRow = DataRowAt(position);
			if (dataRow >= 0)
				selected.Add(dataRow);
		}

		int cursor = DataRowAt(_selectedRowIndex);
		TableRow? cursorRow = null;
		if (cursor >= 0)
		{
			lock (_tableLock)
			{
				if (cursor < _rows.Count)
					cursorRow = _rows[cursor];
			}
		}

		return new RowSelection
		{
			Cursor = cursor,
			CursorRow = cursorRow,
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
	/// A cursor whose row is still displayed follows it to its new position. One whose row is still
	/// in the table but hidden goes to the row <see cref="ResolveHiddenSelectedRow"/> names, and one
	/// whose row was removed to the row <see cref="ResolveRemovedSelectedRow"/> names. One without a
	/// stand-in lands on the row now at its old position, clamped to the rows there are, which is what
	/// removing the selected row has always done.
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
		// The rows moved under the pointer, whether or not the selection can follow them.
		EndRowGestures();

		if (!before.IsTracked) return;

		int rowCount = RowCount;
		int oldPosition = _selectedRowIndex;
		bool rowChanged = false;

		if (before.Cursor != -1)
		{
			int position = before.Cursor >= 0 ? MapDataToDisplay(before.Cursor) : -1;
			if (position < 0 || position >= rowCount)
			{
				// Hidden or removed: a derived table may know which row stands in for it.
				int standIn = ResolveStandInRow(in before);
				position = standIn >= 0 && standIn < DataRowCount ? MapDataToDisplay(standIn) : -1;
				if (position < 0 || position >= rowCount)
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
