using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Tests for Smart adaptive dirty tracking mode.
/// Validates that Smart mode chooses optimal rendering strategy per line.
/// </summary>
public class SmartModeTests
{
	private readonly ITestOutputHelper _output;

	public SmartModeTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void SmartMode_DefaultBehavior_AdaptsToContent()
	{
		// Verify that default test system (which uses Smart mode) adapts behavior
		// This test verifies Smart mode is working by checking adaptive behavior
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Small change should result in minimal output (CELL mode behavior)
		var control = new MarkupControl(new List<string> { "AAAA" + new string(' ', 196) });
		var window = new Window(system) { Left = 10, Top = 5, Width = 200, Height = 10 };
		window.AddControl(control);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		control.SetContent(new List<string> { "BBBB" + new string(' ', 196) });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		// Smart mode should use CELL mode for small changes (< 50 cells rendered)
		Assert.True(metrics.CellsActuallyRendered < 50,
			$"Smart mode should use CELL mode for small changes, got {metrics.CellsActuallyRendered}");
	}

	[Fact]
	public void SmartMode_SmallChange_UsesCellMode()
	{
		// Arrange - Small change (8 cells = 4% coverage)
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "AAAAAAAA" + new string(' ', 192) });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 200,
			Height = 10,
			Title = "Smart Test"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay(); // Frame 1

		// Act - Change 8 characters (should use CELL mode)
		control.SetContent(new List<string> { "BBBBBBBB" + new string(' ', 192) });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - Should render minimal cells (CELL mode behavior)
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");

		// 8 cells dirty, should render ~10-20 cells (CELL mode with small overhead)
		Assert.Equal(8, metrics.DirtyCellsMarked);
		Assert.True(metrics.CellsActuallyRendered < 50,
			$"Expected CELL mode (~10-20 cells), got {metrics.CellsActuallyRendered}");
	}

	[Fact]
	public void SmartMode_HighCoverage_UsesLineMode()
	{
		// Arrange - High coverage change (150 cells = 75% coverage)
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var content = new string('A', 150) + new string(' ', 50);
		var control = new MarkupControl(new List<string> { content });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 200,
			Height = 10,
			Title = "Smart Test"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay(); // Frame 1

		// Act - Change 150 characters (75% coverage → should use LINE mode)
		var newContent = new string('B', 150) + new string(' ', 50);
		control.SetContent(new List<string> { newContent });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - Should render entire line (LINE mode behavior)
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");
		_output.WriteLine($"Coverage: {150.0/200:P}");

		// 150 cells dirty (75% coverage > 60% threshold), should use LINE mode (200 cells)
		Assert.Equal(150, metrics.DirtyCellsMarked);
		Assert.Equal(200, metrics.CellsActuallyRendered);
	}

	[Fact]
	public void SmartMode_HighlyFragmented_UsesLineMode()
	{
		// Arrange - 6 separate regions (exceeds fragmentation threshold of 5)
		var system = TestWindowSystemBuilder.CreateTestSystem();
		// Pattern: "A____B____C____D____E____F____" (6 single chars separated by spaces)
		var content = "A" + new string(' ', 20) +
		              "B" + new string(' ', 20) +
		              "C" + new string(' ', 20) +
		              "D" + new string(' ', 20) +
		              "E" + new string(' ', 20) +
		              "F" + new string(' ', 73);  // Fill to 200
		var control = new MarkupControl(new List<string> { content });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 200,
			Height = 10,
			Title = "Smart Test"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay(); // Frame 1

		// Act - Change all 6 characters (6 regions > threshold of 5)
		var newContent = "X" + new string(' ', 20) +
		                 "X" + new string(' ', 20) +
		                 "X" + new string(' ', 20) +
		                 "X" + new string(' ', 20) +
		                 "X" + new string(' ', 20) +
		                 "X" + new string(' ', 73);
		control.SetContent(new List<string> { newContent });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - Should use LINE mode (too fragmented)
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");

		// 6 cells dirty (6 regions > 5 threshold), should use LINE mode
		Assert.Equal(6, metrics.DirtyCellsMarked);
		Assert.Equal(200, metrics.CellsActuallyRendered);
	}

	[Fact]
	public void SmartMode_MediumFragmentation_UsesCellMode()
	{
		// Arrange - 3 separate regions (below fragmentation threshold of 5)
		var system = TestWindowSystemBuilder.CreateTestSystem();
		// Pattern: "AAAA____BBBB____CCCC" (3 regions, low coverage)
		var content = "AAAA" + new string(' ', 20) +
		              "BBBB" + new string(' ', 20) +
		              "CCCC" + new string(' ', 148);  // Fill to 200
		var control = new MarkupControl(new List<string> { content });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 200,
			Height = 10,
			Title = "Smart Test"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay(); // Frame 1

		// Act - Change all 3 regions (12 cells total, 6% coverage, 3 regions)
		var newContent = "XXXX" + new string(' ', 20) +
		                 "YYYY" + new string(' ', 20) +
		                 "ZZZZ" + new string(' ', 148);
		control.SetContent(new List<string> { newContent });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - Should use CELL mode (3 regions < 5, 6% coverage < 60%)
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");

		// 12 cells dirty, should render ~15-20 cells (CELL mode)
		Assert.Equal(12, metrics.DirtyCellsMarked);
		Assert.True(metrics.CellsActuallyRendered >= 12);
		Assert.True(metrics.CellsActuallyRendered < 50,
			$"Expected CELL mode (~12-20 cells), got {metrics.CellsActuallyRendered}");
	}

	[Fact]
	public void SmartMode_ThresholdBoundary_60Percent()
	{
		// Test the exact 60% threshold boundary
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// 120 cells = exactly 60% of 200
		var content = new string('A', 120) + new string(' ', 80);
		var control = new MarkupControl(new List<string> { content });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 200,
			Height = 10,
			Title = "Smart Test"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay(); // Frame 1

		// Act - Change exactly 120 characters (60% coverage)
		var newContent = new string('B', 120) + new string(' ', 80);
		control.SetContent(new List<string> { newContent });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - At exactly 60%, should still use CELL mode (threshold is ">60%", not ">=60%")
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");
		_output.WriteLine($"Coverage: {120.0/200:P}");

		Assert.Equal(120, metrics.DirtyCellsMarked);
		// At exactly 60%, uses CELL mode (condition is >0.6, not >=0.6)
		Assert.True(metrics.CellsActuallyRendered < 200);
	}

	[Fact]
	public void SmartMode_FullLineChange_UsesLineMode()
	{
		// Arrange - Full line change (content area may be less than window width due to borders)
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var content = new string('A', 200);
		var control = new MarkupControl(new List<string> { content });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 200,
			Height = 10,
			Title = "Smart Test"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay(); // Frame 1

		// Act - Change entire line
		var newContent = new string('B', 200);
		control.SetContent(new List<string> { newContent });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - Full line dirty → should use LINE mode
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");

		// All cells in content area dirty, should use LINE mode
		// (actual count may be less than 200 due to borders, but should be high)
		Assert.True(metrics.DirtyCellsMarked >= 180,
			$"Expected nearly full line dirty, got {metrics.DirtyCellsMarked}");
		Assert.True(metrics.CellsActuallyRendered >= 180,
			$"Expected LINE mode (full line render), got {metrics.CellsActuallyRendered}");
	}

	[Fact]
	public void SmartMode_MixedLinesInSameFrame()
	{
		// Most important test: different lines use different strategies in the same frame
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string>
		{
			"AAAA" + new string(' ', 196),           // Line 0: 4 cells (will change to sparse)
			new string('B', 150) + new string(' ', 50), // Line 1: 150 cells (will change to high coverage)
			"CCCC" + new string(' ', 196)            // Line 2: 4 cells (will stay sparse)
		});
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 200,
			Height = 10,
			Title = "Smart Test"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay(); // Frame 1

		// Act - Different change patterns
		control.SetContent(new List<string>
		{
			"XXXX" + new string(' ', 196),              // Line 0: 4 cells changed → CELL mode
			new string('Y', 150) + new string(' ', 50),    // Line 1: 150 cells changed (75%) → LINE mode
			"ZZZZ" + new string(' ', 196)               // Line 2: 4 cells changed → CELL mode
		});
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");

		// Total dirty: 4 + 150 + 4 = 158 cells
		Assert.Equal(158, metrics.DirtyCellsMarked);

		// Expected output:
		// Line 0: ~4-10 cells (CELL mode)
		// Line 1: 200 cells (LINE mode due to 75% coverage)
		// Line 2: ~4-10 cells (CELL mode)
		// Total: ~208-220 cells
		Assert.True(metrics.CellsActuallyRendered >= 200,
			$"Should include LINE render (200 cells), got {metrics.CellsActuallyRendered}");
		Assert.True(metrics.CellsActuallyRendered < 250,
			$"Should use CELL for sparse lines, got {metrics.CellsActuallyRendered}");
	}
}
