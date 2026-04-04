// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls;

public partial class TableControl
{
	/// <inheritdoc/>
	public bool ProcessMouseEvent(MouseEventArgs args)
	{
		// Mouse enter/leave/move events
		if (args.HasFlag(MouseFlags.MouseEnter))
		{
			MouseEnter?.Invoke(this, args);
			return true;
		}

		if (args.HasFlag(MouseFlags.MouseLeave))
		{
			if (_hoveredRowIndex != -1)
			{
				_hoveredRowIndex = -1;
				Container?.Invalidate(true);
			}
			MouseLeave?.Invoke(this, args);
			return true;
		}

		// Handle drag-in-progress (must be checked early)
		// Button1Dragged = real mouse movement; Button1Pressed = synthetic continuous-press repeats
		if (args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed))
		{
			if (_isVerticalScrollbarDragging)
			{
				HandleVerticalScrollbarDrag(args);
				return true;
			}

			if (_isHorizontalScrollbarDragging)
			{
				HandleHorizontalScrollbarDrag(args);
				return true;
			}

			if (_isResizingColumn)
			{
				HandleColumnResizeDrag(args);
				return true;
			}
		}

		// Handle scrollbar drag end
		if (args.HasFlag(MouseFlags.Button1Released))
		{
			if (_isVerticalScrollbarDragging || _isHorizontalScrollbarDragging)
			{
				_isVerticalScrollbarDragging = false;
				_isHorizontalScrollbarDragging = false;
				return true;
			}
			if (_isResizingColumn)
			{
				_isResizingColumn = false;
				return true;
			}
			// Don't return false here — the event may also contain Button1Clicked
			// which must be processed by handlers below (Unix SGR sends both flags together)
		}

		// Hover tracking
		if (args.HasFlag(MouseFlags.ReportMousePosition))
		{
			if (_hoverEnabled)
			{
				int rowIdx = GetRowIndexAtY(args.Position.Y);
				if (rowIdx != _hoveredRowIndex)
				{
					_hoveredRowIndex = rowIdx;
					Container?.Invalidate(true);
				}
			}

			MouseMove?.Invoke(this, args);
			return true;
		}

		// Right-click: select row/cell first, then fire event
		if (args.HasFlag(MouseFlags.Button3Clicked))
		{
			if (_isEditing)
				CancelEdit();
			if (!HasFocus && CanFocusWithMouse)
				this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
			int rowIdx = GetRowIndexAtY(args.Position.Y);
			if (rowIdx >= 0)
			{
				SetSelectedRow(rowIdx);
				if (_cellNavigationEnabled)
				{
					int colIdx = GetColumnIndexAtX(args.Position.X);
					if (colIdx >= 0)
					{
						_selectedColumnIndex = colIdx;
						Container?.Invalidate(true);
					}
				}
			}
			MouseRightClick?.Invoke(this, args);
			return true;
		}

		// Mouse wheel
		if (args.HasFlag(MouseFlags.WheeledUp))
		{
			if (args.HasFlag(MouseFlags.ButtonShift))
			{
				int oldH = _horizontalScrollOffset;
				_horizontalScrollOffset = Math.Max(0, _horizontalScrollOffset - 3);
				if (_horizontalScrollOffset != oldH)
				{
					Container?.Invalidate(true);
					return true;
				}
				return false; // bubble to parent
			}

			int oldOffset = _scrollOffset;
			ScrollOffset = Math.Max(0, _scrollOffset - 3);
			return _scrollOffset != oldOffset; // bubble if didn't scroll
		}

		if (args.HasFlag(MouseFlags.WheeledDown))
		{
			if (args.HasFlag(MouseFlags.ButtonShift))
			{
				int oldH = _horizontalScrollOffset;
				_horizontalScrollOffset += 3;
				if (_horizontalScrollOffset != oldH)
				{
					Container?.Invalidate(true);
					return true;
				}
				return false; // bubble to parent
			}

			int oldOffset = _scrollOffset;
			int maxOffset = Math.Max(0, RowCount - GetVisibleRowCount());
			ScrollOffset = Math.Min(maxOffset, _scrollOffset + 3);
			return _scrollOffset != oldOffset; // bubble if didn't scroll
		}

