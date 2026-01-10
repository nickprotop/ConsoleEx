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
/// Fluent builder for checkbox controls
/// </summary>
public sealed class CheckboxBuilder
{
	private string _label = "Checkbox";
	private bool _isChecked = false;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private EventHandler<bool>? _checkedChangedHandler;
	private WindowEventHandler<bool>? _checkedChangedWithWindowHandler;
	private EventHandler? _gotFocusHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;

	/// <summary>
	/// Sets the checkbox label
	/// </summary>
	public CheckboxBuilder WithLabel(string label)
	{
		_label = label;
		return this;
	}

	/// <summary>
	/// Sets the checked state
	/// </summary>
	public CheckboxBuilder Checked(bool isChecked = true)
	{
		_isChecked = isChecked;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	public CheckboxBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	public CheckboxBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	public CheckboxBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	public CheckboxBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	public CheckboxBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	public CheckboxBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	public CheckboxBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the checked changed event handler
	/// </summary>
	public CheckboxBuilder OnCheckedChanged(EventHandler<bool> handler)
	{
		_checkedChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the checked changed event handler with window access
	/// </summary>
	public CheckboxBuilder OnCheckedChanged(WindowEventHandler<bool> handler)
	{
		_checkedChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the checkbox receives focus</param>
	/// <returns>The builder for chaining</returns>
	public CheckboxBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public CheckboxBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the checkbox loses focus</param>
	/// <returns>The builder for chaining</returns>
	public CheckboxBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public CheckboxBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the checkbox control
	/// </summary>
	public CheckboxControl Build()
	{
		var checkbox = new CheckboxControl(_label, _isChecked)
		{
			HorizontalAlignment = _alignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag
		};

		// Attach standard handler
		if (_checkedChangedHandler != null)
			checkbox.CheckedChanged += _checkedChangedHandler;

		// Attach window-aware handler
		if (_checkedChangedWithWindowHandler != null)
		{
			checkbox.CheckedChanged += (sender, isChecked) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_checkedChangedWithWindowHandler(sender, isChecked, window);
			};
		}

		// Attach GotFocus handlers
		if (_gotFocusHandler != null)
		{
			checkbox.GotFocus += _gotFocusHandler;
		}

		if (_gotFocusWithWindowHandler != null)
		{
			checkbox.GotFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_gotFocusWithWindowHandler(sender, e, window);
			};
		}

		// Attach LostFocus handlers
		if (_lostFocusHandler != null)
		{
			checkbox.LostFocus += _lostFocusHandler;
		}

		if (_lostFocusWithWindowHandler != null)
		{
			checkbox.LostFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_lostFocusWithWindowHandler(sender, e, window);
			};
		}

		return checkbox;
	}

	/// <summary>
	/// Implicit conversion to CheckboxControl
	/// </summary>
	public static implicit operator CheckboxControl(CheckboxBuilder builder) => builder.Build();
}
