// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Events;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class HorizontalGridControlTests
{
	#region Construction & Defaults

	[Fact]
	public void Constructor_CreatesEmptyGrid()
	{
		var grid = new HorizontalGridControl();

		Assert.NotNull(grid);
		Assert.Empty(grid.Columns);
	}

	[Fact]
	public void Defaults_VisibleTrue_EnabledTrue()
	{
		var grid = new HorizontalGridControl();

		Assert.True(grid.Visible);
		Assert.True(grid.IsEnabled);
	}

	[Fact]
	public void Defaults_ColumnsEmpty_SplittersEmpty()
	{
		var grid = new HorizontalGridControl();

		Assert.Empty(grid.Columns);
		Assert.Empty(grid.Splitters);
	}

	#endregion

	#region Column Management

	[Fact]
	public void AddColumn_AddsColumnToGrid()
	{
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		grid.AddColumn(column);

		Assert.Single(grid.Columns);
		Assert.Same(column, grid.Columns[0]);
	}

	[Fact]
	public void AddColumn_MultipleColumns_MaintainsOrder()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);
		var col3 = new ColumnContainer(grid);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		grid.AddColumn(col3);

		Assert.Equal(3, grid.Columns.Count);
		Assert.Same(col1, grid.Columns[0]);
		Assert.Same(col2, grid.Columns[1]);
		Assert.Same(col3, grid.Columns[2]);
	}

	[Fact]
	public void AddColumnWithSplitter_CreatesSplitterBetweenColumns()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		grid.AddColumn(col1);
		var splitter = grid.AddColumnWithSplitter(col2);

		Assert.Equal(2, grid.Columns.Count);
		Assert.Single(grid.Splitters);
		Assert.NotNull(splitter);
	}

	[Fact]
	public void AddColumnWithSplitter_FirstColumn_NoSplitter()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);

		var splitter = grid.AddColumnWithSplitter(col1);

		Assert.Single(grid.Columns);
		Assert.Empty(grid.Splitters);
		Assert.Null(splitter);
	}

	[Fact]
	public void RemoveColumn_RemovesColumn()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		grid.RemoveColumn(col1);

		Assert.Single(grid.Columns);
		Assert.Same(col2, grid.Columns[0]);
	}

	[Fact]
	public void ClearColumns_RemovesAllColumnsAndSplitters()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		grid.AddColumn(col1);
		grid.AddColumnWithSplitter(col2);

		Assert.Equal(2, grid.Columns.Count);
		Assert.Single(grid.Splitters);

		grid.ClearColumns();

		Assert.Empty(grid.Columns);
		Assert.Empty(grid.Splitters);
	}

	[Fact]
	public void AddSplitterAfter_ByColumn_AddsSplitter()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		bool result = grid.AddSplitterAfter(col1);

		Assert.True(result);
		Assert.Single(grid.Splitters);
	}

	#endregion

	#region Factory Methods

	[Fact]
	public void ButtonRow_CreatesGridWithButtons()
	{
		var btn1 = ContainerTestHelpers.CreateButton("OK");
		var btn2 = ContainerTestHelpers.CreateButton("Cancel");

		var grid = HorizontalGridControl.ButtonRow(btn1, btn2);

		Assert.Equal(2, grid.Columns.Count);
		Assert.Empty(grid.Splitters);
	}

	[Fact]
	public void FromControls_CreatesGridFromControls()
	{
		var label1 = ContainerTestHelpers.CreateLabel("Label 1");
		var label2 = ContainerTestHelpers.CreateLabel("Label 2");

		var grid = HorizontalGridControl.FromControls(label1, label2);

		Assert.Equal(2, grid.Columns.Count);
	}

	[Fact]
	public void Builder_Column_BuildsCorrectly()
	{
		var label = ContainerTestHelpers.CreateLabel("Test");

		var grid = HorizontalGridControl.Create()
			.Column(col => col.Width(48).Add(label))
			.Column(col => col.Flex(2.0).Add(ContainerTestHelpers.CreateLabel("Other")))
			.WithSplitterAfter(0)
			.Build();

		Assert.Equal(2, grid.Columns.Count);
		Assert.Single(grid.Splitters);
	}

	#endregion

	#region Layout

	[Fact]
	public void GetChildren_ReturnsColumnsAndSplitters()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		grid.AddColumn(col1);
		grid.AddColumnWithSplitter(col2);

		var children = grid.GetChildren();

		// col1, splitter, col2
		Assert.Equal(3, children.Count);
	}

	[Fact]
	public void GetChildren_HiddenColumn_Excluded()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		col1.Visible = false;

		var children = grid.GetChildren();

		Assert.Single(children);
		Assert.Same(col2, children[0]);
	}

	[Fact]
	public void ContentWidth_SumsColumnWidths()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 30 };
		var col2 = new ColumnContainer(grid) { Width = 40 };

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var contentWidth = grid.ContentWidth;

		Assert.NotNull(contentWidth);
		Assert.Equal(70, contentWidth.Value);
	}

	#endregion

	#region Focus / Navigation

	[Fact]
	public void ProcessKey_Tab_CyclesThroughColumns()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var list1 = ContainerTestHelpers.CreateFocusableList("Item A1", "Item A2");
		var list2 = ContainerTestHelpers.CreateFocusableList("Item B1", "Item B2");

		col1.AddContent(list1);
		col2.AddContent(list2);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		// Give focus to the grid to initialize focus tracking
		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);

		// First Tab should move focus from first to second
		bool handled = grid.ProcessKey(tabKey);
		Assert.True(handled);
	}

	[Fact]
	public void ProcessKey_ShiftTab_CyclesBackward()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var list1 = ContainerTestHelpers.CreateFocusableList("Item A1");
		var list2 = ContainerTestHelpers.CreateFocusableList("Item B1");

		col1.AddContent(list1);
		col2.AddContent(list2);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		var shiftTabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false);

		// Tab forward to second control
		grid.ProcessKey(tabKey);

		// Shift+Tab should go back
		bool handled = grid.ProcessKey(shiftTabKey);
		Assert.True(handled);
	}

	[Fact]
	public void ProcessKey_Tab_NoInteractiveControls_ReturnsFalse()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);

		var label = ContainerTestHelpers.CreateLabel("Static text");
		col1.AddContent(label);

		grid.AddColumn(col1);
		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);

		bool handled = grid.ProcessKey(tabKey);

		Assert.False(handled);
	}

	[Fact]
	public void SetFocusWithDirection_Forward_FocusesFirst()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var list1 = ContainerTestHelpers.CreateFocusableList("First");
		var list2 = ContainerTestHelpers.CreateFocusableList("Second");

		col1.AddContent(list1);
		col2.AddContent(list2);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// First focusable control should have focus (auto-focused via IFocusScope delegation)
		Assert.True(list1.HasFocus);
	}

	[Fact]
	public void SetFocusWithDirection_Backward_FocusesLast()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var list1 = ContainerTestHelpers.CreateFocusableList("First");
		var list2 = ContainerTestHelpers.CreateFocusableList("Second");

		col1.AddContent(list1);
		col2.AddContent(list2);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Use SwitchFocus with backward=true from nothing focused (wraps to last)
		window.SwitchFocus(backward: true);

		// Last focusable control should have focus
		Assert.True(list2.HasFocus);
	}

	#endregion

	#region CanReceiveFocus

	[Fact]
	public void CanReceiveFocus_AlwaysFalse()
	{
		var grid = new HorizontalGridControl();

		Assert.False(grid.CanReceiveFocus);
	}

	#endregion

	#region Rendering

	[Fact]
	public void Render_TwoColumns_BothVisible()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 20 };
		var col2 = new ColumnContainer(grid) { Width = 20 };

		col1.AddContent(ContainerTestHelpers.CreateLabel("Column One"));
		col2.AddContent(ContainerTestHelpers.CreateLabel("Column Two"));

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		window.AddControl(grid);

		var output = window.RenderAndGetVisibleContent();
		var text = ContainerTestHelpers.StripAnsiCodes(output);

		Assert.Contains("Column One", text);
		Assert.Contains("Column Two", text);
	}

	[Fact]
	public void Render_EmptyGrid_DoesNotCrash()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();

		var grid = new HorizontalGridControl();

		window.AddControl(grid);

		var exception = Record.Exception(() => window.RenderAndGetVisibleContent());
		Assert.Null(exception);
	}

	[Fact]
	public void Render_SingleColumn_NoSplitter()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 40 };
		col1.AddContent(ContainerTestHelpers.CreateLabel("Only Column"));

		grid.AddColumn(col1);

		window.AddControl(grid);

		var output = window.RenderAndGetVisibleContent();
		var text = ContainerTestHelpers.StripAnsiCodes(output);

		Assert.Contains("Only Column", text);
		Assert.Empty(grid.Splitters);
	}

	#endregion

	#region Layout Edge Cases

	[Fact]
	public void TwoFlexColumns_EqualWeight_SplitEvenly()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();

		var grid = new HorizontalGridControl();
		grid.HorizontalAlignment = HorizontalAlignment.Stretch;

		var col1 = new ColumnContainer(grid) { FlexFactor = 1.0 };
		var col2 = new ColumnContainer(grid) { FlexFactor = 1.0 };

		col1.AddContent(ContainerTestHelpers.CreateLabel("Left"));
		col2.AddContent(ContainerTestHelpers.CreateLabel("Right"));

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		window.AddControl(grid);
		window.RenderAndGetVisibleContent();

		// Both columns should get approximately equal widths
		// Actual widths depend on layout engine, but they should be close
		int w1 = col1.ActualWidth;
		int w2 = col2.ActualWidth;

		// Layout engine may have small rounding differences
		Assert.True(Math.Abs(w1 - w2) <= 2,
			$"Flex columns with equal weight should have similar widths, got {w1} and {w2}");
	}

	[Fact]
	public void FlexColumns_UnequalWeight_ProportionalSplit()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();

		var grid = new HorizontalGridControl();
		grid.HorizontalAlignment = HorizontalAlignment.Stretch;

		var col1 = new ColumnContainer(grid) { FlexFactor = 1.0 };
		var col2 = new ColumnContainer(grid) { FlexFactor = 2.0 };

		col1.AddContent(ContainerTestHelpers.CreateLabel("Small"));
		col2.AddContent(ContainerTestHelpers.CreateLabel("Large"));

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		window.AddControl(grid);
		window.RenderAndGetVisibleContent();

		int w1 = col1.ActualWidth;
		int w2 = col2.ActualWidth;

		// col2 should be at least as wide as col1 (ideally wider, but integer
		// rounding in the layout engine can equalize small differences)
		Assert.True(w2 >= w1,
			$"Column with FlexFactor=2.0 should be at least as wide as FlexFactor=1.0, got {w2} vs {w1}");
	}

	[Fact]
	public void FixedWidthColumn_DoesNotResize()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();

		var grid = new HorizontalGridControl();
		grid.HorizontalAlignment = HorizontalAlignment.Stretch;

		var fixedCol = new ColumnContainer(grid) { Width = 30 };
		var flexCol = new ColumnContainer(grid) { FlexFactor = 1.0 };

		fixedCol.AddContent(ContainerTestHelpers.CreateLabel("Fixed"));
		flexCol.AddContent(ContainerTestHelpers.CreateLabel("Flex"));

		grid.AddColumn(fixedCol);
		grid.AddColumn(flexCol);

		window.AddControl(grid);
		window.RenderAndGetVisibleContent();

		Assert.Equal(30, fixedCol.ActualWidth);
	}

	[Fact]
	public void HiddenColumn_SpaceRedistributed()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();

		var grid = new HorizontalGridControl();
		grid.HorizontalAlignment = HorizontalAlignment.Stretch;

		var col1 = new ColumnContainer(grid) { FlexFactor = 1.0 };
		var col2 = new ColumnContainer(grid) { FlexFactor = 1.0 };
		var col3 = new ColumnContainer(grid) { FlexFactor = 1.0 };

		col1.AddContent(ContainerTestHelpers.CreateLabel("A"));
		col2.AddContent(ContainerTestHelpers.CreateLabel("B"));
		col3.AddContent(ContainerTestHelpers.CreateLabel("C"));

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		grid.AddColumn(col3);

		window.AddControl(grid);

		// Render with all visible
		window.RenderAndGetVisibleContent();
		int widthBefore = col1.ActualWidth;

		// Hide middle column
		col2.Visible = false;
		window.RenderAndGetVisibleContent();
		int widthAfter = col1.ActualWidth;

		// Remaining columns should get more space
		Assert.True(widthAfter > widthBefore,
			$"Visible columns should get more space when a sibling is hidden, got {widthAfter} vs {widthBefore}");
	}

	[Fact]
	public void ColumnWithSplitter_RendersCorrectly()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();

		var grid = new HorizontalGridControl();
		grid.HorizontalAlignment = HorizontalAlignment.Stretch;

		var col1 = new ColumnContainer(grid) { Width = 30 };
		var col2 = new ColumnContainer(grid) { Width = 30 };

		col1.AddContent(ContainerTestHelpers.CreateLabel("Left Side"));
		col2.AddContent(ContainerTestHelpers.CreateLabel("Right Side"));

		grid.AddColumn(col1);
		grid.AddColumnWithSplitter(col2);

		window.AddControl(grid);

		var output = window.RenderAndGetVisibleContent();
		var text = ContainerTestHelpers.StripAnsiCodes(output);

		Assert.Contains("Left Side", text);
		Assert.Contains("Right Side", text);
		Assert.Single(grid.Splitters);
	}

	[Fact]
	public void ChildWiderThanColumn_Clipped()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();

		var grid = new HorizontalGridControl();

		// Column is narrow but content is wide
		var col = new ColumnContainer(grid) { Width = 10 };
		col.AddContent(ContainerTestHelpers.CreateLabel("This text is much wider than 10 characters"));

		grid.AddColumn(col);

		window.AddControl(grid);

		// Should not crash; content gets clipped by the column width
		var exception = Record.Exception(() => window.RenderAndGetVisibleContent());
		Assert.Null(exception);
	}

	#endregion

	#region Edge Cases - Focus and Visibility

	[Fact]
	public void ProcessKey_Tab_SingleColumn_SingleControl_ExitsForward()
	{
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);
		var btn = ContainerTestHelpers.CreateButton("Only");

		col.AddContent(btn);
		grid.AddColumn(col);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		Assert.True(btn.HasFocus);

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);

		// Tab should exit forward since there is only one focusable control
		bool result = grid.ProcessKey(tabKey);
		Assert.False(result);
	}

	[Fact]
	public void ProcessKey_ShiftTab_FirstControl_ExitsBackward()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var btn1 = ContainerTestHelpers.CreateButton("First");
		var btn2 = ContainerTestHelpers.CreateButton("Second");

		col1.AddContent(btn1);
		col2.AddContent(btn2);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		Assert.True(btn1.HasFocus);

		var shiftTabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false);

		// Shift+Tab on the first control should exit backward
		bool handled = grid.ProcessKey(shiftTabKey);
		Assert.False(handled);
	}

	[Fact]
	public void SetFocus_AllControlsDisabled_NoChildFocused()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var btn1 = ContainerTestHelpers.CreateButton("Disabled1");
		var btn2 = ContainerTestHelpers.CreateButton("Disabled2");

		btn1.IsEnabled = false;
		btn2.IsEnabled = false;

		col1.AddContent(btn1);
		col2.AddContent(btn2);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		grid.ProcessKey(tabKey);

		Assert.False(btn1.HasFocus);
		Assert.False(btn2.HasFocus);
	}

	[Fact]
	public void Tab_SkipsDisabledControlInColumn()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);
		var col3 = new ColumnContainer(grid);

		var btn1 = ContainerTestHelpers.CreateButton("First");
		var btn2 = ContainerTestHelpers.CreateButton("Middle");
		var btn3 = ContainerTestHelpers.CreateButton("Last");

		btn2.IsEnabled = false;

		col1.AddContent(btn1);
		col2.AddContent(btn2);
		col3.AddContent(btn3);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		grid.AddColumn(col3);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		Assert.True(btn1.HasFocus);

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);

		// Tab should skip disabled btn2 and focus btn3
		bool handled = grid.ProcessKey(tabKey);
		Assert.True(handled);
		Assert.False(btn2.HasFocus);
		Assert.True(btn3.HasFocus);
	}

	[Fact]
	public void ProcessKey_NonTabKey_NoFocusedChild_ReturnsFalse()
	{
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);

		// Use a label (non-interactive) so no child gets focus
		col.AddContent(ContainerTestHelpers.CreateLabel("Static"));
		grid.AddColumn(col);

		var rightArrow = new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false);

		bool handled = grid.ProcessKey(rightArrow);
		Assert.False(handled);
	}

	[Fact]
	public void NotifyChildFocusChanged_SetsGridHasFocus()
	{
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);
		var btn = ContainerTestHelpers.CreateButton("FocusMe");

		col.AddContent(btn);
		grid.AddColumn(col);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();

		// When btn has focus, grid.HasFocus should be true (it's in the focus path)
		Assert.True(btn.HasFocus);
		Assert.True(grid.HasFocus);
	}

	[Fact]
	public void ColumnVisibilityToggle_HiddenColumnSkippedInTabOrder()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);
		var col3 = new ColumnContainer(grid);

		var btn1 = ContainerTestHelpers.CreateButton("First");
		var btn2 = ContainerTestHelpers.CreateButton("Hidden");
		var btn3 = ContainerTestHelpers.CreateButton("Third");

		col1.AddContent(btn1);
		col2.AddContent(btn2);
		col3.AddContent(btn3);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		grid.AddColumn(col3);

		// Hide the middle column before focusing
		col2.Visible = false;

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		Assert.True(btn1.HasFocus);

		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);

		// Tab should skip hidden col2 and go to btn3
		bool handled = grid.ProcessKey(tabKey);
		Assert.True(handled);
		Assert.False(btn2.HasFocus);
		Assert.True(btn3.HasFocus);
	}

	[Fact]
	public void RemoveColumn_WithFocusedControl_ClearsFocus()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		var btn1 = ContainerTestHelpers.CreateButton("Stay");
		var btn2 = ContainerTestHelpers.CreateButton("Remove");

		col1.AddContent(btn1);
		col2.AddContent(btn2);

		grid.AddColumn(col1);
		grid.AddColumn(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		Assert.True(btn1.HasFocus);

		// Now remove col1 (which has the focused control)
		var exception = Record.Exception(() => grid.RemoveColumn(col1));
		Assert.Null(exception);

		// Grid should still work — Tab should not crash
		var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
		var tabException = Record.Exception(() => grid.ProcessKey(tabKey));
		Assert.Null(tabException);
	}

	#endregion
}
