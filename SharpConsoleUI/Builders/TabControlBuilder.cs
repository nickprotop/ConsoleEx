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
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for <see cref="TabControl"/> construction.
/// </summary>
public sealed class TabControlBuilder
{
	private readonly List<(string title, Action<TabPage>? configure)> _tabs = new();
	private Margin _margin = new(0, 0, 0, 0);
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Stretch;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Fill;
	private int? _height;
	private int? _width;
	private bool _visible = true;
	private bool _showContentBorder = true;
	private TabStripAlignment _tabStripAlignment = TabStripAlignment.Left;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private Color? _tabHeaderActiveBackgroundColor;
	private Color? _tabHeaderActiveForegroundColor;
	private Color? _tabHeaderBackgroundColor;
	private Color? _tabHeaderForegroundColor;
	private Color? _tabContentBorderColor;
	private EventHandler? _gotFocusHandler;
	private EventHandler? _lostFocusHandler;
	private EventHandler<TabSelectedEventArgs>? _tabChangedHandler;

	/// <summary>
	/// Adds a tab with the specified title.
	/// </summary>
	public TabControlBuilder AddTab(string title)
	{
		_tabs.Add((title, null));
		return this;
	}

	/// <summary>
	/// Adds a tab with the specified title and a configuration callback.
	/// </summary>
	public TabControlBuilder AddTab(string title, Action<TabPage> configure)
	{
		_tabs.Add((title, configure));
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	public TabControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin.
	/// </summary>
	public TabControlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	public TabControlBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	public TabControlBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	public TabControlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the height.
	/// </summary>
	public TabControlBuilder WithHeight(int height)
	{
		_height = Math.Max(1, height);
		return this;
	}

	/// <summary>
	/// Sets the width.
	/// </summary>
	public TabControlBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	public TabControlBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets whether to show a content border.
	/// </summary>
	public TabControlBuilder WithContentBorder(bool showBorder = true)
	{
		_showContentBorder = showBorder;
		return this;
	}

	/// <summary>
	/// Sets the tab strip alignment.
	/// </summary>
	public TabControlBuilder WithTabStripAlignment(TabStripAlignment alignment)
	{
		_tabStripAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the control name.
	/// </summary>
	public TabControlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object.
	/// </summary>
	public TabControlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	public TabControlBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top.
	/// </summary>
	public TabControlBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom.
	/// </summary>
	public TabControlBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the background color.
	/// </summary>
	public TabControlBuilder WithBackgroundColor(Color? color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground color.
	/// </summary>
	public TabControlBuilder WithForegroundColor(Color? color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the active tab header background color.
	/// </summary>
	public TabControlBuilder WithActiveTabColor(Color backgroundColor, Color foregroundColor)
	{
		_tabHeaderActiveBackgroundColor = backgroundColor;
		_tabHeaderActiveForegroundColor = foregroundColor;
		return this;
	}

	/// <summary>
	/// Sets the inactive tab header colors.
	/// </summary>
	public TabControlBuilder WithInactiveTabColor(Color backgroundColor, Color foregroundColor)
	{
		_tabHeaderBackgroundColor = backgroundColor;
		_tabHeaderForegroundColor = foregroundColor;
		return this;
	}

	/// <summary>
	/// Sets the content border color.
	/// </summary>
	public TabControlBuilder WithBorderColor(Color color)
	{
		_tabContentBorderColor = color;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler.
	/// </summary>
	public TabControlBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler.
	/// </summary>
	public TabControlBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the SelectedTabChanged event handler.
	/// </summary>
	public TabControlBuilder OnTabChanged(EventHandler<TabSelectedEventArgs> handler)
	{
		_tabChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the TabControl.
	/// </summary>
	public TabControl Build()
	{
		var tabControl = new TabControl
		{
			Margin = _margin,
			HorizontalAlignment = _horizontalAlignment,
			VerticalAlignment = _verticalAlignment,
			Height = _height,
			Width = _width,
			Visible = _visible,
			ShowContentBorder = _showContentBorder,
			TabStripAlignment = _tabStripAlignment,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
		};

		if (_backgroundColor.HasValue)
			tabControl.BackgroundColor = _backgroundColor.Value;
		if (_foregroundColor.HasValue)
			tabControl.ForegroundColor = _foregroundColor.Value;
		if (_tabHeaderActiveBackgroundColor.HasValue)
			tabControl.TabHeaderActiveBackgroundColor = _tabHeaderActiveBackgroundColor.Value;
		if (_tabHeaderActiveForegroundColor.HasValue)
			tabControl.TabHeaderActiveForegroundColor = _tabHeaderActiveForegroundColor.Value;
		if (_tabHeaderBackgroundColor.HasValue)
			tabControl.TabHeaderBackgroundColor = _tabHeaderBackgroundColor.Value;
		if (_tabHeaderForegroundColor.HasValue)
			tabControl.TabHeaderForegroundColor = _tabHeaderForegroundColor.Value;
		if (_tabContentBorderColor.HasValue)
			tabControl.TabContentBorderColor = _tabContentBorderColor.Value;

		foreach (var (title, configure) in _tabs)
		{
			var page = tabControl.AddTab(title);
			configure?.Invoke(page);
		}

		if (_gotFocusHandler != null)
			tabControl.GotFocus += _gotFocusHandler;
		if (_lostFocusHandler != null)
			tabControl.LostFocus += _lostFocusHandler;
		if (_tabChangedHandler != null)
			tabControl.SelectedTabChanged += _tabChangedHandler;

		return tabControl;
	}

	/// <summary>
	/// Implicit conversion to TabControl.
	/// </summary>
	public static implicit operator TabControl(TabControlBuilder builder) => builder.Build();
}
