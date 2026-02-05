using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Tests for both Line-level and Cell-level dirty tracking modes.
/// Validates that Cell mode produces minimal output for small changes.
/// </summary>
public class DirtyTrackingModeTests
{
	private readonly ITestOutputHelper _output;

	public DirtyTrackingModeTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void LineMode_SingleCharChange_RendersEntireLine()
	{
		// Arrange - LINE MODE
		var system = TestWindowSystemBuilder.CreateTestSystemWithLineMode();
		var control = new MarkupControl(new List<string> { "ABCDEF" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Line Mode"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change one character
		control.SetContent(new List<string> { "ABXDEF" }); // C → X
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - Line mode renders entire line (200 cells wide)
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Mode: Line");
		_output.WriteLine($"Dirty cells marked: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells actually rendered: {metrics.CellsActuallyRendered}");

		// Line mode: even 1 char change renders entire line (200 cells)
		Assert.Equal(1, metrics.DirtyCellsMarked); // Only 1 cell differs
		Assert.Equal(200, metrics.CellsActuallyRendered); // But renders entire line
	}

	[Fact]
	public void CellMode_SingleCharChange_RendersOnlyChangedRegion()
	{
		// Arrange - CELL MODE
		var system = TestWindowSystemBuilder.CreateTestSystemWithCellMode();
		var control = new MarkupControl(new List<string> { "ABCDEF" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Cell Mode"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change one character
		control.SetContent(new List<string> { "ABXDEF" }); // C → X
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - Cell mode renders only changed region (~1 cell)
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Mode: Cell");
		_output.WriteLine($"Dirty cells marked: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells actually rendered: {metrics.CellsActuallyRendered}");

		// Cell mode: minimal output for minimal change
		Assert.Equal(1, metrics.DirtyCellsMarked); // Only 1 cell differs
		Assert.True(metrics.CellsActuallyRendered <= 10); // Renders only region (~1 cell + margin)
	}

	[Fact]
	public void CellMode_NoChanges_ZeroOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystemWithCellMode();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Static"
		};

		window.AddControl(new MarkupControl(new List<string> { "Static content" }));
		system.WindowStateService.AddWindow(window);

		// Act - Frame 1 then Frame 2 with no changes
		system.Render.UpdateDisplay(); // Frame 1
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - Zero output for no changes (works in both modes)
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.Equal(0, metrics.BytesWritten);
		Assert.Equal(0, metrics.CharactersChanged);
	}

	[Fact]
	public void CellMode_MultipleRegions_RendersEachRegion()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystemWithCellMode();
		var control = new MarkupControl(new List<string> { "AAAA____BBBB____CCCC" }); // 20 chars
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Multi-Region"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change 3 separate regions: AAAA, BBBB, CCCC → XXXX, YYYY, ZZZZ
		control.SetContent(new List<string> { "XXXX____YYYY____ZZZZ" });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - Should render 3 regions (12 cells total)
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells marked: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells actually rendered: {metrics.CellsActuallyRendered}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// 12 cells changed (3 regions * 4 chars each)
		Assert.Equal(12, metrics.DirtyCellsMarked);
		// Cell mode renders only those 12 cells (plus small overhead for cursor positioning)
		Assert.True(metrics.CellsActuallyRendered >= 12);
		Assert.True(metrics.CellsActuallyRendered <= 20); // Some overhead for positioning
	}

	[Fact]
	public void BothModes_SameContentResult_DifferentPerformance()
	{
		// This test validates that both modes produce correct output
		// but Cell mode is more efficient for small changes

		// Line Mode
		var lineSystem = TestWindowSystemBuilder.CreateTestSystemWithLineMode();
		var lineControl = new MarkupControl(new List<string> { "TEST" });
		var lineWindow = new Window(lineSystem) { Left = 10, Top = 5, Width = 30, Height = 10, Title = "Line" };
		lineWindow.AddControl(lineControl);
		lineSystem.WindowStateService.AddWindow(lineWindow);
		lineSystem.Render.UpdateDisplay();
		lineControl.SetContent(new List<string> { "BEST" });
		lineWindow.Invalidate(true);
		lineSystem.Render.UpdateDisplay();
		var lineMetrics = lineSystem.RenderingDiagnostics?.LastMetrics;

		// Cell Mode
		var cellSystem = TestWindowSystemBuilder.CreateTestSystemWithCellMode();
		var cellControl = new MarkupControl(new List<string> { "TEST" });
		var cellWindow = new Window(cellSystem) { Left = 10, Top = 5, Width = 30, Height = 10, Title = "Cell" };
		cellWindow.AddControl(cellControl);
		cellSystem.WindowStateService.AddWindow(cellWindow);
		cellSystem.Render.UpdateDisplay();
		cellControl.SetContent(new List<string> { "BEST" });
		cellWindow.Invalidate(true);
		cellSystem.Render.UpdateDisplay();
		var cellMetrics = cellSystem.RenderingDiagnostics?.LastMetrics;

		// Assert - Cell mode is more efficient
		_output.WriteLine($"Line Mode - Cells rendered: {lineMetrics?.CellsActuallyRendered}, Bytes: {lineMetrics?.BytesWritten}");
		_output.WriteLine($"Cell Mode - Cells rendered: {cellMetrics?.CellsActuallyRendered}, Bytes: {cellMetrics?.BytesWritten}");

		Assert.NotNull(lineMetrics);
		Assert.NotNull(cellMetrics);

		// Cell mode should render significantly fewer cells
		Assert.True(cellMetrics.CellsActuallyRendered < lineMetrics.CellsActuallyRendered);
		// Cell mode should output less data
		Assert.True(cellMetrics.BytesWritten < lineMetrics.BytesWritten);
	}
}
