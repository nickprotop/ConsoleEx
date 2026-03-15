// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Represents a change to a single cell in the buffer.
	/// </summary>
	public readonly record struct CellChange(int X, int Y, Cell Cell);

	/// <summary>
	/// Immutable snapshot of a CharacterBuffer at a point in time.
	/// </summary>
	/// <param name="Width">The width of the captured buffer.</param>
	/// <param name="Height">The height of the captured buffer.</param>
	/// <param name="Cells">The deep copy of all cells in the buffer.</param>
	public readonly record struct BufferSnapshot(int Width, int Height, Cell[,] Cells)
	{
		/// <summary>
		/// Gets the cell at the specified position.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <param name="y">The Y coordinate.</param>
		/// <returns>The cell at the specified position.</returns>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when position is out of bounds.</exception>
		public Cell GetCell(int x, int y)
		{
			if (x < 0 || x >= Width || y < 0 || y >= Height)
				throw new ArgumentOutOfRangeException($"Position ({x}, {y}) is out of bounds");
			return Cells[x, y];
		}
	}

	/// <summary>
	/// A 2D buffer of character cells for rendering.
	/// Supports double-buffering for efficient diff-based output.
	/// </summary>
	public class CharacterBuffer
	{
		private Cell[,] _cells;
		private Cell[,]? _previousCells;
		private readonly Color _defaultBackground;
		private LayoutRect _dirtyRegion = LayoutRect.Empty;

		// Diagnostics support (optional, for testing and debugging)
		private Diagnostics.RenderingDiagnostics? _diagnostics;

		/// <summary>
		/// Gets or sets the diagnostics system for capturing rendering metrics.
		/// </summary>
		public Diagnostics.RenderingDiagnostics? Diagnostics
		{
			get => _diagnostics;
			set => _diagnostics = value;
		}

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
		/// Cleans up wide character pairs when overwriting a cell.
		/// If the target cell is a continuation (right half of a wide char), clears the wide char at x-1.
		/// If the target cell is the first half of a wide char, clears the continuation at x+1.
		/// </summary>
		private void CleanupWideCharAt(int x, int y)
		{
			ref var target = ref _cells[x, y];

			// Overwriting a continuation cell: clear the wide char to the left
			if (target.IsWideContinuation && x > 0)
			{
				ref var left = ref _cells[x - 1, y];
				left.Character = new Rune(' ');
				left.IsWideContinuation = false;
				left.Dirty = true;
				ExpandDirtyRegion(x - 1, y);
			}

			// Overwriting the first half of a wide char: clear the continuation to the right
			if (!target.IsWideContinuation && x + 1 < Width && _cells[x + 1, y].IsWideContinuation)
			{
				ref var right = ref _cells[x + 1, y];
				right.Character = new Rune(' ');
				right.IsWideContinuation = false;
				right.Dirty = true;
				ExpandDirtyRegion(x + 1, y);
			}
		}

		private void ExpandDirtyRegion(int x, int y)
		{
			if (_dirtyRegion.IsEmpty)
			{
				_dirtyRegion = new LayoutRect(x, y, 1, 1);
			}
			else
			{
				int minX = Math.Min(_dirtyRegion.X, x);
				int minY = Math.Min(_dirtyRegion.Y, y);
				int maxX = Math.Max(_dirtyRegion.Right, x + 1);
				int maxY = Math.Max(_dirtyRegion.Bottom, y + 1);
				_dirtyRegion = new LayoutRect(minX, minY, maxX - minX, maxY - minY);
			}
		}

		/// <summary>
		/// Sets a narrow (width-1) cell at the specified position.
		/// Clears IsWideContinuation, Combiners, and Decorations.
		/// Do NOT use for cells from MarkupParser.Parse — use SetCell(Cell) instead to preserve flags.
		/// </summary>
		public void SetNarrowCell(int x, int y, char character, Color foreground, Color background)
			=> SetNarrowCell(x, y, char.IsSurrogate(character) ? new Rune('\uFFFD') : new Rune(character), foreground, background);

		/// <summary>
		/// Sets a narrow (width-1) cell at the specified position with a Rune character.
		/// Clears IsWideContinuation, Combiners, and Decorations.
		/// Do NOT use for cells from MarkupParser.Parse — use SetCell(Cell) instead to preserve flags.
		/// </summary>
		public void SetNarrowCell(int x, int y, Rune character, Color foreground, Color background)
		{
			if (x < 0 || x >= Width || y < 0 || y >= Height)
				return;

			CleanupWideCharAt(x, y);

			ref var cell = ref _cells[x, y];
			if (cell.Character != character ||
				!cell.Foreground.Equals(foreground) ||
				!cell.Background.Equals(background) ||
				cell.Decorations != TextDecoration.None ||
				cell.IsWideContinuation ||
				cell.Combiners != null)
			{
				cell.Character = character;
				cell.Foreground = foreground;
				cell.Background = background;
				cell.Decorations = TextDecoration.None;
				cell.IsWideContinuation = false;
				cell.Combiners = null;
				cell.Dirty = true;

				ExpandDirtyRegion(x, y);
			}
		}

		/// <summary>
		/// Sets a cell at the specified position, preserving all attributes including decorations.
		/// </summary>
		public void SetCell(int x, int y, Cell cell)
		{
			if (x < 0 || x >= Width || y < 0 || y >= Height)
				return;

			CleanupWideCharAt(x, y);

			ref var existing = ref _cells[x, y];
			if (existing.Character != cell.Character ||
				!existing.Foreground.Equals(cell.Foreground) ||
				!existing.Background.Equals(cell.Background) ||
				existing.Decorations != cell.Decorations ||
				existing.IsWideContinuation != cell.IsWideContinuation ||
				existing.Combiners != cell.Combiners)
			{
				existing.Character = cell.Character;
				existing.Foreground = cell.Foreground;
				existing.Background = cell.Background;
				existing.Decorations = cell.Decorations;
				existing.IsWideContinuation = cell.IsWideContinuation;
				existing.Combiners = cell.Combiners;
				existing.Dirty = true;

				ExpandDirtyRegion(x, y);
			}
		}

		/// <summary>
		/// Writes a string at the specified position with the given colors.
		/// Wide characters occupy 2 columns with a continuation cell for the right half.
		/// </summary>
		public void WriteString(int x, int y, string text, Color foreground, Color background)
		{
			if (y < 0 || y >= Height || string.IsNullOrEmpty(text))
				return;

			int cx = x;
			foreach (var rune in text.EnumerateRunes())
			{
				int runeWidth = UnicodeWidth.GetRuneWidth(rune);

				if (runeWidth == 0)
				{
					// Zero-width: attach to previous cell as combiner
					// Skip past continuation cells to find the base cell
					int prevX = cx - 1;
					if (prevX >= 0 && prevX < Width && _cells[prevX, y].IsWideContinuation && prevX > 0)
						prevX--;
					if (prevX >= 0 && prevX < Width)
					{
						ref var prevCell = ref _cells[prevX, y];
						// VS16 widens certain emoji from 1→2 columns
						if (UnicodeWidth.IsVS16(rune) && UnicodeWidth.IsVs16Widened(prevCell.Character) && !UnicodeWidth.IsWideRune(prevCell.Character))
						{
							prevCell.AppendCombiner(rune);
							prevCell.Dirty = true;
							ExpandDirtyRegion(prevX, y);
							// Place continuation cell at current position
							if (cx >= 0 && cx < Width)
							{
								SetCell(cx, y, new Cell(' ', foreground, background) { IsWideContinuation = true });
							}
							cx++;
						}
						else
						{
							prevCell.AppendCombiner(rune);
							prevCell.Dirty = true;
							ExpandDirtyRegion(prevX, y);
						}
					}
					continue;
				}
				else if (runeWidth == 2)
				{
					// Wide char needs 2 columns
					if (cx >= 0 && cx + 1 < Width)
					{
						SetCell(cx, y, new Cell(rune, foreground, background));
						var cont = new Cell(' ', foreground, background) { IsWideContinuation = true, Dirty = true };
						SetCell(cx + 1, y, cont);
					}
					else if (cx >= 0 && cx < Width)
					{
						// No room for continuation — write space instead
						SetNarrowCell(cx, y, ' ', foreground, background);
					}
					cx += 2;
				}
				else
				{
					if (cx >= 0 && cx < Width)
					{
						SetCell(cx, y, new Cell(rune, foreground, background));
					}
					cx++;
				}
			}
		}

		/// <summary>
		/// Writes a string at the specified position, clipped to the specified rectangle.
		/// Wide characters that straddle clip boundaries are replaced with a space.
		/// </summary>
		public void WriteStringClipped(int x, int y, string text, Color foreground, Color background, LayoutRect clipRect)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom || string.IsNullOrEmpty(text))
				return;

			int cx = x;
			foreach (var rune in text.EnumerateRunes())
			{
				int runeWidth = UnicodeWidth.GetRuneWidth(rune);

				if (runeWidth == 0)
				{
					// Zero-width: attach to previous cell as combiner
					// Skip past continuation cells to find the base cell
					int prevX = cx - 1;
					if (prevX >= 0 && prevX < Width && _cells[prevX, y].IsWideContinuation && prevX > 0)
						prevX--;
					if (prevX >= 0 && prevX < Width)
					{
						ref var prevCell = ref _cells[prevX, y];
						// VS16 widens certain emoji from 1→2 columns
						if (UnicodeWidth.IsVS16(rune) && UnicodeWidth.IsVs16Widened(prevCell.Character) && !UnicodeWidth.IsWideRune(prevCell.Character))
						{
							prevCell.AppendCombiner(rune);
							prevCell.Dirty = true;
							ExpandDirtyRegion(prevX, y);
							// Place continuation cell at current position (with clip check)
							if (cx >= clipRect.X && cx < clipRect.Right && cx >= 0 && cx < Width)
							{
								SetCell(cx, y, new Cell(' ', foreground, background) { IsWideContinuation = true });
							}
							cx++;
						}
						else
						{
							prevCell.AppendCombiner(rune);
							prevCell.Dirty = true;
							ExpandDirtyRegion(prevX, y);
						}
					}
					continue;
				}
				else if (runeWidth == 2)
				{
					bool firstInClip = cx >= clipRect.X && cx < clipRect.Right && cx >= 0 && cx < Width;
					bool secondInClip = cx + 1 >= clipRect.X && cx + 1 < clipRect.Right && cx + 1 >= 0 && cx + 1 < Width;

					if (firstInClip && secondInClip)
					{
						// Both columns in clip — write wide char + continuation
						SetCell(cx, y, new Cell(rune, foreground, background));
						var cont = new Cell(' ', foreground, background) { IsWideContinuation = true, Dirty = true };
						SetCell(cx + 1, y, cont);
					}
					else if (firstInClip)
					{
						// Only first column in clip — can't show half a wide char
						SetNarrowCell(cx, y, ' ', foreground, background);
					}
					else if (secondInClip)
					{
						// Only second column in clip — can't show half a wide char
						SetNarrowCell(cx + 1, y, ' ', foreground, background);
					}
					cx += 2;
				}
				else
				{
					if (cx >= clipRect.X && cx < clipRect.Right && cx >= 0 && cx < Width)
					{
						SetCell(cx, y, new Cell(rune, foreground, background));
					}
					cx++;
				}
			}
		}

		/// <summary>
		/// Writes cells from an enumerable.
		/// Continuation cells (from MarkupParser) are written as-is without additional wide-char processing.
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
		/// Wide characters that straddle clip boundaries are replaced with a space.
		/// </summary>
		public void WriteCellsClipped(int x, int y, IEnumerable<Cell> cells, LayoutRect clipRect)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom)
				return;

			int cx = x;
			Cell? pendingWideChar = null;

			foreach (var cell in cells)
			{
				if (cell.IsWideContinuation && pendingWideChar.HasValue)
				{
					// This is the continuation of a wide char
					bool firstInClip = (cx - 1) >= clipRect.X && (cx - 1) < clipRect.Right && (cx - 1) >= 0 && (cx - 1) < Width;
					bool secondInClip = cx >= clipRect.X && cx < clipRect.Right && cx >= 0 && cx < Width;

					if (firstInClip && secondInClip)
					{
						SetCell(cx - 1, y, pendingWideChar.Value);
						SetCell(cx, y, cell);
					}
					else if (firstInClip)
					{
						// Only first column visible — replace with space
						var space = pendingWideChar.Value;
						space.Character = new Rune(' ');
						space.IsWideContinuation = false;
						SetCell(cx - 1, y, space);
					}
					else if (secondInClip)
					{
						// Only second column visible — replace with space
						var space = cell;
						space.Character = new Rune(' ');
						space.IsWideContinuation = false;
						SetCell(cx, y, space);
					}

					pendingWideChar = null;
					cx++;
					continue;
				}

				// If we had a pending wide char without a continuation, write it as space
				if (pendingWideChar.HasValue)
				{
					int prevCx = cx - 1;
					if (prevCx >= clipRect.X && prevCx < clipRect.Right && prevCx >= 0 && prevCx < Width)
					{
						var space = pendingWideChar.Value;
						space.Character = new Rune(' ');
						SetCell(prevCx, y, space);
					}
					pendingWideChar = null;
				}

				// Check if this is a wide character (non-continuation cell with wide char)
				if (!cell.IsWideContinuation && UnicodeWidth.IsWideRune(cell.Character))
				{
					pendingWideChar = cell;
					cx++;
					continue;
				}

				if (cx >= clipRect.X && cx < clipRect.Right && cx >= 0 && cx < Width && y >= 0 && y < Height)
				{
					SetCell(cx, y, cell);
				}
				cx++;
			}

			// Handle trailing pending wide char
			if (pendingWideChar.HasValue)
			{
				int prevCx = cx - 1;
				if (prevCx >= clipRect.X && prevCx < clipRect.Right && prevCx >= 0 && prevCx < Width)
				{
					var space = pendingWideChar.Value;
					space.Character = new Rune(' ');
					SetCell(prevCx, y, space);
				}
			}
		}

		/// <summary>
		/// Writes cells clipped to a rectangle, preserving existing background colors for cells
		/// whose background matches the default. Cells with markup-specified backgrounds (different
		/// from defaultBg) keep their explicit background.
		/// Wide characters that straddle clip boundaries are replaced with a space.
		/// </summary>
		public void WriteCellsClippedPreservingBackground(int x, int y, IEnumerable<Cell> cells, LayoutRect clipRect, Color defaultBg)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom)
				return;

			int cx = x;
			Cell? pendingWideChar = null;

			foreach (var cell in cells)
			{
				if (cell.IsWideContinuation && pendingWideChar.HasValue)
				{
					bool firstInClip = (cx - 1) >= clipRect.X && (cx - 1) < clipRect.Right && (cx - 1) >= 0 && (cx - 1) < Width;
					bool secondInClip = cx >= clipRect.X && cx < clipRect.Right && cx >= 0 && cx < Width;

					if (firstInClip && secondInClip)
					{
						SetCellPreservingBackground(cx - 1, y, pendingWideChar.Value, defaultBg);
						SetCellPreservingBackground(cx, y, cell, defaultBg);
					}
					else if (firstInClip)
					{
						var space = pendingWideChar.Value;
						space.Character = new Rune(' ');
						space.IsWideContinuation = false;
						SetCellPreservingBackground(cx - 1, y, space, defaultBg);
					}
					else if (secondInClip)
					{
						var space = cell;
						space.Character = new Rune(' ');
						space.IsWideContinuation = false;
						SetCellPreservingBackground(cx, y, space, defaultBg);
					}

					pendingWideChar = null;
					cx++;
					continue;
				}

				if (pendingWideChar.HasValue)
				{
					int prevCx = cx - 1;
					if (prevCx >= clipRect.X && prevCx < clipRect.Right && prevCx >= 0 && prevCx < Width)
					{
						var space = pendingWideChar.Value;
						space.Character = new Rune(' ');
						SetCellPreservingBackground(prevCx, y, space, defaultBg);
					}
					pendingWideChar = null;
				}

				if (!cell.IsWideContinuation && UnicodeWidth.IsWideRune(cell.Character))
				{
					pendingWideChar = cell;
					cx++;
					continue;
				}

				if (cx >= clipRect.X && cx < clipRect.Right && cx >= 0 && cx < Width && y >= 0 && y < Height)
				{
					SetCellPreservingBackground(cx, y, cell, defaultBg);
				}
				cx++;
			}

			if (pendingWideChar.HasValue)
			{
				int prevCx = cx - 1;
				if (prevCx >= clipRect.X && prevCx < clipRect.Right && prevCx >= 0 && prevCx < Width)
				{
					var space = pendingWideChar.Value;
					space.Character = new Rune(' ');
					SetCellPreservingBackground(prevCx, y, space, defaultBg);
				}
			}
		}

		private void SetCellPreservingBackground(int cx, int y, Cell cell, Color defaultBg)
		{
			if (cell.Background == defaultBg)
			{
				var existingBg = _cells[cx, y].Background;
				var preserved = cell;
				preserved.Background = existingBg;
				SetCell(cx, y, preserved);
			}
			else
			{
				SetCell(cx, y, cell);
			}
		}

		/// <summary>
		/// Fills a rectangle with the specified character and colors.
		/// </summary>
		public void FillRect(LayoutRect rect, char character, Color foreground, Color background)
			=> FillRect(rect, new Rune(character), foreground, background);

		/// <summary>
		/// Fills a rectangle with the specified Rune character and colors.
		/// </summary>
		public void FillRect(LayoutRect rect, Rune character, Color foreground, Color background)
		{
			var clipped = rect.Intersect(Bounds);
			if (clipped.IsEmpty)
				return;

			for (int y = clipped.Y; y < clipped.Bottom; y++)
			{
				for (int x = clipped.X; x < clipped.Right; x++)
				{
					SetNarrowCell(x, y, character, foreground, background);
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
		/// Fills a rectangle with space characters and the specified foreground color,
		/// while preserving the existing background color from the buffer.
		/// Used by controls to clear margin/padding areas without overwriting gradient backgrounds.
		/// </summary>
		public void FillRectPreservingBackground(LayoutRect rect, Color foregroundColor)
		{
			var clipped = rect.Intersect(Bounds);
			if (clipped.IsEmpty)
				return;

			for (int y = clipped.Y; y < clipped.Bottom; y++)
			{
				for (int x = clipped.X; x < clipped.Right; x++)
				{
					var existingBg = _cells[x, y].Background;
					SetNarrowCell(x, y, ' ', foregroundColor, existingBg);
				}
			}
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
					cell.Character = new Rune(' ');
					cell.Foreground = Color.White;
					cell.Background = background;
					cell.Decorations = TextDecoration.None;
					cell.IsWideContinuation = false;
					cell.Combiners = null;
					cell.Dirty = true;
				}
			}

			// Mark entire buffer as dirty
			_dirtyRegion = new LayoutRect(0, 0, Width, Height);
		}

		/// <summary>
		/// Fills a rectangle with a gradient applied to cell background colors.
		/// Existing characters and foreground colors are preserved.
		/// </summary>
		/// <param name="rect">The rectangle to fill.</param>
		/// <param name="gradient">The color gradient to apply.</param>
		/// <param name="direction">The direction of the gradient.</param>
		public void FillGradient(LayoutRect rect, Helpers.ColorGradient gradient, Rendering.GradientDirection direction)
		{
			Rendering.GradientRenderer.FillGradientBackground(this, rect, gradient, direction);
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
				SetNarrowCell(x + i, y, character, foreground, background);
			}
		}

		/// <summary>
		/// Draws a vertical line.
		/// </summary>
		public void DrawVerticalLine(int x, int y, int length, char character, Color foreground, Color background)
		{
			for (int i = 0; i < length; i++)
			{
				SetNarrowCell(x, y + i, character, foreground, background);
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
			SetNarrowCell(rect.X, rect.Y, chars.TopLeft, foreground, background);
			SetNarrowCell(rect.Right - 1, rect.Y, chars.TopRight, foreground, background);
			SetNarrowCell(rect.X, rect.Bottom - 1, chars.BottomLeft, foreground, background);
			SetNarrowCell(rect.Right - 1, rect.Bottom - 1, chars.BottomRight, foreground, background);

			// Top and bottom edges
			for (int x = rect.X + 1; x < rect.Right - 1; x++)
			{
				SetNarrowCell(x, rect.Y, chars.Horizontal, foreground, background);
				SetNarrowCell(x, rect.Bottom - 1, chars.Horizontal, foreground, background);
			}

			// Left and right edges
			for (int y = rect.Y + 1; y < rect.Bottom - 1; y++)
			{
				SetNarrowCell(rect.X, y, chars.Vertical, foreground, background);
				SetNarrowCell(rect.Right - 1, y, chars.Vertical, foreground, background);
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

			// Only copy dirty region for efficiency
			if (!_dirtyRegion.IsEmpty)
			{
				// Copy only the dirty region
				for (int y = _dirtyRegion.Y; y < _dirtyRegion.Bottom && y < Height; y++)
				{
					for (int x = _dirtyRegion.X; x < _dirtyRegion.Right && x < Width; x++)
					{
						_previousCells[x, y] = _cells[x, y];
						_cells[x, y].Dirty = false;
					}
				}

				_dirtyRegion = LayoutRect.Empty;
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
		/// Creates an immutable snapshot of the current buffer state.
		/// </summary>
		/// <returns>A BufferSnapshot containing a deep copy of all cells.</returns>
		/// <remarks>
		/// This method performs a deep copy of all cells, making the snapshot
		/// independent of the original buffer. Useful for screenshots, recording,
		/// compositing, or capturing state for diagnostics.
		/// </remarks>
		public BufferSnapshot CreateSnapshot()
		{
			var cells = new Cell[Width, Height];
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					cells[x, y] = _cells[x, y];
				}
			}
			return new BufferSnapshot(Width, Height, cells);
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
				TextDecoration lastDec = TextDecoration.None;
				bool firstCell = true;

				for (int x = 0; x < Width; x++)
				{
					var cell = _cells[x, y];

					// Skip continuation cells — the terminal auto-advances for wide chars
					if (cell.IsWideContinuation)
					{
						// Safety: emit any combiners attached to continuation cell
						if (cell.Combiners != null)
							sb.Append(cell.Combiners);
						continue;
					}

					bool fgChanged = lastFg == null || !cell.Foreground.Equals(lastFg.Value);
					bool bgChanged = lastBg == null || !cell.Background.Equals(lastBg.Value);
					bool decChanged = !firstCell && cell.Decorations != lastDec;

					if (fgChanged || bgChanged || decChanged || (firstCell && cell.Decorations != TextDecoration.None))
					{
						// When decorations change, emit reset first to clear previous decorations,
						// then re-emit all attributes. This is simpler and more reliable than
						// tracking individual decoration on/off codes.
						if (decChanged && lastDec != TextDecoration.None)
						{
							sb.Append("\x1b[0m");
							// Full reset clears colors too - force re-emission
							lastFg = null;
							lastBg = null;
							fgChanged = true;
							bgChanged = true;
						}

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
						else if (bgChanged)
						{
							sb.Append($"48;2;{cell.Background.R};{cell.Background.G};{cell.Background.B}");
						}
						// Append decoration SGR codes
						if (cell.Decorations != TextDecoration.None)
						{
							if ((cell.Decorations & TextDecoration.Bold) != 0) sb.Append(";1");
							if ((cell.Decorations & TextDecoration.Dim) != 0) sb.Append(";2");
							if ((cell.Decorations & TextDecoration.Italic) != 0) sb.Append(";3");
							if ((cell.Decorations & TextDecoration.Underline) != 0) sb.Append(";4");
							if ((cell.Decorations & TextDecoration.Blink) != 0) sb.Append(";5");
							if ((cell.Decorations & TextDecoration.Invert) != 0) sb.Append(";7");
							if ((cell.Decorations & TextDecoration.Strikethrough) != 0) sb.Append(";9");
						}
						sb.Append('m');

						lastFg = cell.Foreground;
						lastBg = cell.Background;
						lastDec = cell.Decorations;
					}

					firstCell = false;
					sb.AppendRune(cell.Character);
					if (cell.Combiners != null) sb.Append(cell.Combiners);
				}

				// Reset at end of line
				sb.Append("\x1b[0m");
				lines.Add(sb.ToString());
			}

			// Diagnostics: Capture ANSI lines snapshot
			if (_diagnostics?.IsEnabled == true && _diagnostics.EnabledLayers.HasFlag(Configuration.DiagnosticsLayers.AnsiLines))
			{
				_diagnostics.CaptureAnsiLines(lines);
			}

			return lines;
		}
	}
}
