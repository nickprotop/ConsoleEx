using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class DebugSimultaneousUpdates
{
	private readonly ITestOutputHelper _output;

	public DebugSimultaneousUpdates(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Debug_BackgroundUpdating_WhileWindowMoving()
	{
		// This reproduces the user's described scenario:
		// "the underlying window is making updates while the over window is moving over it"

		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with 'B' characters
		var background = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 20,
			Title = "Background"
		};
		var bgControl = new MarkupControl(new List<string>
		{
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
		});
		background.AddControl(bgControl);

		// Moving window with 'M' characters
		var moving = new Window(system)
		{
			Left = 20,
			Top = 8,
			Width = 25,
			Height = 10,
			Title = "Moving"
		};
		moving.AddControl(new MarkupControl(new List<string>
		{
			"MMMMMMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMMMMMM"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);
		system.Render.UpdateDisplay();

		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		_output.WriteLine($"INITIAL STATE:");
		_output.WriteLine($"  Background: IsDirty={background.IsDirty}");
		_output.WriteLine($"  Moving: IsDirty={moving.IsDirty}");
		_output.WriteLine($"  Char at (25,10): '{snapshot1?.GetBack(25, 10).Character}' (should be 'M')");

		// CRITICAL: Make background window dirty (simulating it updating content)
		// AND move the moving window at the same time
		_output.WriteLine($"\nSIMULTANEOUS UPDATE:");
		_output.WriteLine($"  1. Invalidating background (simulating content update)...");
		background.Invalidate(true);

		_output.WriteLine($"  2. Moving window to the right...");
		moving.Left = 35;

		_output.WriteLine($"\nBEFORE UpdateDisplay:");
		_output.WriteLine($"  Background: IsDirty={background.IsDirty} (should be True - was updated)");
		_output.WriteLine($"  Moving: IsDirty={moving.IsDirty} (should be True - was moved)");

		system.Render.UpdateDisplay();

		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		_output.WriteLine($"\nAFTER UpdateDisplay:");
		_output.WriteLine($"  Background: IsDirty={background.IsDirty} (should be False after render)");
		_output.WriteLine($"  Moving: IsDirty={moving.IsDirty} (should be False after render)");

		// Check exposed area on the left (was covered by moving, now exposed)
		_output.WriteLine($"\nCHECKING EXPOSED AREA:");
		_output.WriteLine($"  Old moving position: x=20-44");
		_output.WriteLine($"  New moving position: x=35-59");
		_output.WriteLine($"  Exposed area: x=20-34");

		// Check position (25, 10) - was covered by moving window, now should show background
		var exposedChar = snapshot2?.GetBack(25, 10).Character;
		_output.WriteLine($"  Position (25,10) in exposed area:");
		_output.WriteLine($"    Character: '{exposedChar}'");
		_output.WriteLine($"    Expected: 'B' (background content)");
		_output.WriteLine($"    Background content area: x={background.Left + 1}-{background.Left + 38}, y={background.Top + 1}-{background.Top + 8}");
		_output.WriteLine($"    Position (25,10) in background content? x:{25 >= background.Left + 1 && 25 <= background.Left + 38}, y:{10 >= background.Top + 1 && 10 <= background.Top + 8}");

		// USER'S BUG: "the line above the moving window, if overlaps other window, is drawn with spaces!"
		// Check line just above the moving window's NEW position
		_output.WriteLine($"\n  Line ABOVE new moving position (y={moving.Top - 1}):");
		var lineAbove = snapshot2?.GetBack(40, moving.Top - 1).Character;
		_output.WriteLine($"    Character at (40,{moving.Top - 1}): '{lineAbove}'");
		_output.WriteLine($"    Expected: 'B' (background content)");

		Assert.Equal('B', exposedChar);
	}

	[Fact]
	public void Debug_BackgroundUpdating_WhileWindowMovingVertically()
	{
		// Test VERTICAL movement with simultaneous background update
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window
		var background = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 25,
			Title = "Background"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
		}));

		// Moving window
		var moving = new Window(system)
		{
			Left = 20,
			Top = 12,
			Width = 25,
			Height = 10,
			Title = "Moving"
		};
		moving.AddControl(new MarkupControl(new List<string>
		{
			"MMMMMMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMMMMMM"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);
		system.Render.UpdateDisplay();

		_output.WriteLine($"INITIAL STATE:");
		_output.WriteLine($"  Moving: Top={moving.Top}, Height={moving.Height}, covers y={moving.Top}-{moving.Top + moving.Height - 1}");

		// SIMULTANEOUS: Background updates AND moving window moves upward
		_output.WriteLine($"\nSIMULTANEOUS UPDATE:");
		background.Invalidate(true);
		_output.WriteLine($"  1. Background invalidated");

		moving.Top = 8;
		_output.WriteLine($"  2. Moving moved UP: Top={moving.Top}, covers y={moving.Top}-{moving.Top + moving.Height - 1}");

		_output.WriteLine($"\nBEFORE UpdateDisplay:");
		_output.WriteLine($"  Background: IsDirty={background.IsDirty}");
		_output.WriteLine($"  Moving: IsDirty={moving.IsDirty}");

		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		_output.WriteLine($"\nAFTER UpdateDisplay:");
		_output.WriteLine($"  Background: IsDirty={background.IsDirty}");
		_output.WriteLine($"  Moving: IsDirty={moving.IsDirty}");

		// Check exposed bottom area (old position y=12-21, new position y=8-17, exposed y=18-21)
		_output.WriteLine($"\nCHECKING EXPOSED BOTTOM AREA:");
		_output.WriteLine($"  Old position: y=12-21");
		_output.WriteLine($"  New position: y=8-17");
		_output.WriteLine($"  Exposed: y=18-21");

		// USER'S BUG FIX VERIFICATION: Check line ABOVE the new moving window position
		// This was showing ' ' instead of 'B' before the fix
		var charAbove = snapshot?.GetBack(30, moving.Top - 1).Character;
		_output.WriteLine($"\n  Line ABOVE new moving position y={moving.Top - 1}:");
		_output.WriteLine($"    Character: '{charAbove}' (expected 'B' - THIS WAS THE BUG!)");

		Assert.Equal('B', charAbove);
	}
}
