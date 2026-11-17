// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Controls;

/// <summary>
/// How many lines each row of a wrapping table needs, remembered between frames.
/// </summary>
/// <remarks>
/// <para>
/// Without a wrapping column a row is one line and nothing here is consulted. With one, the
/// height is a measurement, and a measurement on the paint path is paid per frame per row unless
/// it is kept. A table showing forty rows at sixty frames a second would otherwise wrap two
/// thousand four hundred cells a second to redraw a screen that did not change.
/// </para>
/// <para>
/// The cache is keyed by row and by the column widths that produced the answer. A width change
/// invalidates every entry rather than being tracked per row: widths change together, on a
/// resize or a reflow, so per-row bookkeeping would cost more than the rewrap it saved.
/// </para>
/// <para>
/// Only rows that are painted are ever measured, which is what keeps a ten-thousand-row virtual
/// source affordable. Entries are capped so a long scroll cannot grow this without limit; past
/// the cap the oldest answers are dropped, since the rows being scrolled away from are the ones
/// least likely to be asked about again.
/// </para>
/// </remarks>
internal sealed class TableRowHeightCache
{
	/// <summary>
	/// Rows to remember before dropping the oldest. A few screens' worth: enough that scrolling
	/// back and forth over the same region is free, small enough to stay negligible beside the
	/// row data itself.
	/// </summary>
	private const int MaxEntries = 512;

	private readonly Dictionary<int, int> _heights = new();
	private readonly Queue<int> _order = new();
	private int[]? _widths;

	/// <summary>
	/// How many rows have actually been measured, cache hits excluded. Test-visible: this is what
	/// proves a large virtual source measures only what it paints.
	/// </summary>
	internal int MeasureCount { get; private set; }

	/// <summary>
	/// The lines row <paramref name="displayRow"/> needs at these column widths.
	/// </summary>
	/// <param name="displayRow">The row, in display order.</param>
	/// <param name="colWidths">The widths this paint is using, one per data column.</param>
	/// <param name="cellsForRow">Fetches a row's cell values; called only on a miss.</param>
	/// <param name="cols">
	/// The columns, to read Wrap and MaxLines from. <c>null</c> in DataSource mode, which has no
	/// TableColumn objects and so no column that can wrap: every row is then one line.
	/// </param>
	/// <returns>At least 1, never more than <see cref="ControlDefaults.TableMaxWrapLines"/>.</returns>
	internal int GetHeight(int displayRow, int[] colWidths,
		Func<int, IList<string>> cellsForRow, IReadOnlyList<TableColumn>? cols)
	{
		if (cols == null) return 1;

		// A paint at new widths answers every question differently, so the old answers go.
		if (!SameWidths(colWidths))
		{
			_heights.Clear();
			_order.Clear();
			_widths = (int[])colWidths.Clone();
		}

		if (_heights.TryGetValue(displayRow, out int cached))
			return cached;

		int height = Measure(displayRow, colWidths, cellsForRow, cols);
		MeasureCount++;

		if (_heights.Count >= MaxEntries && _order.Count > 0)
			_heights.Remove(_order.Dequeue());

		_heights[displayRow] = height;
		_order.Enqueue(displayRow);
		return height;
	}

	/// <summary>
	/// Forgets every height, after a column resize, a cell change, or a change of what is
	/// displayed.
	/// </summary>
	/// <remarks>
	/// There is deliberately no per-row counterpart. A cell change resets the display map, so the
	/// display index the changed row now sits at is not knowable from the change itself, and a
	/// per-row drop would have to guess. Dropping everything costs one rewrap of the visible rows
	/// on the next frame, which is the same work a resize already pays.
	/// </remarks>
	internal void InvalidateAll()
	{
		_heights.Clear();
		_order.Clear();
		_widths = null;
	}

	/// <summary>The tallest wrapped cell in the row, which is the row's height.</summary>
	private static int Measure(int displayRow, int[] colWidths,
		Func<int, IList<string>> cellsForRow, IReadOnlyList<TableColumn> cols)
	{
		var cells = cellsForRow(displayRow);
		int tallest = 1;

		for (int c = 0; c < cols.Count; c++)
		{
			if (!cols[c].Wrap) continue;

			// Mid-resize the widths can lag the columns; a column with no width cannot be
			// measured, and guessing one would wrap to a width nothing will be drawn at.
			if (c >= colWidths.Length || c >= cells.Count) continue;

			int lines = TableCellWrapper.MeasureLines(cells[c], colWidths[c], cols[c].MaxLines);
			if (lines > tallest) tallest = lines;
		}

		return Math.Min(tallest, ControlDefaults.TableMaxWrapLines);
	}

	private bool SameWidths(int[] colWidths)
	{
		if (_widths == null || _widths.Length != colWidths.Length) return false;
		for (int i = 0; i < colWidths.Length; i++)
			if (_widths[i] != colWidths[i]) return false;
		return true;
	}
}
