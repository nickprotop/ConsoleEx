using System;
using System.Collections.Generic;
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

			while (contentIndex < content.Length && x < _width)
			{
				if (matchIndex < matches.Count && matches[matchIndex].Index == contentIndex)
				{
					// Process ANSI escape sequence
					var match = matches[matchIndex++];
					_backBuffer[x, y].AnsiEscape = match.Value;
					_backBuffer[x, y].IsDirty = true;
					contentIndex += match.Length;
				}
				else
				{
					// Process visible character
					_backBuffer[x, y].Character = content[contentIndex++];
					_backBuffer[x, y].IsDirty = true;
					x++;
				}
			}

			// Handle remaining ANSI escape sequences after the last character
			while (matchIndex < matches.Count)
			{
				if (x < _width)
				{
					_backBuffer[x, y].AnsiEscape += matches[matchIndex++].Value;
					_backBuffer[x, y].IsDirty = true;
				}
				else
				{
					_trailingEscapes[_width - 1, y] += matches[matchIndex++].Value;

				}
			}	
		}


		public void Render()
		{
			Console.CursorVisible = false;

			for (int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					var cell = _frontBuffer[x, y];
					if (cell.IsDirty)
					{
						Console.SetCursorPosition(x, y);
						Console.Write(cell.AnsiEscape + cell.Character);
						cell.IsDirty = false;
					}
				}

				// Output trailing escapes at the end of the line
				if (!string.IsNullOrEmpty(_trailingEscapes[_width - 1, y]))
				{
					Console.SetCursorPosition(_width - 1, y);
					Console.Write(_trailingEscapes[_width - 1, y]);
					_trailingEscapes[_width - 1, y] = string.Empty;
				}
			}

			// Swap buffers
			SwapBuffers();
		}

		private void SwapBuffers()
		{
			for (int x = 0; x < _width; x++)
			{
				for (int y = 0; y < _height; y++)
				{
					_frontBuffer[x, y].Character = _backBuffer[x, y].Character;
					_frontBuffer[x, y].AnsiEscape = _backBuffer[x, y].AnsiEscape;
					_frontBuffer[x, y].IsDirty = _backBuffer[x, y].IsDirty;
				}
			}
		}

		public void Clear()
		{
			for (int x = 0; x < _width; x++)
			{
				for (int y = 0; y < _height; y++)
				{
					_backBuffer[x, y] = new Cell(' ');
					_trailingEscapes[x, y] = string.Empty;
				}
			}
		}
	}
}