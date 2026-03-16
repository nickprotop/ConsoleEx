// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests column splitter resizing behavior across different width/alignment
/// combinations and control types. Covers keyboard (Left/Right arrow),
/// constraint enforcement (min width), and event firing.
/// </summary>
public class SplitterControlResizeTests
{
	private static ConsoleKeyInfo LeftArrow => new('\0', ConsoleKey.LeftArrow, false, false, false);
	private static ConsoleKeyInfo RightArrow => new('\0', ConsoleKey.RightArrow, false, false, false);
	private static ConsoleKeyInfo ShiftLeft => new('\0', ConsoleKey.LeftArrow, true, false, false);
	private static ConsoleKeyInfo ShiftRight => new('\0', ConsoleKey.RightArrow, true, false, false);

	#region Helpers

	/// <summary>
	/// Creates a rendered grid with two columns, a splitter between them,
	/// and returns all the pieces for testing.
	/// </summary>
	private (HorizontalGridControl grid, ColumnContainer col1, ColumnContainer col2,
		SplitterControl splitter, ConsoleWindowSystem system, Window window)
		CreateTwoColumnGridWithSplitter(
			int? col1Width = null, int? col2Width = null,
			int? col1MinWidth = null, int? col2MinWidth = null,
			int? col1MaxWidth = null, int? col2MaxWidth = null,
			IWindowControl? col1Content = null, IWindowControl? col2Content = null)
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid);
		var col2 = new ColumnContainer(grid);

		if (col1Width.HasValue) col1.Width = col1Width.Value;
		if (col2Width.HasValue) col2.Width = col2Width.Value;
		if (col1MinWidth.HasValue) col1.MinWidth = col1MinWidth.Value;
		if (col2MinWidth.HasValue) col2.MinWidth = col2MinWidth.Value;
		if (col1MaxWidth.HasValue) col1.MaxWidth = col1MaxWidth.Value;
		if (col2MaxWidth.HasValue) col2.MaxWidth = col2MaxWidth.Value;

		col1.AddContent(col1Content ?? ContainerTestHelpers.CreateButton("Left"));
		col2.AddContent(col2Content ?? ContainerTestHelpers.CreateButton("Right"));

		grid.AddColumn(col1);
		var splitter = grid.AddColumnWithSplitter(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		return (grid, col1, col2, splitter!, system, window);
	}

	#endregion

	#region Basic Keyboard Resize - Right Arrow

	[Fact]
	public void RightArrow_ExplicitWidths_IncreasesLeftWidth()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;
		int originalWidth = col1.Width!.Value;

		splitter.ProcessKey(RightArrow);

		Assert.Equal(originalWidth + 1, col1.Width);
		Assert.Null(col2.Width); // Right column cleared to flex
	}

	[Fact]
	public void RightArrow_MultipleSteps_AccumulatesWidth()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		for (int i = 0; i < 5; i++)
			splitter.ProcessKey(RightArrow);

		Assert.Equal(45, col1.Width);
	}

	[Fact]
	public void ShiftRightArrow_Moves5Columns()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		splitter.ProcessKey(ShiftRight);

		Assert.Equal(45, col1.Width);
	}

	#endregion

	#region Basic Keyboard Resize - Left Arrow

	[Fact]
	public void LeftArrow_ExplicitWidths_DecreasesLeftWidth()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		splitter.ProcessKey(LeftArrow);

		Assert.Equal(39, col1.Width);
	}

	[Fact]
	public void ShiftLeftArrow_Moves5Columns()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		splitter.ProcessKey(ShiftLeft);

		Assert.Equal(35, col1.Width);
	}

	#endregion

	#region Min Width Constraint Enforcement

	[Fact]
	public void RightArrow_AtMaxLeft_StopsGrowing()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		// Move right many times with re-renders to update ActualWidth,
		// which the splitter uses for constraint calculation
		int lastWidth = col1.Width!.Value;
		int stoppedAt = -1;
		for (int i = 0; i < 100; i++)
		{
			splitter.ProcessKey(RightArrow);
			window.RenderAndGetVisibleContent(); // Update ActualWidth for constraint calc
			if (col1.Width == lastWidth)
			{
				stoppedAt = i;
				break;
			}
			lastWidth = col1.Width!.Value;
		}

		Assert.True(stoppedAt >= 0, "Splitter should eventually stop growing (hit max constraint)");
		Assert.True(col1.Width > 40, $"Left column should have grown from 40, got {col1.Width}");
	}

	[Fact]
	public void LeftArrow_AtMinLeft_StopsShrinking()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		// Move left many times - should eventually stop shrinking
		int lastWidth = col1.Width!.Value;
		int stoppedAt = -1;
		for (int i = 0; i < 100; i++)
		{
			splitter.ProcessKey(LeftArrow);
			if (col1.Width == lastWidth)
			{
				stoppedAt = i;
				break;
			}
			lastWidth = col1.Width!.Value;
		}

		Assert.True(stoppedAt >= 0, "Splitter should eventually stop shrinking (hit min constraint)");
		Assert.True(col1.Width < 40, $"Left column should have shrunk from 40, got {col1.Width}");
		Assert.True(col1.Width >= 5, $"Left column should respect absolute minimum of 5, got {col1.Width}");
	}

	[Fact]
	public void ShiftArrow_OvershootsMin_ClampsCorrectly()
	{
		// Left=12 out of 80. Shift+Left would try -5=7, but min is ~8
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 12, col2Width: 68);

		splitter.HasFocus = true;

		splitter.ProcessKey(ShiftLeft);

		int total = 80;
		int minLeft = Math.Max(5, (int)(total * 0.1f));
		Assert.True(col1.Width >= minLeft, $"Left width {col1.Width} should be at least {minLeft}");
	}

	#endregion

	#region Flex Column (No Explicit Width) Combinations

	[Fact]
	public void RightArrow_LeftExplicitRightFlex_SetsLeftWidthClearsRight()
	{
		// Only left has explicit width, right flexes
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40);

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);

		Assert.NotNull(col1.Width);
		Assert.True(col1.Width > 40, "Left column should grow");
		Assert.Null(col2.Width); // Right stays flex
	}

	[Fact]
	public void LeftArrow_LeftExplicitRightFlex_DecreasesLeftWidth()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40);

		splitter.HasFocus = true;

		splitter.ProcessKey(LeftArrow);

		Assert.Equal(39, col1.Width);
	}

	[Fact]
	public void RightArrow_BothFlex_SetsLeftWidthFromActual()
	{
		// Neither column has explicit width - both flex
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter();

		splitter.HasFocus = true;
		int actualBefore = col1.ActualWidth;

		splitter.ProcessKey(RightArrow);

		// After resize, left should have explicit width = actualBefore + 1
		Assert.NotNull(col1.Width);
		Assert.Equal(actualBefore + 1, col1.Width);
		Assert.Null(col2.Width); // Right cleared to flex
	}

	#endregion

	#region Different Control Types in Columns

	[Fact]
	public void Resize_WithListControls_Works()
	{
		var list1 = ContainerTestHelpers.CreateFocusableList("A1", "A2", "A3");
		var list2 = ContainerTestHelpers.CreateFocusableList("B1", "B2", "B3");

		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(
				col1Width: 40, col2Width: 40,
				col1Content: list1, col2Content: list2);

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);
	}

	[Fact]
	public void Resize_WithScrollablePanelControls_Works()
	{
		var panel1 = new ScrollablePanelControl { Height = 10 };
		panel1.AddControl(ContainerTestHelpers.CreateButton("PanelBtn"));
		var panel2 = new ScrollablePanelControl { Height = 10 };
		panel2.AddControl(ContainerTestHelpers.CreateLabel("Content"));

		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(
				col1Width: 40, col2Width: 40,
				col1Content: panel1, col2Content: panel2);

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);
	}

	[Fact]
	public void Resize_WithSliderControl_Works()
	{
		var slider = new SliderControl { Value = 50 };
		var label = ContainerTestHelpers.CreateLabel("Info");

		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(
				col1Width: 40, col2Width: 40,
				col1Content: slider, col2Content: label);

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);
	}

	[Fact]
	public void Resize_WithMixedControlTypes_Works()
	{
		// Left: button, Right: list
		var btn = ContainerTestHelpers.CreateButton("Action");
		var list = ContainerTestHelpers.CreateFocusableList("Item1", "Item2");

		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(
				col1Width: 30, col2Width: 50,
				col1Content: btn, col2Content: list);

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(31, col1.Width);

		splitter.ProcessKey(LeftArrow);
		Assert.Equal(30, col1.Width);
	}

	#endregion

	#region Alignment Combinations

	[Fact]
	public void Resize_LeftAlignedColumns_Works()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		col1.HorizontalAlignment = HorizontalAlignment.Left;
		col2.HorizontalAlignment = HorizontalAlignment.Left;
		window.RenderAndGetVisibleContent();

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);
	}

	[Fact]
	public void Resize_RightAlignedColumns_Works()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		col1.HorizontalAlignment = HorizontalAlignment.Right;
		col2.HorizontalAlignment = HorizontalAlignment.Right;
		window.RenderAndGetVisibleContent();

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);
	}

	[Fact]
	public void Resize_CenterAlignedColumns_Works()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		col1.HorizontalAlignment = HorizontalAlignment.Center;
		col2.HorizontalAlignment = HorizontalAlignment.Center;
		window.RenderAndGetVisibleContent();

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);
	}

	[Fact]
	public void Resize_StretchAlignedColumns_Works()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		col1.HorizontalAlignment = HorizontalAlignment.Stretch;
		col2.HorizontalAlignment = HorizontalAlignment.Stretch;
		window.RenderAndGetVisibleContent();

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);
	}

	[Fact]
	public void Resize_MixedAlignments_LeftAndRight_Works()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		col1.HorizontalAlignment = HorizontalAlignment.Left;
		col2.HorizontalAlignment = HorizontalAlignment.Right;
		window.RenderAndGetVisibleContent();

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);
	}

	[Fact]
	public void Resize_MixedAlignments_StretchAndCenter_Works()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		col1.HorizontalAlignment = HorizontalAlignment.Stretch;
		col2.HorizontalAlignment = HorizontalAlignment.Center;
		window.RenderAndGetVisibleContent();

		splitter.HasFocus = true;

		splitter.ProcessKey(LeftArrow);
		Assert.Equal(39, col1.Width);
	}

	#endregion

	#region SplitterMoved Event

	[Fact]
	public void SplitterMoved_FiresOnResize()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;
		SplitterMovedEventArgs? receivedArgs = null;
		splitter.SplitterMoved += (s, e) => receivedArgs = e;

		splitter.ProcessKey(RightArrow);

		Assert.NotNull(receivedArgs);
		Assert.Equal(1, receivedArgs!.Delta);
		Assert.Equal(41, receivedArgs.LeftColumnWidth);
	}

	[Fact]
	public void SplitterMoved_DoesNotFire_WhenClampedToSameWidth()
	{
		// Left is already at minimum
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 5, col2Width: 75);

		splitter.HasFocus = true;
		int total = 80;
		int minWidth = Math.Max(5, (int)(total * 0.1f));

		// Move left to clamp at min
		for (int i = 0; i < 10; i++)
			splitter.ProcessKey(LeftArrow);

		// Now count events from here (at minimum)
		int eventCount = 0;
		splitter.SplitterMoved += (s, e) => eventCount++;

		splitter.ProcessKey(LeftArrow); // Should not move further

		Assert.Equal(0, eventCount);
	}

	#endregion

	#region Dragging State

	[Fact]
	public void ProcessKey_Arrow_SetsDraggingState()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;
		Assert.False(splitter.IsDragging);

		splitter.ProcessKey(RightArrow);

		Assert.True(splitter.IsDragging, "Splitter should be dragging after arrow key");
	}

	[Fact]
	public void FocusLost_ClearsDraggingState()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;
		splitter.ProcessKey(RightArrow);
		Assert.True(splitter.IsDragging);

		splitter.HasFocus = false;

		Assert.False(splitter.IsDragging, "Dragging should stop when focus lost");
	}

	#endregion

	#region Guard Clauses

	[Fact]
	public void ProcessKey_WhenDisabled_ReturnsFalse()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;
		splitter.IsEnabled = false;

		bool handled = splitter.ProcessKey(RightArrow);

		Assert.False(handled);
		Assert.Equal(40, col1.Width);
	}

	[Fact]
	public void ProcessKey_WhenNotFocused_ReturnsFalse()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		bool handled = splitter.ProcessKey(RightArrow);

		Assert.False(handled);
		Assert.Equal(40, col1.Width);
	}

	[Fact]
	public void ProcessKey_NonArrowKey_ReturnsFalse()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
		bool handled = splitter.ProcessKey(enterKey);

		Assert.False(handled);
		Assert.Equal(40, col1.Width);
	}

	#endregion

	#region Three-Column Grid with Two Splitters

	[Fact]
	public void ThreeColumnGrid_ResizeFirstSplitter_AffectsFirstTwoColumns()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 30 };
		var col2 = new ColumnContainer(grid) { Width = 30 };
		var col3 = new ColumnContainer(grid) { Width = 30 };

		col1.AddContent(ContainerTestHelpers.CreateButton("C1"));
		col2.AddContent(ContainerTestHelpers.CreateButton("C2"));
		col3.AddContent(ContainerTestHelpers.CreateButton("C3"));

		grid.AddColumn(col1);
		var splitter1 = grid.AddColumnWithSplitter(col2);
		var splitter2 = grid.AddColumnWithSplitter(col3);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		splitter1!.HasFocus = true;
		splitter1.ProcessKey(RightArrow);

		Assert.Equal(31, col1.Width);
		Assert.Null(col2.Width); // col2 flexes
		Assert.Equal(30, col3.Width); // col3 unaffected
	}

	[Fact]
	public void ThreeColumnGrid_ResizeSecondSplitter_AffectsLastTwoColumns()
	{
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 30 };
		var col2 = new ColumnContainer(grid) { Width = 30 };
		var col3 = new ColumnContainer(grid) { Width = 30 };

		col1.AddContent(ContainerTestHelpers.CreateButton("C1"));
		col2.AddContent(ContainerTestHelpers.CreateButton("C2"));
		col3.AddContent(ContainerTestHelpers.CreateButton("C3"));

		grid.AddColumn(col1);
		var splitter1 = grid.AddColumnWithSplitter(col2);
		var splitter2 = grid.AddColumnWithSplitter(col3);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		splitter2!.HasFocus = true;
		splitter2.ProcessKey(RightArrow);

		Assert.Equal(30, col1.Width); // col1 unaffected
		Assert.Equal(31, col2.Width);
		Assert.Null(col3.Width); // col3 flexes
	}

	#endregion

	#region Bidirectional Resize (Back and Forth)

	[Fact]
	public void ResizeRightThenLeft_ReturnsToOriginalWidth()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);

		splitter.ProcessKey(LeftArrow);
		Assert.Equal(40, col1.Width);
	}

	[Fact]
	public void ShiftResizeRightThenLeft_ReturnsToOriginalWidth()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		splitter.ProcessKey(ShiftRight);
		Assert.Equal(45, col1.Width);

		splitter.ProcessKey(ShiftLeft);
		Assert.Equal(40, col1.Width);
	}

	#endregion

	#region Render After Resize

	[Fact]
	public void Resize_ThenRender_DoesNotCrash()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		// Multiple resizes
		for (int i = 0; i < 10; i++)
			splitter.ProcessKey(RightArrow);

		// Re-render after resize
		var exception = Record.Exception(() => window.RenderAndGetVisibleContent());
		Assert.Null(exception);
	}

	[Fact]
	public void Resize_WithDifferentControlTypes_ThenRender_ContentVisible()
	{
		var list = ContainerTestHelpers.CreateFocusableList("Item1", "Item2");
		var btn = ContainerTestHelpers.CreateButton("Click Me");

		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(
				col1Width: 40, col2Width: 40,
				col1Content: list, col2Content: btn);

		splitter.HasFocus = true;
		splitter.ProcessKey(ShiftRight); // Move right 5

		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		Assert.Contains("Item1", plainText);
		Assert.Contains("Click Me", plainText);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void Resize_VeryNarrowColumns_RespectsAbsoluteMinimum()
	{
		// Both columns very narrow
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 6, col2Width: 6);

		splitter.HasFocus = true;

		// Try to shrink left below absolute minimum
		for (int i = 0; i < 10; i++)
			splitter.ProcessKey(LeftArrow);

		Assert.True(col1.Width >= 5, "Left column should not go below absolute minimum of 5");
	}

	[Fact]
	public void Resize_EqualColumns_SymmetricBehavior()
	{
		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(col1Width: 40, col2Width: 40);

		splitter.HasFocus = true;

		splitter.ProcessKey(RightArrow);
		int afterRight = col1.Width!.Value;

		// Reset
		splitter.ProcessKey(LeftArrow);

		splitter.ProcessKey(LeftArrow);
		int afterLeft = col1.Width!.Value;

		// Moving right by 1 then left by 1 should be symmetric
		Assert.Equal(afterRight, 41);
		Assert.Equal(afterLeft, 39);
	}

	[Fact]
	public void Resize_ColumnWithNestedGrid_Works()
	{
		// Column contains a nested grid
		var innerGrid = new HorizontalGridControl();
		var innerCol = new ColumnContainer(innerGrid);
		innerCol.AddContent(ContainerTestHelpers.CreateButton("Nested"));
		innerGrid.AddColumn(innerCol);

		var (grid, col1, col2, splitter, system, window) =
			CreateTwoColumnGridWithSplitter(
				col1Width: 40, col2Width: 40,
				col1Content: innerGrid);

		splitter.HasFocus = true;
		splitter.ProcessKey(RightArrow);
		Assert.Equal(41, col1.Width);
	}

	#endregion
}
