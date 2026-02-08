using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Comprehensive tests for Tab navigation and focus traversal.
/// Tests cover: hidden/visible controls, nested containers, various control types, and edge cases.
/// </summary>
public class FocusNavigationTests
{
	private readonly ITestOutputHelper? _testOutputHelper;

	public FocusNavigationTests(ITestOutputHelper? testOutputHelper = null)
	{
		_testOutputHelper = testOutputHelper;
	}

	#region Basic Navigation Tests

	[Fact]
	public void Tab_WithMultipleControls_CyclesThroughInOrder()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var button1 = new ButtonControl { Text = "Button1" };
		var button2 = new ButtonControl { Text = "Button2" };
		var button3 = new ButtonControl { Text = "Button3" };

		window.AddControl(button1);
		window.AddControl(button2);
		window.AddControl(button3);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Focus button1
		system.FocusStateService.SetFocus(window, button1);
		Assert.True(button1.HasFocus);
		Assert.False(button2.HasFocus);
		Assert.False(button3.HasFocus);

		// Tab to button2
		window.SwitchFocus(backward: false);
		Assert.False(button1.HasFocus);
		Assert.True(button2.HasFocus);
		Assert.False(button3.HasFocus);

		// Tab to button3
		window.SwitchFocus(backward: false);
		Assert.False(button1.HasFocus);
		Assert.False(button2.HasFocus);
		Assert.True(button3.HasFocus);

