using ConsoleEx.Helpers;
using System.Text;
using System.Text.RegularExpressions;

namespace ConsoleEx.Drivers
{
	public class ConsoleBuffer
	{
		private const string ResetSequence = "\u001b[0m";
		private const string CursorForward = "\u001b[1C";

		// Cached regex for better performance
		private static readonly Regex _ansiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

		private readonly Cell[,] _backBuffer;
		private readonly Cell[,] _frontBuffer;
		private readonly int _height;
		private readonly int _width;

		// StringBuilder for render operations to minimize string allocations
		private readonly StringBuilder _renderBuilder = new(1024);

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
			// Early exit conditions
			if (!IsValidPosition(x, y) || string.IsNullOrEmpty(content))
				return;

			// Remove trailing reset sequence if present
			if (content.EndsWith(ResetSequence))
				content = content[..^ResetSequence.Length];

			var matches = _ansiRegex.Matches(content);
			var activeAnsiSequence = new StringBuilder(64); // Pre-size for typical ANSI sequences
			int contentPos = 0;
			int bufferX = x;
			int contentLength = AnsiConsoleHelper.StripAnsiStringLength(content);

			// Clear the area where new content will be written
			ClearArea(x, y, contentLength);

			// Process each character and ANSI sequence
			while (contentPos < content.Length && bufferX < _width)
			{
				bool isAnsiSequence = false;

				// Check if current position is the start of an ANSI sequence
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
					ref var cell = ref _backBuffer[bufferX, y]; // Use ref for better performance
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
				ref var nextCell = ref _backBuffer[bufferX, y];
				nextCell.AnsiEscape = ResetSequence + nextCell.AnsiEscape;
				nextCell.IsDirty = true;
			}
			else if (y + 1 < _height)
			{
				ref var nextLineCell = ref _backBuffer[0, y + 1];
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

			for (int y = 0; y < Math.Min(_height, Console.WindowHeight); y++)
			{
				if (!IsLineDirty(y))
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
				ref var cell = ref _backBuffer[x + i, y];
				cell.Character = ' ';
				cell.AnsiEscape = string.Empty;
				cell.IsDirty = true;
			}
		}

		private void InitializeBuffers()
		{
			// Use a single cell template to initialize both buffers
			var template = new Cell();

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
				ref readonly var frontCell = ref _frontBuffer[x, y];
				ref readonly var backCell = ref _backBuffer[x, y];

				if (backCell.IsDirty || !frontCell.Equals(backCell))
					return true;
			}
			return false;
		}

		private bool IsValidPosition(int x, int y)
			=> x >= 0 && x < _width && y >= 0 && y < _height;

		private void RenderLine(int y)
		{
			_renderBuilder.Clear();
			int consecutiveUnchanged = 0;

			for (int x = 0; x < _width; x++)
			{
				ref var frontCell = ref _frontBuffer[x, y];
				ref var backCell = ref _backBuffer[x, y];

				if (backCell.IsDirty || !frontCell.Equals(backCell))
				{
					// If we have pending cursor forward movements, append them first
					if (consecutiveUnchanged > 0)
					{
						if (consecutiveUnchanged == 1)
						{
							_renderBuilder.Append(CursorForward);
						}
						else
						{
							_renderBuilder.Append($"\u001b[{consecutiveUnchanged}C");
						}
						consecutiveUnchanged = 0;
					}

					_renderBuilder.Append(backCell.AnsiEscape);
					_renderBuilder.Append(backCell.Character);
					frontCell.CopyFrom(backCell);
					backCell.IsDirty = false;
				}
				else
				{
					consecutiveUnchanged++;
				}
			}

			// Handle any remaining cursor movements
			if (consecutiveUnchanged > 0)
			{
				if (consecutiveUnchanged == 1)
				{
					_renderBuilder.Append(CursorForward);
				}
				else
				{
					_renderBuilder.Append($"\u001b[{consecutiveUnchanged}C");
				}
			}

			// Write all changes at once
			Console.Write(_renderBuilder);
		}

		// Use struct for better memory layout and performance
		private struct Cell
		{
			public string AnsiEscape;
			public char Character;
			public bool IsDirty;

			public Cell()
			{
				AnsiEscape = string.Empty;
				Character = ' ';
				IsDirty = true;
			}

			public void CopyFrom(in Cell other)
			{
				Character = other.Character;
				AnsiEscape = other.AnsiEscape;
				IsDirty = other.IsDirty;
			}

			public bool Equals(in Cell other)
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
