// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// East Asian Ambiguous width policy.
/// <para>
/// The Ambiguous class is the one width question Unicode deliberately leaves open: those codepoints
/// were encoded in both Western and East Asian character sets, so a CJK-configured terminal draws
/// them 2 columns and a Western one draws them 1, and neither is wrong. Wcwidth resolves them to 1
/// and offers no mode, so honouring a terminal that says otherwise needs our own table.
/// </para>
/// <para>
/// These tests mutate process-wide capability state, so they run in their own non-parallel
/// collection — a concurrent test measuring a width would otherwise see whichever policy this class
/// happened to have set.
/// </para>
/// </summary>
[Collection("AmbiguousWidth")]
public class AmbiguousWidthTests : System.IDisposable
{
	public AmbiguousWidthTests() => TerminalCapabilities.SetAmbiguousCharactersAreWide(false);

	public void Dispose() => TerminalCapabilities.SetAmbiguousCharactersAreWide(false);

	#region The default must not move

	[Theory]
	[InlineData("°")]   // DEGREE SIGN
	[InlineData("±")]   // PLUS-MINUS
	[InlineData("“")]   // LEFT DOUBLE QUOTATION MARK
	[InlineData("…")]   // HORIZONTAL ELLIPSIS
	[InlineData("α")]   // GREEK SMALL ALPHA
	[InlineData("д")]   // CYRILLIC DE
	public void ByDefault_AmbiguousCharactersAreNarrow(string s)
	{
		// The historical behaviour, and what every existing application renders today.
		Assert.Equal(1, UnicodeWidth.GetStringWidth(s));
	}

	[Fact]
	public void ByDefault_TheCapabilityIsFalse()
	{
		TerminalCapabilities.Reset();
		Assert.False(TerminalCapabilities.AmbiguousCharactersAreWide);
	}

	#endregion

	#region Policy on

	[Theory]
	[InlineData("°")]
	[InlineData("±")]
	[InlineData("“")]
	[InlineData("…")]
	[InlineData("α")]
	[InlineData("д")]
	public void WhenWide_AmbiguousTextCharactersBecomeTwoColumns(string s)
	{
		TerminalCapabilities.SetAmbiguousCharactersAreWide(true);

		Assert.Equal(2, UnicodeWidth.GetStringWidth(s));
	}

	[Theory]
	[InlineData("a")]    // plain ASCII — Narrow, never ambiguous
	[InlineData("中")]   // unambiguously Wide, already 2
	[InlineData("🚀")]   // emoji, already 2
	public void WhenWide_UnambiguousCharactersAreUnaffected(string s)
	{
		int before = UnicodeWidth.GetStringWidth(s);
		TerminalCapabilities.SetAmbiguousCharactersAreWide(true);

		Assert.Equal(before, UnicodeWidth.GetStringWidth(s));
	}

	#endregion

	#region The chrome exclusion

	[Theory]
	[InlineData("─")]   // BOX DRAWINGS LIGHT HORIZONTAL — window borders
	[InlineData("│")]   // BOX DRAWINGS LIGHT VERTICAL
	[InlineData("┌")]   // BOX DRAWINGS LIGHT DOWN AND RIGHT
	[InlineData("█")]   // FULL BLOCK — scrollbar thumb
	[InlineData("░")]   // LIGHT SHADE
	[InlineData("●")]   // BLACK CIRCLE — radio glyph, password mask
	[InlineData("▼")]   // BLACK DOWN-POINTING TRIANGLE — dropdown
	[InlineData("→")]   // RIGHTWARDS ARROW — menu indicator
	public void ChromeGlyphs_StayNarrow_EvenWhenTheTerminalSaysWide(string s)
	{
		// These ARE East Asian Ambiguous, so a faithful implementation would widen them — and the
		// library draws its own borders, scrollbars and checkboxes from them. Widening them without
		// also teaching every renderer that a glyph may fill two columns would take the chrome apart.
		TerminalCapabilities.SetAmbiguousCharactersAreWide(true);

		Assert.Equal(1, UnicodeWidth.GetStringWidth(s));
	}

