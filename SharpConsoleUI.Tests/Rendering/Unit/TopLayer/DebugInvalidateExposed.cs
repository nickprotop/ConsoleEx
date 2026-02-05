using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class DebugInvalidateExposed
{
	private readonly ITestOutputHelper _output;

	public DebugInvalidateExposed(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Debug_InvalidateExposedRegions_TracksBackgroundInvalidation()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with '1' characters
		var background = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 50,
			Height = 20,
			Title = "Background"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"11111111111111111111111111111111111111111111111",
			"11111111111111111111111111111111111111111111111",
			"11111111111111111111111111111111111111111111111"
		}));

		// Moving window with '2' characters
		var moving = new Window(system)
		{
			Left = 35,
			Top = 10,
			Width = 20,
			Height = 8,
			Title = "Moving"
		};
		moving.AddControl(new MarkupControl(new List<string>
		{
			"22222222222222222",
			"22222222222222222",
			"22222222222222222",
			"22222222222222222"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);

		_output.WriteLine($"After adding windows:");
		_output.WriteLine($"  Background ZIndex: {background.ZIndex}, IsDirty: {background.IsDirty}");
		_output.WriteLine($"  Moving ZIndex: {moving.ZIndex}, IsDirty: {moving.IsDirty}");

		system.Render.UpdateDisplay();

		_output.WriteLine($"\nAfter first render:");
		_output.WriteLine($"  Background ZIndex: {background.ZIndex}, IsDirty: {background.IsDirty}");
		_output.WriteLine($"  Moving ZIndex: {moving.ZIndex}, IsDirty: {moving.IsDirty}");

		// Verify moving window covers position (40, 11) - in moving window content
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		var char1 = snapshot1?.GetBack(40, 11).Character;
		_output.WriteLine($"  Character at (40,11): '{char1}' (expected '2')");

		// Act - Move window to the left, exposing right area
		_output.WriteLine($"\nMoving window.Left from 35 to 15...");
		moving.Left = 15;

		_output.WriteLine($"After moving.Left = 15 (before UpdateDisplay):");
		_output.WriteLine($"  Background IsDirty: {background.IsDirty} (should be True if InvalidateExposedRegions worked)");
		_output.WriteLine($"  Moving IsDirty: {moving.IsDirty}");

		system.Render.UpdateDisplay();

		_output.WriteLine($"\nAfter second render:");
		_output.WriteLine($"  Background IsDirty: {background.IsDirty}");
		_output.WriteLine($"  Moving IsDirty: {moving.IsDirty}");

		// Assert - CRITICAL: Exposed area shows background content (y=7 is second content row)
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		var char2 = snapshot2?.GetBack(40, 7).Character;
		_output.WriteLine($"  Character at (40,7): '{char2}' (expected '1')");

		Assert.Equal('1', char2);
	}
}
