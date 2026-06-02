// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

[Collection("InlineSpinner")]
public class InlineSpinnerWidthTests
{
	[Fact]
	public void BareSpinnerReservesBrailleWidth()
	{
		Assert.Equal(MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Braille),
			MarkupParser.StripLength("[spinner]"));
	}

	[Fact]
	public void DotsSpinnerReservesThreeColumns()
	{
		Assert.Equal(3, MarkupParser.StripLength("[spinner dots]"));
	}

	[Fact]
	public void WidthIsConstantAcrossFrames()
	{
		MarkupSpinnerClock.SetTimeProviderForTests(() => 0);
		try
		{
			int w0 = MarkupParser.StripLength("[spinner dots]");
			MarkupSpinnerClock.SetTimeProviderForTests(() => ControlDefaults.SpinnerDefaultIntervalMs);
			int w1 = MarkupParser.StripLength("[spinner dots]");
			MarkupSpinnerClock.SetTimeProviderForTests(() => ControlDefaults.SpinnerDefaultIntervalMs * 2);
			int w2 = MarkupParser.StripLength("[spinner dots]");
			Assert.Equal(w0, w1);
			Assert.Equal(w1, w2);
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	[Fact]
	public void SpinnerWidthAddsToSurroundingText()
	{
		// "a " (2) + [spinner dots] (3) + " b" (2) = 7
		Assert.Equal(7, MarkupParser.StripLength("a [spinner dots] b"));
	}

	[Fact]
	public void EscapedSpinnerCountsLiteralChars()
	{
		// "[spinner]" literal = 9 visible chars
		Assert.Equal(9, MarkupParser.StripLength("[[spinner]]"));
	}

	// --- Explicit width: argument (min-reserve) ---

	[Fact]
	public void WidthArgPadsNarrowSpinner()
	{
		// braille is 1 col naturally; width:4 reserves 4.
		Assert.Equal(4, MarkupParser.StripLength("[spinner braille width:4]"));
	}

	[Fact]
	public void WidthArgIsAMinimumAndNeverClipsWiderGlyph()
	{
		// aestheticbar reserves 6 naturally; a smaller request clamps up to 6.
		int natural = MarkupSpinnerClock.ReservedWidth(SpinnerStyle.AestheticBar);
		Assert.Equal(natural, MarkupParser.StripLength("[spinner aestheticbar width:3]"));
	}

	[Fact]
	public void ReservedWidthHonorsExplicitMinimum()
	{
		Assert.Equal(5, MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Dots, 5));   // larger than natural 3
		Assert.Equal(3, MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Dots, 2));   // clamped up to natural 3
		Assert.Equal(3, MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Dots, 0));   // 0 → natural
	}

	[Fact]
	public void WidthArgAddsToSurroundingText()
	{
		// "a " (2) + [spinner braille width:4] (4) + " b" (2) = 8
		Assert.Equal(8, MarkupParser.StripLength("a [spinner braille width:4] b"));
	}

	// --- Named argument parsing (ms: / width:) and legacy positional interval ---

	[Theory]
	[InlineData("spinner dots ms:250 width:5", SpinnerStyle.Dots, 250, 5)]
	[InlineData("spinner dots width:5 ms:250", SpinnerStyle.Dots, 250, 5)] // order-independent
	[InlineData("spinner braille width:4", SpinnerStyle.Braille, 100, 4)]  // braille default interval
	[InlineData("spinner dots 80", SpinnerStyle.Dots, 80, 0)]              // legacy positional interval
	[InlineData("spinner dots", SpinnerStyle.Dots, 360, 0)]               // per-style default, no width
	[InlineData("spinner dots width:0", SpinnerStyle.Dots, 360, 0)]       // non-positive width ignored
	[InlineData("spinner dots ms:0", SpinnerStyle.Dots, 360, 0)]          // non-positive ms ignored → default
	[InlineData("spinner ms:200", SpinnerStyle.Braille, 200, 0)]          // named arg without a style word
	public void ParsesStyleIntervalAndWidth(string tag, SpinnerStyle expectedStyle, int expectedInterval, int expectedWidth)
	{
		bool ok = MarkupParser.TryParseSpinnerTagForTests(tag, out var style, out int interval, out int width);
		Assert.True(ok);
		Assert.Equal(expectedStyle, style);
		Assert.Equal(expectedInterval, interval);
		Assert.Equal(expectedWidth, width);
	}
}
