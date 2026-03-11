using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

public class HalfBlockRendererTests
{
	private static readonly Color WindowBg = Color.Black;

	[Fact]
	public void Render_2x2Image_Produces1x2Cells()
	{
		// 2x2 pixels => 2 cols x 1 row of cells (2 pixels high / 2 = 1 row)
		var buffer = new PixelBuffer(2, 2);
		var red = new ImagePixel(255, 0, 0);
		var blue = new ImagePixel(0, 0, 255);
		buffer.SetPixel(0, 0, red);   // top-left
		buffer.SetPixel(1, 0, red);   // top-right
		buffer.SetPixel(0, 1, blue);  // bottom-left
		buffer.SetPixel(1, 1, blue);  // bottom-right

		var cells = HalfBlockRenderer.Render(buffer, WindowBg);

		Assert.Equal(2, cells.GetLength(0)); // width
		Assert.Equal(1, cells.GetLength(1)); // height

		// fg = top pixel (red), bg = bottom pixel (blue)
		Assert.Equal(new Rune(ImagingDefaults.HalfBlockChar), cells[0, 0].Character);
		Assert.Equal(new Color(255, 0, 0), cells[0, 0].Foreground);
		Assert.Equal(new Color(0, 0, 255), cells[0, 0].Background);
	}

	[Fact]
	public void Render_OddHeight_LastRowUsesWindowBackground()
	{
		// 1x3 pixels => 1 col x 2 rows (ceil(3/2) = 2)
		var buffer = new PixelBuffer(1, 3);
		buffer.SetPixel(0, 0, new ImagePixel(255, 0, 0));
		buffer.SetPixel(0, 1, new ImagePixel(0, 255, 0));
		buffer.SetPixel(0, 2, new ImagePixel(0, 0, 255));

		var bg = new Color(50, 50, 50);
		var cells = HalfBlockRenderer.Render(buffer, bg);

		Assert.Equal(1, cells.GetLength(0));
		Assert.Equal(2, cells.GetLength(1));

		// Row 0: top=red, bottom=green
		Assert.Equal(new Color(255, 0, 0), cells[0, 0].Foreground);
		Assert.Equal(new Color(0, 255, 0), cells[0, 0].Background);

		// Row 1: top=blue, bottom=window bg (odd height)
		Assert.Equal(new Color(0, 0, 255), cells[0, 1].Foreground);
		Assert.Equal(bg, cells[0, 1].Background);
	}

	[Fact]
	public void Render_AllBlack_ProducesBlackCells()
	{
		var buffer = new PixelBuffer(3, 2);
		// All pixels default to (0,0,0)

		var cells = HalfBlockRenderer.Render(buffer, WindowBg);

		for (int x = 0; x < 3; x++)
		{
			Assert.Equal(Color.Black, cells[x, 0].Foreground);
			Assert.Equal(Color.Black, cells[x, 0].Background);
		}
	}

	[Fact]
	public void Render_AllWhite_ProducesWhiteCells()
	{
		var buffer = new PixelBuffer(2, 2);
		var white = new ImagePixel(255, 255, 255);
		buffer.SetPixel(0, 0, white);
		buffer.SetPixel(1, 0, white);
		buffer.SetPixel(0, 1, white);
		buffer.SetPixel(1, 1, white);

		var cells = HalfBlockRenderer.Render(buffer, WindowBg);

		Assert.Equal(new Color(255, 255, 255), cells[0, 0].Foreground);
		Assert.Equal(new Color(255, 255, 255), cells[0, 0].Background);
	}

	[Fact]
	public void Render_SinglePixel_ProducesOneCell()
	{
		var buffer = new PixelBuffer(1, 1);
		buffer.SetPixel(0, 0, new ImagePixel(128, 64, 32));

		var bg = new Color(10, 20, 30);
		var cells = HalfBlockRenderer.Render(buffer, bg);

		Assert.Equal(1, cells.GetLength(0));
		Assert.Equal(1, cells.GetLength(1));
		Assert.Equal(new Color(128, 64, 32), cells[0, 0].Foreground);
		Assert.Equal(bg, cells[0, 0].Background); // odd height, so bg = window bg
	}

	[Fact]
	public void RenderScaled_ProducesCorrectDimensions()
	{
		var buffer = new PixelBuffer(10, 10);
		var cells = HalfBlockRenderer.RenderScaled(buffer, 5, 3, WindowBg);

		Assert.Equal(5, cells.GetLength(0));
		Assert.Equal(3, cells.GetLength(1));
	}

	[Fact]
	public void Render_AllCellsUseHalfBlockChar()
	{
		var buffer = new PixelBuffer(3, 4);
		var cells = HalfBlockRenderer.Render(buffer, WindowBg);

		for (int y = 0; y < cells.GetLength(1); y++)
			for (int x = 0; x < cells.GetLength(0); x++)
				Assert.Equal(new Rune(ImagingDefaults.HalfBlockChar), cells[x, y].Character);
	}
}
