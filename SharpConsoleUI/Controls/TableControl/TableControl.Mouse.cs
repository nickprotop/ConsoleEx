// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Helpers.Scrollbar;
namespace SharpConsoleUI.Controls;

public partial class TableControl
{
	/// <summary>
	/// Maps a fresh Button1 press position to the sub-region that should own the resulting gesture, in the
	/// same priority order the handler used before capture: vertical scrollbar, then horizontal scrollbar,
	/// then a column border (only when column-resize is enabled and not read-only), else the cells area.
	/// Called ONLY on a fresh press by <see cref="MouseGestureCapture{TRegion}"/>; never re-invoked mid-
	/// gesture, which is what stops a resent-press-on-motion from leaking a resize/cell drag into the wrong
	/// handler when the pointer crosses a different border/scrollbar.
	/// </summary>
	private TableGestureRegion HitTestRegion(MouseEventArgs args)
	{
		if (IsClickOnVerticalScrollbar(args))
			return TableGestureRegion.VScrollbar;

		if (IsClickOnHorizontalScrollbar(args))
			return TableGestureRegion.HScrollbar;

		if (!_readOnly && _columnResizeEnabled && IsClickOnColumnBorder(args))
			return TableGestureRegion.ColumnResize;

		return TableGestureRegion.Cells;
	}

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
				Invalidate(Invalidation.Repaint);
			}
			MouseLeave?.Invoke(this, args);
			return true;
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
					Invalidate(Invalidation.Repaint);
				}
			}

			MouseMove?.Invoke(this, args);
			return true;
		}

		// Right-click: select row/cell first, then fire event. When multi-select is enabled, this also
		// extends the range from the anchor to the clicked row (a Shift+click stand-in, since this
		// terminal does not deliver Shift/Ctrl mouse-click modifiers - see ExtendOrMoveSelection).
		if (args.HasFlag(MouseFlags.Button3Clicked))
		{
			if (_isEditing)
				CancelEdit();
			if (!HasFocus && CanFocusWithMouse)
				this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
			int rowIdx = GetRowIndexAtY(args.Position.Y);
			if (rowIdx >= 0)
			{
				if (_multiSelectEnabled && _rightClickExtendsSelection)
				{
					int anchor = _selectionAnchorRowIndex >= 0 ? _selectionAnchorRowIndex : _selectedRowIndex;
					_selectionAnchorRowIndex = anchor;
					_selectedRowIndices.Clear();
					SelectRange(anchor, rowIdx);
				}
				SetSelectedRow(rowIdx);
				if (_cellNavigationEnabled)
				{
					int colIdx = GetColumnIndexAtX(args.Position.X);
					if (colIdx >= 0)
					{
						_selectedColumnIndex = colIdx;
						Invalidate(Invalidation.Repaint);
					}
				}
			}
			MouseRightClick?.Invoke(this, args);
			if (IsClickOnHeader(args))
			{
				int headerCol = GetColumnIndexAtX(args.Position.X);
				if (headerCol >= 0)
				{
					var log = Container?.GetConsoleWindowSystem?.LogService;
					Core.AsyncEvent.Raise(HeaderRightClicked, HeaderRightClickedAsync, this, headerCol, log);
				}
			}
			return true;
		}

		// Mouse wheel
		if (args.HasFlag(MouseFlags.WheeledUp))
		{
			if (args.HasFlag(MouseFlags.ButtonShift))
			{
				int oldH = _horizontalScrollOffset;
				_horizontalScrollOffset = Math.Max(0, _horizontalScrollOffset - _mouseWheelScrollSpeed);
				if (_horizontalScrollOffset != oldH)
				{
					Invalidate(Invalidation.Relayout);
					return true;
				}
				return false; // bubble to parent
			}

			int oldOffset = _scrollOffset;
			ScrollOffset = Math.Max(0, _scrollOffset - _mouseWheelScrollSpeed);
			return _scrollOffset != oldOffset; // bubble if didn't scroll
		}

		if (args.HasFlag(MouseFlags.WheeledDown))
		{
			if (args.HasFlag(MouseFlags.ButtonShift))
			{
				int oldH = _horizontalScrollOffset;
				_horizontalScrollOffset += _mouseWheelScrollSpeed;
				if (_horizontalScrollOffset != oldH)
				{
					Invalidate(Invalidation.Relayout);
					return true;
				}
				return false; // bubble to parent
			}

			int oldOffset = _scrollOffset;
			int maxOffset = Math.Max(0, RowCount - GetVisibleRowCount());
			ScrollOffset = Math.Min(maxOffset, _scrollOffset + _mouseWheelScrollSpeed);
			return _scrollOffset != oldOffset; // bubble if didn't scroll
		}

		// Double-click (from driver): arrives as its own flag (not Button1Pressed), so it is handled before
		// the gesture router (which only recognises press/drag/release/click). Acts on the cells area.
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
							Invalidate(Invalidation.Relayout);
							MouseDoubleClick?.Invoke(this, args);
							return true;
						}
					}
				}

				Core.AsyncEvent.Raise(RowActivated, RowActivatedAsync, this, rowIdx, Container?.GetConsoleWindowSystem?.LogService);
			}
			MouseDoubleClick?.Invoke(this, args);
			return true;
		}

		// Button1 gesture routing (press / drag / release / click).
		// A fresh press hit-tests one of { VScrollbar, HScrollbar, ColumnResize, Cells } and captures it;
		// every subsequent resent press/drag routes to the captured region WITHOUT re-hit-testing. This is
		// what stops a column-resize drag whose pointer wanders off the border (or a cell drag crossing a
		// column border/scrollbar) from being re-hit-tested into the wrong handler.
		if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Dragged,
			MouseFlags.Button1Released, MouseFlags.Button1Clicked))
		{
			var route = _gesture.Route(args, HitTestRegion);
			if (route.Phase != GesturePhase.None)
			{
				return DispatchGesture(route.Phase, route.Region, args);
			}

			// A bare Button1Clicked with no prior captured press (some drivers/tests deliver a click without a
			// separate Button1Pressed). Synthesize a full click: hit-test the region fresh and dispatch Down
			// then Up, so discrete click actions (header sort, row selection, scrollbar arrow/track) still fire.
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				var region = HitTestRegion(args);
				DispatchGesture(GesturePhase.Down, region, args);
				return DispatchGesture(GesturePhase.Up, region, args);
			}
		}

		// Let scroll events bubble
		return false;
	}

	#region Per-region gesture handlers

	/// <summary>Routes a gesture phase to the handler for the given region.</summary>
	private bool DispatchGesture(GesturePhase phase, TableGestureRegion region, MouseEventArgs args) => region switch
	{
		TableGestureRegion.VScrollbar => HandleVScrollbarGesture(phase, args),
		TableGestureRegion.HScrollbar => HandleHScrollbarGesture(phase, args),
		TableGestureRegion.ColumnResize => HandleColumnResizeGesture(phase, args),
		_ => HandleCellsGesture(phase, args),
	};

	/// <summary>
	/// Vertical scrollbar gesture: Down = arrow / thumb-start / track-page; Move = apply the thumb-drag
	/// delta if a thumb drag was started on Down; Up = end.
	/// </summary>
	private bool HandleVScrollbarGesture(GesturePhase phase, MouseEventArgs args)
	{
		switch (phase)
		{
			case GesturePhase.Down:
				if (_isEditing)
					CancelEdit();
				if (!HasFocus && CanFocusWithMouse)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				_vThumbDragging = false;
				HandleVerticalScrollbarThumbPress(args);
				if (!_vThumbDragging)
					HandleVerticalScrollbarClick(args);
				return true;

			case GesturePhase.Move:
				// Only a thumb-drag (not an arrow/track-page Down) tracks subsequent motion. The captured
				// region keeps the drag glued to the scrollbar even when the pointer leaves the track column.
				if (_vThumbDragging)
					HandleVerticalScrollbarDrag(args);
				return true;

			default: // Up
				_vThumbDragging = false;
				return true;
		}
	}

	/// <summary>
	/// Horizontal scrollbar gesture: Down = arrow / thumb-start / track-page; Move = apply the thumb-drag
	/// delta if a thumb drag was started on Down; Up = end.
	/// </summary>
	private bool HandleHScrollbarGesture(GesturePhase phase, MouseEventArgs args)
	{
		switch (phase)
		{
			case GesturePhase.Down:
				if (_isEditing)
					CancelEdit();
				if (!HasFocus && CanFocusWithMouse)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				_hThumbDragging = false;
				HandleHorizontalScrollbarThumbPress(args);
				if (!_hThumbDragging)
					HandleHorizontalScrollbarClick(args);
				return true;

			case GesturePhase.Move:
				if (_hThumbDragging)
					HandleHorizontalScrollbarDrag(args);
				return true;

			default: // Up
				_hThumbDragging = false;
				return true;
		}
	}

	/// <summary>
	/// Column-resize gesture: Down begins the resize on the pressed border; Move applies the resize delta
	/// (the captured region keeps resizing the ORIGINAL column even if the pointer wanders off the border or
	/// past the next column); Up ends it.
	/// </summary>
	private bool HandleColumnResizeGesture(GesturePhase phase, MouseEventArgs args)
	{
		switch (phase)
		{
			case GesturePhase.Down:
				if (_isEditing)
					CancelEdit();
				if (!HasFocus && CanFocusWithMouse)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				BeginColumnResize(args);
				return true;

			case GesturePhase.Move:
				HandleColumnResizeDrag(args);
				return true;

			default: // Up
				return true;
		}
	}

	/// <summary>
	/// Cells gesture: Down cancels any edit, sets focus and moves the cursor to the pressed row; Up (the
	/// Button1Clicked) performs the discrete click actions (header sort, selection / multi-select toggling,
	/// cell navigation, double-click detection); Move is consumed so a cell drag stays owned by the cells
	/// area even when it crosses a column border or scrollbar.
	/// </summary>
	private bool HandleCellsGesture(GesturePhase phase, MouseEventArgs args)
	{
		if (phase == GesturePhase.Down)
		{
			// Cancel any active cell edit before changing selection
			if (_isEditing)
				CancelEdit();

			// Set focus on click
			if (!HasFocus && CanFocusWithMouse)
				this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

			// Fresh press over a data row: just move the cursor, don't touch multi-select state
			// (multi-select toggling happens on the Button1Clicked / Up phase to avoid double-toggle).
			// EXCEPTION: a Shift-click's range (Up phase below) is anchored on whatever _selectedRowIndex
			// was BEFORE this click - moving it here first would make every range collapse to just the
			// pressed row (anchor == target).
			int pressRow = GetRowIndexAtY(args.Position.Y);

			// A plain (no Ctrl/Shift) press on a data row seeds a click-and-drag range select - the mouse-only
			// stand-in for Shift+Click, since terminals routinely swallow Shift+Click for native text selection.
			_isRowDragSelecting = _multiSelectEnabled && pressRow >= 0 && !IsClickOnHeader(args)
				&& !args.HasFlag(MouseFlags.ButtonShift) && !args.HasFlag(MouseFlags.ButtonCtrl)
				&& !(_checkboxMode && IsClickOnCheckbox(args.Position.X));

			// A Ctrl-held press on a data row seeds a Ctrl+drag: additional ranges added on top of whatever
			// was already selected, instead of the plain drag's "replace the whole selection" behavior.
			bool isCtrlDragSelecting = _multiSelectEnabled && pressRow >= 0 && !IsClickOnHeader(args)
				&& args.HasFlag(MouseFlags.ButtonCtrl) && !args.HasFlag(MouseFlags.ButtonShift)
				&& !(_checkboxMode && IsClickOnCheckbox(args.Position.X));
			_ctrlDragBaseSelection = isCtrlDragSelecting ? new HashSet<int>(_selectedRowIndices) : null;

			if (_isRowDragSelecting || isCtrlDragSelecting)
				_selectionAnchorRowIndex = pressRow;

			if (pressRow >= 0 && !(_multiSelectEnabled && args.HasFlag(MouseFlags.ButtonShift)))
				SetSelectedRow(pressRow);
			return true;
		}

		if (phase == GesturePhase.Move)
		{
			// A cell drag stays owned by the cells area. A plain-press drag seeded above extends the
			// multi-select range live to the row under the pointer.
			if (_isRowDragSelecting)
			{
				int dragRowIdx = GetRowIndexAtY(args.Position.Y);
				if (dragRowIdx >= 0)
				{
					_selectedRowIndices.Clear();
					SelectRange(_selectionAnchorRowIndex, dragRowIdx);
					SetSelectedRow(dragRowIdx);
				}
			}
			else if (_ctrlDragBaseSelection != null)
			{
				// Recompute from the pre-drag snapshot each time (rather than accumulating) so backtracking
				// the pointer past the anchor correctly drops rows the live preview had added.
				int dragRowIdx = GetRowIndexAtY(args.Position.Y);
				if (dragRowIdx >= 0)
				{
					_selectedRowIndices.Clear();
					_selectedRowIndices.UnionWith(_ctrlDragBaseSelection);
					SelectRange(_selectionAnchorRowIndex, dragRowIdx);
					SetSelectedRow(dragRowIdx);
				}
			}
			return true;
		}

		// Up phase: the Button1Clicked discrete actions. If the release did not carry Button1Clicked
		// (e.g. a bare Button1Released ending a press) there is nothing further to do.
		// Captured once here (rather than read from the field below) so every exit path from this phase -
		// header sort, empty-area click, or a normal row click - consistently ends the drag-select gesture.
		bool wasDragSelecting = _isRowDragSelecting;
		HashSet<int>? ctrlDragBase = _ctrlDragBaseSelection;
		_isRowDragSelecting = false;
		_ctrlDragBaseSelection = null;
		if (!args.HasFlag(MouseFlags.Button1Clicked))
			return true;

		// Header sort
		if (IsClickOnHeader(args))
		{
			int headerColIdx = GetColumnIndexAtX(args.Position.X);
			if (headerColIdx >= 0 && _sortingEnabled)
				SortByColumn(headerColIdx);
			MouseClick?.Invoke(this, args);
			if (headerColIdx >= 0)
			{
				var log = Container?.GetConsoleWindowSystem?.LogService;
				Core.AsyncEvent.Raise(HeaderClicked, HeaderClickedAsync, this, headerColIdx, log);
			}
			return true;
		}

		// Click on data row
		int rowIdx = GetRowIndexAtY(args.Position.Y);
		if (rowIdx < 0 && _clearSelectionOnEmptyClick)
		{
			// Empty data area click: clear selection entirely.
			if (_multiSelectEnabled)
				_selectedRowIndices.Clear();
			SetSelectedRow(-1);
			MouseClick?.Invoke(this, args);
			return true;
		}
		if (rowIdx >= 0)
		{
			if (_multiSelectEnabled && args.HasFlag(MouseFlags.ButtonCtrl))
			{
				// A completed Ctrl+drag (anchor differs from the release row) adds the whole dragged range
				// on top of whatever was selected before the drag; a plain Ctrl+click (no movement) keeps the
				// original single-row toggle behavior.
				if (ctrlDragBase != null && _selectionAnchorRowIndex != rowIdx)
				{
					_selectedRowIndices.Clear();
					_selectedRowIndices.UnionWith(ctrlDragBase);
					SelectRange(_selectionAnchorRowIndex, rowIdx);
				}
				else
				{
					ToggleRowSelection(rowIdx);
				}
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
					// A click-and-drag range select (seeded on Down, extended on Move) finalizes here using its
					// original press-row anchor, so a completed drag isn't collapsed back down to the single
					// release row. A plain click (no drag) has anchor == rowIdx, so this degrades to the same
					// single-row selection as before.
					int anchor = wasDragSelecting && _selectionAnchorRowIndex >= 0 ? _selectionAnchorRowIndex : rowIdx;
					_selectedRowIndices.Clear();
					SelectRange(anchor, rowIdx);
					// Re-anchor keyboard Shift+Up/Down range-select (see TableControl.Keyboard.cs) here too,
					// so it starts fresh from wherever was just plain-clicked/dragged.
					_selectionAnchorRowIndex = rowIdx;
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
					Invalidate(Invalidation.Repaint);
				}
			}

			// Double-click detection
			lock (_clickLock)
			{
				var now = DateTime.Now;
				if (_lastClickRowIndex == rowIdx &&
					(now - _lastClickTime).TotalMilliseconds < _doubleClickThresholdMs)
				{
					Core.AsyncEvent.Raise(RowActivated, RowActivatedAsync, this, rowIdx, Container?.GetConsoleWindowSystem?.LogService);
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

	#endregion

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
		// Checkbox is a silent column rendered before the first data column
		bool hasBorder = _borderStyle != BorderStyle.None;
		int checkboxStart = _renderedColumnX[0] - ActualX - 4 - (hasBorder ? 1 : 0);
		int checkboxEnd = checkboxStart + 4;
		return relativeX >= checkboxStart && relativeX < checkboxEnd;
	}

	private bool IsClickOnHeader(MouseEventArgs args) => IsOnHeaderRow(args.Position.Y);

	private bool IsOnHeaderRow(int y)
	{
		if (!_showHeader) return false;
		int headerY = Margin.Top;
		if (!string.IsNullOrEmpty(_title)) headerY++;
		if (_borderStyle != BorderStyle.None) headerY++;
		return y == headerY;
	}

	/// <summary>The column index at display X (accounting for horizontal scroll), or -1 if X is past the
	/// last column. Lets a consumer resolve the column for a click handled via MouseClick/MouseRightClick.</summary>
	public int GetColumnIndexAt(int x) => GetColumnIndexAtX(x);

	/// <summary>True if (x, y) is on the header row.</summary>
	public bool IsOnHeader(int x, int y) => IsOnHeaderRow(y);

	/// <summary>Returns the Y coordinate of the header row, for unit-testing header hit detection.</summary>
	internal int HeaderRowYForTest()
	{
		int hy = Margin.Top;
		if (!string.IsNullOrEmpty(_title)) hy++;
		if (_borderStyle != BorderStyle.None) hy++;
		return hy;
	}

	/// <summary>Returns the Y coordinate of the given display row index, for unit-testing row hit detection.</summary>
	internal int RowYForTest(int displayIndex)
	{
		int dataStartY = Margin.Top;
		if (!string.IsNullOrEmpty(_title)) dataStartY++;
		if (_borderStyle != BorderStyle.None) dataStartY++;
		if (_showHeader) dataStartY++;
		if (_showHeader && _borderStyle != BorderStyle.None) dataStartY++;

		int effectiveRowIndex = displayIndex - _scrollOffset;
		int rowOffset = (_showRowSeparators && _borderStyle != BorderStyle.None) ? effectiveRowIndex * 2 : effectiveRowIndex;
		return dataStartY + rowOffset;
	}

	private bool IsClickOnVerticalScrollbar(MouseEventArgs args)
	{
		if (!ShouldShowVerticalScrollbar()) return false;
		var (x, _, _) = GetVerticalScrollbarRect();
		return args.Position.X == x;
	}

	private bool IsClickOnHorizontalScrollbar(MouseEventArgs args)
	{
		if (!ShouldShowHorizontalScrollbar()) return false;
		var (_, y, _) = GetHorizontalScrollbarRect();
		return args.Position.Y == y;
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

	private void HandleVerticalScrollbarThumbPress(MouseEventArgs args)
	{
		var (_, rectY, rectHeight) = GetVerticalScrollbarRect();
		var metrics = VerticalScrollbarMetrics(rectHeight);
		int relY = args.Position.Y - rectY;

		if (ScrollbarInput.HitTest(metrics, relY) == ScrollbarHitZone.Thumb)
		{
			_vThumbDragging = true;
			_scrollbarDragStartY = args.Position.Y;
			_scrollbarDragStartThumbPos = ScrollbarGeometry.ThumbPosForOffset(metrics);
		}
	}

	private void HandleVerticalScrollbarClick(MouseEventArgs args)
	{
		var (_, rectY, rectHeight) = GetVerticalScrollbarRect();
		var metrics = VerticalScrollbarMetrics(rectHeight);
		int relY = args.Position.Y - rectY;

		var zone = ScrollbarInput.HitTest(metrics, relY);

		// Arrow/track clicks are gated on scrollability, exactly as before: an arrow at an
		// already-exhausted end, or track on the side with nothing left to page, does nothing.
		bool canApply = zone switch
		{
			ScrollbarHitZone.UpArrow => _scrollOffset > 0,
			ScrollbarHitZone.DownArrow => _scrollOffset < ScrollbarGeometry.MaxOffset(metrics),
			ScrollbarHitZone.TrackAbove => true,
			ScrollbarHitZone.TrackBelow => true,
			_ => false,
		};

		if (canApply)
		{
			// Table's arrow step has always been a literal 1 row (not DefaultScrollWheelLines, which
			// only governs the wheel handlers) — preserved here.
			int newOffset = ScrollbarInput.OffsetForZone(metrics, zone, smallChange: 1, largeChange: GetVisibleRowCount());

			// Route through the property (not the field) so a click participates in the AutoScroll
			// detach/re-attach rule like every other user scroll. The setter clamps and invalidates.
			ScrollOffset = newOffset;
		}
	}

	private void HandleVerticalScrollbarDrag(MouseEventArgs args)
	{
		var (_, _, rectHeight) = GetVerticalScrollbarRect();
		var metrics = VerticalScrollbarMetrics(rectHeight);

		if (ScrollbarGeometry.MaxOffset(metrics) <= 0) return;

		int delta = args.Position.Y - _scrollbarDragStartY;
		int newOffset = ScrollbarInput.OffsetForDrag(metrics, _scrollbarDragStartThumbPos, delta);

		// Route through the property (not the field) so the drag participates in the AutoScroll
		// detach/re-attach rule like every other user scroll. The setter clamps and invalidates.
		ScrollOffset = newOffset;
	}

	private void HandleHorizontalScrollbarThumbPress(MouseEventArgs args)
	{
		var (rectX, _, rectWidth) = GetHorizontalScrollbarRect();
		int totalColumnsWidth = GetTotalColumnsWidth();
		var metrics = HorizontalScrollbarMetrics(rectWidth, totalColumnsWidth);
		int relX = args.Position.X - rectX;

		if (ScrollbarInput.HitTest(metrics, relX) == ScrollbarHitZone.Thumb)
		{
			_hThumbDragging = true;
			_scrollbarDragStartX = args.Position.X;
			_hScrollbarDragStartThumbPos = ScrollbarGeometry.ThumbPosForOffset(metrics);
		}
	}

	private void HandleHorizontalScrollbarClick(MouseEventArgs args)
	{
		var (rectX, _, rectWidth) = GetHorizontalScrollbarRect();
		int totalColumnsWidth = GetTotalColumnsWidth();
		var metrics = HorizontalScrollbarMetrics(rectWidth, totalColumnsWidth);
		int relX = args.Position.X - rectX;
		int maxHScroll = ScrollbarGeometry.MaxOffset(metrics);

		var zone = ScrollbarInput.HitTest(metrics, relX);

		bool canApply = zone switch
		{
			ScrollbarHitZone.UpArrow => _horizontalScrollOffset > 0,
			ScrollbarHitZone.DownArrow => _horizontalScrollOffset < maxHScroll,
			ScrollbarHitZone.TrackAbove => true,
			ScrollbarHitZone.TrackBelow => true,
			_ => false,
		};

		if (canApply)
		{
			// Table's arrow step has always been a literal 1 column - preserved here.
			_horizontalScrollOffset = ScrollbarInput.OffsetForZone(metrics, zone, smallChange: 1, largeChange: rectWidth);
		}

		Invalidate(Invalidation.Relayout);
	}

	private void HandleHorizontalScrollbarDrag(MouseEventArgs args)
	{
		var (_, _, rectWidth) = GetHorizontalScrollbarRect();
		int totalColumnsWidth = GetTotalColumnsWidth();
		var metrics = HorizontalScrollbarMetrics(rectWidth, totalColumnsWidth);

		int maxHScroll = ScrollbarGeometry.MaxOffset(metrics);
		if (maxHScroll <= 0) return;

		int delta = args.Position.X - _scrollbarDragStartX;
		int newOffset = ScrollbarInput.OffsetForDrag(metrics, _hScrollbarDragStartThumbPos, delta);
		_horizontalScrollOffset = Math.Clamp(newOffset, 0, maxHScroll);
		Invalidate(Invalidation.Relayout);
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
		Invalidate(Invalidation.Relayout);
	}

	#endregion
}
