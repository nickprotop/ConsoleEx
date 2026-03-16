// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Tests that mouse-clicking a focusable control inside a HorizontalGrid
/// inside a ScrollablePanel correctly routes subsequent keyboard input
/// to that control (not to the panel's scroll handler).
/// </summary>
public class MouseFocusKeyboardRoutingTests
{
	/// <summary>
	/// Creates a ScrollablePanel containing a HorizontalGrid with two buttons in separate columns.
	/// Returns everything needed to test mouse/keyboard routing.
	/// </summary>
	private (ScrollablePanelControl panel, HorizontalGridControl grid,
		ButtonControl button1, ButtonControl button2,
		ConsoleWindowSystem system, Window window)
		CreatePanelWithGridAndButtons()
	{
		var panel = new ScrollablePanelControl();
		panel.Height = 10;

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var button1 = new ButtonControl { Text = "Button1" };
		var button2 = new ButtonControl { Text = "Button2" };

		col1.AddContent(button1);
		col2.AddContent(button2);
		grid.AddColumn(col1);
		grid.AddColumn(col2);

		panel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		return (panel, grid, button1, button2, system, window);
	}

	/// <summary>
	/// Helper to create a mouse click event at the given panel-relative position.
	/// </summary>
	private static MouseEventArgs CreateClick(int x, int y)
	{
		return ContainerTestHelpers.CreateClick(x, y);
	}

	[Fact]
	public void MouseClick_OnControlInGridInPanel_SetsFocusChain()
	{
		// Arrange
		var (panel, grid, button1, button2, system, window) = CreatePanelWithGridAndButtons();

		// Act: click on button1 (at position 0,0 inside the panel content area)
		var click = CreateClick(0, 0);
		panel.ProcessMouseEvent(click);

		// Assert: focus chain is set correctly
		Assert.True(button1.HasFocus, "Button1 should have focus after mouse click");
		Assert.True(grid.HasFocus, "HorizontalGrid should track focus (HasFocus=true)");
		Assert.True(panel.HasFocus, "ScrollablePanel should have focus");
	}

	[Fact]
	public void KeyboardInput_ReachesMouseFocusedControlInGrid()
	{
		// Arrange: use a slider that responds to keyboard input
		var panel = new ScrollablePanelControl();
		panel.Height = 15;

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);

		var slider = new SliderControl
		{
			Orientation = SliderOrientation.Horizontal,
			MinValue = 0,
			MaxValue = 100,
			Value = 50,
			Step = 1
		};

		col1.AddContent(slider);
		grid.AddColumn(col1);
		panel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Act: click on the slider area to focus it
		var click = CreateClick(0, 0);
		panel.ProcessMouseEvent(click);

		// Verify focus chain is set
		Assert.True(slider.HasFocus, "Slider should have focus after click");
		Assert.True(grid.HasFocus, "Grid should track focus");
		Assert.True(panel.HasFocus, "Panel should have focus");

		// Act: send Right arrow key to increase slider value
		var rightArrow = new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false);
		bool handled = panel.ProcessKey(rightArrow);

		// Assert: key was handled by the slider, not by the panel's scroll handler
		Assert.True(handled, "Key should be handled");
		Assert.Equal(51, slider.Value);
	}

	[Fact]
	public void MouseClick_DifferentControlInGrid_SwitchesFocus()
	{
		// Arrange: use explicit column widths so we know exact X positions
		var panel = new ScrollablePanelControl();
		panel.Height = 10;

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		col1.Width = 30;
		var col2 = new ColumnContainer(grid);
		col2.Width = 30;

		var button1 = new ButtonControl { Text = "Button1" };
		var button2 = new ButtonControl { Text = "Button2" };

		col1.AddContent(button1);
		col2.AddContent(button2);
		grid.AddColumn(col1);
		grid.AddColumn(col2);

		panel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Act: click on button1 first (x=0 is inside col1)
		var click1 = CreateClick(0, 0);
		panel.ProcessMouseEvent(click1);
		Assert.True(button1.HasFocus, "Button1 should have focus after first click");

		// Act: click on button2 (in col2, which starts at col1's actual width)
		var click2 = CreateClick(col1.ActualWidth, 0);
		grid.ProcessMouseEvent(click2);

		// Assert: button2 has focus, button1 doesn't
		// Assert: button2 has focus, button1 doesn't
		Assert.True(button2.HasFocus, "Button2 should have focus after clicking it");
		Assert.False(button1.HasFocus, "Button1 should have lost focus");
	}

	[Fact]
	public void MouseClick_OutsideGrid_ClearsGridFocus()
	{
		// Arrange: panel with a grid AND a standalone button below it
		var panel = new ScrollablePanelControl();
		panel.Height = 15;

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var button1 = new ButtonControl { Text = "InGrid" };
		col1.AddContent(button1);
		grid.AddColumn(col1);

		var standaloneButton = new ButtonControl { Text = "Standalone" };

		panel.AddControl(grid);
		panel.AddControl(standaloneButton);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		// Act: click button in grid to focus it
		var click1 = CreateClick(0, 0);
		panel.ProcessMouseEvent(click1);
		Assert.True(button1.HasFocus, "Button1 in grid should have focus");
		Assert.True(grid.HasFocus, "Grid should track focus");

		// Act: click on standalone button (below the grid)
		// The standalone button is after the grid, so its Y position is after the grid's height
		int standaloneY = standaloneButton.ActualY > 0 ? standaloneButton.ActualY : 3;
		var click2 = CreateClick(0, standaloneY);
		panel.ProcessMouseEvent(click2);

		// Assert: grid focus is cleared, standalone has focus
		Assert.True(standaloneButton.HasFocus, "Standalone button should have focus");
		Assert.False(button1.HasFocus, "Button in grid should have lost focus");
	}

	[Fact]
	public void MouseFocus_ThenTab_ContinuesCorrectly()
	{
		// Arrange
		var (panel, grid, button1, button2, system, window) = CreatePanelWithGridAndButtons();

		// Act: click on button1 to focus it via mouse
		var click = CreateClick(0, 0);
		panel.ProcessMouseEvent(click);
		Assert.True(button1.HasFocus, "Button1 should have focus from mouse click");

		// Act: press Tab to move to button2
		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		bool handled = panel.ProcessKey(tabKey);

		// Assert: Tab should navigate within the grid
		// The panel delegates to grid, grid handles Tab internally
		Assert.True(handled, "Tab should be handled");
		Assert.True(button2.HasFocus, "Button2 should have focus after Tab");
		Assert.False(button1.HasFocus, "Button1 should have lost focus after Tab");
	}
}