		// Left-click
		if (args.HasFlag(MouseFlags.Button1Clicked) || args.HasFlag(MouseFlags.Button1Pressed))
		{
			// Cancel any active cell edit before changing selection
			if (_isEditing)
				CancelEdit();

			// Set focus on click
			if (!HasFocus && CanFocusWithMouse)
				this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

			// Check if clicking on scrollbar
			if (IsClickOnVerticalScrollbar(args))
			{
				HandleVerticalScrollbarClick(args);
				return true;
			}

			if (IsClickOnHorizontalScrollbar(args))
			{
				HandleHorizontalScrollbarClick(args);
				return true;
			}

			// Check if clicking on column resize border (only when not read-only)
			if (!_readOnly && _columnResizeEnabled && IsClickOnColumnBorder(args))
			{
				BeginColumnResize(args);
				return true;
			}

			// Check if clicking on header (for sorting)
			if (IsClickOnHeader(args))
			{
				if (_sortingEnabled)
				{
					int colIdx = GetColumnIndexAtX(args.Position.X);
					if (colIdx >= 0)
						SortByColumn(colIdx);
				}
				MouseClick?.Invoke(this, args);
				return true;
			}

			// Click on data row
			int rowIdx = GetRowIndexAtY(args.Position.Y);
			bool isClick = args.HasFlag(MouseFlags.Button1Clicked);
			if (rowIdx >= 0)
			{
				// Multi-select toggling only on Button1Clicked to avoid
				// double-toggle from Button1Pressed + Button1Clicked pair
				if (!isClick)
				{
					// Button1Pressed: just move cursor, don't touch multi-select state
					SetSelectedRow(rowIdx);
				}
				else if (_multiSelectEnabled && args.HasFlag(MouseFlags.ButtonCtrl))
				{
					ToggleRowSelection(rowIdx);
					SetSelectedRow(rowIdx);
				}
				else if (_multiSelectEnabled && args.HasFlag(MouseFlags.ButtonShift))
				{
					int anchor = _selectedRowIndex >= 0 ? _selectedRowIndex : 0;
					_selectedRowIndices.Clear();
					SelectRange(anchor, rowIdx);
					SetSelectedRow(rowIdx);
				}
				else if (_checkboxMode && IsClickOnCheckbox(args.Position.X))
				{
					// Click on checkbox column — toggle without clearing other selections
					ToggleRowSelection(rowIdx);
					SetSelectedRow(rowIdx);
				}
				else
				{
					if (_multiSelectEnabled)
					{
						// Clear checkboxes when clicking outside checkbox column
						if (_checkboxMode && _dataSource == null)
						{
							lock (_tableLock)
							{
								foreach (var row in _rows)
									row.IsChecked = false;
							}
						}
						_selectedRowIndices.Clear();
					}
					SetSelectedRow(rowIdx);
				}

				// Cell navigation: select column
				if (_cellNavigationEnabled)
				{
					int colIdx = GetColumnIndexAtX(args.Position.X);
					if (colIdx >= 0)
					{
						_selectedColumnIndex = colIdx;
						Container?.Invalidate(true);
					}
				}

				// Double-click detection
				lock (_clickLock)
				{
					var now = DateTime.Now;
					if (_lastClickRowIndex == rowIdx &&
						(now - _lastClickTime).TotalMilliseconds < _doubleClickThresholdMs)
					{
						RowActivated?.Invoke(this, rowIdx);
						MouseDoubleClick?.Invoke(this, args);
						_lastClickTime = DateTime.MinValue;
						_lastClickRowIndex = -1;
					}
					else
					{
						_lastClickTime = now;
						_lastClickRowIndex = rowIdx;
					}
				}
			}

			MouseClick?.Invoke(this, args);
			return true;
		}

