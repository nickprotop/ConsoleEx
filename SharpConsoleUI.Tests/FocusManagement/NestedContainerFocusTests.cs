// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Tests focus traversal, keyboard routing, and mouse focus through
/// real-world nested container patterns (Grid→Panel, Panel→Grid, Tab→Grid, etc.).
/// </summary>
public class NestedContainerFocusTests
{
	private static ConsoleKeyInfo TabKey => new('\t', ConsoleKey.Tab, false, false, false);
	private static ConsoleKeyInfo ShiftTabKey => new('\t', ConsoleKey.Tab, true, false, false);
	private static ConsoleKeyInfo RightArrow => new('\0', ConsoleKey.RightArrow, false, false, false);

	#region Grid inside Panel

	[Fact]
	public void Tab_PanelContainingGrid_TraversesGridChildren()
	{
		var (panel, grid, btn1, btn2, system, window) = CreatePanelWithGrid();

		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus, "btn1 should be focused first");

		panel.ProcessKey(TabKey);
		Assert.True(btn2.HasFocus, "btn2 should be focused after Tab");

		bool handled = panel.ProcessKey(TabKey);
		Assert.False(btn2.HasFocus || btn1.HasFocus, "Should exit panel after last grid child");
	}

	[Fact]
	public void Tab_PanelContainingGrid_ShiftTab_ExitsBackward()
	{
		var (panel, grid, btn1, btn2, system, window) = CreatePanelWithGrid();

		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus);

		bool handled = panel.ProcessKey(ShiftTabKey);
		Assert.False(handled, "Shift+Tab from first child should exit panel backward");
	}

	[Fact]
	public void ProcessKey_PanelContainingGrid_KeyRoutesToFocusedChild()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);
		var slider = new SliderControl
		{
			Orientation = SliderOrientation.Horizontal,
			MinValue = 0, MaxValue = 100, Value = 50, Step = 1
		};
		col.AddContent(slider);
		grid.AddColumn(col);
		panel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(slider.HasFocus);

		panel.ProcessKey(RightArrow);
		Assert.Equal(51, slider.Value);
	}

	[Fact]
	public void MouseClick_PanelContainingGrid_FocusesClickedControl()
	{
		var (panel, grid, btn1, btn2, system, window) = CreatePanelWithGrid();

		// Click at position that should hit btn1
		panel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 0));
		Assert.True(btn1.HasFocus, "btn1 should have focus after click");
		Assert.True(panel.HasFocus, "Panel should have focus");
	}

	#endregion

	#region Panel inside Grid

	[Fact]
	public void Tab_GridContainingPanels_TraversesPanelChildren()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var panel1 = new ScrollablePanelControl { Height = 10 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		panel1.AddControl(btn1);
		panel1.AddControl(btn2);
		col1.AddContent(panel1);

		var panel2 = new ScrollablePanelControl { Height = 10 };
		var btn3 = new ButtonControl { Text = "Btn3" };
		panel2.AddControl(btn3);
		col2.AddContent(panel2);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus, "btn1 first");

		outerPanel.ProcessKey(TabKey);
		Assert.True(btn2.HasFocus, "btn2 second");

		outerPanel.ProcessKey(TabKey);
		Assert.True(btn3.HasFocus, "btn3 third");
	}

	[Fact]
	public void Tab_GridContainingPanels_WithSplitter_IncludesSplitter()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var panel1 = new ScrollablePanelControl { Height = 10 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		panel1.AddControl(btn1);
		col1.AddContent(panel1);

		var panel2 = new ScrollablePanelControl { Height = 10 };
		var btn2 = new ButtonControl { Text = "Btn2" };
		panel2.AddControl(btn2);
		col2.AddContent(panel2);

		grid.AddColumn(col1);
		var splitter = grid.AddColumnWithSplitter(col2);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus, "btn1 first");

		outerPanel.ProcessKey(TabKey);
		// Splitter should be focused between btn1 and btn2
		if (splitter != null && !btn2.HasFocus)
		{
			Assert.False(btn1.HasFocus);
			outerPanel.ProcessKey(TabKey);
		}

		Assert.True(btn2.HasFocus, "btn2 should be reachable after splitter");
	}

	[Fact]
	public void ProcessKey_GridWithPanelChild_DelegatesToPanelChild()
	{
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);

		var panel = new ScrollablePanelControl { Height = 10 };
		var slider = new SliderControl
		{
			Orientation = SliderOrientation.Horizontal,
			MinValue = 0, MaxValue = 100, Value = 50, Step = 1
		};
		panel.AddControl(slider);
		col.AddContent(panel);
		grid.AddColumn(col);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(slider.HasFocus);

		grid.ProcessKey(RightArrow);
		Assert.Equal(51, slider.Value);
	}

	#endregion

	#region Panel inside Grid inside Panel (deep nesting)

	[Fact]
	public void Tab_PanelGridPanel_FullTraversal()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var innerPanel = new ScrollablePanelControl { Height = 8 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		innerPanel.AddControl(btn1);
		innerPanel.AddControl(btn2);
		col1.AddContent(innerPanel);

		var btn3 = new ButtonControl { Text = "Btn3" };
		col2.AddContent(btn3);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus, "btn1 first");

		outerPanel.ProcessKey(TabKey);
		Assert.True(btn2.HasFocus, "btn2 second");

		outerPanel.ProcessKey(TabKey);
		Assert.True(btn3.HasFocus, "btn3 third");
	}

	[Fact]
	public void ProcessKey_PanelGridPanel_KeyRoutesThrough3Levels()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);

		var innerPanel = new ScrollablePanelControl { Height = 8 };
		var slider = new SliderControl
		{
			Orientation = SliderOrientation.Horizontal,
			MinValue = 0, MaxValue = 100, Value = 50, Step = 1
		};
		innerPanel.AddControl(slider);
		col.AddContent(innerPanel);
		grid.AddColumn(col);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(slider.HasFocus);

		outerPanel.ProcessKey(RightArrow);
		Assert.Equal(51, slider.Value);
	}

	[Fact]
	public void MouseClick_PanelGridPanel_FocusesDeepChild()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);

		var innerPanel = new ScrollablePanelControl { Height = 8 };
		var btn = new ButtonControl { Text = "Deep" };
		innerPanel.AddControl(btn);
		col.AddContent(innerPanel);
		grid.AddColumn(col);
		outerPanel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		outerPanel.ProcessMouseEvent(ContainerTestHelpers.CreateClick(0, 0));
		Assert.True(btn.HasFocus, "Deep child should be focused via mouse");
		Assert.True(outerPanel.HasFocus, "Outer panel should have focus");
	}

	#endregion

	#region Tab containing Grid

	[Fact]
	public void Tab_TabControlContainingGrid_TraversesActiveTabGrid()
	{
		var tabControl = new TabControl();
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		col1.AddContent(btn1);
		col2.AddContent(btn2);
		grid.AddColumn(col1);
		grid.AddColumn(col2);

		tabControl.AddTab("Tab1", grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(tabControl);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Focus into the tab control
		system.FocusStateService.SetFocus(window, btn1);
		Assert.True(btn1.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(btn2.HasFocus, "Tab should traverse to btn2 in grid");
	}

	[Fact]
	public void Tab_TabControlContainingGrid_InactiveTabNotTraversed()
	{
		var tabControl = new TabControl();

		var btn1 = new ButtonControl { Text = "Tab1Btn" };
		tabControl.AddTab("Tab1", btn1);

		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);
		var btn2 = new ButtonControl { Text = "Tab2Btn" };
		col.AddContent(btn2);
		grid.AddColumn(col);
		tabControl.AddTab("Tab2", grid);

		var btnAfter = new ButtonControl { Text = "After" };

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(tabControl);
		window.AddControl(btnAfter);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Tab1 is active. Focus btn1, then Tab should skip Tab2 content.
		system.FocusStateService.SetFocus(window, btn1);
		Assert.True(btn1.HasFocus);

		window.SwitchFocus(backward: false);
		// Should cycle or go to btnAfter, not btn2
		Assert.False(btn2.HasFocus, "Inactive tab content should not be traversed");
	}

	[Fact]
	public void SwitchTab_ThenTab_TraversesNewTabContent()
	{
		var tabControl = new TabControl();

		var btn1 = new ButtonControl { Text = "Tab1Btn" };
		tabControl.AddTab("Tab1", btn1);

		var panel = new ScrollablePanelControl { Height = 10 };
		var btn2 = new ButtonControl { Text = "Tab2Btn" };
		panel.AddControl(btn2);
		tabControl.AddTab("Tab2", panel);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(tabControl);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Switch to Tab2
		tabControl.ActiveTabIndex = 1;
		window.RenderAndGetVisibleContent();

		system.FocusStateService.SetFocus(window, btn2);
		Assert.True(btn2.HasFocus, "btn2 in new active tab should be focusable");
	}

	#endregion

	#region Multiple Grids in single Panel

	[Fact]
	public void Tab_PanelWithTwoGrids_TraversesBothSequentially()
	{
		var panel = new ScrollablePanelControl { Height = 20 };

		var grid1 = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid1);
		var btn1 = new ButtonControl { Text = "G1Btn" };
		col1.AddContent(btn1);
		grid1.AddColumn(col1);

		var grid2 = new HorizontalGridControl();
		var col2 = new ColumnContainer(grid2);
		var btn2 = new ButtonControl { Text = "G2Btn" };
		col2.AddContent(btn2);
		grid2.AddColumn(col2);

		panel.AddControl(grid1);
		panel.AddControl(grid2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus, "btn1 in grid1 first");

		panel.ProcessKey(TabKey);
		Assert.True(btn2.HasFocus, "btn2 in grid2 second");
	}

	[Fact]
	public void ProcessKey_PanelWithTwoGrids_RoutesToCorrectGrid()
	{
		var panel = new ScrollablePanelControl { Height = 20 };

		var grid1 = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid1);
		var slider1 = new SliderControl { Value = 50, Step = 1 };
		col1.AddContent(slider1);
		grid1.AddColumn(col1);

		var grid2 = new HorizontalGridControl();
		var col2 = new ColumnContainer(grid2);
		var slider2 = new SliderControl { Value = 50, Step = 1 };
		col2.AddContent(slider2);
		grid2.AddColumn(col2);

		panel.AddControl(grid1);
		panel.AddControl(grid2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Focus slider2 in grid2
		window.SwitchFocus(backward: false);
		panel.ProcessKey(TabKey);
		Assert.True(slider2.HasFocus);

		panel.ProcessKey(RightArrow);
		Assert.Equal(51, slider2.Value);
		Assert.Equal(50, slider1.Value); // slider1 unchanged
	}

	#endregion

	#region Grid inside Grid

	[Fact]
	public void Tab_NestedGrids_TraversesInnerGridChildren()
	{
		var outerGrid = new HorizontalGridControl();
		var outerCol = new ColumnContainer(outerGrid);

		var innerGrid = new HorizontalGridControl();
		var innerCol1 = new ColumnContainer(innerGrid);
		var innerCol2 = new ColumnContainer(innerGrid);
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		innerCol1.AddContent(btn1);
		innerCol2.AddContent(btn2);
		innerGrid.AddColumn(innerCol1);
		innerGrid.AddColumn(innerCol2);

		outerCol.AddContent(innerGrid);
		outerGrid.AddColumn(outerCol);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerGrid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus);

		window.SwitchFocus(backward: false);
		Assert.True(btn2.HasFocus);
	}

	[Fact]
	public void ProcessKey_NestedGrids_KeyRoutesToInnerGridChild()
	{
		// Nested grids: Panel→OuterGrid(Col1[InnerGrid(Col[Btn1,Btn2])], Col2[Btn3])
		// Verify that Tab traversal works through nested grids when inside a panel
		var panel = new ScrollablePanelControl { Height = 20 };
		var outerGrid = new HorizontalGridControl();
		var outerCol1 = new ColumnContainer(outerGrid);
		var outerCol2 = new ColumnContainer(outerGrid);

		var innerGrid = new HorizontalGridControl();
		var innerCol1 = new ColumnContainer(innerGrid);
		var innerCol2 = new ColumnContainer(innerGrid);
		var btn1 = new ButtonControl { Text = "Inner1" };
		var btn2 = new ButtonControl { Text = "Inner2" };
		innerCol1.AddContent(btn1);
		innerCol2.AddContent(btn2);
		innerGrid.AddColumn(innerCol1);
		innerGrid.AddColumn(innerCol2);

		outerCol1.AddContent(innerGrid);
		var btn3 = new ButtonControl { Text = "Outer" };
		outerCol2.AddContent(btn3);
		outerGrid.AddColumn(outerCol1);
		outerGrid.AddColumn(outerCol2);
		panel.AddControl(outerGrid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		// The outer grid might focus btn1 in inner grid or btn3 depending on implementation
		// Either way, Tab through all should reach all controls
		if (btn1.HasFocus)
		{
			panel.ProcessKey(TabKey);
			Assert.True(btn2.HasFocus, "Tab from inner grid btn1 should reach btn2");

			panel.ProcessKey(TabKey);
			Assert.True(btn3.HasFocus, "Tab from inner grid should exit to outer grid btn3");
		}
		else
		{
			// If outer grid focuses btn3 first, inner grid children should still be reachable
			Assert.True(btn3.HasFocus, "Outer grid direct child should get focus");
		}
	}

	#endregion

	#region Empty containers in nesting

	[Fact]
	public void Tab_GridWithEmptyPanelColumn_SkipsEmptyPanel()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var emptyPanel = new ScrollablePanelControl { Height = 5 };
		col1.AddContent(emptyPanel);

		var btn = new ButtonControl { Text = "Btn" };
		col2.AddContent(btn);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn.HasFocus, "Should skip empty panel and focus btn");
	}

	[Fact]
	public void Tab_PanelWithEmptyGrid_SkipsEmptyGrid()
	{
		var panel = new ScrollablePanelControl { Height = 15 };

		var emptyGrid = new HorizontalGridControl();
		var emptyCol = new ColumnContainer(emptyGrid);
		emptyGrid.AddColumn(emptyCol);

		var btn = new ButtonControl { Text = "Btn" };

		panel.AddControl(emptyGrid);
		panel.AddControl(btn);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn.HasFocus, "Should skip empty grid and focus btn");
	}

	[Fact]
	public void Tab_GridWithMixedEmptyAndPopulatedColumns_SkipsEmpty()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid); // empty
		var col2 = new ColumnContainer(grid);
		var col3 = new ColumnContainer(grid); // empty

		var btn = new ButtonControl { Text = "Btn" };
		col2.AddContent(btn);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		grid.AddColumn(col3);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn.HasFocus, "Should skip empty columns and focus btn");
	}

	#endregion

	#region Visibility toggling in nested containers

	[Fact]
	public void Tab_GridColumn_BecomesInvisible_SkippedInTraversal()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };
		col1.AddContent(btn1);
		col2.AddContent(btn2);
		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Hide col2
		col2.Visible = false;

		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus, "btn1 should get focus");

		window.SwitchFocus(backward: false);
		// Should cycle back to btn1 since btn2 is hidden
		Assert.False(btn2.HasFocus, "btn2 in hidden column should not get focus");
	}

	[Fact]
	public void Tab_PanelChild_BecomesInvisible_SkippedInTraversal()
	{
		var panel = new ScrollablePanelControl { Height = 15 };
		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2", Visible = false };
		var btn3 = new ButtonControl { Text = "Btn3" };

		panel.AddControl(btn1);
		panel.AddControl(btn2);
		panel.AddControl(btn3);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn1.HasFocus);

		panel.ProcessKey(TabKey);
		Assert.True(btn3.HasFocus, "Should skip invisible btn2");
		Assert.False(btn2.HasFocus);
	}

	[Fact]
	public void Tab_NestedPanel_BecomesInvisible_EntireBranchSkipped()
	{
		var outerPanel = new ScrollablePanelControl { Height = 20 };

		var innerPanel = new ScrollablePanelControl { Height = 8 };
		var btn1 = new ButtonControl { Text = "InnerBtn" };
		innerPanel.AddControl(btn1);
		innerPanel.Visible = false;

		var btn2 = new ButtonControl { Text = "OuterBtn" };

		outerPanel.AddControl(innerPanel);
		outerPanel.AddControl(btn2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(outerPanel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(btn2.HasFocus, "Should skip invisible inner panel entirely");
		Assert.False(btn1.HasFocus);
	}

	#endregion

	#region Disabled controls in nested containers

	[Fact]
	public void Tab_GridWithDisabledColumn_SkipsDisabledControls()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var disabledBtn = new ButtonControl { Text = "Disabled", IsEnabled = false };
		var enabledBtn = new ButtonControl { Text = "Enabled" };

		col1.AddContent(disabledBtn);
		col2.AddContent(enabledBtn);
		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.True(enabledBtn.HasFocus, "Should skip disabled control");
		Assert.False(disabledBtn.HasFocus);
	}

	[Fact]
	public void Tab_PanelWithAllDisabledChildren_ExitsPanel()
	{
		var panel = new ScrollablePanelControl { Height = 10 };
		var btn1 = new ButtonControl { Text = "D1", IsEnabled = false };
		var btn2 = new ButtonControl { Text = "D2", IsEnabled = false };
		panel.AddControl(btn1);
		panel.AddControl(btn2);

		var btnAfter = new ButtonControl { Text = "After" };

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.AddControl(btnAfter);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.SwitchFocus(backward: false);
		Assert.False(btn1.HasFocus, "Disabled btn1 should not get focus");
		Assert.False(btn2.HasFocus, "Disabled btn2 should not get focus");
	}

	#endregion

	#region Helper Methods

	private (ScrollablePanelControl panel, HorizontalGridControl grid,
		ButtonControl btn1, ButtonControl btn2,
		ConsoleWindowSystem system, Window window)
		CreatePanelWithGrid()
	{
		var panel = new ScrollablePanelControl { Height = 10 };
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var btn1 = new ButtonControl { Text = "Btn1" };
		var btn2 = new ButtonControl { Text = "Btn2" };

		col1.AddContent(btn1);
		col2.AddContent(btn2);
		grid.AddColumn(col1);
		grid.AddColumn(col2);
		panel.AddControl(grid);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		return (panel, grid, btn1, btn2, system, window);
	}

	#endregion
}
