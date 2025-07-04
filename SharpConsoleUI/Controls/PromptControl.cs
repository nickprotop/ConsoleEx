// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	public class PromptControl : IWIndowControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider
	{
		public Action<PromptControl, string>? OnEnter;
		private List<string>? _cachedContent;
		private int _cursorPosition = 0;
		private string _input = string.Empty;
		private Color? _inputBackgroundColor;
		private Color? _inputFocusedBackgroundColor;
		private Color? _inputFocusedForegroundColor;
		private Color? _inputForegroundColor;
		private int? _inputWidth;
		private Alignment _justify = Alignment.Left;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private string? _prompt;
		private int _scrollOffset = 0;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

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

		public Alignment Alignment
		{ get => _justify; set { _justify = value; _cachedContent = null; Container?.Invalidate(true); } }

		public IContainer? Container { get; set; }
		
		private bool _hasFocus;
		public bool HasFocus 
		{ 
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;
				
				// Invalidate cached content to trigger re-rendering when focus changes
				_cachedContent = null;
				
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

		public int? InputWidth
		{
			get => _inputWidth;
			set
			{
				_inputWidth = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool IsEnabled { get; set; } = true;

		public Margin Margin
		{ get => _margin; set { _margin = value; _cachedContent = null; Container?.Invalidate(true); } }

		public Action<PromptControl, string>? OnInputChange { get; set; }

		public string? Prompt
		{ get => _prompt; set { _prompt = value; _cachedContent = null; Container?.Invalidate(true); } }

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public object? Tag { get; set; }
		public bool UnfocusOnEnter { get; set; } = true;

		public bool Visible
		{ get => _visible; set { _visible = value; _cachedContent = null; Container?.Invalidate(true); } }

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(true); } }

		public void Dispose()
		{
			Container = null;
		}

		// ILogicalCursorProvider implementation
		public Point? GetLogicalCursorPosition()
		{
			// Return the logical cursor position within the input field
			// This is the cursor position in the content coordinate system
			return new Point(AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty) + _cursorPosition, 0);
		}

		public System.Drawing.Size GetLogicalContentSize()
		{
			// Return the size of the prompt content (prompt + input area)
			string fullContent = (_prompt ?? string.Empty) + _input;
			int width = Math.Max(AnsiConsoleHelper.StripSpectreLength(fullContent), _width ?? 0);
			return new System.Drawing.Size(width, 1); // Single line control
		}

		public void SetLogicalCursorPosition(Point position)
		{
			// Calculate cursor position within the input field (excluding prompt length)
			int promptLength = AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty);
			int inputCursorPos = Math.Max(0, position.X - promptLength);
			
			// Clamp to valid input range
			_cursorPosition = Math.Max(0, Math.Min(inputCursorPos, _input.Length));
			
			// Update scroll offset if needed
			if (_inputWidth.HasValue)
			{
				if (_cursorPosition < _scrollOffset)
				{
					SetScrollOffset(_cursorPosition);
				}
				else if (_cursorPosition >= _scrollOffset + _inputWidth.Value)
				{
					SetScrollOffset(_cursorPosition - _inputWidth.Value + 1);
				}
			}
			
			Container?.Invalidate(false, this);
		}

		public void Invalidate()
		{
			_cachedContent = null;
		}

		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (key.Key == ConsoleKey.Enter)
			{
				OnEnter?.Invoke(this, _input);
				if (UnfocusOnEnter)
				{
					_cursorPosition = 0;
					HasFocus = false;
				}
				Container?.Invalidate(true);
				OnInputChange?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Backspace && _cursorPosition > 0)
			{
				_input = _input.Remove(_cursorPosition - 1, 1);
				_cursorPosition--;
				if (_cursorPosition < _scrollOffset + (_inputWidth ?? _input.Length))
				{
					SetScrollOffset(_scrollOffset - 1);
				}
				Container?.Invalidate(true);
				OnInputChange?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Delete && _cursorPosition < _input.Length)
			{
				_input = _input.Remove(_cursorPosition, 1);
				Container?.Invalidate(true);
				OnInputChange?.Invoke(this, _input);
				return true;
			}
			else if (key.Key == ConsoleKey.Home)
			{
				_cursorPosition = 0;
				SetScrollOffset(0);
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.End)
			{
				_cursorPosition = _input.Length;
				SetScrollOffset(Math.Max(0, _input.Length - (_inputWidth ?? _input.Length)));
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.LeftArrow && _cursorPosition > 0)
			{
				_cursorPosition--;
				if (_cursorPosition < _scrollOffset + (_inputWidth ?? _input.Length))
				{
					SetScrollOffset(_scrollOffset - 1);
				}
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.RightArrow && _cursorPosition < _input.Length)
			{
				_cursorPosition++;
				if (_cursorPosition >= (_scrollOffset + (_inputWidth ?? _input.Length)))
				{
					SetScrollOffset(_scrollOffset + 1);
				}
				Container?.Invalidate(true);
				return true;
			}
			else if (key.Key == ConsoleKey.Escape)
			{
				HasFocus = false;
				Container?.Invalidate(true);
				OnInputChange?.Invoke(this, _input);
				return true;
			}
			else if (!char.IsControl(key.KeyChar))
			{
				_input = _input.Insert(_cursorPosition, key.KeyChar.ToString());
				_cursorPosition++;
				if (_inputWidth.HasValue && _cursorPosition > _inputWidth.Value)
				{
					SetScrollOffset(_cursorPosition - _inputWidth.Value);
				}
				Container?.Invalidate(true);
				OnInputChange?.Invoke(this, _input);
				return true;
			}
			return false;
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (_cachedContent != null) return _cachedContent;

			_cachedContent = new List<string>();

			int maxContentWidth = _width ?? (AnsiConsoleHelper.StripSpectreLength(_prompt + _input));
			int paddingLeft = 0;
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, maxContentWidth);
			}

			string visibleInput = _input;
			if (_inputWidth.HasValue && _input.Length > _inputWidth.Value)
			{
				visibleInput = _input.Substring(_scrollOffset, _inputWidth.Value);
			}

			Color inputBackgroundColor = HasFocus ? InputFocusedBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedBackgroundColor ?? Color.White : InputBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputBackgroundColor ?? Color.Black;
			Color inputForegroundColor = HasFocus ? InputFocusedForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedForegroundColor ?? Color.Black : InputBackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.PromptInputForegroundColor ?? Color.White;

			int paddingRight = _inputWidth ?? ((_width ?? availableWidth ?? 50) - AnsiConsoleHelper.StripSpectreLength(_prompt ?? string.Empty));
			if (paddingRight < 0) paddingRight = 0;

			int rightWhiteSpace = paddingRight - visibleInput.Length;

			_cachedContent = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{_prompt}[{inputForegroundColor} on {inputBackgroundColor}]{visibleInput}{new string(' ', rightWhiteSpace)}[/]", (_width ?? (availableWidth ?? 50)), availableHeight, true, Container?.BackgroundColor, Container?.ForegroundColor);

			for (int i = 0; i < _cachedContent.Count; i++)
			{
				_cachedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + _cachedContent[i];
			}

			return _cachedContent;
		}

		// IFocusableControl implementation
		public bool CanReceiveFocus => IsEnabled;
		
		public event EventHandler? GotFocus;
		public event EventHandler? LostFocus;
		
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}

		public void SetInput(string? input)
		{
			if (string.IsNullOrEmpty(input))
			{
				_cursorPosition = 0;
			}
			else
			{
				_cursorPosition = input.Length; // Update cursor position to the end of the input
			}

			_cachedContent = null;
			_input = input ?? string.Empty;

			Container?.Invalidate(true);
			OnInputChange?.Invoke(this, _input);
		}

		private void SetScrollOffset(int value)
		{
			_scrollOffset = Math.Max(0, value);
		}
	}
}