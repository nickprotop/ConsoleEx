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
/// Event arguments for table row events (click, double-click, activation).
/// </summary>
public class TableRowEventArgs : EventArgs
{
	/// <summary>
	/// Gets the row index.
	/// </summary>
	public int RowIndex { get; }

	/// <summary>
	/// Gets the row data.
	/// </summary>
	public TableRow Row { get; }

	/// <summary>
	/// Gets the mouse event that triggered this row event, if applicable.
	/// </summary>
	public MouseEventArgs? MouseEvent { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TableRowEventArgs"/> class.
	/// </summary>
	public TableRowEventArgs(int rowIndex, TableRow row, MouseEventArgs? mouseEvent)
	{
		RowIndex = rowIndex;
		Row = row;
		MouseEvent = mouseEvent;
	}
}
