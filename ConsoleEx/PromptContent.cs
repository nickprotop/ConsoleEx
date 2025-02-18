// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx;

public class PromptContent : IWIndowContent, IInteractiveContent
{
	private string _prompt;
	private string _input = string.Empty;
	private int _cursorPosition = 0;
	private List<string>? _cachedContent;
	public Action<PromptContent, string>? OnEnter;
	private Alignment _justify;

	private int? _width;

	public void Invalidate()
	{
		_cachedContent = null;
	}

	public int? ActualWidth
	{
		get
		{
			if (_cachedContent == null) return null;

			int maxLength = 0;
			foreach (var line in _cachedContent)
			{
				int length = AnsiConsoleExtensions.StripAnsiStringLength(line);
				if (length > maxLength) maxLength = length;
			}
			return maxLength;
		}
	}

	public int? Width
	{ get => _width; set { _width = value; Container?.Invalidate(); } }

	public bool HasFocus { get; set; }

	public Action<PromptContent, string>? OnInputChange { get; set; }

	public bool DisableOnEnter { get; set; } = true;

	public bool IsEnabled { get; set; } = true;

	public IContainer? Container { get; set; }	

	public Alignment Alignment
	{ get => _justify; set { _justify = value; Container?.Invalidate(); } }

	public PromptContent(string prompt)
	{
		_prompt = prompt;
	}

	public void SetPrompt(string prompt)
	{
		_cachedContent = null;
		_prompt = prompt;
		Container?.Invalidate();
	}

	public void SetInput(string input)
	{
		_cachedContent = null;
		_input = input;
		Container?.Invalidate();
		OnInputChange?.Invoke(this, _input);
	}

	public List<string> RenderContent(int? width, int? height)
	{
		if (_cachedContent != null) return _cachedContent;

		_cachedContent = new List<string>();

		_cachedContent = AnsiConsoleExtensions.ConvertSpectreMarkupToAnsi(_prompt + _input, (_width ?? (width ?? 50)) - 1, height, true, Container?.BackgroundColor, Container?.ForegroundColor);
		return _cachedContent;
	}

	public bool ProcessKey(ConsoleKeyInfo key)
	{
		if (key.Key == ConsoleKey.Enter)
		{
			OnEnter?.Invoke(this, _input);
			if (DisableOnEnter)
			{
				_cursorPosition = 0;
				IsEnabled = false;
			}
			Container?.Invalidate();
			OnInputChange?.Invoke(this, _input);
			return true;
		}
		else if (key.Key == ConsoleKey.Backspace && _cursorPosition > 0)
		{
			_input = _input.Remove(_cursorPosition - 1, 1);
			_cursorPosition--;
			Container?.Invalidate();
			OnInputChange?.Invoke(this, _input);
			return true;
		}
		else if (key.Key == ConsoleKey.Delete && _cursorPosition < _input.Length)
		{
			_input = _input.Remove(_cursorPosition, 1);
			Container?.Invalidate();
			OnInputChange?.Invoke(this, _input);
			return true;
		}
		else if (key.Key == ConsoleKey.Home)
		{
			_cursorPosition = 0;
			Container?.Invalidate();
			return true;
		}
		else if (key.Key == ConsoleKey.End)
		{
			_cursorPosition = _input.Length;
			Container?.Invalidate();
			return true;
		}
		else if (key.Key == ConsoleKey.LeftArrow && _cursorPosition > 0)
		{
			_cursorPosition--;
			Container?.Invalidate();
			return true;
		}
		else if (key.Key == ConsoleKey.RightArrow && _cursorPosition < _input.Length)
		{
			_cursorPosition++;
			Container?.Invalidate();
			return true;
		}
		else if (!char.IsControl(key.KeyChar))
		{
			_input = _input.Insert(_cursorPosition, key.KeyChar.ToString());
			_cursorPosition++;
			Container?.Invalidate();
			OnInputChange?.Invoke(this, _input);
			return true;
		}
		return false;
	}

	public (int Left, int Top)? GetCursorPosition()
	{
		if (_cachedContent == null) return null;
		return (AnsiConsoleExtensions.StripAnsiStringLength(_cachedContent?.LastOrDefault() ?? string.Empty) - _input.Length + _cursorPosition, (_cachedContent?.Count ?? 0) - 1);
	}

	public void Dispose()
	{
		Container = null;
	}
}
