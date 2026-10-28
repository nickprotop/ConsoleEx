// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Surfaces;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Surfaces;

public class CellSurfaceTests
{
	private static readonly Color Fg = Color.White;
	private static readonly Color Bg = Color.Black;

	/// <summary>A buffer with known content, for the typed-source adapters.</summary>
	private static CharacterBuffer MakeBuffer(string text, Color fg, Color bg, int width = 10, int height = 2)
	{
		var buffer = new CharacterBuffer(width, height, bg);
		var cells = MarkupParser.Parse(text, fg, bg);
		for (int i = 0; i < cells.Count && i < width; i++)
			buffer.SetCell(i, 0, cells[i]);
		return buffer;
	}

	#region Adapters

	[Fact]
	public void BufferSnapshot_And_LiveBuffer_Agree()
	{
		var buffer = MakeBuffer("hi", Fg, Bg);
		var live = new TestSurface(buffer);
		var snap = buffer.CreateSnapshot().AsSurface();

		Assert.Equal(live.Width, snap.Width);
		Assert.Equal(live.Height, snap.Height);

		for (int y = 0; y < live.Height; y++)
			for (int x = 0; x < live.Width; x++)
				Assert.Equal(live[x, y], snap[x, y]);
	}

	[Fact]
	public void TypedSource_NeverReportsDefaultedBackground()
	{
		var surface = MakeBuffer("abc", Fg, Bg).CreateSnapshot().AsSurface();

		for (int y = 0; y < surface.Height; y++)
			for (int x = 0; x < surface.Width; x++)
				Assert.False(surface[x, y].BackgroundWasDefaulted);
	}

	[Fact]
	public void AnsiSource_ReportsDefaultedBackground_WhenEscapeSays49()
	{
		// "49" is the terminal's own default background - a colour this process cannot know.
		var snapshot = MakeConsoleSnapshot(new ConsoleCell(new Rune('x'), "\u001b[39;49m"));
		var surface = snapshot.AsSurface(new ModernGrayTheme());

		Assert.True(surface[0, 0].BackgroundWasDefaulted);
	}

	[Fact]
	public void AnsiSource_ParsesTrueColour_AndDoesNotReportDefaulted()
	{
		var snapshot = MakeConsoleSnapshot(
			new ConsoleCell(new Rune('x'), "\u001b[38;2;10;20;30;48;2;40;50;60m"));
		var cell = snapshot.AsSurface(new ModernGrayTheme())[0, 0];

		Assert.False(cell.BackgroundWasDefaulted);
		Assert.Equal(new Color(10, 20, 30), cell.Foreground);
		Assert.Equal(new Color(40, 50, 60), cell.Background);
	}

	[Fact]
	public void AnsiSource_ParsesDecorations_WithoutMisreadingColourComponents()
	{
		// The green component of the foreground is 1, which is also the code for bold. A
		// parser that does not skip the truecolour run would report a spurious Bold here.
		var plain = MakeConsoleSnapshot(
			new ConsoleCell(new Rune('x'), "\u001b[38;2;0;1;0;48;2;0;4;0m"));
		Assert.Equal(TextDecoration.None, plain.AsSurface(new ModernGrayTheme())[0, 0].Decorations);

		var bold = MakeConsoleSnapshot(
			new ConsoleCell(new Rune('x'), "\u001b[38;2;0;0;0;48;2;0;0;0;1;4m"));
		var dec = bold.AsSurface(new ModernGrayTheme())[0, 0].Decorations;
		Assert.True(dec.HasFlag(TextDecoration.Bold));
		Assert.True(dec.HasFlag(TextDecoration.Underline));
	}

	[Fact]
	public void AnsiSource_CarriesGraphemeState()
	{
		// The fidelity bug: ConsoleCell used to hold only the rune and the escape.
		var snapshot = MakeConsoleSnapshot(
			new ConsoleCell(new Rune('e'), "\u001b[0m", isWideContinuation: false, combiners: "́"));
		var cell = snapshot.AsSurface(new ModernGrayTheme())[0, 0];

		Assert.Equal("́", cell.Combiners);
		Assert.False(cell.IsWideContinuation);
	}

	#endregion

	#region Crop

