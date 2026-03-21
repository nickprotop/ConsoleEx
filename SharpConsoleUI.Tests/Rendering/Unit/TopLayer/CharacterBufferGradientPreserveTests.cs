using SharpConsoleUI.Layout;
using Xunit;
using System.Text;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class CharacterBufferGradientPreserveTests
{
	[Fact]
	public void FillRectPreservingBackground_KeepsExistingBgColor()
	{
		var buffer = new CharacterBuffer(5, 1);
		// Pre-fill with gradient-like distinct backgrounds
		buffer.SetNarrowCell(0, 0, 'X', Color.White, Color.Red);
		buffer.SetNarrowCell(1, 0, 'X', Color.White, Color.Green);
		buffer.SetNarrowCell(2, 0, 'X', Color.White, Color.Blue);
		buffer.SetNarrowCell(3, 0, 'X', Color.White, Color.Yellow);
		buffer.SetNarrowCell(4, 0, 'X', Color.White, Color.Cyan1);

		buffer.FillRectPreservingBackground(new LayoutRect(0, 0, 5, 1), Color.Grey);

		// Background colors should be preserved
		Assert.Equal(Color.Red, buffer.GetCell(0, 0).Background);
		Assert.Equal(Color.Green, buffer.GetCell(1, 0).Background);
		Assert.Equal(Color.Blue, buffer.GetCell(2, 0).Background);
		Assert.Equal(Color.Yellow, buffer.GetCell(3, 0).Background);
		Assert.Equal(Color.Cyan1, buffer.GetCell(4, 0).Background);
	}

	[Fact]
	public void FillRectPreservingBackground_WritesSpaceCharacters()
	{
		var buffer = new CharacterBuffer(3, 1);
		buffer.WriteString(0, 0, "ABC", Color.White, Color.Black);

		buffer.FillRectPreservingBackground(new LayoutRect(0, 0, 3, 1), Color.White);

		Assert.Equal(new Rune(' '), buffer.GetCell(0, 0).Character);
		Assert.Equal(new Rune(' '), buffer.GetCell(1, 0).Character);
		Assert.Equal(new Rune(' '), buffer.GetCell(2, 0).Character);
	}

	[Fact]
	public void FillRectPreservingBackground_SetsForegroundColor()
	{
		var buffer = new CharacterBuffer(3, 1);
		buffer.SetNarrowCell(0, 0, 'A', Color.White, Color.Red);
		buffer.SetNarrowCell(1, 0, 'B', Color.White, Color.Green);
		buffer.SetNarrowCell(2, 0, 'C', Color.White, Color.Blue);

		var newFg = Color.Yellow;
		buffer.FillRectPreservingBackground(new LayoutRect(0, 0, 3, 1), newFg);

		Assert.Equal(newFg, buffer.GetCell(0, 0).Foreground);
		Assert.Equal(newFg, buffer.GetCell(1, 0).Foreground);
		Assert.Equal(newFg, buffer.GetCell(2, 0).Foreground);
	}

	[Fact]
	public void FillRectPreservingBackground_ClipsToBufferBounds()
	{
		var buffer = new CharacterBuffer(3, 3);
		buffer.SetNarrowCell(0, 0, 'A', Color.White, Color.Red);

		// Rect extends beyond buffer
		buffer.FillRectPreservingBackground(new LayoutRect(-1, -1, 10, 10), Color.Grey);

		// Should not throw, and cell within bounds should be updated
		Assert.Equal(new Rune(' '), buffer.GetCell(0, 0).Character);
		Assert.Equal(Color.Red, buffer.GetCell(0, 0).Background);
	}

	// --- Alpha blending via Color.Transparent ---

	[Fact]
	public void SetNarrowCell_TransparentBg_PreservesExistingBackground()
	{
		var buffer = new CharacterBuffer(3, 1);
		buffer.SetNarrowCell(0, 0, 'X', Color.White, Color.Red);
		buffer.SetNarrowCell(1, 0, 'X', Color.White, Color.Green);
		buffer.SetNarrowCell(2, 0, 'X', Color.White, Color.Blue);

		buffer.SetNarrowCell(0, 0, ' ', Color.White, Color.Transparent);
		buffer.SetNarrowCell(1, 0, ' ', Color.White, Color.Transparent);
		buffer.SetNarrowCell(2, 0, ' ', Color.White, Color.Transparent);

		Assert.Equal(Color.Red,   buffer.GetCell(0, 0).Background);
		Assert.Equal(Color.Green, buffer.GetCell(1, 0).Background);
		Assert.Equal(Color.Blue,  buffer.GetCell(2, 0).Background);
	}

	[Fact]
	public void FillRect_TransparentBg_PreservesGradientBackground()
	{
		var buffer = new CharacterBuffer(5, 1);
		buffer.SetNarrowCell(0, 0, 'X', Color.White, Color.Red);
		buffer.SetNarrowCell(1, 0, 'X', Color.White, Color.Green);
		buffer.SetNarrowCell(2, 0, 'X', Color.White, Color.Blue);
		buffer.SetNarrowCell(3, 0, 'X', Color.White, Color.Yellow);
		buffer.SetNarrowCell(4, 0, 'X', Color.White, Color.Cyan1);

		buffer.FillRect(new LayoutRect(0, 0, 5, 1), ' ', Color.Grey, Color.Transparent);

		Assert.Equal(Color.Red,    buffer.GetCell(0, 0).Background);
		Assert.Equal(Color.Green,  buffer.GetCell(1, 0).Background);
		Assert.Equal(Color.Blue,   buffer.GetCell(2, 0).Background);
		Assert.Equal(Color.Yellow, buffer.GetCell(3, 0).Background);
		Assert.Equal(Color.Cyan1,  buffer.GetCell(4, 0).Background);
		Assert.Equal(new Rune(' '), buffer.GetCell(0, 0).Character);
	}

	[Fact]
	public void SetNarrowCell_50PercentAlpha_BlendsBackground()
	{
		var buffer = new CharacterBuffer(1, 1);
		buffer.SetNarrowCell(0, 0, 'X', Color.White, new Color(0, 0, 200, 255));

		buffer.SetNarrowCell(0, 0, 'Y', Color.White, new Color(200, 0, 0, 128));

		var result = buffer.GetCell(0, 0).Background;
		Assert.Equal(255, result.A);
		Assert.InRange(result.R, 98, 101);
		Assert.InRange(result.B, 98, 101);
	}

	[Fact]
	public void SetCell_TransparentBg_PreservesExistingBackground()
	{
		var buffer = new CharacterBuffer(1, 1);
		buffer.SetNarrowCell(0, 0, 'X', Color.White, Color.Red);

		var cell = new Cell(new Rune('Y'), Color.White, Color.Transparent);
		buffer.SetCell(0, 0, cell);

		Assert.Equal(Color.Red, buffer.GetCell(0, 0).Background);
	}
}
