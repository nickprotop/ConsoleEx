// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Represents a row in a TableControl with cell data and optional styling.
/// </summary>
public class TableRow
{
	/// <summary>
	/// Gets the list of cell values for this row.
	/// </summary>
	public List<string> Cells { get; set; } = new();

	/// <summary>
	/// Gets or sets an arbitrary object associated with this row for user data.
	/// </summary>
	public object? Tag { get; set; }

	/// <summary>
	/// Gets or sets the background color for this row, overriding table defaults.
	/// </summary>
	public Color? BackgroundColor { get; set; }

	/// <summary>
	/// Gets or sets the foreground color for this row, overriding table defaults.
	/// </summary>
	public Color? ForegroundColor { get; set; }

	/// <summary>
	/// Gets or sets whether this row is enabled for interaction.
	/// </summary>
	public bool IsEnabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the rendered Y position (cached for mouse hit testing).
	/// </summary>
	internal int RenderedY { get; set; }

	/// <summary>
	/// Gets or sets the rendered height (cached for mouse hit testing).
	/// </summary>
	internal int RenderedHeight { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TableRow"/> class with no cells.
	/// </summary>
	public TableRow()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TableRow"/> class with the specified cells.
	/// </summary>
	public TableRow(params string[] cells)
	{
		Cells = new List<string>(cells);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TableRow"/> class with the specified cells.
	/// </summary>
	public TableRow(IEnumerable<string> cells)
	{
		Cells = new List<string>(cells);
	}

	/// <summary>
	/// Gets or sets the cell value at the specified index.
	/// </summary>
	public string this[int index]
	{
		get => Cells[index];
		set => Cells[index] = value;
	}
}
