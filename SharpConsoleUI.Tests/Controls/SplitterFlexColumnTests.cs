// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A splitter drag must not convert a flex column (Width == null) into a fixed one.
/// The canonical layout is fixed sidebar / flex content / fixed panel, where the flex
/// column is adjacent to BOTH splitters: if a drag pins it, the layout stops absorbing
/// slack on resize and any width persisted from it is far larger than the on-screen size.
/// </summary>
public class SplitterFlexColumnTests
{
	private static ConsoleKeyInfo LeftArrow => new('\0', ConsoleKey.LeftArrow, false, false, false);
	private static ConsoleKeyInfo RightArrow => new('\0', ConsoleKey.RightArrow, false, false, false);

	/// <summary>
	/// Builds the real three-column layout: fixed / flex / fixed, with a splitter on
	/// each side of the flex column, rendered through a live window.
	/// </summary>
	private static (ColumnContainer left, ColumnContainer middle, ColumnContainer right,
		SplitterControl leftSplitter, SplitterControl rightSplitter, Window window,
		HorizontalGridControl grid)
		CreateFixedFlexFixed(int leftWidth = 26, int rightWidth = 30)
	{
		// Stretch, as real layouts do: an unstretched grid sizes to its content, so ActualWidth
		// would be narrower than the columns and every width assertion here would be meaningless.
		var grid = new HorizontalGridControl
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill,
		};

		var left = new ColumnContainer(grid) { Width = leftWidth };
		var middle = new ColumnContainer(grid);            // flex: Width stays null
		var right = new ColumnContainer(grid) { Width = rightWidth };

		left.AddContent(ContainerTestHelpers.CreateButton("L"));
		middle.AddContent(ContainerTestHelpers.CreateButton("M"));
		right.AddContent(ContainerTestHelpers.CreateButton("R"));

