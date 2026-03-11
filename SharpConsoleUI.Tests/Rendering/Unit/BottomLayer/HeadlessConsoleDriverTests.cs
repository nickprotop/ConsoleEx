using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using System.Text;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Unit tests for HeadlessConsoleDriver's cell-level API (SetCell, FillCells, WriteBufferRegion).
/// Validates that the driver correctly delegates to ConsoleBuffer and that cells
/// arrive in the back buffer with correct characters and colors.
/// </summary>
public class HeadlessConsoleDriverTests
{
	private readonly ITestOutputHelper _output;

	public HeadlessConsoleDriverTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void SetCell_WritesCharacterToBackBuffer()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay(); // Initialize buffers

		// Act - write directly via driver
		system.ConsoleDriver.SetCell(10, 5, 'A', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
		Assert.Equal(new Rune('A'), snapshot.GetBack(10, 5).Character);
	}

	[Fact]
	public void SetCell_WritesCorrectColors()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();

		// Act - write two cells with different colors
		system.ConsoleDriver.SetCell(10, 5, 'R', Color.Red, Color.Black);
		system.ConsoleDriver.SetCell(11, 5, 'G', Color.Green, Color.Black);
		system.Render.UpdateDisplay();

		// Assert - cells should have different ANSI escapes (different fg colors)
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cellR = snapshot.GetBack(10, 5);
		var cellG = snapshot.GetBack(11, 5);
		Assert.Equal(new Rune('R'), cellR.Character);
		Assert.Equal(new Rune('G'), cellG.Character);
		Assert.NotEqual(cellR.AnsiEscape, cellG.AnsiEscape);

