// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// <see cref="CanvasGraphics.SetCell"/> writes a whole cell, keeping what
/// <see cref="CanvasGraphics.SetNarrowCell"/> discards.
/// </summary>
/// <remarks>
/// The canvas could read a <see cref="Cell"/> through <c>GetCell</c> but had no way
/// to write one back, so there was no correct way to put parsed text on a canvas at
/// all: everything had to go through <c>SetNarrowCell</c>, which drops the
/// wide-character continuation flag and combining marks. That is the case CLAUDE.md
/// rule 12 exists to prevent.
/// </remarks>
public class CanvasGraphicsSetCellTests
{
	private static (CharacterBuffer buffer, CanvasGraphics g) Make(
		int w = 30, int h = 10, int offsetX = 0, int offsetY = 0)
	{
		var buffer = new CharacterBuffer(w, h, Color.Black);
		var clip = new LayoutRect(0, 0, w, h);
		return (buffer, new CanvasGraphics(buffer, offsetX, offsetY, w, h, clip));
	}

	[Fact]
	public void SetCell_WritesTheCharacterAndColours()
	{
		var (buffer, g) = Make();
		var cell = new Cell(new Rune('A'), Color.Red, Color.Blue);

		g.SetCell(3, 2, cell);

		var written = buffer.GetCell(3, 2);
		Assert.Equal(new Rune('A'), written.Character);
		Assert.Equal(Color.Red, written.Foreground);
		Assert.Equal(Color.Blue, written.Background);
	}

	[Fact]
	public void SetCell_KeepsTheWideContinuationFlag()
	{
		// The flag that makes a wide glyph occupy two columns. Losing it leaves the
		// second column showing a stale character and every later column shifted.
		var (buffer, g) = Make();
		var continuation = new Cell(new Rune(' '), Color.White, Color.Black)
		{
			IsWideContinuation = true,
		};

		g.SetCell(4, 1, continuation);

		Assert.True(buffer.GetCell(4, 1).IsWideContinuation);
	}

	[Fact]
	public void SetCell_KeepsCombiningMarks()
	{
		// A combining mark shares its base glyph's cell. Dropping it renders "é" as
		// "e" - silently, and only for the accented languages nobody tests in.
		var (buffer, g) = Make();
		var cell = new Cell(new Rune('e'), Color.White, Color.Black)
		{
			Combiners = "́",
		};

		g.SetCell(2, 2, cell);

		Assert.Equal("́", buffer.GetCell(2, 2).Combiners);
	}

	[Fact]
	public void SetCell_KeepsDecorations()
	{
		var (buffer, g) = Make();
		var cell = new Cell(new Rune('x'), Color.White, Color.Black, TextDecoration.Underline);

		g.SetCell(1, 1, cell);

		Assert.Equal(TextDecoration.Underline, buffer.GetCell(1, 1).Decorations);
	}

	[Fact]
	public void SetNarrowCell_StillDiscardsThoseFlags()
	{
		// The contract of the narrow path: it is for literal narrow characters -
		// borders, padding, fill - and clears the flags on purpose. This pins the
		// distinction so the two do not quietly converge.
		var (buffer, g) = Make();
		g.SetCell(5, 5, new Cell(new Rune('e'), Color.White, Color.Black) { Combiners = "́" });

		g.SetNarrowCell(5, 5, ' ', Color.White, Color.Black);

		Assert.Null(buffer.GetCell(5, 5).Combiners);
	}

	[Fact]
	public void SetCell_AppliesTheCanvasOffset()
	{
		var (buffer, g) = Make(offsetX: 6, offsetY: 4);

		g.SetCell(2, 1, new Cell(new Rune('Z'), Color.White, Color.Black));

		Assert.Equal(new Rune('Z'), buffer.GetCell(8, 5).Character);
	}

	[Fact]
	public void SetCell_RespectsTheClipRect()
	{
		// A canvas scrolled inside a panel must not paint over its siblings. Every
		// other primitive clips; this one has to as well.
		var buffer = new CharacterBuffer(40, 20, Color.Black);
		var clip = new LayoutRect(10, 5, 10, 5);
		var g = new CanvasGraphics(buffer, 0, 0, 40, 20, clip);

		g.SetCell(2, 2, new Cell(new Rune('!'), Color.White, Color.Black));   // outside
		g.SetCell(12, 6, new Cell(new Rune('?'), Color.White, Color.Black));  // inside

		Assert.NotEqual(new Rune('!'), buffer.GetCell(2, 2).Character);
		Assert.Equal(new Rune('?'), buffer.GetCell(12, 6).Character);
	}

	[Fact]
	public void ParsedMarkupSurvivesARoundTripThroughTheCanvas()
	{
		// The real usage path: parse markup, write the cells to a canvas, read them
		// back. A wide glyph must still occupy two columns with its continuation
		// intact - the whole reason this overload exists.
		var (buffer, g) = Make(w: 20, h: 4);
		var cells = MarkupParser.Parse("[red]漢[/]a", Color.White, Color.Black);

		for (int i = 0; i < cells.Count; i++) g.SetCell(i, 0, cells[i]);

		Assert.Equal(3, cells.Count);                                  // wide glyph + continuation + 'a'
		Assert.Equal(new Rune('漢'), buffer.GetCell(0, 0).Character);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
		Assert.Equal(new Rune('a'), buffer.GetCell(2, 0).Character);
	}
}
