using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Tests for double-buffering optimization mechanism.
/// Validates that the system correctly implements front/back buffer swapping
/// and only outputs differences between frames.
/// </summary>
public class DoubleBufferingTests
{
	private readonly ITestOutputHelper _output;

	public DoubleBufferingTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void DoubleBuffering_StaticContent_ProducesZeroOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 15,
			Title = "Static"
		};

		window.AddControl(new MarkupControl(new List<string> { "Static content" }));
		system.WindowStateService.AddWindow(window);

		// Frame 1 - initial render
		system.Render.UpdateDisplay();
		var frame1 = system.RenderingDiagnostics?.LastMetrics;
		Assert.True(frame1?.BytesWritten > 100); // Initial render outputs everything

		// Act - Frame 2 with no changes
		system.Render.UpdateDisplay();
		var frame2 = system.RenderingDiagnostics?.LastMetrics;

		// Assert - CRITICAL: Zero output required!
		_output.WriteLine($"Frame 1: {frame1?.BytesWritten} bytes");
		_output.WriteLine($"Frame 2: {frame2?.BytesWritten} bytes");

		Assert.NotNull(frame2);
		Assert.Equal(0, frame2.BytesWritten);
		Assert.Equal(0, frame2.CharactersChanged);
		Assert.True(frame2.IsStaticFrame);
	}

	[Fact]
	public void DoubleBuffering_MinimalChange_MinimalOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "AAAAAAAAAA" }); // 10 A's
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 15,
			Title = "Minimal"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change one character
		control.SetContent(new List<string> { "AAAAABAAAA" }); // Change 6th A to B
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - should only output the changed region
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Characters changed: {metrics.CharactersChanged}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// Should be minimal (1 changed char + cursor positioning + ANSI)
		Assert.True(metrics.CharactersChanged <= 5);
		Assert.True(metrics.BytesWritten < 100);
	}

	[Fact]
	public void DoubleBuffering_MultipleFramesNoChange_AllZeroOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Multi-Static"
		};

		window.AddControl(new MarkupControl(new List<string> { "Unchanged" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1 - initial

		// Act - render 10 frames with no changes
		for (int i = 2; i <= 11; i++)
		{
			system.Render.UpdateDisplay();

			var metrics = system.RenderingDiagnostics?.GetMetrics(i);
			_output.WriteLine($"Frame {i}: {metrics?.BytesWritten} bytes");

			// Assert each frame produces zero output
			Assert.NotNull(metrics);
			Assert.Equal(0, metrics.BytesWritten);
			Assert.True(metrics.IsStaticFrame);
		}
	}

	[Fact]
	public void DoubleBuffering_AlternatingChanges_OptimalOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "A" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Alternating"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - alternate between A and B
		for (int i = 2; i <= 6; i++)
		{
			control.SetContent(new List<string> { i % 2 == 0 ? "B" : "A" });
			window.Invalidate(true);
			system.Render.UpdateDisplay();

			var metrics = system.RenderingDiagnostics?.GetMetrics(i);
			_output.WriteLine($"Frame {i}: {metrics?.BytesWritten} bytes");

			// Each change should produce minimal output
			Assert.NotNull(metrics);
			Assert.True(metrics.BytesWritten > 0); // Change occurred
			Assert.True(metrics.BytesWritten < 100); // But kept minimal
		}
	}

	[Fact]
	public void DoubleBuffering_LargeWindow_StillOptimized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 100,
			Height = 40,
			Title = "Large"
		};

		// Fill with content
		var lines = new List<string>();
		for (int i = 0; i < 35; i++)
		{
			lines.Add($"Line {i:D2}: Static content here");
		}
		window.AddControl(new MarkupControl(lines));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - no changes
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - even large window should produce zero output when static
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.Equal(0, metrics.BytesWritten);
		Assert.True(metrics.IsStaticFrame);
	}

	[Fact]
	public void DoubleBuffering_WindowOverlap_OnlyVisibleChangesDirty()
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

		// Window 2 (front, overlapping)
		var window2 = new Window(system)
		{
			Left = 20,
			Top = 15,
			Width = 40,
			Height = 20,
			Title = "Front"
		};
		window2.AddControl(new MarkupControl(new List<string> { "Front content" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - update behind window (hidden by front window)
		window1.AddControl(new MarkupControl(new List<string> { "Updated behind" }));
		window1.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - only visible parts should output
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// Output should be minimal since most of window1 is occluded
		// Only the non-overlapped portion needs updating
		Assert.True(metrics.BytesWritten > 0); // Some visible area changed
	}

	[Fact]
	public void DoubleBuffering_BringToFront_OptimizedRedraw()
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

		// Act - bring window1 to front
		system.WindowStateService.BringToFront(window1);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - z-order change requires re-render of overlap region
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.True(metrics.BytesWritten > 0); // Z-order change visible
	}

	[Fact]
	public void DoubleBuffering_ColorChangeOnly_OptimizedOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "TEXT" });
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

		// Act - change color but keep same text
		control.SetContent(new List<string> { "[red]TEXT[/]" });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - color change should be efficient
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// Color change requires ANSI codes but should still be efficient
		Assert.True(metrics.BytesWritten > 0);
		Assert.True(metrics.BytesWritten < 200); // Reasonable for 4 chars + color codes
	}

	[Fact]
	public void DoubleBuffering_MovingWindow_OnlyNewAndOldPositionsDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 30,
			Height = 10,
			Title = "Moving"
		};

		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - move window
		window.Left = 20;
		window.Top = 15;
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - should clear old position and draw new position
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.True(metrics.BytesWritten > 100); // Old + new positions
	}

	[Fact]
	public void DoubleBuffering_PartialUpdate_OnlyUpdatedRegionOutput()
	{
		// Arrange - CELL MODE (default)
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 20,
			Title = "Partial"
		};

		var lines = new List<string>
		{
			"Line 1",
			"Line 2",
			"Line 3",
			"Line 4",
			"Line 5"
		};
		window.AddControl(new MarkupControl(lines));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change only first line
		lines[0] = "CHANGED";
		window.AddControl(new MarkupControl(lines));
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - should only output changed region
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Characters changed: {metrics.CharactersChanged}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// CELL mode: efficient - only changed region output (full window would be 800 = 40 width * 20 height)
		Assert.True(metrics.CharactersChanged < 400); // Much less than full window
		Assert.True(metrics.BytesWritten > 0); // Something was output
	}

	[Fact]
	public void DoubleBuffering_PartialUpdate_OnlyUpdatedRegionOutput_LineMode()
	{
		// Arrange - LINE MODE
		var system = TestWindowSystemBuilder.CreateTestSystemWithLineMode();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 20,
			Title = "Partial"
		};

		var lines = new List<string>
		{
			"Line 1",
			"Line 2",
			"Line 3",
			"Line 4",
			"Line 5"
		};
		window.AddControl(new MarkupControl(lines));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change only first line
		lines[0] = "CHANGED";
		window.AddControl(new MarkupControl(lines));
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - LINE mode outputs full line
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Characters changed: {metrics.CharactersChanged}");
		_output.WriteLine($"Cells rendered: {metrics.CellsActuallyRendered}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// LINE mode: renders entire line (200 cells)
		Assert.True(metrics.CellsActuallyRendered >= 200);
		Assert.True(metrics.BytesWritten > 0);
	}

	[Fact]
	public void DoubleBuffering_Efficiency_HighRatioForSmallChanges()
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

		// Assert - efficiency ratio should be high
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Efficiency ratio: {metrics.EfficiencyRatio:P}");
		_output.WriteLine($"Dirty cells: {metrics.DirtyCellsMarked}");
		_output.WriteLine($"Rendered cells: {metrics.CellsActuallyRendered}");

		// Good efficiency for small changes
		Assert.True(metrics.EfficiencyRatio > 0.5);
	}

	[Fact]
	public void DoubleBuffering_WindowClose_ClearsOldPosition()
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

		// Assert - should output to clear window area
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.True(metrics.BytesWritten > 0); // Clearing requires output
	}

	[Fact]
	public void DoubleBuffering_ConsecutiveChanges_EachOptimized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "0" }); // Start with different char so loop creates actual changes
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Consecutive"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - make 5 consecutive changes
		for (int i = 2; i <= 6; i++)
		{
			control.SetContent(new List<string> { $"{(char)('A' + i - 2)}" });
			window.Invalidate(true);
			system.Render.UpdateDisplay();

			var metrics = system.RenderingDiagnostics?.GetMetrics(i);
			_output.WriteLine($"Frame {i}: {metrics?.BytesWritten} bytes");

			// Each change should be minimal
			Assert.NotNull(metrics);
			Assert.True(metrics.BytesWritten > 0);
			Assert.True(metrics.BytesWritten < 100);
		}
	}

	[Fact]
	public void DoubleBuffering_LastWindowClose_StillClearsDesktop()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 20,
			Top = 10,
			Width = 40,
			Height = 15,
			Title = "Last Window"
		};

		window.AddControl(new MarkupControl(new List<string> { "Last Window Content" }));
		system.WindowStateService.AddWindow(window);

		// Frame 1: Render window
		system.Render.UpdateDisplay();
		var frame1 = system.RenderingDiagnostics?.LastMetrics;
		Assert.True(frame1?.BytesWritten > 100); // Window rendered

		// Act - Close the ONLY window (no windows remain!)
		window.Close();
		Assert.Equal(0, system.WindowStateService.WindowCount); // Verify no windows left

		// Frame 2: Should clear window area even though no windows remain
		system.Render.UpdateDisplay();
		var frame2 = system.RenderingDiagnostics?.LastMetrics;

		// Assert - CRITICAL: Must output clearing even when window count = 0
		_output.WriteLine($"Frame 2 bytes written: {frame2?.BytesWritten}");
		_output.WriteLine($"Window count: {system.WindowStateService.WindowCount}");

		Assert.NotNull(frame2);
		Assert.True(frame2.BytesWritten > 0,
			$"Expected clearing output after last window closes, got {frame2.BytesWritten} bytes");
	}

	[Fact]
	public void DoubleBuffering_SetSameContent_ZeroOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "Content" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Same Content"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - set content to the SAME value (this will call Invalidate internally)
		control.SetContent(new List<string> { "Content" }); // Same as before!
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - should produce zero output because content is actually the same
		// CORE PRINCIPLE: Even with Invalidate(true), if nothing changed, zero output!
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.Equal(0, metrics.BytesWritten);
	}
}