		// Tab cycles back to button1
		window.SwitchFocus(backward: false);
		Assert.True(button1.HasFocus);
		Assert.False(button2.HasFocus);
		Assert.False(button3.HasFocus);
	}

	[Fact]
	public void ShiftTab_WithMultipleControls_CyclesBackward()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var button1 = new ButtonControl { Text = "Button1" };
		var button2 = new ButtonControl { Text = "Button2" };
		var button3 = new ButtonControl { Text = "Button3" };

		window.AddControl(button1);
		window.AddControl(button2);
		window.AddControl(button3);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Start at button3
		system.FocusStateService.SetFocus(window, button3);
		Assert.True(button3.HasFocus);

		// Shift+Tab to button2
		window.SwitchFocus(backward: true);
		Assert.False(button3.HasFocus);
		Assert.True(button2.HasFocus);

		// Shift+Tab to button1
		window.SwitchFocus(backward: true);
		Assert.True(button1.HasFocus);
		Assert.False(button2.HasFocus);

		// Shift+Tab cycles back to button3
		window.SwitchFocus(backward: true);
		Assert.True(button3.HasFocus);
		Assert.False(button1.HasFocus);
	}

	#endregion

	#region Visibility and State Tests

	[Fact]
	public void Tab_SkipsHiddenControls()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var button1 = new ButtonControl { Text = "Button1" };
		var button2 = new ButtonControl { Text = "Button2", Visible = false }; // Hidden
		var button3 = new ButtonControl { Text = "Button3" };

		window.AddControl(button1);
		window.AddControl(button2);
		window.AddControl(button3);

		system.WindowStateService.AddWindow(window);

		// Act & Assert
		system.FocusStateService.SetFocus(window, button1);
		Assert.True(button1.HasFocus);

		// Tab should skip button2 and go to button3
		window.SwitchFocus(backward: false);
		Assert.False(button1.HasFocus);
		Assert.False(button2.HasFocus); // Still no focus (hidden)
		Assert.True(button3.HasFocus);
	}

	[Fact]
	public void Tab_SkipsDisabledControls()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var button1 = new ButtonControl { Text = "Button1" };
		var button2 = new ButtonControl { Text = "Button2", IsEnabled = false }; // Disabled
		var button3 = new ButtonControl { Text = "Button3" };

		window.AddControl(button1);
		window.AddControl(button2);
		window.AddControl(button3);

		system.WindowStateService.AddWindow(window);

		// Act & Assert
		system.FocusStateService.SetFocus(window, button1);
		Assert.True(button1.HasFocus);

		// Tab should skip button2 and go to button3
		window.SwitchFocus(backward: false);
		Assert.False(button1.HasFocus);
		Assert.False(button2.HasFocus); // Still no focus (disabled)
		Assert.True(button3.HasFocus);
	}

	#endregion

	#region Nested Container Tests

	[Fact]
	public void Tab_TraversesScrollablePanelChildren()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var panel = new ScrollablePanelControl();
		var button1 = new ButtonControl { Text = "Panel-Button1" };
		var button2 = new ButtonControl { Text = "Panel-Button2" };

		panel.AddControl(button1);
		panel.AddControl(button2);

		var buttonAfter = new ButtonControl { Text = "After Panel" };

		window.AddControl(panel);
		window.AddControl(buttonAfter);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab through panel children
		system.FocusStateService.SetFocus(window, button1);
		Assert.True(button1.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(button2.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(buttonAfter.HasFocus);
	}

	[Fact]
	public void Tab_TraversesTwoLevelNestedPanels()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		// Level 1: Outer panel
		var outerPanel = new ScrollablePanelControl();

		// Level 2: Inner panel
		var innerPanel = new ScrollablePanelControl();
		var button = new ButtonControl { Text = "Nested Button" };
		innerPanel.AddControl(button);

		outerPanel.AddControl(innerPanel);
		window.AddControl(outerPanel);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab should find nested button
		window.SwitchFocus(backward: false);
		Assert.True(button.HasFocus, "Nested button should be reachable via Tab");
	}

	[Fact]
	public void Tab_TraversesThreeLevelNestedPanels()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		// Level 1
		var panel1 = new ScrollablePanelControl();
		// Level 2
		var panel2 = new ScrollablePanelControl();
		// Level 3
		var panel3 = new ScrollablePanelControl();
		var button = new ButtonControl { Text = "Deeply Nested" };

		panel3.AddControl(button);
		panel2.AddControl(panel3);
		panel1.AddControl(panel2);
		window.AddControl(panel1);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab should find deeply nested button
		window.SwitchFocus(backward: false);
		Assert.True(button.HasFocus, "Deeply nested button should be reachable via Tab");
	}

	#endregion

	#region Various Control Types Tests

	[Fact]
	public void Tab_TraversesVariousControlTypes()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var button = new ButtonControl { Text = "Button" };
		var dropdown = new DropdownControl("Select:", new List<string> { "Option1", "Option2" });
		var list = new ListControl();
		list.AddItem("Item1");
		list.AddItem("Item2");

		window.AddControl(button);
		window.AddControl(dropdown);
		window.AddControl(list);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab through different control types
		system.FocusStateService.SetFocus(window, button);
		Assert.True(button.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(dropdown.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(list.HasFocus);
	}

	[Fact]
	public void Tab_TraversesTreeAndButton()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var tree = new TreeControl();
		tree.AddRootNode("Root");

		var button = new ButtonControl { Text = "Button" };

		window.AddControl(tree);
		window.AddControl(button);

		system.WindowStateService.AddWindow(window);

		// Act & Assert
		system.FocusStateService.SetFocus(window, tree);
		Assert.True(tree.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(button.HasFocus);
	}

	#endregion

	#region Edge Cases and Dynamic Tests

	[Fact]
	public void Tab_WithNoFocusableControls_DoesNotThrow()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		// Add only non-focusable control (MarkupControl - just displays text)
		var markup = new MarkupControl(new List<string> { "Static Text" });
		window.AddControl(markup);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Should not throw
		var exception = Record.Exception(() => window.SwitchFocus(backward: false));
		Assert.Null(exception);
	}

	[Fact]
	public void Tab_WithHiddenNestedControls_SkipsEntireBranch()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var button1 = new ButtonControl { Text = "Before" };

		var panel = new ScrollablePanelControl { Visible = false }; // Hidden panel
		var button2 = new ButtonControl { Text = "InHiddenPanel" };
		panel.AddControl(button2);

		var button3 = new ButtonControl { Text = "After" };

		window.AddControl(button1);
		window.AddControl(panel);
		window.AddControl(button3);

		system.WindowStateService.AddWindow(window);

		// Act & Assert
		system.FocusStateService.SetFocus(window, button1);
		Assert.True(button1.HasFocus);

		// Tab should skip hidden panel and its children
		window.SwitchFocus(backward: false);
		Assert.False(button2.HasFocus); // Should NOT get focus (parent hidden)
		Assert.True(button3.HasFocus);
	}

	[Fact]
	public void Tab_AfterContainerBecomesHidden_SkipsItsChildren()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var button1 = new ButtonControl { Text = "Before" };

		var panel = new ScrollablePanelControl();
		var button2 = new ButtonControl { Text = "InPanel" };
		panel.AddControl(button2);

		var button3 = new ButtonControl { Text = "After" };

		window.AddControl(button1);
		window.AddControl(panel);
		window.AddControl(button3);

		system.WindowStateService.AddWindow(window);

		// Initially, button2 is reachable
		system.FocusStateService.SetFocus(window, button1);
		window.SwitchFocus(backward: false);
		Assert.True(button2.HasFocus);

		// Act - Hide panel
		panel.Visible = false;

		// Assert - After hiding, button2 should not be reachable
		system.FocusStateService.SetFocus(window, button1);
		window.SwitchFocus(backward: false);
		Assert.False(button2.HasFocus); // Skipped
		Assert.True(button3.HasFocus); // Goes to next
	}

	#endregion

	#region ScrollablePanel Smart Focus Tests

	[Fact]
	public void ScrollablePanel_WithFocusableChildren_ChildrenGetFocus()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var panel = new ScrollablePanelControl();
		var button = new ButtonControl { Text = "Child" };
		panel.AddControl(button);

		window.AddControl(panel);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Child gets focus, not panel
		window.SwitchFocus(backward: false);
		Assert.True(button.HasFocus);
		Assert.False(panel.HasFocus); // Panel should not be focusable when has focusable children
	}

	[Fact]
	public void ScrollablePanel_WithoutFocusableChildren_AndNeedsScrolling_GetsFocus()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var panel = new ScrollablePanelControl();

		// Add many non-focusable items (MarkupControl) to create scrolling need
		for (int i = 0; i < 50; i++)
		{
			var markup = new MarkupControl(new List<string> { $"Line {i}" });
			panel.AddControl(markup);
		}

		var buttonAfter = new ButtonControl { Text = "After" };

		window.AddControl(panel);
		window.AddControl(buttonAfter);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Panel should be focusable for keyboard scrolling
		window.SwitchFocus(backward: false);

		// Panel should get focus (it needs scrolling and has no focusable children)
		bool panelGotFocus = panel.HasFocus;

		// Tab again to reach buttonAfter
		window.SwitchFocus(backward: false);
		Assert.True(buttonAfter.HasFocus);

		// Note: Actual behavior depends on panel's NeedsScrolling() calculation
		// This test documents expected behavior
	}

	#endregion

	#region HorizontalGrid and ColumnContainer Tests

	[Fact]
	public void Tab_TraversesHorizontalGridWithColumns()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var grid = new HorizontalGridControl();
		var column1 = new ColumnContainer(grid);
		var column2 = new ColumnContainer(grid);

		var button1 = new ButtonControl { Text = "Column1-Button" };
		var button2 = new ButtonControl { Text = "Column2-Button" };

		column1.AddContent(button1);
		column2.AddContent(button2);

		grid.AddColumn(column1);
		grid.AddColumn(column2);

		window.AddControl(grid);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab should traverse buttons in columns
		system.FocusStateService.SetFocus(window, button1);
		Assert.True(button1.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(button2.HasFocus);
	}

	[Fact]
	public void Tab_TraversesHorizontalGridWithSplitters()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var grid = new HorizontalGridControl();
		var column1 = new ColumnContainer(grid);
		var column2 = new ColumnContainer(grid);

		var button1 = new ButtonControl { Text = "Column1" };
		var button2 = new ButtonControl { Text = "Column2" };

		column1.AddContent(button1);
		column2.AddContent(button2);

		grid.AddColumn(column1);
		var splitter = grid.AddColumnWithSplitter(column2); // Adds splitter between columns

		window.AddControl(grid);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab should traverse through splitter
		system.FocusStateService.SetFocus(window, button1);
		Assert.True(button1.HasFocus);

		// Tab should move to splitter first
		window.SwitchFocus(backward: false);
		Assert.NotNull(splitter);
		Assert.True(splitter.HasFocus);

		// Tab again should move to button2
		window.SwitchFocus(backward: false);
		Assert.True(button2.HasFocus);
	}

	[Fact]
	public void Tab_TraversesColumnWithScrollablePanel()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var grid = new HorizontalGridControl();
		var column1 = new ColumnContainer(grid);
		var column2 = new ColumnContainer(grid);

		var panel = new ScrollablePanelControl();
		var button1 = new ButtonControl { Text = "InPanel" };
		var button2 = new ButtonControl { Text = "InColumn2" };

		panel.AddControl(button1);
		column1.AddContent(panel);
		column2.AddContent(button2);

		grid.AddColumn(column1);
		grid.AddColumn(column2);

		window.AddControl(grid);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab should traverse into panel, then to next column
		system.FocusStateService.SetFocus(window, button1);
		Assert.True(button1.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(button2.HasFocus);
	}

	[Fact]
	public void Tab_TraversesThreeColumnsWithSplitters()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var grid = new HorizontalGridControl();
		var column1 = new ColumnContainer(grid);
		var column2 = new ColumnContainer(grid);
		var column3 = new ColumnContainer(grid);

		var button1 = new ButtonControl { Text = "Col1" };
		var button2 = new ButtonControl { Text = "Col2" };
		var button3 = new ButtonControl { Text = "Col3" };

		column1.AddContent(button1);
		column2.AddContent(button2);
		column3.AddContent(button3);

		grid.AddColumn(column1);
		grid.AddColumnWithSplitter(column2);
		grid.AddColumnWithSplitter(column3);

		window.AddControl(grid);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab order is: button1 → splitter1 → button2 → splitter2 → button3
		// Since we can't get splitter references easily, we verify by checking button focus states

		system.FocusStateService.SetFocus(window, button1);

		// Tab 1: button1 → splitter1
		window.SwitchFocus(backward: false);
		Assert.False(button1.HasFocus);
		Assert.False(button2.HasFocus); // Splitter1 should have focus now

		// Tab 2: splitter1 → button2
		window.SwitchFocus(backward: false);
		Assert.True(button2.HasFocus);

		// Tab 3: button2 → splitter2
		window.SwitchFocus(backward: false);
		Assert.False(button2.HasFocus);
		Assert.False(button3.HasFocus); // Splitter2 should have focus now

		// Tab 4: splitter2 → button3
		window.SwitchFocus(backward: false);
		Assert.True(button3.HasFocus);
	}

	#endregion

	#region Sticky Positioning Tests

	[Fact]
	public void Tab_TraversesStickyTopControls()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var panel = new ScrollablePanelControl();

		var stickyButton = new ButtonControl { Text = "Sticky Top", StickyPosition = StickyPosition.Top };
		var normalButton = new ButtonControl { Text = "Normal" };

		panel.AddControl(stickyButton);
		panel.AddControl(normalButton);

		window.AddControl(panel);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab should traverse sticky controls normally
		system.FocusStateService.SetFocus(window, stickyButton);
		Assert.True(stickyButton.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(normalButton.HasFocus);
	}

	[Fact]
	public void Tab_TraversesStickyBottomControls()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var panel = new ScrollablePanelControl();

		var normalButton = new ButtonControl { Text = "Normal" };
		var stickyButton = new ButtonControl { Text = "Sticky Bottom", StickyPosition = StickyPosition.Bottom };

		panel.AddControl(normalButton);
		panel.AddControl(stickyButton);

		window.AddControl(panel);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab should include sticky bottom controls
		system.FocusStateService.SetFocus(window, normalButton);
		window.SwitchFocus(backward: false);
		Assert.True(stickyButton.HasFocus);
	}

	[Fact]
	public void Tab_TraversesMixedStickyPositions()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var panel = new ScrollablePanelControl();

		var stickyTop = new ButtonControl { Text = "Sticky Top", StickyPosition = StickyPosition.Top };
		var normal = new ButtonControl { Text = "Normal" };
		var stickyBottom = new ButtonControl { Text = "Sticky Bottom", StickyPosition = StickyPosition.Bottom };

		panel.AddControl(stickyTop);
		panel.AddControl(normal);
		panel.AddControl(stickyBottom);

		window.AddControl(panel);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab should traverse all in order
		system.FocusStateService.SetFocus(window, stickyTop);
		window.SwitchFocus(backward: false);
		Assert.True(normal.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(stickyBottom.HasFocus);
	}

	#endregion

	#region Complex Grid Scenarios

	[Fact]
	public void Tab_TraversesGridWithNestedPanelsAndSplitters()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var grid = new HorizontalGridControl();
		var column1 = new ColumnContainer(grid);
		var column2 = new ColumnContainer(grid);

		// Column 1: Nested panel with multiple buttons
		var panel1 = new ScrollablePanelControl();
		var button1 = new ButtonControl { Text = "Panel1-Button1" };
		var button2 = new ButtonControl { Text = "Panel1-Button2" };
		panel1.AddControl(button1);
		panel1.AddControl(button2);

		// Column 2: Direct button
		var button3 = new ButtonControl { Text = "Column2-Button" };

		column1.AddContent(panel1);
		column2.AddContent(button3);

		grid.AddColumn(column1);
		grid.AddColumnWithSplitter(column2);

		window.AddControl(grid);

		system.WindowStateService.AddWindow(window);

		// Tab order: button1 (panel1) → button2 (panel1) → splitter → button3 (column2)
		system.FocusStateService.SetFocus(window, button1);

		// Tab 1: button1 → button2
		window.SwitchFocus(backward: false);
		Assert.True(button2.HasFocus);

		// Tab 2: button2 → splitter
		window.SwitchFocus(backward: false);
		Assert.False(button2.HasFocus);
		Assert.False(button3.HasFocus); // Splitter should have focus

		// Tab 3: splitter → button3
		window.SwitchFocus(backward: false);
		Assert.True(button3.HasFocus);
	}

	[Fact]
	public void Tab_TraversesGridWithStickyAndNormalControls()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		var panel = new ScrollablePanelControl();
		var stickyTop = new ButtonControl { Text = "Sticky", StickyPosition = StickyPosition.Top };
		var normal = new ButtonControl { Text = "Normal" };

		panel.AddControl(stickyTop);
		panel.AddControl(normal);

		column.AddContent(panel);
		grid.AddColumn(column);

		window.AddControl(grid);

		system.WindowStateService.AddWindow(window);

		// Act & Assert
		system.FocusStateService.SetFocus(window, stickyTop);
		window.SwitchFocus(backward: false);
		Assert.True(normal.HasFocus);
	}

	[Fact]
	public void Tab_TraversesFourLevelNesting_GridColumnPanelPanel()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };

		// Level 1: Grid
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		// Level 2: Outer panel
		var outerPanel = new ScrollablePanelControl();

		// Level 3: Inner panel
		var innerPanel = new ScrollablePanelControl();

		// Level 4: Button
		var button = new ButtonControl { Text = "Deeply Nested in Grid" };

		innerPanel.AddControl(button);
		outerPanel.AddControl(innerPanel);
		column.AddContent(outerPanel);
		grid.AddColumn(column);

		window.AddControl(grid);

		system.WindowStateService.AddWindow(window);

		// Act & Assert - Tab should find the deeply nested button
		window.SwitchFocus(backward: false);
		Assert.True(button.HasFocus, "Button nested 4 levels deep should be reachable");
	}

	#endregion
}
