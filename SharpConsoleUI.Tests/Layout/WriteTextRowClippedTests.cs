// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// <see cref="CharacterBuffer.WriteTextRowClipped"/> against the path it replaces.
/// </summary>
/// <remarks>
/// <para>
/// The fast row writer computes its own start and end from the clip rectangle, with -1 and +1
/// adjustments that <see cref="CharacterBuffer.WriteCellsClipped"/> does not need because it walks
/// every cell and tests each one. The two must paint identically: the only thing that may differ
/// is how much they allocate.
/// </para>
/// <para>
/// A wide glyph straddling the clip edge is where an off-by-one in that arithmetic shows up, and it
/// is silent - a CJK line at a window edge loses or gains a column and nothing fails. The writer
/// has a single caller and no coverage, so these paint the same cells both ways and compare.
/// </para>
/// </remarks>
public class WriteTextRowClippedTests
{
	private const int Width = 20;
	private const int Height = 3;

	private static CharacterBuffer Fresh() => new(Width, Height, Color.Black);

	private static string RowText(CharacterBuffer buffer, int y)
	{
		var sb = new StringBuilder();
		for (int x = 0; x < Width; x++)
			sb.Append(buffer.GetCell(x, y).Character.ToString());
		return sb.ToString();
	}

	/// <summary>
	/// Builds the packed representation the fast writer takes, from the same cells the enumerable
	/// path is given, so the two describe one identical row.
	/// </summary>
	private static PaintedTextRow Row(List<Cell> cells)
	{
		var builder = new StyledText.Builder();
		foreach (var cell in cells)
			builder.Add(cell);

		var styled = builder.Build(new List<LinkSpan>());
		return new PaintedTextRow(new StyledRow(styled, 0, styled.Count));
	}

	/// <summary>Paints the same cells both ways and asserts the two buffers agree, cell for cell.</summary>
	private static void AssertSamePainting(string markup, int x, LayoutRect clip)
	{
		var cells = MarkupParser.Parse(markup, Color.White, Color.Black);

		var viaEnumerable = Fresh();
		viaEnumerable.WriteCellsClipped(x, 1, cells, clip);

		var viaRow = Fresh();
		viaRow.WriteTextRowClipped(x, 1, Row(cells), clip);

		Assert.Equal(RowText(viaEnumerable, 1), RowText(viaRow, 1));
	}

	public static IEnumerable<object[]> ClipWindows()
	{
		// Every left edge across the buffer, so a wide glyph is straddled at each offset in turn.
		for (int left = 0; left <= 8; left++)
			yield return new object[] { left };
	}

	/// <summary>
	/// Narrow text, every clip offset. If the start and end arithmetic is off by one anywhere, one
	/// of these windows paints a column the slow path does not.
	/// </summary>
	[Theory]
	[MemberData(nameof(ClipWindows))]
	public void NarrowText_PaintsTheSameAsTheEnumerablePath(int left)
	{
		AssertSamePainting("abcdefghij", x: 2, new LayoutRect(left, 0, Width - left, Height));
	}

	/// <summary>
	/// WIDE GLYPHS, every clip offset. Each takes two columns, so a clip edge can fall between a
	/// glyph's two halves: the writer has to decide whether that glyph is in or out, and both paths
	/// must decide the same way.
	/// </summary>
	[Theory]
	[MemberData(nameof(ClipWindows))]
	public void WideGlyphs_StraddlingTheClipEdge_PaintTheSame(int left)
	{
		AssertSamePainting("你好世界", x: 2, new LayoutRect(left, 0, Width - left, Height));
	}

	/// <summary>A clip that ends mid-glyph, from the right rather than the left.</summary>
	[Theory]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(6)]
	[InlineData(7)]
	public void WideGlyphs_ClippedOnTheRight_PaintTheSame(int right)
	{
		AssertSamePainting("你好世界", x: 2, new LayoutRect(0, 0, right, Height));
	}

	/// <summary>Text starting left of the clip, so the writer must skip into the middle of the row.</summary>
	[Fact]
	public void TextStartingBeforeTheClip_PaintsTheSame()
	{
		AssertSamePainting("abcdefghij", x: 0, new LayoutRect(4, 0, 10, Height));
	}

	/// <summary>Wide text starting left of the clip: skipping and straddling at once.</summary>
	[Fact]
	public void WideTextStartingBeforeTheClip_PaintsTheSame()
	{
		AssertSamePainting("你好世界", x: 0, new LayoutRect(3, 0, 10, Height));
	}

	/// <summary>A row wholly outside the clip paints nothing, as the enumerable path does.</summary>
	[Fact]
	public void RowOutsideTheClip_PaintsNothing()
	{
		AssertSamePainting("abcdefghij", x: 2, new LayoutRect(0, 2, Width, 1));
	}
}
