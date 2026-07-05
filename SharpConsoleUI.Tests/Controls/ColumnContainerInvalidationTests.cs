// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class ColumnContainerInvalidationTests
	{
		private static (ConsoleWindowSystem system, Window window, HorizontalGridControl grid, ColumnContainer col, ButtonControl child) BuildGrid()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 16);
			var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16 };
			var grid = new HorizontalGridControl();
			var child = new ButtonControl { Text = "C" };
			var col = new ColumnContainer(grid);
			col.AddContent(child);
			grid.AddColumn(col);
			window.AddControl(grid);
			system.AddWindow(window);
			system.Render.UpdateDisplay();
			return (system, window, grid, col, child);
		}

		[Fact]
		public void ChildInvalidation_Terminates_NoRunaway()
		{
			var (system, window, grid, col, child) = BuildGrid();
			child.Text = "C2"; // child -> column.Invalidate(work, child) -> guarded grid propagation
			system.Render.UpdateDisplay(); // must not StackOverflow / hang
			Assert.NotNull(system.RenderingDiagnostics!.LastBufferSnapshot);
			System.GC.KeepAlive(window);
		}

		[Fact]
		public void ColumnColorChange_MarksWindowDirty_ThroughGuardedPath()
		{
			// The folded color setters route through the guarded Invalidate(Repaint, this), which propagates up
			// to the grid and thus the window. Render first (clears pending work), then a color change must
			// mark the window dirty again — proving the invalidation actually propagated, not just that a
			// frame was produced.
			var (system, window, grid, col, child) = BuildGrid();
			system.Render.UpdateDisplay(); // clear any pending work from setup
			Assert.False(system.WindowStateService.AnyWindowDirty(), "window should be clean after a render");

			col.BackgroundColor = new Color(10, 20, 30);

			Assert.True(system.WindowStateService.AnyWindowDirty(),
				"a column color change must propagate an invalidation up to the window");
			system.Render.UpdateDisplay(); // and it must terminate + render
			Assert.NotNull(system.RenderingDiagnostics!.LastBufferSnapshot);
			System.GC.KeepAlive(window);
		}

		[Fact]
		public void ColumnContentChange_MarksWindowDirty_ThroughGuardedPath()
		{
			// Adding content routes a Relayout up through the guarded path; after a clean render the add must
			// mark the window dirty again, then terminate + render.
			var (system, window, grid, col, child) = BuildGrid();
			system.Render.UpdateDisplay();
			Assert.False(system.WindowStateService.AnyWindowDirty(), "window should be clean after a render");

			col.AddContent(new ButtonControl { Text = "D" });

			Assert.True(system.WindowStateService.AnyWindowDirty(),
				"adding column content must propagate an invalidation up to the window");
			system.Render.UpdateDisplay();
			Assert.NotNull(system.RenderingDiagnostics!.LastBufferSnapshot);
			System.GC.KeepAlive(window);
		}
	}
}