		// Double-click (from driver)
		if (args.HasFlag(MouseFlags.Button1DoubleClicked))
		{
			// Cancel any active edit before starting a new one
			if (_isEditing)
				CancelEdit();

			int rowIdx = GetRowIndexAtY(args.Position.Y);
			if (rowIdx >= 0)
			{
				SetSelectedRow(rowIdx);

				// Cell navigation: select the clicked cell
				if (_cellNavigationEnabled)
				{
					int colIdx = GetColumnIndexAtX(args.Position.X);
					if (colIdx >= 0)
					{
						_selectedColumnIndex = colIdx;

						// Enter edit mode on double-click if editing is enabled
						if (!_readOnly && _inlineEditingEnabled)
						{
							BeginCellEdit();
							Container?.Invalidate(true);
							MouseDoubleClick?.Invoke(this, args);
							return true;
						}
					}
				}

				RowActivated?.Invoke(this, rowIdx);
			}
			MouseDoubleClick?.Invoke(this, args);
			return true;
		}

		// Let scroll events bubble
		return false;
	}

	#region Hit Testing

	private int GetRowIndexAtY(int relativeY)
	{
		int dataStartY = 0;
		bool hasBorder = _borderStyle != BorderStyle.None;

		dataStartY += Margin.Top;
		if (!string.IsNullOrEmpty(_title)) dataStartY++;
		if (hasBorder) dataStartY++;
		if (_showHeader) dataStartY++;
		if (_showHeader && hasBorder) dataStartY++;

		int rowOffset = relativeY - dataStartY;
		if (rowOffset < 0) return -1;

		int effectiveRowIndex;
		if (_showRowSeparators && hasBorder)
		{
			effectiveRowIndex = rowOffset / 2;
			if (rowOffset % 2 != 0) return -1;
		}
		else
		{
			effectiveRowIndex = rowOffset;
		}

		int displayIndex = _scrollOffset + effectiveRowIndex;
		if (displayIndex >= RowCount) return -1;

		return displayIndex;
	}

	private int GetColumnIndexAtX(int relativeX)
	{
		// Use rendered column geometry (populated during PaintDOM, works for both DataSource and in-memory)
		for (int c = 0; c < _renderedColumnX.Length; c++)
		{
			int colStart = _renderedColumnX[c] - ActualX;
			int colEnd = colStart + _renderedColumnWidths[c];
			if (relativeX >= colStart && relativeX < colEnd)
				return c;
		}
		return -1;
	}

	private bool IsClickOnCheckbox(int relativeX)
	{
		if (!_checkboxMode || _renderedColumnX == null || _renderedColumnX.Length == 0) return false;
		// Checkbox "[x] " is 4 chars prepended to first column
		int firstColStart = _renderedColumnX[0] - ActualX;
		int checkboxEnd = firstColStart + 4;
		return relativeX >= firstColStart && relativeX < checkboxEnd;
	}

	private bool IsClickOnHeader(MouseEventArgs args)
	{
		if (!_showHeader) return false;
		int headerY = Margin.Top;
		if (!string.IsNullOrEmpty(_title)) headerY++;
		if (_borderStyle != BorderStyle.None) headerY++;
		return args.Position.Y == headerY;
	}

	private bool IsClickOnVerticalScrollbar(MouseEventArgs args)
	{
		if (!ShouldShowVerticalScrollbar()) return false;
		int scrollbarX = ActualWidth - Margin.Right - 1;
		return args.Position.X == scrollbarX;
	}

	private bool IsClickOnHorizontalScrollbar(MouseEventArgs args)
	{
		int maxY = ActualHeight - Margin.Bottom - 1;
		return ShouldShowHorizontalScrollbar() && args.Position.Y == maxY;
	}

	private bool IsClickOnColumnBorder(MouseEventArgs args)
	{
		if (!_showHeader) return false;
		for (int c = 0; c < _renderedColumnX.Length; c++)
		{
			int colEnd = _renderedColumnX[c] - ActualX + _renderedColumnWidths[c];
			if (Math.Abs(args.Position.X - colEnd) <= 1)
				return true;
		}
		return false;
	}

	#endregion

	#region Scrollbar Interaction

	private int GetScrollbarContentHeight()
	{
		int dataStartY = Margin.Top;
		if (!string.IsNullOrEmpty(_title)) dataStartY++;
		if (_borderStyle != BorderStyle.None) dataStartY++;
		if (_showHeader) dataStartY++;
		if (_showHeader && _borderStyle != BorderStyle.None) dataStartY++;

		int height = ActualHeight - dataStartY - Margin.Bottom;
		if (_borderStyle != BorderStyle.None) height--; // bottom border
		if (ShouldShowHorizontalScrollbar()) height--;
		return Math.Max(0, height);
	}

	private int GetScrollbarDataStartY()
	{
		int dataStartY = Margin.Top;
		if (!string.IsNullOrEmpty(_title)) dataStartY++;
		if (_borderStyle != BorderStyle.None) dataStartY++;
		if (_showHeader) dataStartY++;
		if (_showHeader && _borderStyle != BorderStyle.None) dataStartY++;
		return dataStartY;
	}

	private void HandleVerticalScrollbarClick(MouseEventArgs args)
	{
		int contentAreaHeight = GetScrollbarContentHeight();
		var (trackTop, trackHeight, thumbY, thumbHeight) = GetVerticalScrollbarGeometry(contentAreaHeight);
		int relY = args.Position.Y - GetScrollbarDataStartY();
		int maxOffset = Math.Max(0, RowCount - GetVisibleRowCount());

		if (relY == 0 && _scrollOffset > 0)
		{
			// Arrow up
			ScrollOffset = Math.Max(0, _scrollOffset - 1);
		}
		else if (relY == trackHeight - 1 && _scrollOffset < maxOffset)
		{
			// Arrow down
			ScrollOffset = Math.Min(maxOffset, _scrollOffset + 1);
		}
		else if (relY >= thumbY && relY < thumbY + thumbHeight)
		{
			// Thumb: start drag
			_isVerticalScrollbarDragging = true;
			_scrollbarDragStartY = args.Position.Y;
			_scrollbarDragStartOffset = _scrollOffset;
		}
		else if (relY < thumbY)
		{
			// Track above thumb: page up
			ScrollOffset = Math.Max(0, _scrollOffset - GetVisibleRowCount());
		}
		else
		{
			// Track below thumb: page down
			ScrollOffset = Math.Min(maxOffset, _scrollOffset + GetVisibleRowCount());
		}
	}

	private void HandleVerticalScrollbarDrag(MouseEventArgs args)
	{
		int contentAreaHeight = GetScrollbarContentHeight();
		var (_, _, _, thumbHeight) = GetVerticalScrollbarGeometry(contentAreaHeight);

		int totalRows = RowCount;
		int visibleRows = GetVisibleRowCount();
		int maxOffset = Math.Max(0, totalRows - visibleRows);
		int trackRange = Math.Max(1, contentAreaHeight - thumbHeight);

		if (maxOffset <= 0) return;

		int deltaY = args.Position.Y - _scrollbarDragStartY;
		int newOffset = _scrollbarDragStartOffset + (int)(deltaY * (double)maxOffset / trackRange);
		_scrollOffset = Math.Clamp(newOffset, 0, maxOffset);
		Container?.Invalidate(true);
	}

	private int GetScrollbarContentWidth()
	{
		int contentWidth = ActualWidth - Margin.Left - Margin.Right;
		if (_borderStyle != BorderStyle.None) contentWidth -= 2; // left + right border
		if (ShouldShowVerticalScrollbar()) contentWidth--;
		return Math.Max(0, contentWidth);
	}

	private void HandleHorizontalScrollbarClick(MouseEventArgs args)
	{
		int contentWidth = GetScrollbarContentWidth();
		int totalColumnsWidth = GetTotalColumnsWidth();
		var (trackLeft, trackWidth, thumbX, thumbWidth) = GetHorizontalScrollbarGeometry(contentWidth, totalColumnsWidth);

		int relX = args.Position.X - Margin.Left;
		if (_borderStyle != BorderStyle.None) relX--; // account for left border
		int maxHScroll = Math.Max(0, totalColumnsWidth - contentWidth);

		if (relX == 0 && _horizontalScrollOffset > 0)
		{
			// Arrow left
			_horizontalScrollOffset = Math.Max(0, _horizontalScrollOffset - 1);
		}
		else if (relX == trackWidth - 1 && _horizontalScrollOffset < maxHScroll)
		{
			// Arrow right
			_horizontalScrollOffset = Math.Min(maxHScroll, _horizontalScrollOffset + 1);
		}
		else if (relX >= thumbX && relX < thumbX + thumbWidth)
		{
			// Thumb: start drag
			_isHorizontalScrollbarDragging = true;
			_scrollbarDragStartX = args.Position.X;
			_scrollbarDragStartOffset = _horizontalScrollOffset;
		}
		else if (relX < thumbX)
		{
			// Track left of thumb: page left
			_horizontalScrollOffset = Math.Max(0, _horizontalScrollOffset - contentWidth);
		}
		else
		{
			// Track right of thumb: page right
			_horizontalScrollOffset = Math.Min(maxHScroll, _horizontalScrollOffset + contentWidth);
		}

		Container?.Invalidate(true);
	}

	private void HandleHorizontalScrollbarDrag(MouseEventArgs args)
	{
		int contentWidth = GetScrollbarContentWidth();
		int totalColumnsWidth = GetTotalColumnsWidth();
		var (_, _, _, thumbWidth) = GetHorizontalScrollbarGeometry(contentWidth, totalColumnsWidth);

		int maxHScroll = Math.Max(0, totalColumnsWidth - contentWidth);
		int trackRange = Math.Max(1, contentWidth - thumbWidth);

		if (maxHScroll <= 0) return;

		int deltaX = args.Position.X - _scrollbarDragStartX;
		int newOffset = _scrollbarDragStartOffset + (int)(deltaX * (double)maxHScroll / trackRange);
		_horizontalScrollOffset = Math.Clamp(newOffset, 0, maxHScroll);
		Container?.Invalidate(true);
	}

	#endregion

	#region Column Resizing

	private void BeginColumnResize(MouseEventArgs args)
	{
		for (int c = 0; c < _renderedColumnX.Length; c++)
		{
			int colEnd = _renderedColumnX[c] - ActualX + _renderedColumnWidths[c];
			if (Math.Abs(args.Position.X - colEnd) <= 1)
			{
				_isResizingColumn = true;
				_resizingColumnIndex = c;
				_resizeDragStartX = args.Position.X;
				_resizeDragStartWidth = _renderedColumnWidths[c];
				return;
			}
		}
	}

	private void HandleColumnResizeDrag(MouseEventArgs args)
	{
		int deltaX = args.Position.X - _resizeDragStartX;
		int newWidth = Math.Max(3, _resizeDragStartWidth + deltaX); // Minimum 3 chars

		if (_dataSource != null)
		{
			// For DataSource mode, store resize overrides
			SetColumnWidthOverride(_resizingColumnIndex, newWidth);
		}
		else
		{
			lock (_tableLock)
			{
				if (_resizingColumnIndex >= 0 && _resizingColumnIndex < _columns.Count)
				{
					_columns[_resizingColumnIndex].Width = newWidth;
				}
			}
		}

		InvalidateColumnWidths();
		Container?.Invalidate(true);
	}

	#endregion
}
