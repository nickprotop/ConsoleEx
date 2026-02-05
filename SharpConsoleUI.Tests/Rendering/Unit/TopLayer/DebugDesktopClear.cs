using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class DebugDesktopClear
{
	private readonly ITestOutputHelper _output;

	public DebugDesktopClear(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Debug_DesktopClearDoesNotAffectLineAbove()
	{
		// This test verifies that desktop clears for the old window position
		// do NOT incorrectly clear areas outside the old bounds (like the line above)

		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with 'B' characters
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

		// Small moving window
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
		_output.WriteLine($"  Background covers: y={background.Top}-{background.Top + background.Height - 1}");
		_output.WriteLine($"  Moving covers: y={moving.Top}-{moving.Top + moving.Height - 1}");

		// Check y=7 before move (should be 'B')
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		var char7Before = snapshot1?.GetBack(30, 7).Character;
		_output.WriteLine($"  Before move: char at (30,7) = '{char7Before}' (expected 'B')");

		// Move without invalidating background
		_output.WriteLine($"\nMOVE WITHOUT BACKGROUND UPDATE:");
		_output.WriteLine($"  Moving window from Top={moving.Top} to Top=8");
		_output.WriteLine($"  Old bounds to be cleared: Left={moving.Left}, Top={moving.Top}, Width={moving.Width}, Height={moving.Height}");
		_output.WriteLine($"  Old bounds covers: y={moving.Top}-{moving.Top + moving.Height - 1}");
		_output.WriteLine($"  Desktop clear should clear y=12-21, NOT y=7!");

		moving.Top = 8;
		system.Render.UpdateDisplay();

		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		var char7After = snapshot2?.GetBack(30, 7).Character;
		_output.WriteLine($"\nAFTER MOVE:");
		_output.WriteLine($"  Moving now at: Top={moving.Top}, covers y={moving.Top}-{moving.Top + moving.Height - 1}");
		_output.WriteLine($"  Char at (30,7) = '{char7After}' (expected 'B' - unchanged)");
		_output.WriteLine($"  y=7 is above the new moving window (which is at y=8-17)");
		_output.WriteLine($"  y=7 was also above the old moving window (which was at y=12-21)");
		_output.WriteLine($"  Therefore, y=7 should show background content 'B' unchanged!");

		Assert.Equal('B', char7After);
	}

	[Fact]
	public void Debug_SimultaneousUpdate_CheckRenderOrder()
	{
		// This test checks if the render order is correct when both windows are dirty

		var system = TestWindowSystemBuilder.CreateTestSystem();

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

		_output.WriteLine($"SIMULTANEOUS UPDATE TEST:");
		_output.WriteLine($"  Z-order: Background (Z={background.ZIndex}), Moving (Z={moving.ZIndex})");

		// BOTH dirty at the same time
		background.Invalidate(true);
		moving.Top = 8;

		_output.WriteLine($"\nBefore render:");
		_output.WriteLine($"  Background: IsDirty={background.IsDirty}");
		_output.WriteLine($"  Moving: IsDirty={moving.IsDirty}");

		_output.WriteLine($"\nExpected render sequence:");
		_output.WriteLine($"  1. Desktop clear: old moving position y=12-21");
		_output.WriteLine($"  2. Render background (lower Z) with visible regions (excluding y=8-17)");
		_output.WriteLine($"     - Should render y=5-7 (top strip) with 'B'");
		_output.WriteLine($"     - Should render y=18-29 (bottom strip) with 'B'");
		_output.WriteLine($"     - Should render x=10-19, y=8-17 (left strip) with 'B'");
		_output.WriteLine($"     - Should render x=45-49, y=8-17 (right strip) with 'B'");
		_output.WriteLine($"  3. Render moving (higher Z) at y=8-17");

		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;

		// Check various positions
		_output.WriteLine($"\nRESULTS:");

		// Position above moving window (should be background)
		var charAbove = snapshot?.GetBack(30, 7).Character;
		_output.WriteLine($"  Above moving (30,7): '{charAbove}' (expected 'B')");

		// Position in left strip (should be background)
		var charLeft = snapshot?.GetBack(15, 10).Character;
		_output.WriteLine($"  Left strip (15,10): '{charLeft}' (expected 'B')");

		// Position below moving window (should be desktop background from clear)
		var charBelow = snapshot?.GetBack(30, 20).Character;
		_output.WriteLine($"  Below moving (30,20): '{charBelow}' (expected ' ' from desktop clear)");

		// Position in exposed bottom area (below the desktop clear)
		var charExposed = snapshot?.GetBack(30, 22).Character;
		_output.WriteLine($"  Exposed bottom (30,22): '{charExposed}' (expected 'B' if in background bounds)");

		Assert.Equal('B', charAbove);
		Assert.Equal('B', charLeft);
	}
}
