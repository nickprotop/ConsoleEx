// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Works out how wide each column of a <see cref="TableControl"/> is drawn.
/// </summary>
/// <remarks>
/// <para>
/// ONE ALGORITHM FOR BOTH ROW SOURCES. In-memory columns and an <see cref="ITableDataSource"/> used
/// to have a copy each, differing only in where a column's fixed width, floor, header and cells
/// come from — and copies drift. Those four questions are now an <see cref="ITableWidthSource"/>,
/// implemented by a struct per source so the generic call stays allocation-free on the paint path.
/// </para>
/// <para>
/// Only the in-memory path is cached, exactly as before: a data source may change its values
/// without telling the table, and has always been measured afresh on every paint.
/// </para>
/// </remarks>
internal sealed class TableColumnWidthCalculator
{
	private readonly TextMeasurementCache _measurementCache;
	private int[]? _cachedWidths;
	private int _cachedForWidth = -1;
	private int _cachedScrollBucket = -1;

	/// <summary>Creates a calculator measuring text through the table's own cache.</summary>
	internal TableColumnWidthCalculator(TextMeasurementCache measurementCache)
	{
		_measurementCache = measurementCache;
	}

	/// <summary>Drops the cached widths so the next paint measures again.</summary>
	internal void Invalidate()
	{
		_cachedWidths = null;
		_cachedForWidth = -1;
		_cachedScrollBucket = -1;
	}

	/// <summary>
	/// Computes column widths for the given total available width.
	/// Uses sample-based measurement for auto-width columns (visible rows + small buffer).
	/// </summary>
	/// <param name="source">Where each column's width, floor, header and cells come from.</param>
	/// <param name="layout">The space available and the table settings that consume part of it.</param>
	/// <param name="useCache">Whether to reuse and remember the result for the same width and scroll bucket.</param>
	internal int[] Compute<TSource>(TSource source, in TableWidthLayout layout, bool useCache)
		where TSource : ITableWidthSource
	{
		int colCount = source.ColumnCount;
		if (colCount == 0) return Array.Empty<int>();

		// Check cache - invalidate on significant scroll change
		int scrollBucket = layout.ScrollOffset / Math.Max(1, layout.VisibleRowCount / 2);
		if (useCache && _cachedWidths != null && _cachedForWidth == layout.AvailableWidth && _cachedScrollBucket == scrollBucket)
			return _cachedWidths;

		int separatorOverhead = layout.HasBorder ? (colCount + 1)
			: (layout.ColumnSeparator.HasValue ? Math.Max(0, colCount - 1) * layout.SeparatorWidth : 0);
		int contentWidth = layout.AvailableWidth - separatorOverhead;
		if (contentWidth < colCount) contentWidth = colCount;

		var widths = new int[colCount];
		int autoCount = 0;

		// Determine sample range for auto-width columns
		int sampleStart = Math.Max(0, layout.ScrollOffset);
		int sampleEnd = Math.Min(source.RowCount, layout.ScrollOffset + Math.Max(50, layout.VisibleRowCount));

		for (int c = 0; c < colCount; c++)
		{
			if (source.GetFixedWidth(c) is int fixedWidth)
			{
				widths[c] = fixedWidth;
			}
			else
			{
				// Sample-based measurement: header + visible rows + buffer
				int maxW = _measurementCache.GetCachedLength(source.GetHeader(c));

				for (int r = sampleStart; r < sampleEnd; r++)
				{
					if (source.GetCell(r, c) is string cell)
					{
						int cellW = _measurementCache.GetCachedLength(cell);
						if (cellW > maxW) maxW = cellW;
					}
				}

				widths[c] = maxW;
				autoCount++;
			}
		}

		// Distribute remaining space
		int totalNatural = 0;
		for (int c = 0; c < colCount; c++) totalNatural += widths[c];

		if (layout.Stretch && totalNatural < contentWidth)
		{
			int remaining = contentWidth - totalNatural;
			int distributeCount = autoCount > 0 ? autoCount : colCount;
			int perCol = remaining / distributeCount;
			int extraCols = remaining % distributeCount;

			for (int c = 0; c < colCount; c++)
			{
				bool isAutoCol = !source.GetFixedWidth(c).HasValue;
				if (autoCount > 0 && !isAutoCol) continue;

				widths[c] += perCol;
				if (extraCols > 0) { widths[c]++; extraCols--; }
			}
		}
		else if (totalNatural > contentWidth)
		{
			// Shrink only auto-width columns first; preserve fixed-width columns
			int fixedTotal = 0;
			int autoTotal = 0;
			for (int c = 0; c < colCount; c++)
			{
				if (source.GetFixedWidth(c).HasValue)
					fixedTotal += widths[c];
				else
					autoTotal += widths[c];
			}

			ShrinkToFit(widths, colCount, contentWidth, fixedTotal, autoTotal, totalNatural, source);
		}

		if (useCache)
		{
			_cachedWidths = widths;
			_cachedForWidth = layout.AvailableWidth;
			_cachedScrollBucket = scrollBucket;
		}

		return widths;
	}

