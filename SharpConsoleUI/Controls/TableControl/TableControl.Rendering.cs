// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

public partial class TableControl
{
	#region Cell Prefixes

	/// <summary>
	/// Markup to draw at the left edge of a cell, in front of its value, or null for none.
	/// </summary>
	/// <param name="dataRowIndex">The data row, as <see cref="GetRow"/> counts rows (or the data source's row).</param>
	/// <param name="columnIndex">The data column; the checkbox column is never asked about.</param>
	/// <returns>The prefix's markup, or null.</returns>
	/// <remarks>
	/// <para>
	/// FOR WHAT A CELL SHOWS BUT DOES NOT CONTAIN: a tree's guide lines and expander in front of a
	/// name, a status glyph in front of a value. Writing such things into the cell text would make
	/// them part of the value — filtered, sorted, edited and copied — and would shift the value's
	/// alignment with every level of indentation.
	/// </para>
	/// <para>
	/// The prefix takes the cell's first cells and the value is laid out in what is left: alignment,
	/// truncation, the truncation fade, filter-match highlighting and inline editing all apply to the
	/// value alone. A prefix wider than the cell is cut at the cell's edge, never a glyph in half.
	/// Auto-width columns count it, so it does not push the value out of view. A selected or hovered
	/// row draws it in the row's colours, as it draws the value; otherwise the markup's own colours
	/// apply. <see cref="HitTest"/> counts a cell's offset from the cell's left edge, so a click on the
	/// prefix can be told from a click on the value.
	/// </para>
	/// <para>
	/// WHY MARKUP AND NOT A PAINT CALLBACK. Clipping, horizontal scrolling and wide characters stay the
	/// table's business, done the one way it does them for every cell.
	/// </para>
	/// <para>
	/// Called for the data rows being painted and those sampled for auto width, on the UI thread and
	/// never while <see cref="SyncRoot"/> is held. A prefix that changes width without the rows
	/// changing needs <see cref="InvalidateColumnWidths"/> to be re-measured.
	/// </para>
	/// </remarks>
	protected virtual string? GetCellPrefixMarkup(int dataRowIndex, int columnIndex) => null;

	/// <summary>
	/// The prefix for the lines of a wrapping row AFTER its first, where
	/// <see cref="GetCellPrefixMarkup"/> gives the first line's.
	/// </summary>
	/// <param name="dataRowIndex">The row, in data order.</param>
	/// <param name="columnIndex">The data column.</param>
	/// <returns>Markup of the same display width as the row's own prefix, or null for none.</returns>
	/// <remarks>
	/// <para>
	/// The default returns null, so a prefix appears on the row's first line only. That is right
	/// for a prefix that marks the row, like a badge or a bullet, and wrong for one that draws
	/// structure: a tree's guides have to carry down a tall row or the hierarchy stops reading
	/// halfway through it.
	/// </para>
	/// <para>
	/// Whatever this returns must be the same display width as
	/// <see cref="GetCellPrefixMarkup"/> returned for the same row and column, or the value
	/// column will start at a different x on the continuation lines.
	/// </para>
	/// </remarks>
	protected virtual string? GetCellPrefixContinuationMarkup(int dataRowIndex, int columnIndex) => null;

	/// <summary>The checkbox cell's text for one row.</summary>
	/// <param name="row">The row being painted, or null over a data source.</param>
	/// <param name="isChecked">Whether the row reads as checked.</param>
	/// <returns>
	/// Text exactly <see cref="Configuration.ControlDefaults.TableCheckboxColumnWidth"/> wide. The
	/// checkbox column is a fixed width, so anything else shifts every column after it.
	/// </returns>
	/// <remarks>
	/// Called on the paint path, which reads a row snapshot so another thread can paint a
	/// consistent frame. An override must only read.
	/// </remarks>
	protected virtual string GetCheckboxMarkup(TableRow? row, bool isChecked) =>
		isChecked
			? Configuration.ControlDefaults.TableCheckboxChecked
			: Configuration.ControlDefaults.TableCheckboxUnchecked;

	/// <summary>
	/// The prefixes of one row's cells, indexed as the painter indexes columns (the checkbox column
	/// first, in checkbox mode), or null when the row has none.
	/// </summary>
	/// <param name="dataRowIndex">The row, in data order.</param>
	/// <param name="dataColumnCount">How many data columns the row has.</param>
	/// <param name="paintedColumnCount">How many columns the painter draws, checkbox included.</param>
	/// <param name="line">
	/// Which line of a wrapping row is being drawn. 0 is the row's own line and gets the prefix a
	/// derived table supplies; a later line gets its continuation, so a tree's guides carry down a
	/// tall row instead of stopping after its first line.
	/// </param>
	private string?[]? GetCellPrefixes(int dataRowIndex, int dataColumnCount, int paintedColumnCount, int line = 0)
	{
		string?[]? prefixes = null;
		int shift = paintedColumnCount - dataColumnCount;
		for (int c = 0; c < dataColumnCount; c++)
		{
			string? prefix = line == 0
				? GetCellPrefixMarkup(dataRowIndex, c)
				: GetCellPrefixContinuationMarkup(dataRowIndex, c);
			if (string.IsNullOrEmpty(prefix)) continue;

			prefixes ??= new string?[paintedColumnCount];
			prefixes[c + shift] = prefix;
		}
		return prefixes;
	}

