using SharpConsoleUI;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Tests window activation and deactivation behavior.
/// </summary>
public class WindowActivationTests
{
	[Fact]
	public void Window_Activate_BecomesActiveWindow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test Window" };
		system.WindowStateService.AddWindow(window);

		// Act
		system.WindowStateService.SetActiveWindow(window);

		// Assert
		Assert.Same(window, system.WindowStateService.ActiveWindow);
	}

	[Fact]
	public void Window_ActivateAnother_DeactivatesPrevious()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Act
		system.WindowStateService.SetActiveWindow(window1);
		Assert.Same(window1, system.WindowStateService.ActiveWindow);

		system.WindowStateService.SetActiveWindow(window2);

		// Assert
		Assert.NotEqual(window1, system.WindowStateService.ActiveWindow);
		Assert.Same(window2, system.WindowStateService.ActiveWindow);
		Assert.Same(window2, system.WindowStateService.ActiveWindow);
	}

	[Fact]
	public void Window_CloseActiveWindow_ClearsActiveWindow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Active Window" };
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		// Act
		window.Close();

		// Assert
		Assert.Null(system.WindowStateService.ActiveWindow);
	}

	[Fact]
	public void Window_ActivateNonExistentWindow_NoEffect()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.SetActiveWindow(window1);

		// Act - Try to activate window that's not in the system
		system.WindowStateService.SetActiveWindow(window2);

		// Assert - Window1 should remain active
		Assert.Same(window1, system.WindowStateService.ActiveWindow);
		Assert.Same(window1, system.WindowStateService.ActiveWindow);
		Assert.NotEqual(window2, system.WindowStateService.ActiveWindow);
	}

	[Fact]
	public void Window_AddWindow_DoesNotAutoActivate()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };

		// Add first window (will be activated since it's the first)
		system.WindowStateService.AddWindow(window1);
		Assert.Same(window1, system.WindowStateService.ActiveWindow);

		// Act - Add second window with activateWindow: false
		system.WindowStateService.AddWindow(window2, activateWindow: false);

		// Assert - Window2 should NOT steal activation from window1
		Assert.Same(window1, system.WindowStateService.ActiveWindow);
		Assert.NotSame(window2, system.WindowStateService.ActiveWindow);
	}

	[Fact]
	public void Window_MultipleWindows_OnlyOneActive()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system) { Title = "Window 1" };
		var window2 = new Window(system) { Title = "Window 2" };
		var window3 = new Window(system) { Title = "Window 3" };

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.AddWindow(window3);

		// Act
		system.WindowStateService.SetActiveWindow(window2);

		// Assert
		Assert.NotEqual(window1, system.WindowStateService.ActiveWindow);
		Assert.Same(window2, system.WindowStateService.ActiveWindow);
		Assert.NotEqual(window3, system.WindowStateService.ActiveWindow);
	}

	[Fact]
	public void Window_ActivateSameWindow_NoChange()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		// Act - Activate again
		system.WindowStateService.SetActiveWindow(window);

		// Assert - Should still be active
		Assert.Same(window, system.WindowStateService.ActiveWindow);
		Assert.Same(window, system.WindowStateService.ActiveWindow);
	}

	[Fact]
	public void Window_DeactivateBySettingNull_ClearsActiveWindow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		// Act
		system.WindowStateService.DeactivateCurrentWindow();

		// Assert
		Assert.Null(system.WindowStateService.ActiveWindow);
		Assert.NotEqual(window, system.WindowStateService.ActiveWindow);
	}

	[Fact]
	public void Window_ActivationState_PersistsAcrossRenders()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		// Act - Multiple renders
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Assert - Should remain active
		Assert.Same(window, system.WindowStateService.ActiveWindow);
		Assert.Same(window, system.WindowStateService.ActiveWindow);
	}

	[Fact]
	public void Window_ActivateMinimizedWindow_RestoresAndActivates()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);
		window.Minimize();

		// Act
		system.WindowStateService.SetActiveWindow(window);

		// Assert - Should be restored and active
		Assert.Equal(WindowState.Normal, window.State);
		Assert.Same(window, system.WindowStateService.ActiveWindow);
	}
}
