// -----------------------------------------------------------------------
// ConsoleEx - ZWJ cluster cell-emit tests
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Parsing;

public class MarkupClusterCellTests
{
	private const string Family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";

	[Fact]
	public void Parse_FamilyZwj_EmitsTwoCells()
	{
		var cells = MarkupParser.Parse(Family, Color.White, Color.Black, out _, null);
		Assert.Equal(2, cells.Count);
		Assert.False(cells[0].IsWideContinuation);
		Assert.True(cells[1].IsWideContinuation);
	}

	[Fact]
	public void Parse_FamilyZwj_BaseCarriesFullClusterTail()
	{
		var cells = MarkupParser.Parse(Family, Color.White, Color.Black, out _, null);
		var sb = new StringBuilder();
		sb.Append(cells[0].Character.ToString());
		if (cells[0].Combiners != null) sb.Append(cells[0].Combiners);
		Assert.Equal(Family, sb.ToString());
	}

	[Fact]
	public void Parse_FamilyZwjThenText_TextStartsRightAfterTheTwoCells()
	{
		var cells = MarkupParser.Parse(Family + "AB", Color.White, Color.Black, out _, null);
		Assert.Equal(4, cells.Count);
		Assert.Equal(new System.Text.Rune('A'), cells[2].Character);
		Assert.Equal(new System.Text.Rune('B'), cells[3].Character);
	}

	[Fact]
	public void FamilyZwj_TrailingTextColumn_MatchesMeasuredWidth()
	{
		// The lockstep invariant, end to end: the cell index of the first trailing-text glyph must equal
		// the measured display width of everything before it (no drift — the visible bug this fix removes).
		string prefix = "X " + Family + " ";
		var cells = MarkupParser.Parse(prefix + "family", Color.White, Color.Black, out _, null);
		int firstFamilyCell = -1;
		for (int i = 0; i < cells.Count; i++)
		{
			if (cells[i].Character == new System.Text.Rune('f') && !cells[i].IsWideContinuation) { firstFamilyCell = i; break; }
		}
		Assert.True(firstFamilyCell > 0);
		Assert.Equal(SharpConsoleUI.Helpers.UnicodeWidth.GetStringWidth(prefix), firstFamilyCell);
	}
}