		grid.AddColumn(left);
		var leftSplitter = grid.AddColumnWithSplitter(middle);
		var rightSplitter = grid.AddColumnWithSplitter(right);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		return (left, middle, right, leftSplitter!, rightSplitter!, window, grid);
	}

	[Fact]
	public void DraggingLeftSplitter_LeavesTheFlexColumnFlexible()
	{
		var (left, middle, _, leftSplitter, _, window, _) = CreateFixedFlexFixed();
		window.FocusManager.SetFocus(leftSplitter, FocusReason.Programmatic);

		leftSplitter.ProcessKey(RightArrow);

		Assert.Null(middle.Width);          // the flex column must stay flex
		Assert.NotNull(left.Width);         // the fixed column absorbs the drag
	}

	[Fact]
	public void DraggingRightSplitter_LeavesTheFlexColumnFlexible()
	{
		var (_, middle, right, _, rightSplitter, window, _) = CreateFixedFlexFixed();
		window.FocusManager.SetFocus(rightSplitter, FocusReason.Programmatic);

		rightSplitter.ProcessKey(LeftArrow);

		Assert.Null(middle.Width);
		Assert.NotNull(right.Width);
	}

	[Fact]
	public void DraggingLeftSplitter_MovesTheFixedColumnByTheDelta()
	{
		var (left, _, _, leftSplitter, _, window, _) = CreateFixedFlexFixed(leftWidth: 26);
		window.FocusManager.SetFocus(leftSplitter, FocusReason.Programmatic);

		leftSplitter.ProcessKey(RightArrow);

		Assert.Equal(27, left.Width);
	}

	[Fact]
	public void DraggingRightSplitter_MovesTheFixedColumnByTheDelta()
	{
		// The splitter's LEFT neighbour is the flex column, so a left-arrow drag has to
		// grow the fixed RIGHT column rather than shrink the flex one.
		var (_, _, right, _, rightSplitter, window, _) = CreateFixedFlexFixed(rightWidth: 30);
		window.FocusManager.SetFocus(rightSplitter, FocusReason.Programmatic);

		rightSplitter.ProcessKey(LeftArrow);

		Assert.Equal(31, right.Width);
	}

	[Fact]
	public void FixedColumnWidth_StaysProportionalToItsOnScreenSize()
	{
		// The regression: the fixed column used to be assigned a slice of the COMBINED
		// width of both neighbours, so a 30-wide panel could be handed a width of ~128.
		var (_, _, right, _, rightSplitter, window, _) = CreateFixedFlexFixed(rightWidth: 30);
		window.FocusManager.SetFocus(rightSplitter, FocusReason.Programmatic);

		for (int i = 0; i < 3; i++)
			rightSplitter.ProcessKey(LeftArrow);

		Assert.NotNull(right.Width);
		// Three cells of drag from 30 — nowhere near the combined width of both columns.
		Assert.InRange(right.Width!.Value, 31, 40);
	}

	[Fact]
	public void FlexColumn_StaysUnpinned_AcrossDragsAndReRenders()
	{
		// The consequence that matters: a pinned column keeps a stale Width forever, so the
		// layout stops responding to the window. Repeated drags on both splitters, with a
		// re-render and a resize in between, must never give the flex column a Width.
		var (left, middle, right, leftSplitter, rightSplitter, window, _) = CreateFixedFlexFixed();

		window.FocusManager.SetFocus(leftSplitter, FocusReason.Programmatic);
		leftSplitter.ProcessKey(RightArrow);
		leftSplitter.ProcessKey(RightArrow);
		window.RenderAndGetVisibleContent();
		Assert.Null(middle.Width);

		window.FocusManager.SetFocus(rightSplitter, FocusReason.Programmatic);
		rightSplitter.ProcessKey(LeftArrow);
		window.RenderAndGetVisibleContent();
		Assert.Null(middle.Width);

		window.Width -= 20;
		window.RenderAndGetVisibleContent();

		// Still flexible, and the fixed columns kept exactly what the drags gave them.
		Assert.Null(middle.Width);
		Assert.Equal(28, left.Width);
		Assert.Equal(31, right.Width);
	}

	[Fact]
	public void BothColumnsFixed_KeepsTheOriginalPairwiseBehaviour()
	{
		// Unchanged contract: with two fixed columns the pair still splits their combined
		// width, so existing two-pane layouts behave exactly as before.
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 40 };
		var col2 = new ColumnContainer(grid) { Width = 40 };
		col1.AddContent(ContainerTestHelpers.CreateButton("L"));
		col2.AddContent(ContainerTestHelpers.CreateButton("R"));
		grid.AddColumn(col1);
		var splitter = grid.AddColumnWithSplitter(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(splitter!, FocusReason.Programmatic);
		splitter!.ProcessKey(RightArrow);

		Assert.Equal(41, col1.Width);
		Assert.Equal(39, col2.Width);
	}

	[Fact]
	public void SplitterMoved_ReportsBothColumnWidths()
	{
		// RightColumnWidth was hardcoded to 0, forcing consumers to derive the right
		// column's size by tracking LeftColumnWidth deltas themselves.
		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 40 };
		var col2 = new ColumnContainer(grid) { Width = 40 };
		col1.AddContent(ContainerTestHelpers.CreateButton("L"));
		col2.AddContent(ContainerTestHelpers.CreateButton("R"));
		grid.AddColumn(col1);
		var splitter = grid.AddColumnWithSplitter(col2);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		SplitterMovedEventArgs? captured = null;
		splitter!.SplitterMoved += (_, e) => captured = e;

		window.FocusManager.SetFocus(splitter, FocusReason.Programmatic);
		splitter.ProcessKey(RightArrow);

		Assert.NotNull(captured);
		Assert.Equal(1, captured!.Delta);
		Assert.Equal(col1.Width, captured.LeftColumnWidth);
		Assert.Equal(col2.Width, captured.RightColumnWidth);
	}

	[Fact]
	public void PersistedWidth_TracksTheOnScreenSize_AcrossManyDrags()
	{
		// The LazyDotIDE bug: a side panel rendering ~30 wide persisted a Width of 128, which
		// pushed it off-screen on restore. Whatever the drag history, the fixed column's Width
		// must stay near what it occupies — never a slice of the whole strip.
		var (_, middle, right, _, rightSplitter, window, _) = CreateFixedFlexFixed(rightWidth: 30);
		window.FocusManager.SetFocus(rightSplitter, FocusReason.Programmatic);

		for (int i = 0; i < 4; i++) rightSplitter.ProcessKey(LeftArrow);
		for (int i = 0; i < 2; i++) rightSplitter.ProcessKey(RightArrow);
		window.RenderAndGetVisibleContent();

		Assert.Null(middle.Width);
		Assert.Equal(32, right.Width);   // 30 + 4 - 2, exactly the net drag
	}

	[Fact]
	public void OverCommittedGrid_DragStillMovesTheColumn()
	{
		// Setting one column's Width without adjusting its neighbour (restoring a saved layout,
		// say) over-commits the grid. Drags used to clamp against the stale column sum and
		// silently no-op — while still raising SplitterMoved with a delta that was never applied.
		var (_, _, right, _, rightSplitter, window, _) = CreateFixedFlexFixed();

		right.Width = window.Width;   // wildly over-committed
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(rightSplitter, FocusReason.Programmatic);
		int before = right.Width!.Value;
		rightSplitter.ProcessKey(RightArrow);   // shrink the over-wide column

		Assert.NotEqual(before, right.Width);
	}

	[Fact]
	public void OverCommittedGrid_DoesNotGrowAColumnBeyondTheGrid()
	{
		// The converse failure: a drag must not keep inflating a column that already exceeds the
		// grid, or the layout can never recover and the column renders off-screen.
		var (_, _, right, _, rightSplitter, window, _) = CreateFixedFlexFixed();

		right.Width = window.Width;
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(rightSplitter, FocusReason.Programmatic);
		for (int i = 0; i < 25; i++)
			rightSplitter.ProcessKey(LeftArrow);   // repeatedly try to grow it

		Assert.NotNull(right.Width);
		Assert.True(right.Width!.Value <= window.Width,
			$"column grew to {right.Width} in a {window.Width}-wide window");
	}

	[Fact]
	public void SplitterMoved_IsNotRaisedWhenNothingMoves()
	{
		// A drag that cannot be applied must stay silent: consumers accumulate Delta, so an
		// event carrying a delta that was never applied makes their tracking drift.
		var (left, _, _, leftSplitter, _, window, _) = CreateFixedFlexFixed(leftWidth: 26);
		window.FocusManager.SetFocus(leftSplitter, FocusReason.Programmatic);

		// Drive the fixed column down to its floor, then keep pushing.
		for (int i = 0; i < 40; i++)
			leftSplitter.ProcessKey(LeftArrow);

		int settled = left.Width!.Value;
		int raised = 0;
		leftSplitter.SplitterMoved += (_, _) => raised++;

		leftSplitter.ProcessKey(LeftArrow);

		Assert.Equal(settled, left.Width);
		Assert.Equal(0, raised);
	}

	[Fact]
	public void DraggingToTheLimit_LeavesEveryColumnOnScreen()
	{
		// The ceiling has to be the space genuinely available to this column: the grid minus the
		// OTHER columns, the splitters, and a minimum for the flex column. Clamping against the
		// full grid width ignores the sibling columns and drives this one off the right edge.
		var (left, middle, right, _, rightSplitter, window, grid) = CreateFixedFlexFixed(leftWidth: 26, rightWidth: 30);

		window.FocusManager.SetFocus(rightSplitter, FocusReason.Programmatic);
		for (int i = 0; i < 300; i++)
			rightSplitter.ProcessKey(LeftArrow);      // grow the right column as far as it will go

		window.RenderAndGetVisibleContent();

		int splitters = 2;
		int committed = (left.Width ?? 0) + (right.Width ?? 0) + splitters;
		Assert.Null(middle.Width);
		Assert.True(committed <= grid.ActualWidth,
			$"columns commit {committed} in a {grid.ActualWidth}-wide grid (left={left.Width} right={right.Width})");
	}

	[Fact]
	public void DraggingToTheLimit_LeavesRoomForTheFlexColumn()
	{
		var (left, middle, right, _, rightSplitter, window, grid) = CreateFixedFlexFixed(leftWidth: 26, rightWidth: 30);

		window.FocusManager.SetFocus(rightSplitter, FocusReason.Programmatic);
		for (int i = 0; i < 300; i++)
			rightSplitter.ProcessKey(LeftArrow);

		window.RenderAndGetVisibleContent();

		// The flex column must keep a usable slice, not be squeezed to nothing.
		int leftover = grid.ActualWidth - (left.Width ?? 0) - (right.Width ?? 0) - 2;
		Assert.True(leftover > 0, $"flex column left with {leftover} columns");
	}

	[Fact]
	public void DragBeforeFirstPaint_StillClampsTheColumn()
	{
		// ActualWidth is only assigned in PaintDOM, so a drag processed before the grid paints
		// sees gridWidth == 0. That must not remove the ceiling altogether.
		var grid = new HorizontalGridControl { HorizontalAlignment = HorizontalAlignment.Stretch };
		var left = new ColumnContainer(grid) { Width = 26 };
		var middle = new ColumnContainer(grid);
		var right = new ColumnContainer(grid) { Width = 30 };
		left.AddContent(ContainerTestHelpers.CreateButton("L"));
		middle.AddContent(ContainerTestHelpers.CreateButton("M"));
		right.AddContent(ContainerTestHelpers.CreateButton("R"));
		grid.AddColumn(left);
		grid.AddColumnWithSplitter(middle);
		var rightSplitter = grid.AddColumnWithSplitter(right);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		// deliberately NOT rendered: ActualWidth stays 0 everywhere

		window.FocusManager.SetFocus(rightSplitter!, FocusReason.Programmatic);
		for (int i = 0; i < 300; i++)
			rightSplitter!.ProcessKey(LeftArrow);

		Assert.NotNull(right.Width);
		Assert.True(right.Width!.Value <= window.Width,
			$"pre-paint drag grew the column to {right.Width} in a {window.Width}-wide window");
	}

	[Fact]
	public void FlexColumnWithWideContent_DoesNotSkewTheClamp()
	{
		// A scrollable control reports its CONTENT width (the longest line), which has nothing to
		// do with how much space the column has. It must not leak into the width path.
		var grid = new HorizontalGridControl { HorizontalAlignment = HorizontalAlignment.Stretch };
		var left = new ColumnContainer(grid) { Width = 26 };
		var middle = new ColumnContainer(grid);
		var right = new ColumnContainer(grid) { Width = 30 };
		left.AddContent(ContainerTestHelpers.CreateButton("L"));
		middle.AddContent(new MultilineEditControl { Content = new string('x', 436) });
		right.AddContent(ContainerTestHelpers.CreateButton("R"));
		grid.AddColumn(left);
		grid.AddColumnWithSplitter(middle);
		var rightSplitter = grid.AddColumnWithSplitter(right);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(rightSplitter!, FocusReason.Programmatic);
		for (int i = 0; i < 300; i++)
			rightSplitter!.ProcessKey(LeftArrow);

		window.RenderAndGetVisibleContent();

		int committed = (left.Width ?? 0) + (right.Width ?? 0) + 2;
		Assert.True(committed <= grid.ActualWidth,
			$"content width skewed the clamp: {committed} committed in {grid.ActualWidth}");
	}

	[Fact]
	public void WideScrollingContent_DoesNotPinTheFlexColumn()
	{
		// A scrolling control renders at whatever width it is given, so its longest line is a
		// preference, not a minimum. Reporting it as ContentWidth floored the flex track at the
		// line length: a 436-character line pinned the column at 436 inside a 98-wide grid, put
		// the sibling columns off-screen, and made every narrower splitter position unreachable.
		var grid = new HorizontalGridControl
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		var left = new ColumnContainer(grid) { Width = 26 };
		var editor = new ColumnContainer(grid);
		var right = new ColumnContainer(grid) { Width = 30 };
		left.AddContent(ContainerTestHelpers.CreateButton("L"));
		editor.AddContent(new MultilineEditControl { Content = new string('x', 436) + "\n" + new string('y', 400) });
		right.AddContent(ContainerTestHelpers.CreateButton("R"));
		grid.AddColumn(left);
		grid.AddColumnWithSplitter(editor);
		var rightSplitter = grid.AddColumnWithSplitter(right);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		for (int i = 0; i < 3; i++) window.RenderAndGetVisibleContent();

		// The column asks for what it needs to render, not for its longest line.
		Assert.True(editor.GetContentWidth() < grid.ActualWidth,
			$"flex column demands {editor.GetContentWidth()} in a {grid.ActualWidth}-wide grid");

		// Every column is on-screen.
		int arranged = grid.GetColumnArrangedSizeForTest(0)
			+ grid.GetColumnArrangedSizeForTest(2)
			+ grid.GetColumnArrangedSizeForTest(4) + 2;
		Assert.True(arranged <= grid.ActualWidth,
			$"columns arrange to {arranged} in a {grid.ActualWidth}-wide grid");

		// And the splitter can squeeze the flex column far below its content width.
		window.FocusManager.SetFocus(rightSplitter!, FocusReason.Programmatic);
		int narrowest = int.MaxValue;
		for (int i = 0; i < 200; i++)
		{
			rightSplitter!.ProcessKey(LeftArrow);
			window.RenderAndGetVisibleContent();
			narrowest = Math.Min(narrowest, grid.GetColumnArrangedSizeForTest(2));
		}
		Assert.True(narrowest < 50, $"flex column never got below {narrowest} columns");
	}

	[Fact]
	public void MultilineEdit_ReportsAMinimumItCanActuallyRenderAt()
	{
		// ContentWidth is contracted as the MINIMUM width needed to display the content. This
		// control scrolls, so that is its chrome plus a little text — never the longest line,
		// which is exposed separately as LongestLineWidth.
		var editor = new MultilineEditControl { Content = new string('x', 436) };

		Assert.Equal(436, editor.LongestLineWidth);
		Assert.NotNull(editor.ContentWidth);
		Assert.True(editor.ContentWidth!.Value < 40,
			$"a scrolling editor claimed it needs {editor.ContentWidth} columns");
	}

	[Fact]
	public void SplitterMoved_ReportsFlexNeighbourAsItsRenderedWidth()
	{
		var (left, middle, _, leftSplitter, _, window, _) = CreateFixedFlexFixed();

		SplitterMovedEventArgs? captured = null;
		leftSplitter.SplitterMoved += (_, e) => captured = e;

		window.FocusManager.SetFocus(leftSplitter, FocusReason.Programmatic);
		leftSplitter.ProcessKey(RightArrow);

		Assert.NotNull(captured);
		Assert.Equal(left.Width, captured!.LeftColumnWidth);
		// The flex column has no explicit Width; the event reports what it actually occupies.
		Assert.True(captured.RightColumnWidth > 0);
	}
}