	/// <summary>
	/// Shrinks natural column widths to fit <paramref name="contentWidth"/>, honouring each auto
	/// column's minimum-width floor.
	/// </summary>
	/// <remarks>
	/// <para>Fixed-width columns have no floor concept: they are already explicit, so theirs stays 1.
	/// A null or non-positive <c>MinWidth</c> also means 1 (no floor), matching the behaviour before
	/// floors existed.</para>
	///
	/// <para>When honouring every floor needs more room than there is, the total is allowed to exceed
	/// <paramref name="contentWidth"/>. The table then pans with its horizontal scrollbar rather than
	/// crushing a column below a usable width — that overflow is the point of the feature, not a bug.</para>
	/// </remarks>
	/// <param name="widths">Natural widths, shrunk in place.</param>
	/// <param name="colCount">Number of columns.</param>
	/// <param name="contentWidth">Space available for content.</param>
	/// <param name="fixedTotal">Total width of the fixed columns.</param>
	/// <param name="autoTotal">Total natural width of the auto columns.</param>
	/// <param name="totalNatural">Total natural width of every column.</param>
	/// <param name="source">Answers whether a column is fixed and what its floor is.</param>
	private static void ShrinkToFit<TSource>(int[] widths, int colCount, int contentWidth,
		int fixedTotal, int autoTotal, int totalNatural, TSource source)
		where TSource : ITableWidthSource
	{
		// A fixed column is already explicit, so it has no floor; anything unset or non-positive
		// means "no floor", i.e. 1.
		int Floor(int c) => source.GetFixedWidth(c).HasValue ? 1 : Math.Max(1, source.GetMinWidth(c) ?? 1);

		int autoTarget = contentWidth - fixedTotal;
		if (autoTarget > 0 && autoTotal > 0)
		{
			// Enough room for the fixed columns: distribute what is left across the auto ones.
			double ratio = (double)autoTarget / autoTotal;
			int assigned = fixedTotal;
			int lastAutoCol = -1;
			for (int c = 0; c < colCount; c++)
			{
				if (source.GetFixedWidth(c).HasValue) continue;
				widths[c] = Math.Max(Floor(c), (int)(widths[c] * ratio));
				assigned += widths[c];
				lastAutoCol = c;
			}
			// Rounding leftovers go to the last auto column so the row fills exactly.
			if (lastAutoCol >= 0)
				widths[lastAutoCol] = Math.Max(Floor(lastAutoCol), widths[lastAutoCol] + (contentWidth - assigned));
		}
		else
		{
			// Not even the fixed columns fit — shrink everything proportionally, floors still applying.
			double ratio = (double)contentWidth / totalNatural;
			int assigned = 0;
			for (int c = 0; c < colCount - 1; c++)
			{
				widths[c] = Math.Max(Floor(c), (int)(widths[c] * ratio));
				assigned += widths[c];
			}
			widths[colCount - 1] = Math.Max(Floor(colCount - 1), contentWidth - assigned);
		}
	}
}

/// <summary>
/// The questions <see cref="TableColumnWidthCalculator"/> asks about the columns it sizes.
/// </summary>
internal interface ITableWidthSource
{
	/// <summary>Number of columns.</summary>
	int ColumnCount { get; }

