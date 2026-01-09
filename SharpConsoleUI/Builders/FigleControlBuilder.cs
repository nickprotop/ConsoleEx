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
			Tag = _tag
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
