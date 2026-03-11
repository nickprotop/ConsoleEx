using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using Xunit;
using System.Text;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class CharacterBufferGradientTests
{
	[Fact]
	public void FillGradient_AppliesGradientBackground()
	{
		var buffer = new CharacterBuffer(10, 1);
		var gradient = ColorGradient.FromColors(Color.Black, Color.White);
		var rect = new LayoutRect(0, 0, 10, 1);

		buffer.FillGradient(rect, gradient, GradientDirection.Horizontal);

		Assert.Equal(Color.Black, buffer.GetCell(0, 0).Background);
		Assert.Equal(Color.White, buffer.GetCell(9, 0).Background);
	}

	[Fact]
	public void FillGradient_PreservesExistingCharacters()
	{
		var buffer = new CharacterBuffer(5, 1);
		buffer.WriteString(0, 0, "Hello", Color.White, Color.Black);

		var gradient = ColorGradient.FromColors(Color.DarkBlue, Color.DarkRed);
		buffer.FillGradient(new LayoutRect(0, 0, 5, 1), gradient, GradientDirection.Horizontal);

		Assert.Equal(new Rune('H'), buffer.GetCell(0, 0).Character);
		Assert.Equal(new Rune('e'), buffer.GetCell(1, 0).Character);
		Assert.Equal(new Rune('l'), buffer.GetCell(2, 0).Character);
		Assert.Equal(new Rune('l'), buffer.GetCell(3, 0).Character);
		Assert.Equal(new Rune('o'), buffer.GetCell(4, 0).Character);
	}

	[Fact]
	public void FillGradient_Vertical_RowsHaveDistinctColors()
	{
		var buffer = new CharacterBuffer(3, 5);
		var gradient = ColorGradient.FromColors(Color.Black, Color.White);

		buffer.FillGradient(new LayoutRect(0, 0, 3, 5), gradient, GradientDirection.Vertical);

		// Each row should have a different background brightness
		var row0 = buffer.GetCell(0, 0).Background;
		var row4 = buffer.GetCell(0, 4).Background;

		Assert.Equal(Color.Black, row0);
		Assert.Equal(Color.White, row4);
		// Middle row should be somewhere in between
		var row2 = buffer.GetCell(0, 2).Background;
		Assert.True(row2.R > row0.R && row2.R < row4.R);
	}

	[Fact]
	public void FillGradient_DiagonalDown_CornersAreCorrect()
	{
		var buffer = new CharacterBuffer(5, 5);
		var gradient = ColorGradient.FromColors(Color.Black, Color.White);

		buffer.FillGradient(new LayoutRect(0, 0, 5, 5), gradient, GradientDirection.DiagonalDown);

		Assert.Equal(Color.Black, buffer.GetCell(0, 0).Background);
		Assert.Equal(Color.White, buffer.GetCell(4, 4).Background);
	}

	[Fact]
	public void FillGradient_SubRect_OnlyAffectsTargetRegion()
	{
		var buffer = new CharacterBuffer(10, 10);
		buffer.Clear(Color.Black);
		buffer.Commit();

		var gradient = ColorGradient.FromColors(Color.Red, Color.Blue);
		buffer.FillGradient(new LayoutRect(2, 2, 3, 3), gradient, GradientDirection.Horizontal);

		// Cell outside the rect should still be black
		Assert.Equal(Color.Black, buffer.GetCell(0, 0).Background);
		// Cell inside should be colored
		Assert.Equal(Color.Red, buffer.GetCell(2, 2).Background);
	}
}
