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
	private List<string> _cachedContent = new List<string>();
	private int _width;
	private Action<PromptContent, string>? _onEnter;

	public bool HasFocus { get; set; }

	public Action<PromptContent, string>? OnInputChange { get; set; } 

	public bool DisableOnEnter { get; set; } = true;

	public bool IsEnabled { get; set; } = true;

	public Window? Container { get; set; }

	public string Guid { get; } = System.Guid.NewGuid().ToString();

	public PromptContent(string prompt, Action<PromptContent, string> onEnter)
	{
		_prompt = prompt;
		_onEnter = onEnter;
	}

	public PromptContent(string prompt)
	{
		_prompt = prompt;
	}

	public List<string> RenderContent(int? width, int? height)
	{
		_width = width ?? 80;
		return RenderContent(width, height, true);
	}

	public void SetPrompt(string prompt)
	{
		_cachedContent = new List<string>();
		_prompt = prompt;
		Container?.Invalidate();
	}

	public void SetInput(string input)
	{
		_cachedContent = new List<string>();
		_input = input;
		Container?.Invalidate();
		OnInputChange?.Invoke(this, _input);
	}

	public List<string> RenderContent(int? width, int? height, bool overflow)
	{
		_width = width ?? 80;
		_cachedContent = AnsiConsoleExtensions.ConvertMarkupToAnsi(_prompt + _input, width, height, true);
		return _cachedContent;
	}

	public bool ProcessKey(ConsoleKeyInfo key)
	{
		if (key.Key == ConsoleKey.Enter)
		{
			_onEnter?.Invoke(this, _input);
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

	public (int Left, int Top) GetCursorPosition()
	{
		int width = _width;
		int promptLength = AnsiConsoleExtensions.CalculateEffectiveLength(_prompt);
		int totalLength = promptLength + _cursorPosition;

		// Calculate the row and column based on the width
		int row = totalLength / width;
		int column = totalLength % width;

		return (column, row);

		return (AnsiConsoleExtensions.CalculateEffectiveLength(_prompt) + _cursorPosition, _cachedContent.Count - 1);
	}
}
