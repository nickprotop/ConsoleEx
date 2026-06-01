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
}