	/// <summary>
	/// One row's cells wrapped to their columns, so each painted line can be drawn by the same
	/// row painter the single-line table has always used.
	/// </summary>
	/// <remarks>
	/// Deliberately a string per cell per line rather than pre-parsed cells: that is what
	/// <see cref="TableRowPainter.DrawDataRow"/> already takes, so every truncation, wide-glyph,
	/// alignment and fade path in it keeps working untouched. Wrapping produces cells, and this
	/// turns them back into text; the alternative was a second copy of that painter for tall
	/// rows, which would drift from the original on the first bug fix to either.
	/// </remarks>
	private sealed class WrappedRow
	{
		private readonly string[][] _linesPerColumn;
		private readonly VerticalAlignment[] _align;
		private readonly IList<string> _original;

		internal WrappedRow(string[][] linesPerColumn, VerticalAlignment[] align, IList<string> original)
		{
			_linesPerColumn = linesPerColumn;
			_align = align;
			_original = original;
		}

		/// <summary>What each column shows on one line of the row.</summary>
		internal IList<string> LineCells(int line, int rowHeight)
		{
			var cells = new string[_linesPerColumn.Length];
			for (int c = 0; c < _linesPerColumn.Length; c++)
			{
				string[] lines = _linesPerColumn[c];
				int index = line - VerticalOffset(_align[c], rowHeight, lines.Length);
				cells[c] = index >= 0 && index < lines.Length ? lines[index] : string.Empty;
			}
			return cells;
		}

		/// <summary>
		/// The line of the row a cell's first line goes on, given where the column wants to sit.
		/// </summary>
		/// <remarks>
		/// Fill reads as Top: a cell's text cannot stretch to fill a height, and refusing a valid
		/// enum value would be worse than giving it the sensible reading. A cell as tall as the
		/// row gets offset 0 whatever the alignment, since there is nowhere to move it to.
		/// </remarks>
		private static int VerticalOffset(VerticalAlignment align, int rowHeight, int cellLines)
		{
			int slack = rowHeight - cellLines;
			if (slack <= 0) return 0;

			return align switch
			{
				VerticalAlignment.Center => slack / 2,
				VerticalAlignment.Bottom => slack,
				_ => 0,
			};
		}

		/// <summary>The row's untouched cells, for the single-line path.</summary>
		internal IList<string> Original => _original;
	}

	/// <summary>
	/// Wraps a row's cells and reports how many lines it needs, or null when no column wraps.
	/// </summary>
	/// <remarks>
	/// Returning null rather than a height-of-one WrappedRow is what keeps the non-wrapping table
	/// on the code path it has always used: the caller passes the row's own cells straight to the
	/// painter, with no wrap, no allocation and no per-cell round trip through the parser.
	/// </remarks>
	private WrappedRow? BuildWrappedRow(IList<string> rowCells, int[] colWidths,
		List<TableColumn>? cols, Color fg, Color bg, int editCellIndex, bool wrapsAnything,
		out int rowHeight)
	{
		rowHeight = 1;
		if (cols == null || !wrapsAnything) return null;

		var linesPerColumn = new string[colWidths.Length][];
		var align = new VerticalAlignment[colWidths.Length];
		bool anyWrapped = false;

		for (int c = 0; c < colWidths.Length; c++)
		{
			string text = c < rowCells.Count ? rowCells[c] : string.Empty;
			var col = c < cols.Count ? cols[c] : null;
			align[c] = col?.VerticalAlignment ?? CellVerticalAlignment;

			// An editing cell stays one line: the caret moves through a single run of text, and
			// wrapping it would move the caret to a line the cursor arithmetic knows nothing of.
			if (col?.Wrap != true || c == editCellIndex)
			{
				linesPerColumn[c] = new[] { text };
				continue;
			}

			var wrapped = TableCellWrapper.WrapCell(text, colWidths[c], col.MaxLines, fg, bg);
			linesPerColumn[c] = wrapped
				.Select(line => Parsing.MarkupParser.CellsToMarkup(line, fg, bg))
				.ToArray();
			if (wrapped.Count > 1) anyWrapped = true;
			if (wrapped.Count > rowHeight) rowHeight = wrapped.Count;
		}

		// Every column fitted on one line, so this row is an ordinary row. Hand back null and let
		// it paint through the original path with its own cells.
		if (!anyWrapped)
		{
			rowHeight = 1;
			return null;
		}

		return new WrappedRow(linesPerColumn, align, rowCells);
	}


	#endregion

	#region IDOMPaintable Implementation

