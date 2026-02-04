// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for CharacterBuffer - the top layer of the rendering pipeline.
/// Validates cell operations, dirty tracking, and buffer manipulation.
/// </summary>
public class CharacterBufferTests
{
	[Fact]
	public void CharacterBuffer_Create_HasCorrectDimensions()
	{
		// Arrange & Act
		var buffer = new CharacterBuffer(80, 25);

		// Assert
		Assert.Equal(80, buffer.Width);
		Assert.Equal(25, buffer.Height);
	}

	[Fact]
	public void CharacterBuffer_SetCell_UpdatesCell()
	{
		// Arrange
		var buffer = new CharacterBuffer(10, 10);

		// Act
		buffer.SetCell(5, 5, 'X', Color.Red, Color.Blue);

		// Assert
		var cell = buffer.GetCell(5, 5);
		Assert.Equal('X', cell.Character);
		Assert.Equal(Color.Red, cell.Foreground);
		Assert.Equal(Color.Blue, cell.Background);
	}

	[Fact]
	public void CharacterBuffer_WriteString_WritesAllCharacters()
	{
		// Arrange
		var buffer = new CharacterBuffer(20, 10);
		var text = "Hello";

		// Act
		buffer.WriteString(5, 3, text, Color.White, Color.Black);

		// Assert
		Assert.Equal('H', buffer.GetCell(5, 3).Character);
		Assert.Equal('e', buffer.GetCell(6, 3).Character);
		Assert.Equal('l', buffer.GetCell(7, 3).Character);
		Assert.Equal('l', buffer.GetCell(8, 3).Character);
		Assert.Equal('o', buffer.GetCell(9, 3).Character);
	}

	[Fact]
	public void CharacterBuffer_WriteStringClipped_RespectsClipRect()
	{
		// Arrange
		var buffer = new CharacterBuffer(20, 10);
		var text = "Hello World";
		var clipRect = new LayoutRect(5, 3, 5, 1); // Only 5 chars wide

		// Act
		buffer.WriteStringClipped(5, 3, text, Color.White, Color.Black, clipRect);

		// Assert - Only "Hello" should be written (5 chars)
		Assert.Equal('H', buffer.GetCell(5, 3).Character);
		Assert.Equal('o', buffer.GetCell(9, 3).Character);
		// "World" should not be written
		Assert.NotEqual('W', buffer.GetCell(10, 3).Character);
	}

	[Fact]
	public void CharacterBuffer_FillRect_FillsEntireRegion()
	{
		// Arrange
		var buffer = new CharacterBuffer(20, 10);
		var rect = new LayoutRect(5, 3, 10, 4);

		// Act
		buffer.FillRect(rect, '#', Color.Yellow, Color.Green);

		// Assert - Check corners and middle
		Assert.Equal('#', buffer.GetCell(5, 3).Character); // Top-left
		Assert.Equal('#', buffer.GetCell(14, 6).Character); // Bottom-right
		Assert.Equal('#', buffer.GetCell(10, 5).Character); // Middle
		Assert.Equal(Color.Yellow, buffer.GetCell(10, 5).Foreground);
		Assert.Equal(Color.Green, buffer.GetCell(10, 5).Background);
	}

	[Fact]
	public void CharacterBuffer_Clear_ClearsAllCells()
	{
		// Arrange
		var buffer = new CharacterBuffer(10, 10);
		buffer.SetCell(5, 5, 'X', Color.Red, Color.Blue);

		// Act
		buffer.Clear(Color.Black);

		// Assert
		var cell = buffer.GetCell(5, 5);
		Assert.Equal(' ', cell.Character);
		Assert.Equal(Color.Black, cell.Background);
	}

	[Fact]
	public void CharacterBuffer_DrawHorizontalLine_DrawsLine()
	{
		// Arrange
		var buffer = new CharacterBuffer(20, 10);

		// Act
		buffer.DrawHorizontalLine(5, 5, 10, '─', Color.White, Color.Black);

		// Assert
		for (int x = 5; x < 15; x++)
		{
			Assert.Equal('─', buffer.GetCell(x, 5).Character);
		}
	}

	[Fact]
	public void CharacterBuffer_DrawVerticalLine_DrawsLine()
	{
		// Arrange
		var buffer = new CharacterBuffer(20, 10);

		// Act
		buffer.DrawVerticalLine(5, 2, 6, '│', Color.White, Color.Black);

		// Assert
		for (int y = 2; y < 8; y++)
		{
			Assert.Equal('│', buffer.GetCell(5, y).Character);
		}
	}

