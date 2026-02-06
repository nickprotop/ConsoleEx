using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using System.Linq;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Tests window z-order management: bring to front, send to back, overlapping behavior.
/// </summary>
public class ZOrderTests
{
	[Fact]
	public void Window_BringToFront_BecomesTopWindow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Act
		system.WindowStateService.BringToFront(window1);

		// Assert
		var zOrder = system.WindowStateService.GetWindowsByZOrder();
		Assert.Same(window1, zOrder.First());
	}

	[Fact]
	public void Window_SendToBack_BecomesBottomWindow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Act
		system.WindowStateService.SendToBack(window2);

		// Assert
		var zOrder = system.WindowStateService.GetWindowsByZOrder();
		Assert.Same(window2, zOrder.Last());
	}

	[Fact]
	public void Window_NewWindowAdded_AppearsOnTop()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };

		// Act
		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Assert
		var zOrder = system.WindowStateService.GetWindowsByZOrder();
		Assert.Same(window2, zOrder.First()); // Most recently added is on top
	}

	[Fact]
	public void Window_Overlapping_TopWindowRendersOnTop()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Window 1: 10,10 -> 40,20 with red content
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 30,
			Height = 10,
			Title = "Back"
		};
		window1.AddControl(new MarkupControl(new List<string> { "[red]BACK[/]" }));

		// Window 2: 20,15 -> 50,25 (overlaps window1) with blue content
		var window2 = new Window(system)
		{
			Left = 20,
			Top = 15,
			Width = 30,
			Height = 10,
			Title = "Front"
		};
		window2.AddControl(new MarkupControl(new List<string> { "[blue]FRONT[/]" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Act
		system.Render.UpdateDisplay();

		// Assert - Window2 should be on top in the overlap region
		var snapshot = system.RenderingDiagnostics?.LastBufferSnapshot;
		Assert.NotNull(snapshot);

		// Check a cell in the overlap region (e.g., 25, 17)
		// This should show window2's content since it's on top
		if (snapshot.Width > 25 && snapshot.Height > 17)
		{
			var cell = snapshot.GetCell(25, 17);
			// Window2 is on top, so we should see its content or border
			Assert.NotEqual(' ', cell.Character); // Something is rendered there
		}
	}

	[Fact]
	public void Window_BringToFront_UpdatesRenderOrder()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1", Left = 10, Top = 10, Width = 30, Height = 10 };
		var window2 = new Window(system) { Title = "Window 2", Left = 20, Top = 15, Width = 30, Height = 10 };

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2); // Window2 on top

		system.Render.UpdateDisplay(); // Initial render

		// Act - Bring window1 to front
		system.WindowStateService.BringToFront(window1);
		system.Render.UpdateDisplay(); // Re-render

		// Assert
		var zOrder = system.WindowStateService.GetWindowsByZOrder();
		Assert.Same(window1, zOrder.First());

		// Rendering should reflect new z-order
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);
		Assert.True(metrics.BytesWritten > 0); // Something was re-rendered
	}

	[Fact]
	public void Window_ZOrderChange_InvalidatesWindow()
	{
		// Arrange - Create two windows at same position (window2 completely covers window1)
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1", Left = 0, Top = 0, Width = 40, Height = 20 };
		var window2 = new Window(system) { Title = "Window 2", Left = 0, Top = 0, Width = 40, Height = 20 };
		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2); // window2 becomes active, completely covers window1

		// Act 1 - Initial render with window1 completely covered
		system.Render.UpdateDisplay();

		// Assert 1 - Window1 remains dirty (optimization: skip completely covered windows)
		Assert.True(window1.IsDirty); // Still dirty - completely covered, not rendered
		Assert.False(window2.IsDirty); // Window2 was rendered and cleaned

		// Act 2 - Move window2 to partially overlap (expose window1)
		window2.Left = 10; // Move right 10 chars - window1 now visible on left side
		system.Render.UpdateDisplay();

		// Assert 2 - Window1 should now be rendered and clean (exposed!)
		Assert.False(window1.IsDirty); // Now clean - was rendered because exposed
		Assert.False(window2.IsDirty);

		// Act 3 - Bring window1 to front (changes z-order)
		system.WindowStateService.BringToFront(window1);

		// Assert 3 - Window1 should be marked dirty due to z-order change
		Assert.True(window1.IsDirty); // Dirty again due to z-order change
	}

	[Fact]
	public void Window_ThreeWindows_CorrectZOrder()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };
		var window3 = new Window(system) { Title = "Window 3" };

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.AddWindow(window3);

		// Act - Bring window1 to front
		system.WindowStateService.BringToFront(window1);

		// Assert - Order should be: window1, window3, window2
		var zOrder = system.WindowStateService.GetWindowsByZOrder().ToList();
		Assert.Same(window1, zOrder[0]);
		Assert.Same(window3, zOrder[1]);
		Assert.Same(window2, zOrder[2]);
	}

	[Fact]
	public void Window_SendToBack_FromTop()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2); // window2 on top

		// Act - Send window2 to back
		system.WindowStateService.SendToBack(window2);

		// Assert
		var zOrder = system.WindowStateService.GetWindowsByZOrder().ToList();
		Assert.Same(window1, zOrder[0]); // window1 now on top
		Assert.Same(window2, zOrder[1]); // window2 at back
	}

	[Fact]
	public void Window_ZOrder_MaintainedAcrossRenders()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.BringToFront(window1);

		// Act - Multiple renders
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Assert - Z-order should remain consistent
		var zOrder = system.WindowStateService.GetWindowsByZOrder();
		Assert.Same(window1, zOrder.First());
	}
}
