// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Shared box-border drawing primitives used by panel-style controls.
	/// Extracted from <see cref="Controls.PanelControl"/> so both it and
	/// <see cref="Controls.CollapsiblePanel"/> can render identical bordered chrome
	/// without duplicating the drawing logic.
	/// </summary>
	public static class PanelBorderRenderer
	{
		/// <summary>
		/// Draws the top border line with optional header text embedded.
		/// </summary>
		/// <param name="buffer">The character buffer to draw into.</param>
		/// <param name="x">The left edge column.</param>
		/// <param name="y">The row to draw on.</param>
		/// <param name="width">The total width of the border row (including corners).</param>
		/// <param name="clipRect">The clipping rectangle.</param>
		/// <param name="box">The box-drawing character set.</param>
		/// <param name="borderColor">The border foreground color.</param>
		/// <param name="bgColor">The border background color.</param>
		/// <param name="header">Optional header text embedded in the top border (supports markup).</param>
		/// <param name="headerAlignment">The horizontal alignment of the header text.</param>
		public static void DrawTopBorder(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor, string? header, TextJustification headerAlignment)
		{
			DrawTitledLine(buffer, x, y, width, clipRect, box, borderColor, bgColor, header, headerAlignment, box.TopLeft, box.TopRight);
		}

		/// <summary>
		/// Draws a flat horizontal rule with the header text embedded, using the box horizontal
		/// character at both ends instead of corner glyphs. Used to render a collapsed bordered
		/// <see cref="Controls.CollapsiblePanel"/> header as a titled rule (no dangling corners).
		/// </summary>
		/// <param name="buffer">The character buffer to draw into.</param>
		/// <param name="x">The left edge column.</param>
		/// <param name="y">The row to draw on.</param>
		/// <param name="width">The total width of the rule row.</param>
		/// <param name="clipRect">The clipping rectangle.</param>
		/// <param name="box">The box-drawing character set.</param>
		/// <param name="borderColor">The rule foreground color.</param>
		/// <param name="bgColor">The rule background color.</param>
		/// <param name="header">Optional header text embedded in the rule (supports markup).</param>
		/// <param name="headerAlignment">The horizontal alignment of the header text.</param>
		public static void DrawTitledRule(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor, string? header, TextJustification headerAlignment)
		{
			DrawTitledLine(buffer, x, y, width, clipRect, box, borderColor, bgColor, header, headerAlignment, box.Horizontal, box.Horizontal);
		}

		/// <summary>
		/// Shared core that draws a single horizontal line with an optional title embedded, using
		/// the supplied end-cap characters. <see cref="DrawTopBorder"/> passes the box corners
		/// (<c>┌</c>/<c>┐</c>) and <see cref="DrawTitledRule"/> passes the horizontal char at both
		/// ends. Keeps the title-embedding logic in one place (CLAUDE.md rule #1).
		/// </summary>
		private static void DrawTitledLine(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor, string? header, TextJustification headerAlignment, char leftCap, char rightCap)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			int innerWidth = width - 2; // minus end caps

			// Left cap
			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(x, y, leftCap, borderColor, cellBg);
			}

			if (string.IsNullOrEmpty(header) || innerWidth < 4)
			{
				// No header — fill with horizontal chars
				for (int i = 0; i < innerWidth; i++)
				{
					int px = x + 1 + i;
					if (px >= clipRect.X && px < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
					}
				}
			}
			else
			{
				// Parse header and calculate position
				var headerCells = MarkupParser.Parse(header, borderColor, bgColor);
				int headerLen = headerCells.Count;
				int headerWithSpaces = headerLen + 2; // space before and after

				if (headerWithSpaces > innerWidth)
				{
					// Header too long — just fill with horizontal
					for (int i = 0; i < innerWidth; i++)
					{
						int px = x + 1 + i;
						if (px >= clipRect.X && px < clipRect.Right)
						{
							var cellBg = bgColor;
							buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
						}
					}
				}
				else
				{
					int dashSpace = innerWidth - headerWithSpaces;
					int leftDashes, rightDashes;

					switch (headerAlignment)
					{
						case TextJustification.Center:
							leftDashes = dashSpace / 2;
							rightDashes = dashSpace - leftDashes;
							break;
						case TextJustification.Right:
							leftDashes = dashSpace - 1;
							rightDashes = 1;
							break;
						default: // Left
							leftDashes = 1;
							rightDashes = dashSpace - 1;
							break;
					}

					int writeX = x + 1;

					// Left dashes
					for (int i = 0; i < leftDashes; i++)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = bgColor;
							buffer.SetNarrowCell(writeX, y, box.Horizontal, borderColor, cellBg);
						}
						writeX++;
					}

					// Space + header + space
					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(writeX, y, ' ', borderColor, cellBg);
					}
					writeX++;

					foreach (var cell in headerCells)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							buffer.SetCell(writeX, y, cell);
						}
						writeX++;
					}

					if (writeX >= clipRect.X && writeX < clipRect.Right)
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(writeX, y, ' ', borderColor, cellBg);
					}
					writeX++;

					// Right dashes
					for (int i = 0; i < rightDashes; i++)
					{
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = bgColor;
							buffer.SetNarrowCell(writeX, y, box.Horizontal, borderColor, cellBg);
						}
						writeX++;
					}
				}
			}

			// Right cap
			int rightX = x + width - 1;
			if (rightX >= clipRect.X && rightX < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(rightX, y, rightCap, borderColor, cellBg);
			}
		}

		/// <summary>
		/// Draws the bottom border line.
		/// </summary>
		/// <param name="buffer">The character buffer to draw into.</param>
		/// <param name="x">The left edge column.</param>
		/// <param name="y">The row to draw on.</param>
		/// <param name="width">The total width of the border row (including corners).</param>
		/// <param name="clipRect">The clipping rectangle.</param>
		/// <param name="box">The box-drawing character set.</param>
		/// <param name="borderColor">The border foreground color.</param>
		/// <param name="bgColor">The border background color.</param>
		public static void DrawBottomBorder(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(x, y, box.BottomLeft, borderColor, cellBg);
			}

			int innerWidth = width - 2;
			for (int i = 0; i < innerWidth; i++)
			{
				int px = x + 1 + i;
				if (px >= clipRect.X && px < clipRect.Right)
				{
					var cellBg = bgColor;
					buffer.SetNarrowCell(px, y, box.Horizontal, borderColor, cellBg);
				}
			}

			int rightX = x + width - 1;
			if (rightX >= clipRect.X && rightX < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(rightX, y, box.BottomRight, borderColor, cellBg);
			}
		}

		/// <summary>
		/// Draws a row with vertical borders and content between them.
		/// </summary>
		/// <param name="buffer">The character buffer to draw into.</param>
		/// <param name="x">The left edge column.</param>
		/// <param name="y">The row to draw on.</param>
		/// <param name="width">The total width of the row (including borders).</param>
		/// <param name="clipRect">The clipping rectangle.</param>
		/// <param name="box">The box-drawing character set.</param>
		/// <param name="borderColor">The border foreground color.</param>
		/// <param name="bgColor">The interior background color.</param>
		/// <param name="contentCells">Optional content cells to render in the interior.</param>
		/// <param name="contentOffset">The left padding offset before content begins.</param>
		public static void DrawBorderedRow(CharacterBuffer buffer, int x, int y, int width, LayoutRect clipRect, BoxChars box, Color borderColor, Color bgColor, List<Cell>? contentCells = null, int contentOffset = 0)
		{
			if (y < clipRect.Y || y >= clipRect.Bottom) return;

			int innerWidth = width - 2;

			// Left border
			if (x >= clipRect.X && x < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(x, y, box.Vertical, borderColor, cellBg);
			}

			// Inner area
			int innerX = x + 1;
			for (int i = 0; i < innerWidth; i++)
			{
				int px = innerX + i;
				if (px >= clipRect.X && px < clipRect.Right)
				{
					int contentIdx = i - contentOffset;
					if (contentCells != null && contentIdx >= 0 && contentIdx < contentCells.Count)
					{
						var cell = contentCells[contentIdx];
						buffer.SetCell(px, y, cell);
					}
					else
					{
						var cellBg = bgColor;
						buffer.SetNarrowCell(px, y, ' ', borderColor, cellBg);
					}
				}
			}

			// Right border
			int rightX = x + width - 1;
			if (rightX >= clipRect.X && rightX < clipRect.Right)
			{
				var cellBg = bgColor;
				buffer.SetNarrowCell(rightX, y, box.Vertical, borderColor, cellBg);
			}
		}
	}
}
