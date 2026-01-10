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
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for rule controls (horizontal separator lines)
/// </summary>
public sealed class RuleBuilder
{
	private string? _title;
	private Justify _titleAlignment = Justify.Left;
	private Color? _color;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;

	/// <summary>
	/// Sets the title text displayed within the rule
	/// </summary>
	/// <param name="title">The title text</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithTitle(string title)
	{
		_title = title;
		return this;
	}

	/// <summary>
	/// Sets the title alignment
	/// </summary>
	/// <param name="alignment">The title alignment</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithTitleAlignment(Justify alignment)
	{
		_titleAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Aligns the title to the left
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder TitleLeft()
	{
		_titleAlignment = Justify.Left;
		return this;
	}

	/// <summary>
	/// Centers the title
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder TitleCenter()
	{
		_titleAlignment = Justify.Center;
		return this;
	}

	/// <summary>
	/// Aligns the title to the right
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder TitleRight()
	{
		_titleAlignment = Justify.Right;
		return this;
	}

	/// <summary>
	/// Sets the color of the rule line
	/// </summary>
	/// <param name="color">The line color</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithColor(Color color)
	{
		_color = color;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	/// <param name="alignment">The alignment</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The vertical alignment</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithVerticalAlignment(VerticalAlignment alignment)
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
	public RuleBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides
	/// </summary>
	/// <param name="margin">The margin value</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	/// <param name="margin">The margin</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">True if visible</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	/// <param name="width">The width</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for FindControl queries
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets the control tag for custom data storage
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public RuleBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Builds the rule control
	/// </summary>
	/// <returns>The configured control</returns>
	public RuleControl Build()
	{
		var control = new RuleControl
		{
			Title = _title,
			TitleAlignment = _titleAlignment,
			Color = _color,
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
		};

		return control;
	}

	/// <summary>
	/// Implicit conversion to RuleControl
	/// </summary>
	public static implicit operator RuleControl(RuleBuilder builder)
	{
		return builder.Build();
	}
}
