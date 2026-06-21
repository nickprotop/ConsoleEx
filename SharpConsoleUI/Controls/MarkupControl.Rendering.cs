// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	public partial class MarkupControl
	{
		/// <summary>
		/// Resolves the box-drawing characters for the current border style, honoring
		/// <see cref="UseSafeBorder"/> (ASCII-safe glyphs for maximum terminal compatibility).
		/// Mirrors the old <c>PanelControl.GetBoxChars()</c> behavior.
		/// </summary>
		private BoxChars GetBorderBoxChars()
		{
			if (_useSafeBorder)
				return BoxChars.Ascii;
			return BoxChars.FromBorderStyle(_border);
		}

		/// <summary>
		/// Draws the optional border frame around the markup content. Lays down ONLY the chrome:
		/// the top border (with optional header), the bottom border, and the left/right vertical
		/// edge cells for each interior row. The interior CONTENT (text + fills) is painted by the
		/// main <see cref="PaintDOM"/> loop into the inset area, so this helper never touches the
		/// interior of content rows — it only paints the frame edges. It is therefore safe to call
		/// AFTER the content/margin paint to overwrite any spill.
		/// </summary>
		/// <param name="buffer">The character buffer to draw into.</param>
		/// <param name="bounds">The control's full bounds.</param>
		/// <param name="clipRect">The active clipping rectangle.</param>
		/// <param name="borderColor">The resolved border color.</param>
		/// <param name="bgColor">The resolved (transparent-or-explicit) background color.</param>
		/// <param name="contentRowCount">Number of painted content rows (their interiors are left intact).</param>
		/// <param name="topInset">Top inset (Margin.Top + border + padding) — first content row offset.</param>
		/// <param name="bottomInset">Bottom inset (Margin.Bottom + border + padding).</param>
		private void PaintBorderFrame(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color borderColor, Color bgColor, int contentRowCount, int topInset, int bottomInset)
		{
			var box = GetBorderBoxChars();

			// Frame spans the area inside the outer margins.
			int frameX = bounds.X + Margin.Left;
			int frameWidth = bounds.Width - Margin.Left - Margin.Right;
			if (frameWidth < 2) return; // not enough room for both vertical edges

			int topRow = bounds.Y + Margin.Top;                 // top border row
			int bottomRow = bounds.Bottom - 1 - Margin.Bottom;  // bottom border row
			if (bottomRow <= topRow) return;                     // not enough room for a real frame

			// Top border with optional header.
			PanelBorderRenderer.DrawTopBorder(buffer, frameX, topRow, frameWidth, clipRect, box, borderColor, bgColor, _header, _headerAlignment);

			// Bottom border.
			PanelBorderRenderer.DrawBottomBorder(buffer, frameX, bottomRow, frameWidth, clipRect, box, borderColor, bgColor);

			// Interior rows: paint only the left/right vertical edges. The first content row begins
			// at bounds.Y + topInset; the main loop painted contentRowCount rows from there. Rows
			// inside the frame that DON'T carry content (top/bottom padding, blank tail) had their
			// interior filled by the main loop's fills, so we still only need the edges here.
			int contentStartY = bounds.Y + topInset;
			int contentEndYExclusive = contentStartY + contentRowCount;
			int rightX = frameX + frameWidth - 1;

			for (int y = topRow + 1; y < bottomRow; y++)
			{
				if (y < clipRect.Y || y >= clipRect.Bottom)
					continue;

				bool isContentRow = y >= contentStartY && y < contentEndYExclusive;
				if (isContentRow)
				{
					// Content row: draw only the two vertical edges, leave the painted interior intact.
					if (frameX >= clipRect.X && frameX < clipRect.Right)
						buffer.SetNarrowCell(frameX, y, box.Vertical, borderColor, bgColor);
					if (rightX >= clipRect.X && rightX < clipRect.Right)
						buffer.SetNarrowCell(rightX, y, box.Vertical, borderColor, bgColor);
				}
				else
				{
					// Non-content interior row (padding / blank tail): edges + blank interior.
					PanelBorderRenderer.DrawBorderedRow(buffer, frameX, y, frameWidth, clipRect, box, borderColor, bgColor);
				}
			}
		}
	}
}
