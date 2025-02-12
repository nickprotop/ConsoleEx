// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx;

public class PromptContent : IWIndowContent
{
	private string _prompt;
	private string _input = string.Empty;
	private int _cursorPosition = 0;
	private Action<PromptContent, string> _onEnter;

	public bool IsInteractive { get; private set; } = true;

	public Window? Container { get; set; }

	public string Guid { get; } = System.Guid.NewGuid().ToString();

	public PromptContent(string prompt, Action<PromptContent, string> onEnter)
	{
		_prompt = prompt;
		_onEnter = onEnter;
	}

	public List<string> RenderContent(int? width, int? height)
	{
		return RenderContent(width, height, true);
	}

	public List<string> RenderContent(int? width, int? height, bool overflow)
	{
		var content = AnsiConsoleExtensions.ConvertMarkupToAnsi(_prompt + _input, width, height, true);
		return content;
	}

	public bool ProcessKey(ConsoleKeyInfo key)
	{
		if (key.Key == ConsoleKey.Enter)
		{
			_onEnter(this, _input);
			_input = string.Empty;
			_cursorPosition = 0;
			IsInteractive = false;
			Container?.Invalidate();
			return true;
		}
		else if (key.Key == ConsoleKey.Backspace && _cursorPosition > 0)
		{
			_input = _input.Remove(_cursorPosition - 1, 1);
			_cursorPosition--;
			Container?.Invalidate();
			return true;
		}
		else if (key.Key == ConsoleKey.Delete && _cursorPosition < _input.Length)
		{
			_input = _input.Remove(_cursorPosition, 1);
			Container?.Invalidate();
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
			return true;
		}
		return false;
	}
}
