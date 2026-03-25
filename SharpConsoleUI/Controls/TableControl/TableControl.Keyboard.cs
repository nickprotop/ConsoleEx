using SharpConsoleUI.Extensions;
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
	/// <inheritdoc/>
	public bool ProcessKey(ConsoleKeyInfo key)
	{
		if (!_isEnabled || !HasFocus)
			return false;

		// Handle filter typing mode
		if (_filterMode == FilterMode.Typing)
			return ProcessFilterKey(key);

		// Handle editing mode (only available when not read-only)
		if (_isEditing)
			return ProcessEditKey(key);

		int rowCount = RowCount;
		if (rowCount == 0) return false;

		switch (key.Key)
		{
			case ConsoleKey.DownArrow:
				if (_hoveredRowIndex != -1) _hoveredRowIndex = -1;
				if (_selectedRowIndex < rowCount - 1)
				{
					SetSelectedRow(_selectedRowIndex + 1);
					return true;
				}
				return false;

			case ConsoleKey.UpArrow:
				if (_hoveredRowIndex != -1) _hoveredRowIndex = -1;
				if (_selectedRowIndex > 0)
				{
					SetSelectedRow(_selectedRowIndex - 1);
					return true;
				}
				return false;

			case ConsoleKey.Home:
				if (_hoveredRowIndex != -1) _hoveredRowIndex = -1;
				if (rowCount > 0)
				{
					SetSelectedRow(0);
					return true;
				}
				return false;

			case ConsoleKey.End:
				if (_hoveredRowIndex != -1) _hoveredRowIndex = -1;
				if (rowCount > 0)
				{
					SetSelectedRow(rowCount - 1);
					return true;
				}
				return false;

			case ConsoleKey.PageDown:
				if (_hoveredRowIndex != -1) _hoveredRowIndex = -1;
				if (_selectedRowIndex < rowCount - 1)
				{
					int visibleRows = GetVisibleRowCount();
					SetSelectedRow(Math.Min(rowCount - 1, _selectedRowIndex + visibleRows));
					return true;
				}
				return false;

			case ConsoleKey.PageUp:
				if (_hoveredRowIndex != -1) _hoveredRowIndex = -1;
				if (_selectedRowIndex > 0)
				{
					int visibleRows = GetVisibleRowCount();
					SetSelectedRow(Math.Max(0, _selectedRowIndex - visibleRows));
					return true;
				}
				return false;

			case ConsoleKey.Tab when _cellNavigationEnabled:
				if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
				{
					// Shift+Tab: move backward
					if (_selectedColumnIndex > 0)
					{
						_selectedColumnIndex--;
					}
					else if (_selectedRowIndex > 0)
					{
						SetSelectedRow(_selectedRowIndex - 1);
						_selectedColumnIndex = ColumnCount - 1;
					}
					else
					{
						return false; // Let tab move to previous control
					}
				}
				else
				{
					// Tab: move forward
					int colCount = ColumnCount;
					if (_selectedColumnIndex < colCount - 1)
					{
						_selectedColumnIndex++;
					}
					else if (_selectedRowIndex < rowCount - 1)
					{
						SetSelectedRow(_selectedRowIndex + 1);
						_selectedColumnIndex = 0;
					}
					else
					{
						return false; // Let tab move to next control
					}
				}
				Container?.Invalidate(true);
				return true;

			case ConsoleKey.LeftArrow when _cellNavigationEnabled:
				if (_selectedColumnIndex > 0)
				{
					_selectedColumnIndex--;
					Container?.Invalidate(true);
					return true;
				}
				return false;

			case ConsoleKey.RightArrow when _cellNavigationEnabled:
				if (_selectedColumnIndex < ColumnCount - 1)
				{
					_selectedColumnIndex++;
					Container?.Invalidate(true);
					return true;
				}
				return false;

			case ConsoleKey.Escape:
				// Escape clears confirmed filter
				if (_filterMode == FilterMode.Confirmed)
				{
					ClearFilter();
					return true;
				}
				// Escape deselects cell (back to row mode) when cell is selected
				if (_cellNavigationEnabled && _selectedColumnIndex >= 0)
				{
					_selectedColumnIndex = -1;
					Container?.Invalidate(true);
					return true;
				}
				return false;

			case ConsoleKey.Enter:
				if (_selectedRowIndex >= 0)
				{
					// Enter on a selected cell: enter edit mode if available
					if (_cellNavigationEnabled && _selectedColumnIndex >= 0)
					{
						if (!_readOnly && _inlineEditingEnabled)
						{
							BeginCellEdit();
							return true;
						}
						CellActivated?.Invoke(this, (_selectedRowIndex, _selectedColumnIndex));
					}
					else
					{
						RowActivated?.Invoke(this, _selectedRowIndex);
					}
					return true;
				}
				return false;

			case ConsoleKey.F2 when !_readOnly && _inlineEditingEnabled && _cellNavigationEnabled:
				if (_selectedRowIndex >= 0 && _selectedColumnIndex >= 0)
				{
					BeginCellEdit();
					return true;
				}
				return false;

			case ConsoleKey.Spacebar when _checkboxMode && _multiSelectEnabled:
				if (_selectedRowIndex >= 0)
				{
					int dataIdx = MapDisplayToData(_selectedRowIndex);
					if (_dataSource == null)
					{
						lock (_tableLock)
						{
							if (dataIdx >= 0 && dataIdx < _rows.Count)
							{
								_rows[dataIdx].IsChecked = !_rows[dataIdx].IsChecked;
								Container?.Invalidate(true);
								return true;
							}
						}
					}
				}
				return false;

			case ConsoleKey.A when key.Modifiers.HasFlag(ConsoleModifiers.Control) && _multiSelectEnabled:
				SelectAll();
				return true;

			default:
				// '/' enters filter mode
				if (_filteringEnabled && !_readOnly && key.KeyChar == '/')
				{
					EnterFilterMode();
					return true;
				}
				return false;
		}
	}
}
