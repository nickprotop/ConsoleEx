using ConsoleEx.Helpers;
using System.Text;
using System.Text.RegularExpressions;

namespace ConsoleEx.Drivers
{
	public class ConsoleBuffer
	{
		private const string ResetSequence = "\u001b[0m";

		// Cached regex for better performance
		private static readonly Regex _ansiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

		private readonly Cell[,] _backBuffer;
		private readonly Cell[,] _frontBuffer;
		private readonly int _height;
		private readonly int _width;

		public ConsoleBuffer(int width, int height)
		{
			_width = width;
			_height = height;
			_backBuffer = new Cell[width, height];
			_frontBuffer = new Cell[width, height];

			InitializeBuffers();
		}

		public bool Lock { get; set; } = false;

		public void AddContent(int x, int y, string content)
		{
			if (!IsValidPosition(x, y)) return;
			//throw new ArgumentOutOfRangeException($"Position ({x},{y}) is outside buffer bounds");

			if (string.IsNullOrEmpty(content))
				return;

			// Remove trailing reset sequence if present
			if (content.EndsWith(ResetSequence))
				content = content[..^ResetSequence.Length];

			var matches = _ansiRegex.Matches(content);
			var activeAnsiSequence = new StringBuilder();
			int contentPos = 0;
			int bufferX = x;

			// Clear the area where new content will be written
			ClearArea(x, y, AnsiConsoleHelper.StripAnsiStringLength(content));

			while (contentPos < content.Length && bufferX < _width)
			{
				bool isAnsiSequence = false;
				foreach (Match match in matches)
				{
					if (match.Index == contentPos)
					{
						activeAnsiSequence.Append(match.Value);
						contentPos += match.Length;
						isAnsiSequence = true;
						break;
					}
				}

				if (!isAnsiSequence)
				{
					var cell = _backBuffer[bufferX, y];
					cell.Character = content[contentPos];
					cell.AnsiEscape = activeAnsiSequence.ToString();
					cell.IsDirty = true;
					contentPos++;
					bufferX++;
				}
			}

			// Handle end of line formatting
			if (bufferX < _width)
			{
				var nextCell = _backBuffer[bufferX, y];
				nextCell.AnsiEscape = ResetSequence + nextCell.AnsiEscape;
				nextCell.IsDirty = true;
			}
			else if (y + 1 < _height)
			{
				var nextLineCell = _backBuffer[0, y + 1];
				nextLineCell.AnsiEscape = ResetSequence + nextLineCell.AnsiEscape;
				nextLineCell.IsDirty = true;
			}
		}

		public void Clear()
		{
			for (int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					_backBuffer[x, y].Reset();
				}
			}
		}

		public void Render()
		{
			if (Lock)
				return;

			Console.CursorVisible = false;

			for (int y = 0; y < _height; y++)
			{
				if (!IsLineDirty(y))
					continue;

				// check if x and y is in boundaries
				if (y >= Console.WindowHeight)
					continue;

				Console.SetCursorPosition(0, y);
				RenderLine(y);
			}
		}

		private void ClearArea(int x, int y, int length)
		{
			length = Math.Min(length, _width - x);
			for (int i = 0; i < length; i++)
			{
				var cell = _backBuffer[x + i, y];
				cell.Character = ' ';
				cell.AnsiEscape = string.Empty;
				cell.IsDirty = true;
			}
		}

		private void InitializeBuffers()
		{
			for (int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					_frontBuffer[x, y] = new Cell();
					_backBuffer[x, y] = new Cell();
				}
			}
		}

		private bool IsLineDirty(int y)
		{
			for (int x = 0; x < _width; x++)
			{
				var frontCell = _frontBuffer[x, y];
				var backCell = _backBuffer[x, y];
				if (backCell.IsDirty || !frontCell.Equals(backCell))
					return true;
			}
			return false;
		}

		private bool IsValidPosition(int x, int y)
					=> x >= 0 && x < _width && y >= 0 && y < _height;

		private void RenderLine(int y)
		{
			for (int x = 0; x < _width; x++)
			{
				var frontCell = _frontBuffer[x, y];
				var backCell = _backBuffer[x, y];

				if (backCell.IsDirty || !frontCell.Equals(backCell))
				{
					Console.Write(backCell.AnsiEscape + backCell.Character);
					frontCell.CopyFrom(backCell);
					backCell.IsDirty = false;
				}
				else
				{
					Console.Write("\u001b[1C");
				}
			}
		}

		private class Cell
		{
			public string AnsiEscape { get; set; } = string.Empty;
			public char Character { get; set; } = ' ';
			public bool IsDirty { get; set; } = true;

			public void CopyFrom(Cell other)
			{
				Character = other.Character;
				AnsiEscape = other.AnsiEscape;
				IsDirty = other.IsDirty;
			}

			public bool Equals(Cell other)
				=> Character == other.Character && AnsiEscape == other.AnsiEscape;

			public void Reset()
			{
				Character = ' ';
				AnsiEscape = string.Empty;
				IsDirty = true;
			}
		}
	}
}