	[Fact]
	public void TheExcludedGlyphsAreGenuinelyAmbiguous()
	{
		// Pins the honesty of the exclusion: it is a deliberate deviation from the standard, not a
		// gap in the table. If a future table regenerates without these, this fails and says so.
		foreach (var s in new[] { "─", "│", "█", "●", "▼", "→" })
		{
			int cp = char.ConvertToUtf32(s, 0);
			Assert.True(EastAsianAmbiguous.IsAmbiguous(cp), $"U+{cp:X4} should be in the EAW=A table");
			Assert.True(EastAsianAmbiguous.IsChromeExcluded(cp), $"U+{cp:X4} should be chrome-excluded");
			Assert.False(EastAsianAmbiguous.IsWidenedWhenAmbiguousWide(cp));
		}
	}

	[Fact]
	public void WarningGlyph_IsNeutral_SoThePolicyNeverTouchesIt()
	{
		// ⚠ (U+26A0) is the only glyph this library draws from Miscellaneous Symbols, and it is East
		// Asian NEUTRAL, not Ambiguous. That is why the block is not chrome-excluded: excluding it
		// would have narrowed 69 genuinely-ambiguous codepoints (★, ☎) to protect a character the
		// policy cannot affect. This pins the fact the decision rests on.
		int cp = char.ConvertToUtf32("⚠", 0);
		Assert.False(EastAsianAmbiguous.IsAmbiguous(cp));

		TerminalCapabilities.SetAmbiguousCharactersAreWide(true);
		Assert.Equal(1, UnicodeWidth.GetStringWidth("⚠"));
	}

	[Fact]
	public void AmbiguousSymbolsOutsideChromeRanges_AreStillWidened()
	{
		// ★ (U+2605) is Ambiguous and is NOT chrome — user content, so the policy applies.
		TerminalCapabilities.SetAmbiguousCharactersAreWide(true);
		Assert.Equal(2, UnicodeWidth.GetStringWidth("★"));
	}

	#endregion

	#region Table integrity

	[Fact]
	public void Table_IsSortedAndNonOverlapping()
	{
		// A binary search over the flattened pairs is only correct if this holds; the generator
		// guarantees it, and this catches a hand-edit that breaks it.
		int previousHigh = -1;
		for (int cp = 0; cp <= 0x10FFFF; cp += 1)
		{
			if (!EastAsianAmbiguous.IsAmbiguous(cp)) continue;
			Assert.True(cp > previousHigh);
			previousHigh = cp;
		}
	}

	[Theory]
	[InlineData(0x00A1, true)]    // INVERTED EXCLAMATION MARK — first ambiguous codepoint
	[InlineData(0x0061, false)]   // 'a' — Narrow
	[InlineData(0x4E2D, false)]   // 中 — Wide, not Ambiguous
	[InlineData(0x2026, true)]    // … — Ambiguous
	[InlineData(0xE000, true)]    // Private Use Area — Ambiguous per UCD
	public void Table_ClassifiesKnownCodepoints(int codepoint, bool expected)
	{
		Assert.Equal(expected, EastAsianAmbiguous.IsAmbiguous(codepoint));
	}

	#endregion

	#region Layout consequence

	[Fact]
	public void WhenWide_StringWidthAccountsForEveryOccurrence()
	{
		// The reason this matters: the error accumulates once per ambiguous character, so a line of
		// them drifts further the longer it is.
		const string s = "±±±±±";
		Assert.Equal(5, UnicodeWidth.GetStringWidth(s));

		TerminalCapabilities.SetAmbiguousCharactersAreWide(true);

		Assert.Equal(10, UnicodeWidth.GetStringWidth(s));
	}

	[Fact]
	public void WhenWide_ColumnAndCharOffsetsStayConsistent()
	{
		// ColumnToCharOffset and CharOffsetToColumn must agree with GetStringWidth under the policy,
		// or caret placement and wrapping would disagree with what is painted.
		TerminalCapabilities.SetAmbiguousCharactersAreWide(true);
		const string s = "a±b";

		Assert.Equal(4, UnicodeWidth.GetStringWidth(s));
		Assert.Equal(0, UnicodeWidth.CharOffsetToColumn(s, 0));
		Assert.Equal(1, UnicodeWidth.CharOffsetToColumn(s, 1)); // after 'a'
		Assert.Equal(3, UnicodeWidth.CharOffsetToColumn(s, 2)); // after the 2-column '±'
		Assert.Equal(4, UnicodeWidth.CharOffsetToColumn(s, 3)); // after 'b'
	}

	#endregion
}
