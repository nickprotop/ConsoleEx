// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

[Collection("InlineSpinner")]
public class InlineSpinnerParseTests
{
	[Fact]
	public void ParsesBareSpinnerToBrailleGlyph()
	{
		MarkupSpinnerClock.SetTimeProviderForTests(() => 0);
		try
		{
			var cells = MarkupParser.Parse("[spinner]", Color.White, Color.Black);
			Assert.NotEmpty(cells);
			string expected = SpinnerControl.FramesForStyle(SpinnerStyle.Braille)[0];
			Assert.Equal(expected, cells[0].Character.ToString());
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	[Fact]
	public void ParsesStyledSpinner()
	{
		MarkupSpinnerClock.SetTimeProviderForTests(() => 0);
		try
		{
			var cells = MarkupParser.Parse("[spinner circle]", Color.White, Color.Black);
			string expected = SpinnerControl.FramesForStyle(SpinnerStyle.Circle)[0];
			Assert.Equal(expected, cells[0].Character.ToString());
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	[Fact]
	public void GlyphInheritsScopeColor()
	{
		MarkupSpinnerClock.SetTimeProviderForTests(() => 0);
		try
		{
			var cells = MarkupParser.Parse("[yellow][spinner][/]", Color.White, Color.Black);
			Assert.Equal(Color.Yellow, cells[0].Foreground);
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	[Fact]
	public void EscapedSpinnerIsLiteral()
	{
		var cells = MarkupParser.Parse("[[spinner]]", Color.White, Color.Black);
		string text = string.Concat(cells.Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString()));
		Assert.Equal("[spinner]", text);
	}

	[Fact]
	public void UnknownStyleFallsBackToBraille()
	{
		MarkupSpinnerClock.SetTimeProviderForTests(() => 0);
		try
		{
			var cells = MarkupParser.Parse("[spinner bogus]", Color.White, Color.Black);
			string expected = SpinnerControl.FramesForStyle(SpinnerStyle.Braille)[0];
			Assert.Equal(expected, cells[0].Character.ToString());
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	[Fact]
	public void DotsSpinnerEmitsReservedWidthCells()
	{
		MarkupSpinnerClock.SetTimeProviderForTests(() => 0);
		try
		{
			var cells = MarkupParser.Parse("[spinner dots]", Color.White, Color.Black);
			Assert.Equal(MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Dots), cells.Count);
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	[Fact]
	public void NarrowDotsFrameIsPaddedToReservedWidth()
	{
		// Pin the clock to a narrower Dots frame and confirm the emitted cell count
		// still equals ReservedWidth (i.e. the glyph was padded).
		int interval = SharpConsoleUI.Configuration.ControlDefaults.SpinnerDefaultIntervalMs;
		// Dots frames: [".  " (3), ".. " (3), "..." (3)] are all width 3 after the preset's own padding,
		// so use the clock's CurrentGlyph contract directly: every frame must be ReservedWidth wide.
		MarkupSpinnerClock.SetTimeProviderForTests(() => interval); // frame 1
		try
		{
			var cells = MarkupParser.Parse("[spinner dots]", Color.White, Color.Black);
			Assert.Equal(MarkupSpinnerClock.ReservedWidth(SpinnerStyle.Dots), cells.Count);
		}
		finally { MarkupSpinnerClock.ResetTimeProviderForTests(); MarkupSpinnerClock.ResetForTests(); }
	}

	[Fact]
	public void NoWindowSystemDoesNotThrow()
	{
		var ex = Record.Exception(() => MarkupParser.Parse("plain [spinner] text", Color.White, Color.Black));
		Assert.Null(ex);
	}
}
