using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Targeted tests for ConsoleBuffer.FillCells covering edge cases:
/// clipping, boundary conditions, and overwrite behavior.
/// These complement the integration-level tests in ConsoleBufferTests.
/// </summary>
public class FillCellsTests
{
	private readonly ITestOutputHelper _output;

	public FillCellsTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void FillCells_NegativeX_IsIgnored()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay(); // Clean

		// Act
		system.ConsoleDriver.FillCells(-5, 0, 10, '#', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert - should not crash, no cells should be modified
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);
		Assert.Equal(0, metrics.CharactersChanged);
	}

	[Fact]
	public void FillCells_NegativeY_IsIgnored()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Act
		system.ConsoleDriver.FillCells(0, -1, 10, '#', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);
		Assert.Equal(0, metrics.CharactersChanged);
	}

	[Fact]
	public void FillCells_NegativeWidth_IsIgnored()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Act
		system.ConsoleDriver.FillCells(5, 5, -3, '#', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);
		Assert.Equal(0, metrics.CharactersChanged);
	}

	[Fact]
	public void FillCells_ExactBufferWidth_FillsEntireLine()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem(); // 200x50

		// Act - fill entire line
		system.ConsoleDriver.FillCells(0, 25, 200, '=', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('=', snapshot.GetBack(0, 25).Character);
		Assert.Equal('=', snapshot.GetBack(99, 25).Character);
		Assert.Equal('=', snapshot.GetBack(199, 25).Character);
	}

	[Fact]
	public void FillCells_ExceedsBufferWidth_ClipsToEdge()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem(); // 200x50

		// Act - request 500 cells starting at x=0
		system.ConsoleDriver.FillCells(0, 0, 500, '*', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert - should fill all 200 cells without error
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('*', snapshot.GetBack(0, 0).Character);
		Assert.Equal('*', snapshot.GetBack(199, 0).Character);
	}

	[Fact]
	public void FillCells_StartNearEndOfBuffer_ClipsCorrectly()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem(); // 200x50

		// Act - start at x=195, request 20 cells (should only fill 5)
		system.ConsoleDriver.FillCells(195, 0, 20, '!', Color.Yellow, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		for (int x = 195; x < 200; x++)
		{
			Assert.Equal('!', snapshot.GetBack(x, 0).Character);
		}

		// Cell before fill start should NOT be modified
		Assert.NotEqual('!', snapshot.GetBack(194, 0).Character);
	}

	[Fact]
	public void FillCells_SingleCell_WorksLikeSetCell()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Render.UpdateDisplay();

		// Act - fill width=1 should behave like SetCell
		system.ConsoleDriver.FillCells(50, 25, 1, 'Q', Color.Magenta1, Color.Grey);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('Q', snapshot.GetBack(50, 25).Character);
		// Adjacent cells unaffected
		Assert.NotEqual('Q', snapshot.GetBack(49, 25).Character);
		Assert.NotEqual('Q', snapshot.GetBack(51, 25).Character);
	}

	[Fact]
	public void FillCells_Overwrite_PreviousContentReplaced()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// First fill
		system.ConsoleDriver.FillCells(10, 5, 10, 'A', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Act - overwrite with different character and color
		system.ConsoleDriver.FillCells(10, 5, 10, 'B', Color.Red, Color.Blue);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		for (int x = 10; x < 20; x++)
		{
			Assert.Equal('B', snapshot.GetBack(x, 5).Character);
		}
	}

	[Fact]
	public void FillCells_PartialOverwrite_OnlyOverwrittenCellsChange()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Fill 10 cells with 'A'
		system.ConsoleDriver.FillCells(10, 5, 10, 'A', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Act - overwrite middle 4 cells with 'B'
		system.ConsoleDriver.FillCells(13, 5, 4, 'B', Color.Red, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('A', snapshot.GetBack(10, 5).Character);
		Assert.Equal('A', snapshot.GetBack(12, 5).Character);
		Assert.Equal('B', snapshot.GetBack(13, 5).Character);
		Assert.Equal('B', snapshot.GetBack(16, 5).Character);
		Assert.Equal('A', snapshot.GetBack(17, 5).Character);
		Assert.Equal('A', snapshot.GetBack(19, 5).Character);
	}

	[Fact]
	public void FillCells_SameContentTwice_SecondCallProducesZeroOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// First fill and render
		system.ConsoleDriver.FillCells(10, 5, 20, '#', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Act - fill exact same content again
		system.ConsoleDriver.FillCells(10, 5, 20, '#', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert - no changes should be detected (double-buffer optimization)
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Characters changed: {metrics.CharactersChanged}");
		Assert.Equal(0, metrics.CharactersChanged);
		Assert.Equal(0, metrics.BytesWritten);
	}

	[Fact]
	public void FillCells_LastRow_WorksCorrectly()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem(); // 200x50

		// Act - fill on the very last row
		system.ConsoleDriver.FillCells(0, 49, 50, '-', Color.White, Color.Black);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('-', snapshot.GetBack(0, 49).Character);
		Assert.Equal('-', snapshot.GetBack(49, 49).Character);
	}
}
