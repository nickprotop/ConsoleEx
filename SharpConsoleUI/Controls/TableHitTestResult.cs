// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// What part of a <see cref="TableControl"/> a position falls on, returned from
/// <see cref="TableControl.HitTest"/>.
/// </summary>
/// <remarks>
/// <para>
/// A struct rather than a column index and a row index, because a position on a table is more than
/// a cell: the same x can be a cell on one line and the header, a border or the filter bar on
/// another, and a cell has an offset within it that a caller drawing something inside the cell
/// needs to act on. Code that handled clicks itself had to re-derive all of this from the layout,
/// and re-deriving it is how hit tests drift from what is drawn.
/// </para>
/// <para>
/// The row is reported twice: by display position, which is what the selection and the row events
/// use, and by data row, which is what <see cref="TableControl.GetRow"/> and the cell accessors
/// use. Under a sort or a filter the two differ.
/// </para>
/// </remarks>
public readonly struct TableHitTestResult
{
	/// <summary>Creates a result.</summary>
	/// <param name="zone">The part of the table hit.</param>
	/// <param name="displayRowIndex">The display row, or -1.</param>
	/// <param name="dataRowIndex">The data row, or -1.</param>
	/// <param name="columnIndex">The data column, or -1.</param>
	/// <param name="cellOffset">The cells from the column's left edge, or -1.</param>
	public TableHitTestResult(TableHitZone zone, int displayRowIndex, int dataRowIndex, int columnIndex, int cellOffset)
	{
		Zone = zone;
		DisplayRowIndex = displayRowIndex;
		DataRowIndex = dataRowIndex;
		ColumnIndex = columnIndex;
		CellOffset = cellOffset;
	}

	/// <summary>The part of the table the position falls on.</summary>
	public TableHitZone Zone { get; }

	/// <summary>
	/// The display row hit, as <see cref="TableControl.SelectedRowIndex"/> counts rows, or -1 when
	/// the position is not on a row.
	/// </summary>
	public int DisplayRowIndex { get; }

	/// <summary>
	/// The data row hit, as <see cref="TableControl.GetRow"/> counts rows (or the data source's row),
	/// or -1 when the position is not on a row.
	/// </summary>
	public int DataRowIndex { get; }

	/// <summary>
	/// The data column hit, or -1 when the position is not on a column. The checkbox column is not a
	/// data column: it is reported as <see cref="TableHitZone.Checkbox"/> with no column index.
	/// </summary>
	public int ColumnIndex { get; }

	/// <summary>
	/// How many cells into the column — or into the checkbox — the position is, counted from its
	/// left edge as laid out, so horizontal scrolling does not change it; -1 when there is no
	/// column.
	/// </summary>
	public int CellOffset { get; }

	/// <summary>A position on no part of the table, or a table not painted yet.</summary>
	public static TableHitTestResult None { get; } = new(TableHitZone.None, -1, -1, -1, -1);
}

/// <summary>
/// The parts of a <see cref="TableControl"/> a <see cref="TableHitTestResult"/> can name.
/// </summary>
public enum TableHitZone
{
	/// <summary>No part of the table: a margin, a horizontal border, or a table not painted yet.</summary>
	None,

	/// <summary>The title row.</summary>
	Title,

	/// <summary>The header row; the column is set when the position is on one.</summary>
	Header,

	/// <summary>A cell of a data row: the row and the column are both set.</summary>
	Cell,

	/// <summary>A data row, but between or beside its cells: a border, a separator, or a gutter.</summary>
	Row,

	/// <summary>The checkbox of a data row, in checkbox mode.</summary>
	Checkbox,

	/// <summary>The data area where no row is drawn: below the last row, or a line between rows.</summary>
	EmptyDataArea,

	/// <summary>The filter status bar and the line above it.</summary>
	FilterBar,

	/// <summary>The vertical scrollbar.</summary>
	VerticalScrollbar,

	/// <summary>The horizontal scrollbar.</summary>
	HorizontalScrollbar,
}
