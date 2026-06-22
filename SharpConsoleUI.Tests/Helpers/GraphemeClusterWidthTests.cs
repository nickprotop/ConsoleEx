// -----------------------------------------------------------------------
// ConsoleEx - ZWJ grapheme-cluster width tests
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class GraphemeClusterWidthTests
{
	// U+1F468 U+200D U+1F469 U+200D U+1F467 U+200D U+1F466 — family ZWJ sequence (one rendered glyph).
	private const string Family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";

	[Fact]
	public void SupportsZwjLigation_DefaultsTrue()
	{
		Assert.True(TerminalCapabilities.SupportsZwjLigation);
	}

	[Theory]
	[InlineData(3, true)]    // cursor at column 3 → 2-wide → ligated
	[InlineData(9, false)]   // cursor at column 9 → 8-wide → not ligated
	[InlineData(-1, true)]   // timeout/error → assume modern (ligating)
	public void ProbeZwjLigation_InterpretsCursorColumn(int dsrColumn, bool expected)
	{
		Assert.Equal(expected, TerminalCapabilities.ProbeZwjLigationForTest(dsrColumn));
	}

	[Fact]
	public void GetStringWidth_FamilyZwj_IsTwo()
	{
		Assert.Equal(2, UnicodeWidth.GetStringWidth(Family));
	}

	[Fact]
	public void GetStringWidth_FamilyZwjPlusText_AddsCorrectly()
	{
		Assert.Equal(4, UnicodeWidth.GetStringWidth(Family + " x"));
	}

	[Theory]
	[InlineData("\U0001F1EC\U0001F1E7", 2)]                                   // 🇬🇧
	[InlineData("\U0001F1EC\U0001F1E7\U0001F1EB\U0001F1F7", 4)]               // 🇬🇧🇫🇷
	public void GetStringWidth_Flags_Unchanged(string s, int expected)
	{
		Assert.Equal(expected, UnicodeWidth.GetStringWidth(s));
	}

	[Fact]
	public void WidthMethods_AgreeAcrossCluster()
	{
		string s = Family + " x";
		int clusterChars = Family.Length;
		Assert.Equal(2, UnicodeWidth.CharOffsetToColumn(s, clusterChars));
		Assert.Equal(clusterChars, UnicodeWidth.ColumnToCharOffset(s, 2));
		var (endChar, width) = UnicodeWidth.TakeColumns(s, 0, 2);
		Assert.Equal(clusterChars, endChar);
		Assert.Equal(2, width);
	}

	[Fact]
	public void TakeColumns_NeverStallsOnCluster()
	{
		var (endChar, width) = UnicodeWidth.TakeColumns(Family, 0, 1);
		Assert.True(endChar > 0, "must consume at least the cluster, never stall");
		Assert.Equal(2, width);
	}
}
