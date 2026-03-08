using SharpConsoleUI.Imaging;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

public class PixelBufferTests
{
	[Fact]
	public void SetGet_Roundtrip()
	{
		var buffer = new PixelBuffer(10, 10);
		var pixel = new ImagePixel(255, 128, 64);

		buffer.SetPixel(3, 5, pixel);
		var result = buffer.GetPixel(3, 5);

		Assert.Equal(pixel, result);
	}

	[Fact]
	public void GetPixel_OutOfBounds_ReturnsDefault()
	{
		var buffer = new PixelBuffer(5, 5);

		Assert.Equal(default, buffer.GetPixel(-1, 0));
		Assert.Equal(default, buffer.GetPixel(0, -1));
		Assert.Equal(default, buffer.GetPixel(5, 0));
		Assert.Equal(default, buffer.GetPixel(0, 5));
	}

	[Fact]
	public void SetPixel_OutOfBounds_DoesNotThrow()
	{
		var buffer = new PixelBuffer(5, 5);

		buffer.SetPixel(-1, 0, new ImagePixel(255, 0, 0));
		buffer.SetPixel(5, 0, new ImagePixel(255, 0, 0));
	}

	[Fact]
	public void Resize_ProduceCorrectDimensions()
	{
		var buffer = new PixelBuffer(10, 20);
		var resized = buffer.Resize(5, 10);

		Assert.Equal(5, resized.Width);
		Assert.Equal(10, resized.Height);
	}

	[Fact]
	public void Resize_PreservesCornerPixels()
	{
		var buffer = new PixelBuffer(2, 2);
		var red = new ImagePixel(255, 0, 0);
		buffer.SetPixel(0, 0, red);
		buffer.SetPixel(1, 0, red);
		buffer.SetPixel(0, 1, red);
		buffer.SetPixel(1, 1, red);

		var resized = buffer.Resize(4, 4);

		// All pixels should be red (uniform source)
		for (int y = 0; y < 4; y++)
			for (int x = 0; x < 4; x++)
				Assert.Equal(red, resized.GetPixel(x, y));
	}

	[Fact]
	public void FromPixelArray_CorrectLayout()
	{
		var pixels = new ImagePixel[]
		{
			new(1, 0, 0), new(2, 0, 0),
			new(3, 0, 0), new(4, 0, 0),
		};

		var buffer = PixelBuffer.FromPixelArray(pixels, 2, 2);

		Assert.Equal(new ImagePixel(1, 0, 0), buffer.GetPixel(0, 0));
		Assert.Equal(new ImagePixel(2, 0, 0), buffer.GetPixel(1, 0));
		Assert.Equal(new ImagePixel(3, 0, 0), buffer.GetPixel(0, 1));
		Assert.Equal(new ImagePixel(4, 0, 0), buffer.GetPixel(1, 1));
	}

	[Fact]
	public void FromPixelArray_WrongLength_Throws()
	{
		var pixels = new ImagePixel[] { new(1, 0, 0) };
		Assert.Throws<ArgumentException>(() => PixelBuffer.FromPixelArray(pixels, 2, 2));
	}

	[Fact]
	public void FromArgbArray_ExtractsRgb()
	{
		int argb = (0xFF << 24) | (0xAA << 16) | (0xBB << 8) | 0xCC;
		var buffer = PixelBuffer.FromArgbArray(new[] { argb }, 1, 1);

		var pixel = buffer.GetPixel(0, 0);
		Assert.Equal(0xAA, pixel.R);
		Assert.Equal(0xBB, pixel.G);
		Assert.Equal(0xCC, pixel.B);
	}

	[Fact]
	public void Constructor_InvalidDimensions_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(0, 10));
		Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(10, 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => new PixelBuffer(-1, 10));
	}
}
