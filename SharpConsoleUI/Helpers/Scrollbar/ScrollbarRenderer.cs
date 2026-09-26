// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Helpers.Scrollbar
{
	/// <summary>Which way a scrollbar runs.</summary>
	public enum ScrollbarAxis
	{
		/// <summary>Top to bottom.</summary>
		Vertical,

		/// <summary>Left to right.</summary>
		Horizontal
	}

	/// <summary>The three colours a scrollbar paints with.</summary>
	/// <param name="Thumb">The draggable thumb, and the arrows.</param>
	/// <param name="Track">The groove behind the thumb.</param>
	/// <param name="Background">Cell background for every scrollbar cell.</param>
	public readonly record struct ScrollbarPalette(Color Thumb, Color Track, Color Background);

	/// <summary>
	/// Draws a scrollbar on either axis.
	/// </summary>
	/// <remarks>
	/// The arrow glyphs are U+25B2/U+25BC vertically and U+25C4/U+25BA horizontally. CLAUDE.md rule
	/// 12E prefers narrower alternatives for new indicators, but these already ship in several
	/// controls and render correctly in the terminals this library targets: keeping them is a
	/// deliberate exception, not an oversight.
	/// </remarks>
	internal static class ScrollbarRenderer
	{
		private const char VerticalThumbChar = '█';   // █
		private const char VerticalTrackChar = '│';   // │
		private const char UpArrowChar = '▲';         // ▲
		private const char DownArrowChar = '▼';       // ▼

		private const char HorizontalThumbChar = '▬'; // ▬
		private const char HorizontalTrackChar = '─'; // ─
		private const char LeftArrowChar = '◄';       // ◄
		private const char RightArrowChar = '►';      // ►

		/// <summary>
		/// Paints the bar. <paramref name="x"/> and <paramref name="y"/> are its first cell; the bar
		/// runs <see cref="ScrollbarMetrics.TrackLength"/> cells along <paramref name="axis"/>.
		/// </summary>
		/// <param name="buffer">The buffer to paint into.</param>
		/// <param name="axis">Whether the bar runs vertically or horizontally.</param>
		/// <param name="x">Column of the bar's first cell.</param>
		/// <param name="y">Row of the bar's first cell.</param>
		/// <param name="m">The bar's geometry.</param>
		/// <param name="palette">The colours to paint with.</param>
		/// <param name="clip">When given, cells outside it are not written (nested-panel overdraw).</param>
		/// <param name="drawArrows">False in overlay mode, where the bar sits on a window border.</param>
		/// <param name="drawTrack">False in overlay mode: only the thumb is painted.</param>
		public static void Draw(
			CharacterBuffer buffer,
			ScrollbarAxis axis,
			int x,
			int y,
			in ScrollbarMetrics m,
			in ScrollbarPalette palette,
			LayoutRect? clip = null,
			bool drawArrows = true,
			bool drawTrack = true)
		{
			if (m.TrackLength <= 0) return;

			bool vertical = axis == ScrollbarAxis.Vertical;
			char thumbChar = vertical ? VerticalThumbChar : HorizontalThumbChar;
			char trackChar = vertical ? VerticalTrackChar : HorizontalTrackChar;
			char startArrow = vertical ? UpArrowChar : LeftArrowChar;
			char endArrow = vertical ? DownArrowChar : RightArrowChar;

			int thumbPos = ScrollbarGeometry.ThumbPosForOffset(m);
			int thumbLen = ScrollbarGeometry.ThumbLength(m);
			bool hasArrows = drawArrows && ScrollbarGeometry.ArrowSlots(m.TrackLength) > 0;

			for (int i = 0; i < m.TrackLength; i++)
			{
				bool isThumb = i >= thumbPos && i < thumbPos + thumbLen;
				if (!isThumb && !drawTrack) continue;

				Put(buffer, vertical ? x : x + i, vertical ? y + i : y,
					isThumb ? thumbChar : trackChar,
					isThumb ? palette.Thumb : palette.Track,
					palette.Background, clip);
			}

			if (hasArrows)
			{
				Put(buffer, x, y, startArrow, palette.Thumb, palette.Background, clip);
				Put(buffer,
					vertical ? x : x + m.TrackLength - 1,
					vertical ? y + m.TrackLength - 1 : y,
					endArrow, palette.Thumb, palette.Background, clip);
			}
		}

		private static void Put(CharacterBuffer buffer, int x, int y, char c, Color fg, Color bg, LayoutRect? clip)
		{
			if (clip is { } r && (x < r.X || x >= r.X + r.Width || y < r.Y || y >= r.Y + r.Height))
				return;

			buffer.SetNarrowCell(x, y, c, fg, bg);
		}
	}
}
