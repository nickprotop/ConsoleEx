using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for the direct cell rendering path (EnsureContentReady → CharacterBuffer → SetCellsFromBuffer).
/// Validates that cells flow correctly from the window buffer to the screen buffer
/// without ANSI serialization.
/// </summary>
public class DirectCellPathTests
{
	[Fact]
	public void EnsureContentReady_ReturnsBuffer_WithCorrectDimensions()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 30, Height = 10 };
		window.AddControl(new MarkupControl(new List<string> { "Hello" }));

		// Act
		var buffer = window.EnsureContentReady();

		// Assert - buffer dimensions should be content area (width-2, height-2)
		Assert.NotNull(buffer);
		Assert.Equal(28, buffer.Width);
		Assert.Equal(8, buffer.Height);
	}

	[Fact]
	public void EnsureContentReady_BufferContainsCorrectCharacters()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 30, Height = 10 };
		window.AddControl(new MarkupControl(new List<string> { "ABCDE" }));

		// Act
		var buffer = window.EnsureContentReady();

		// Assert - first row should contain "ABCDE" followed by spaces
		Assert.NotNull(buffer);
		Assert.Equal('A', buffer.GetCell(0, 0).Character);
		Assert.Equal('B', buffer.GetCell(1, 0).Character);
		Assert.Equal('C', buffer.GetCell(2, 0).Character);
		Assert.Equal('D', buffer.GetCell(3, 0).Character);
		Assert.Equal('E', buffer.GetCell(4, 0).Character);
		Assert.Equal(' ', buffer.GetCell(5, 0).Character);
	}

	[Fact]
	public void EnsureContentReady_ReturnsNull_WhenMinimized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 30, Height = 10 };
		window.AddControl(new MarkupControl(new List<string> { "Hello" }));
		window.State = WindowState.Minimized;

		// Act
		var buffer = window.EnsureContentReady();

		// Assert
		Assert.Null(buffer);
	}

	[Fact]
	public void EnsureContentReady_RebuildOnInvalidation()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 30, Height = 10 };
		window.AddControl(new MarkupControl(new List<string> { "First" }));

		var buffer1 = window.EnsureContentReady();
		Assert.NotNull(buffer1);
		Assert.Equal('F', buffer1.GetCell(0, 0).Character);

		// Act - add new content and invalidate
		window.ClearControls();
		window.AddControl(new MarkupControl(new List<string> { "Second" }));
		window.Invalidate(true);
		var buffer2 = window.EnsureContentReady();

		// Assert - buffer should have new content
		Assert.NotNull(buffer2);
		Assert.Equal('S', buffer2.GetCell(0, 0).Character);
	}

	[Fact]
	public void DirectCellPath_CellsFlowToConsoleBuffer()
	{
		// Arrange - full pipeline test
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 3,
			Width = 20,
			Height = 8,
			Title = "Test"
		};
		window.AddControl(new MarkupControl(new List<string> { "XYZ" }));
		system.WindowStateService.AddWindow(window);

		// Act - run the full rendering pipeline
		system.Render.UpdateDisplay();

		// Assert - verify cells arrived in the console buffer snapshot
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Content starts at (Left+1, Top+1) in screen space (+DesktopUpperLeft.Y offset)
		// With title bar, content starts at row Top+1 (border) + DesktopUpperLeft.Y
		int contentX = 6; // Left + 1 (border)
		int contentY = 4 + system.DesktopUpperLeft.Y; // Top + 1 (border) + desktop offset
		Assert.Equal('X', snapshot.GetBack(contentX, contentY).Character);
		Assert.Equal('Y', snapshot.GetBack(contentX + 1, contentY).Character);
		Assert.Equal('Z', snapshot.GetBack(contentX + 2, contentY).Character);
	}

	[Fact]
	public void DirectCellPath_PreservesColors()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 3,
			Width = 30,
			Height = 8,
			Title = "Colors"
		};
		window.AddControl(new MarkupControl(new List<string> { "[red]R[/][green]G[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - verify cells have correct characters in the console buffer
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		int contentX = 6;
		int contentY = 4 + system.DesktopUpperLeft.Y;
		Assert.Equal('R', snapshot.GetBack(contentX, contentY).Character);
		Assert.Equal('G', snapshot.GetBack(contentX + 1, contentY).Character);

		// Verify ANSI escape strings contain different color codes (red vs green)
		var cellR = snapshot.GetBack(contentX, contentY);
		var cellG = snapshot.GetBack(contentX + 1, contentY);
		Assert.NotEqual(cellR.AnsiEscape, cellG.AnsiEscape);
	}

	[Fact]
	public void ContentLineCount_ReturnsBufferHeight()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 30, Height = 10 };
		window.AddControl(new MarkupControl(new List<string> { "Line 1", "Line 2", "Line 3" }));

		// Act - trigger buffer rebuild
		window.EnsureContentReady();

		// Assert - ContentLineCount should match buffer height (Height - 2 for borders)
		Assert.Equal(8, window.ContentLineCount);
	}

	[Fact]
	public void RenderAndGetVisibleContent_UsesBufferPath()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 30, Height = 10 };
		window.AddControl(new MarkupControl(new List<string> { "Hello World" }));

		// Act - call the legacy API which should now use EnsureContentReady internally
		var lines = window.RenderAndGetVisibleContent();

		// Assert - should still return ANSI-formatted lines
		Assert.NotNull(lines);
		Assert.Equal(8, lines.Count); // Height - 2 for borders
		Assert.Contains("\x1b[", string.Join("", lines)); // Should contain ANSI codes

		// Buffer should also be populated
		var buffer = window.ContentBuffer;
		Assert.NotNull(buffer);
		Assert.Equal('H', buffer.GetCell(0, 0).Character);
	}
}
