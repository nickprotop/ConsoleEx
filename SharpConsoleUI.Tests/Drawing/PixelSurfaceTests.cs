// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Drawing;

/// <summary>
/// <see cref="PixelSurface"/> draws in pixels at twice a terminal's vertical
/// resolution and composites into a canvas as half-block cells.
/// </summary>
public class PixelSurfaceTests
{
	private const char UpperHalf = '▀';

	private static (CharacterBuffer buffer, CanvasGraphics g) Canvas(int w, int h)
	{
		var buffer = new CharacterBuffer(w, h, Color.Black);
		return (buffer, new CanvasGraphics(buffer, 0, 0, w, h, new LayoutRect(0, 0, w, h)));
	}

	private static PixelSurface Sized(int columns, int rows)
	{
		var s = new PixelSurface();
		s.EnsureSize(columns, rows);
		return s;
	}

	#region Geometry

	[Fact]
	public void TheSurfaceIsTwiceAsTallAsTheCanvas()
	{
		// The whole point: two pixels per cell row.
		var s = Sized(20, 8);

		Assert.Equal(20, s.Width);
		Assert.Equal(16, s.Height);
		Assert.Equal(8, s.Rows);
	}

	[Fact]
	public void EnsureSize_IsCheapWhenNothingChanged()
	{
		var s = Sized(10, 4);
		s.Set(3, 3, Color.Red);

		s.EnsureSize(10, 4);   // same size: must not reallocate and lose the pixel

		Assert.Equal(Color.Red, s.Get(3, 3));
	}

	[Fact]
	public void WritesOutsideTheSurfaceAreIgnored()
	{
		// Out of bounds is a no-op, not a throw and not a wrap onto another row -
		// a renderer draws past the edge constantly and should not have to clip.
		var s = Sized(5, 2);
		s.Fill(Color.Black);

		s.Set(-1, 0, Color.Red);
		s.Set(99, 0, Color.Red);
		s.Set(0, 99, Color.Red);

		Assert.False(s.InBounds(-1, 0));
		Assert.False(s.InBounds(99, 0));
		Assert.False(s.InBounds(0, 99));

		// Nothing leaked onto a real pixel, including the row a wrap would hit.
		for (int y = 0; y < s.Height; y++)
			for (int x = 0; x < s.Width; x++)
				Assert.Equal(Color.Black, s.Get(x, y));
	}

	#endregion

	#region Compositing

	[Fact]
	public void TwoDifferentPixels_BecomeAHalfBlockCell()
	{
		var s = Sized(4, 2);
		var (buffer, g) = Canvas(4, 2);

		s.Set(0, 0, Color.Red);     // top half of cell (0,0)
		s.Set(0, 1, Color.Blue);    // bottom half
		s.Present(g, Color.Black);

		var cell = buffer.GetCell(0, 0);
		Assert.Equal(new Rune(UpperHalf), cell.Character);
		Assert.Equal(Color.Red, cell.Foreground);    // top
		Assert.Equal(Color.Blue, cell.Background);   // bottom
	}

	[Fact]
	public void TwoMatchingPixels_BecomeASpace()
	{
		// A space costs the same as a half block and avoids a glyph the font might
		// render fractionally differently.
		var s = Sized(4, 2);
		var (buffer, g) = Canvas(4, 2);

		s.Set(1, 0, Color.Green);
		s.Set(1, 1, Color.Green);
		s.Present(g, Color.Black);

		var cell = buffer.GetCell(1, 0);
		Assert.Equal(new Rune(' '), cell.Character);
		Assert.Equal(Color.Green, cell.Background);
	}

	[Fact]
	public void TheOffsetShiftsWhereCellsRead_AndTheEdgeShowsTheVoid()
	{
		// A screen shake is a sampling offset, not a transform: pixels shifted off
		// the surface come back as voidColor rather than wrapping or stretching.
		var s = Sized(4, 2);
		var (buffer, g) = Canvas(4, 2);
		s.Fill(Color.Red);

		s.Present(g, 1, 0, Color.Blue);

		Assert.Equal(Color.Blue, buffer.GetCell(0, 0).Background);   // shifted in from nowhere
		Assert.Equal(Color.Red, buffer.GetCell(1, 0).Background);    // real content
	}

	#endregion

	#region Text layer, the part rule 12 cares about

