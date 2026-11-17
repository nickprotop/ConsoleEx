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

	private int? _minWidth;

	/// <summary>
	/// Gets or sets a minimum width floor applied when this column is auto-width and gets shrunk to
	/// fit the available space. Ignored for columns with a fixed <see cref="Width"/>. Null means no
	/// floor (the column can be shrunk down to 1 character, the previous fixed behavior). When the
	/// column's natural content would still be shrunk below this floor, the table instead allows its
	/// total content width to exceed the viewport so a horizontal scrollbar can be used to pan to it.
	/// </summary>
	public int? MinWidth
	{
		get => _minWidth;
		set { if (_minWidth == value) return; _minWidth = value; Owner?.OnColumnDisplayChanged(this, true, Invalidation.Relayout); }
	}

	private bool _wrap = false;

	/// <summary>
	/// Gets or sets whether text in this column wraps onto further lines instead of being clipped
	/// at the column's edge. Default is <c>false</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A wrapping column makes its row as tall as the wrapped value needs, up to
	/// <see cref="MaxLines"/>. Every other cell in that row is drawn on the row's first line
	/// unless <see cref="VerticalCenter"/> is set. Wrapping is word-based: a word is broken only
	/// when it alone is wider than the column.
	/// </para>
	/// <para>
	/// Each visible row of a wrapping column is measured on render, so the saving of leaving this
	/// off on a large table is real. It is off by default for that reason as well as for
	/// compatibility.
	/// </para>
	/// </remarks>
	public bool Wrap
	{
		get => _wrap;
		set { if (_wrap == value) return; _wrap = value; Owner?.OnColumnDisplayChanged(this, true, Invalidation.Relayout); }
	}

	/// <summary>
	/// Gets or sets whether text in this column should not wrap. This is the inverse of
	/// <see cref="Wrap"/>: the two share one backing field and cannot disagree.
	/// </summary>
	/// <remarks>
	/// Before the release that added <see cref="Wrap"/> this property was read by nothing, so it
	/// reported <c>false</c> while no column ever wrapped. It now defaults to <c>true</c>, which
	/// is what it always meant. No table renders differently; only the value reported changed.
	/// </remarks>
	public bool NoWrap
	{
		get => !Wrap;
		set => Wrap = !value;
	}

	private int _maxLines = 0;

	/// <summary>
	/// Gets or sets the most lines a wrapping cell in this column may occupy. <c>0</c>, the
	/// default, means no limit. Ignored when <see cref="Wrap"/> is <c>false</c>.
	/// </summary>
	/// <remarks>
	/// When a value needs more lines than this allows, the last line kept ends with
	/// <see cref="SharpConsoleUI.Configuration.ControlDefaults.TableWrapEllipsis"/> to show that
	/// content was dropped. A value that fits within the limit gets no ellipsis.
	/// </remarks>
	public int MaxLines
	{
		get => _maxLines;
		set { if (_maxLines == value) return; _maxLines = Math.Max(0, value); Owner?.OnColumnDisplayChanged(this, true, Invalidation.Relayout); }
	}

	private bool _verticalCenter = false;

	/// <summary>
	/// Gets or sets whether this column's cell is centred vertically within a row taller than one
	/// line. Default is <c>false</c>, which puts it on the row's first line.
	/// </summary>
	/// <remarks>
	/// Only has an effect in a row made tall by some other column's wrapping. It moves a cell
	/// within a height that is already decided, so it never changes layout.
	/// </remarks>
	public bool VerticalCenter
	{
		get => _verticalCenter;
		set { if (_verticalCenter == value) return; _verticalCenter = value; Owner?.OnColumnDisplayChanged(this, false, Invalidation.Repaint); }
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
