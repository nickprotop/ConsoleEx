using System.Text;
using System.Text.RegularExpressions;

namespace ConsoleEx
{
	public class ConsoleBuffer
	{
		private class BufferCell
		{
			public string Content { get; set; } = " ";
			public string? ActiveAnsi { get; set; }
			public bool IsModified { get; set; } = false;
		}

		private readonly BufferCell[][] _buffer;
		private readonly int _width;
		private readonly int _height;
		private static readonly Regex AnsiRegex = new("\u001b\\[[0-9;]*m");
		private string _currentAnsi = "";

		public ConsoleBuffer(int width, int height)
		{
			_width = width;
			_height = height;
			_buffer = new BufferCell[height][];

			for (int i = 0; i < height; i++)
			{
				_buffer[i] = new BufferCell[width];
				for (int j = 0; j < width; j++)
				{
					_buffer[i][j] = new BufferCell();
				}
			}
		}

		public void Clear()
		{
			for (int i = 0; i < _height; i++)
			{
				for (int j = 0; j < _width; j++)
				{
					_buffer[i][j].Content = " ";
					_buffer[i][j].ActiveAnsi = null;
					_buffer[i][j].IsModified = true;
				}
			}
			_currentAnsi = "";
		}

		private (List<string> chars, List<string> ansiCodes) SplitAnsiString(string input)
		{
			var chars = new List<string>();
			var ansiCodes = new List<string>();
			var matches = AnsiRegex.Matches(input);
			var lastIndex = 0;

			foreach (Match match in matches)
			{
				// Add any characters before this ANSI code
				if (match.Index > lastIndex)
				{
					for (int i = lastIndex; i < match.Index; i++)
					{
						chars.Add(input[i].ToString());
						ansiCodes.Add(""); // Add empty string for characters without ANSI code
					}
				}

				// Add the ANSI code
				if (chars.Count > 0)
				{
					ansiCodes[ansiCodes.Count - 1] = match.Value; // Assign ANSI code to the last character
				}
				else
				{
					ansiCodes.Add(match.Value); // If no characters yet, add ANSI code directly
				}
				lastIndex = match.Index + match.Length;
			}

			// Add any remaining characters after the last ANSI code
			for (int i = lastIndex; i < input.Length; i++)
			{
				chars.Add(input[i].ToString());
				ansiCodes.Add(""); // Add empty string for characters without ANSI code
			}

			return (chars, ansiCodes);
		}

		public void Write(int x, int y, string content)
		{
			if (y < 0 || y >= _height || x < 0) return;

			var (chars, ansiCodes) = SplitAnsiString(content);
			var currentX = x;

			for (int i = 0; i < chars.Count && currentX < _width; i++)
			{
				if (!string.IsNullOrEmpty(ansiCodes[i]))
				{
					_currentAnsi = ansiCodes[i];
				}
				
				//if (!string.IsNullOrEmpty(chars[i]))
				//{
					_buffer[y][currentX].Content = chars[i] ?? " ";
					_buffer[y][currentX].ActiveAnsi = _currentAnsi;
					_buffer[y][currentX].IsModified = true;
					currentX++;
				//}
			}
		}

		public void Render()
		{
			var currentAnsi = "";
			var originalPosition = Console.GetCursorPosition();

			for (int y = 0; y < _height; y++)
			{
				var lineModified = false;
				var lineBuilder = new StringBuilder();
				var lastWrittenX = -1;

				for (int x = 0; x < _width; x++)
				{
					var cell = _buffer[y][x];
					if (cell.IsModified)
					{
						lineModified = true;

						// If there's a gap, fill it with spaces
						if (lastWrittenX != x - 1 && lastWrittenX != -1)
						{
							lineBuilder.Append(new string(' ', x - lastWrittenX - 1));
						}

						// If the ANSI code changed, append the new one
						if (cell.ActiveAnsi != currentAnsi)
						{
							if (!string.IsNullOrEmpty(currentAnsi))
							{
								lineBuilder.Append("\u001b[0m"); // Reset
							}
							if (!string.IsNullOrEmpty(cell.ActiveAnsi))
							{
								lineBuilder.Append(cell.ActiveAnsi);
							}
							currentAnsi = cell.ActiveAnsi ?? "";
						}

						lineBuilder.Append(cell.Content);
						lastWrittenX = x;
						cell.IsModified = false;
					}
				}

				if (lineModified)
				{
					// Reset ANSI at end of line if needed
					if (!string.IsNullOrEmpty(currentAnsi))
					{
						lineBuilder.Append("\u001b[0m");
						currentAnsi = "";
					}

					Console.SetCursorPosition(0, y);
					Console.Write(lineBuilder.ToString());
				}
			}

			// Reset ANSI and restore cursor position
			if (!string.IsNullOrEmpty(currentAnsi))
			{
				Console.Write("\u001b[0m");
			}
			Console.SetCursorPosition(originalPosition.Left, originalPosition.Top);
		}
	}
}