	/// <summary>Number of rows that can be sampled.</summary>
	int RowCount { get; }

	/// <summary>The column's explicit width, or null when it is sized from its content.</summary>
	int? GetFixedWidth(int column);

	/// <summary>The auto-width column's floor, or null for none.</summary>
	int? GetMinWidth(int column);

	/// <summary>The column's header text.</summary>
	string GetHeader(int column);

	/// <summary>The cell's text, or null when the row has no such cell.</summary>
	string? GetCell(int row, int column);
}

/// <summary>Sizes the table's own <see cref="TableColumn"/>s from its <see cref="TableRow"/>s.</summary>
internal readonly struct TableColumnWidthSource : ITableWidthSource
{
	private readonly List<TableColumn> _columns;
	private readonly List<TableRow>? _rows;

	/// <summary>Creates a source over the given column and row snapshots.</summary>
	internal TableColumnWidthSource(List<TableColumn> columns, List<TableRow>? rows)
	{
		_columns = columns;
		_rows = rows;
	}

	/// <inheritdoc/>
	public int ColumnCount => _columns.Count;

	/// <inheritdoc/>
	public int RowCount => _rows?.Count ?? 0;

	/// <inheritdoc/>
	public int? GetFixedWidth(int column) => _columns[column].Width;

	/// <inheritdoc/>
	public int? GetMinWidth(int column) => _columns[column].MinWidth;

	/// <inheritdoc/>
	public string GetHeader(int column) => _columns[column].Header;

	/// <inheritdoc/>
	public string? GetCell(int row, int column)
	{
		var cells = _rows![row].Cells;
		return column < cells.Count ? cells[column] : null;
	}
}

/// <summary>
/// Sizes the columns of an <see cref="ITableDataSource"/>, letting a column the user resized keep
/// the width it was dragged to.
/// </summary>
internal readonly struct TableDataSourceWidthSource : ITableWidthSource
{
	private readonly ITableDataSource _dataSource;
	private readonly Dictionary<int, int> _widthOverrides;

	/// <summary>Creates a source over the data source and the user's resize overrides.</summary>
	internal TableDataSourceWidthSource(ITableDataSource dataSource, Dictionary<int, int> widthOverrides)
	{
		_dataSource = dataSource;
		_widthOverrides = widthOverrides;
	}

	/// <inheritdoc/>
	public int ColumnCount => _dataSource.ColumnCount;

	/// <inheritdoc/>
	public int RowCount => _dataSource.RowCount;

	/// <inheritdoc/>
	public int? GetFixedWidth(int column)
		=> _widthOverrides.TryGetValue(column, out int overrideWidth) ? overrideWidth : _dataSource.GetColumnWidth(column);

	/// <inheritdoc/>
	public int? GetMinWidth(int column) => _dataSource.GetColumnMinWidth(column);

	/// <inheritdoc/>
	public string GetHeader(int column) => _dataSource.GetColumnHeader(column);

	/// <inheritdoc/>
	public string? GetCell(int row, int column) => _dataSource.GetCellValue(row, column);
}

/// <summary>
/// The space a <see cref="TableColumnWidthCalculator"/> divides and the settings that decide how.
/// </summary>
/// <param name="AvailableWidth">Cells available to the columns and their separators.</param>
/// <param name="HasBorder">Whether vertical borders separate the columns.</param>
/// <param name="ColumnSeparator">The separator glyph drawn between columns without borders, if any.</param>
/// <param name="SeparatorWidth">Cells one such separator takes.</param>
/// <param name="Stretch">Whether spare width is handed out to the columns.</param>
/// <param name="ScrollOffset">First row on screen, where content sampling starts.</param>
/// <param name="VisibleRowCount">Rows on screen, which sets how far sampling reaches.</param>
internal readonly record struct TableWidthLayout(
	int AvailableWidth,
	bool HasBorder,
	char? ColumnSeparator,
	int SeparatorWidth,
	bool Stretch,
	int ScrollOffset,
	int VisibleRowCount);
