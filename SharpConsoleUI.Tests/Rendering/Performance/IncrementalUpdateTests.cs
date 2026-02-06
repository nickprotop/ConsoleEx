using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Performance;

/// <summary>
/// Tests that incremental updates produce minimal output.
/// Small changes should only output the changed regions, not entire windows.
/// </summary>
public class IncrementalUpdateTests
{
	private readonly ITestOutputHelper _output;

	public IncrementalUpdateTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void SingleCharacterChange_MinimalOutput()
	{
		// Changing 1 character should output < 50 bytes

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "AAAA" });
		var window = new Window(system) { Left = 10, Top = 5, Width = 40, Height = 10 };
		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		// Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Change 1 character
		control.SetContent(new List<string> { "ABAA" });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"1 char change: {metrics.BytesWritten} bytes, {metrics.CharactersChanged} chars");

		// Assert - Minimal output (Smart mode should use CELL mode)
		Assert.Equal(1, metrics.CharactersChanged);
		Assert.True(metrics.BytesWritten < 50,
			$"Expected <50 bytes for 1 char change, got {metrics.BytesWritten}");
	}

	[Fact]
	public void SmallTextChange_MinimalOutput()
	{
		// Changing a small word should output < 100 bytes

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "Hello World" });
		var window = new Window(system) { Left = 10, Top = 5, Width = 50, Height = 15 };
		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		// Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Change "World" to "There"
		control.SetContent(new List<string> { "Hello There" });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Small text change: {metrics.BytesWritten} bytes, {metrics.CharactersChanged} chars");

		// Assert - Minimal output
		Assert.True(metrics.CharactersChanged >= 5); // "There" = 5 chars
		Assert.True(metrics.BytesWritten < 100,
			$"Expected <100 bytes for small word change, got {metrics.BytesWritten}");
	}

	[Fact]
	public void StatusBarUpdate_MinimalOutput()
	{
		// Simulates typical status bar update (time, status text)
		// Should be very efficient (<50 bytes for ~8 char change)

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "Status: 12:34:56" });
		var window = new Window(system) { Left = 0, Top = 0, Width = 80, Height = 3 };
		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		// Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Update time (8 chars changed)
		control.SetContent(new List<string> { "Status: 12:34:57" });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Status update: {metrics.BytesWritten} bytes, {metrics.CharactersChanged} chars");

		// Assert - Very efficient for status bar updates
		Assert.True(metrics.CharactersChanged <= 8);
		Assert.True(metrics.BytesWritten < 50,
			$"Expected <50 bytes for status update, got {metrics.BytesWritten}");
	}

	[Fact]
	public void CursorBlink_MinimalOutput()
	{
		// Cursor blink (2 cells: old position + new position) should be very efficient

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "Text_here" });
		var window = new Window(system) { Left = 10, Top = 5, Width = 40, Height = 10 };
		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		// Frame 1: Initial render with cursor at position 4
		system.Render.UpdateDisplay();

		// Act - Frame 2: Move cursor (change 2 cells)
		control.SetContent(new List<string> { "Text here" }); // Remove underscore, simulates cursor move
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Cursor blink: {metrics.BytesWritten} bytes, {metrics.CharactersChanged} chars");

		// Assert - Very minimal for cursor updates
		Assert.True(metrics.CharactersChanged <= 2);
		Assert.True(metrics.BytesWritten < 50,
			$"Expected <50 bytes for cursor update, got {metrics.BytesWritten}");
	}

	[Fact]
	public void MultiLineTextUpdate_ReasonableOutput()
	{
		// Updating multiple lines of actual text content should be efficient

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string>
		{
			"Line 1: Original",
			"Line 2: Original",
			"Line 3: Unchanged"
		});
		var window = new Window(system) { Left = 10, Top = 5, Width = 50, Height = 15 };
		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		// Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Update first two lines
		control.SetContent(new List<string>
		{
			"Line 1: Modified",
			"Line 2: Modified",
			"Line 3: Unchanged"
		});
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Multi-line update: {metrics.BytesWritten} bytes, {metrics.CharactersChanged} chars");

		// Assert - Should update the changed characters efficiently
		// "Original" -> "Modified" = 8 characters changed per line × 2 lines = 16 chars minimum
		Assert.True(metrics.CharactersChanged >= 16,
			$"Expected >= 16 chars changed (2 lines), got {metrics.CharactersChanged}");
		Assert.True(metrics.BytesWritten < 300,
			$"Expected <300 bytes for 2-line update, got {metrics.BytesWritten}");
	}

	[Fact]
	public void MultipleSmallChanges_CumulativeMinimal()
	{
		// Multiple small changes across different windows should be efficient

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window1 = new Window(system) { Left = 10, Top = 5, Width = 30, Height = 8 };
		var control1 = new MarkupControl(new List<string> { "Window 1: A" });
		window1.AddControl(control1);

		var window2 = new Window(system) { Left = 50, Top = 10, Width = 30, Height = 8 };
		var control2 = new MarkupControl(new List<string> { "Window 2: B" });
		window2.AddControl(control2);

		var window3 = new Window(system) { Left = 90, Top = 15, Width = 30, Height = 8 };
		var control3 = new MarkupControl(new List<string> { "Window 3: C" });
		window3.AddControl(control3);

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.AddWindow(window3);

		// Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Change 1 character in each window (3 total)
		control1.SetContent(new List<string> { "Window 1: X" });
		window1.Invalidate(true);

		control2.SetContent(new List<string> { "Window 2: Y" });
		window2.Invalidate(true);

		control3.SetContent(new List<string> { "Window 3: Z" });
		window3.Invalidate(true);

		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"3 small changes: {metrics.BytesWritten} bytes, {metrics.CharactersChanged} chars");

		// Assert - Efficient cumulative output
		Assert.Equal(3, metrics.CharactersChanged);
		Assert.True(metrics.BytesWritten < 150,
			$"Expected <150 bytes for 3 small changes, got {metrics.BytesWritten}");
	}

	[Fact]
	public void ProgressBarUpdate_ReasonableOutput()
	{
		// Progress bar update (one line changing ~10 chars) should be efficient

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "[green]████████[/]          [50%]" });
		var window = new Window(system) { Left = 10, Top = 5, Width = 50, Height = 10 };
		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		// Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Update progress
		control.SetContent(new List<string> { "[green]██████████[/]        [60%]" });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Progress update: {metrics.BytesWritten} bytes, {metrics.CharactersChanged} chars");

		// Assert - Reasonable output for progress bar
		Assert.True(metrics.CharactersChanged >= 3); // Efficient rendering - only cells that actually differ
		Assert.True(metrics.BytesWritten < 200,
			$"Expected <200 bytes for progress update, got {metrics.BytesWritten}");
	}

	[Fact]
	public void ColorOnlyChange_DetectedAndRendered()
	{
		// Changing only colors (not text) should still trigger rendering

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string>
		{
			"[white on black]ABC[/]",  // White on black
			"[blue]DEF[/]",             // Blue text
			"GHI"                       // Default colors
		});
		var window = new Window(system) { Left = 10, Top = 5, Width = 50, Height = 15 };
		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		// Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Change only colors, same text
		control.SetContent(new List<string>
		{
			"[black on white]ABC[/]",  // FG & BG swapped
			"[red]DEF[/]",              // FG changed only
			"[on yellow]GHI[/]"         // BG changed only
		});
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Color-only change: {metrics.BytesWritten} bytes, {metrics.CharactersChanged} chars");

		// Assert - Color changes should be detected (characters stay same, but ANSI codes change)
		// All 9 characters (ABC + DEF + GHI) have color changes
		Assert.True(metrics.CharactersChanged >= 9,
			$"Expected >= 9 chars changed (all had color changes), got {metrics.CharactersChanged}");
		Assert.True(metrics.BytesWritten > 0,
			$"Expected output for color changes, got {metrics.BytesWritten} bytes");
		Assert.True(metrics.BytesWritten < 500,
			$"Expected <500 bytes for color-only changes, got {metrics.BytesWritten}");
	}

	[Fact]
	public void PartialLineUpdate_OnlyChangedRegion()
	{
		// Update in middle of line should only output that region

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var longLine = "The quick brown fox jumps over the lazy dog";
		var control = new MarkupControl(new List<string> { longLine });
		var window = new Window(system) { Left = 10, Top = 5, Width = 60, Height = 10 };
		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		// Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Change "brown" to "BLACK" (middle of line)
		var updatedLine = "The quick BLACK fox jumps over the lazy dog";
		control.SetContent(new List<string> { updatedLine });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Partial line update: {metrics.BytesWritten} bytes, {metrics.CharactersChanged} chars");

		// Assert - Only changed region output (Smart mode should use CELL mode)
		Assert.Equal(5, metrics.CharactersChanged); // "BLACK" = 5 chars
		Assert.True(metrics.BytesWritten < 100,
			$"Expected <100 bytes for partial line, got {metrics.BytesWritten}");
	}
}
