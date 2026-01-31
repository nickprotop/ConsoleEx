// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpConsoleUI.Drivers
{
	/// <summary>
	/// Provides double-buffered console rendering with ANSI escape sequence support.
	/// </summary>
	/// <remarks>
	/// This class maintains a front buffer (what is currently displayed) and a back buffer
	/// (what will be rendered next). Only changed cells are written to the console,
	/// optimizing rendering performance by minimizing console output operations.
	/// </remarks>
	public class ConsoleBuffer
	{
		private const string CursorForward = "\u001b[1C";
		private const string ResetSequence = "\u001b[0m";

		// Cached regex for better performance
		private static readonly Regex _ansiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

		private readonly Cell[,] _backBuffer;
		private readonly Cell[,] _frontBuffer;
		private readonly int _height;

		// StringBuilder for render operations to minimize string allocations
		private readonly StringBuilder _renderBuilder = new(1024);

		private readonly int _width;
		private readonly object? _consoleLock; // Shared lock for thread-safe Console I/O

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleBuffer"/> class with the specified dimensions.
		/// </summary>
		/// <param name="width">The width of the buffer in characters.</param>
		/// <param name="height">The height of the buffer in lines.</param>
		/// <param name="consoleLock">Optional shared lock for thread-safe Console I/O operations.</param>
		public ConsoleBuffer(int width, int height, object? consoleLock = null)
		{
			_width = width;
			_height = height;
			_consoleLock = consoleLock;
			_backBuffer = new Cell[width, height];
			_frontBuffer = new Cell[width, height];

			InitializeBuffers();
		}

		/// <summary>
		/// Gets or sets a value indicating whether rendering is locked.
		/// </summary>
		/// <value>
		/// <c>true</c> if rendering should be skipped during <see cref="Render"/>; otherwise, <c>false</c>.
		/// </value>
		/// <remarks>
		/// This property is used to prevent rendering during buffer resizing operations.
		/// </remarks>
		public bool Lock { get; set; } = false;

		/// <summary>
		/// Adds content to the back buffer at the specified position.
		/// </summary>
		/// <param name="x">The horizontal position (column) to start writing at.</param>
		/// <param name="y">The vertical position (row) to write to.</param>
		/// <param name="content">The content to write, which may include ANSI escape sequences for formatting.</param>
		/// <remarks>
		/// ANSI escape sequences in the content are parsed and associated with the appropriate cells,
		/// ensuring proper formatting when rendered. Content that extends beyond the buffer width is truncated.
		/// </remarks>
		public void AddContent(int x, int y, string content)
		{
			// Early exit conditions
			if (!IsValidPosition(x, y) || string.IsNullOrEmpty(content))
				return;

			// Remove trailing reset sequence if present
			if (content.EndsWith(ResetSequence))
				content = content[..^ResetSequence.Length];

			var activeAnsiSequence = new StringBuilder(64); // Pre-size for typical ANSI sequences
			int contentPos = 0;
			int bufferX = x;
			int contentLength = AnsiConsoleHelper.StripAnsiStringLength(content);

			// Clear the area where new content will be written
			ClearArea(x, y, contentLength);

			// Single-pass state machine parser for ANSI sequences
			while (contentPos < content.Length && bufferX < _width)
			{
				// State machine: detect ESC character
				if (content[contentPos] == '\x1B' &&
					contentPos + 1 < content.Length &&
					content[contentPos + 1] == '[')
				{
					// Parse ANSI sequence inline
					int seqStart = contentPos;
					contentPos += 2;  // Skip ESC[

					// Read parameters and command (consume all non-letter characters)
					while (contentPos < content.Length &&
						   !char.IsLetter(content[contentPos]))
					{
						contentPos++;
					}

					// Include the terminating command letter
					if (contentPos < content.Length)
					{
						contentPos++;  // Include terminator
						activeAnsiSequence.Append(content, seqStart, contentPos - seqStart);
					}
				}
				else
				{
					// Regular character - write to buffer
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

		/// <summary>
		/// Clears the back buffer by resetting all cells to their default state.
		/// </summary>
		/// <remarks>
		/// All cells are reset to a space character with no ANSI formatting and marked as dirty.
		/// </remarks>
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

		/// <summary>
		/// Gets the count of dirty characters in the back buffer.
		/// </summary>
		/// <returns>The number of characters marked as dirty.</returns>
		public int GetDirtyCharacterCount()
		{
			int count = 0;
			for (int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					if (_backBuffer[x, y].IsDirty)
						count++;
				}
			}
			return count;
		}

		/// <summary>
		/// Renders the back buffer to the console, updating only the changed portions.
		/// </summary>
		/// <remarks>
		/// This method compares the back buffer with the front buffer and only writes
		/// cells that have changed, using cursor positioning to skip unchanged regions.
		/// After rendering, the front buffer is synchronized with the back buffer.
		/// The cursor is hidden during rendering to prevent flickering.
		/// </remarks>
	public void Render()
	{
		if (Lock)
			return;

		// Lock Console I/O to prevent concurrent input operations from being corrupted
		lock (_consoleLock ?? new object())
		{
			Console.CursorVisible = false;

			// Build entire screen in one string for atomic output
			// This eliminates flickering by doing a single write instead of multiple cursor moves
			var screenBuilder = new StringBuilder();

			for (int y = 0; y < Math.Min(_height, Console.WindowHeight); y++)
			{
				if (!IsLineDirty(y))
					continue;

				// Add ANSI absolute positioning: ESC[row;colH (1-based)
				screenBuilder.Append($"\x1b[{y + 1};1H");

				// Append this line's content to the screen builder
				AppendLineToBuilder(y, screenBuilder);
			}

			// Single atomic write of entire screen - no cursor jumps, no flicker!
			if (screenBuilder.Length > 0)
			{
				Console.Write(screenBuilder.ToString());
			}
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

		private void AppendLineToBuilder(int y, StringBuilder builder)
		{
			// DO NOT clear - we are appending to the screen builder
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
							builder.Append(CursorForward);
						}
						else
						{
							builder.Append($"\u001b[{consecutiveUnchanged}C");
						}
						consecutiveUnchanged = 0;
					}

					builder.Append(backCell.AnsiEscape);
					builder.Append(backCell.Character);
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
					builder.Append(CursorForward);
				}
				else
				{
					builder.Append($"\u001b[{consecutiveUnchanged}C");
				}
			}

			// Write all changes at once
		// Content appended to builder (removed Console.Write for atomic rendering)
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