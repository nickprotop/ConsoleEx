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
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private EventHandler<int>? _selectionChangedHandler;
	private EventHandler<DropdownItem?>? _selectedItemChangedHandler;
	private WindowEventHandler<int>? _selectionChangedWithWindowHandler;
	private WindowEventHandler<DropdownItem?>? _selectedItemChangedWithWindowHandler;
	private EventHandler<string?>? _selectedValueChangedHandler;
	private WindowEventHandler<string?>? _selectedValueChangedWithWindowHandler;
	private EventHandler? _gotFocusHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;

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
	/// Sets the vertical alignment
	/// </summary>
	public DropdownBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	public DropdownBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	public DropdownBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	public DropdownBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
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
	/// Sets the selected value changed event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the selected value changes</param>
	/// <returns>The builder for chaining</returns>
	public DropdownBuilder OnSelectedValueChanged(EventHandler<string?> handler)
	{
		_selectedValueChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the selected value changed event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public DropdownBuilder OnSelectedValueChanged(WindowEventHandler<string?> handler)
	{
		_selectedValueChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the dropdown receives focus</param>
	/// <returns>The builder for chaining</returns>
	public DropdownBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public DropdownBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the dropdown loses focus</param>
	/// <returns>The builder for chaining</returns>
	public DropdownBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public DropdownBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
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
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
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

		// Attach SelectedValueChanged handlers
		if (_selectedValueChangedHandler != null)
		{
			dropdown.SelectedValueChanged += _selectedValueChangedHandler;
		}

		if (_selectedValueChangedWithWindowHandler != null)
		{
			dropdown.SelectedValueChanged += (sender, value) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_selectedValueChangedWithWindowHandler(sender, value, window);
			};
		}

		// Attach GotFocus handlers
		if (_gotFocusHandler != null)
		{
			dropdown.GotFocus += _gotFocusHandler;
		}

		if (_gotFocusWithWindowHandler != null)
		{
			dropdown.GotFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_gotFocusWithWindowHandler(sender, e, window);
			};
		}

		// Attach LostFocus handlers
		if (_lostFocusHandler != null)
		{
			dropdown.LostFocus += _lostFocusHandler;
		}

		if (_lostFocusWithWindowHandler != null)
		{
			dropdown.LostFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_lostFocusWithWindowHandler(sender, e, window);
			};
		}

		return dropdown;
	}

	/// <summary>
	/// Implicit conversion to DropdownControl
	/// </summary>
	public static implicit operator DropdownControl(DropdownBuilder builder) => builder.Build();
}
