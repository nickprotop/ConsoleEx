using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit;

public class GradientRendererTests
{
	#region Horizontal Gradient

	[Fact]
	public void FillGradientBackground_Horizontal_EndpointsMatchGradientColors()
	{
		var buffer = new CharacterBuffer(10, 1);
		var gradient = ColorGradient.FromColors(Color.Black, Color.White);
		var rect = new LayoutRect(0, 0, 10, 1);

		GradientRenderer.FillGradientBackground(buffer, rect, gradient, GradientDirection.Horizontal);

		// First cell should be black (start of gradient)
		Assert.Equal(Color.Black, buffer.GetCell(0, 0).Background);
		// Last cell should be white (end of gradient)
		Assert.Equal(Color.White, buffer.GetCell(9, 0).Background);
	}

	[Fact]
	public void FillGradientBackground_Horizontal_MidpointIsInterpolated()
	{
		var buffer = new CharacterBuffer(3, 1);
		var gradient = ColorGradient.FromColors(Color.Black, Color.White);
		var rect = new LayoutRect(0, 0, 3, 1);

		GradientRenderer.FillGradientBackground(buffer, rect, gradient, GradientDirection.Horizontal);

		// Midpoint should be approximately gray (127 or 128)
		var mid = buffer.GetCell(1, 0).Background;
		Assert.InRange(mid.R, 125, 130);
		Assert.InRange(mid.G, 125, 130);
		Assert.InRange(mid.B, 125, 130);
	}

	#endregion

	#region Vertical Gradient

	[Fact]
	public void FillGradientBackground_Vertical_EndpointsMatchGradientColors()
	{
		var buffer = new CharacterBuffer(1, 10);
		var gradient = ColorGradient.FromColors(Color.Red, Color.Blue);
		var rect = new LayoutRect(0, 0, 1, 10);

		GradientRenderer.FillGradientBackground(buffer, rect, gradient, GradientDirection.Vertical);

		Assert.Equal(Color.Red, buffer.GetCell(0, 0).Background);
		Assert.Equal(Color.Blue, buffer.GetCell(0, 9).Background);
	}

	[Fact]
	public void FillGradientBackground_Vertical_SameColorAcrossRow()
	{
		var buffer = new CharacterBuffer(5, 3);
		var gradient = ColorGradient.FromColors(Color.Black, Color.White);
		var rect = new LayoutRect(0, 0, 5, 3);

		GradientRenderer.FillGradientBackground(buffer, rect, gradient, GradientDirection.Vertical);

		// All cells in the same row should have the same background
		var row1Color = buffer.GetCell(0, 1).Background;
		for (int x = 1; x < 5; x++)
		{
			Assert.Equal(row1Color, buffer.GetCell(x, 1).Background);
		}
	}

	#endregion

	#region Diagonal Gradient

	[Fact]
	public void FillGradientBackground_DiagonalDown_TopLeftIsStart()
	{
		var buffer = new CharacterBuffer(5, 5);
		var gradient = ColorGradient.FromColors(Color.Black, Color.White);
		var rect = new LayoutRect(0, 0, 5, 5);

		GradientRenderer.FillGradientBackground(buffer, rect, gradient, GradientDirection.DiagonalDown);

		Assert.Equal(Color.Black, buffer.GetCell(0, 0).Background);
		Assert.Equal(Color.White, buffer.GetCell(4, 4).Background);
	}

	[Fact]
	public void FillGradientBackground_DiagonalUp_BottomLeftIsStart()
	{
		var buffer = new CharacterBuffer(5, 5);
		var gradient = ColorGradient.FromColors(Color.Black, Color.White);
		var rect = new LayoutRect(0, 0, 5, 5);

		GradientRenderer.FillGradientBackground(buffer, rect, gradient, GradientDirection.DiagonalUp);

		Assert.Equal(Color.Black, buffer.GetCell(0, 4).Background);
		Assert.Equal(Color.White, buffer.GetCell(4, 0).Background);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void FillGradientBackground_SingleCell_UsesStartColor()
	{
		var buffer = new CharacterBuffer(5, 5);
		var gradient = ColorGradient.FromColors(Color.Red, Color.Blue);
		var rect = new LayoutRect(2, 2, 1, 1);

		GradientRenderer.FillGradientBackground(buffer, rect, gradient, GradientDirection.Horizontal);

		Assert.Equal(Color.Red, buffer.GetCell(2, 2).Background);
	}

	[Fact]
	public void FillGradientBackground_EmptyRect_NoChanges()
	{
		var buffer = new CharacterBuffer(5, 5);
		buffer.Clear(Color.Black);
		buffer.Commit();

		var gradient = ColorGradient.FromColors(Color.Red, Color.Blue);
		var rect = new LayoutRect(0, 0, 0, 0);

		GradientRenderer.FillGradientBackground(buffer, rect, gradient, GradientDirection.Horizontal);

		// All cells should still be black
		for (int y = 0; y < 5; y++)
			for (int x = 0; x < 5; x++)
				Assert.Equal(Color.Black, buffer.GetCell(x, y).Background);
	}

	[Fact]
	public void FillGradientBackground_ClipsToBufferBounds()
	{
		var buffer = new CharacterBuffer(5, 5);
		var gradient = ColorGradient.FromColors(Color.Red, Color.Blue);
		// Rect extends beyond buffer
		var rect = new LayoutRect(3, 3, 10, 10);

		// Should not throw
		GradientRenderer.FillGradientBackground(buffer, rect, gradient, GradientDirection.Horizontal);

		// Cells within bounds should be colored
		Assert.NotEqual(Color.Black, buffer.GetCell(3, 3).Background);
	}

	#endregion

	#region Foreground Gradient

	[Fact]
	public void FillGradientForeground_PreservesExistingCharactersAndBackground()
	{
		var buffer = new CharacterBuffer(5, 1);
		buffer.WriteString(0, 0, "Hello", Color.White, Color.DarkBlue);

		var gradient = ColorGradient.FromColors(Color.Red, Color.Green);
		var rect = new LayoutRect(0, 0, 5, 1);

		GradientRenderer.FillGradientForeground(buffer, rect, gradient, GradientDirection.Horizontal);

		// Characters should be preserved
		Assert.Equal('H', buffer.GetCell(0, 0).Character);
		Assert.Equal('o', buffer.GetCell(4, 0).Character);

		// Background should be preserved
		Assert.Equal(Color.DarkBlue, buffer.GetCell(0, 0).Background);

		// Foreground should be gradient colors
		Assert.Equal(Color.Red, buffer.GetCell(0, 0).Foreground);
		Assert.Equal(Color.Green, buffer.GetCell(4, 0).Foreground);
	}

	#endregion

	#region Normalized Position Calculation

	[Fact]
	public void CalculateNormalizedPosition_Horizontal_CorrectRange()
	{
		double start = GradientRenderer.CalculateNormalizedPosition(0, 0, 0, 0, 10, 1, GradientDirection.Horizontal);
		double end = GradientRenderer.CalculateNormalizedPosition(9, 0, 0, 0, 10, 1, GradientDirection.Horizontal);

		Assert.Equal(0.0, start);
		Assert.Equal(1.0, end);
	}

	[Fact]
	public void CalculateNormalizedPosition_SingleWidth_ReturnsZero()
	{
		double t = GradientRenderer.CalculateNormalizedPosition(0, 0, 0, 0, 1, 1, GradientDirection.Horizontal);
		Assert.Equal(0.0, t);
	}

	#endregion
}
