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

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for Figlet ASCII art controls
/// </summary>
public sealed class FigleControlBuilder
{
	private string? _text;
	private Color? _color;
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private bool _rightPadded = true;
	private FigletSize _size = FigletSize.Medium;
	private FigletFont? _customFont;
	private string? _fontPath;
	private ShadowStyle _shadowStyle = ShadowStyle.None;
	private Color? _shadowColor;
	private int _shadowOffsetX = 1;
	private int _shadowOffsetY = 1;

	/// <summary>
	/// Sets the FIGlet text to render
	/// </summary>
	/// <param name="text">The text to render as ASCII art</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithText(string text)
	{
		_text = text;
		return this;
	}

	/// <summary>
	/// Sets the FIGlet text color
	/// </summary>
	/// <param name="color">The text color</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithColor(Color color)
	{
		_color = color;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	/// <param name="alignment">The horizontal alignment</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Centers the FIGlet text horizontally
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder Centered()
	{
		_horizontalAlignment = HorizontalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The vertical alignment</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	/// <param name="left">Left margin</param>
	/// <param name="top">Top margin</param>
	/// <param name="right">Right margin</param>
	/// <param name="bottom">Bottom margin</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	/// <param name="margin">The margin value for all sides</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	/// <param name="width">The control width</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">Whether the control is visible</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets whether the right side should be padded
	/// </summary>
	/// <param name="rightPadded">Whether to pad the right side</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithRightPadded(bool rightPadded)
	{
		_rightPadded = rightPadded;
		return this;
	}

	/// <summary>
	/// Sets the FIGlet text size
	/// </summary>
	/// <param name="size">The font size</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithSize(FigletSize size)
	{
		_size = size;
		return this;
	}

	/// <summary>
	/// Sets the FIGlet text to use small font
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder Small()
	{
		_size = FigletSize.Small;
		return this;
	}

	/// <summary>
	/// Sets the FIGlet text to use large font
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder Large()
	{
		_size = FigletSize.Large;
		return this;
	}

	/// <summary>
	/// Sets a custom FigletFont instance
	/// </summary>
	/// <param name="font">The custom font</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithCustomFont(FigletFont font)
	{
		_customFont = font;
		return this;
	}

	/// <summary>
	/// Sets the path to a custom .flf font file
	/// </summary>
	/// <param name="fontPath">Path to the .flf font file</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithFontPath(string fontPath)
	{
		_fontPath = fontPath;
		return this;
	}

	/// <summary>
	/// Sets the shadow style
	/// </summary>
	/// <param name="style">The shadow style</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithShadow(ShadowStyle style)
	{
		_shadowStyle = style;
		return this;
	}

	/// <summary>
	/// Adds drop shadow effect
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithDropShadow()
	{
		_shadowStyle = ShadowStyle.DropShadow;
		return this;
	}

	/// <summary>
	/// Adds outline effect
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithOutline()
	{
		_shadowStyle = ShadowStyle.Outline;
		return this;
	}

	/// <summary>
	/// Adds 3D extrusion effect
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder With3D()
	{
		_shadowStyle = ShadowStyle.Extrude3D;
		return this;
	}

	/// <summary>
	/// Sets the shadow color
	/// </summary>
	/// <param name="color">The shadow color</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithShadowColor(Color color)
	{
		_shadowColor = color;
		return this;
	}

	/// <summary>
	/// Sets the shadow offset
	/// </summary>
	/// <param name="offsetX">Horizontal offset</param>
	/// <param name="offsetY">Vertical offset</param>
	/// <returns>The builder for chaining</returns>
	public FigleControlBuilder WithShadowOffset(int offsetX, int offsetY)
	{
		_shadowOffsetX = offsetX;
		_shadowOffsetY = offsetY;
		return this;
	}

	/// <summary>
	/// Builds the Figlet control
	/// </summary>
	/// <returns>The configured Figlet control</returns>
	public FigleControl Build()
	{
		var control = new FigleControl
		{
			Text = _text,
			HorizontalAlignment = _horizontalAlignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition,
			RightPadded = _rightPadded,
			Size = _size,
			CustomFont = _customFont,
			FontPath = _fontPath,
			ShadowStyle = _shadowStyle,
			ShadowColor = _shadowColor,
			ShadowOffsetX = _shadowOffsetX,
			ShadowOffsetY = _shadowOffsetY
		};

		if (_color.HasValue)
			control.Color = _color.Value;

		return control;
	}

	/// <summary>
	/// Implicit conversion to FigleControl
	/// </summary>
	/// <param name="builder">The builder</param>
	/// <returns>The built Figlet control</returns>
	public static implicit operator FigleControl(FigleControlBuilder builder) => builder.Build();
}
