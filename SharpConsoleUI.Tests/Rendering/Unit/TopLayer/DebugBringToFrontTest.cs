using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class DebugBringToFrontTest
{
	private readonly ITestOutputHelper _output;

	public DebugBringToFrontTest(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Debug_BringToFront_TraceExecution()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window1 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Window 1"
		};
		window1.AddControl(new MarkupControl(new List<string>
		{
			"OOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOO"
		}));

		var window2 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Window 2"
		};
		window2.AddControl(new MarkupControl(new List<string>
		{
			"XXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXX"
		}));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		_output.WriteLine($"After adding windows:");
		_output.WriteLine($"  Window1 ZIndex: {window1.ZIndex}, IsDirty: {window1.IsDirty}");
		_output.WriteLine($"  Window1 bounds: Left={window1.Left}, Top={window1.Top}, Width={window1.Width}, Height={window1.Height}");
		_output.WriteLine($"  Window2 ZIndex: {window2.ZIndex}, IsDirty: {window2.IsDirty}");
		_output.WriteLine($"  Window2 bounds: Left={window2.Left}, Top={window2.Top}, Width={window2.Width}, Height={window2.Height}");

		system.Render.UpdateDisplay();

		_output.WriteLine($"After first render:");
		_output.WriteLine($"  Window1 ZIndex: {window1.ZIndex}, IsDirty: {window1.IsDirty}, IsActive: {window1.GetIsActive()}");
		_output.WriteLine($"  Window2 ZIndex: {window2.ZIndex}, IsDirty: {window2.IsDirty}, IsActive: {window2.GetIsActive()}");
		_output.WriteLine($"  ActiveWindow: {system.WindowStateService.ActiveWindow?.Title ?? "null"}");

		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		var char1 = snapshot1?.GetBack(15, 8).Character;
		_output.WriteLine($"  Character at (15,8): '{char1}' (expected 'X')");

		// Act - Bring window1 to front
		_output.WriteLine($"\nCalling BringToFront(window1)...");
		system.WindowStateService.BringToFront(window1);

		_output.WriteLine($"After BringToFront:");
		_output.WriteLine($"  Window1 ZIndex: {window1.ZIndex}, IsDirty: {window1.IsDirty}, IsActive: {window1.GetIsActive()}");
		_output.WriteLine($"  Window2 ZIndex: {window2.ZIndex}, IsDirty: {window2.IsDirty}, IsActive: {window2.GetIsActive()}");
		_output.WriteLine($"  ActiveWindow: {system.WindowStateService.ActiveWindow?.Title ?? "null"}");

		system.Render.UpdateDisplay();

		_output.WriteLine($"After second render:");
		_output.WriteLine($"  Window1 ZIndex: {window1.ZIndex}, IsDirty: {window1.IsDirty}, IsActive: {window1.GetIsActive()}");
		_output.WriteLine($"  Window2 ZIndex: {window2.ZIndex}, IsDirty: {window2.IsDirty}, IsActive: {window2.GetIsActive()}");
		_output.WriteLine($"  ActiveWindow: {system.WindowStateService.ActiveWindow?.Title ?? "null"}");

		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		var char2 = snapshot2?.GetBack(15, 8).Character;
		_output.WriteLine($"  Character at (15,8): '{char2}' (expected 'O')");

		Assert.Equal('O', char2);
	}
}
