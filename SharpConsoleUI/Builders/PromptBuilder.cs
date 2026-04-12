// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for prompt controls
/// </summary>
public sealed class PromptBuilder : IControlBuilder<PromptControl>
{
	private string _prompt = "> ";
	private string _initialInput = "";
	private bool _unfocusOnEnter = true;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private EventHandler<string>? _enteredHandler;
	private EventHandler<string>? _inputChangedHandler;
	private WindowEventHandler<string>? _enteredWithWindowHandler;
	private WindowEventHandler<string>? _inputChangedWithWindowHandler;
	private EventHandler? _gotFocusHandler;
	private WindowEventHandler<EventArgs>? _gotFocusWithWindowHandler;
	private EventHandler? _lostFocusHandler;
	private WindowEventHandler<EventArgs>? _lostFocusWithWindowHandler;
	private char? _maskCharacter;
	private int? _inputWidth;
	private bool _historyEnabled;
	private Func<string, int, IEnumerable<string>?>? _tabCompleter;
	private Color? _inputBackgroundColor;
	private Color? _inputFocusedBackgroundColor;
	private Color? _inputForegroundColor;
	private Color? _inputFocusedForegroundColor;

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

	/// <summary>Sets the mask character for password fields.</summary>
	public PromptBuilder WithMaskCharacter(char mask)
	{
		_maskCharacter = mask;
		return this;
	}

	/// <summary>Sets the input field width in characters (enables horizontal scrolling).</summary>
	public PromptBuilder WithInputWidth(int width)
	{
		_inputWidth = Math.Max(1, width);
		return this;
	}

	/// <summary>Enables command history (Up/Down arrow recall).</summary>
	public PromptBuilder WithHistory(bool enabled = true)
	{
		_historyEnabled = enabled;
		return this;
	}

	/// <summary>Sets the tab completion delegate.</summary>
	public PromptBuilder WithTabCompleter(Func<string, int, IEnumerable<string>?> completer)
	{
		_tabCompleter = completer;
		return this;
	}

	/// <summary>Sets the input background color when not focused.</summary>
	public PromptBuilder WithInputBackgroundColor(Color color) { _inputBackgroundColor = color; return this; }
	/// <summary>Sets the input background color when focused.</summary>
	public PromptBuilder WithInputFocusedBackgroundColor(Color color) { _inputFocusedBackgroundColor = color; return this; }
	/// <summary>Sets the input foreground color when not focused.</summary>
	public PromptBuilder WithInputForegroundColor(Color color) { _inputForegroundColor = color; return this; }
	/// <summary>Sets the input foreground color when focused.</summary>
	public PromptBuilder WithInputFocusedForegroundColor(Color color) { _inputFocusedForegroundColor = color; return this; }

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
	/// Sets the vertical alignment
	/// </summary>
	public PromptBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the sticky position
	/// </summary>
	public PromptBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	public PromptBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	public PromptBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
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
	/// Sets the GotFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the prompt receives focus</param>
	/// <returns>The builder for chaining</returns>
	public PromptBuilder OnGotFocus(EventHandler handler)
	{
		_gotFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the GotFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public PromptBuilder OnGotFocus(WindowEventHandler<EventArgs> handler)
	{
		_gotFocusWithWindowHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler
	/// </summary>
	/// <param name="handler">The event handler to invoke when the prompt loses focus</param>
	/// <returns>The builder for chaining</returns>
	public PromptBuilder OnLostFocus(EventHandler handler)
	{
		_lostFocusHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LostFocus event handler with window access
	/// </summary>
	/// <param name="handler">Handler that receives sender, event data, and window</param>
	/// <returns>The builder for chaining</returns>
	public PromptBuilder OnLostFocus(WindowEventHandler<EventArgs> handler)
	{
		_lostFocusWithWindowHandler = handler;
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
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
		};

		if (_maskCharacter.HasValue)
			prompt.MaskCharacter = _maskCharacter.Value;
		if (_inputWidth.HasValue)
			prompt.InputWidth = _inputWidth.Value;
		if (_historyEnabled)
			prompt.HistoryEnabled = true;
		if (_tabCompleter != null)
			prompt.TabCompleter = _tabCompleter;
		if (_inputBackgroundColor.HasValue)
			prompt.InputBackgroundColor = _inputBackgroundColor.Value;
		if (_inputFocusedBackgroundColor.HasValue)
			prompt.InputFocusedBackgroundColor = _inputFocusedBackgroundColor.Value;
		if (_inputForegroundColor.HasValue)
			prompt.InputForegroundColor = _inputForegroundColor.Value;
		if (_inputFocusedForegroundColor.HasValue)
			prompt.InputFocusedForegroundColor = _inputFocusedForegroundColor.Value;

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

		// Attach GotFocus handlers
		if (_gotFocusHandler != null)
		{
			prompt.GotFocus += _gotFocusHandler;
		}

		if (_gotFocusWithWindowHandler != null)
		{
			prompt.GotFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_gotFocusWithWindowHandler(sender, e, window);
			};
		}

		// Attach LostFocus handlers
		if (_lostFocusHandler != null)
		{
			prompt.LostFocus += _lostFocusHandler;
		}

		if (_lostFocusWithWindowHandler != null)
		{
			prompt.LostFocus += (sender, e) =>
			{
				var window = (sender as IWindowControl)?.GetParentWindow();
				if (window != null)
					_lostFocusWithWindowHandler(sender, e, window);
			};
		}

		BindingHelper.ApplyDeferredBindings(this, prompt);
		return prompt;
	}

	/// <summary>
	/// Implicit conversion to PromptControl
	/// </summary>
	public static implicit operator PromptControl(PromptBuilder builder) => builder.Build();
}
