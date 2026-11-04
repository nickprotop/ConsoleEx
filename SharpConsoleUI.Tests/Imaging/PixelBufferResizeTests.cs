// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Imaging;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

/// <summary>
/// <see cref="PixelBuffer.Resize"/>: the size it gives, and what it does to the pixels.
/// </summary>
/// <remarks>
/// Both halves were wrong in ways nothing reported. Each side was clamped to 500 independently, so
/// an image asked to become 1568x882 came back 500x500 with its aspect ratio destroyed; and every
/// resize sampled four source pixels, which discards most of an image being reduced by more than
/// half.
/// </remarks>
public class PixelBufferResizeTests
{
	private static PixelBuffer Filled(int width, int height, Func<int, int, ImagePixel> pixel)
	{
		var buffer = new PixelBuffer(width, height);
		for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
				buffer.SetPixel(x, y, pixel(x, y));
		return buffer;
	}

	#region The size asked for

	/// <summary>
	/// The case that sent a caller away to write its own shrink: a photo reduced for sending, far
	/// larger than the old cap on both sides.
	/// </summary>
	[Fact]
	public void Resize_ToOverTheOldCap_GivesExactlyTheSizeAsked()
	{
		var source = Filled(3000, 2000, (_, _) => new ImagePixel(10, 20, 30));

		var resized = source.Resize(1568, 1045);

		Assert.Equal(1568, resized.Width);
		Assert.Equal(1045, resized.Height);
	}

	/// <summary>
	/// The old clamp was per axis, so a wide image came back square. Nothing said so; the image was
	/// simply the wrong shape from then on.
	/// </summary>
	[Fact]
	public void Resize_KeepsTheShapeItWasAskedFor_NotASquare()
	{
		var source = Filled(2000, 1000, (_, _) => new ImagePixel(1, 2, 3));

		var resized = source.Resize(1568, 784);

		Assert.NotEqual(resized.Width, resized.Height);
		Assert.Equal(2.0, (double)resized.Width / resized.Height, 3);
	}

	[Fact]
	public void Resize_RefusesANonPositiveSize()
	{
		var source = Filled(4, 4, (_, _) => default);

		Assert.Throws<ArgumentOutOfRangeException>(() => source.Resize(0, 4));
		Assert.Throws<ArgumentOutOfRangeException>(() => source.Resize(4, -1));
	}

	#endregion

	#region Shrinking averages

	/// <summary>
	/// A one-pixel checkerboard reduced fourfold: every target pixel covers four black and four
	/// white source pixels, so each must be mid-grey. Sampling four corners instead returns whichever
	/// colour happened to be at them, and the pattern survives as a shimmer.
	/// </summary>
	[Fact]
	public void Shrinking_AveragesTheAreaCovered_RatherThanSamplingIt()
	{
		var checker = Filled(64, 64, (x, y) => ((x + y) % 2 == 0)
			? new ImagePixel(0, 0, 0)
			: new ImagePixel(255, 255, 255));

		var small = checker.Resize(16, 16);

		for (int y = 0; y < small.Height; y++)
		{
			for (int x = 0; x < small.Width; x++)
			{
				var pixel = small.GetPixel(x, y);
				Assert.InRange(pixel.R, 120, 135);
				Assert.InRange(pixel.G, 120, 135);
				Assert.InRange(pixel.B, 120, 135);
			}
		}
	}

	/// <summary>
	/// A single bright row in an otherwise black image must still be visible after a large
	/// reduction. Sampling can miss it entirely; averaging cannot.
	/// </summary>
	[Fact]
	public void Shrinking_DoesNotDropDetailBetweenSamples()
	{
		var striped = Filled(100, 100, (_, y) => y == 50
			? new ImagePixel(255, 255, 255)
			: new ImagePixel(0, 0, 0));

		var small = striped.Resize(10, 10);

		bool anyLit = false;
		for (int y = 0; y < small.Height && !anyLit; y++)
			for (int x = 0; x < small.Width && !anyLit; x++)
				anyLit = small.GetPixel(x, y).R > 0;

		Assert.True(anyLit, "the bright row vanished, which is what sampling does and averaging does not");
	}

	/// <summary>A flat image stays its own colour: averaging must not shift it.</summary>
	[Fact]
	public void Shrinking_AFlatImage_KeepsItsColour()
	{
		var flat = Filled(90, 90, (_, _) => new ImagePixel(70, 140, 210));

		var small = flat.Resize(9, 9);

		var pixel = small.GetPixel(4, 4);
		Assert.Equal(70, pixel.R);
		Assert.Equal(140, pixel.G);
		Assert.Equal(210, pixel.B);
	}

	#endregion

	#region Enlarging is unchanged

	/// <summary>Enlarging still interpolates: there is nothing to average, and smoothing is wanted.</summary>
	[Fact]
	public void Enlarging_StillInterpolates()
	{
		var source = Filled(2, 2, (x, y) => (x == 0 && y == 0)
			? new ImagePixel(0, 0, 0)
			: new ImagePixel(255, 255, 255));

		var big = source.Resize(8, 8);

		Assert.Equal(8, big.Width);
		Assert.Equal(8, big.Height);

		// A value strictly between the two source colours proves interpolation rather than
		// nearest-neighbour duplication.
		bool anyBlend = false;
		for (int y = 0; y < big.Height && !anyBlend; y++)
			for (int x = 0; x < big.Width && !anyBlend; x++)
			{
				byte r = big.GetPixel(x, y).R;
				anyBlend = r > 0 && r < 255;
			}

		Assert.True(anyBlend, "expected interpolated values between the source colours");
	}

	[Fact]
	public void SameSize_IsAFaithfulCopy()
	{
		var source = Filled(8, 8, (x, y) => new ImagePixel((byte)(x * 8), (byte)(y * 8), 0));

		var copy = source.Resize(8, 8);

		for (int y = 0; y < 8; y++)
			for (int x = 0; x < 8; x++)
				Assert.Equal(source.GetPixel(x, y), copy.GetPixel(x, y));
	}

	/// <summary>Shrinking one axis while growing the other still gives both sizes asked for.</summary>
	[Fact]
	public void MixedDirections_GiveBothSizesAsked()
	{
		var source = Filled(100, 10, (_, _) => new ImagePixel(50, 60, 70));

		var resized = source.Resize(25, 40);

		Assert.Equal(25, resized.Width);
		Assert.Equal(40, resized.Height);
	}

	#endregion
}