		// Red cell should contain red RGB values
		Assert.Contains("255;0;0", cellR.AnsiEscape);
		// Green cell should contain green RGB values
		Assert.Contains("0;128;0", cellG.AnsiEscape);
	}

	[Fact]
	public void SetCell_OutOfBounds_DoesNotThrow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem(); // 200x50

		// Act & Assert - should silently ignore out-of-bounds writes
		system.ConsoleDriver.SetCell(-1, 0, 'X', Color.White, Color.Black);
		system.ConsoleDriver.SetCell(0, -1, 'X', Color.White, Color.Black);
		system.ConsoleDriver.SetCell(200, 0, 'X', Color.White, Color.Black);
		system.ConsoleDriver.SetCell(0, 50, 'X', Color.White, Color.Black);
	}

	[Fact]
	public void SetCell_MarksCellDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay(); // Frame 1: sync buffers
		system.Render.UpdateDisplay(); // Frame 2: everything clean

		// Act - write a cell
		system.ConsoleDriver.SetCell(15, 8, 'Z', Color.Yellow, Color.Blue);
		system.Render.UpdateDisplay(); // Frame 3: should detect the change

		// Assert
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);
		Assert.True(metrics.CharactersChanged > 0);
		Assert.True(metrics.BytesWritten > 0);
	}

	[Fact]
	public void FillCells_FillsHorizontalRun()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();

		// Act - fill 10 cells with '#'
		system.ConsoleDriver.FillCells(5, 10, 10, '#', Color.Cyan, Color.DarkBlue);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		for (int x = 5; x < 15; x++)
		{
			var cell = snapshot.GetBack(x, 10);
			Assert.Equal(new Rune('#'), cell.Character);
			_output.WriteLine($"Cell ({x}, 10): '{cell.Character}' ANSI: {cell.AnsiEscape}");
		}

		// Cell before and after should NOT be '#'
		Assert.NotEqual(new Rune('#'), snapshot.GetBack(4, 10).Character);
		Assert.NotEqual(new Rune('#'), snapshot.GetBack(15, 10).Character);
	}

	[Fact]
	public void FillCells_AllCellsHaveSameColor()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();

		// Act
		system.ConsoleDriver.FillCells(0, 0, 20, '.', Color.White, Color.Red);
		system.Render.UpdateDisplay();

		// Assert - all filled cells should have identical ANSI escape
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var firstAnsi = snapshot.GetBack(0, 0).AnsiEscape;
		for (int x = 1; x < 20; x++)
		{
			Assert.Equal(firstAnsi, snapshot.GetBack(x, 0).AnsiEscape);
		}
	}

	[Fact]
	public void FillCells_ZeroWidth_DoesNothing()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay(); // Clean state

		// Act
		system.ConsoleDriver.FillCells(5, 5, 0, 'X', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert - no changes
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);
		Assert.Equal(0, metrics.CharactersChanged);
	}

	[Fact]
	public void FillCells_ClipsToBufferWidth()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem(); // 200x50

		// Act - try to fill 100 cells starting at x=150 (would exceed 200)
		system.ConsoleDriver.FillCells(150, 5, 100, '#', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert - should fill up to x=199 without throwing
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal(new Rune('#'), snapshot.GetBack(150, 5).Character);
		Assert.Equal(new Rune('#'), snapshot.GetBack(199, 5).Character);
	}

	[Fact]
	public void WriteBufferRegion_CopiesCellsFromCharacterBuffer()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();

		var sourceBuffer = new CharacterBuffer(10, 1, Color.Black);
		sourceBuffer.SetCell(0, 0, 'H', Color.White, Color.Black);
		sourceBuffer.SetCell(1, 0, 'E', Color.White, Color.Black);
		sourceBuffer.SetCell(2, 0, 'L', Color.White, Color.Black);
		sourceBuffer.SetCell(3, 0, 'L', Color.White, Color.Black);
		sourceBuffer.SetCell(4, 0, 'O', Color.White, Color.Black);

		// Act
		system.ConsoleDriver.WriteBufferRegion(20, 10, sourceBuffer, 0, 0, 5, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal(new Rune('H'), snapshot.GetBack(20, 10).Character);
		Assert.Equal(new Rune('E'), snapshot.GetBack(21, 10).Character);
		Assert.Equal(new Rune('L'), snapshot.GetBack(22, 10).Character);
		Assert.Equal(new Rune('L'), snapshot.GetBack(23, 10).Character);
		Assert.Equal(new Rune('O'), snapshot.GetBack(24, 10).Character);
	}

	[Fact]
	public void WriteBufferRegion_PreservesSourceColors()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();

		var sourceBuffer = new CharacterBuffer(5, 1, Color.Black);
		sourceBuffer.SetCell(0, 0, 'A', Color.Red, Color.Blue);
		sourceBuffer.SetCell(1, 0, 'B', Color.Green, Color.Yellow);

		// Act
		system.ConsoleDriver.WriteBufferRegion(30, 15, sourceBuffer, 0, 0, 2, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cellA = snapshot.GetBack(30, 15);
		var cellB = snapshot.GetBack(31, 15);

		Assert.Equal(new Rune('A'), cellA.Character);
		Assert.Equal(new Rune('B'), cellB.Character);
		// Different colors should produce different ANSI
		Assert.NotEqual(cellA.AnsiEscape, cellB.AnsiEscape);
	}

	[Fact]
	public void WriteBufferRegion_WithSrcXOffset_CopiesCorrectSlice()
	{
		// Arrange - this tests the clipping path used by border rendering
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();

		var sourceBuffer = new CharacterBuffer(10, 1, Color.Black);
		sourceBuffer.SetCell(0, 0, 'A', Color.White, Color.Black);
		sourceBuffer.SetCell(1, 0, 'B', Color.White, Color.Black);
		sourceBuffer.SetCell(2, 0, 'C', Color.White, Color.Black);
		sourceBuffer.SetCell(3, 0, 'D', Color.White, Color.Black);
		sourceBuffer.SetCell(4, 0, 'E', Color.White, Color.Black);

		// Act - copy starting from srcX=2 (should get 'C', 'D', 'E')
		system.ConsoleDriver.WriteBufferRegion(50, 20, sourceBuffer, 2, 0, 3, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal(new Rune('C'), snapshot.GetBack(50, 20).Character);
		Assert.Equal(new Rune('D'), snapshot.GetBack(51, 20).Character);
		Assert.Equal(new Rune('E'), snapshot.GetBack(52, 20).Character);
	}

	[Fact]
	public void WriteBufferRegion_OutOfSourceBounds_WritesPaddingSpaces()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();

		var sourceBuffer = new CharacterBuffer(3, 1, Color.Black);
		sourceBuffer.SetCell(0, 0, 'X', Color.White, Color.Black);
		sourceBuffer.SetCell(1, 0, 'Y', Color.White, Color.Black);
		sourceBuffer.SetCell(2, 0, 'Z', Color.White, Color.Black);

		// Act - request 5 cells but source only has 3
		system.ConsoleDriver.WriteBufferRegion(10, 10, sourceBuffer, 0, 0, 5, Color.DarkRed);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal(new Rune('X'), snapshot.GetBack(10, 10).Character);
		Assert.Equal(new Rune('Y'), snapshot.GetBack(11, 10).Character);
		Assert.Equal(new Rune('Z'), snapshot.GetBack(12, 10).Character);
		// Positions 3-4 should be padding spaces
		Assert.Equal(new Rune(' '), snapshot.GetBack(13, 10).Character);
		Assert.Equal(new Rune(' '), snapshot.GetBack(14, 10).Character);
	}

	[Fact]
	public void GetDirtyCharacterCount_ReflectsDriverWrites()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay(); // Frame 1
		system.Render.UpdateDisplay(); // Frame 2: clean state

		// Act - write 5 cells
		system.ConsoleDriver.SetCell(0, 0, 'A', Color.White, Color.Black);
		system.ConsoleDriver.SetCell(1, 0, 'B', Color.White, Color.Black);
		system.ConsoleDriver.SetCell(2, 0, 'C', Color.White, Color.Black);
		system.ConsoleDriver.SetCell(3, 0, 'D', Color.White, Color.Black);
		system.ConsoleDriver.SetCell(4, 0, 'E', Color.White, Color.Black);

		// Assert - driver should report dirty cells
		var dirtyCount = system.ConsoleDriver.GetDirtyCharacterCount();
		_output.WriteLine($"Dirty character count: {dirtyCount}");
		Assert.True(dirtyCount >= 5);
	}
}
