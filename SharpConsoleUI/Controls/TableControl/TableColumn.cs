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
	/// The owning <see cref="TableControl"/>, set when the column is added. Used to route
	/// display-property changes back to the table for cache busting and invalidation.
	/// </summary>
	internal TableControl? Owner;

	private string _header = string.Empty;

	/// <summary>
	/// Gets or sets the header text for this column.
	/// </summary>
	public string Header
	{
		get => _header;
		set { if (_header == value) return; _header = value; Owner?.OnColumnDisplayChanged(this, true, Invalidation.Relayout); }
	}

	private TextJustification _alignment = TextJustification.Left;

	/// <summary>
	/// Gets or sets the text alignment for cells in this column.
	/// </summary>
	public TextJustification Alignment
	{
		get => _alignment;
		set { if (_alignment == value) return; _alignment = value; Owner?.OnColumnDisplayChanged(this, false, Invalidation.Relayout); }
	}

	private int? _width;

	/// <summary>
	/// Gets or sets the fixed width for this column. Null means auto-width.
	/// </summary>
	public int? Width
	{
		get => _width;
		set { if (_width == value) return; _width = value; Owner?.OnColumnDisplayChanged(this, true, Invalidation.Relayout); }
	}

	private bool _noWrap = false;

	/// <summary>
	/// Gets or sets whether text in this column should not wrap.
	/// </summary>
	public bool NoWrap
	{
		get => _noWrap;
		set { if (_noWrap == value) return; _noWrap = value; Owner?.OnColumnDisplayChanged(this, false, Invalidation.Relayout); }
	}

	private Color? _headerColor;

	/// <summary>
	/// Gets or sets the foreground color for the header text.
	/// </summary>
	public Color? HeaderColor
	{
		get => _headerColor;
		set { if (_headerColor == value) return; _headerColor = value; Owner?.OnColumnDisplayChanged(this, false, Invalidation.Repaint); }
	}

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
