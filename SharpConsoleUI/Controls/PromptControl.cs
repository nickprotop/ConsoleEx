// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A single-line text input control with optional prompt text.
	/// Supports text editing, cursor navigation, and horizontal scrolling for overflow text.
	/// </summary>
	public class PromptControl : IWindowControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider, ICursorShapeProvider
	{
		/// <summary>
		/// Event fired when Enter is pressed (modern standardized event)
		/// </summary>
		public event EventHandler<string>? Entered;

		/// <summary>
		/// Event fired when input text changes (modern standardized event)
		/// </summary>
		public event EventHandler<string>? InputChanged;
		private List<string>? _cachedContent;
		private string _input = string.Empty;
		private Color? _inputBackgroundColor;
		private Color? _inputFocusedBackgroundColor;
		private Color? _inputFocusedForegroundColor;
		private Color? _inputForegroundColor;
		private int? _inputWidth;
		private Alignment _justify = Alignment.Left;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private string? _prompt;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		// Convenience property to access EditStateService
		private EditStateService? EditService => Container?.GetConsoleWindowSystem?.EditStateService;

		// Read-only helpers that read from state services (single source of truth)
		private int CurrentCursorPosition => EditService?.GetCursorPosition(this).Column ?? 0;
		private int CurrentScrollOffset => EditService?.GetEditState(this).HorizontalScrollOffset ?? 0;

		/// <summary>
		/// Gets the actual rendered width of the control content in characters.
		/// </summary>
		public int? ActualWidth
		{
			get
			{
				if (_cachedContent == null) return null;

				int maxLength = 0;
				foreach (var line in _cachedContent)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		/// <summary>
		/// Gets or sets the text alignment within the control.
		/// </summary>
		public Alignment Alignment
		{ get => _justify; set { _justify = value; _cachedContent = null; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public IContainer? Container { get; set; }
		
		private bool _hasFocus;

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;

				// Invalidate cached content to trigger re-rendering when focus changes
				_cachedContent = null;

				// Sync editing mode with EditStateService
				EditService?.SetEditingMode(this, value);

				// Fire focus events
				if (value && !hadFocus)
				{
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
				else if (!value && hadFocus)
				{
					LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets the preferred cursor shape - always VerticalBar for text input
		/// </summary>
		public CursorShape? PreferredCursorShape => CursorShape.VerticalBar;

		/// <summary>
		/// Gets or sets the background color of the input area when not focused.
		/// </summary>
		public Color? InputBackgroundColor
		{
			get => _inputBackgroundColor;
			set
			{
				_inputBackgroundColor = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the input area when focused.
		/// </summary>
		public Color? InputFocusedBackgroundColor
		{
			get => _inputFocusedBackgroundColor;
			set
			{
				_inputFocusedBackgroundColor = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the input text when focused.
		/// </summary>
		public Color? InputFocusedForegroundColor
		{
			get => _inputFocusedForegroundColor;
			set
			{
				_inputFocusedForegroundColor = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the input text when not focused.
		/// </summary>
		public Color? InputForegroundColor
		{
			get => _inputForegroundColor;
			set
			{
				_inputForegroundColor = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the width of the input area in characters. When set, enables horizontal scrolling.
		/// </summary>
		public int? InputWidth
		{
			get => _inputWidth;
			set
			{
				_inputWidth = value.HasValue ? Math.Max(1, value.Value) : value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;

		/// <inheritdoc/>
		public Margin Margin
		{ get => _margin; set { _margin = value; _cachedContent = null; Container?.Invalidate(true); } }



		/// <summary>
		/// Gets or sets the prompt text displayed before the input area.
		/// </summary>
		public string? Prompt
		{ get => _prompt; set { _prompt = value; _cachedContent = null; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets whether the control loses focus when Enter is pressed.
		/// </summary>
		public bool UnfocusOnEnter { get; set; } = true;

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; _cachedContent = null; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
				if (_width != validatedValue)
				{
					_width = validatedValue;
					_cachedContent = null;
					Container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
		}

		/// <inheritdoc/>
		public Point? GetLogicalCursorPosition()
		{
			// Return the visual cursor position within the input field
			// Account for scroll offset to get the position relative to visible content
			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty);
			int visualCursorX = promptLength + (CurrentCursorPosition - CurrentScrollOffset);
			return new Point(visualCursorX, 0);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			// Return the size of the prompt content (prompt + input area)
			string fullContent = (_prompt ?? string.Empty) + _input;
			int width = Math.Max(AnsiConsoleHelper.StripSpectreLength(fullContent), _width ?? 0);
			return new System.Drawing.Size(width, 1); // Single line control
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			// Calculate cursor position within the input field (excluding prompt length)
			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty);
			int inputCursorPos = Math.Max(0, position.X - promptLength);

			// Clamp to valid input range
			int newCursorPos = Math.Max(0, Math.Min(inputCursorPos, _input.Length));
			EditService?.SetCursorPosition(this, 0, newCursorPos);

			// Update scroll offset if needed
			if (_inputWidth.HasValue)
			{
				int scrollOffset = CurrentScrollOffset;
				if (newCursorPos < scrollOffset)
				{
					SetScrollOffset(newCursorPos);
				}
				else if (newCursorPos >= scrollOffset + _inputWidth.Value)
				{
					SetScrollOffset(newCursorPos - _inputWidth.Value + 1);
				}
			}

			Container?.Invalidate(false, this);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			_cachedContent = null;
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			int cursorPos = CurrentCursorPosition;
			int scrollOffset = CurrentScrollOffset;

			if (key.Key == ConsoleKey.Enter)
			{
				Entered?.Invoke(this, _input);
				if (UnfocusOnEnter)
				{
					EditService?.SetCursorPosition(this, 0, 0);
					HasFocus = false;
				}
				_cachedContent = null;
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Backspace && cursorPos > 0)
			{
				_input = _input.Remove(cursorPos - 1, 1);
				int newCursorPos = cursorPos - 1;
				EditService?.SetCursorPosition(this, 0, newCursorPos);
				if (newCursorPos < scrollOffset + (_inputWidth ?? _input.Length))
				{
					SetScrollOffset(scrollOffset - 1);
				}
				_cachedContent = null;
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Delete && cursorPos < _input.Length)
			{
				_input = _input.Remove(cursorPos, 1);
				_cachedContent = null;
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Home)
			{
				EditService?.SetCursorPosition(this, 0, 0);
				SetScrollOffset(0);
				_cachedContent = null;
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.End)
			{
				EditService?.SetCursorPosition(this, 0, _input.Length);
				SetScrollOffset(Math.Max(0, _input.Length - (_inputWidth ?? _input.Length)));
				_cachedContent = null;
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.LeftArrow && cursorPos > 0)
			{
				int newCursorPos = cursorPos - 1;
				EditService?.SetCursorPosition(this, 0, newCursorPos);
				if (newCursorPos < scrollOffset + (_inputWidth ?? _input.Length))
				{
					SetScrollOffset(scrollOffset - 1);
				}
				_cachedContent = null;
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.RightArrow && cursorPos < _input.Length)
			{
				int newCursorPos = cursorPos + 1;
				EditService?.SetCursorPosition(this, 0, newCursorPos);
				if (newCursorPos >= (scrollOffset + (_inputWidth ?? _input.Length)))
				{
					SetScrollOffset(scrollOffset + 1);
				}
				_cachedContent = null;
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.Escape)
			{
				HasFocus = false;
				_cachedContent = null;
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			else if (!char.IsControl(key.KeyChar))
			{
				_input = _input.Insert(cursorPos, key.KeyChar.ToString());
				int newCursorPos = cursorPos + 1;
				EditService?.SetCursorPosition(this, 0, newCursorPos);
				if (_inputWidth.HasValue && newCursorPos > _inputWidth.Value)
				{
					SetScrollOffset(newCursorPos - _inputWidth.Value);
				}
				_cachedContent = null;
				Container?.Invalidate(true);
				InputChanged?.Invoke(this, _input);
				return true;
			}
			return false;
		}

		/// <inheritdoc/>
		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			var layoutService = Container?.GetConsoleWindowSystem?.LayoutStateService;

			// Smart invalidation: check if re-render is needed due to size change
			if (layoutService == null || layoutService.NeedsRerender(this, availableWidth, availableHeight))
			{
				// Dimensions changed - invalidate cached content
				_cachedContent = null;
			}
			else
			{
				// Dimensions unchanged - return cached content if available
				if (_cachedContent != null) return _cachedContent;
			}

			// Update available space tracking
			layoutService?.UpdateAvailableSpace(this, availableWidth, availableHeight, LayoutChangeReason.ContainerResize);

			_cachedContent = new List<string>();

			int maxContentWidth = _width ?? (AnsiConsoleHelper.StripSpectreLength(_prompt + _input));
			int paddingLeft = 0;
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, maxContentWidth);
			}

			int scrollOffset = CurrentScrollOffset;
			string visibleInput = _input;
			if (_inputWidth.HasValue && _input.Length > _inputWidth.Value)
			{
				int maxLength = Math.Min(_inputWidth.Value, _input.Length - scrollOffset);
				if (maxLength > 0 && scrollOffset < _input.Length)
				{
					visibleInput = _input.Substring(scrollOffset, maxLength);
				}
				else
				{
					visibleInput = string.Empty;
				}
			}

			Color inputBackgroundColor = HasFocus ? InputFocusedBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedBackgroundColor ?? Color.White : InputBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputBackgroundColor ?? Color.Black;
			Color inputForegroundColor = HasFocus ? InputFocusedForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedForegroundColor ?? Color.Black : InputForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputForegroundColor ?? Color.White;

			int paddingRight = _inputWidth ?? ((_width ?? availableWidth ?? 50) - AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty));
			if (paddingRight < 0) paddingRight = 0;

			int rightWhiteSpace = Math.Max(0, paddingRight - visibleInput.Length);

			_cachedContent = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{_prompt}[{inputForegroundColor} on {inputBackgroundColor}]{visibleInput}{new string(' ', rightWhiteSpace)}[/]", (_width ?? (availableWidth ?? 50)), availableHeight, true, Container?.BackgroundColor, Container?.ForegroundColor);

			for (int i = 0; i < _cachedContent.Count; i++)
			{
				_cachedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + _cachedContent[i];
			}

			return _cachedContent;
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;
		
		/// <summary>
		/// Occurs when the control receives focus.
		/// </summary>
		public event EventHandler? GotFocus;

		/// <summary>
		/// Occurs when the control loses focus.
		/// </summary>
		public event EventHandler? LostFocus;
		
		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}

		/// <summary>
		/// Sets the input text and positions the cursor at the end.
		/// </summary>
		/// <param name="input">The text to set as input.</param>
		public void SetInput(string? input)
		{
			int newCursorPos = string.IsNullOrEmpty(input) ? 0 : input.Length;

			_cachedContent = null;
			_input = input ?? string.Empty;

			// Set cursor and scroll via services (single source of truth)
			EditService?.SetCursorPosition(this, 0, newCursorPos);
			EditService?.SetScrollPosition(this, 0, 0);

			Container?.Invalidate(true);
			InputChanged?.Invoke(this, _input);
		}

		private void SetScrollOffset(int value)
		{
			int newOffset = Math.Max(0, value);
			// Set scroll position via service (single source of truth)
			EditService?.SetScrollPosition(this, newOffset, 0);
		}
	}
}