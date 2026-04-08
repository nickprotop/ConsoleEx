// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Represents a column configuration in a TableControl.
/// </summary>
public class TableColumn
{
	/// <summary>
	/// Gets or sets the header text for this column.
	/// </summary>
	public string Header { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the text alignment for cells in this column.
	/// </summary>
	public TextJustification Alignment { get; set; } = TextJustification.Left;

	/// <summary>
	/// Gets or sets the fixed width for this column. Null means auto-width.
	/// </summary>
	public int? Width { get; set; }

	/// <summary>
	/// Gets or sets whether text in this column should not wrap.
	/// </summary>
	public bool NoWrap { get; set; } = false;

	/// <summary>
	/// Gets or sets the foreground color for the header text.
	/// </summary>
	public Color? HeaderColor { get; set; }

	/// <summary>
	/// Gets or sets an arbitrary object associated with this column for user data.
	/// </summary>
	public object? Tag { get; set; }

	/// <summary>
	/// Gets or sets a custom comparer for sorting this column by cell string values.
	/// When null, default string comparison is used.
	/// </summary>
	public IComparer<string>? CustomComparer { get; set; }

	/// <summary>
	/// Gets or sets a custom comparer for sorting this column by full row data.
	/// Takes precedence over CustomComparer when set. Receives the full TableRow
	/// objects, allowing sort by Tag or any row property.
	/// </summary>
	public Comparison<TableRow>? CustomRowComparer { get; set; }

	/// <summary>
	/// Gets or sets whether this column supports sorting.
	/// Default is true.
	/// </summary>
	public bool IsSortable { get; set; } = true;

	/// <summary>
	/// Gets or sets the rendered width (cached for mouse hit testing).
	/// </summary>
	internal int RenderedWidth { get; set; }

	/// <summary>
	/// Gets or sets the rendered X position (cached for mouse hit testing).
	/// </summary>
	internal int RenderedX { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TableColumn"/> class with default settings.
	/// </summary>
	public TableColumn()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TableColumn"/> class with the specified header.
	/// </summary>
	public TableColumn(string header)
	{
		Header = header;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TableColumn"/> class with the specified header and alignment.
	/// </summary>
	public TableColumn(string header, TextJustification alignment)
	{
		Header = header;
		Alignment = alignment;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TableColumn"/> class with the specified header, alignment, and width.
	/// </summary>
	public TableColumn(string header, TextJustification alignment, int? width)
	{
		Header = header;
		Alignment = alignment;
		Width = width;
	}
}