	/// <inheritdoc/>
	public override LayoutSize MeasureDOM(LayoutConstraints constraints)
	{
		int colCount;
		List<TableColumn>? colSnapshot = null;
		List<TableRow>? rowSnapshot = null;

		if (_dataSource != null)
		{
			colCount = _dataSource.ColumnCount;
		}
		else
		{
			lock (_tableLock)
			{
				colSnapshot = _columns.ToList();
				rowSnapshot = _rows.ToList();
				colCount = colSnapshot.Count;
			}
		}

		int targetWidth = Width ?? constraints.MaxWidth;
		int contentWidth = targetWidth - Margin.Left - Margin.Right;

		// Reserve space for vertical scrollbar (+ optional gutter before it)
		if (ShouldShowVerticalScrollbar())
			contentWidth = Math.Max(1, contentWidth - 1 - ScrollbarGutterWidth);

		// Reserve space for checkbox column
		int cbWidth = _checkboxMode ? ControlDefaults.TableCheckboxColumnWidth : 0;
		bool hasBorder = _borderStyle != BorderStyle.None;
		int dataContentWidth2 = contentWidth;
		if (_checkboxMode)
			dataContentWidth2 = Math.Max(1, contentWidth - cbWidth - (hasBorder ? 1 : 0));

		int[] colWidths;
		if (_dataSource != null)
			colWidths = ComputeColumnWidthsFromDataSource(dataContentWidth2, _scrollOffset, GetVisibleRowCount());
		else
			colWidths = ComputeColumnWidths(dataContentWidth2, colSnapshot!, rowSnapshot!, _scrollOffset, GetVisibleRowCount(), _rowView.Map);

		int borderOverhead = hasBorder ? (colCount + 1)
			: (_columnSeparator.HasValue ? Math.Max(0, colCount - 1) * SeparatorWidth : 0);
		int measuredWidth = cbWidth + (cbWidth > 0 && hasBorder ? 1 : 0);
		foreach (int w in colWidths) measuredWidth += w;
		measuredWidth += borderOverhead;

		// Add scrollbar width (+ optional gutter before it)
		if (ShouldShowVerticalScrollbar())
			measuredWidth += 1 + ScrollbarGutterWidth;

		if (!string.IsNullOrEmpty(_title))
		{
			int titleWidth = _measurementCache.GetCachedLength(_title);
			if (titleWidth > measuredWidth)
				measuredWidth = titleWidth;
		}

		// Calculate height
		int rowCount = RowCount;
		int height = 0;
		if (!string.IsNullOrEmpty(_title)) height++;
		if (hasBorder) height++; // top border
		if (_showHeader) height++;
		if (_showHeader && hasBorder) height++; // header separator

		// Determine visible rows: explicit height > constraint-based > all rows
		int visibleRows;
		if (_height.HasValue)
		{
			visibleRows = CalculateVisibleRowsFromHeight(_height.Value);
		}
		else if (!_readOnly && constraints.MaxHeight < int.MaxValue)
		{
			// Interactive table: respect container constraint so internal scrolling works
			visibleRows = Math.Min(rowCount, CalculateVisibleRowsFromHeight(constraints.MaxHeight));
		}
		else
		{
			visibleRows = rowCount;
		}
		// A wrapping table has to ask for the lines it will DRAW, not the rows it holds. A host
		// that reserved one line per row would give a five-row table five lines and then watch it
		// paint twenty, so a ScrollablePanel could never scroll to its end.
		//
		// Measured at the widths THIS measure just computed rather than the last paint's: on the
		// first measure there is no last paint, and on a reflow the old widths are exactly the
		// ones that no longer apply.
		if (HasWrappingColumns && colSnapshot != null && rowSnapshot != null)
		{
			int shown = Math.Min(rowCount, visibleRows);
			for (int displayR = 0; displayR < shown; displayR++)
			{
				int dataR = MapDisplayToData(displayR);
				if (dataR < 0 || dataR >= rowSnapshot.Count) { height++; continue; }

				// Keyed by DATA index, as GetRowHeight is, so the two agree and a remapped
				// display order cannot hand back the height of a different row.
				int rowCopy = dataR;
				height += _rowHeights.GetHeight(dataR, colWidths,
					_ => rowSnapshot[rowCopy].Cells, colSnapshot);
			}
		}
		else
		{
			height += Math.Min(rowCount, visibleRows);
		}
		if (_showRowSeparators && hasBorder)
		{
			int visibleDataRows = Math.Min(rowCount, visibleRows);
			if (visibleDataRows > 1) height += visibleDataRows - 1;
		}

		// Filter status bar
		if (_filteringEnabled && !_readOnly)
			height += 2; // separator + status row

		if (hasBorder) height++; // bottom border

		// Horizontal scrollbar
		if (ShouldShowHorizontalScrollbar())
			height++;

		int width;
		if (Width.HasValue)
			width = Width.Value + Margin.Left + Margin.Right;
		else if (HorizontalAlignment == HorizontalAlignment.Stretch)
			width = constraints.MaxWidth;
		else
			width = measuredWidth + Margin.Left + Margin.Right;

		height += Margin.Top + Margin.Bottom;

		// VerticalAlignment.Fill: take all offered vertical space (then scroll internally
		// for any overflow) instead of capping at content size. Only applies when a bounded
		// height is offered and no explicit Height was set — content-sized tables and tables
		// with an explicit Height keep their existing behavior.
		if (VerticalAlignment == VerticalAlignment.Fill
			&& !_height.HasValue
			&& constraints.MaxHeight < int.MaxValue
			&& height < constraints.MaxHeight)
		{
			height = constraints.MaxHeight;
		}

		return new LayoutSize(
			Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
			Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
		);
	}

