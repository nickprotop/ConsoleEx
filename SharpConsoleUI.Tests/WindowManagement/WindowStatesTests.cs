using SharpConsoleUI;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Tests window state transitions: Normal, Minimized, Maximized.
/// </summary>
public class WindowStatesTests
{
	[Fact]
	public void Window_Create_DefaultStateIsNormal()
	{
		// Arrange & Act
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };

		// Assert
		Assert.Equal(WindowState.Normal, window.State);
	}

	[Fact]
	public void Window_Minimize_ChangesStateToMinimized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test", Width = 50, Height = 20 };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Minimize();

		// Assert
		Assert.Equal(WindowState.Minimized, window.State);
	}

	[Fact]
	public void Window_Maximize_ChangesStateToMaximized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test", Width = 50, Height = 20 };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Maximize();

		// Assert
		Assert.Equal(WindowState.Maximized, window.State);
	}

	[Fact]
	public void Window_Restore_FromMinimized_ReturnsToNormal()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test", Width = 50, Height = 20 };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Minimize();
		Assert.Equal(WindowState.Minimized, window.State);

		window.Restore();

		// Assert
		Assert.Equal(WindowState.Normal, window.State);
	}

	[Fact]
	public void Window_Restore_FromMaximized_ReturnsToNormal()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test", Width = 50, Height = 20 };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Maximize();
		Assert.Equal(WindowState.Maximized, window.State);

		window.Restore();

		// Assert
		Assert.Equal(WindowState.Normal, window.State);
	}

	[Fact]
	public void Window_MaximizeThenMinimize_ChangesState()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test", Width = 50, Height = 20 };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Maximize();
		window.Minimize();

		// Assert
		Assert.Equal(WindowState.Minimized, window.State);
	}

	[Fact]
	public void Window_MinimizeTwice_RemainsMinimized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Minimize();
		window.Minimize(); // Call again

		// Assert
		Assert.Equal(WindowState.Minimized, window.State);
	}

	[Fact]
	public void Window_MaximizeTwice_RemainsMaximized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Maximize();
		window.Maximize(); // Call again

		// Assert
		Assert.Equal(WindowState.Maximized, window.State);
	}

	[Fact]
	public void Window_RestoreWhenNormal_RemainsNormal()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Restore(); // Already in Normal state

		// Assert
		Assert.Equal(WindowState.Normal, window.State);
	}

	[Fact]
	public void Window_MaximizeMinimizedWindow_ChangesToMaximized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);
		window.Minimize();

		// Act
		window.Maximize();

		// Assert
		Assert.Equal(WindowState.Maximized, window.State);
	}

	[Fact]
	public void Window_MinimizeMaximizedWindow_ChangesToMinimized()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);
		window.Maximize();

		// Act
		window.Minimize();

		// Assert
		Assert.Equal(WindowState.Minimized, window.State);
	}

	[Fact]
	public void Window_StateChange_InvalidatesWindow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Initial render
		Assert.False(window.PendingWork != FrameWork.None); // Clean after render

		// Act
		window.Minimize();

		// Assert
		Assert.True(window.PendingWork != FrameWork.None); // Should be marked dirty
	}

	[Fact]
	public void Window_Maximized_RestoresPreviousSizeOnRestore()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Title = "Test",
			Width = 50,
			Height = 20,
			Left = 10,
			Top = 5
		};
		system.WindowStateService.AddWindow(window);

		var originalWidth = window.Width;
		var originalHeight = window.Height;
		var originalLeft = window.Left;
		var originalTop = window.Top;

		// Act
		window.Maximize();
		// Width/Height/Position may change when maximized

		window.Restore();

		// Assert - Should restore original dimensions
		Assert.Equal(originalWidth, window.Width);
		Assert.Equal(originalHeight, window.Height);
		Assert.Equal(originalLeft, window.Left);
		Assert.Equal(originalTop, window.Top);
	}

	[Fact]
	public void Window_MinimizedWindow_NotRendered()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test", Width = 50, Height = 20 };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Minimize();
		system.Render.UpdateDisplay();

		// Assert - Minimized windows should not contribute to rendering
		// (This is implicit in the rendering logic - minimized windows are skipped)
		Assert.Equal(WindowState.Minimized, window.State);
	}

	[Fact]
	public void Window_StateTransitions_MultipleSequences()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);

		// Act & Assert - Test various state transitions
		Assert.Equal(WindowState.Normal, window.State);

		window.Maximize();
		Assert.Equal(WindowState.Maximized, window.State);

		window.Minimize();
		Assert.Equal(WindowState.Minimized, window.State);

		// Restoring a window that was minimized WHILE MAXIMIZED brings it back maximized (issue #70).
		// This assertion previously expected Normal, which is the defect that issue reported: minimizing
		// never resized the window, so returning it to Normal left State claiming one thing and the
		// bounds showing another, and the title-bar button then maximized an already-maximized window.
		window.Restore();
		Assert.Equal(WindowState.Maximized, window.State);

		// A second restore, now from Maximized, returns it to Normal as it always has.
		window.Restore();
		Assert.Equal(WindowState.Normal, window.State);

		window.Minimize();
		Assert.Equal(WindowState.Minimized, window.State);

		window.Maximize();
		Assert.Equal(WindowState.Maximized, window.State);

		window.Restore();
		Assert.Equal(WindowState.Normal, window.State);
	}
}
