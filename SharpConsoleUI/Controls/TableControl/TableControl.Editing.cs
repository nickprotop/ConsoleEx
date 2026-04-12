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
	#region Editing Events

	/// <summary>
	/// Occurs when a cell edit is committed (Enter key).
	/// </summary>
	public event EventHandler<(int Row, int Column, string OldValue, string NewValue)>? CellEditCompleted;

	/// <summary>
	/// Occurs when a cell edit is cancelled (Escape key).
	/// </summary>
	public event EventHandler<(int Row, int Column)>? CellEditCancelled;

	#endregion

	#region Editing Properties

	/// <summary>
	/// Gets or sets whether inline cell editing is enabled (F2 to edit, Enter to commit, Escape to cancel).
	/// Requires CellNavigationEnabled = true.
	/// </summary>
	public bool InlineEditingEnabled
	{
		get => _inlineEditingEnabled;
		set
		{
			_inlineEditingEnabled = value;
			OnPropertyChanged();
			if (value) _cellNavigationEnabled = true;
		}
	}

	/// <summary>
	/// Gets whether the control is currently in cell editing mode.
	/// </summary>
	public bool IsEditing => _isEditing;

	/// <summary>
	/// Gets or sets whether column resizing by dragging column borders is enabled.
	/// </summary>
	public bool ColumnResizeEnabled
	{
		get => _columnResizeEnabled;
		set { _columnResizeEnabled = value; OnPropertyChanged(); }
	}

	#endregion

	#region Editing Methods

	/// <summary>
	/// Begins editing the currently selected cell.
	/// </summary>
	internal void BeginCellEdit()
	{
		if (!_inlineEditingEnabled || !_cellNavigationEnabled) return;
		if (_selectedRowIndex < 0 || _selectedColumnIndex < 0) return;
		if (_readOnly) return;

		int dataIdx = MapDisplayToData(_selectedRowIndex);
		string currentValue;

		if (_dataSource != null)
		{
			if (dataIdx < 0 || dataIdx >= _dataSource.RowCount) return;
			if (_selectedColumnIndex >= _dataSource.ColumnCount) return;
			currentValue = _dataSource.GetCellValue(dataIdx, _selectedColumnIndex);
		}
		else
		{
			lock (_tableLock)
			{
				if (dataIdx < 0 || dataIdx >= _rows.Count) return;
				if (_selectedColumnIndex >= _rows[dataIdx].Cells.Count) return;
				currentValue = _rows[dataIdx].Cells[_selectedColumnIndex];
			}
		}

		// Strip markup for editing
		_editBuffer = Parsing.MarkupParser.Remove(currentValue);
		_editCursorPosition = _editBuffer.Length;
		_isEditing = true;
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Commits the current cell edit.
	/// </summary>
	internal void CommitEdit()
	{
		if (!_isEditing) return;

		int dataIdx = MapDisplayToData(_selectedRowIndex);
		string oldValue;

		if (_dataSource != null)
		{
			if (dataIdx < 0 || dataIdx >= _dataSource.RowCount || _selectedColumnIndex >= _dataSource.ColumnCount)
			{
				_isEditing = false;
				return;
			}
			oldValue = _dataSource.GetCellValue(dataIdx, _selectedColumnIndex);
		}
		else
		{
			lock (_tableLock)
			{
				if (dataIdx < 0 || dataIdx >= _rows.Count)
				{
					_isEditing = false;
					return;
				}
				if (_selectedColumnIndex >= _rows[dataIdx].Cells.Count)
				{
					_isEditing = false;
					return;
				}

				oldValue = _rows[dataIdx].Cells[_selectedColumnIndex];
				_rows[dataIdx].Cells[_selectedColumnIndex] = _editBuffer;
			}
		}

		_isEditing = false;
		string committedValue = _editBuffer;
		_editBuffer = string.Empty;
		_editCursorPosition = 0;
		InvalidateColumnWidths();
		_measurementCache.InvalidateCachedEntry(committedValue);
		CellEditCompleted?.Invoke(this, (_selectedRowIndex, _selectedColumnIndex, oldValue, committedValue));
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Cancels the current cell edit.
	/// </summary>
	internal void CancelEdit()
	{
		if (!_isEditing) return;
		_isEditing = false;
		_editBuffer = string.Empty;
		_editCursorPosition = 0;
		CellEditCancelled?.Invoke(this, (_selectedRowIndex, _selectedColumnIndex));
		Container?.Invalidate(true);
	}

	/// <summary>
	/// Processes a key while in edit mode.
	/// </summary>
	internal bool ProcessEditKey(ConsoleKeyInfo key)
	{
		bool ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);

		// Ctrl combinations (readline-style)
		if (ctrl)
		{
			switch (key.Key)
			{
				case ConsoleKey.A: // move to start (no selection model in inline editor)
					_editCursorPosition = 0;
					Container?.Invalidate(true);
					return true;

				case ConsoleKey.E: // move to end
					_editCursorPosition = _editBuffer.Length;
					Container?.Invalidate(true);
					return true;

				case ConsoleKey.K: // kill to end
					if (_editCursorPosition < _editBuffer.Length)
					{
						_editBuffer = _editBuffer.Substring(0, _editCursorPosition);
						Container?.Invalidate(true);
					}
					return true;

				case ConsoleKey.U: // kill to start
					if (_editCursorPosition > 0)
					{
						_editBuffer = _editBuffer.Substring(_editCursorPosition);
						_editCursorPosition = 0;
						Container?.Invalidate(true);
					}
					return true;

				case ConsoleKey.W: // kill word backward
					if (_editCursorPosition > 0)
					{
						int i = _editCursorPosition - 1;
						while (i > 0 && char.IsWhiteSpace(_editBuffer[i])) i--;
						while (i > 0 && !char.IsWhiteSpace(_editBuffer[i - 1])) i--;
						_editBuffer = _editBuffer.Remove(i, _editCursorPosition - i);
						_editCursorPosition = i;
						Container?.Invalidate(true);
					}
					return true;

				case ConsoleKey.C: // copy all edit buffer
					if (!string.IsNullOrEmpty(_editBuffer))
						Helpers.ClipboardHelper.SetText(_editBuffer);
					return true;

				case ConsoleKey.V: // paste
				{
					var clip = Helpers.ClipboardHelper.GetText();
					if (!string.IsNullOrEmpty(clip))
					{
						clip = clip.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
						_editBuffer = _editBuffer.Insert(_editCursorPosition, clip);
						_editCursorPosition += clip.Length;
						Container?.Invalidate(true);
					}
					return true;
				}

				case ConsoleKey.X: // cut (copy + clear)
					if (!string.IsNullOrEmpty(_editBuffer))
					{
						Helpers.ClipboardHelper.SetText(_editBuffer);
						_editBuffer = string.Empty;
						_editCursorPosition = 0;
						Container?.Invalidate(true);
					}
					return true;

				case ConsoleKey.LeftArrow: // word left
					if (_editCursorPosition > 0)
					{
						int i = _editCursorPosition - 1;
						while (i > 0 && char.IsWhiteSpace(_editBuffer[i])) i--;
						while (i > 0 && !char.IsWhiteSpace(_editBuffer[i - 1])) i--;
						_editCursorPosition = i;
						Container?.Invalidate(true);
					}
					return true;

				case ConsoleKey.RightArrow: // word right
					if (_editCursorPosition < _editBuffer.Length)
					{
						int i = _editCursorPosition;
						while (i < _editBuffer.Length && !char.IsWhiteSpace(_editBuffer[i])) i++;
						while (i < _editBuffer.Length && char.IsWhiteSpace(_editBuffer[i])) i++;
						_editCursorPosition = i;
						Container?.Invalidate(true);
					}
					return true;
			}
			return true; // consume other Ctrl combos in edit mode
		}

		switch (key.Key)
		{
			case ConsoleKey.Enter:
				CommitEdit();
				return true;

			case ConsoleKey.Escape:
				CancelEdit();
				return true;

			case ConsoleKey.Backspace:
				if (_editCursorPosition > 0)
				{
					_editBuffer = _editBuffer.Remove(_editCursorPosition - 1, 1);
					_editCursorPosition--;
					Container?.Invalidate(true);
				}
				return true;

			case ConsoleKey.Delete:
				if (_editCursorPosition < _editBuffer.Length)
				{
					_editBuffer = _editBuffer.Remove(_editCursorPosition, 1);
					Container?.Invalidate(true);
				}
				return true;

			case ConsoleKey.LeftArrow:
				if (_editCursorPosition > 0)
				{
					_editCursorPosition--;
					Container?.Invalidate(true);
				}
				return true;

			case ConsoleKey.RightArrow:
				if (_editCursorPosition < _editBuffer.Length)
				{
					_editCursorPosition++;
					Container?.Invalidate(true);
				}
				return true;

			case ConsoleKey.Home:
				_editCursorPosition = 0;
				Container?.Invalidate(true);
				return true;

			case ConsoleKey.End:
				_editCursorPosition = _editBuffer.Length;
				Container?.Invalidate(true);
				return true;

			default:
				if (!char.IsControl(key.KeyChar))
				{
					_editBuffer = _editBuffer.Insert(_editCursorPosition, key.KeyChar.ToString());
					_editCursorPosition++;
					Container?.Invalidate(true);
					return true;
				}
				return false; // let unrecognized keys bubble up
		}
	}

	#endregion
}
