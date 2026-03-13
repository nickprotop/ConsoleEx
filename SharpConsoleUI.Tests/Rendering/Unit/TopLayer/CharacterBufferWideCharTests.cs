// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using Xunit;
using System.Text;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for wide character (CJK, fullwidth) handling in CharacterBuffer.
/// Wide characters occupy 2 columns: the character itself and a continuation cell.
/// </summary>
public class CharacterBufferWideCharTests
{
	// --- WriteString with wide chars ---

	[Fact]
	public void WriteString_CjkChar_OccupiesTwoColumns()
	{
		var buffer = new CharacterBuffer(20, 5);

		buffer.WriteString(0, 0, "\u4E2D", Color.White, Color.Black);

		var cell0 = buffer.GetCell(0, 0);
		var cell1 = buffer.GetCell(1, 0);

		Assert.Equal(new Rune('\u4E2D'), cell0.Character);
		Assert.False(cell0.IsWideContinuation);

		Assert.True(cell1.IsWideContinuation);
	}

	[Fact]
	public void WriteString_MixedText_CorrectColumnPositions()
	{
		var buffer = new CharacterBuffer(20, 5);

		buffer.WriteString(0, 0, "A\u4E2DB", Color.White, Color.Black);

		// Column 0: 'A'
		var cellA = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('A'), cellA.Character);
		Assert.False(cellA.IsWideContinuation);

		// Columns 1-2: wide char + continuation
		var cellWide = buffer.GetCell(1, 0);
		var cellCont = buffer.GetCell(2, 0);
		Assert.Equal(new Rune('\u4E2D'), cellWide.Character);
		Assert.False(cellWide.IsWideContinuation);
		Assert.True(cellCont.IsWideContinuation);

