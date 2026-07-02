// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests;

public class RadioGeometryTests
{
	private enum Size { Small }

	private static RadioControl<Size> Radio(HorizontalAlignment h = HorizontalAlignment.Left, bool wrap = false, string label = "Small")
	{
		var g = new RadioGroup<Size>();
		return new RadioControl<Size>(g, Size.Small, label) { HorizontalAlignment = h, Wrap = wrap };
	}

	// Find the column of the first non-space glyph on row `y`.
	private static int FirstGlyphCol(CharacterBuffer buf, int y, int width)
	{
		for (int x = 0; x < width; x++)
		{
			var c = buf.GetCell(x, y);
			if (c.Character.Value != ' ' && c.Character.Value != 0) return x;
		}
		return -1;
	}

	// Content string is " (mark) label " with default marker "○"/"●", so for label "Small"
	// (width 20, margin 0): first glyph '(' at col 1, marker at col 2, label 'S' at col 5,
	// content block = marker-prefix (5) + "Small" (5) = 10 columns wide.
	[Theory]
	[InlineData(HorizontalAlignment.Left, 1)]     // content starts col 0; first glyph is '(' at col 1
	[InlineData(HorizontalAlignment.Center, 6)]   // alignOffset 5 → '(' at 6
	[InlineData(HorizontalAlignment.Right, 11)]   // alignOffset 10 → '(' at 11
	public void HorizontalAlignment_PlacesContentBlock(HorizontalAlignment h, int expectedFirstGlyphCol)
	{
		var r = Radio(h);
		var buf = new CharacterBuffer(20, 3);
		var bounds = new LayoutRect(0, 0, 20, 1);
		r.PaintDOM(buf, bounds, bounds, Color.White, Color.Black);
		Assert.Equal(expectedFirstGlyphCol, FirstGlyphCol(buf, 0, 20));
	}

	[Fact]
	public void LeftAligned_MarkerAndLabelColumns()
	{
		var r = Radio(HorizontalAlignment.Left);
		var buf = new CharacterBuffer(20, 3);
		var bounds = new LayoutRect(0, 0, 20, 1);
		r.PaintDOM(buf, bounds, bounds, Color.White, Color.Black);

		// prefix " (○) " → '(' col1, marker col2, ')' col3, space col4, label 'S' col5
		Assert.Equal('(', (char)buf.GetCell(1, 0).Character.Value);
		Assert.Equal('○', (char)buf.GetCell(2, 0).Character.Value);
		Assert.Equal(')', (char)buf.GetCell(3, 0).Character.Value);
		Assert.Equal('S', (char)buf.GetCell(5, 0).Character.Value);
	}

	[Fact]
	public void Wrap_LongLabel_MeasuresMultipleLines()
	{
		var g = new RadioGroup<Size>();
		var r = new RadioControl<Size>(g, Size.Small, "Use the system default color") { Wrap = true };
		// narrow width forces wrap: prefix 5 + label wraps into remaining columns
		var size = r.MeasureDOM(new LayoutConstraints(0, 14, 0, 10));
		Assert.True(size.Height > 1);
	}

	[Fact]
	public void WrapFalse_SingleLine()
	{
		var g = new RadioGroup<Size>();
		var r = new RadioControl<Size>(g, Size.Small, "Use the system default color") { Wrap = false };
		var size = r.MeasureDOM(new LayoutConstraints(0, 14, 0, 10));
		Assert.Equal(1, size.Height);
	}

	[Fact]
	public void Wrap_ContinuationLine_HangingIndentEqualsPrefixWidth()
	{
		var g = new RadioGroup<Size>();
		// Two words that will wrap onto separate lines within the label column.
		var r = new RadioControl<Size>(g, Size.Small, "Hello World") { Wrap = true };
		var buf = new CharacterBuffer(11, 4);
		// width 11: margin 0, prefix 5 → label column width = 6 → "Hello" fits, "World" wraps to row 1
		var bounds = new LayoutRect(0, 0, 11, 3);
		r.PaintDOM(buf, bounds, bounds, Color.White, Color.Black);

		int prefixWidth = MarkupParser.Parse(" (○) ", Color.White, Color.Black).Count;

		int row0First = FirstGlyphCol(buf, 0, 11); // '(' of the marker prefix at col 1
		int row1First = FirstGlyphCol(buf, 1, 11); // continuation label, indented under the label column

		Assert.Equal(1, row0First);
		// Continuation line hangs at the label column: startX(0) + prefixWidth(5).
		Assert.Equal(prefixWidth, row1First);
		Assert.Equal('W', (char)buf.GetCell(prefixWidth, 1).Character.Value);
	}

	[Fact]
	public void WideMarker_ShiftsLabelByOneColumn()
	{
		// A 2-column marker glyph (🔘) pushes the label right by one extra column
		// relative to the 1-column default "○".
		var g = new RadioGroup<Size>();
		var r = new RadioControl<Size>(g, Size.Small, "Small") { HorizontalAlignment = HorizontalAlignment.Left };
		r.UnselectedCharacter = "🔘";

		var buf = new CharacterBuffer(20, 3);
		var bounds = new LayoutRect(0, 0, 20, 1);
		r.PaintDOM(buf, bounds, bounds, Color.White, Color.Black);

		// prefix " (🔘) " occupies: space(0) '('(1) marker(2)+continuation(3) ')'(4) space(5) → label at col 6.
		// Compared to the narrow "○" case (label at col 5) the label is shifted +1.
		int prefixWidth = MarkupParser.Parse(" (🔘) ", Color.White, Color.Black).Count;
		Assert.Equal(6, prefixWidth); // 1 wider than the narrow prefix (5)
		Assert.Equal('S', (char)buf.GetCell(prefixWidth, 0).Character.Value);
	}

	[Fact]
	public void MixedWidthMarkers_MeasureSameWidth_CheckedOrNot()
	{
		// A 2-wide selected marker (🔘) and a 1-wide unselected marker (○) must produce the SAME
		// measured width regardless of the current Checked state — the layout slot is sized from the
		// MAX marker width so measure and paint never disagree when the glyphs differ in display width.
		var g = new RadioGroup<Size>();
		var r = new RadioControl<Size>(g, Size.Small, "Small")
		{
			Wrap = false,
			SelectedCharacter = "🔘",   // 2 display columns
			UnselectedCharacter = "○"   // 1 display column
		};

		var constraints = new LayoutConstraints(0, 40, 0, 10);

		// Unselected (nothing selected) → uses the narrow ○ glyph at paint time.
		Assert.False(r.Checked);
		var unselectedSize = r.MeasureDOM(constraints);

		// Select it → Checked becomes true, so paint would use the wide 🔘 glyph.
		g.SelectedValue = Size.Small;
		Assert.True(r.Checked);
		var selectedSize = r.MeasureDOM(constraints);

		Assert.Equal(unselectedSize.Width, selectedSize.Width);
	}
}
