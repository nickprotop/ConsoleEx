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
/// Fluent builder for prompt controls
/// </summary>
public sealed class PromptBuilder
{
	private string _prompt = "> ";
	private string _initialInput = "";
	private bool _unfocusOnEnter = true;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private EventHandler<string>? _enteredHandler;
	private EventHandler<string>? _inputChangedHandler;
	private WindowEventHandler<string>? _enteredWithWindowHandler;
	private WindowEventHandler<string>? _inputChangedWithWindowHandler;

	/// <summary>
	/// Sets the prompt text (displayed before the input area)
	/// </summary>
	public PromptBuilder WithPrompt(string prompt)
	{
		_prompt = prompt;
		return this;
	}

	/// <summary>
	/// Sets the initial input value
	/// </summary>
	public PromptBuilder WithInput(string input)
	{
		_initialInput = input;
		return this;
	}

	/// <summary>
	/// Sets whether the control loses focus when Enter is pressed
	/// </summary>
	public PromptBuilder UnfocusOnEnter(bool unfocus = true)
	{
		_unfocusOnEnter = unfocus;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment
	/// </summary>
	public PromptBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin
	/// </summary>
	public PromptBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin
	/// </summary>
	public PromptBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	public PromptBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the width
	/// </summary>
	public PromptBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup
	/// </summary>
	public PromptBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object
	/// </summary>
	public PromptBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the entered event handler (fired when Enter is pressed)
	/// </summary>
	public PromptBuilder OnEntered(EventHandler<string> handler)
	{
		_enteredHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the entered event handler with window access
	/// </summary>
	public PromptBuilder OnEntered(WindowEventHandler<string> handler)
	{
		_enteredWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the input changed event handler (fired when text changes)
	/// </summary>
	public PromptBuilder OnInputChanged(EventHandler<string> handler)
	{
		_inputChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the input changed event handler with window access
	/// </summary>
	public PromptBuilder OnInputChanged(WindowEventHandler<string> handler)
	{
		_inputChangedWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the prompt control
	/// </summary>
	public PromptControl Build()
	{
		var prompt = new PromptControl
		{
			Prompt = _prompt,
			UnfocusOnEnter = _unfocusOnEnter,
			HorizontalAlignment = _alignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag
		};

		if (!string.IsNullOrEmpty(_initialInput))
			prompt.SetInput(_initialInput);

		// Attach standard handlers
		if (_enteredHandler != null)
			prompt.Entered += _enteredHandler;
		if (_inputChangedHandler != null)
			prompt.InputChanged += _inputChangedHandler;

		// Attach window-aware handlers
		if (_enteredWithWindowHandler != null)
		{
			prompt.Entered += (sender, text) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_enteredWithWindowHandler(sender, text, window);
			};
		}
		if (_inputChangedWithWindowHandler != null)
		{
			prompt.InputChanged += (sender, text) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_inputChangedWithWindowHandler(sender, text, window);
			};
		}

		return prompt;
	}

	/// <summary>
	/// Implicit conversion to PromptControl
	/// </summary>
	public static implicit operator PromptControl(PromptBuilder builder) => builder.Build();
}
