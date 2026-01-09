// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Represents a change to a single cell in the buffer.
	/// </summary>
	public readonly record struct CellChange(int X, int Y, Cell Cell);

	/// <summary>
	/// A 2D buffer of character cells for rendering.
	/// Supports double-buffering for efficient diff-based output.
	/// </summary>
	public class CharacterBuffer
	{
		private Cell[,] _cells;
		private Cell[,]? _previousCells;
		private readonly Color _defaultBackground;

		/// <summary>
		/// Gets the width of the buffer.
		/// </summary>
		public int Width { get; private set; }

		/// <summary>
		/// Gets the height of the buffer.
		/// </summary>
		public int Height { get; private set; }

		/// <summary>
		/// Gets the bounds of this buffer as a rectangle.
		/// </summary>
		public LayoutRect Bounds => new(0, 0, Width, Height);

		/// <summary>
		/// Gets the size of this buffer.
		/// </summary>
		public LayoutSize Size => new(Width, Height);

		/// <summary>
		/// Creates a new character buffer with the specified dimensions.
		/// </summary>
		public CharacterBuffer(int width, int height, Color? defaultBackground = null)
		{
			Width = width;
			Height = height;
			_defaultBackground = defaultBackground ?? Color.Black;
			_cells = new Cell[width, height];
			Clear(_defaultBackground);
		}

		/// <summary>
		/// Resizes the buffer, preserving content where possible.
		/// </summary>
		public void Resize(int newWidth, int newHeight)
		{
			if (newWidth == Width && newHeight == Height)
				return;

			var newCells = new Cell[newWidth, newHeight];

			// Initialize with blanks
			for (int y = 0; y < newHeight; y++)
			{
				for (int x = 0; x < newWidth; x++)
				{
					newCells[x, y] = Cell.BlankWithBackground(_defaultBackground);
					newCells[x, y].Dirty = true;
				}
			}

			// Copy existing content
			int copyWidth = Math.Min(Width, newWidth);
			int copyHeight = Math.Min(Height, newHeight);
			for (int y = 0; y < copyHeight; y++)
			{
				for (int x = 0; x < copyWidth; x++)
				{
					newCells[x, y] = _cells[x, y];
				}
			}

			Width = newWidth;
			Height = newHeight;
			_cells = newCells;
			_previousCells = null; // Force full redraw
		}

		/// <summary>
		/// Gets the cell at the specified position.
		/// </summary>
		public Cell GetCell(int x, int y)
		{
			if (x < 0 || x >= Width || y < 0 || y >= Height)
				return Cell.Blank;
			return _cells[x, y];
		}

		/// <summary>
		/// Sets a cell at the specified position.
		/// </summary>
		public void SetCell(int x, int y, char character, Color foreground, Color background)
		{
			if (x < 0 || x >= Width || y < 0 || y >= Height)
				return;

			ref var cell = ref _cells[x, y];
			if (cell.Character != character ||
				!cell.Foreground.Equals(foreground) ||
				!cell.Background.Equals(background))
			{
				cell.Character = character;
				cell.Foreground = foreground;
				cell.Background = background;
				cell.Dirty = true;
			}
		}

		/// <summary>
		/// Sets a cell at the specified position.
		/// </summary>
		public void SetCell(int x, int y, Cell cell)
		{
			SetCell(x, y, cell.Character, cell.Foreground, cell.Background);
		}

		/// <summary>
		/// Writes a string at the specified position with the given colors.
		/// </summary>
		public void WriteString(int x, int y, string text, Color foreground, Color background)
		{
			if (y < 0 || y >= Height || string.IsNullOrEmpty(text))
				return;

			for (int i = 0; i < text.Length; i++)
			{
				int cx = x + i;
				if (cx >= 0 && cx < Width)
				{
					SetCell(cx, y, text[i], foreground, background);
				}
			}
		}

		/// <summary>
		/// Writes a string at the specified position, clipped to the specified rectangle.
		/// </summary>
		public void WriteStringClipped(int x, int y, string text, Color foreground, Color background, LayoutRect clipRect)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom || string.IsNullOrEmpty(text))
				return;

			for (int i = 0; i < text.Length; i++)
			{
				int cx = x + i;
				if (cx >= clipRect.X && cx < clipRect.Right && cx >= 0 && cx < Width)
				{
					SetCell(cx, y, text[i], foreground, background);
				}
			}
		}

		/// <summary>
		/// Writes cells from an enumerable, typically from AnsiParser.
		/// </summary>
		public void WriteCells(int x, int y, IEnumerable<Cell> cells)
		{
			if (y < 0 || y >= Height)
				return;

			int cx = x;
			foreach (var cell in cells)
			{
				if (cx >= 0 && cx < Width)
				{
					SetCell(cx, y, cell);
				}
				cx++;
			}
		}

		/// <summary>
		/// Writes cells clipped to a rectangle.
		/// </summary>
		public void WriteCellsClipped(int x, int y, IEnumerable<Cell> cells, LayoutRect clipRect)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom)
				return;

			int cx = x;
			foreach (var cell in cells)
			{
				if (cx >= clipRect.X && cx < clipRect.Right && cx >= 0 && cx < Width && y >= 0 && y < Height)
				{
					SetCell(cx, y, cell);
				}
				cx++;
			}
		}

		/// <summary>
		/// Fills a rectangle with the specified character and colors.
		/// </summary>
		public void FillRect(LayoutRect rect, char character, Color foreground, Color background)
		{
			var clipped = rect.Intersect(Bounds);
			if (clipped.IsEmpty)
				return;

			for (int y = clipped.Y; y < clipped.Bottom; y++)
			{
				for (int x = clipped.X; x < clipped.Right; x++)
				{
					SetCell(x, y, character, foreground, background);
				}
			}
		}

		/// <summary>
		/// Fills a rectangle with spaces using the specified background color.
		/// </summary>
		public void FillRect(LayoutRect rect, Color background)
		{
			FillRect(rect, ' ', Color.White, background);
		}

		/// <summary>
		/// Clears the entire buffer with the specified background color.
		/// </summary>
		public void Clear(Color background)
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					ref var cell = ref _cells[x, y];
					cell.Character = ' ';
					cell.Foreground = Color.White;
					cell.Background = background;
					cell.Dirty = true;
				}
			}
		}

		/// <summary>
		/// Clears a rectangular region with the specified background color.
		/// </summary>
		public void ClearRect(LayoutRect rect, Color background)
		{
			FillRect(rect, ' ', Color.White, background);
		}

		/// <summary>
		/// Draws a horizontal line.
		/// </summary>
		public void DrawHorizontalLine(int x, int y, int length, char character, Color foreground, Color background)
		{
			for (int i = 0; i < length; i++)
			{
				SetCell(x + i, y, character, foreground, background);
			}
		}

		/// <summary>
		/// Draws a vertical line.
		/// </summary>
		public void DrawVerticalLine(int x, int y, int length, char character, Color foreground, Color background)
		{
			for (int i = 0; i < length; i++)
			{
				SetCell(x, y + i, character, foreground, background);
			}
		}

		/// <summary>
		/// Draws a box border.
		/// </summary>
		public void DrawBox(LayoutRect rect, BoxChars chars, Color foreground, Color background)
		{
			if (rect.Width < 2 || rect.Height < 2)
				return;

			// Corners
			SetCell(rect.X, rect.Y, chars.TopLeft, foreground, background);
			SetCell(rect.Right - 1, rect.Y, chars.TopRight, foreground, background);
			SetCell(rect.X, rect.Bottom - 1, chars.BottomLeft, foreground, background);
			SetCell(rect.Right - 1, rect.Bottom - 1, chars.BottomRight, foreground, background);

			// Top and bottom edges
			for (int x = rect.X + 1; x < rect.Right - 1; x++)
			{
				SetCell(x, rect.Y, chars.Horizontal, foreground, background);
				SetCell(x, rect.Bottom - 1, chars.Horizontal, foreground, background);
			}

			// Left and right edges
			for (int y = rect.Y + 1; y < rect.Bottom - 1; y++)
			{
				SetCell(rect.X, y, chars.Vertical, foreground, background);
				SetCell(rect.Right - 1, y, chars.Vertical, foreground, background);
			}
		}

		/// <summary>
		/// Gets all cells that have changed since the last commit.
		/// </summary>
		public IEnumerable<CellChange> GetChanges()
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					var cell = _cells[x, y];
					if (cell.Dirty)
					{
						// If we have a previous buffer, only yield if actually changed
						if (_previousCells != null)
						{
							if (!cell.VisuallyEquals(_previousCells[x, y]))
							{
								yield return new CellChange(x, y, cell);
							}
						}
						else
						{
							yield return new CellChange(x, y, cell);
						}
					}
				}
			}
		}

		/// <summary>
		/// Gets all dirty cells regardless of whether they visually changed.
		/// </summary>
		public IEnumerable<CellChange> GetDirtyCells()
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					if (_cells[x, y].Dirty)
					{
						yield return new CellChange(x, y, _cells[x, y]);
					}
				}
			}
		}

		/// <summary>
		/// Commits the current buffer state and clears dirty flags.
		/// Call this after rendering to prepare for the next frame.
		/// </summary>
		public void Commit()
		{
			// Allocate previous buffer if needed
			if (_previousCells == null || _previousCells.GetLength(0) != Width || _previousCells.GetLength(1) != Height)
			{
				_previousCells = new Cell[Width, Height];
			}

			// Copy current to previous and clear dirty flags
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					_previousCells[x, y] = _cells[x, y];
					_cells[x, y].Dirty = false;
				}
			}
		}

		/// <summary>
		/// Marks all cells as dirty, forcing a full redraw on next render.
		/// </summary>
		public void InvalidateAll()
		{
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					_cells[x, y].Dirty = true;
				}
			}
		}

		/// <summary>
		/// Marks a rectangular region as dirty.
		/// </summary>
		public void InvalidateRect(LayoutRect rect)
		{
			var clipped = rect.Intersect(Bounds);
			if (clipped.IsEmpty)
				return;

			for (int y = clipped.Y; y < clipped.Bottom; y++)
			{
				for (int x = clipped.X; x < clipped.Right; x++)
				{
					_cells[x, y].Dirty = true;
				}
			}
		}

		/// <summary>
		/// Copies a region from another buffer.
		/// </summary>
		public void CopyFrom(CharacterBuffer source, LayoutRect sourceRect, int destX, int destY)
		{
			var srcClipped = sourceRect.Intersect(source.Bounds);
			if (srcClipped.IsEmpty)
				return;

			for (int sy = srcClipped.Y; sy < srcClipped.Bottom; sy++)
			{
				for (int sx = srcClipped.X; sx < srcClipped.Right; sx++)
				{
					int dx = destX + (sx - sourceRect.X);
					int dy = destY + (sy - sourceRect.Y);
					if (dx >= 0 && dx < Width && dy >= 0 && dy < Height)
					{
						SetCell(dx, dy, source.GetCell(sx, sy));
					}
				}
			}
		}

		/// <summary>
		/// Converts the buffer to a list of ANSI-formatted strings.
		/// Each line is a row of the buffer with ANSI escape codes for colors.
		/// </summary>
		/// <param name="defaultForeground">Default foreground color for optimization.</param>
		/// <param name="defaultBackground">Default background color for optimization.</param>
		/// <returns>List of ANSI-formatted strings, one per row.</returns>
		public List<string> ToLines(Color defaultForeground, Color defaultBackground)
		{
			var lines = new List<string>(Height);
			var sb = new System.Text.StringBuilder();

			for (int y = 0; y < Height; y++)
			{
				sb.Clear();
				Color? lastFg = null;
				Color? lastBg = null;

				for (int x = 0; x < Width; x++)
				{
					var cell = _cells[x, y];
					bool fgChanged = lastFg == null || !cell.Foreground.Equals(lastFg.Value);
					bool bgChanged = lastBg == null || !cell.Background.Equals(lastBg.Value);

					if (fgChanged || bgChanged)
					{
						// Build ANSI escape sequence for color change
						// Use 24-bit RGB: \e[38;2;R;G;Bm (foreground) and \e[48;2;R;G;Bm (background)
						sb.Append("\x1b[");
						if (fgChanged && bgChanged)
						{
							sb.Append($"38;2;{cell.Foreground.R};{cell.Foreground.G};{cell.Foreground.B};");
							sb.Append($"48;2;{cell.Background.R};{cell.Background.G};{cell.Background.B}");
						}
						else if (fgChanged)
						{
							sb.Append($"38;2;{cell.Foreground.R};{cell.Foreground.G};{cell.Foreground.B}");
						}
						else
						{
							sb.Append($"48;2;{cell.Background.R};{cell.Background.G};{cell.Background.B}");
						}
						sb.Append('m');

						lastFg = cell.Foreground;
						lastBg = cell.Background;
					}

					sb.Append(cell.Character);
				}

				// Reset colors at end of line
				sb.Append("\x1b[0m");
				lines.Add(sb.ToString());
			}

			return lines;
		}
	}

	/// <summary>
	/// Box drawing characters for borders.
	/// </summary>
	public readonly record struct BoxChars(
		char TopLeft,
		char TopRight,
		char BottomLeft,
		char BottomRight,
		char Horizontal,
		char Vertical
	)
	{
		/// <summary>
		/// Single-line box characters.
		/// </summary>
		public static BoxChars Single => new('┌', '┐', '└', '┘', '─', '│');

		/// <summary>
		/// Double-line box characters.
		/// </summary>
		public static BoxChars Double => new('╔', '╗', '╚', '╝', '═', '║');

		/// <summary>
		/// Rounded box characters.
		/// </summary>
		public static BoxChars Rounded => new('╭', '╮', '╰', '╯', '─', '│');

		/// <summary>
		/// ASCII box characters.
		/// </summary>
		public static BoxChars Ascii => new('+', '+', '+', '+', '-', '|');
	}
}
