// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	public partial class PromptControl
	{
		#region Wrap cache

		// Wrapped rows for the current value at a given width. Keyed on width plus a version bumped by
		// every mutation, so a repaint at an unchanged width and value costs nothing.
		private List<TextWrapping.Segment>? _wrapCache;
		private int _wrapCacheWidth = -1;
		private int _lastWrapWidth = 1;

		/// <summary>The wrap width the last paint or measure used; the mouse maps points with it.</summary>
		private int LastWrapWidth => _lastWrapWidth;

		private void InvalidateWrapCache()
		{
			_wrapCache = null;
			_wrapCacheWidth = -1;
		}

		/// <summary>
		/// The value split into visual rows at <paramref name="width"/> display columns. Newlines split
		/// first, then each logical line soft-wraps through <see cref="TextWrapping"/> — the same
		/// wrapper <see cref="MultilineEditControl"/> uses, so the two cannot disagree about where a
		/// string breaks. Offsets are absolute into the value.
		/// </summary>
		private List<TextWrapping.Segment> GetWrappedRows(int width)
		{
			int safeWidth = Math.Max(1, width);
			if (_wrapCache != null && _wrapCacheWidth == safeWidth)
				return _wrapCache;

			var rows = new List<TextWrapping.Segment>();
			var scratch = new List<TextWrapping.Segment>();

			int lineStart = 0;
			while (true)
			{
				int nl = _multiline ? _input.IndexOf('\n', lineStart) : -1;
				int lineEnd = nl < 0 ? _input.Length : nl;
				string line = _input.Substring(lineStart, lineEnd - lineStart);

				scratch.Clear();
				TextWrapping.WrapWords(scratch, line, safeWidth);
				foreach (var seg in scratch)
					rows.Add(new TextWrapping.Segment(lineStart + seg.Offset, seg.Length));

				if (nl < 0) break;
				lineStart = nl + 1;
			}

			_wrapCache = rows;
			_wrapCacheWidth = safeWidth;
			return rows;
		}

		/// <summary>Index of the visual row containing <paramref name="pos"/>.</summary>
		private int RowIndexFor(List<TextWrapping.Segment> rows, int pos)
		{
			for (int i = rows.Count - 1; i >= 0; i--)
				if (rows[i].Offset <= pos)
					return i;
			return 0;
		}

		#endregion

		#region Cursor geometry

		/// <inheritdoc/>
		public Point? GetLogicalCursorPosition()
		{
			// Only show cursor when control has focus
			if (!HasFocus)
			{
				return null;
			}

			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);

			if (!_multiline)
			{
				// Measured in DISPLAY columns: a value of wide characters advances two cells each, so
				// a character index would put the caret short of where the text actually renders.
				int cursorColumn = UnicodeWidth.CharOffsetToColumn(_input, CurrentCursorPosition);
				int scrollColumn = UnicodeWidth.CharOffsetToColumn(_input, CurrentScrollOffset);
				int visualCursorX = Margin.Left + _lastAlignOffset + promptLength + (cursorColumn - scrollColumn);
				return new Point(visualCursorX, Margin.Top);
			}

			var rows = GetWrappedRows(LastWrapWidth);
			int rowIndex = RowIndexFor(rows, CurrentCursorPosition);
			var row = rows.Count > 0 ? rows[rowIndex] : new TextWrapping.Segment(0, 0);
			string rowText = _input.Substring(row.Offset, row.Length);
			int within = Math.Clamp(CurrentCursorPosition - row.Offset, 0, rowText.Length);
			int column = UnicodeWidth.CharOffsetToColumn(rowText, within);

			return new Point(
				Margin.Left + _lastAlignOffset + promptLength + column,
				Margin.Top + rowIndex - _verticalScrollOffset);
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);

			if (!_multiline)
			{
				string fullContent = (_prompt ?? string.Empty) + _input;
				int singleWidth = Math.Max(Parsing.MarkupParser.StripLength(fullContent), Width ?? 0);
				return new System.Drawing.Size(singleWidth, 1);
			}

			var rows = GetWrappedRows(LastWrapWidth);
			int width = Math.Max(promptLength + LastWrapWidth, Width ?? 0);
			return new System.Drawing.Size(width, Math.Max(1, rows.Count));
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);

			if (!_multiline)
			{
				int column = Math.Max(0, position.X - promptLength);
				int scrollColumn = UnicodeWidth.CharOffsetToColumn(_input, CurrentScrollOffset);
				MoveCursorTo(UnicodeWidth.ColumnToCharOffset(_input, column + scrollColumn));
				Container?.Invalidate(Invalidation.Repaint, this);
				return;
			}

			var rows = GetWrappedRows(LastWrapWidth);
			if (rows.Count == 0) { MoveCursorTo(0); return; }
			int rowIndex = Math.Clamp(position.Y + _verticalScrollOffset, 0, rows.Count - 1);
			var row = rows[rowIndex];
			string rowText = _input.Substring(row.Offset, row.Length);
			int within = UnicodeWidth.ColumnToCharOffset(rowText, Math.Max(0, position.X - promptLength));
			MoveCursorTo(Math.Clamp(row.Offset + within, 0, _input.Length));
			Container?.Invalidate(Invalidation.Repaint, this);
		}

		/// <summary>
		/// Moves the caret one visual row up or down, keeping its display column where it can.
		/// Returns false when there is no such row, which is what lets the arrow keys fall through to
		/// history recall at the top and bottom of the box.
		/// </summary>
		private bool TryMoveCaretVertical(int delta)
		{
			var rows = GetWrappedRows(LastWrapWidth);
			if (rows.Count <= 1) return false;

			int rowIndex = RowIndexFor(rows, _cursorPosition);
			int targetRow = rowIndex + delta;
			if (targetRow < 0 || targetRow >= rows.Count) return false;

			var current = rows[rowIndex];
			string currentText = _input.Substring(current.Offset, current.Length);
			int column = UnicodeWidth.CharOffsetToColumn(currentText, Math.Clamp(_cursorPosition - current.Offset, 0, currentText.Length));

			var target = rows[targetRow];
			string targetText = _input.Substring(target.Offset, target.Length);
			int within = UnicodeWidth.ColumnToCharOffset(targetText, column);

			MoveCursorTo(Math.Clamp(target.Offset + within, 0, _input.Length));
			return true;
		}

		/// <summary>First character index of the visual row containing <paramref name="pos"/>.</summary>
		private int VisualRowStart(int pos)
		{
			var rows = GetWrappedRows(LastWrapWidth);
			if (rows.Count == 0) return 0;
			return rows[RowIndexFor(rows, pos)].Offset;
		}

		/// <summary>Last character index of the visual row containing <paramref name="pos"/>.</summary>
		private int VisualRowEnd(int pos)
		{
			var rows = GetWrappedRows(LastWrapWidth);
			if (rows.Count == 0) return _input.Length;
			var row = rows[RowIndexFor(rows, pos)];
			return Math.Min(row.Offset + row.Length, _input.Length);
		}

		/// <summary>
		/// Scrolls the visible row window so the caret's row is inside it. Only meaningful when the
		/// content is taller than <see cref="MaxRows"/>.
		/// </summary>
		private void EnsureCaretRowVisible()
		{
			var rows = GetWrappedRows(LastWrapWidth);
			if (rows.Count == 0) { _verticalScrollOffset = 0; return; }

			int rowIndex = RowIndexFor(rows, _cursorPosition);
			int visibleRows = Math.Clamp(rows.Count, _minRows, _maxRows);

			if (rowIndex < _verticalScrollOffset)
				_verticalScrollOffset = rowIndex;
			else if (rowIndex >= _verticalScrollOffset + visibleRows)
				_verticalScrollOffset = rowIndex - visibleRows + 1;

			_verticalScrollOffset = Math.Clamp(_verticalScrollOffset, 0, Math.Max(0, rows.Count - visibleRows));
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);
			// Cap measured input width to available space — the control scrolls when text overflows
			int naturalInputWidth = Math.Max(UnicodeWidth.GetStringWidth(_input), 10);
			int inputFieldWidth = _inputWidth ?? Math.Min(naturalInputWidth, Math.Max(10, constraints.MaxWidth - promptLength - Margin.Left - Margin.Right));
			int contentWidth = promptLength + inputFieldWidth;
			int width = (Width ?? contentWidth) + Margin.Left + Margin.Right;

			int rows = 1;
			if (_multiline)
			{
				// Wrap against the width this will actually be painted at, so the measured row count
				// is the row count the paint produces rather than an estimate.
				int available = (Width ?? constraints.MaxWidth) - Margin.Left - Margin.Right - promptLength;
				_lastWrapWidth = Math.Max(1, available);
				rows = Math.Clamp(GetWrappedRows(_lastWrapWidth).Count, _minRows, _maxRows);
			}

			int height = rows + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			var bgColor = Container?.BackgroundColor ?? defaultBg;
			var fgColor = Container?.ForegroundColor ?? defaultFg;
			var effectiveBg = Color.Transparent;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;

			if (targetWidth <= 0) return;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);

			// Calculate colors (role link applied between explicit override and theme default;
			// identical to legacy when ColorRole==Default since the role helpers return null).
			ColorRoleState roleState = CurrentRoleState;
			Color inputBackgroundColor = HasFocus
				? ColorResolver.Coalesce(InputFocusedBackgroundColor)
					?? ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, roleState, mode: ColorRoleMode)
					?? ColorResolver.Coalesce(Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedBackgroundColor)
					?? Color.Transparent
				: ColorResolver.Coalesce(InputBackgroundColor)
					?? ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, roleState, mode: ColorRoleMode)
					?? ColorResolver.Coalesce(Container?.GetConsoleWindowSystem?.Theme?.PromptInputBackgroundColor)
					?? Color.Transparent;
			Color inputForegroundColor = HasFocus
				? InputFocusedForegroundColor
					?? ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, roleState, mode: ColorRoleMode)
					?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedForegroundColor ?? Color.Black
				: InputForegroundColor
					?? ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, roleState, mode: ColorRoleMode)
					?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputForegroundColor ?? Color.White;

			if (_multiline)
				PaintMultiline(buffer, bounds, clipRect, fgColor, bgColor, effectiveBg, inputForegroundColor, inputBackgroundColor, startX, startY, targetWidth);
			else
				PaintSingleLine(buffer, bounds, clipRect, fgColor, bgColor, effectiveBg, inputForegroundColor, inputBackgroundColor, startX, startY, targetWidth);

			// Fill bottom margin
			int rowsPainted = _multiline ? Math.Clamp(GetWrappedRows(LastWrapWidth).Count, _minRows, _maxRows) : 1;
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + rowsPainted, fgColor, effectiveBg);
		}

		/// <summary>
		/// The historical single-row paint: prompt, then a horizontally scrolled slice of the value.
		/// Unchanged in behaviour — this is the path every existing application takes, and the
		/// characterization tests pin it.
		/// </summary>
		private void PaintSingleLine(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color fgColor, Color bgColor, Color effectiveBg, Color inputForegroundColor, Color inputBackgroundColor,
			int startX, int startY, int targetWidth)
		{
			if (startY < clipRect.Y || startY >= clipRect.Bottom || startY >= bounds.Bottom)
				return;

			// Fill left margin
			if (Margin.Left > 0)
			{
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, startY, Margin.Left, 1), fgColor, effectiveBg);
			}

			int currentX = startX;
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);

			// Calculate alignment offset
			int inputFieldWidth = _inputWidth ?? (targetWidth - promptLength);
			_effectiveInputWidth = Math.Max(1, inputFieldWidth); // cache for scroll calculations
			int totalContentWidth = promptLength + inputFieldWidth;
			int alignOffset = 0;
			if (totalContentWidth < targetWidth)
			{
				switch (HorizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - totalContentWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - totalContentWidth;
						break;
				}
			}

			// Cache alignment offset for cursor positioning
			_lastAlignOffset = alignOffset;

			// Fill left alignment padding
			if (alignOffset > 0)
			{
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, startY, alignOffset, 1), fgColor, effectiveBg);
				currentX += alignOffset;
			}

			// Render prompt text (if any)
			if (!string.IsNullOrEmpty(_prompt))
			{
				var promptCells = Parsing.MarkupParser.Parse(_prompt, fgColor, bgColor);
				buffer.WriteCellsClipped(currentX, startY, promptCells, clipRect);
				currentX += promptLength;
			}

			// Calculate visible input with scroll offset
			int scrollOffset = CurrentScrollOffset;
			string visibleInput = _input;
			if (_inputWidth.HasValue && _input.Length > _inputWidth.Value)
			{
				int maxLength = Math.Min(_inputWidth.Value, _input.Length - scrollOffset);
				if (maxLength > 0 && scrollOffset < _input.Length)
				{
					visibleInput = _input.Substring(scrollOffset, maxLength);
				}
				else
				{
					visibleInput = string.Empty;
				}
			}
			else if (scrollOffset > 0 && scrollOffset < _input.Length)
			{
				visibleInput = _input.Substring(scrollOffset);
			}
			else if (scrollOffset >= _input.Length)
			{
				visibleInput = string.Empty;
			}

			// Render input field with background color
			int remainingWidth = bounds.Right - currentX - Margin.Right;
			int inputDisplayWidth = _inputWidth ?? Math.Max(remainingWidth, 0);
			inputDisplayWidth = Math.Min(inputDisplayWidth, remainingWidth);

			bool showPlaceholder = _input.Length == 0 && !string.IsNullOrEmpty(_placeholder);

			// Write the visible input text using Unicode-aware rendering
			// Escape markup in user input to prevent [ ] from being parsed as tags
			string displayInput = showPlaceholder
				? Parsing.MarkupParser.Escape(_placeholder!)
				: _maskCharacter.HasValue
					? new string(_maskCharacter.Value, UnicodeWidth.GetStringWidth(visibleInput))
					: Parsing.MarkupParser.Escape(visibleInput);
			var inputCells = Parsing.MarkupParser.Parse(displayInput, showPlaceholder ? PlaceholderColor(inputForegroundColor) : inputForegroundColor, inputBackgroundColor);
			int visibleDisplayWidth = inputCells.Count;

			// Clamp to inputDisplayWidth and write cells
			int cellsToWrite = Math.Min(visibleDisplayWidth, inputDisplayWidth);
			for (int i = 0; i < cellsToWrite; i++)
			{
				int x = currentX + i;
				if (x >= clipRect.X && x < clipRect.Right)
				{
					buffer.SetCell(x, startY, inputCells[i]);
				}
			}

			// Highlight selection (invert colors)
			if (HasSelection && !showPlaceholder)
			{
				int selStart = Math.Min(_selectionAnchor, _cursorPosition);
				int selEnd = Math.Max(_selectionAnchor, _cursorPosition);
				int visStart = Math.Max(selStart - scrollOffset, 0);
				int visEnd = Math.Min(selEnd - scrollOffset, cellsToWrite);
				for (int i = visStart; i < visEnd; i++)
				{
					int x = currentX + i;
					if (x >= clipRect.X && x < clipRect.Right)
					{
						var cell = buffer.GetCell(x, startY);
						buffer.SetCellColors(x, startY, cell.Background, cell.Foreground);
					}
				}
			}

			// Fill remaining input field with background color
			int inputEndX = currentX + cellsToWrite;
			int fillWidth = inputDisplayWidth - cellsToWrite;
			if (fillWidth > 0 && inputEndX < bounds.Right - Margin.Right)
			{
				for (int i = 0; i < fillWidth; i++)
				{
					int x = inputEndX + i;
					if (x >= clipRect.X && x < clipRect.Right && x < bounds.Right - Margin.Right)
					{
						buffer.SetNarrowCell(x, startY, ' ', inputForegroundColor, inputBackgroundColor);
					}
				}
			}

			// Fill right padding (after input field, before margin)
			int rightPadStart = currentX + inputDisplayWidth;
			int rightPadWidth = bounds.Right - rightPadStart - Margin.Right;
			if (rightPadWidth > 0)
			{
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightPadStart, startY, rightPadWidth, 1), fgColor, effectiveBg);
			}

			// Fill right margin
			if (Margin.Right > 0)
			{
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, startY, Margin.Right, 1), fgColor, effectiveBg);
			}
		}

		/// <summary>
		/// The wrapping paint: the prompt on the first row, and the value laid out under a hanging
		/// indent so every row starts in the same column and the block reads as one field.
		/// </summary>
		private void PaintMultiline(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color fgColor, Color bgColor, Color effectiveBg, Color inputForegroundColor, Color inputBackgroundColor,
			int startX, int startY, int targetWidth)
		{
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);
			_lastAlignOffset = 0;
			_lastWrapWidth = Math.Max(1, targetWidth - promptLength);
			_effectiveInputWidth = _lastWrapWidth;

			var rows = GetWrappedRows(_lastWrapWidth);
			EnsureCaretRowVisible();

			int visibleRows = Math.Clamp(rows.Count, _minRows, _maxRows);
			bool showPlaceholder = _input.Length == 0 && !string.IsNullOrEmpty(_placeholder);

			int selStart = HasSelection ? Math.Min(_selectionAnchor, _cursorPosition) : -1;
			int selEnd = HasSelection ? Math.Max(_selectionAnchor, _cursorPosition) : -1;

			for (int r = 0; r < visibleRows; r++)
			{
				int y = startY + r;
				if (y < clipRect.Y || y >= clipRect.Bottom || y >= bounds.Bottom) continue;

				// Fill left margin for this row
				if (Margin.Left > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, Margin.Left, 1), fgColor, effectiveBg);

				int currentX = startX;

				// The prompt is painted on the first row; later rows are blank in that column so the
				// text keeps a straight left edge under it.
				if (promptLength > 0)
				{
					if (r == 0 && !string.IsNullOrEmpty(_prompt))
					{
						var promptCells = Parsing.MarkupParser.Parse(_prompt, fgColor, bgColor);
						buffer.WriteCellsClipped(currentX, y, promptCells, clipRect);
					}
					else
					{
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(currentX, y, promptLength, 1), fgColor, effectiveBg);
					}
					currentX += promptLength;
				}

				int rowIndex = r + _verticalScrollOffset;
				string rowText;
				int rowOffset;
				if (showPlaceholder && rowIndex == 0)
				{
					rowText = _placeholder!;
					rowOffset = -1; // not part of the value: never selected, never masked
				}
				else if (rowIndex < rows.Count)
				{
					var seg = rows[rowIndex];
					rowOffset = seg.Offset;
					rowText = _input.Substring(seg.Offset, seg.Length);
				}
				else
				{
					rowOffset = -1;
					rowText = string.Empty;
				}

				string display = rowOffset >= 0 && _maskCharacter.HasValue
					? new string(_maskCharacter.Value, UnicodeWidth.GetStringWidth(rowText))
					: Parsing.MarkupParser.Escape(rowText);

				var cells = Parsing.MarkupParser.Parse(
					display,
					rowOffset < 0 && showPlaceholder ? PlaceholderColor(inputForegroundColor) : inputForegroundColor,
					inputBackgroundColor);

				int fieldWidth = Math.Max(0, bounds.Right - currentX - Margin.Right);
				int cellsToWrite = Math.Min(cells.Count, fieldWidth);
				for (int i = 0; i < cellsToWrite; i++)
				{
					int x = currentX + i;
					if (x >= clipRect.X && x < clipRect.Right)
						buffer.SetCell(x, y, cells[i]);
				}

				// Selection highlight, in the row's own coordinates.
				if (rowOffset >= 0 && selStart >= 0)
				{
					int visStart = Math.Max(selStart - rowOffset, 0);
					int visEnd = Math.Min(selEnd - rowOffset, cellsToWrite);
					for (int i = visStart; i < visEnd; i++)
					{
						int x = currentX + i;
						if (x >= clipRect.X && x < clipRect.Right)
						{
							var cell = buffer.GetCell(x, y);
							buffer.SetCellColors(x, y, cell.Background, cell.Foreground);
						}
					}
				}

				// Pad the rest of the row with the field background so the box reads as one surface.
				for (int i = cellsToWrite; i < fieldWidth; i++)
				{
					int x = currentX + i;
					if (x >= clipRect.X && x < clipRect.Right)
						buffer.SetNarrowCell(x, y, ' ', inputForegroundColor, inputBackgroundColor);
				}

				// Fill right margin
				if (Margin.Right > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, y, Margin.Right, 1), fgColor, effectiveBg);
			}
		}

		/// <summary>
		/// The colour placeholder text is drawn in: the field's own foreground pulled halfway to mid
		/// grey, so the hint reads as dimmer than real text against either a light or a dark field
		/// rather than being pinned to one fixed grey that could vanish on one of them.
		/// </summary>
		private static Color PlaceholderColor(Color inputForeground)
			=> new Color(
				(byte)((inputForeground.R + 128) / 2),
				(byte)((inputForeground.G + 128) / 2),
				(byte)((inputForeground.B + 128) / 2));

		#endregion
	}
}
