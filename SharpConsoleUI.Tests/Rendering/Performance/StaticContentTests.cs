using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Performance;

/// <summary>
/// ⭐ CRITICAL: Tests that static content produces ZERO output.
/// This is the most important optimization - if content doesn't change between frames,
/// we must output ZERO bytes. This is a CI/CD quality gate.
/// </summary>
public class StaticContentTests
{
	private readonly ITestOutputHelper _output;

	public StaticContentTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void StaticContent_ProducesZeroOutput()
	{
		// ⭐ CRITICAL QUALITY GATE: This test MUST pass or double-buffering is broken
		// If this fails, we're wasting bandwidth on every frame

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 50,
			Height = 20,
			Title = "Static Window"
		};
		window.AddControl(new MarkupControl(new List<string> { "Static Text That Never Changes" }));
		system.WindowStateService.AddWindow(window);

		// Act - Frame 1: Initial render (should output content)
		system.Render.UpdateDisplay();
		var frame1Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame1Metrics);

		_output.WriteLine($"Frame 1: {frame1Metrics.BytesWritten} bytes, {frame1Metrics.CharactersChanged} chars");
		Assert.True(frame1Metrics.BytesWritten > 100, "Frame 1 should render initial content");

		// Act - Frame 2: No changes (MUST output ZERO bytes)
		system.Render.UpdateDisplay();
		var frame2Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame2Metrics);

		_output.WriteLine($"Frame 2: {frame2Metrics.BytesWritten} bytes, {frame2Metrics.CharactersChanged} chars");

		// Assert - ⭐ CRITICAL: Zero output required!
		Assert.Equal(0, frame2Metrics.BytesWritten);
		Assert.Equal(0, frame2Metrics.CharactersChanged);
		Assert.True(frame2Metrics.IsStaticFrame);
		Assert.Equal(0, frame2Metrics.DirtyCellsMarked);
	}

	[Fact]
	public void StaticContent_MultipleFrames_AllZeroOutput()
	{
		// Verify zero output persists across many frames

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Left = 10, Top = 5, Width = 40, Height = 15 };
		window.AddControl(new MarkupControl(new List<string> { "Static content" }));
		system.WindowStateService.AddWindow(window);

		// Act - Initial render
		system.Render.UpdateDisplay();

		// Act - Render 10 static frames
		for (int i = 0; i < 10; i++)
		{
			system.Render.UpdateDisplay();
			var metrics = system.RenderingDiagnostics?.LastMetrics;
			Assert.NotNull(metrics);

			_output.WriteLine($"Frame {i + 2}: {metrics.BytesWritten} bytes");

			// All static frames must produce zero output
			Assert.Equal(0, metrics.BytesWritten);
			Assert.Equal(0, metrics.CharactersChanged);
			Assert.True(metrics.IsStaticFrame);
		}
	}

	[Fact]
	public void StaticContent_MultipleWindows_ZeroOutput()
	{
		// Multiple static windows should all produce zero output on frame 2

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window1 = new Window(system) { Left = 10, Top = 5, Width = 30, Height = 10, Title = "Window 1" };
		window1.AddControl(new MarkupControl(new List<string> { "Content 1" }));

		var window2 = new Window(system) { Left = 50, Top = 10, Width = 30, Height = 10, Title = "Window 2" };
		window2.AddControl(new MarkupControl(new List<string> { "Content 2" }));

		var window3 = new Window(system) { Left = 90, Top = 15, Width = 30, Height = 10, Title = "Window 3" };
		window3.AddControl(new MarkupControl(new List<string> { "Content 3" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.AddWindow(window3);

		// Act - Frame 1: Initial render
		system.Render.UpdateDisplay();
		var frame1Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame1Metrics);
		_output.WriteLine($"Frame 1 (3 windows): {frame1Metrics.BytesWritten} bytes");
		Assert.True(frame1Metrics.BytesWritten > 500);

		// Act - Frame 2: No changes (all windows static)
		system.Render.UpdateDisplay();
		var frame2Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame2Metrics);
		_output.WriteLine($"Frame 2 (3 windows): {frame2Metrics.BytesWritten} bytes");

		// Assert - Zero output for all static windows
		Assert.Equal(0, frame2Metrics.BytesWritten);
		Assert.Equal(0, frame2Metrics.CharactersChanged);
	}

	[Fact]
	public void StaticContent_AfterWindowMove_ThenStatic_ZeroOutput()
	{
		// After a window moves (frame 2), if nothing changes (frame 3), output should be zero

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 15,
			Title = "Moving Window"
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Act - Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Move window (should output)
		window.Left = 20;
		window.Top = 10;
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var frame2Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame2Metrics);
		_output.WriteLine($"Frame 2 (after move): {frame2Metrics.BytesWritten} bytes");
		Assert.True(frame2Metrics.BytesWritten > 0, "Moving window should output");

		// Act - Frame 3: No changes (MUST be zero)
		system.Render.UpdateDisplay();
		var frame3Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame3Metrics);
		_output.WriteLine($"Frame 3 (static after move): {frame3Metrics.BytesWritten} bytes");

		// Assert - Zero output after move stabilizes
		Assert.Equal(0, frame3Metrics.BytesWritten);
		Assert.Equal(0, frame3Metrics.CharactersChanged);
	}

	[Fact]
	public void StaticContent_LargeWindow_ZeroOutput()
	{
		// Even large windows must produce zero output when static

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem(200, 50);
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 180,
			Height = 45,
			Title = "Large Window"
		};

		// Fill with many lines of content
		var lines = new List<string>();
		for (int i = 0; i < 40; i++)
		{
			lines.Add($"Line {i}: This is a long line of static content that fills the window");
		}
		window.AddControl(new MarkupControl(lines));
		system.WindowStateService.AddWindow(window);

		// Act - Frame 1: Initial render
		system.Render.UpdateDisplay();
		var frame1Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame1Metrics);
		_output.WriteLine($"Frame 1 (large window): {frame1Metrics.BytesWritten} bytes, {frame1Metrics.CharactersChanged} chars");
		Assert.True(frame1Metrics.BytesWritten > 5000, "Large window should output lots of content");

		// Act - Frame 2: No changes (MUST be zero despite size)
		system.Render.UpdateDisplay();
		var frame2Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame2Metrics);
		_output.WriteLine($"Frame 2 (large window static): {frame2Metrics.BytesWritten} bytes");

		// Assert - Zero output even for large content
		Assert.Equal(0, frame2Metrics.BytesWritten);
		Assert.Equal(0, frame2Metrics.CharactersChanged);
	}

	[Fact]
	public void StaticContent_WithColors_ZeroOutput()
	{
		// Static colored content must also produce zero output

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Left = 10, Top = 5, Width = 50, Height = 15 };
		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]Red text[/]",
			"[blue]Blue text[/]",
			"[green]Green text[/]",
			"[bold yellow]Bold yellow text[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act - Frame 1: Initial render
		system.Render.UpdateDisplay();
		var frame1Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame1Metrics);
		_output.WriteLine($"Frame 1 (colored): {frame1Metrics.BytesWritten} bytes");
		Assert.True(frame1Metrics.BytesWritten > 100);

		// Act - Frame 2: No changes
		system.Render.UpdateDisplay();
		var frame2Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame2Metrics);
		_output.WriteLine($"Frame 2 (colored static): {frame2Metrics.BytesWritten} bytes");

		// Assert - Colors don't affect zero-output guarantee
		Assert.Equal(0, frame2Metrics.BytesWritten);
		Assert.Equal(0, frame2Metrics.CharactersChanged);
	}

	[Fact]
	public void StaticContent_AfterClose_ThenStatic_ZeroOutput()
	{
		// After closing a window (frame 2), if nothing else changes (frame 3), output should be zero

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Left = 10, Top = 5, Width = 30, Height = 10 };
		window1.AddControl(new MarkupControl(new List<string> { "Window 1" }));

		var window2 = new Window(system) { Left = 50, Top = 10, Width = 30, Height = 10 };
		window2.AddControl(new MarkupControl(new List<string> { "Window 2" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Act - Frame 1: Initial render
		system.Render.UpdateDisplay();

		// Act - Frame 2: Close window2 (should output clearing)
		system.WindowStateService.CloseWindow(window2);
		system.Render.UpdateDisplay();

		var frame2Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame2Metrics);
		_output.WriteLine($"Frame 2 (after close): {frame2Metrics.BytesWritten} bytes");
		Assert.True(frame2Metrics.BytesWritten > 0, "Closing window should output clearing");

		// Act - Frame 3: No more changes
		system.Render.UpdateDisplay();
		var frame3Metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame3Metrics);
		_output.WriteLine($"Frame 3 (static after close): {frame3Metrics.BytesWritten} bytes");

		// Assert - Zero output after close stabilizes
		Assert.Equal(0, frame3Metrics.BytesWritten);
		Assert.Equal(0, frame3Metrics.CharactersChanged);
	}
}
