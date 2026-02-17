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
/// Fluent builder for tab controls
/// </summary>
public sealed class TabControlBuilder
{
	private readonly TabControl _control = new();
	private int _initialActiveTab = 0;

	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private Color _backgroundColor = Color.Black;
	private Color _foregroundColor = Color.White;

	/// <summary>
	/// Adds a new tab to the control
	/// </summary>
	/// <param name="title">The title displayed in the tab header</param>
	/// <param name="content">The control to display when this tab is active</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder AddTab(string title, IWindowControl content)
	{
		_control.AddTab(title, content);
		return this;
	}

	/// <summary>
	/// Adds a new tab using a builder function
	/// </summary>
	/// <param name="title">The title displayed in the tab header</param>
	/// <param name="contentBuilder">A function that builds the tab content</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder AddTab(string title, Func<IWindowControl> contentBuilder)
	{
		_control.AddTab(title, contentBuilder());
		return this;
	}

	/// <summary>
	/// Adds multiple tabs at once
	/// </summary>
	/// <param name="tabs">Tuples of (title, content) for each tab</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder AddTabs(params (string title, IWindowControl content)[] tabs)
	{
		foreach (var (title, content) in tabs)
		{
			_control.AddTab(title, content);
		}
		return this;
	}

	/// <summary>
	/// Adds a tab only if the condition is true
	/// </summary>
	/// <param name="condition">Whether to add the tab</param>
	/// <param name="title">The title displayed in the tab header</param>
	/// <param name="content">The control to display when this tab is active</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder AddTabIf(bool condition, string title, IWindowControl content)
	{
		if (condition)
		{
			_control.AddTab(title, content);
		}
		return this;
	}

	/// <summary>
	/// Adds a tab only if the condition is true, using a builder function
	/// </summary>
	/// <param name="condition">Whether to add the tab</param>
	/// <param name="title">The title displayed in the tab header</param>
	/// <param name="contentBuilder">A function that builds the tab content</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder AddTabIf(bool condition, string title, Func<IWindowControl> contentBuilder)
	{
		if (condition)
		{
			_control.AddTab(title, contentBuilder());
		}
		return this;
	}

	/// <summary>
	/// Sets the initially active tab index (0-based)
	/// </summary>
	/// <param name="index">The index of the tab to activate initially</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithActiveTab(int index)
	{
		_initialActiveTab = index;
		return this;
	}

	/// <summary>
	/// Sets the header style for the tab control
	/// </summary>
	/// <param name="style">The header style to use</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithHeaderStyle(TabHeaderStyle style)
	{
		_control.HeaderStyle = style;
		return this;
	}

	/// <summary>
	/// Sets the height of the tab control
	/// </summary>
	/// <param name="height">The height in lines (minimum 2)</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithHeight(int height)
	{
		_control.Height = height;
		return this;
	}

	/// <summary>
	/// Sets the width of the tab control
	/// </summary>
	/// <param name="width">The width in characters</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithWidth(int width)
	{
		_control.Width = width;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	/// <param name="alignment">The alignment</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Centers the control horizontally
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder Centered()
	{
		_alignment = HorizontalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The vertical alignment</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Makes the control fill the available vertical space
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder Fill()
	{
		_verticalAlignment = VerticalAlignment.Fill;
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
	public TabControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides
	/// </summary>
	/// <param name="margin">The margin value</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	/// <param name="margin">The margin</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">True if visible</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the control name
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	/// <param name="tag">The tag object</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the background color
	/// </summary>
	/// <param name="color">The background color</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color
	/// </summary>
	/// <param name="color">The foreground color</param>
	/// <returns>The builder for chaining</returns>
	public TabControlBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Builds the tab control
	/// </summary>
	/// <returns>The configured TabControl</returns>
	public TabControl Build()
	{
		// Apply properties
		_control.HorizontalAlignment = _alignment;
		_control.VerticalAlignment = _verticalAlignment;
		_control.Margin = _margin;
		_control.Visible = _visible;
		_control.Name = _name;
		_control.Tag = _tag;
		_control.StickyPosition = _stickyPosition;
		_control.BackgroundColor = _backgroundColor;
		_control.ForegroundColor = _foregroundColor;

		// Set initial active tab
		if (_initialActiveTab >= 0 && _initialActiveTab < _control.TabPages.Count)
		{
			_control.ActiveTabIndex = _initialActiveTab;
		}

		return _control;
	}

	/// <summary>
	/// Implicit conversion to TabControl
	/// </summary>
	public static implicit operator TabControl(TabControlBuilder builder) => builder.Build();
}