	/// <inheritdoc/>
	public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
	{
		SetActualBounds(bounds);

		Color bgColor = ResolveBackgroundColor(defaultBg);
		Color fgColor = ResolveForegroundColor(defaultFg);
		Color borderColor = ResolveBorderColor();
		Color headerBg = ResolveHeaderBackgroundColor();
		Color headerFg = ResolveHeaderForegroundColor();
		var effectiveBg = (_backgroundColorValue == null || _backgroundColorValue == Color.Default) ? Color.Transparent : bgColor;

		// Fill margins
		ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, bounds.Y + Margin.Top, fgColor, effectiveBg);

		int colCount;
		List<TableColumn>? colSnapshot = null;
		List<TableRow>? rowSnapshot = null;

		if (_dataSource != null)
		{
			colCount = _dataSource.ColumnCount;
		}
		else
		{
			lock (_tableLock)
			{
				colSnapshot = _columns.ToList();
				rowSnapshot = _rows.ToList();
				colCount = colSnapshot.Count;
			}
		}

		int targetWidth = bounds.Width - Margin.Left - Margin.Right;

		// Determine scrollbar visibility
		bool showVScrollbar = ShouldShowVerticalScrollbar();
		int contentWidth = targetWidth;
		if (showVScrollbar)
			contentWidth = Math.Max(1, contentWidth - 1);

