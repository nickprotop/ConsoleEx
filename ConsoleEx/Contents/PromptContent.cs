// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx;
using ConsoleEx.Contents;
using ConsoleEx.Helpers;

public class PromptContent : IWIndowContent, IInteractiveContent
{
	public Action<PromptContent, string>? OnEnter;
	private List<string>? _cachedContent;
	private int _cursorPosition = 0;
	private string _input = string.Empty;
	private Alignment _justify = Alignment.Left;
	private string? _prompt;
	private StickyPosition _stickyPosition = StickyPosition.None;
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
	{ get => _justify; set { _justify = value; _cachedContent = null; Container?.Invalidate(); } }

	public IContainer? Container { get; set; }

	public bool DisableOnEnter { get; set; } = true;

	public bool HasFocus { get; set; }

	public bool IsEnabled { get; set; } = true;

	public Action<PromptContent, string>? OnInputChange { get; set; }

	public string? Prompt
	{ get => _prompt; set { _prompt = value; _cachedContent = null; Container?.Invalidate(); } }

	public StickyPosition StickyPosition
	{
		get => _stickyPosition;
		set
		{
			_stickyPosition = value;
			Container?.Invalidate();
		}
	}

	public int? Width
	{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(); } }

	public void Dispose()
	{
		Container = null;
	}

	public (int Left, int Top)? GetCursorPosition()
	{
		if (_cachedContent == null) return null;
		return (AnsiConsoleHelper.StripAnsiStringLength(_cachedContent?.LastOrDefault() ?? string.Empty) - _input.Length + _cursorPosition, (_cachedContent?.Count ?? 0) - 1);
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

		_cachedContent = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(_prompt + _input, (_width ?? (availableWidth ?? 50)) - 1, availableHeight, true, Container?.BackgroundColor, Container?.ForegroundColor);

		for (int i = 0; i < _cachedContent.Count; i++)
		{
			_cachedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + _cachedContent[i];
		}

		return _cachedContent;
	}

	public void SetInput(string input)
	{
		_cachedContent = null;
		_input = input;
		Container?.Invalidate();
		OnInputChange?.Invoke(this, _input);
	}
}