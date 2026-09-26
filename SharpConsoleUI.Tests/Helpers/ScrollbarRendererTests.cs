// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Helpers.Scrollbar;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class ScrollbarRendererTests
{
	private static readonly ScrollbarPalette Palette =
		new(Color.Cyan, Color.Grey, Color.Black);

	private static ScrollbarMetrics M(int track = 12, int offset = 0)
		=> new(track, ContentExtent: 100, ViewportExtent: 50, Offset: offset);

	// Cell.Character is a Rune, not a char: use ToString() so surrogate pairs survive.
	private static string ColumnText(CharacterBuffer buffer, int x, int height)
	{
		var sb = new System.Text.StringBuilder();
		for (int y = 0; y < height; y++)
			sb.Append(buffer.GetCell(x, y).Character.ToString());
		return sb.ToString();
	}

	private static string RowText(CharacterBuffer buffer, int y, int width)
	{
		var sb = new System.Text.StringBuilder();
		for (int x = 0; x < width; x++)
			sb.Append(buffer.GetCell(x, y).Character.ToString());
		return sb.ToString();
	}

	[Fact]
	public void Vertical_DrawsArrowsThumbAndTrack()
	{
		var buffer = new CharacterBuffer(4, 12);
		ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Vertical, x: 0, y: 0, M(), Palette);

		// Arrow, 5 thumb cells, 5 track cells, arrow.
		Assert.Equal("▲█████│││││▼", ColumnText(buffer, 0, 12));
	}

	[Fact]
	public void Horizontal_DrawsItsOwnGlyphs()
	{
		var buffer = new CharacterBuffer(12, 4);
		ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Horizontal, x: 0, y: 0, M(), Palette);

		Assert.Equal("◄▬▬▬▬▬─────►", RowText(buffer, 0, 12));
	}

	[Fact]
	public void ThumbMovesWithTheOffset()
	{
		var buffer = new CharacterBuffer(4, 12);
		ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Vertical, 0, 0, M(offset: 50), Palette);

		Assert.Equal("▲│││││█████▼", ColumnText(buffer, 0, 12));
	}

	[Fact]
	public void ShortTrack_DrawsNoArrows()
	{
		var buffer = new CharacterBuffer(4, 2);
		ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Vertical, 0, 0, M(track: 2), Palette);

		Assert.DoesNotContain("▲", ColumnText(buffer, 0, 2));
		Assert.DoesNotContain("▼", ColumnText(buffer, 0, 2));
	}

	[Fact]
	public void OverlayMode_DrawsOnlyTheThumb()
	{
		var buffer = new CharacterBuffer(4, 12);
		for (int y = 0; y < 12; y++)
			buffer.SetNarrowCell(0, y, '#', Color.White, Color.Black);

		ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Vertical, 0, 0, M(), Palette,
			clip: null, drawArrows: false, drawTrack: false);

		// Thumb painted, everything else left as the border character underneath.
		Assert.Equal("#█████######", ColumnText(buffer, 0, 12));
	}

	[Fact]
	public void ClipRect_KeepsWritesInside()
	{
		var buffer = new CharacterBuffer(4, 12);
		for (int y = 0; y < 12; y++)
			buffer.SetNarrowCell(0, y, '#', Color.White, Color.Black);

		// Only rows 2..5 may be written.
		ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Vertical, 0, 0, M(), Palette,
			clip: new LayoutRect(0, 2, 4, 4));

		// Every glyph here is BMP, so indexing the string by char is safe.
		string column = ColumnText(buffer, 0, 12);
		Assert.Equal('#', column[0]);
		Assert.Equal('#', column[1]);
		Assert.NotEqual('#', column[2]);
		Assert.NotEqual('#', column[5]);
		Assert.Equal('#', column[6]);
		Assert.Equal('#', column[11]);
	}

	[Fact]
	public void DegenerateTrack_DrawsNothingAndDoesNotThrow()
	{
		var buffer = new CharacterBuffer(4, 4);
		ScrollbarRenderer.Draw(buffer, ScrollbarAxis.Vertical, 0, 0, M(track: 0), Palette);
	}
}
