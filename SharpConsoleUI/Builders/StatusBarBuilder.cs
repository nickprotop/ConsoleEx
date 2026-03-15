// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for <see cref="StatusBarControl"/>.
/// Methods follow standard builder naming (AddLeft, WithBackgroundColor, etc.).
/// </summary>
#pragma warning disable CS1591 // Builder methods are self-documenting
public sealed class StatusBarBuilder : IControlBuilder<StatusBarControl>
{
	private readonly List<(StatusBarItem Item, char Zone)> _items = new();
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private Color? _shortcutForegroundColor;
	private Margin _margin = new(0, 0, 0, 0);
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.Bottom;
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Stretch;
	private int? _itemSpacing;
	private string? _separatorChar;
	private string? _shortcutLabelSeparator;
	private bool _showAboveLine;
	private Color? _aboveLineColor;
	private EventHandler<StatusBarItemClickedEventArgs>? _itemClickedHandler;

	#region Add Left

	public StatusBarBuilder AddLeft(string shortcut, string label, Action? onClick = null)
	{
		_items.Add((new StatusBarItem { Shortcut = shortcut, Label = label, OnClick = onClick }, 'L'));
		return this;
	}

	public StatusBarBuilder AddLeft(StatusBarItem item)
	{
		_items.Add((item, 'L'));
		return this;
	}

	public StatusBarBuilder AddLeftText(string text, Action? onClick = null)
	{
		_items.Add((new StatusBarItem { Label = text, OnClick = onClick }, 'L'));
		return this;
	}

	public StatusBarBuilder AddLeftSeparator()
	{
		_items.Add((new StatusBarItem { IsSeparator = true }, 'L'));
		return this;
	}

	#endregion

	#region Add Center

	public StatusBarBuilder AddCenter(string shortcut, string label, Action? onClick = null)
	{
		_items.Add((new StatusBarItem { Shortcut = shortcut, Label = label, OnClick = onClick }, 'C'));
		return this;
	}

	public StatusBarBuilder AddCenter(StatusBarItem item)
	{
		_items.Add((item, 'C'));
		return this;
	}

	public StatusBarBuilder AddCenterText(string text, Action? onClick = null)
	{
		_items.Add((new StatusBarItem { Label = text, OnClick = onClick }, 'C'));
		return this;
	}

	public StatusBarBuilder AddCenterSeparator()
	{
		_items.Add((new StatusBarItem { IsSeparator = true }, 'C'));
		return this;
	}

	#endregion

	#region Add Right

	public StatusBarBuilder AddRight(string shortcut, string label, Action? onClick = null)
	{
		_items.Add((new StatusBarItem { Shortcut = shortcut, Label = label, OnClick = onClick }, 'R'));
		return this;
	}

	public StatusBarBuilder AddRight(StatusBarItem item)
	{
		_items.Add((item, 'R'));
		return this;
	}

	public StatusBarBuilder AddRightText(string text, Action? onClick = null)
	{
		_items.Add((new StatusBarItem { Label = text, OnClick = onClick }, 'R'));
		return this;
	}

	public StatusBarBuilder AddRightSeparator()
	{
		_items.Add((new StatusBarItem { IsSeparator = true }, 'R'));
		return this;
	}

	#endregion

	#region Configuration

	public StatusBarBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	public StatusBarBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	public StatusBarBuilder WithShortcutForegroundColor(Color color)
	{
		_shortcutForegroundColor = color;
		return this;
	}

	public StatusBarBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	public StatusBarBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	public StatusBarBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	public StatusBarBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	public StatusBarBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	public StatusBarBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	public StatusBarBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	public StatusBarBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	public StatusBarBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return this;
	}

	public StatusBarBuilder WithItemSpacing(int spacing)
	{
		_itemSpacing = Math.Max(0, spacing);
		return this;
	}

	public StatusBarBuilder WithSeparatorChar(string separator)
	{
		_separatorChar = separator;
		return this;
	}

	public StatusBarBuilder WithShortcutLabelSeparator(string separator)
	{
		_shortcutLabelSeparator = separator;
		return this;
	}

	public StatusBarBuilder WithAboveLine(bool show = true)
	{
		_showAboveLine = show;
		return this;
	}

	public StatusBarBuilder WithAboveLineColor(Color color)
	{
		_aboveLineColor = color;
		_showAboveLine = true;
		return this;
	}

	public StatusBarBuilder OnItemClicked(EventHandler<StatusBarItemClickedEventArgs> handler)
	{
		_itemClickedHandler = handler;
		return this;
	}

	#endregion

	#region Build

	public StatusBarControl Build()
	{
		var bar = new StatusBarControl
		{
			Margin = _margin,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition,
			HorizontalAlignment = _horizontalAlignment,
		};

		if (_backgroundColor.HasValue) bar.BackgroundColor = _backgroundColor.Value;
		if (_foregroundColor.HasValue) bar.ForegroundColor = _foregroundColor.Value;
		if (_shortcutForegroundColor.HasValue) bar.ShortcutForegroundColor = _shortcutForegroundColor.Value;
		if (_itemSpacing.HasValue) bar.ItemSpacing = _itemSpacing.Value;
		if (_separatorChar != null) bar.SeparatorChar = _separatorChar;
		if (_shortcutLabelSeparator != null) bar.ShortcutLabelSeparator = _shortcutLabelSeparator;
		bar.ShowAboveLine = _showAboveLine;
		if (_aboveLineColor.HasValue) bar.AboveLineColor = _aboveLineColor;

		bar.BatchUpdate(() =>
		{
			foreach (var (item, zone) in _items)
			{
				switch (zone)
				{
					case 'L': bar.AddLeft(item); break;
					case 'C': bar.AddCenter(item); break;
					case 'R': bar.AddRight(item); break;
				}
			}
		});

		if (_itemClickedHandler != null)
			bar.ItemClicked += _itemClickedHandler;

		BindingHelper.ApplyDeferredBindings(this, bar);
		return bar;
	}

	/// <summary>
	/// Implicit conversion to StatusBarControl.
	/// </summary>
	public static implicit operator StatusBarControl(StatusBarBuilder builder) => builder.Build();
#pragma warning restore CS1591

	#endregion
}