		// Column 3: 'B'
		var cellB = buffer.GetCell(3, 0);
		Assert.Equal(new Rune('B'), cellB.Character);
		Assert.False(cellB.IsWideContinuation);
	}

	[Fact]
	public void WriteString_WideCharAtEndOfBuffer_Clipped()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Write wide char at last column — no room for continuation
		buffer.WriteString(19, 0, "\u4E2D", Color.White, Color.Black);

		var cell = buffer.GetCell(19, 0);
		Assert.Equal(new Rune(' '), cell.Character);
		Assert.False(cell.IsWideContinuation);
	}

	[Fact]
	public void WriteString_MultipleWideChars_CorrectSpacing()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Three CJK chars: U+4E2D U+6587 U+5B57
		buffer.WriteString(0, 0, "\u4E2D\u6587\u5B57", Color.White, Color.Black);

		// Columns 0-1
		Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(0, 0).Character);
		Assert.False(buffer.GetCell(0, 0).IsWideContinuation);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);

		// Columns 2-3
		Assert.Equal(new Rune('\u6587'), buffer.GetCell(2, 0).Character);
		Assert.False(buffer.GetCell(2, 0).IsWideContinuation);
		Assert.True(buffer.GetCell(3, 0).IsWideContinuation);

		// Columns 4-5
		Assert.Equal(new Rune('\u5B57'), buffer.GetCell(4, 0).Character);
		Assert.False(buffer.GetCell(4, 0).IsWideContinuation);
		Assert.True(buffer.GetCell(5, 0).IsWideContinuation);
	}

	[Fact]
	public void WriteString_AsciiAfterWide_CorrectPosition()
	{
		var buffer = new CharacterBuffer(20, 5);

		buffer.WriteString(0, 0, "\u4E2DA", Color.White, Color.Black);

		// Columns 0-1: wide char + continuation
		Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(0, 0).Character);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);

		// Column 2: 'A'
		Assert.Equal(new Rune('A'), buffer.GetCell(2, 0).Character);
		Assert.False(buffer.GetCell(2, 0).IsWideContinuation);
	}

	// --- WriteStringClipped with wide chars ---

	[Fact]
	public void WriteStringClipped_WideCharStraddlesRightClip_ReplacedWithSpace()
	{
		var buffer = new CharacterBuffer(20, 5);
		var clipRect = new LayoutRect(0, 0, 1, 5);

		// Wide char at x=0, clip right edge is 1 — only first column is in clip
		buffer.WriteStringClipped(0, 0, "\u4E2D", Color.White, Color.Black, clipRect);

		var cell0 = buffer.GetCell(0, 0);
		Assert.Equal(new Rune(' '), cell0.Character);
		Assert.False(cell0.IsWideContinuation);
	}

	[Fact]
	public void WriteStringClipped_WideCharFullyInClip_WrittenNormally()
	{
		var buffer = new CharacterBuffer(20, 5);
		var clipRect = new LayoutRect(0, 0, 10, 5);

		buffer.WriteStringClipped(0, 0, "\u4E2D", Color.White, Color.Black, clipRect);

		Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(0, 0).Character);
		Assert.False(buffer.GetCell(0, 0).IsWideContinuation);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
	}

	[Fact]
	public void WriteStringClipped_MixedTextInClipRect_CorrectLayout()
	{
		var buffer = new CharacterBuffer(20, 5);
		var clipRect = new LayoutRect(0, 0, 6, 5);

		buffer.WriteStringClipped(0, 0, "A\u4E2DB", Color.White, Color.Black, clipRect);

		Assert.Equal(new Rune('A'), buffer.GetCell(0, 0).Character);
		Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(1, 0).Character);
		Assert.True(buffer.GetCell(2, 0).IsWideContinuation);
		Assert.Equal(new Rune('B'), buffer.GetCell(3, 0).Character);
	}

	[Fact]
	public void WriteStringClipped_WideCharStraddlesLeftClip_ReplacedWithSpace()
	{
		var buffer = new CharacterBuffer(20, 5);
		// Clip starts at column 1 — the second column of a wide char at x=0
		var clipRect = new LayoutRect(1, 0, 10, 5);

		buffer.WriteStringClipped(0, 0, "\u4E2D", Color.White, Color.Black, clipRect);

		// The first column (x=0) is outside clip, the second column (x=1) is in clip
		// Since wide char straddles left clip, only continuation column visible -> space
		var cell1 = buffer.GetCell(1, 0);
		Assert.Equal(new Rune(' '), cell1.Character);
		Assert.False(cell1.IsWideContinuation);
	}

	// --- WriteCells with wide chars ---

	[Fact]
	public void WriteCells_CellsWithWideChar_ContinuationWritten()
	{
		var buffer = new CharacterBuffer(20, 5);

		var cells = new[]
		{
			new Cell('\u4E2D', Color.White, Color.Black),
			new Cell(' ', Color.White, Color.Black) { IsWideContinuation = true },
			new Cell('X', Color.White, Color.Black)
		};

		buffer.WriteCells(0, 0, cells);

		// The wide char cell is written at x=0
		var cell0 = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('\u4E2D'), cell0.Character);

		// Continuation at x=1
		var cell1 = buffer.GetCell(1, 0);
		Assert.True(cell1.IsWideContinuation);

		// 'X' at x=2
		var cell2 = buffer.GetCell(2, 0);
		Assert.Equal(new Rune('X'), cell2.Character);
		Assert.False(cell2.IsWideContinuation);
	}

	// --- Overwrite scenarios ---

	[Fact]
	public void SetCell_OverwriteContinuationCell_ClearsWideCharAtPrevious()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Write a wide char at x=0 (occupies 0 and 1)
		buffer.WriteString(0, 0, "\u4E2D", Color.White, Color.Black);
		Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(0, 0).Character);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);

		// Overwrite the continuation cell at x=1
		buffer.SetNarrowCell(1, 0, 'Z', Color.White, Color.Black);

		// The wide char at x=0 should be cleaned up (replaced with space)
		Assert.Equal(new Rune(' '), buffer.GetCell(0, 0).Character);
		Assert.False(buffer.GetCell(0, 0).IsWideContinuation);

		// x=1 should now be 'Z'
		Assert.Equal(new Rune('Z'), buffer.GetCell(1, 0).Character);
		Assert.False(buffer.GetCell(1, 0).IsWideContinuation);
	}

	[Fact]
	public void SetCell_OverwriteWideCharFirstCell_ClearsContinuation()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Write a wide char at x=0 (occupies 0 and 1)
		buffer.WriteString(0, 0, "\u4E2D", Color.White, Color.Black);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);

		// Overwrite the first cell at x=0
		buffer.SetNarrowCell(0, 0, 'A', Color.White, Color.Black);

		// x=0 should now be 'A'
		Assert.Equal(new Rune('A'), buffer.GetCell(0, 0).Character);
		Assert.False(buffer.GetCell(0, 0).IsWideContinuation);

		// The continuation at x=1 should be cleaned up
		Assert.Equal(new Rune(' '), buffer.GetCell(1, 0).Character);
		Assert.False(buffer.GetCell(1, 0).IsWideContinuation);
	}

	[Fact]
	public void SetCell_NarrowOverWide_BothCellsUpdated()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Write a wide char at x=2
		buffer.WriteString(2, 0, "\u4E2D", Color.White, Color.Black);
		Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(2, 0).Character);
		Assert.True(buffer.GetCell(3, 0).IsWideContinuation);

		// Overwrite first half with narrow char
		buffer.SetNarrowCell(2, 0, 'N', Color.White, Color.Black);

		// x=2 should be 'N'
		Assert.Equal(new Rune('N'), buffer.GetCell(2, 0).Character);
		Assert.False(buffer.GetCell(2, 0).IsWideContinuation);

		// x=3 continuation should be cleaned up
		Assert.Equal(new Rune(' '), buffer.GetCell(3, 0).Character);
		Assert.False(buffer.GetCell(3, 0).IsWideContinuation);
	}

	[Fact]
	public void SetCell_WideOverNarrow_ContinuationCreated()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Write narrow chars
		buffer.WriteString(0, 0, "AB", Color.White, Color.Black);
		Assert.Equal(new Rune('A'), buffer.GetCell(0, 0).Character);
		Assert.Equal(new Rune('B'), buffer.GetCell(1, 0).Character);

		// Overwrite with wide char via WriteString
		buffer.WriteString(0, 0, "\u4E2D", Color.White, Color.Black);

		Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(0, 0).Character);
		Assert.False(buffer.GetCell(0, 0).IsWideContinuation);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
	}

	// --- Fill/Clear ---

	[Fact]
	public void Clear_RemovesAllContinuationFlags()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Write several wide chars
		buffer.WriteString(0, 0, "\u4E2D\u6587", Color.White, Color.Black);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
		Assert.True(buffer.GetCell(3, 0).IsWideContinuation);

		// Clear the buffer
		buffer.Clear(Color.Black);

		// All cells should have no continuation flags
		for (int x = 0; x < buffer.Width; x++)
		{
			for (int y = 0; y < buffer.Height; y++)
			{
				Assert.False(buffer.GetCell(x, y).IsWideContinuation,
					$"Cell ({x},{y}) should not have IsWideContinuation after Clear");
			}
		}
	}

	[Fact]
	public void FillRect_OverWideChars_CleansUpContinuations()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Write wide chars spanning columns 0-5
		buffer.WriteString(0, 0, "\u4E2D\u6587\u5B57", Color.White, Color.Black);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
		Assert.True(buffer.GetCell(3, 0).IsWideContinuation);
		Assert.True(buffer.GetCell(5, 0).IsWideContinuation);

		// FillRect over the wide chars area
		buffer.FillRect(new LayoutRect(0, 0, 6, 1), ' ', Color.White, Color.Black);

		// All cells in the filled region should be spaces with no continuation
		for (int x = 0; x < 6; x++)
		{
			var cell = buffer.GetCell(x, 0);
			Assert.Equal(new Rune(' '), cell.Character);
			Assert.False(cell.IsWideContinuation,
				$"Cell ({x},0) should not have IsWideContinuation after FillRect");
		}
	}

	// --- Emoji (Surrogate Pair) Tests ---

	[Fact]
	public void WriteString_Emoji_OccupiesTwoColumns()
	{
		var buffer = new CharacterBuffer(20, 5);

		buffer.WriteString(0, 0, "\U0001F4A9", Color.White, Color.Black); // 💩

		var cell0 = buffer.GetCell(0, 0);
		var cell1 = buffer.GetCell(1, 0);

		Assert.Equal(new Rune(0x1F4A9), cell0.Character);
		Assert.False(cell0.IsWideContinuation);
		Assert.True(cell1.IsWideContinuation);
	}

	[Fact]
	public void WriteString_EmojiWithAscii_CorrectPositions()
	{
		var buffer = new CharacterBuffer(20, 5);

		// "A💩B" = A(1) + 💩(2) + B(1) = 4 columns
		buffer.WriteString(0, 0, "A\U0001F4A9B", Color.White, Color.Black);

		Assert.Equal(new Rune('A'), buffer.GetCell(0, 0).Character);
		Assert.Equal(new Rune(0x1F4A9), buffer.GetCell(1, 0).Character);
		Assert.True(buffer.GetCell(2, 0).IsWideContinuation);
		Assert.Equal(new Rune('B'), buffer.GetCell(3, 0).Character);
	}

	[Fact]
	public void WriteString_MultipleEmoji_CorrectSpacing()
	{
		var buffer = new CharacterBuffer(20, 5);

		// "🔥🎉" = 🔥(2) + 🎉(2) = 4 columns
		buffer.WriteString(0, 0, "\U0001F525\U0001F389", Color.White, Color.Black);

		Assert.Equal(new Rune(0x1F525), buffer.GetCell(0, 0).Character);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
		Assert.Equal(new Rune(0x1F389), buffer.GetCell(2, 0).Character);
		Assert.True(buffer.GetCell(3, 0).IsWideContinuation);
	}

	[Fact]
	public void WriteString_EmojiAtEndOfBuffer_Clipped()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Emoji at last column — no room for continuation
		buffer.WriteString(19, 0, "\U0001F525", Color.White, Color.Black);

		var cell = buffer.GetCell(19, 0);
		Assert.Equal(new Rune(' '), cell.Character);
		Assert.False(cell.IsWideContinuation);
	}

	[Fact]
	public void WriteString_MixedEmojiCjkAscii_CorrectLayout()
	{
		var buffer = new CharacterBuffer(30, 5);

		// "Hello🔥中B" = H(1)+e(1)+l(1)+l(1)+o(1)+🔥(2)+中(2)+B(1) = 10 columns
		buffer.WriteString(0, 0, "Hello\U0001F525\u4E2DB", Color.White, Color.Black);

		Assert.Equal(new Rune('H'), buffer.GetCell(0, 0).Character);
		Assert.Equal(new Rune('o'), buffer.GetCell(4, 0).Character);
		Assert.Equal(new Rune(0x1F525), buffer.GetCell(5, 0).Character);
		Assert.True(buffer.GetCell(6, 0).IsWideContinuation);
		Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(7, 0).Character);
		Assert.True(buffer.GetCell(8, 0).IsWideContinuation);
		Assert.Equal(new Rune('B'), buffer.GetCell(9, 0).Character);
	}

	[Fact]
	public void WriteStringClipped_EmojiStraddlesRightClip_ReplacedWithSpace()
	{
		var buffer = new CharacterBuffer(20, 5);
		var clipRect = new LayoutRect(0, 0, 1, 5);

		// Emoji at x=0, clip right edge is 1 — only first column in clip
		buffer.WriteStringClipped(0, 0, "\U0001F525", Color.White, Color.Black, clipRect);

		var cell0 = buffer.GetCell(0, 0);
		Assert.Equal(new Rune(' '), cell0.Character);
		Assert.False(cell0.IsWideContinuation);
	}

	[Fact]
	public void SetCell_OverwriteEmojiContinuation_CleansUp()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Write emoji at x=0 (occupies 0 and 1)
		buffer.WriteString(0, 0, "\U0001F4A9", Color.White, Color.Black);
		Assert.Equal(new Rune(0x1F4A9), buffer.GetCell(0, 0).Character);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);

		// Overwrite the continuation cell at x=1
		buffer.SetNarrowCell(1, 0, 'Z', Color.White, Color.Black);

		// The emoji at x=0 should be cleaned up
		Assert.Equal(new Rune(' '), buffer.GetCell(0, 0).Character);
		Assert.Equal(new Rune('Z'), buffer.GetCell(1, 0).Character);
	}

	// --- Zero-Width / Combiners Tests ---

	[Fact]
	public void WriteString_WithVariationSelector_AttachesToPreviousCell()
	{
		var buffer = new CharacterBuffer(20, 5);

		// ⚡ (U+26A1) is wide (2 cols) per Wcwidth + FE0F attaches to base cell (not continuation)
		buffer.WriteString(0, 0, "\u26A1\uFE0F", Color.White, Color.Black);

		var cell0 = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('\u26A1'), cell0.Character);
		// FE0F attaches to base cell (skips past continuation)
		Assert.NotNull(cell0.Combiners);
		Assert.Contains("\uFE0F", cell0.Combiners);

		var cell1 = buffer.GetCell(1, 0);
		Assert.True(cell1.IsWideContinuation);
	}

	[Fact]
	public void WriteString_CombiningAccent_AttachesToPreviousCell()
	{
		var buffer = new CharacterBuffer(20, 5);

		// "e" + combining acute (U+0301) → "é" displayed, 1 column
		buffer.WriteString(0, 0, "e\u0301", Color.White, Color.Black);

		var cell0 = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('e'), cell0.Character);
		Assert.NotNull(cell0.Combiners);
		Assert.Contains("\u0301", cell0.Combiners);
	}

	[Fact]
	public void WriteString_ZeroWidthAfterWide_AttachesToWideChar()
	{
		var buffer = new CharacterBuffer(20, 5);

		// 中 (wide, 2 cols) + FE0F → FE0F attaches to continuation cell (col 1)
		// Actually: cx goes to 2 after wide, so prevX = 1 (continuation cell)
		// Let's test with narrow + FE0F first
		buffer.WriteString(0, 0, "A\uFE0F", Color.White, Color.Black);

		var cell0 = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('A'), cell0.Character);
		Assert.NotNull(cell0.Combiners);
	}

	[Fact]
	public void WriteStringClipped_WithZeroWidth_AttachesToPreviousCell()
	{
		var buffer = new CharacterBuffer(20, 5);
		var clipRect = new LayoutRect(0, 0, 10, 5);

		// ✈ (U+2708, narrow) + FE0F = 1 column + combiner
		buffer.WriteStringClipped(0, 0, "\u2708\uFE0F", Color.White, Color.Black, clipRect);

		var cell0 = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('\u2708'), cell0.Character);
		Assert.NotNull(cell0.Combiners);
		Assert.Contains("\uFE0F", cell0.Combiners);
	}

	[Fact]
	public void Clear_ResetsCombinersToNull()
	{
		var buffer = new CharacterBuffer(20, 5);

		// Use narrow char + combiner so we can check cell 0
		buffer.WriteString(0, 0, "\u2708\uFE0F", Color.White, Color.Black);
		Assert.NotNull(buffer.GetCell(0, 0).Combiners);

		buffer.Clear(Color.Black);

		Assert.Null(buffer.GetCell(0, 0).Combiners);
	}
}
