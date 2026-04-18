using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

public class HalfBlockImageRendererTests
{
    private static readonly Color WindowBg = Color.Black;

    [Fact]
    public void Paint_WritesHalfBlockCells_MatchingDirectRenderer()
    {
        var pixels = new PixelBuffer(4, 4);
        pixels.SetPixel(0, 0, new ImagePixel(255, 0, 0));
        pixels.SetPixel(1, 0, new ImagePixel(0, 255, 0));
        pixels.SetPixel(2, 0, new ImagePixel(0, 0, 255));
        pixels.SetPixel(3, 0, new ImagePixel(255, 255, 0));
        pixels.SetPixel(0, 1, new ImagePixel(0, 255, 255));
        pixels.SetPixel(1, 1, new ImagePixel(255, 0, 255));
        pixels.SetPixel(2, 1, new ImagePixel(128, 128, 128));
        pixels.SetPixel(3, 1, new ImagePixel(64, 64, 64));

        var expectedCells = HalfBlockRenderer.Render(pixels, WindowBg);

        using var renderer = new HalfBlockImageRenderer();
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(0, 0, 4, 2);

        renderer.Paint(buffer, bounds, bounds, pixels, ImageScaleMode.None,
            cropOffsetX: 0, cropOffsetY: 0, renderCols: 4, renderRows: 2, windowBackground: WindowBg);

        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                var actual = buffer.GetCell(x, y);
                var expected = expectedCells[x, y];
                Assert.Equal(expected.Character, actual.Character);
                Assert.Equal(expected.Foreground, actual.Foreground);
                Assert.Equal(expected.Background, actual.Background);
            }
        }
    }

    [Fact]
    public void Paint_Scaled_ProducesCorrectDimensions()
    {
        var pixels = new PixelBuffer(10, 10);
        using var renderer = new HalfBlockImageRenderer();
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(5, 3, 5, 3);

        renderer.Paint(buffer, bounds, bounds, pixels, ImageScaleMode.Fit,
            cropOffsetX: 0, cropOffsetY: 0, renderCols: 5, renderRows: 3, windowBackground: WindowBg);

        var cell = buffer.GetCell(5, 3);
        Assert.Equal(new Rune(ImagingDefaults.HalfBlockChar), cell.Character);
    }

    [Fact]
    public void Paint_RespectsClipRect()
    {
        var pixels = new PixelBuffer(4, 4);
        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
                pixels.SetPixel(x, y, new ImagePixel(255, 255, 255));

        using var renderer = new HalfBlockImageRenderer();
        var buffer = new CharacterBuffer(80, 25);
        var bounds = new LayoutRect(0, 0, 4, 2);
        var clipRect = new LayoutRect(1, 0, 2, 1);

        renderer.Paint(buffer, bounds, clipRect, pixels, ImageScaleMode.None,
            cropOffsetX: 0, cropOffsetY: 0, renderCols: 4, renderRows: 2, windowBackground: WindowBg);

        var outsideCell = buffer.GetCell(0, 0);
        Assert.NotEqual(new Rune(ImagingDefaults.HalfBlockChar), outsideCell.Character);

        var insideCell = buffer.GetCell(1, 0);
        Assert.Equal(new Rune(ImagingDefaults.HalfBlockChar), insideCell.Character);
    }

    [Fact]
    public void OnSourceChanged_DoesNotThrow()
    {
        using var renderer = new HalfBlockImageRenderer();
        renderer.OnSourceChanged();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var renderer = new HalfBlockImageRenderer();
        renderer.Dispose();
    }
}
