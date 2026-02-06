using SharpConsoleUI;
using SharpConsoleUI.Tests.Infrastructure;
using System.Linq;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Tests window lifecycle operations: creation, adding to system, closing, and events.
/// </summary>
public class WindowLifecycleTests
{
	[Fact]
	public void Window_CreateAndAdd_AppearsInWindowList()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test Window" };

		// Act
		system.WindowStateService.AddWindow(window);

		// Assert
		Assert.Contains(window, system.WindowStateService.GetVisibleWindows());
		Assert.Equal(1, system.WindowStateService.GetVisibleWindows().Count);
	}

	[Fact]
	public void Window_Create_HasDefaultProperties()
	{
		// Arrange & Act
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);

		// Assert
		Assert.NotNull(window);
		Assert.Equal(WindowState.Normal, window.State);
		Assert.False(window.IsDirty);
	}

	[Fact]
	public void Window_Close_RemovedFromWindowList()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Closing Window" };
		system.WindowStateService.AddWindow(window);

		// Act
		window.Close();

		// Assert
		Assert.DoesNotContain(window, system.WindowStateService.GetVisibleWindows());
		Assert.Equal(0, system.WindowStateService.GetVisibleWindows().Count);
	}

	[Fact]
	public void Window_Close_TriggersClosingEvent()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		bool closingFired = false;

		window.OnClosing += (s, e) => closingFired = true;

		// Act
		window.Close();

		// Assert
		Assert.True(closingFired);
	}

	[Fact]
	public void Window_Close_TriggersClosedEvent()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		bool closedFired = false;

		window.OnClosed += (s, e) => closedFired = true;

		// Act
		window.Close();

		// Assert
		Assert.True(closedFired);
	}

	[Fact]
	public void Window_ClosingEvent_CanCancel()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		system.WindowStateService.AddWindow(window);

		window.OnClosing += (s, e) => e.Allow = false;

		// Act
		window.Close();

		// Assert - Window should still be in the list
		Assert.Contains(window, system.WindowStateService.GetVisibleWindows());
		Assert.Equal(1, system.WindowStateService.GetVisibleWindows().Count);
	}

	[Fact]
	public void Window_CloseMultiple_AllRemoved()
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
		window1.Close();
		window2.Close();
		window3.Close();

		// Assert
		Assert.Equal(0, system.WindowStateService.GetVisibleWindows().Count);
	}

	[Fact]
	public void Window_AddSameWindowTwice_OnlyAppearsOnce()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);

		// Act
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.AddWindow(window); // Add again

		// Assert
		Assert.Equal(1, system.WindowStateService.GetVisibleWindows().Count);
	}

	[Fact]
	public void Window_CloseAlreadyClosed_NoError()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		system.WindowStateService.AddWindow(window);

		// Act
		window.Close();
		window.Close(); // Close again

		// Assert - Should not throw
		Assert.Equal(0, system.WindowStateService.GetVisibleWindows().Count);
	}

	[Fact]
	public void Window_EventsOrder_ClosingThenClosed()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		var events = new List<string>();

		window.OnClosing += (s, e) => events.Add("Closing");
		window.OnClosed += (s, e) => events.Add("Closed");

		// Act
		window.Close();

		// Assert
		Assert.Equal(2, events.Count);
		Assert.Equal("Closing", events[0]);
		Assert.Equal("Closed", events[1]);
	}
}
