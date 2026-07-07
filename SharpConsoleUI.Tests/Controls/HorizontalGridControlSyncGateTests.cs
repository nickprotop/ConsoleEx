// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	// Regression: the grid-backed HorizontalGridControl re-ran Sync() (ClearControls + rebuild all column
	// tracks + re-Place every column) on EVERY Relayout invalidation. A child-content Relayout (typing, cursor
	// blink, syntax repaint) propagates up to the grid's Invalidate(Relayout), so the grid rebuilt its columns
	// every frame — flicker + "recreates columns each frame" (seen live in LazyDotIDE's editor grid). Sync()
	// must rebuild only when the COLUMN MODEL actually changed, not on child-content relayouts.
	public class HorizontalGridControlSyncGateTests
	{
		private static (HorizontalGridControl grid, ColumnContainer col, MarkupControl child) BuildGrid()
		{
			var grid = new HorizontalGridControl();
			var col = new ColumnContainer(grid) { FlexFactor = 1 };
			var child = new MarkupControl(new List<string> { "content" });
			col.AddContent(child);
			grid.AddColumn(col);
			return (grid, col, child);
		}

		[Fact]
		public void ChildContentRelayout_DoesNotRebuildColumns()
		{
			var (grid, _, child) = BuildGrid();
			int after = grid.SyncRebuildCount; // rebuilds from construction are done

			// Simulate many frames of child content changing (each raises Relayout, propagating up to the grid).
			for (int i = 0; i < 20; i++)
			{
				child.SetContent(new List<string> { $"content {i}" });
			}

			Assert.Equal(after, grid.SyncRebuildCount); // ZERO rebuilds — the column model never changed.
		}

		[Fact]
		public void ColumnWidthChange_DoesRebuildColumns()
		{
			var (grid, col, _) = BuildGrid();
			int before = grid.SyncRebuildCount;

			col.Width = 25; // a real structural change to the column model.

			Assert.True(grid.SyncRebuildCount > before, "a column Width change must rebuild the grid tracks");
		}

		[Fact]
		public void AddColumn_DoesRebuildColumns()
		{
			var (grid, _, _) = BuildGrid();
			int before = grid.SyncRebuildCount;

			var col2 = new ColumnContainer(grid) { FlexFactor = 1 };
			col2.AddContent(new MarkupControl(new List<string> { "second" }));
			grid.AddColumn(col2);

			Assert.True(grid.SyncRebuildCount > before, "adding a column must rebuild the grid tracks");
		}
	}
}
