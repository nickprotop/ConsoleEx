using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class DebugVisibleRegions
{
	private readonly ITestOutputHelper _output;

	public DebugVisibleRegions(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Debug_VisibleRegionsCalculation_DuringSimultaneousUpdate()
	{
		// This test investigates the visible regions calculation when:
		// 1. Background window is dirty (updating)
		// 2. Moving window moves upward
		// 3. We want to verify the line ABOVE the moving window renders correctly

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

		// Moving window with 'M' characters
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

		_output.WriteLine($"INITIAL STATE:");
		_output.WriteLine($"  Background: Left={background.Left}, Top={background.Top}, Width={background.Width}, Height={background.Height}");
		_output.WriteLine($"  Background covers: y={background.Top}-{background.Top + background.Height - 1}");
		_output.WriteLine($"  Background content rows: y={background.Top + 1}-{background.Top + 12}");
		_output.WriteLine($"  Moving: Left={moving.Left}, Top={moving.Top}, Width={moving.Width}, Height={moving.Height}");
		_output.WriteLine($"  Moving covers: y={moving.Top}-{moving.Top + moving.Height - 1}");

		system.Render.UpdateDisplay();

		// CRITICAL: Now make BOTH dirty simultaneously
		_output.WriteLine($"\nSIMULTANEOUS UPDATE:");
		_output.WriteLine($"  1. Invalidating background (simulating content update)...");
		background.Invalidate(true);

		_output.WriteLine($"  2. Moving window from Top={moving.Top} to Top=8...");
		int oldTop = moving.Top;
		moving.Top = 8;

		_output.WriteLine($"\nBEFORE UpdateDisplay:");
		_output.WriteLine($"  Background: IsDirty={background.IsDirty}, ZIndex={background.ZIndex}");
		_output.WriteLine($"  Moving: IsDirty={moving.IsDirty}, ZIndex={moving.ZIndex}");
		_output.WriteLine($"  Moving old bounds: y={oldTop}-{oldTop + moving.Height - 1}");
		_output.WriteLine($"  Moving new bounds: y={moving.Top}-{moving.Top + moving.Height - 1}");
		_output.WriteLine($"  Line to check (above new moving): y={moving.Top - 1}");

		// Calculate what visible regions SHOULD be for background
		_output.WriteLine($"\nEXPECTED VISIBLE REGIONS FOR BACKGROUND:");
		_output.WriteLine($"  Background bounds: Left={background.Left}, Top={background.Top}, Width={background.Width}, Height={background.Height}");
		_output.WriteLine($"  Moving (overlapping): Left={moving.Left}, Top={moving.Top}, Width={moving.Width}, Height={moving.Height}");
		_output.WriteLine($"  Expected regions after subtracting moving:");
		_output.WriteLine($"    - Top strip: Left={background.Left}, Top={background.Top}, Width={background.Width}, Height={moving.Top - background.Top} (y={background.Top}-{moving.Top - 1})");
		_output.WriteLine($"    - Bottom strip: Left={background.Left}, Top={moving.Top + moving.Height}, Width={background.Width}, Height={background.Top + background.Height - (moving.Top + moving.Height)}");
		_output.WriteLine($"    - Left strip: Left={background.Left}, Top={moving.Top}, Width={moving.Left - background.Left}, Height={moving.Height}");
		_output.WriteLine($"    - Right strip: Left={moving.Left + moving.Width}, Top={moving.Top}, Width={background.Left + background.Width - (moving.Left + moving.Width)}, Height={moving.Height}");

		// Now render and check
		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;

		_output.WriteLine($"\nAFTER UpdateDisplay:");
		_output.WriteLine($"  Background: IsDirty={background.IsDirty}");
		_output.WriteLine($"  Moving: IsDirty={moving.IsDirty}");

		// Check the critical line ABOVE moving window
		int checkY = moving.Top - 1;  // Should be y=7
		int checkX = 30;  // Middle of moving window's x-range

		var charAbove = snapshot?.GetBack(checkX, checkY).Character;
		_output.WriteLine($"\n  CRITICAL CHECK - Line above moving window:");
		_output.WriteLine($"    Position: ({checkX},{checkY})");
		_output.WriteLine($"    Character: '{charAbove}'");
		_output.WriteLine($"    Expected: 'B' (background content)");
		_output.WriteLine($"    In background bounds? {checkY >= background.Top && checkY < background.Top + background.Height}");
		_output.WriteLine($"    In background content area? {checkY >= background.Top + 1 && checkY <= background.Top + 12}");
		_output.WriteLine($"    In moving bounds? {checkY >= moving.Top && checkY < moving.Top + moving.Height}");

		// Also check a position in the left strip
		int leftStripX = 15;  // Should be in left strip (x=10-19)
		int leftStripY = 10;  // Should be in left strip (y=8-17)
		var charLeftStrip = snapshot?.GetBack(leftStripX, leftStripY).Character;
		_output.WriteLine($"\n  LEFT STRIP CHECK:");
		_output.WriteLine($"    Position: ({leftStripX},{leftStripY})");
		_output.WriteLine($"    Character: '{charLeftStrip}'");
		_output.WriteLine($"    Expected: 'B' (background content visible in left strip)");

		// Check a position definitely covered by moving window
		int coveredX = 30;
		int coveredY = 10;
		var charCovered = snapshot?.GetBack(coveredX, coveredY).Character;
		_output.WriteLine($"\n  COVERED AREA CHECK:");
		_output.WriteLine($"    Position: ({coveredX},{coveredY})");
		_output.WriteLine($"    Character: '{charCovered}'");
		_output.WriteLine($"    Expected: 'M' or ' ' (moving window content/background)");

		Assert.Equal('B', charAbove);
	}

	[Fact]
	public void Debug_VisibleRegions_HorizontalMovement_ForComparison()
	{
		// Compare with horizontal movement to see if the issue is specific to vertical movement
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 20,
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
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
		}));

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

		_output.WriteLine($"INITIAL STATE (HORIZONTAL TEST):");
		_output.WriteLine($"  Moving: Left={moving.Left}, Top={moving.Top}");

		// SIMULTANEOUS: Background updates AND moving window moves horizontally
		background.Invalidate(true);
		moving.Left = 35;

		_output.WriteLine($"\nAFTER HORIZONTAL MOVE:");
		_output.WriteLine($"  Moving: Left={moving.Left}, Top={moving.Top}");

		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;

		// Check exposed area on the left
		int exposedX = 25;
		int exposedY = 10;
		var charExposed = snapshot?.GetBack(exposedX, exposedY).Character;

		_output.WriteLine($"\nEXPOSED AREA (LEFT OF NEW POSITION):");
		_output.WriteLine($"  Position: ({exposedX},{exposedY})");
		_output.WriteLine($"  Character: '{charExposed}'");
		_output.WriteLine($"  Expected: 'B'");

		Assert.Equal('B', charExposed);
	}
}
