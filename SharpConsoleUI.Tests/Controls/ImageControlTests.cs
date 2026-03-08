using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ImageControlTests
{
	#region Basic MeasureDOM / PaintDOM

	[Fact]
	public void MeasureDOM_NullSource_ReturnsMinimal()
	{
		var control = new ImageControl();
		var constraints = LayoutConstraints.Loose(80, 25);

		var size = control.MeasureDOM(constraints);

		Assert.Equal(0, size.Width);
		Assert.Equal(0, size.Height);
	}

	[Fact]
	public void MeasureDOM_WithSource_DefaultFit_ReturnsNaturalSize()
	{
		// Default ScaleMode is Fit, which does not expand beyond natural size
		var buffer = new PixelBuffer(10, 4);
		var control = new ImageControl { Source = buffer };
		var constraints = LayoutConstraints.Loose(80, 25);

		var size = control.MeasureDOM(constraints);

		// 10 wide, 4 pixels tall / 2 = 2 cell rows
		Assert.Equal(10, size.Width);
		Assert.Equal(2, size.Height);
	}

	[Fact]
	public void PaintDOM_NullSource_DoesNotCrash()
	{
		var control = new ImageControl();
		var buffer = new CharacterBuffer(80, 25);
		var bounds = new LayoutRect(0, 0, 40, 10);

		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void PaintDOM_WritesHalfBlockCharacters()
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
		Assert.Equal(ImagingDefaults.HalfBlockChar, cell.Character);
	}

	#endregion

	#region ScaleMode without alignment (default Left/Top)

	[Fact]
	public void ScaleMode_Fit_ScalesDownToFitConstraints()
	{
		// 20x10 pixels => natural 20 cols x 5 rows
		// Constraints 10x10 => Fit scales down to fit
		var pixels = new PixelBuffer(20, 10);
		var control = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.Fit };
		var constraints = LayoutConstraints.Loose(10, 10);

		var size = control.MeasureDOM(constraints);

		Assert.True(size.Width <= 10);
		Assert.True(size.Height <= 10);
		Assert.True(size.Width > 0);
		Assert.True(size.Height > 0);
	}

	[Fact]
	public void ScaleMode_Fit_DoesNotScaleUpBeyondNaturalSize()
	{
		// 6x4 pixels => natural 6 cols x 2 rows
		// Constraints 80x25 => Fit does NOT expand beyond natural size
		var pixels = new PixelBuffer(6, 4);
		var control = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.Fit };
		var constraints = LayoutConstraints.Loose(80, 25);

		var size = control.MeasureDOM(constraints);

		Assert.Equal(6, size.Width);
		Assert.Equal(2, size.Height);
	}

	[Fact]
	public void ScaleMode_Fill_WithoutAlignment_ExpandsToConstraints()
	{
		// Fill inherently wants to expand to fill available space.
		// 10x8 pixels => natural 10 cols x 4 rows
		// Constraints 80x25 => Fill expands and crops to cover the area
		var pixels = new PixelBuffer(10, 8);
		var control = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.Fill };
		var constraints = LayoutConstraints.Loose(80, 25);

		var size = control.MeasureDOM(constraints);

		// Fill uses constraint space (crops to cover)
		Assert.Equal(80, size.Width);
		Assert.Equal(25, size.Height);
	}

	[Fact]
	public void ScaleMode_Stretch_WithoutAlignment_ExpandsToConstraints()
	{
		// Stretch inherently wants to expand to fill available space.
		// 5x4 pixels => natural 5 cols x 2 rows
		// Constraints 20x15 => Stretch fills exactly
		var pixels = new PixelBuffer(5, 4);
		var control = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.Stretch };
		var constraints = LayoutConstraints.Loose(20, 15);

		var size = control.MeasureDOM(constraints);

		Assert.Equal(20, size.Width);
		Assert.Equal(15, size.Height);
	}

	[Fact]
	public void ScaleMode_None_WithoutAlignment_UsesNaturalSize()
	{
		// None never scales — always natural size (clipped to constraints)
		// 10x6 pixels => natural 10 cols x 3 rows
		var pixels = new PixelBuffer(10, 6);
		var control = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.None };
		var constraints = LayoutConstraints.Loose(80, 25);

		var size = control.MeasureDOM(constraints);

		Assert.Equal(10, size.Width);
		Assert.Equal(3, size.Height);
	}

	[Fact]
	public void ScaleModes_ProduceDifferentResults_WithoutAlignment()
	{
		// Verify that Fit, Fill, Stretch, None give different sizes
		var pixels = new PixelBuffer(10, 8); // natural 10x4
		var constraints = LayoutConstraints.Loose(40, 20);

		var fit = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.Fit };
		var fill = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.Fill };
		var stretch = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.Stretch };
		var none = new ImageControl { Source = pixels, ScaleMode = ImageScaleMode.None };

		var fitSize = fit.MeasureDOM(constraints);
		var fillSize = fill.MeasureDOM(constraints);
		var stretchSize = stretch.MeasureDOM(constraints);
		var noneSize = none.MeasureDOM(constraints);

		// Fit/None use natural size (10x4)
		Assert.Equal(10, fitSize.Width);
		Assert.Equal(4, fitSize.Height);
		Assert.Equal(10, noneSize.Width);
		Assert.Equal(4, noneSize.Height);

		// Fill/Stretch expand to constraints (40x20)
		Assert.Equal(40, fillSize.Width);
		Assert.Equal(20, fillSize.Height);
		Assert.Equal(40, stretchSize.Width);
		Assert.Equal(20, stretchSize.Height);
	}

	#endregion

	#region ScaleMode with alignment (Stretch/Fill)

	[Fact]
	public void ScaleMode_Stretch_WithAlignment_FillsAvailableSpace()
	{
		var pixels = new PixelBuffer(5, 4);
		var control = new ImageControl
		{
			Source = pixels,
			ScaleMode = ImageScaleMode.Stretch,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill
		};
		var constraints = LayoutConstraints.Loose(20, 15);

		var size = control.MeasureDOM(constraints);

		Assert.Equal(20, size.Width);
		Assert.Equal(15, size.Height);
	}

	[Fact]
	public void ScaleMode_Fill_WithAlignment_FillsAvailableSpace()
	{
		// 10x10 pixels => natural 10 cols x 5 rows
		// With alignment, available = 40x20
		var pixels = new PixelBuffer(10, 10);
		var control = new ImageControl
		{
			Source = pixels,
			ScaleMode = ImageScaleMode.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill
		};
		var constraints = LayoutConstraints.Loose(40, 20);

		var size = control.MeasureDOM(constraints);

		Assert.Equal(40, size.Width);
		Assert.Equal(20, size.Height);
	}

	[Fact]
	public void ScaleMode_Fit_WithAlignment_FitsWithinExpandedSpace()
	{
		// 20x10 pixels => natural 20 cols x 5 rows
		// With alignment, available = 40x20
		// Fit: scale = min(40/20, 20/5) = min(2, 4) = 2
		// cols = 40, rows = 10
		var pixels = new PixelBuffer(20, 10);
		var control = new ImageControl
		{
			Source = pixels,
			ScaleMode = ImageScaleMode.Fit,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill
		};
		var constraints = LayoutConstraints.Loose(40, 20);

		var size = control.MeasureDOM(constraints);

		// Fit preserves aspect ratio, so one dimension is smaller
		Assert.True(size.Width <= 40);
		Assert.True(size.Height <= 20);
		Assert.True(size.Width > 0);
		Assert.True(size.Height > 0);
	}

	[Fact]
	public void HorizontalStretchOnly_Fit_ExpandsWidthOnly()
	{
		// 10x8 pixels => natural 10 cols x 4 rows
		// With Fit mode + horizontal stretch, height stays natural
		var pixels = new PixelBuffer(10, 8);
		var control = new ImageControl
		{
			Source = pixels,
			ScaleMode = ImageScaleMode.Fit,
			HorizontalAlignment = HorizontalAlignment.Stretch
			// VerticalAlignment defaults to Top
		};
		var constraints = LayoutConstraints.Loose(40, 20);

		var size = control.MeasureDOM(constraints);

		// Fit with expanded width but natural height
		// scale = min(40/10, 4/4) = min(4, 1) = 1
		// Result: 10x4 (can't scale up vertically)
		Assert.True(size.Width <= 40);
		Assert.Equal(4, size.Height);
	}

	[Fact]
	public void VerticalFillOnly_Fit_ExpandsHeightOnly()
	{
		// 10x8 pixels => natural 10 cols x 4 rows
		// With Fit mode + vertical fill, width stays natural
		var pixels = new PixelBuffer(10, 8);
		var control = new ImageControl
		{
			Source = pixels,
			ScaleMode = ImageScaleMode.Fit,
			VerticalAlignment = VerticalAlignment.Fill
			// HorizontalAlignment defaults to Left
		};
		var constraints = LayoutConstraints.Loose(40, 20);

		var size = control.MeasureDOM(constraints);

		// Fit with natural width but expanded height
		// scale = min(10/10, 20/4) = min(1, 5) = 1
		// Result: 10x4 (can't scale up horizontally)
		Assert.Equal(10, size.Width);
		Assert.True(size.Height <= 20);
	}

	#endregion

	#region Unbounded constraints guard

	[Fact]
	public void UnboundedConstraints_Fit_FallsBackToNaturalSize()
	{
		// Scrollable layout passes int.MaxValue — Fit should use natural size
		var pixels = new PixelBuffer(10, 8);
		var control = new ImageControl
		{
			Source = pixels,
			ScaleMode = ImageScaleMode.Fit
		};
		var constraints = LayoutConstraints.Loose(int.MaxValue, int.MaxValue);

		var size = control.MeasureDOM(constraints);

		Assert.Equal(10, size.Width);
		Assert.Equal(4, size.Height);
	}

	[Fact]
	public void UnboundedConstraints_Stretch_CapsToMaxImageDimension()
	{
		// Stretch with unbounded constraints caps to MaxImageDimension
		var pixels = new PixelBuffer(10, 8);
		var control = new ImageControl
		{
			Source = pixels,
			ScaleMode = ImageScaleMode.Stretch
		};
		var constraints = LayoutConstraints.Loose(int.MaxValue, int.MaxValue);

		var size = control.MeasureDOM(constraints);

		Assert.True(size.Width <= ImagingDefaults.MaxImageDimension);
		Assert.True(size.Height <= ImagingDefaults.MaxImageDimension);
	}

	#endregion

	#region ContentWidth / LogicalContentSize

	[Fact]
	public void GetLogicalContentSize_NullSource_ReturnsMargins()
	{
		var control = new ImageControl();
		control.Margin = new Margin(2, 3, 4, 5);

		var size = control.GetLogicalContentSize();

		Assert.Equal(6, size.Width);  // 2 + 4
		Assert.Equal(8, size.Height); // 3 + 5
	}

	[Fact]
	public void ContentWidth_NullSource_ReturnsZero()
	{
		var control = new ImageControl();
		Assert.Equal(0, control.ContentWidth);
	}

	[Fact]
	public void ContentWidth_WithSource_IncludesMargins()
	{
		var pixels = new PixelBuffer(10, 4);
		var control = new ImageControl { Source = pixels };
		control.Margin = new Margin(2, 0, 3, 0);

		Assert.Equal(15, control.ContentWidth); // 10 + 2 + 3
	}

	#endregion
}
