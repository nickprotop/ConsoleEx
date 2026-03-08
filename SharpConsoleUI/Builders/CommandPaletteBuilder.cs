// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Events;

#pragma warning disable CS1591

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for constructing a <see cref="CommandPaletteControl"/>.
/// </summary>
public sealed class CommandPaletteBuilder
{
	private readonly List<CommandPaletteItem> _items = new();
	private string? _placeholder;
	private int? _maxVisibleItems;
	private bool _showCategories;
	private bool _showShortcuts = true;
	private int? _width;
	private string? _name;
	private EventHandler<CommandPaletteItem>? _itemSelectedHandler;
	private WindowEventHandler<CommandPaletteItem>? _itemSelectedWithWindowHandler;
	private EventHandler? _dismissedHandler;
	private EventHandler<string>? _searchChangedHandler;

	public CommandPaletteBuilder WithItems(IEnumerable<CommandPaletteItem> items)
	{
		_items.AddRange(items);
		return this;
	}

	public CommandPaletteBuilder AddItem(string label, Action action, string? shortcut = null, string? category = null, string? icon = null)
	{
		_items.Add(new CommandPaletteItem(label, action)
		{
			Shortcut = shortcut,
			Category = category,
			Icon = icon
		});
		return this;
	}

	public CommandPaletteBuilder AddItem(CommandPaletteItem item)
	{
		_items.Add(item);
		return this;
	}

	public CommandPaletteBuilder WithPlaceholder(string placeholder)
	{
		_placeholder = placeholder;
		return this;
	}

	public CommandPaletteBuilder WithMaxVisibleItems(int count)
	{
		_maxVisibleItems = count;
		return this;
	}

	public CommandPaletteBuilder WithShowCategories(bool show = true)
	{
		_showCategories = show;
		return this;
	}

	public CommandPaletteBuilder WithShowShortcuts(bool show = true)
	{
		_showShortcuts = show;
		return this;
	}

	public CommandPaletteBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	public CommandPaletteBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	public CommandPaletteBuilder OnItemSelected(EventHandler<CommandPaletteItem> handler)
	{
		_itemSelectedHandler = handler;
		return this;
	}

	public CommandPaletteBuilder OnItemSelected(WindowEventHandler<CommandPaletteItem> handler)
	{
		_itemSelectedWithWindowHandler = handler;
		return this;
	}

	public CommandPaletteBuilder OnDismissed(EventHandler handler)
	{
		_dismissedHandler = handler;
		return this;
	}

	public CommandPaletteBuilder OnSearchChanged(EventHandler<string> handler)
	{
		_searchChangedHandler = handler;
		return this;
	}

	public CommandPaletteControl Build()
	{
		var palette = new CommandPaletteControl(_items)
		{
			ShowCategories = _showCategories,
			ShowShortcuts = _showShortcuts,
			Name = _name
		};

		if (_placeholder != null)
			palette.Placeholder = _placeholder;
		if (_maxVisibleItems.HasValue)
			palette.MaxVisibleItems = _maxVisibleItems.Value;
		if (_width.HasValue)
			palette.PaletteWidth = _width.Value;

		if (_itemSelectedHandler != null)
			palette.ItemSelected += _itemSelectedHandler;

		if (_itemSelectedWithWindowHandler != null)
		{
			palette.ItemSelected += (sender, item) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_itemSelectedWithWindowHandler(sender, item, window);
			};
		}

		if (_dismissedHandler != null)
			palette.Dismissed += _dismissedHandler;

		if (_searchChangedHandler != null)
			palette.SearchChanged += _searchChangedHandler;

		return palette;
	}

	public static implicit operator CommandPaletteControl(CommandPaletteBuilder builder) => builder.Build();
}