		if (contentWidth <= 0 || colCount == 0)
		{
			_geometry = null;
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, effectiveBg);
			return;
		}

		// Optional gutter between the column content and the scrollbar: the columns are laid out into
		// columnContentWidth (one cell narrower), leaving the cell at startX + columnContentWidth blank,
		// while the scrollbar stays at startX + contentWidth on the far right.
		int scrollbarGutter = ScrollbarGutterWidth;
		int columnContentWidth = Math.Max(1, contentWidth - scrollbarGutter);

		// Reserve space for checkbox column before computing data column widths
		int checkboxColWidth = _checkboxMode ? ControlDefaults.TableCheckboxColumnWidth : 0;
		int dataContentWidth = columnContentWidth;
		if (_checkboxMode)
		{
			bool hasBorderForCb = _borderStyle != BorderStyle.None;
			dataContentWidth = Math.Max(1, columnContentWidth - checkboxColWidth - (hasBorderForCb ? 1 : 0));
		}

		int[] dataColWidths;
		if (_dataSource != null)
			dataColWidths = ComputeColumnWidthsFromDataSource(dataContentWidth, _scrollOffset, GetVisibleRowCount());
		else
			dataColWidths = ComputeColumnWidths(dataContentWidth, colSnapshot!, rowSnapshot!, _scrollOffset, GetVisibleRowCount(), _rowView.Map);

		// Prepend silent checkbox column to colWidths so all drawing infrastructure handles it
		int[] colWidths;
		if (_checkboxMode)
		{
			colWidths = new int[dataColWidths.Length + 1];
			colWidths[0] = checkboxColWidth;
			Array.Copy(dataColWidths, 0, colWidths, 1, dataColWidths.Length);
		}
		else
		{
			colWidths = dataColWidths;
		}

		int totalColumnsWidth = GetTotalColumnsWidth(colWidths);
		bool showHScrollbar = ShouldShowHorizontalScrollbar(totalColumnsWidth, contentWidth);

		// Clamp locally rather than mutating _horizontalScrollOffset here - the field is authoritatively
		// clamped by the mouse drag/wheel handlers; this just guards against a stale offset from before
		// a resize/data change made the content narrower than it used to be.
		int maxHScroll = Math.Max(0, totalColumnsWidth - contentWidth);
		int effectiveHScroll = Math.Min(_horizontalScrollOffset, maxHScroll);

		int startX = bounds.X + Margin.Left;
		int currentY = bounds.Y + Margin.Top;
		int maxY = bounds.Bottom - Margin.Bottom;
		if (showHScrollbar) maxY--; // reserve row for horizontal scrollbar

		bool hasBorder = _borderStyle != BorderStyle.None;
		var box = GetBoxChars();
		var style = new TableRowStyle(hasBorder, box, borderColor, effectiveBg,
			_columnSeparator, _columnSeparatorColor, _columnSeparatorPadded, _checkboxMode, _truncationFade);

		// Selection colors
		Color selBg = ResolveSelectionBackgroundColor();
		Color selFg = ResolveSelectionForegroundColor();
		Color unfocusedSelBg = ResolveUnfocusedSelectionBackgroundColor();
		Color unfocusedSelFg = ResolveUnfocusedSelectionForegroundColor();
		Color hoverBg = ResolveHoverBackgroundColor();
		Color hoverFg = ResolveHoverForegroundColor();

		void FillSideMargins(int y)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;
			if (Margin.Left > 0)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, Margin.Left, 1), fgColor, effectiveBg);
			if (Margin.Right > 0)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, y, Margin.Right, 1), fgColor, effectiveBg);
		}

		// The lines each part is drawn on, control-relative, recorded for hit testing (-1 = not drawn)
		int titleLine = -1;
		int headerLine = -1;
		int filterBarTop = -1;
		int filterBarBottom = -1;

		// Title row
		if (!string.IsNullOrEmpty(_title) && currentY < maxY)
		{
			titleLine = currentY - bounds.Y;
			FillSideMargins(currentY);
			TableRowPainter.DrawTitleRow(buffer, startX, currentY, contentWidth, clipRect,
				_title, _titleAlignment, headerFg, effectiveBg);
			currentY++;
		}

		// Build render-time column list with dummy entry for checkbox column.
		//
		// IN DATA-SOURCE MODE colSnapshot IS NULL (the branch above reads only ColumnCount), so
		// without this the null flowed into DrawDataRow and its alignment lookup fell through to the
		// Left default for every column — ITableDataSource.GetColumnAlignment was declared,
		// implemented by data sources, and never consulted anywhere (issue #78). SetColumnAlignment
		// could not compensate either: it writes into _columns, which is empty here.
		//
		// Projecting the alignment into the same TableColumn list the non-data-source path already
		// builds keeps DrawDataRow and the checkbox offset below untouched — the checkbox dummy is
		// prepended to this list exactly as it is for bound columns, so indices stay aligned.
		List<TableColumn>? renderCols = colSnapshot;
		if (renderCols == null && _dataSource != null)
		{
			renderCols = new List<TableColumn>(colCount);
			for (int c = 0; c < colCount; c++)
			{
				renderCols.Add(new TableColumn
				{
					Header = _dataSource.GetColumnHeader(c),
					Alignment = _dataSource.GetColumnAlignment(c),
					Width = _dataSource.GetColumnWidth(c)
				});
			}
		}

		if (_checkboxMode && renderCols != null)
		{
			var withCheckbox = new List<TableColumn>(renderCols.Count + 1);
			withCheckbox.Add(new TableColumn { Header = "", Alignment = TextJustification.Left, Width = ControlDefaults.TableCheckboxColumnWidth });
			withCheckbox.AddRange(renderCols);
			renderCols = withCheckbox;
		}

		// Column geometry for hit testing, in the painter's logical positions (see TableGeometry).
		// Recorded on every paint, not only when the header is drawn: with ShowHeader off it used to
		// stay empty, and no click could tell which column it landed on.
		int contentLeft = startX - bounds.X + (hasBorder ? 1 : 0);
		var columnStarts = new int[colCount];
		{
			int logical = _checkboxMode ? checkboxColWidth + (hasBorder ? 1 : 0) : 0;
			for (int c = 0; c < colCount; c++)
			{
				columnStarts[c] = logical;
				if (colSnapshot != null && c < colSnapshot.Count)
				{
					colSnapshot[c].RenderedX = bounds.X + contentLeft + logical;
					colSnapshot[c].RenderedWidth = dataColWidths[c];
				}
				bool addSep = hasBorder || (_columnSeparator.HasValue && c < colCount - 1);
				int sepW = hasBorder ? 1 : SeparatorWidth;
				logical += dataColWidths[c] + (addSep ? sepW : 0);
			}
		}

		// Top border
		if (hasBorder && currentY < maxY)
		{
			FillSideMargins(currentY);
			TableRowPainter.DrawHorizontalLine(buffer, style, startX, currentY, colWidths, clipRect,
				box.TopLeft, box.TopTee, box.TopRight, box.Horizontal, hScrollOffset: effectiveHScroll);
			currentY++;
		}

		// Header row
		if (_showHeader && currentY < maxY)
		{
			FillSideMargins(currentY);

			List<string> headerCells;
			if (_dataSource != null)
			{
				headerCells = new List<string>();
				for (int c = 0; c < colCount; c++)
				{
					string header = _dataSource.GetColumnHeader(c);
					// Append sort indicator
					if (_sortingEnabled && _sortColumnIndex == c)
					{
						header += _sortDirection == SortDirection.Ascending ? " \u25b2" : " \u25bc";
					}
					headerCells.Add(header);
				}
			}
			else
			{
				headerCells = new List<string>();
				for (int c = 0; c < colSnapshot!.Count; c++)
				{
					string header = colSnapshot[c].Header;
					if (_sortingEnabled && _sortColumnIndex == c)
					{
						header += _sortDirection == SortDirection.Ascending ? " \u25b2" : " \u25bc";
					}
					headerCells.Add(header);
				}
			}

			// Prepend empty cell for checkbox column
			if (_checkboxMode)
				headerCells.Insert(0, "");

			TableRowPainter.DrawDataRow(buffer, style, startX, currentY, colWidths, clipRect,
				headerCells, renderCols, headerFg, headerBg,
				hScrollOffset: effectiveHScroll, trailingFillWidth: scrollbarGutter);

			headerLine = currentY - bounds.Y;
			currentY++;

			// Header separator
			if (hasBorder && currentY < maxY)
			{
				FillSideMargins(currentY);
				TableRowPainter.DrawHorizontalLine(buffer, style, startX, currentY, colWidths, clipRect,
					box.LeftTee, box.Cross, box.RightTee, box.Horizontal, hScrollOffset: effectiveHScroll);
				currentY++;
			}
		}

		// Track data row rendering area for scrollbar
		int dataStartY = currentY;

		// Data rows - virtual rendering (only visible rows). RowCount, not the snapshot's count: the
		// snapshot holds every data row, and the display positions past a filter's matches fell back
		// to identity in MapDisplayToData, painting the unmatched rows below the matches.
		int rowCount = RowCount;
		int startRow = _scrollOffset;
		int endRow = Math.Min(rowCount, _scrollOffset + GetVisibleRowCount());

		// Asked once per paint. HasWrappingColumns takes the table lock and walks the columns, and
		// the row loop below would otherwise ask it per row through BuildWrappedRow.
		bool wrapsAnything = HasWrappingColumns;

		// Where each row actually landed. The hit test used to derive this by subtracting the
		// chrome and dividing by a constant, which is only right while every row is the same
		// height; recording it makes a click resolve to the row that was drawn there.
		//
		// Recorded only for a wrapping table. With every row one line that arithmetic is exact and
		// needs no bands, so a non-wrapping table should not pay a list plus a ToArray every frame
		// for something nothing will read, and should keep resolving clicks against the live
		// scroll offset rather than against the last paint.
		var rowBands = wrapsAnything
			? new List<(int Row, int FirstLine, int Height)>(Math.Max(0, endRow - startRow))
			: null;

		for (int displayR = startRow; displayR < endRow && currentY < maxY; displayR++)
		{
			int dataR = MapDisplayToData(displayR);
			// Guard against snapshot/data race — dataR may be invalid if _rows changed after snapshot
			if (rowSnapshot != null && (dataR < 0 || dataR >= rowSnapshot.Count))
				continue;

			// Row separator (between rows, not before first)
			if (displayR > startRow && _showRowSeparators && hasBorder && currentY < maxY)
			{
				FillSideMargins(currentY);
				TableRowPainter.DrawHorizontalLine(buffer, style, startX, currentY, colWidths, clipRect,
					box.LeftTee, box.Cross, box.RightTee, box.Horizontal, hScrollOffset: effectiveHScroll);
				currentY++;
			}

			if (currentY >= maxY) break;

			// Get row data
			IList<string> rowCells;
			Color rowBg, rowFg;
			bool isEnabled = true;
			bool isChecked = false;

			// Hoisted out of the branches below so the checkbox cell can ask about the row
			// itself: null over a data source, which has rows but no TableRow objects.
			TableRow? row = null;

			if (_dataSource != null)
			{
				rowCells = new List<string>();
				for (int c = 0; c < colCount; c++)
					rowCells.Add(_dataSource.GetCellValue(dataR, c));
				rowBg = _dataSource.GetRowBackgroundColor(dataR) ?? bgColor;
				rowFg = _dataSource.GetRowForegroundColor(dataR) ?? fgColor;
				isEnabled = _dataSource.IsRowEnabled(dataR);
				if (_checkboxMode)
					isChecked = _selectedRowIndices.Contains(displayR);
			}
			else
			{
				row = rowSnapshot![dataR];
				rowCells = row.Cells;
				rowBg = row.BackgroundColor ?? bgColor;
				rowFg = row.ForegroundColor ?? fgColor;
				isEnabled = row.IsEnabled;
				isChecked = row.IsChecked;
			}

			// Determine row colors based on state (cursor, checked, hover)
			bool isCursorRow = displayR == _selectedRowIndex;
			bool isMultiSelected = _multiSelectEnabled && _selectedRowIndices.Contains(displayR);
			bool isRowSel = isCursorRow || isMultiSelected;
			bool isHovered = _hoveredRowIndex == displayR;

			Color effectiveRowBg = rowBg;
			Color effectiveRowFg = rowFg;

			if (isCursorRow)
			{
				// Cursor row — brightest highlight
				if (HasFocus)
				{
					effectiveRowBg = selBg;
					effectiveRowFg = selFg;
				}
				else
				{
					effectiveRowBg = unfocusedSelBg;
					effectiveRowFg = unfocusedSelFg;
				}
			}
			else if (isMultiSelected)
			{
				// Checked but not cursor — subtle highlight
				effectiveRowBg = new Color(
					(byte)Math.Min(255, selBg.R / 3 + rowBg.R * 2 / 3),
					(byte)Math.Min(255, selBg.G / 3 + rowBg.G * 2 / 3),
					(byte)Math.Min(255, selBg.B / 3 + rowBg.B * 2 / 3));
				effectiveRowFg = rowFg;
			}
			else if (isHovered)
			{
				effectiveRowBg = hoverBg;
				effectiveRowFg = hoverFg;
			}

			if (!isEnabled)
			{
				effectiveRowFg = Color.Grey;
			}

			// Cell-level highlight
			int selectedCell = -1;
			Color? cellHighlightBg = null;
			Color? cellHighlightFg = null;
			if (_cellNavigationEnabled && displayR == _selectedRowIndex && _selectedColumnIndex >= 0)
			{
				selectedCell = _selectedColumnIndex;
				cellHighlightBg = HasFocus ? Color.Cyan1 : Color.Grey50;
				cellHighlightFg = Color.Black;
			}

			// Inline editing: replace cell content with edit buffer (use a copy to avoid mutating data)
			int editCellIndex = -1;
			int editCursorPos = -1;
			if (_isEditing && displayR == _selectedRowIndex && _selectedColumnIndex >= 0)
			{
				editCellIndex = _selectedColumnIndex;
				editCursorPos = _editCursorPosition;
				rowCells = new List<string>(rowCells);
				if (_selectedColumnIndex < rowCells.Count)
					rowCells[_selectedColumnIndex] = _editBuffer;
				else
					rowCells.Add(_editBuffer);
			}

			// Compute filter match positions for highlighting.
			//
			// While typing counts too. ApplyFilterLive already narrows the rows
			// on every keystroke and sets _activeFilter, so the matches are known
			// and the only thing the Confirmed check bought was showing the rows
			// a filter kept without showing WHY it kept them - the answer arrived
			// a keystroke late, on Enter, once the question had been settled.
			List<(int Column, int Start, int Length)>? filterMatches = null;
			if (_filterMode != FilterMode.None && _activeFilter != null)
				filterMatches = FindMatchPositions(dataR, _activeFilter);

			// Prepend checkbox cell and offset indices for silent column
			if (_checkboxMode)
			{
				string checkText = GetCheckboxMarkup(row, isChecked);
				rowCells = new List<string>(rowCells);
				rowCells.Insert(0, checkText);
				if (selectedCell >= 0) selectedCell++;
				if (editCellIndex >= 0) editCellIndex++;
				if (filterMatches != null)
					filterMatches = filterMatches.Select(m => (m.Column + 1, m.Start, m.Length)).ToList();
			}

			// How many lines this row takes, and what each of its cells shows on each of them.
			// Null when no column wraps, which keeps the original single-line call below exactly
			// as it was rather than routing it through a general case with a height of one.
			var wrapped = BuildWrappedRow(rowCells, colWidths, renderCols, effectiveRowFg, effectiveRowBg,
				editCellIndex, wrapsAnything, out int rowHeight);

			// A row taller than the space left would be drawn half on and half off the bottom
			// edge. Stop before it, as the loop already does for a single-line row that does not
			// fit: a partly drawn row reads as a rendering fault, while a missing one reads as
			// more content below, which is what the scrollbar is already saying.
			if (rowHeight > 1 && currentY + rowHeight > maxY && currentY > dataStartY)
				break;

			int rowTop = currentY;
			for (int line = 0; line < rowHeight && currentY < maxY; line++)
			{
				FillSideMargins(currentY);
				TableRowPainter.DrawDataRow(buffer, style, startX, currentY, colWidths, clipRect,
					wrapped == null ? rowCells : wrapped.LineCells(line, rowHeight),
					renderCols, effectiveRowFg, effectiveRowBg,
					hScrollOffset: effectiveHScroll,
					isSelected: isRowSel || isHovered,
					selectedCellIndex: selectedCell, selectedCellBg: cellHighlightBg, selectedCellFg: cellHighlightFg,
					// The edit cursor belongs to the value being typed, which is on the row's
					// first line; a cursor repeated down a tall row would show one caret per line.
					editCellIndex: line == 0 ? editCellIndex : -1,
					editCursorPos: line == 0 ? editCursorPos : -1,
					filterMatches: line == 0 ? filterMatches : null,
					trailingFillWidth: scrollbarGutter,
					cellPrefixes: GetCellPrefixes(dataR, colCount, colWidths.Length, line));

				currentY++;
			}

			// Update row rendered position for hit testing (for in-memory rows)
			if (_dataSource == null && rowSnapshot != null && dataR < rowSnapshot.Count)
			{
				rowSnapshot[dataR].RenderedY = rowTop;
				rowSnapshot[dataR].RenderedHeight = currentY - rowTop;
			}

			// Where each visible row landed, for the hit test to search instead of dividing.
			rowBands?.Add((displayR, rowTop - bounds.Y, currentY - rowTop));
		}

		// Pad with empty rows so the bottom border sits at the bottom of the allocated
		// bounds when the table has been given more height than its content needs (e.g.
		// VerticalAlignment.Fill, or an explicit Height larger than the data). Without this
		// the border would close right after the last data row, leaving blank space below
		// it inside the table's own slot. Reserve the rows the bottom chrome will consume.
		int bottomChromeHeight = (_filteringEnabled && !_readOnly && hasBorder ? 2 : 0) + (hasBorder ? 1 : 0);
		int paddingStopY = maxY - bottomChromeHeight;
		while (currentY < paddingStopY)
		{
			FillSideMargins(currentY);
			TableRowPainter.DrawDataRow(buffer, style, startX, currentY, colWidths, clipRect,
				new List<string>(), renderCols, fgColor, bgColor,
				hScrollOffset: effectiveHScroll, trailingFillWidth: scrollbarGutter);
			currentY++;
		}
		int dataEndY = currentY;

		// Filter status bar (separator + status row + bottom border as one merged section)
		if (_filteringEnabled && !_readOnly && hasBorder)
		{
			// Separator: ├────────────────────────────┤ (no column crosses)
			if (currentY < maxY)
			{
				filterBarTop = currentY - bounds.Y;
				FillSideMargins(currentY);
				TableRowPainter.DrawMergedHorizontalLine(buffer, style, startX, currentY, colWidths, clipRect,
					box.LeftTee, box.RightTee, box.Horizontal);
				currentY++;
			}

			// Status bar content row
			if (currentY < maxY)
			{
				int statusWidth = 0;
				foreach (int w in colWidths) statusWidth += w;
				statusWidth += colWidths.Length + 1; // border overhead

				FillSideMargins(currentY);
				DrawFilterStatusBar(buffer, startX, currentY, statusWidth, clipRect,
					fgColor, effectiveBg, box, borderColor, hasBorder);
				currentY++;
				filterBarBottom = currentY - bounds.Y;
			}

			// Bottom border: ╰────────────────────────────╯ (no column tees)
			if (currentY < maxY)
			{
				FillSideMargins(currentY);
				TableRowPainter.DrawMergedHorizontalLine(buffer, style, startX, currentY, colWidths, clipRect,
					box.BottomLeft, box.BottomRight, box.Horizontal);
				currentY++;
			}
		}
		else if (hasBorder && currentY < maxY)
		{
			// Standard bottom border with column tees
			FillSideMargins(currentY);
			TableRowPainter.DrawHorizontalLine(buffer, style, startX, currentY, colWidths, clipRect,
				box.BottomLeft, box.BottomTee, box.BottomRight, box.Horizontal, hScrollOffset: effectiveHScroll);
			currentY++;
		}

		// Fill remaining height (before scrollbar)
		while (currentY < maxY)
		{
			if (currentY >= clipRect.Y && currentY < clipRect.Bottom)
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), fgColor, effectiveBg);
			currentY++;
		}

		// Draw vertical scrollbar. GetVerticalScrollbarRect() is the single source of truth for this
		// bar's rectangle, shared with hit testing in TableControl.Mouse.cs.
		int vScrollbarX = bounds.X + GetVerticalScrollbarRect().x;
		if (showVScrollbar)
		{
			int dataRowsHeight = currentY - dataStartY;
			if (dataRowsHeight > 0)
				DrawVerticalScrollbar(buffer, vScrollbarX, dataStartY, dataRowsHeight, bgColor);
		}

		// Draw horizontal scrollbar
		if (showHScrollbar && currentY < bounds.Bottom - Margin.Bottom)
		{
			FillSideMargins(currentY);
			int hScrollWidth = contentWidth;
			if (showVScrollbar) hScrollWidth--; // don't overlap vertical scrollbar

			DrawHorizontalScrollbar(buffer, startX, currentY, hScrollWidth, totalColumnsWidth, bgColor);

			// Corner cell when both scrollbars visible
			if (showVScrollbar)
			{
				int cornerX = vScrollbarX;
				if (cornerX >= clipRect.X && cornerX < clipRect.Right)
					buffer.SetNarrowCell(cornerX, currentY, ' ', fgColor, effectiveBg);
			}
			currentY++;
		}

		ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, effectiveBg);

		// Published once the paint is complete, so a hit test never sees half of one.
		_geometry = new TableGeometry(contentLeft, startX - bounds.X + contentWidth - (hasBorder ? 1 : 0), effectiveHScroll, columnStarts,
			(int[])dataColWidths.Clone(), _checkboxMode ? checkboxColWidth : 0,
			new TableLineBands(titleLine, headerLine, dataStartY - bounds.Y, dataEndY - bounds.Y, filterBarTop, filterBarBottom),
			rowBands?.ToArray());

		// Apply row animation overlays (flash, highlight, fade-out)
		if (HasActiveRowAnimations)
			ApplyRowAnimationOverlays(buffer);
	}

	#endregion
}
