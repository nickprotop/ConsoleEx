// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Events;

/// <summary>
/// Event arguments for table cell events (click, double-click).
/// </summary>
public class TableCellEventArgs : EventArgs
{
	/// <summary>
	/// Gets the row index of the cell.
	/// </summary>
	public int RowIndex { get; }

	/// <summary>
	/// Gets the column index of the cell.
	/// </summary>
	public int ColumnIndex { get; }

	/// <summary>
	/// Gets the row containing the cell.
	/// </summary>
	public TableRow Row { get; }

	/// <summary>
	/// Gets the column containing the cell.
	/// </summary>
	public TableColumn Column { get; }

	/// <summary>
	/// Gets the text value of the cell.
	/// </summary>
	public string CellValue { get; }

	/// <summary>
	/// Gets the mouse event that triggered this cell event, if applicable.
	/// </summary>
	public MouseEventArgs? MouseEvent { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TableCellEventArgs"/> class.
	/// </summary>
	public TableCellEventArgs(int rowIndex, int columnIndex, TableRow row, TableColumn column, string cellValue, MouseEventArgs? mouseEvent)
	{
		RowIndex = rowIndex;
		ColumnIndex = columnIndex;
		Row = row;
		Column = column;
		CellValue = cellValue;
		MouseEvent = mouseEvent;
	}
}
