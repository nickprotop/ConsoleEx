using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Tests for dirty cell tracking accuracy.
/// Validates that the system correctly identifies which cells need re-rendering
/// and avoids marking clean cells as dirty.
/// </summary>
public class DirtyTrackingTests
{
	private readonly ITestOutputHelper _output;

	public DirtyTrackingTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void DirtyTracking_UnchangedCells_NotMarkedDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Clean"
		};

		window.AddControl(new MarkupControl(new List<string> { "Static" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - render with no changes
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - no cells should be marked dirty
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var dirtyCount = snapshot.GetDirtyCount();
		_output.WriteLine($"Dirty cells: {dirtyCount}");

		Assert.Equal(0, dirtyCount);
	}

	[Fact]
	public void DirtyTracking_SingleCharChange_OnlyThatCellDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "ABCDEF" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Single"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change one character
		control.SetContent(new List<string> { "ABXDEF" }); // C → X
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - minimal dirty cells
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells marked: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells actually rendered: {metrics.CellsActuallyRendered}");

		// Should be small number
		Assert.True(metrics.DirtyCellsMarked <= 10);
		Assert.True(metrics.CellsActuallyRendered <= metrics.DirtyCellsMarked);
	}

	[Fact]
	public void DirtyTracking_LineChange_OnlyThatLineDirty()
	{
		// Arrange - CELL MODE (default)
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var lines = new List<string>
		{
			"Line 1",
			"Line 2",
			"Line 3",
			"Line 4",
			"Line 5"
		};
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 15,
			Title = "Line Change"
		};

		window.AddControl(new MarkupControl(lines));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change only line 3
		lines[2] = "CHANGED";
		window.AddControl(new MarkupControl(lines));
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - should not mark all lines dirty
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");

		// CELL mode: should mark reasonable number of cells, not entire window (600 cells = 30 width * 20 visible height)
		Assert.True(metrics.DirtyCellsMarked < 300); // Much less than full window
	}

	[Fact]
	public void DirtyTracking_LineChange_OnlyThatLineDirty_LineMode()
	{
		// Arrange - LINE MODE
		var system = TestWindowSystemBuilder.CreateTestSystemWithLineMode();
		var lines = new List<string>
		{
			"Line 1",
			"Line 2",
			"Line 3",
			"Line 4",
			"Line 5"
		};
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 15,
			Title = "Line Change"
		};

		window.AddControl(new MarkupControl(lines));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change only line 3
		lines[2] = "CHANGED";
		window.AddControl(new MarkupControl(lines));
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - LINE mode renders entire lines
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");

		// LINE mode: changes in one line, renders full line width (200 cells)
		Assert.True(metrics.DirtyCellsMarked < 300);
		Assert.True(metrics.CellsActuallyRendered >= 200); // At least one full line
	}

	[Fact]
	public void DirtyTracking_WindowMove_OldAndNewRegionsDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 20,
			Height = 8,
			Title = "Move"
		};

		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - move window
		window.Left = 30;
		window.Top = 15;
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - both old and new positions should be dirty
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");

		// Should include old position (to clear) + new position (to draw)
		// Window is 20x8 = 160 cells, so roughly 320 cells dirty (old + new)
		Assert.True(metrics.DirtyCellsMarked > 100);
	}

	[Fact]
	public void DirtyTracking_ColorChangeOnly_CellsMarkedDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "Text" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Color"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change color but keep same characters
		control.SetContent(new List<string> { "[red]Text[/]" });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - color change should mark cells dirty
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		Assert.True(metrics.DirtyCellsMarked > 0);
	}

	[Fact]
	public void DirtyTracking_OverlappingWindows_OccludedCellsNotDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Window 1 (behind)
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 30,
			Height = 15,
			Title = "Behind"
		};
		window1.AddControl(new MarkupControl(new List<string> { "Behind" }));

		// Window 2 (front, completely covers window1)
		var window2 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 30,
			Height = 15,
			Title = "Front"
		};
		window2.AddControl(new MarkupControl(new List<string> { "Front" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - update hidden window
		window1.AddControl(new MarkupControl(new List<string> { "Updated" }));
		window1.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - should produce minimal output since window1 is hidden
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// Behind window is completely occluded, so minimal output
		Assert.True(metrics.BytesWritten < 50);
	}

	[Fact]
	public void DirtyTracking_PartialOverlap_OnlyVisiblePortionDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Window 1 (behind)
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 40,
			Height = 20,
			Title = "Behind"
		};
		window1.AddControl(new MarkupControl(new List<string> { "Behind content" }));

		// Window 2 (front, partially overlaps)
		var window2 = new Window(system)
		{
			Left = 25,
			Top = 15,
			Width = 30,
			Height = 15,
			Title = "Front"
		};
		window2.AddControl(new MarkupControl(new List<string> { "Front content" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - update behind window
		window1.AddControl(new MarkupControl(new List<string> { "Updated behind" }));
		window1.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - only visible portion should be dirty
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// Some output (visible portion) but not full window
		Assert.True(metrics.BytesWritten > 0);
		Assert.True(metrics.BytesWritten < 500); // Less than full window
	}

	[Fact]
	public void DirtyTracking_EfficiencyRatio_GoodForSmallChanges()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "AAAAAAAAAA" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 15,
			Title = "Efficiency"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - small change
		control.SetContent(new List<string> { "AAAAABAAAA" });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - efficiency should be good
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Efficiency ratio: {metrics.EfficiencyRatio:P}");
		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Rendered cells: {metrics.CellsActuallyRendered}");

		// High efficiency means we're not over-invalidating
		Assert.True(metrics.EfficiencyRatio > 0.5);
	}

	[Fact]
	public void DirtyTracking_MultipleSmallChanges_AccurateCounts()
	{
		// Arrange - CELL MODE (default)
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 15,
			Title = "Multiple"
		};

		var lines = new List<string> { "AAAA", "BBBB", "CCCC", "DDDD" };
		window.AddControl(new MarkupControl(lines));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change multiple lines
		lines[0] = "AXAA"; // 1 char change
		lines[2] = "CXCC"; // 1 char change
		window.AddControl(new MarkupControl(lines));
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - should track both changes
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Characters changed: {metrics.CharactersChanged}");

		// 2 characters changed
		Assert.True(metrics.CharactersChanged >= 2);
		// CELL mode: tracks precise dirty cells (full window would be 600 = 40 width * 15 height)
		Assert.True(metrics.DirtyCellsMarked < 300); // Much less than full window
	}

	[Fact]
	public void DirtyTracking_MultipleSmallChanges_AccurateCounts_LineMode()
	{
		// Arrange - LINE MODE
		var system = TestWindowSystemBuilder.CreateTestSystemWithLineMode();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 15,
			Title = "Multiple"
		};

		var lines = new List<string> { "AAAA", "BBBB", "CCCC", "DDDD" };
		window.AddControl(new MarkupControl(lines));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change multiple lines
		lines[0] = "AXAA"; // 1 char change
		lines[2] = "CXCC"; // 1 char change
		window.AddControl(new MarkupControl(lines));
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - LINE mode renders full lines
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Characters changed: {metrics.CharactersChanged}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");

		// 2 characters changed across 2 lines
		Assert.True(metrics.CharactersChanged >= 2);
		// LINE mode: renders 2 full lines (2 * 200 = 400 cells)
		Assert.True(metrics.CellsActuallyRendered >= 400);
	}

	[Fact]
	public void DirtyTracking_WindowResize_AffectedRegionsDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Resize"
		};

		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - resize window
		window.SetSize(40, 15);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - resize affects dirty tracking
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		Assert.True(metrics.DirtyCellsMarked > 0);
	}

	[Fact]
	public void DirtyTracking_BackgroundColorChange_AllWindowCellsDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "BG Change",
			BackgroundColor = Color.White
		};

		window.AddControl(new MarkupControl(new List<string> { "Text" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change background color (affects entire window)
		window.BackgroundColor = Color.Blue;
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - background change affects entire window
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// Should be significant since entire window background changed
		Assert.True(metrics.DirtyCellsMarked > 50);
		Assert.True(metrics.BytesWritten > 100);
	}

	[Fact]
	public void DirtyTracking_AddNewWindow_OnlyNewWindowRegionDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "First"
		};

		window1.AddControl(new MarkupControl(new List<string> { "Window 1" }));
		system.WindowStateService.AddWindow(window1);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - add second window (non-overlapping)
		var window2 = new Window(system)
		{
			Left = 50,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Second"
		};

		window2.AddControl(new MarkupControl(new List<string> { "Window 2" }));
		system.WindowStateService.AddWindow(window2);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - only new window should be dirty
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");

		// Should be roughly the size of window2
		Assert.True(metrics.DirtyCellsMarked > 50);
	}

	[Fact]
	public void DirtyTracking_RemoveWindow_OldRegionMarkedForClearing()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Closing"
		};

		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - close window
		window.Close();
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - window area should be marked dirty for clearing
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		Assert.True(metrics.DirtyCellsMarked > 0);
		Assert.True(metrics.BytesWritten > 0);
	}

	[Fact]
	public void DirtyTracking_InvalidateWithoutChanges_StillOptimized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Invalidate"
		};

		window.AddControl(new MarkupControl(new List<string> { "Static" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - invalidate but no actual changes
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - even though invalidated, no actual output if content same
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// Should still produce zero output due to buffer comparison
		Assert.Equal(0, metrics.BytesWritten);
	}

	[Fact]
	public void DirtyTracking_ConsecutiveStaticFrames_AllZeroDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Static"
		};

		window.AddControl(new MarkupControl(new List<string> { "Static" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - render 5 static frames
		for (int i = 2; i <= 6; i++)
		{
			system.Render.UpdateDisplay();

			var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
			_output.WriteLine($"Frame {i}: {snapshot?.GetDirtyCount()} dirty cells");

			// Assert each frame has zero dirty cells
			Assert.Equal(0, snapshot?.GetDirtyCount());
		}
	}

	[Fact]
	public void DirtyTracking_ZOrderChange_OverlapRegionDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window1 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 30,
			Height = 15,
			Title = "Window 1"
		};
		window1.AddControl(new MarkupControl(new List<string> { "Window 1" }));

		var window2 = new Window(system)
		{
			Left = 20,
			Top = 12,
			Width = 30,
			Height = 15,
			Title = "Window 2"
		};
		window2.AddControl(new MarkupControl(new List<string> { "Window 2" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change z-order
		system.WindowStateService.BringToFront(window1);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - overlap region should be dirty
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		Assert.True(metrics.DirtyCellsMarked > 0);
		Assert.True(metrics.BytesWritten > 0);
	}
}
