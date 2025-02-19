using System.Text.RegularExpressions;

namespace ConsoleEx
{
	public class ConsoleBuffer
	{
		private class Cell
		{
			public char Character { get; set; }
			public string AnsiEscape { get; set; }
			public bool IsDirty { get; set; }

			public Cell(char character, string ansiEscape = "")
			{
				Character = character;
				AnsiEscape = ansiEscape;
				IsDirty = true;
			}
		}

		private readonly int _width;
		private readonly int _height;
		private readonly Cell[,] _frontBuffer;
		private readonly Cell[,] _backBuffer;
		private readonly string[,] _trailingEscapes;

		public ConsoleBuffer(int width, int height)
		{
			_width = width;
			_height = height;
			_frontBuffer = new Cell[width, height];
			_backBuffer = new Cell[width, height];
			_trailingEscapes = new string[width, height];

			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					_frontBuffer[x, y] = new Cell(' ');
					_backBuffer[x, y] = new Cell(' ');
					_trailingEscapes[x, y] = string.Empty;
				}
			}
		}

		public void AddContent(int x, int y, string content)
		{
			if (x < 0 || x >= _width || y < 0 || y >= _height)
				throw new ArgumentOutOfRangeException();

			var ansiRegex = new Regex(@"\x1B\[[0-9;]*[a-zA-Z]");
			var matches = ansiRegex.Matches(content);
			int contentIndex = 0;
			int matchIndex = 0;

			if (content.EndsWith("\u001b[0m")) content = content.Substring(0, content.Length - 4);

			for (int i = x; i < x + AnsiConsoleExtensions.StripAnsiStringLength(content); i++)
			{
				_backBuffer[i, y].AnsiEscape = string.Empty;
				_backBuffer[i, y].IsDirty = true;
				_backBuffer[i, y].Character = ' ';
			}

			int index = 0;

			while (contentIndex < content.Length && (index + x) < _width)
			{
				if (matchIndex < matches.Count && matches[matchIndex].Index == contentIndex)
				{
					// Process ANSI escape sequence
					var match = matches[matchIndex++];
					_backBuffer[x + index, y].AnsiEscape += match.Value;
					_backBuffer[x + index, y].IsDirty = true;
					contentIndex += match.Length;
				}
				else
				{
					// Process visible character
					_backBuffer[x + index, y].Character = content[contentIndex++];
					_backBuffer[x + index, y].IsDirty = true;
					index++;
				}
			}

			// Handle remaining ANSI escape sequences after the last character
			while (matchIndex < matches.Count)
			{
				if ((x + index + 1) < _width)
				{
					_backBuffer[x + index + 1, y].AnsiEscape += matches[matchIndex++].Value;
					_backBuffer[x + index + 1, y].IsDirty = true;
				}
				else
				{
					_trailingEscapes[_width - 1, y] = matches[matchIndex++].Value;
				}
			}

			if ((x + index) < _width)
			{
				_backBuffer[x + index, y].AnsiEscape = "\u001b[0m" + _backBuffer[x + index, y].AnsiEscape;
				_backBuffer[x + index, y].IsDirty = true;
			}
			else
			{
				if (y + 1 < _height) _backBuffer[0, y + 1].AnsiEscape = "\u001b[0m" + _backBuffer[0, y + 1].AnsiEscape;
			}
		}

		public void Render()
		{
			Console.CursorVisible = false;

			for (int y = 0; y < _height; y++)
			{
				bool lineDirty = false;
				for (int x = 0; x < _width; x++)
				{
					var frontCell = _frontBuffer[x, y];
					var backCell = _backBuffer[x, y];
					if (backCell.IsDirty || (frontCell.Character != backCell.Character || frontCell.AnsiEscape != backCell.AnsiEscape))
					{
						lineDirty = true;
						break;
					}
				}

				if (lineDirty)
				{
					Console.SetCursorPosition(0, y);
					for (int x = 0; x < _width; x++)
					{
						var frontCell = _frontBuffer[x, y];
						var backCell = _backBuffer[x, y];
						if (backCell.IsDirty || (frontCell.Character != backCell.Character || frontCell.AnsiEscape != backCell.AnsiEscape))
						{
							Console.Write(backCell.AnsiEscape + backCell.Character);
							frontCell.Character = backCell.Character;
							frontCell.AnsiEscape = backCell.AnsiEscape;
							backCell.IsDirty = false;
						}
						else
						{
							// move cursor to the write
							Console.Write("\u001b[1C");
						}
					}

					// Output trailing escapes at the end of the line
					if (!string.IsNullOrEmpty(_trailingEscapes[_width - 1, y]))
					{
						Console.SetCursorPosition(_width - 1, y);
						Console.Write(_trailingEscapes[_width - 1, y]);
					}
				}
			}
		}

		public void Clear()
		{
			for (int x = 0; x < _width; x++)
			{
				for (int y = 0; y < _height; y++)
				{
					_backBuffer[x, y].Character = ' ';
					_backBuffer[x, y].AnsiEscape = string.Empty;
					_backBuffer[x, y].IsDirty = true;
					_trailingEscapes[x, y] = string.Empty;
				}
			}
		}
	}
}