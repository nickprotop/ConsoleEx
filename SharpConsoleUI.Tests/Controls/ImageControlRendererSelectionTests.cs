using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ImageControlRendererSelectionTests
{
	[Fact]
	public void PaintDOM_WithoutGraphicsProtocol_UsesHalfBlockRendering()
	{
		var pixels = new PixelBuffer(4, 4);
		pixels.SetPixel(0, 0, new ImagePixel(255, 0, 0));
		pixels.SetPixel(1, 0, new ImagePixel(0, 255, 0));
		pixels.SetPixel(0, 1, new ImagePixel(0, 0, 255));
		pixels.SetPixel(1, 1, new ImagePixel(255, 255, 0));

		var control = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.None };
		var buffer = new CharacterBuffer(80, 25);
		var bounds = new LayoutRect(0, 0, 40, 10);

		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		var cell = buffer.GetCell(0, 0);
		Assert.Equal(new Rune(ImagingDefaults.HalfBlockChar), cell.Character);
	}

	[Fact]
	public void PaintDOM_ExistingTests_StillPass_WithRefactoring()
	{
		var pixels = new PixelBuffer(2, 2);
		pixels.SetPixel(0, 0, new ImagePixel(255, 0, 0));
		pixels.SetPixel(1, 0, new ImagePixel(0, 255, 0));
		pixels.SetPixel(0, 1, new ImagePixel(0, 0, 255));
		pixels.SetPixel(1, 1, new ImagePixel(255, 255, 0));

		var control = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.None };
		var charBuffer = new CharacterBuffer(80, 25);
		var bounds = new LayoutRect(0, 0, 40, 10);

		control.PaintDOM(charBuffer, bounds, bounds, Color.White, Color.Black);

		var cell = charBuffer.GetCell(0, 0);
		Assert.Equal(new Rune(ImagingDefaults.HalfBlockChar), cell.Character);
		Assert.Equal(new Color(255, 0, 0), cell.Foreground);
		Assert.Equal(new Color(0, 0, 255), cell.Background);
	}
}
