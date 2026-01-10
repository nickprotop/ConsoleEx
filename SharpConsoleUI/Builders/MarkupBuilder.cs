// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for markup controls
/// </summary>
public sealed class MarkupBuilder
{
	private readonly List<string> _lines = new();
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;

	/// <summary>
	/// Adds a line of markup text
	/// </summary>
	/// <param name="markup">The markup text</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder AddLine(string markup)
	{
		_lines.Add(markup ?? string.Empty);
		return this;
	}

	/// <summary>
	/// Adds multiple lines of markup text
	/// </summary>
	/// <param name="markupLines">The markup lines</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder AddLines(params string[] markupLines)
	{
		foreach (var line in markupLines)
		{
			AddLine(line);
		}
		return this;
	}

	/// <summary>
	/// Adds an empty line
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder AddEmptyLine()
	{
		_lines.Add(string.Empty);
		return this;
	}

	/// <summary>
	/// Clears all lines
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder Clear()
	{
		_lines.Clear();
		return this;
	}

	/// <summary>
	/// Sets the alignment
	/// </summary>
	/// <param name="alignment">The alignment</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Centers the content
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder Centered()
	{
		_alignment = HorizontalAlignment.Center;
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
	public MarkupBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	/// <param name="margin">The margin value for all sides</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">Whether the control is visible</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	/// <param name="width">The control width</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Builds the markup control
	/// </summary>
	/// <returns>The configured markup control</returns>
	public MarkupControl Build()
	{
		var markup = new MarkupControl(_lines.ToList())
		{
			HorizontalAlignment = _alignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
		};

		return markup;
	}

	/// <summary>
	/// Implicit conversion to MarkupControl
	/// </summary>
	/// <param name="builder">The builder</param>
	/// <returns>The built markup control</returns>
	public static implicit operator MarkupControl(MarkupBuilder builder) => builder.Build();
}
