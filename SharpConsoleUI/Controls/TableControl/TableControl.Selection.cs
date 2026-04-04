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
	#region Selection Events

	/// <summary>
	/// Occurs when the selected row index changes.
	/// </summary>
	public event EventHandler<int>? SelectedRowChanged;

	/// <summary>
	/// Fired when multi-selection changes (checkbox toggled, selection cleared, select all).
	/// Argument is the current count of selected rows.
	/// </summary>
	public event EventHandler<int>? MultiSelectionChanged;

	/// <summary>
	/// Occurs when the selected row item changes.
	/// </summary>
	public event EventHandler<TableRow?>? SelectedRowItemChanged;

	/// <summary>
	/// Occurs when a row is activated (Enter key or double-click).
	/// </summary>
	public event EventHandler<int>? RowActivated;

	/// <summary>
	/// Occurs when a cell is activated (Enter key in cell navigation mode).
	/// </summary>
	public event EventHandler<(int Row, int Column)>? CellActivated;

	#endregion

	#region Selection Properties

	/// <summary>
	/// Gets or sets the selected row index. -1 means no selection.
	/// </summary>
	public int SelectedRowIndex
	{
		get => _selectedRowIndex;
		set => SetSelectedRow(value);
	}

	/// <summary>
	/// Gets the currently selected row, or null if no selection.
	/// </summary>
	public TableRow? SelectedRow
	{
		get
		{
			if (_dataSource != null || _selectedRowIndex < 0) return null;
			int dataIndex = MapDisplayToData(_selectedRowIndex);
			lock (_tableLock)
			{
				return dataIndex >= 0 && dataIndex < _rows.Count ? _rows[dataIndex] : null;
			}
		}
	}

	/// <summary>
	/// Gets or sets the selected column index for cell navigation. -1 means no column selected.
	/// </summary>
	public int SelectedColumnIndex
	{
		get => _cellNavigationEnabled ? _selectedColumnIndex : -1;
		set
		{
			if (!_cellNavigationEnabled) return;
			int colCount = ColumnCount;
			_selectedColumnIndex = Math.Clamp(value, -1, colCount - 1);
			OnPropertyChanged();
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Gets or sets whether cell-level navigation is enabled (Tab/Left/Right to move between cells).
	/// </summary>
	public bool CellNavigationEnabled
	{
		get => _cellNavigationEnabled;
		set
		{
			_cellNavigationEnabled = value;
			OnPropertyChanged();
			if (!value) _selectedColumnIndex = -1;
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Gets or sets whether mouse hover highlighting is enabled. Default: true.
	/// </summary>
	public bool HoverEnabled
	{
		get => _hoverEnabled;
		set { _hoverEnabled = value; OnPropertyChanged(); Container?.Invalidate(true); }
	}

	/// <summary>
	/// Gets or sets whether multi-selection is enabled.
	/// </summary>
	public bool MultiSelectEnabled
	{
		get => _multiSelectEnabled;
		set
		{
			_multiSelectEnabled = value;
			OnPropertyChanged();
			if (!value)
			{
				_selectedRowIndices.Clear();
				_checkboxMode = false;
			}
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Gets or sets whether checkbox mode is enabled (shows [x]/[ ] before each row).
	/// Requires MultiSelectEnabled = true.
	/// </summary>
	public bool CheckboxMode
	{
		get => _checkboxMode;
		set
		{
			_checkboxMode = value;
			OnPropertyChanged();
			if (value) _multiSelectEnabled = true;
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Gets or sets whether to auto-highlight the first row when the control gains focus.
	/// Default: true.
	/// </summary>
	public bool AutoHighlightOnFocus
	{
		get => _autoHighlightOnFocus;
		set { _autoHighlightOnFocus = value; OnPropertyChanged(); }
	}

	#endregion

	#region Selection Methods

	/// <summary>
	/// Sets the selected row, clamping to valid range and firing events.
	/// </summary>
	internal void SetSelectedRow(int index)
	{
		int rowCount = RowCount;
		int newIndex = rowCount == 0 ? -1 : Math.Clamp(index, -1, rowCount - 1);

		// Skip disabled rows (search forward)
		if (newIndex >= 0)
		{
			int dataIndex = MapDisplayToData(newIndex);
			if (_dataSource != null)
			{
				if (!_dataSource.IsRowEnabled(dataIndex))
				{
					// Try to find next enabled row
					int direction = newIndex > _selectedRowIndex ? 1 : -1;
					int original = newIndex;
					while (newIndex >= 0 && newIndex < rowCount && !_dataSource.IsRowEnabled(MapDisplayToData(newIndex)))
						newIndex += direction;
					if (newIndex < 0 || newIndex >= rowCount)
						newIndex = _selectedRowIndex; // stay at current
				}
			}
			else
			{
				lock (_tableLock)
				{
					if (dataIndex >= 0 && dataIndex < _rows.Count && !_rows[dataIndex].IsEnabled)
					{
						int direction = newIndex > _selectedRowIndex ? 1 : -1;
						while (newIndex >= 0 && newIndex < rowCount)
						{
							int di = MapDisplayToData(newIndex);
							if (di >= 0 && di < _rows.Count && _rows[di].IsEnabled) break;
							newIndex += direction;
						}
						if (newIndex < 0 || newIndex >= rowCount)
							newIndex = _selectedRowIndex;
					}
				}
			}
		}

		if (newIndex == _selectedRowIndex) return;

		int oldIndex = _selectedRowIndex;
		_selectedRowIndex = newIndex;

		EnsureSelectedRowVisible();

		SelectedRowChanged?.Invoke(this, newIndex);

		if (_dataSource == null)
		{
			int dataIdx = newIndex >= 0 ? MapDisplayToData(newIndex) : -1;
			TableRow? row = null;
			lock (_tableLock)
			{
				if (dataIdx >= 0 && dataIdx < _rows.Count)
					row = _rows[dataIdx];
			}
			SelectedRowItemChanged?.Invoke(this, row);
		}

		Container?.Invalidate(true);
	}

	/// <summary>
	/// Selects all rows (multi-select mode).
	/// </summary>
	public void SelectAll()
	{
		if (!_multiSelectEnabled) return;
		int count = RowCount;
		_selectedRowIndices.Clear();
		for (int i = 0; i < count; i++)
		{
			_selectedRowIndices.Add(i);
			if (_checkboxMode)
				SyncCheckboxState(i);
		}
		Container?.Invalidate(true);
		MultiSelectionChanged?.Invoke(this, _selectedRowIndices.Count);
	}

	/// <summary>
	/// Clears all selection (multi-select mode).
	/// </summary>
	public void ClearSelection()
	{
		if (_checkboxMode && _dataSource == null)
		{
			lock (_tableLock)
			{
				foreach (var row in _rows)
					row.IsChecked = false;
			}
		}
		_selectedRowIndices.Clear();
		Container?.Invalidate(true);
		MultiSelectionChanged?.Invoke(this, 0);
	}

	/// <summary>
	/// Gets all selected rows in multi-select mode.
	/// </summary>
	public List<TableRow> GetSelectedRows()
	{
		if (_dataSource != null) return new List<TableRow>();
		var result = new List<TableRow>();
		lock (_tableLock)
		{
			foreach (int displayIdx in _selectedRowIndices.OrderBy(i => i))
			{
				int dataIdx = MapDisplayToData(displayIdx);
				if (dataIdx >= 0 && dataIdx < _rows.Count)
					result.Add(_rows[dataIdx]);
			}
		}
		return result;
	}

	/// <summary>
	/// Gets all checked rows (checkbox mode).
	/// </summary>
	public List<TableRow> GetCheckedRows()
	{
		lock (_tableLock)
		{
			return _rows.Where(r => r.IsChecked).ToList();
		}
	}

	/// <summary>
	/// Toggles selection of a row in multi-select mode (Ctrl+Click).
	/// </summary>
	internal void ToggleRowSelection(int displayIndex)
	{
		if (!_multiSelectEnabled) return;
		if (_selectedRowIndices.Contains(displayIndex))
			_selectedRowIndices.Remove(displayIndex);
		else
			_selectedRowIndices.Add(displayIndex);

		// Sync checkbox state
		if (_checkboxMode)
			SyncCheckboxState(displayIndex);

		Container?.Invalidate(true);
		MultiSelectionChanged?.Invoke(this, _selectedRowIndices.Count);
	}

	/// <summary>
	/// Selects a range of rows in multi-select mode (Shift+Click).
	/// </summary>
	internal void SelectRange(int fromDisplayIndex, int toDisplayIndex)
	{
		if (!_multiSelectEnabled) return;
		int start = Math.Min(fromDisplayIndex, toDisplayIndex);
		int end = Math.Max(fromDisplayIndex, toDisplayIndex);
		for (int i = start; i <= end; i++)
		{
			_selectedRowIndices.Add(i);
			if (_checkboxMode)
				SyncCheckboxState(i);
		}
		Container?.Invalidate(true);
		MultiSelectionChanged?.Invoke(this, _selectedRowIndices.Count);
	}

	/// <summary>
	/// Syncs the IsChecked property of a row with its selection state.
	/// </summary>
	private void SyncCheckboxState(int displayIndex)
	{
		if (_dataSource != null) return;
		lock (_tableLock)
		{
			int dataIdx = MapDisplayToData(displayIndex);
			if (dataIdx >= 0 && dataIdx < _rows.Count)
				_rows[dataIdx].IsChecked = _selectedRowIndices.Contains(displayIndex);
		}
	}

	/// <summary>
	/// Returns whether a display row index is selected (in multi-select mode) or is the current selected row.
	/// </summary>
	internal bool IsRowSelected(int displayIndex)
	{
		if (_multiSelectEnabled && _selectedRowIndices.Contains(displayIndex))
			return true;
		return displayIndex == _selectedRowIndex;
	}

	#endregion
}
