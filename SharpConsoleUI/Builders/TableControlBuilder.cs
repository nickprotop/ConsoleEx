// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using TableColumn = SharpConsoleUI.Controls.TableColumn;
using TableRow = SharpConsoleUI.Controls.TableRow;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating TableControl instances with comprehensive configuration.
/// </summary>
public sealed class TableControlBuilder
{
	private readonly List<TableColumn> _columns = new();
	private readonly List<TableRow> _rows = new();
	private BorderStyle _borderStyle = BorderStyle.Single;
	private Color? _borderColor;
	private Color? _headerBackgroundColor;
	private Color? _headerForegroundColor;
	private bool _showHeader = true;
	private bool _showRowSeparators = false;
	private bool _useSafeBorder = false;
	private string? _title;
	private Justify _titleAlignment = Justify.Center;

	// Base properties
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private int? _height;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private Color? _backgroundColor;
	private Color? _foregroundColor;

	#region Column Configuration

	/// <summary>
	/// Adds a column to the table.
	/// </summary>
	public TableControlBuilder AddColumn(string header, Justify alignment = Justify.Left, int? width = null)
	{
		_columns.Add(new TableColumn(header, alignment, width));
		return this;
	}

	/// <summary>
	/// Adds a custom column to the table.
	/// </summary>
	public TableControlBuilder AddColumn(TableColumn column)
	{
		_columns.Add(column);
		return this;
	}

	/// <summary>
	/// Adds multiple columns from header names.
	/// </summary>
	public TableControlBuilder WithColumns(params string[] headers)
	{
		foreach (var header in headers)
			_columns.Add(new TableColumn(header));
		return this;
	}

	#endregion

	#region Row Data

	/// <summary>
	/// Adds a row with the specified cells.
	/// </summary>
	public TableControlBuilder AddRow(params string[] cells)
	{
		_rows.Add(new TableRow(cells));
		return this;
	}

	/// <summary>
	/// Adds a custom row to the table.
	/// </summary>
	public TableControlBuilder AddRow(TableRow row)
	{
		_rows.Add(row);
		return this;
	}

	/// <summary>
	/// Adds multiple rows to the table.
	/// </summary>
	public TableControlBuilder WithRows(IEnumerable<TableRow> rows)
	{
		_rows.AddRange(rows);
		return this;
	}

	#endregion

	#region Border Styling

	/// <summary>
	/// Sets the border style.
	/// </summary>
	public TableControlBuilder WithBorderStyle(BorderStyle style)
	{
		_borderStyle = style;
		return this;
	}

	/// <summary>
	/// Uses double-line border style.
	/// </summary>
	public TableControlBuilder DoubleLine()
	{
		_borderStyle = BorderStyle.DoubleLine;
		return this;
	}

	/// <summary>
	/// Uses single-line border style.
	/// </summary>
	public TableControlBuilder SingleLine()
	{
		_borderStyle = BorderStyle.Single;
		return this;
	}

	/// <summary>
	/// Uses rounded border style.
	/// </summary>
	public TableControlBuilder Rounded()
	{
		_borderStyle = BorderStyle.Rounded;
		return this;
	}

	/// <summary>
	/// Uses double-line border style.
	/// </summary>
	public TableControlBuilder DoubleLineBorder()
	{
		_borderStyle = BorderStyle.DoubleLine;
		return this;
	}

	/// <summary>
	/// Uses no border.
	/// </summary>
	public TableControlBuilder NoBorder()
	{
		_borderStyle = BorderStyle.None;
		return this;
	}

	/// <summary>
	/// Sets the border color.
	/// </summary>
	public TableControlBuilder WithBorderColor(Color color)
	{
		_borderColor = color;
		return this;
	}

	/// <summary>
	/// Enables safe border characters for compatibility.
	/// </summary>
	public TableControlBuilder WithSafeBorder(bool useSafe = true)
	{
		_useSafeBorder = useSafe;
		return this;
	}

	#endregion

	#region Header Configuration

	/// <summary>
	/// Shows or hides the header row.
	/// </summary>
	public TableControlBuilder ShowHeader(bool show = true)
	{
		_showHeader = show;
		return this;
	}

	/// <summary>
	/// Hides the header row.
	/// </summary>
	public TableControlBuilder HideHeader()
	{
		_showHeader = false;
		return this;
	}

