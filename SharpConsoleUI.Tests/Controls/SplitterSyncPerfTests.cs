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
/// A column's width changing is a track resize, not a structural change. It must not tear the
/// grid down and re-Place every column — each teardown triggers a full window layout-tree rebuild,
/// which during a splitter drag runs once per mouse event on the input thread.
/// </summary>
public class SplitterSyncPerfTests
{
	private static ConsoleKeyInfo RightArrow => new('\0', ConsoleKey.RightArrow, false, false, false);

	private static (HorizontalGridControl grid, ColumnContainer left, ColumnContainer middle,
		ColumnContainer right, SplitterControl splitter, Window window) Create()
	{
		var grid = new HorizontalGridControl
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill,
		};
		var left = new ColumnContainer(grid) { Width = 26 };
		var middle = new ColumnContainer(grid);
		var right = new ColumnContainer(grid) { Width = 30 };
		left.AddContent(ContainerTestHelpers.CreateButton("L"));
		middle.AddContent(ContainerTestHelpers.CreateButton("M"));
		right.AddContent(ContainerTestHelpers.CreateButton("R"));
		grid.AddColumn(left);
		grid.AddColumnWithSplitter(middle);
		var splitter = grid.AddColumnWithSplitter(right);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		system.WindowStateService.AddWindow(window);
		window.RenderAndGetVisibleContent();

		return (grid, left, middle, right, splitter!, window);
	}

	[Fact]
	public void ChangingOnlyAWidth_DoesNotRebuildTheGrid()
	{
		var (grid, left, _, _, _, window) = Create();
		int before = grid.SyncRebuildCount;

		left.Width = 40;
		window.RenderAndGetVisibleContent();

		Assert.Equal(before, grid.SyncRebuildCount);
	}

	[Fact]
	public void DraggingASplitter_DoesNotRebuildThePerEvent()
	{
		var (grid, _, _, _, splitter, window) = Create();
		window.FocusManager.SetFocus(splitter, FocusReason.Programmatic);
		int before = grid.SyncRebuildCount;

		for (int i = 0; i < 20; i++)
		{
			splitter.ProcessKey(RightArrow);
			window.RenderAndGetVisibleContent();
		}

		Assert.Equal(before, grid.SyncRebuildCount);
	}

	[Fact]
	public void AddingAColumn_StillRebuilds()
	{
		var (grid, _, _, _, _, window) = Create();
		int before = grid.SyncRebuildCount;

		var extra = new ColumnContainer(grid) { Width = 10 };
		extra.AddContent(ContainerTestHelpers.CreateButton("X"));
		grid.AddColumn(extra);
		window.RenderAndGetVisibleContent();

		Assert.True(grid.SyncRebuildCount > before, "adding a column must re-Place the grid");
	}

	[Fact]
	public void HidingAColumn_StillRebuilds()
	{
		var (grid, _, _, right, _, window) = Create();
		int before = grid.SyncRebuildCount;

		right.Visible = false;
		window.RenderAndGetVisibleContent();

		Assert.True(grid.SyncRebuildCount > before, "visibility is structural and must rebuild");
	}

	[Fact]
	public void AddingContentToAColumn_StillRebuilds()
	{
		var (grid, left, _, _, _, window) = Create();
		int before = grid.SyncRebuildCount;

		left.AddContent(ContainerTestHelpers.CreateButton("extra"));
		window.RenderAndGetVisibleContent();

		Assert.True(grid.SyncRebuildCount > before, "a new child control must re-Place");
	}

	[Fact]
	public void WidthOnlySync_StillLaysOutTheNewWidths()
	{
		// The cheap path must actually resize: skipping the rebuild is only correct if the track
		// definition is updated in place and the grid re-arranges.
		var (grid, left, _, _, _, window) = Create();

		left.Width = 40;
		window.RenderAndGetVisibleContent();

		Assert.Equal(40, grid.GetColumnArrangedSizeForTest(0));
	}

	[Fact]
	public void WidthOnlySync_ThenAStructuralChange_StillRebuilds()
	{
		// A width-only sync updates the stored signature; a later structural change must still be
		// detected against it rather than being swallowed.
		var (grid, left, _, _, _, window) = Create();

		left.Width = 40;
		window.RenderAndGetVisibleContent();
		int afterWidth = grid.SyncRebuildCount;

		var extra = new ColumnContainer(grid) { Width = 10 };
		extra.AddContent(ContainerTestHelpers.CreateButton("X"));
		grid.AddColumn(extra);
		window.RenderAndGetVisibleContent();

		Assert.True(grid.SyncRebuildCount > afterWidth);
	}
}