	[Fact]
	public void Crop_NarrowsTheView_AndOffsetsReads()
	{
		var surface = MakeBuffer("abcdef", Fg, Bg, width: 6, height: 1);
		var cropped = new TestSurface(surface).Crop(new LayoutRect(2, 0, 3, 1));

		Assert.Equal(3, cropped.Width);
		Assert.Equal(1, cropped.Height);
		Assert.Equal(new Rune('c'), cropped[0, 0].Character);
		Assert.Equal(new Rune('e'), cropped[2, 0].Character);
	}

	[Fact]
	public void Crop_ClampsAPartlyOutsideRegion()
	{
		var surface = new TestSurface(MakeBuffer("abcd", Fg, Bg, width: 4, height: 1));
		var cropped = surface.Crop(new LayoutRect(2, 0, 100, 1));

		Assert.Equal(2, cropped.Width);   // clamped to what exists
		Assert.Equal(new Rune('c'), cropped[0, 0].Character);
	}

	[Fact]
	public void Crop_ThrowsForAFullyOutsideRegion()
	{
		// Returning an empty surface would read as "that area is blank", a different answer.
		var surface = new TestSurface(MakeBuffer("ab", Fg, Bg, width: 4, height: 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => surface.Crop(new LayoutRect(50, 0, 4, 1)));
	}

	[Fact]
	public void Crop_ThrowsForANonPositiveExtent()
	{
		var surface = new TestSurface(MakeBuffer("ab", Fg, Bg, width: 4, height: 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => surface.Crop(new LayoutRect(0, 0, 0, 1)));
	}

	#endregion

	#region Text formats

	[Fact]
	public void Plain_EmitsCharacters_AndTrimsTrailingBlanks()
	{
		var surface = new TestSurface(MakeBuffer("hi", Fg, Bg, width: 10, height: 1));
		Assert.Equal("hi", surface.ToText(TextFormat.Plain));
	}

	[Fact]
	public void Plain_KeepsCombiningMarks()
	{
		var surface = new TestSurface(MakeBuffer("éx", Fg, Bg, width: 6, height: 1));
		Assert.Equal("éx", surface.ToText(TextFormat.Plain));
	}

	[Fact]
	public void Plain_DoesNotRepeatAWideGlyph()
	{
		// "中" occupies two columns; the second is a continuation and must not re-emit it.
		var surface = new TestSurface(MakeBuffer("中a", Fg, Bg, width: 6, height: 1));
		Assert.Equal("中a", surface.ToText(TextFormat.Plain));
	}

	[Fact]
	public void Ansi_CoalescesARunOfUnchangedStyle()
	{
		var surface = new TestSurface(MakeBuffer("aaaaaaaa", Fg, Bg, width: 8, height: 1));
		string ansi = surface.ToText(TextFormat.Ansi);

		// One opening escape for the whole run, plus the reset that precedes it and the one
		// that closes the line - never one escape per cell.
		int sgrCount = CountOccurrences(ansi, "38;2;");
		Assert.Equal(1, sgrCount);
	}

	[Fact]
	public void Ansi_EmitsDefaultBackgroundAs49_RatherThanTheSubstitute()
	{
		var snapshot = MakeConsoleSnapshot(new ConsoleCell(new Rune('x'), "\u001b[39;49m"));
		string ansi = snapshot.AsSurface(new ModernGrayTheme()).ToText(TextFormat.Ansi);

		Assert.Contains(";49", ansi, StringComparison.Ordinal);
		Assert.DoesNotContain("48;2;", ansi, StringComparison.Ordinal);
	}

	[Fact]
	public void Ansi_IncludesDecorations()
	{
		var surface = new TestSurface(MakeBuffer("[bold]x[/]", Fg, Bg, width: 4, height: 1));
		Assert.Contains(";1", surface.ToText(TextFormat.Ansi), StringComparison.Ordinal);
	}

	[Fact]
	public void Markup_RoundTripsThroughTheParser()
	{
		// The format's whole justification: output that parses back into equal cells.
		var buffer = MakeBuffer("[#FF0000 bold]red[/] plain", Fg, Bg, width: 12, height: 1);
		string markup = new TestSurface(buffer).ToText(TextFormat.Markup);

		var reparsed = MarkupParser.Parse(markup, Fg, Bg);
		for (int x = 0; x < 9; x++)
		{
			Assert.Equal(buffer.GetCell(x, 0).Character, reparsed[x].Character);
			Assert.Equal(buffer.GetCell(x, 0).Foreground, reparsed[x].Foreground);
			Assert.Equal(buffer.GetCell(x, 0).Decorations, reparsed[x].Decorations);
		}
	}

	[Fact]
	public void Markup_EscapesALiteralBracket()
	{
		var surface = new TestSurface(MakeBuffer("[[x", Fg, Bg, width: 4, height: 1));
		string markup = surface.ToText(TextFormat.Markup);

		// Re-parsing must yield the literal '[', not an unterminated tag.
		var reparsed = MarkupParser.Parse(markup, Fg, Bg);
		Assert.Equal(new Rune('['), reparsed[0].Character);
	}

	[Fact]
	public void Markup_KeepsCombiningMarks()
	{
		var surface = new TestSurface(MakeBuffer("éx", Fg, Bg, width: 6, height: 1));
		string markup = surface.ToText(TextFormat.Markup);
		Assert.True(markup.Contains("́", StringComparison.Ordinal));
	}

	[Fact]
	public void MultipleRows_AreNewlineSeparated()
	{
		var buffer = new CharacterBuffer(3, 2, Bg);
		buffer.SetCell(0, 0, new Cell('a', Fg, Bg));
		buffer.SetCell(0, 1, new Cell('b', Fg, Bg));

		Assert.Equal("a\nb", new TestSurface(buffer).ToText(TextFormat.Plain));
	}

	#endregion

	#region Images

	[Fact]
	public void ToImage_ProducesAPngWithTheExpectedGeometry()
	{
		var surface = new TestSurface(MakeBuffer("hi", Fg, Bg, width: 4, height: 2));
		byte[] png = surface.ToImage(new ExportOptions { CellHeight = 8, CellWidth = 4, Glyphs = false });

		Assert.True(png.Length > 0);
		Assert.Equal(0x89, png[0]);                       // PNG magic
		Assert.Equal((byte)'P', png[1]);
		Assert.Equal(4 * 4, ReadBigEndianInt(png, 16));   // width  = cols * cellWidth
		Assert.Equal(2 * 8, ReadBigEndianInt(png, 20));   // height = rows * cellHeight
	}

	[Fact]
	public void ToImage_DefaultsToPng()
	{
		var surface = new TestSurface(MakeBuffer("x", Fg, Bg, width: 2, height: 1));
		byte[] bytes = surface.ToImage(new ExportOptions { Glyphs = false });

		Assert.Equal(0x89, bytes[0]);
		Assert.Equal((byte)'P', bytes[1]);
	}

	[Fact]
	public void ToImage_IsDeterministic()
	{
		var surface = new TestSurface(MakeBuffer("golden", Fg, Bg, width: 8, height: 2));
		var options = new ExportOptions { CellHeight = 8, CellWidth = 4, Glyphs = false };

		Assert.Equal(surface.ToImage(options), surface.ToImage(options));
	}

	[Fact]
	public void ToImage_ThrowsForAFontThatCannotLoad()
	{
		var surface = new TestSurface(MakeBuffer("x", Fg, Bg, width: 2, height: 1));
		Assert.Throws<InvalidOperationException>(() =>
			surface.ToImage(new ExportOptions { FontPath = "/nonexistent/font.ttf" }));
	}

	[Fact]
	public void ToImage_Tga_HasTheExpectedGeometry()
	{
		var surface = new TestSurface(MakeBuffer("hi", Fg, Bg, width: 4, height: 2));
		byte[] tga = surface.ToImage(new ExportOptions
		{
			Format = ImageFormat.Tga,
			CellHeight = 8,
			CellWidth = 4,
			Glyphs = false,
		});

		// TGA header: width and height are little-endian 16-bit at offsets 12 and 14.
		Assert.True(tga.Length > 18);
		Assert.Equal(4 * 4, tga[12] | (tga[13] << 8));
		Assert.Equal(2 * 8, tga[14] | (tga[15] << 8));
	}

	[Fact]
	public void ToImage_Jpeg_IsAJpeg()
	{
		var surface = new TestSurface(MakeBuffer("hi", Fg, Bg, width: 4, height: 2));
		byte[] jpg = surface.ToImage(new ExportOptions
		{
			Format = ImageFormat.Jpeg,
			CellHeight = 8,
			CellWidth = 4,
			Glyphs = false,
		});

		Assert.True(jpg.Length > 0);
		Assert.Equal(0xFF, jpg[0]);   // SOI
		Assert.Equal(0xD8, jpg[1]);
	}

	[Fact]
	public void ToImage_EachFormatProducesItsOwnEncoding()
	{
		var surface = new TestSurface(MakeBuffer("compare", Fg, Bg, width: 10, height: 3));
		var baseOptions = new ExportOptions { CellHeight = 8, CellWidth = 4, Glyphs = false };

		byte[] png = surface.ToImage(baseOptions with { Format = ImageFormat.Png });
		byte[] tga = surface.ToImage(baseOptions with { Format = ImageFormat.Tga });
		byte[] jpg = surface.ToImage(baseOptions with { Format = ImageFormat.Jpeg });

		Assert.NotEqual(png, tga);
		Assert.NotEqual(png, jpg);
		Assert.NotEqual(tga, jpg);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(101)]
	[InlineData(-1)]
	public void ToImage_RejectsAQualityOutsideTheAllowedRange(int quality)
	{
		var surface = new TestSurface(MakeBuffer("x", Fg, Bg, width: 2, height: 1));

		// Rejected rather than clamped: a caller who asks for 0 has made a mistake, and a
		// silently-corrected image hides it.
		var ex = Assert.Throws<InvalidOperationException>(() => surface.ToImage(
			new ExportOptions { Format = ImageFormat.Jpeg, JpegQuality = quality, Glyphs = false }));

		Assert.Contains("quality", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void ToImage_QualityIsIgnoredByTheLosslessFormats()
	{
		var surface = new TestSurface(MakeBuffer("x", Fg, Bg, width: 2, height: 1));
		var options = new ExportOptions { CellHeight = 8, CellWidth = 4, Glyphs = false };

		// An out-of-range quality is still rejected, so use a valid one and check it has
		// no effect on a format that does not read it.
		Assert.Equal(
			surface.ToImage(options with { Format = ImageFormat.Png, JpegQuality = 10 }),
			surface.ToImage(options with { Format = ImageFormat.Png, JpegQuality = 95 }));
	}

	[Fact]
	public void ToImage_LowerJpegQualityProducesFewerBytes()
	{
		var surface = new TestSurface(MakeBuffer("quality matters here", Fg, Bg, width: 24, height: 6));
		var options = new ExportOptions
		{
			Format = ImageFormat.Jpeg,
			CellHeight = 16,
			CellWidth = 8,
			Glyphs = false,
		};

		Assert.True(
			surface.ToImage(options with { JpegQuality = 40 }).Length
			< surface.ToImage(options with { JpegQuality = 95 }).Length);
	}

	#endregion

	#region Helpers

	private static int CountOccurrences(string haystack, string needle)
	{
		int count = 0, i = 0;
		while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
		return count;
	}

	private static int ReadBigEndianInt(byte[] b, int offset) =>
		(b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];

	private static ConsoleBufferSnapshot MakeConsoleSnapshot(ConsoleCell cell)
	{
		var cells = new ConsoleCell[1, 1];
		cells[0, 0] = cell;
		return new ConsoleBufferSnapshot
		{
			FrontBuffer = cells,
			BackBuffer = cells,
			Width = 1,
			Height = 1,
		};
	}

	/// <summary>Wraps a live buffer, mirroring what Window.Surface() returns.</summary>
	private sealed class TestSurface(CharacterBuffer buffer) : ICellSurface
	{
		public int Width => buffer.Width;

		public int Height => buffer.Height;

		public SurfaceCell this[int x, int y]
		{
			get
			{
				var c = buffer.GetCell(x, y);
				return new SurfaceCell(c.Character, c.Foreground, c.Background,
					c.Decorations, c.IsWideContinuation, c.Combiners, false);
			}
		}
	}

	#endregion
}