	[Fact]
	public void TextFloatsOverThePixelsBeneathIt()
	{
		// No explicit background: the glyph takes the average of its two pixels, so
		// it sits in the scene instead of on a hole cut out of it.
		var s = Sized(6, 2);
		var (buffer, g) = Canvas(6, 2);

		s.Set(0, 0, new Color(0, 0, 0));
		s.Set(0, 1, new Color(100, 100, 100));
		s.Text(0, 0, "X", Color.White);
		s.Present(g, Color.Black);

		var cell = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('X'), cell.Character);
		Assert.Equal(new Color(50, 50, 50), cell.Background);   // the average
	}

	[Fact]
	public void TextWithABackgroundUsesIt()
	{
		var s = Sized(6, 2);
		var (buffer, g) = Canvas(6, 2);
		s.Fill(Color.Red);

		s.Text(0, 0, "X", Color.White, Color.Blue);
		s.Present(g, Color.Black);

		Assert.Equal(Color.Blue, buffer.GetCell(0, 0).Background);
	}

	[Fact]
	public void AWideGlyphTakesTwoColumns()
	{
		// The rule-12 case. Writing per char would put the glyph in one column and
		// shift everything after it.
		var s = Sized(10, 2);
		var (buffer, g) = Canvas(10, 2);

		int width = s.Text(0, 0, "漢a", Color.White);
		s.Present(g, Color.Black);

		Assert.Equal(3, width);                                       // 2 + 1 columns
		Assert.Equal(new Rune('漢'), buffer.GetCell(0, 0).Character);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
		Assert.Equal(new Rune('a'), buffer.GetCell(2, 0).Character);
	}

	[Fact]
	public void ACombiningMarkStaysWithItsLetter()
	{
		var s = Sized(10, 2);
		var (buffer, g) = Canvas(10, 2);

		s.Text(0, 0, "é", Color.White);
		s.Present(g, Color.Black);

		var cell = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('e'), cell.Character);
		Assert.Equal("́", cell.Combiners);
	}

	[Fact]
	public void MarkupIsHonoured()
	{
		var s = Sized(10, 2);
		var (buffer, g) = Canvas(10, 2);

		s.Text(0, 0, "[red]R[/]", Color.White);
		s.Present(g, Color.Black);

		Assert.Equal(new Rune('R'), buffer.GetCell(0, 0).Character);
		Assert.Equal(Color.Red, buffer.GetCell(0, 0).Foreground);
	}

	[Fact]
	public void CentringMeasuresDisplayColumns_NotTheMarkupString()
	{
		// "[bold]ab[/]" is 11 characters and 2 columns. Centring on the string
		// length would push the text off the left edge.
		var s = Sized(10, 2);
		var (buffer, g) = Canvas(10, 2);

		s.TextCentered(0, "[bold]ab[/]", Color.White);
		s.Present(g, Color.Black);

		Assert.Equal(new Rune('a'), buffer.GetCell(4, 0).Character);
		Assert.Equal(new Rune('b'), buffer.GetCell(5, 0).Character);
	}

	[Fact]
	public void ClearTextLeavesThePixelsAlone()
	{
		var s = Sized(6, 2);
		var (buffer, g) = Canvas(6, 2);
		s.Fill(Color.Red);
		s.Text(0, 0, "X", Color.White);

		s.ClearText();
		s.Present(g, Color.Black);

		Assert.NotEqual(new Rune('X'), buffer.GetCell(0, 0).Character);
		Assert.Equal(Color.Red, buffer.GetCell(0, 0).Background);
	}

	#endregion

	#region Drawing

	[Fact]
	public void BlendMixesTowardsTheNewColour()
	{
		var s = Sized(4, 2);
		s.Set(0, 0, Color.Black);

		s.Blend(0, 0, Color.White, 0.5);

		var c = s.Get(0, 0);
		Assert.InRange(c.R, 126, 129);
	}

	[Fact]
	public void AddBrightensAndClampsAtWhite()
	{
		var s = Sized(4, 2);
		s.Set(0, 0, new Color(200, 200, 200));

		s.Add(0, 0, Color.White, 1.0);

		Assert.Equal(new Color(255, 255, 255), s.Get(0, 0));
	}

	[Fact]
	public void FillCircleCoversItsCentreAndMissesTheCorners()
	{
		var s = Sized(20, 10);
		s.Fill(Color.Black);

		s.FillCircle(10, 10, 5, Color.Red);

		Assert.Equal(Color.Red, s.Get(10, 10));          // centre
		Assert.Equal(Color.Black, s.Get(0, 0));          // well outside
	}

	[Fact]
	public void DrawLineConnectsBothEnds()
	{
		var s = Sized(20, 10);
		s.Fill(Color.Black);

		s.DrawLine(2, 2, 10, 6, Color.Red);

		Assert.Equal(Color.Red, s.Get(2, 2));
		Assert.Equal(Color.Red, s.Get(10, 6));
	}

	[Fact]
	public void DimDarkensEverything()
	{
		var s = Sized(4, 2);
		s.Fill(new Color(200, 200, 200));

		s.Dim(0.5);

		Assert.InRange(s.Get(0, 0).R, 99, 101);
	}

	[Fact]
	public void DesaturateGoesToLuminance()
	{
		var s = Sized(4, 2);
		s.Fill(new Color(255, 0, 0));

		s.Desaturate(1.0);

		var c = s.Get(0, 0);
		Assert.Equal(c.R, c.G);
		Assert.Equal(c.G, c.B);
	}

	#endregion

	#region Masks

	[Fact]
	public void AMaskLightsOnlyItsHashes()
	{
		var s = Sized(10, 4);
		s.Fill(Color.Black);
		var mask = new PixelMask(
			".#.",
			"###");

		s.Draw(mask, 2, 1, Color.Red);

		Assert.Equal(Color.Red, s.Get(3, 1));      // the lone top pixel
		Assert.Equal(Color.Black, s.Get(2, 1));    // clear beside it
		Assert.Equal(Color.Red, s.Get(2, 2));      // the full row below
		Assert.Equal(Color.Red, s.Get(4, 2));
	}

	[Fact]
	public void RaggedRowsArePaddedWithClearPixels()
	{
		// A shape can be written without padding every line to the same length.
		var mask = new PixelMask("#", "###");

		Assert.Equal(3, mask.Width);
		Assert.Equal(2, mask.Height);
		Assert.True(mask[0, 0]);
		Assert.False(mask[2, 0]);      // past the short row
		Assert.True(mask[2, 1]);
	}

	[Fact]
	public void ReadingOutsideAMaskIsClear_NotAThrow()
	{
		var mask = new PixelMask("#");

		Assert.False(mask[-1, 0]);
		Assert.False(mask[0, -1]);
		Assert.False(mask[99, 99]);
	}

	[Fact]
	public void AnEmptyMaskIsRejected()
	{
		Assert.Throws<ArgumentException>(() => new PixelMask());
	}

	[Fact]
	public void RowShadeDarkensTowardsTheBottom()
	{
		var s = Sized(10, 4);
		s.Fill(Color.Black);
		var mask = new PixelMask("#", "#", "#");

		s.Draw(mask, 0, 0, new Color(200, 200, 200), rowShade: 1.0);

		Assert.True(s.Get(0, 0).R > s.Get(0, 2).R, "the bottom row should be darker");
	}

	[Fact]
	public void GlowMaskLightsTheNeighbours()
	{
		var s = Sized(10, 4);
		s.Fill(Color.Black);
		var mask = new PixelMask("#");

		s.GlowMask(mask, 4, 2, Color.White, 0.5);

		Assert.True(s.Get(3, 2).R > 0);    // left
		Assert.True(s.Get(5, 2).R > 0);    // right
		Assert.True(s.Get(4, 1).R > 0);    // above
		Assert.True(s.Get(4, 3).R > 0);    // below
	}

	[Fact]
	public void CloneMaskIsIndependentOfTheMask()
	{
		// For shapes that erode in play - a destructible shield - while the mask
		// itself stays immutable and shareable.
		var mask = new PixelMask("##", "##");
		var copy = mask.CloneMask();

		copy[0, 0] = false;

		Assert.True(mask[0, 0]);
	}

	[Fact]
	public void DrawingAMaskOffTheEdgeIsClipped()
	{
		var s = Sized(4, 2);
		s.Fill(Color.Black);
		var mask = new PixelMask("###", "###");

		s.Draw(mask, 3, 3, Color.Red);   // mostly past the right and bottom

		Assert.Equal(Color.Red, s.Get(3, 3));
	}

	#endregion

	#region The canvas's own surface

	[Fact]
	public void ACanvasAllocatesNoSurfaceUntilAsked()
	{
		// Opt-in: a canvas that only draws text must not pay for a pixel grid.
		var canvas = new SharpConsoleUI.Controls.CanvasControl
		{
			AutoSize = false,
			CanvasWidth = 20,
			CanvasHeight = 10,
		};

		Assert.False(canvas.HasPixelSurface);
	}

	[Fact]
	public void AskingForPixelsCreatesAndSizesThem()
	{
		var canvas = new SharpConsoleUI.Controls.CanvasControl
		{
			AutoSize = false,
			CanvasWidth = 20,
			CanvasHeight = 10,
		};

		var px = canvas.Pixels;

		Assert.True(canvas.HasPixelSurface);
		Assert.Equal(20, px.Width);
		Assert.Equal(20, px.Height);   // 10 rows x 2
	}

	[Fact]
	public void TheSurfaceIsResizedWithTheCanvas()
	{
		// The step easiest to forget by hand, and the reason this property exists.
		var canvas = new SharpConsoleUI.Controls.CanvasControl
		{
			AutoSize = false,
			CanvasWidth = 20,
			CanvasHeight = 10,
		};
		_ = canvas.Pixels;

		canvas.CanvasWidth = 40;
		canvas.CanvasHeight = 6;

		Assert.Equal(40, canvas.Pixels.Width);
		Assert.Equal(12, canvas.Pixels.Height);
	}

	[Fact]
	public void TheSameSurfaceComesBackEachTime()
	{
		var canvas = new SharpConsoleUI.Controls.CanvasControl
		{
			AutoSize = false,
			CanvasWidth = 8,
			CanvasHeight = 4,
		};

		Assert.Same(canvas.Pixels, canvas.Pixels);
	}

	[Fact]
	public void PresentPixelsWritesIntoTheCanvas()
	{
		var canvas = new SharpConsoleUI.Controls.CanvasControl
		{
			AutoSize = false,
			CanvasWidth = 6,
			CanvasHeight = 3,
		};

		canvas.Pixels.Fill(Color.Red);
		canvas.PresentPixels(Color.Black);

		var g = canvas.BeginPaint();
		try
		{
			Assert.Equal(Color.Red, g.GetCell(0, 0).Background);
		}
		finally
		{
			canvas.EndPaint();
		}
	}

	[Fact]
	public void PresentPixelsDoesNothingWhenThereIsNoSurface()
	{
		// Must not allocate one just to present an empty grid over the canvas.
		var canvas = new SharpConsoleUI.Controls.CanvasControl
		{
			AutoSize = false,
			CanvasWidth = 6,
			CanvasHeight = 3,
		};

		canvas.PresentPixels(Color.Black);

		Assert.False(canvas.HasPixelSurface);
	}

	[Fact]
	public void AStandaloneSurfaceStillPresentsIntoAnyCanvas()
	{
		// The convenience must not become the only way: both games allocate their
		// own, and one presents with a shake offset.
		var canvas = new SharpConsoleUI.Controls.CanvasControl
		{
			AutoSize = false,
			CanvasWidth = 6,
			CanvasHeight = 3,
		};

		var mine = new PixelSurface();
		mine.EnsureSize(6, 3);
		mine.Fill(Color.Blue);

		var g = canvas.BeginPaint();
		try
		{
			mine.Present(g, Color.Black);
			Assert.Equal(Color.Blue, g.GetCell(0, 0).Background);
		}
		finally
		{
			canvas.EndPaint();
		}

		Assert.False(canvas.HasPixelSurface);   // the canvas never made its own
	}

	#endregion

	#region Colour helpers

	[Theory]
	[InlineData(0.0, 0)]
	[InlineData(1.0, 255)]
	[InlineData(-5.0, 0)]     // clamped
	[InlineData(9.0, 255)]    // clamped
	public void LerpClampsItsParameter(double t, byte expected)
	{
		Assert.Equal(expected, PixelSurface.Lerp(Color.Black, Color.White, t).R);
	}

	[Fact]
	public void ScaleClampsAtWhite()
	{
		Assert.Equal(255, PixelSurface.Scale(new Color(200, 200, 200), 4).R);
	}

	#endregion
}
