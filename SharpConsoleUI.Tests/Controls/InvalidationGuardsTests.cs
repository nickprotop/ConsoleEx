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
	public class InvalidationGuardsTests
	{
		[Fact]
		public void WindowSink_IgnoresCallerControl_NoThrow()
		{
			// The Window root implements IContainer and must accept any caller identity without throwing or
			// branching (identity dies harmlessly at the root).
			var system = TestWindowSystemBuilder.CreateTestSystem(40, 12);
			var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
			system.AddWindow(window);

			var origin = new ButtonControl();
			((IContainer)window).Invalidate(Invalidation.Relayout, origin); // harmless no-branch sink
			((IContainer)window).Invalidate(Invalidation.Repaint, null);

			system.Render.UpdateDisplay();
			Assert.NotNull(system.RenderingDiagnostics!.LastBufferSnapshot);
			System.GC.KeepAlive(window);
		}

		[Fact]
		public void ColumnGrid_Invalidation_Terminates_NoRunaway()
		{
			// A horizontal grid with columns + child content. A child invalidation must propagate without
			// infinite recursion (the _invalidatingContainers re-entrancy set holds) and the render must
			// still complete — with real control identity now flowing through the chain.
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 16);
			var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16 };

			var grid = new HorizontalGridControl();
			var left = new ButtonControl { Text = "L" };
			var leftCol = new ColumnContainer(grid);
			leftCol.AddContent(left);
			grid.AddColumn(leftCol);
			var right = new ButtonControl { Text = "R" };
			var rightCol = new ColumnContainer(grid);
			rightCol.AddContent(right);
			grid.AddColumn(rightCol);
			window.AddControl(grid);
			system.AddWindow(window);
			system.Render.UpdateDisplay();

			// A child property change drives invalidation up through ColumnContainer/HGC guards.
			left.Text = "L2";
			system.Render.UpdateDisplay(); // must terminate; no StackOverflow / runaway

			Assert.NotNull(system.RenderingDiagnostics!.LastBufferSnapshot);
			System.GC.KeepAlive(window);
		}
	}
}
