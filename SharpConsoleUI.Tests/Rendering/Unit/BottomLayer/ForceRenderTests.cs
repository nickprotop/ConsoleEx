using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Tests that verify the DesktopNeedsRender flag forces rendering
/// even when no windows are dirty (e.g., after closing last window).
/// </summary>
public class ForceRenderTests
{
	[Fact]
	public void ForceRender_Flag_CausesUpdateDisplay()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Frame 1: Render nothing (empty desktop)
		system.Render.UpdateDisplay();
		var frame1 = system.RenderingDiagnostics?.LastMetrics;

		// Act: Set force render flag
		system.Render.DesktopNeedsRender = true;

		// Frame 2: Should render even with no windows
		system.Render.UpdateDisplay();
		var frame2 = system.RenderingDiagnostics?.LastMetrics;

		// Assert: Flag was cleared after render
		Assert.False(system.Render.DesktopNeedsRender);
	}

	[Fact]
	public void CloseLastWindow_SetsForceRenderFlag()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Only Window"
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1
		Assert.False(system.Render.DesktopNeedsRender); // Should be false initially

		// Act: Close the ONLY window
		window.Close();

		// Assert: DesktopNeedsRender flag was set
		Assert.True(system.Render.DesktopNeedsRender,
			"DesktopNeedsRender flag should be set after closing window");

		// Frame 2: Render and clear the window area
		system.Render.UpdateDisplay();
		var metrics = system.RenderingDiagnostics?.LastMetrics;

		// Verify clearing was output
		Assert.True(metrics?.BytesWritten > 0,
			$"Expected clearing output, got {metrics?.BytesWritten} bytes");

		// Flag should be cleared after rendering
		Assert.False(system.Render.DesktopNeedsRender,
			"DesktopNeedsRender flag should be cleared after rendering");
	}

	[Fact]
	public void ForceRender_Flag_WorksInRunLoop()
	{
		// This test simulates the Run() loop behavior
		// where UpdateDisplay() is only called if AnyWindowDirty() || DesktopNeedsRender

		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Left = 10, Top = 5, Width = 20, Height = 10 };
		window.AddControl(new MarkupControl(new List<string> { "Window" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1: Window visible

		// Close window
		window.Close();
		Assert.Equal(0, system.Windows.Count);

		// Simulate Run() loop logic
		bool anyWindowDirty = system.Windows.Values.Any(w => w.IsDirty);
		bool desktopNeedsRender = system.Render.DesktopNeedsRender;
		bool shouldRender = anyWindowDirty || desktopNeedsRender;

		// Assert: shouldRender is true because DesktopNeedsRender is true
		Assert.False(anyWindowDirty, "No windows should be dirty (no windows exist)");
		Assert.True(desktopNeedsRender, "DesktopNeedsRender should be true after close");
		Assert.True(shouldRender, "Should render to clear the window");

		// Frame 2: Render (would be called by Run() loop because shouldRender = true)
		system.Render.UpdateDisplay();
		var metrics = system.RenderingDiagnostics?.LastMetrics;

		Assert.True(metrics?.BytesWritten > 0, "Window area should be cleared");
	}

	[Fact]
	public void ForceRender_MultipleFrames_OnlyRenderOnce()
	{
		// Verify that the flag is cleared after rendering,
		// preventing unnecessary renders on subsequent frames

		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Left = 10, Top = 5, Width = 20, Height = 10 };
		window.AddControl(new MarkupControl(new List<string> { "Window" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		window.Close();
		Assert.True(system.Render.DesktopNeedsRender);

		// Frame 2: Should render
		system.Render.UpdateDisplay();
		var frame2 = system.RenderingDiagnostics?.LastMetrics;
		Assert.True(frame2?.BytesWritten > 0);
		Assert.False(system.Render.DesktopNeedsRender); // Cleared

		// Frame 3: Should NOT render (no changes, flag is false)
		system.Render.UpdateDisplay();
		var frame3 = system.RenderingDiagnostics?.LastMetrics;
		Assert.Equal(0, frame3?.BytesWritten); // Zero output

		// Frame 4: Should NOT render
		system.Render.UpdateDisplay();
		var frame4 = system.RenderingDiagnostics?.LastMetrics;
		Assert.Equal(0, frame4?.BytesWritten); // Zero output
	}
}