	/// <summary>
	/// Sets the header colors.
	/// </summary>
	public TableControlBuilder WithHeaderColors(Color foreground, Color background)
	{
		_headerForegroundColor = foreground;
		_headerBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the table title.
	/// </summary>
	public TableControlBuilder WithTitle(string title, Justify alignment = Justify.Center)
	{
		_title = title;
		_titleAlignment = alignment;
		return this;
	}

	#endregion

	#region Row Configuration

	/// <summary>
	/// Shows or hides row separators.
	/// </summary>
	public TableControlBuilder ShowRowSeparators(bool show = true)
	{
		_showRowSeparators = show;
		return this;
	}

	#endregion


	#region Layout Configuration

	/// <summary>
	/// Sets the explicit width.
	/// </summary>
	public TableControlBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the explicit height.
	/// </summary>
	public TableControlBuilder WithHeight(int height)
	{
		_height = height;
		return this;
	}

	/// <summary>
	/// Sets the margin around the table.
	/// </summary>
	public TableControlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the margin with separate horizontal and vertical values.
	/// </summary>
	public TableControlBuilder WithMargin(int horizontal, int vertical)
	{
		_margin = new Margin(horizontal, vertical, horizontal, vertical);
		return this;
	}

	/// <summary>
	/// Sets the margin with individual values.
	/// </summary>
	public TableControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Centers the table horizontally and vertically.
	/// </summary>
	public TableControlBuilder Centered()
	{
		_horizontalAlignment = HorizontalAlignment.Center;
		_verticalAlignment = VerticalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Centers the table horizontally.
	/// </summary>
	public TableControlBuilder CenterHorizontal()
	{
		_horizontalAlignment = HorizontalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Centers the table vertically.
	/// </summary>
	public TableControlBuilder CenterVertical()
	{
		_verticalAlignment = VerticalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Stretches the table horizontally to fill available width.
	/// </summary>
	public TableControlBuilder StretchHorizontal()
	{
		_horizontalAlignment = HorizontalAlignment.Stretch;
		return this;
	}

	/// <summary>
	/// Aligns the table to the bottom vertically.
	/// </summary>
	public TableControlBuilder AlignBottom()
	{
		_verticalAlignment = VerticalAlignment.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	public TableControlBuilder WithHorizontalAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	public TableControlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	public TableControlBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the table stick to the top during scrolling.
	/// </summary>
	public TableControlBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the table stick to the bottom during scrolling.
	/// </summary>
	public TableControlBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	#endregion

	#region Colors

	/// <summary>
	/// Sets the background color.
	/// </summary>
	public TableControlBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground (text) color.
	/// </summary>
	public TableControlBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets both foreground and background colors.
	/// </summary>
	public TableControlBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	#endregion

	#region Base Properties

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	public TableControlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets the tag for custom data.
	/// </summary>
	public TableControlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	public TableControlBuilder WithVisibility(bool visible)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Hides the table.
	/// </summary>
	public TableControlBuilder Hidden()
	{
		_visible = false;
		return this;
	}

	#endregion

	#region Build

	/// <summary>
	/// Builds the TableControl with all configured options.
	/// </summary>
	public TableControl Build()
	{
		var table = new TableControl
		{
			BorderStyle = _borderStyle,
			BorderColor = _borderColor,
			HeaderBackgroundColor = _headerBackgroundColor,
			HeaderForegroundColor = _headerForegroundColor,
			ShowHeader = _showHeader,
			ShowRowSeparators = _showRowSeparators,
			UseSafeBorder = _useSafeBorder,
			Title = _title,
			TitleAlignment = _titleAlignment,

			// Base properties
			HorizontalAlignment = _horizontalAlignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Height = _height,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition,
			BackgroundColor = _backgroundColor,
			ForegroundColor = _foregroundColor
		};

		// Add columns and rows
		foreach (var column in _columns)
			table.AddColumn(column);

		foreach (var row in _rows)
			table.AddRow(row);

		return table;
	}

	/// <summary>
	/// Implicit conversion to TableControl.
	/// </summary>
	public static implicit operator TableControl(TableControlBuilder builder) => builder.Build();

	#endregion
}
