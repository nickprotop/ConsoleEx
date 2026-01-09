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
/// Fluent builder for dropdown controls
/// </summary>
public sealed class DropdownBuilder
{
	private readonly List<DropdownItem> _items = new();
	private string _prompt = "Select...";
	private int _selectedIndex = -1;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private EventHandler<int>? _selectionChangedHandler;
	private EventHandler<DropdownItem?>? _selectedItemChangedHandler;
	private WindowEventHandler<int>? _selectionChangedWithWindowHandler;
	private WindowEventHandler<DropdownItem?>? _selectedItemChangedWithWindowHandler;

	/// <summary>
	/// Sets the dropdown prompt text
	/// </summary>
	public DropdownBuilder WithPrompt(string prompt)
	{
		_prompt = prompt;
		return this;
	}

	/// <summary>
	/// Adds an item to the dropdown
	/// </summary>
	public DropdownBuilder AddItem(string text, string? value = null, Color? color = null)
	{
		_items.Add(new DropdownItem(text, value ?? text, color));
		return this;
	}

	/// <summary>
	/// Adds a DropdownItem to the dropdown
	/// </summary>
	public DropdownBuilder AddItem(DropdownItem item)
	{
		_items.Add(item);
		return this;
	}

	/// <summary>
	/// Adds multiple items to the dropdown
	/// </summary>
	public DropdownBuilder AddItems(params string[] items)
	{
		foreach (var item in items)
			_items.Add(new DropdownItem(item, item, null));
		return this;
	}

	/// <summary>
	/// Adds multiple DropdownItems to the dropdown
	/// </summary>
	public DropdownBuilder AddItems(IEnumerable<DropdownItem> items)
	{
		_items.AddRange(items);
		return this;
	}

	/// <summary>
	/// Sets the initially selected index
	/// </summary>
	public DropdownBuilder SelectedIndex(int index)
	{
		_selectedIndex = index;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	public DropdownBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	public DropdownBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	public DropdownBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	public DropdownBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	public DropdownBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	public DropdownBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	public DropdownBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the selection changed event handler (index-based)
	/// </summary>
	public DropdownBuilder OnSelectionChanged(EventHandler<int> handler)
	{
		_selectionChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selection changed event handler with window access
	/// </summary>
	public DropdownBuilder OnSelectionChanged(WindowEventHandler<int> handler)
	{
		_selectionChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected item changed event handler
	/// </summary>
	public DropdownBuilder OnSelectedItemChanged(EventHandler<DropdownItem?> handler)
	{
		_selectedItemChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected item changed event handler with window access
	/// </summary>
	public DropdownBuilder OnSelectedItemChanged(WindowEventHandler<DropdownItem?> handler)
	{
		_selectedItemChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the dropdown control
	/// </summary>
	public DropdownControl Build()
	{
		var dropdown = new DropdownControl(_prompt)
		{
			HorizontalAlignment = _alignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag
		};

		foreach (var item in _items)
			dropdown.AddItem(item);

		if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
			dropdown.SelectedIndex = _selectedIndex;

		// Attach standard handlers
		if (_selectionChangedHandler != null)
			dropdown.SelectedIndexChanged += _selectionChangedHandler;
		if (_selectedItemChangedHandler != null)
			dropdown.SelectedItemChanged += _selectedItemChangedHandler;

		// Attach window-aware handlers
		if (_selectionChangedWithWindowHandler != null)
		{
			dropdown.SelectedIndexChanged += (sender, idx) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_selectionChangedWithWindowHandler(sender, idx, window);
			};
		}
		if (_selectedItemChangedWithWindowHandler != null)
		{
			dropdown.SelectedItemChanged += (sender, item) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_selectedItemChangedWithWindowHandler(sender, item, window);
			};
		}

		return dropdown;
	}

	/// <summary>
	/// Implicit conversion to DropdownControl
	/// </summary>
	public static implicit operator DropdownControl(DropdownBuilder builder) => builder.Build();
}
