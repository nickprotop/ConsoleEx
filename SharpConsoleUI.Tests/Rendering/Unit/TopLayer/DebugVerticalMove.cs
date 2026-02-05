using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class DebugVerticalMove
{
	private readonly ITestOutputHelper _output;

	public DebugVerticalMove(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Debug_VerticalMove_CheckBoundaries()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with 'B' characters - tall enough to see boundary issues
		var background = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 25,
			Title = "Background"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBB"
		}));

		// Moving window with 'M' characters
		var moving = new Window(system)
		{
			Left = 15,
			Top = 10,
			Width = 20,
			Height = 12,
			Title = "Moving"
		};
		moving.AddControl(new MarkupControl(new List<string>
		{
			"MMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMM"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);

		_output.WriteLine($"Background: Top={background.Top}, ContentTop={background.Top+1}, Height={background.Height}");
		_output.WriteLine($"  Content rows: y={background.Top+1} to y={background.Top+1+9}");
		_output.WriteLine($"Moving BEFORE: Top={moving.Top}, ContentTop={moving.Top+1}, Height={moving.Height}");
		_output.WriteLine($"  Content rows: y={moving.Top+1} to y={moving.Top+1+4}");

		system.Render.UpdateDisplay();

		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;

		// Check position (20, 14) - should be in moving window content
		_output.WriteLine($"\nBEFORE move - checking (20, 14):");
		_output.WriteLine($"  Moving covers y={moving.Top} to y={moving.Top+moving.Height-1}");
		_output.WriteLine($"  y=14 is in moving content (y={moving.Top+1} to y={moving.Top+1+4})? {14 >= moving.Top+1 && 14 <= moving.Top+1+4}");
		var char1 = snapshot1?.GetBack(20, 14).Character;
		_output.WriteLine($"  Char at (20,14): '{char1}' (expected 'M')");

		// Move window upward
		_output.WriteLine($"\nMoving window from Top={moving.Top} to Top=8...");
		moving.Top = 8;

		_output.WriteLine($"Moving AFTER: Top={moving.Top}, ContentTop={moving.Top+1}");
		_output.WriteLine($"  Content rows: y={moving.Top+1} to y={moving.Top+1+4}");
		_output.WriteLine($"  Background IsDirty: {background.IsDirty} (should be True before UpdateDisplay)");

		system.Render.UpdateDisplay();

		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;

		_output.WriteLine($"\nAFTER move - checking boundary areas:");
		_output.WriteLine($"  Moving now covers y={moving.Top} to y={moving.Top+moving.Height-1}");
		_output.WriteLine($"  Background IsDirty: {background.IsDirty} (should be False after render)");

		// Check multiple y positions in the exposed area at the BOTTOM of old position
		// Old position was y=10-21, new position is y=8-19, so exposed area is y=20-21
		_output.WriteLine($"\n  Checking exposed bottom area (y=20-21):");
		for (int y = 20; y <= 21; y++)
		{
			var c = snapshot2?.GetBack(20, y).Character;
			var inBgContent = y >= background.Top + 1 && y <= background.Top + 1 + 14; // 15 rows
			_output.WriteLine($"    y={y}: '{c}' (in background content: {inBgContent}, expected: {(inBgContent ? "B" : " ")})");
		}

		// Check the boundary line just ABOVE the new moving window position
		// This is the "line above" that user mentioned might have issues
		_output.WriteLine($"\n  Checking line just ABOVE new moving position (y={moving.Top - 1}):");
		var charAbove = snapshot2?.GetBack(20, moving.Top - 1).Character;
		var inBgContentAbove = (moving.Top - 1) >= background.Top + 1 && (moving.Top - 1) <= background.Top + 1 + 14;
		_output.WriteLine($"    y={moving.Top - 1}: '{charAbove}' (in background content: {inBgContentAbove}, expected: {(inBgContentAbove ? "B" : " ")})");

		// Check the boundary line just BELOW the new moving window position
		_output.WriteLine($"\n  Checking line just BELOW new moving position (y={moving.Top + moving.Height}):");
		var charBelow = snapshot2?.GetBack(20, moving.Top + moving.Height).Character;
		var inBgContentBelow = (moving.Top + moving.Height) >= background.Top + 1 && (moving.Top + moving.Height) <= background.Top + 1 + 14;
		_output.WriteLine($"    y={moving.Top + moving.Height}: '{charBelow}' (in background content: {inBgContentBelow}, expected: {(inBgContentBelow ? "B" : " ")})");

		// Assert on exposed area that should show background
		Assert.Equal('B', snapshot2?.GetBack(20, 20).Character);
	}
}
