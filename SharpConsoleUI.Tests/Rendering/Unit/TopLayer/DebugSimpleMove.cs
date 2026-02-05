using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class DebugSimpleMove
{
	private readonly ITestOutputHelper _output;

	public DebugSimpleMove(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Debug_SimpleMove_CheckVisibleRegions()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with 'B' characters
		var background = new Window(system)
		{
			Left = 5,
			Top = 3,
			Width = 40,
			Height = 15,
			Title = "Background"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
		}));

		// Moving window with 'A' characters
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Moving"
		};
		window.AddControl(new MarkupControl(new List<string>
		{
			"AAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAA"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);

		_output.WriteLine($"Background bounds: Left={background.Left}, Top={background.Top}, Width={background.Width}, Height={background.Height}");
		_output.WriteLine($"Moving bounds: Left={window.Left}, Top={window.Top}, Width={window.Width}, Height={window.Height}");

		system.Render.UpdateDisplay();

		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;

		// Check various positions
		_output.WriteLine($"\nAfter first render:");
		for (int x = 9; x <= 31; x += 2)
		{
			var c = snapshot1?.GetBack(x, 8).Character;
			_output.WriteLine($"  Char at ({x},8): '{c}' (expected: B if x<11, A if 11-27, B if x>27)");
		}

		// Move window
		_output.WriteLine($"\nMoving window to Left=15...");
		window.Left = 15;
		system.Render.UpdateDisplay();

		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;

		_output.WriteLine($"\nAfter move:");
		_output.WriteLine($"Moving bounds: Left={window.Left}, Top={window.Top}, Width={window.Width}, Height={window.Height}");
		_output.WriteLine($"Background bounds: Left={background.Left}, Top={background.Top}, Width={background.Width}, Height={background.Height}");
		_output.WriteLine($"Background IsDirty: {background.IsDirty} (should be False after render)");
		for (int x = 9; x <= 36; x += 2)
		{
			var c = snapshot2?.GetBack(x, 8).Character;
			var expected = x < 16 ? "B" : (x < 33 ? "A" : "B");
			_output.WriteLine($"  Char at ({x},8): '{c}' (expected: {expected})");
		}
	}
}
