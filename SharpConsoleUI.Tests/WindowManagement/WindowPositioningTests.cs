using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Tests.Infrastructure;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Tests window positioning and size management.
/// </summary>
public class WindowPositioningTests
{
	[Fact]
	public void Window_Create_HasDefaultPosition()
	{
		// Arrange & Act
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };

		// Assert - Default position (not checking specific values, just that it's set)
		Assert.True(window.Left >= 0);
		Assert.True(window.Top >= 0);
	}

	[Fact]
	public void Window_SetPosition_UpdatesLeftAndTop()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };

		// Act
		window.Left = 25;
		window.Top = 15;

		// Assert
		Assert.Equal(25, window.Left);
		Assert.Equal(15, window.Top);
	}

	[Fact]
	public void Window_SetSize_UpdatesWidthAndHeight()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };

		// Act
		window.Width = 80;
		window.Height = 40;

		// Assert
		Assert.Equal(80, window.Width);
		Assert.Equal(40, window.Height);
	}

	[Fact]
	public void Window_ChangePosition_InvalidatesWindow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Initial render
		Assert.False(window.IsDirty);

		// Act
		window.Left = 50;

		// Assert
		Assert.True(window.IsDirty);
	}

	[Fact]
	public void Window_ChangeSize_InvalidatesWindow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Initial render
		Assert.False(window.IsDirty);

		// Act
		window.Width = 100;

		// Assert
		Assert.True(window.IsDirty);
	}

	[Fact]
	public void Window_StartDrag_EntersDragMode()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Title = "Draggable",
			Left = 10,
			Top = 10,
			Width = 50,
			Height = 20
		};
		system.WindowStateService.AddWindow(window);

		// Act
		system.WindowStateService.StartDrag(window, new Point(15, 12));

		// Assert
		Assert.True(system.WindowStateService.IsDragging);
	}

	[Fact]
	public void Window_EndDrag_ExitsDragMode()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Title = "Draggable",
			Left = 10,
			Top = 10
		};
		system.WindowStateService.AddWindow(window);

		system.WindowStateService.StartDrag(window, new Point(15, 12));
		Assert.True(system.WindowStateService.IsDragging);

		// Act
		system.WindowStateService.EndDrag();

		// Assert
		Assert.False(system.WindowStateService.IsDragging);
	}

	[Fact]
	public void Window_StartResize_EntersResizeMode()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Title = "Resizable",
			Left = 10,
			Top = 10,
			Width = 50,
			Height = 30,
			IsResizable = true
		};
		system.WindowStateService.AddWindow(window);

		// Act
		system.WindowStateService.StartResize(window, ResizeDirection.BottomRight, new Point(60, 40));

		// Assert
		Assert.True(system.WindowStateService.IsResizing);
	}

	[Fact]
	public void Window_EndResize_ExitsResizeMode()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Title = "Resizable",
			IsResizable = true
		};
		system.WindowStateService.AddWindow(window);

		system.WindowStateService.StartResize(window, ResizeDirection.BottomRight, new Point(60, 40));
		Assert.True(system.WindowStateService.IsResizing);

		// Act
		system.WindowStateService.EndResize();

		// Assert
		Assert.False(system.WindowStateService.IsResizing);
	}

	[Fact]
	public void Window_StartResizeNonResizableWindow_NoEffect()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Title = "Not Resizable",
			IsResizable = false
		};
		system.WindowStateService.AddWindow(window);

		// Act
		system.WindowStateService.StartResize(window, ResizeDirection.BottomRight, new Point(60, 40));

		// Assert - Should not enter resize mode
		Assert.False(system.WindowStateService.IsResizing);
	}

	[Fact]
	public void Window_PositionPersists_AcrossRenders()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Title = "Test",
			Left = 25,
			Top = 15
		};
		system.WindowStateService.AddWindow(window);

		// Act - Multiple renders
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Assert - Position should not change
		Assert.Equal(25, window.Left);
		Assert.Equal(15, window.Top);
	}

	[Fact]
	public void Window_SizePersists_AcrossRenders()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Title = "Test",
			Width = 80,
			Height = 40
		};
		system.WindowStateService.AddWindow(window);

		// Act - Multiple renders
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Assert - Size should not change
		Assert.Equal(80, window.Width);
		Assert.Equal(40, window.Height);
	}

	[Fact]
	public void Window_SetNegativePosition_Accepted()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };

		// Act
		window.Left = -5;
		window.Top = -10;

		// Assert - Negative positions are allowed (off-screen windows)
		Assert.Equal(-5, window.Left);
		Assert.Equal(-10, window.Top);
	}

	[Fact]
	public void Window_SetZeroSize_Accepted()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };

		// Act
		window.Width = 0;
		window.Height = 0;

		// Assert - Zero size is allowed (though not useful)
		Assert.Equal(0, window.Width);
		Assert.Equal(0, window.Height);
	}

	[Fact]
	public void Window_MultiplePositionChanges_AllApplied()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };

		// Act
		window.Left = 10;
		window.Left = 20;
		window.Left = 30;

		// Assert - Last value wins
		Assert.Equal(30, window.Left);
	}

	[Fact]
	public void Window_IsResizable_DefaultTrue()
	{
		// Arrange & Act
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };

		// Assert
		Assert.True(window.IsResizable);
	}

	[Fact]
	public void Window_SetIsResizable_UpdatesProperty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test", IsResizable = false };

		// Act
		window.IsResizable = true;

		// Assert
		Assert.True(window.IsResizable);
	}
}
