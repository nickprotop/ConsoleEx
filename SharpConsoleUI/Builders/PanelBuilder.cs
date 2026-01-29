// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using Spectre.Console.Rendering;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating PanelControl instances.
/// </summary>
public sealed class PanelBuilder
{
	private IRenderable? _content;
	private BorderStyle _borderStyle = BorderStyle.Single;
	private Color? _borderColor;
	private string? _header;
	private Justify _headerAlignment = Justify.Left;
	private Spectre.Console.Padding _padding = new Spectre.Console.Padding(1, 0, 1, 0);
	private bool _useSafeBorder = false;
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

	/// <summary>
	/// Sets the content to display inside the panel.
	/// </summary>
	/// <param name="content">The Spectre renderable content.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithContent(IRenderable content)
	{
		_content = content;
		return this;
	}

	/// <summary>
	/// Sets the content to display inside the panel using markup text.
	/// </summary>
	/// <param name="text">The text to display (supports Spectre markup).</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithContent(string text)
	{
		_content = new Markup(text);
		return this;
	}

	/// <summary>
	/// Sets the border style for the panel.
	/// </summary>
	/// <param name="style">The border style.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithBorderStyle(BorderStyle style)
	{
		_borderStyle = style;
		return this;
	}

	/// <summary>
	/// Uses double-line border style.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder DoubleLine()
	{
		_borderStyle = BorderStyle.DoubleLine;
		return this;
	}

	/// <summary>
	/// Uses single-line border style.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder SingleLine()
	{
		_borderStyle = BorderStyle.Single;
		return this;
	}

	/// <summary>
	/// Uses rounded border style.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder Rounded()
	{
		_borderStyle = BorderStyle.Rounded;
		return this;
	}

	/// <summary>
	/// Uses no border.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder NoBorder()
	{
		_borderStyle = BorderStyle.None;
		return this;
	}

	/// <summary>
	/// Sets the border color.
	/// </summary>
	/// <param name="color">The border color.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithBorderColor(Color color)
	{
		_borderColor = color;
		return this;
	}

	/// <summary>
	/// Sets the header text displayed at the top of the panel border.
	/// </summary>
	/// <param name="header">The header text.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithHeader(string header)
	{
		_header = header;
		return this;
	}

	/// <summary>
	/// Sets the header alignment.
	/// </summary>
	/// <param name="alignment">The header alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithHeaderAlignment(Justify alignment)
	{
		_headerAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Aligns the header to the left.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder HeaderLeft()
	{
		_headerAlignment = Justify.Left;
		return this;
	}

	/// <summary>
	/// Centers the header.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder HeaderCenter()
	{
		_headerAlignment = Justify.Center;
		return this;
	}

	/// <summary>
	/// Aligns the header to the right.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder HeaderRight()
	{
		_headerAlignment = Justify.Right;
		return this;
	}

	/// <summary>
	/// Sets the padding inside the panel border.
	/// </summary>
	/// <param name="left">Left padding.</param>
	/// <param name="top">Top padding.</param>
	/// <param name="right">Right padding.</param>
	/// <param name="bottom">Bottom padding.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithPadding(int left, int top, int right, int bottom)
	{
		_padding = new Spectre.Console.Padding(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform padding on all sides.
	/// </summary>
	/// <param name="padding">The padding value.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithPadding(int padding)
	{
		_padding = new Spectre.Console.Padding(padding);
		return this;
	}

	/// <summary>
	/// Sets horizontal and vertical padding.
	/// </summary>
	/// <param name="horizontal">Horizontal padding (left and right).</param>
	/// <param name="vertical">Vertical padding (top and bottom).</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithPadding(int horizontal, int vertical)
	{
		_padding = new Spectre.Console.Padding(horizontal, vertical);
		return this;
	}

	/// <summary>
	/// Enables safe border characters for better terminal compatibility.
	/// </summary>
	/// <param name="useSafe">True to use safe borders.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder UseSafeBorder(bool useSafe = true)
	{
		_useSafeBorder = useSafe;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	/// <param name="alignment">The alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	/// <param name="alignment">The vertical alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets vertical alignment to Fill, causing the panel to stretch vertically.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder FillVertical()
	{
		_verticalAlignment = VerticalAlignment.Fill;
		return this;
	}

	/// <summary>
	/// Sets horizontal alignment to Stretch, causing the panel to stretch horizontally.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder StretchHorizontal()
	{
		_horizontalAlignment = HorizontalAlignment.Stretch;
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	/// <param name="left">Left margin.</param>
	/// <param name="top">Top margin.</param>
	/// <param name="right">Right margin.</param>
	/// <param name="bottom">Bottom margin.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides.
	/// </summary>
	/// <param name="margin">The margin value.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	/// <param name="margin">The margin.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	/// <param name="visible">True if visible.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width.
	/// </summary>
	/// <param name="width">The width.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the height.
	/// </summary>
	/// <param name="height">The height.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithHeight(int height)
	{
		_height = height;
		return this;
	}

	/// <summary>
	/// Sets the control name for FindControl queries.
	/// </summary>
	/// <param name="name">The control name.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets the control tag for custom data storage.
	/// </summary>
	/// <param name="tag">The tag object.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	/// <param name="position">The sticky position.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the background color.
	/// </summary>
	/// <param name="color">The background color.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color.
	/// </summary>
	/// <param name="color">The foreground color.</param>
	/// <returns>The builder for chaining.</returns>
	public PanelBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Builds the PanelControl.
	/// </summary>
	/// <returns>The configured control.</returns>
	public PanelControl Build()
	{
		var control = new PanelControl
		{
			Content = _content,
			BorderStyle = _borderStyle,
			BorderColor = _borderColor,
			Header = _header,
			HeaderAlignment = _headerAlignment,
			Padding = _padding,
			UseSafeBorder = _useSafeBorder,
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

		return control;
	}

	/// <summary>
	/// Implicit conversion to PanelControl.
	/// </summary>
	public static implicit operator PanelControl(PanelBuilder builder)
	{
		return builder.Build();
	}
}