	[Fact]
	public void CharacterBuffer_DrawBox_DrawsCompleteBox()
	{
		// Arrange
		var buffer = new CharacterBuffer(20, 10);
		var rect = new LayoutRect(5, 3, 10, 5);
		var boxChars = BoxChars.Single;

		// Act
		buffer.DrawBox(rect, boxChars, Color.White, Color.Black);

		// Assert - Check corners
		Assert.Equal(boxChars.TopLeft, buffer.GetCell(5, 3).Character);
		Assert.Equal(boxChars.TopRight, buffer.GetCell(14, 3).Character);
		Assert.Equal(boxChars.BottomLeft, buffer.GetCell(5, 7).Character);
		Assert.Equal(boxChars.BottomRight, buffer.GetCell(14, 7).Character);

		// Check edges
		Assert.Equal(boxChars.Horizontal, buffer.GetCell(10, 3).Character); // Top
		Assert.Equal(boxChars.Horizontal, buffer.GetCell(10, 7).Character); // Bottom
		Assert.Equal(boxChars.Vertical, buffer.GetCell(5, 5).Character); // Left
		Assert.Equal(boxChars.Vertical, buffer.GetCell(14, 5).Character); // Right
	}

	[Fact]
	public void CharacterBuffer_GetDirtyCells_ReturnsModifiedCells()
	{
		// Arrange
		var buffer = new CharacterBuffer(10, 10);
		// Clear initial dirty state by rendering once
		buffer.ToLines(Color.White, Color.Black);

		// Act
		buffer.SetCell(3, 4, 'A', Color.Red, Color.Black);
		buffer.SetCell(7, 2, 'B', Color.Blue, Color.White);

		var dirtyCells = buffer.GetDirtyCells().ToList();

		// Assert - At minimum, the two cells we modified should be in the dirty list
		Assert.True(dirtyCells.Count >= 2);
		Assert.Contains(dirtyCells, c => c.X == 3 && c.Y == 4);
		Assert.Contains(dirtyCells, c => c.X == 7 && c.Y == 2);
	}

	[Fact]
	public void CharacterBuffer_GetChanges_ReturnsAllChanges()
	{
		// Arrange
		var buffer = new CharacterBuffer(10, 10);
		// Clear initial dirty state by rendering once
		buffer.ToLines(Color.White, Color.Black);

		// Act
		buffer.SetCell(3, 4, 'A', Color.Red, Color.Black);
		buffer.SetCell(7, 2, 'B', Color.Blue, Color.White);

		var changes = buffer.GetChanges().ToList();

		// Assert - Should include our modifications
		Assert.True(changes.Count >= 2);
		var change1 = changes.First(c => c.X == 3 && c.Y == 4);
		Assert.Equal('A', change1.Cell.Character);
		Assert.Equal(Color.Red, change1.Cell.Foreground);
	}

	[Fact]
	public void CharacterBuffer_Resize_PreservesContent()
	{
		// Arrange
		var buffer = new CharacterBuffer(10, 10);
		buffer.SetCell(5, 5, 'X', Color.Red, Color.Blue);

		// Act
		buffer.Resize(20, 20);

		// Assert
		Assert.Equal(20, buffer.Width);
		Assert.Equal(20, buffer.Height);
		// Content should still be there
		var cell = buffer.GetCell(5, 5);
		Assert.Equal('X', cell.Character);
		Assert.Equal(Color.Red, cell.Foreground);
	}

	[Fact]
	public void CharacterBuffer_Resize_TruncatesWhenShrinking()
	{
		// Arrange
		var buffer = new CharacterBuffer(20, 20);
		buffer.SetCell(15, 15, 'X', Color.Red, Color.Blue);

		// Act - Shrink to 10x10
		buffer.Resize(10, 10);

		// Assert
		Assert.Equal(10, buffer.Width);
		Assert.Equal(10, buffer.Height);
		// Cell at (15,15) should no longer be accessible
	}

	[Fact]
	public void CharacterBuffer_ToLines_GeneratesAnsiOutput()
	{
		// Arrange
		var buffer = new CharacterBuffer(10, 3);
		buffer.WriteString(0, 0, "Red", Color.Red, Color.Black);
		buffer.WriteString(0, 1, "Blue", Color.Blue, Color.Black);

		// Act
		var lines = buffer.ToLines(Color.White, Color.Black);

		// Assert
		Assert.Equal(3, lines.Count);
		Assert.Contains("Red", lines[0]);
		Assert.Contains("Blue", lines[1]);
		// Lines should contain ANSI escape sequences
		Assert.Contains("\x1b[", lines[0]); // ANSI escape start
	}

	[Fact]
	public void CharacterBuffer_ClearRect_ClearsOnlySpecifiedRegion()
	{
		// Arrange
		var buffer = new CharacterBuffer(20, 10);
		buffer.FillRect(new LayoutRect(0, 0, 20, 10), 'X', Color.White, Color.Black);

		// Act - Clear only a small region
		buffer.ClearRect(new LayoutRect(5, 3, 5, 3), Color.Red);

		// Assert
		// Inside cleared region
		Assert.Equal(' ', buffer.GetCell(7, 5).Character);
		Assert.Equal(Color.Red, buffer.GetCell(7, 5).Background);

		// Outside cleared region (should still have X)
		Assert.Equal('X', buffer.GetCell(2, 2).Character);
		Assert.Equal('X', buffer.GetCell(15, 7).Character);
	}
}
