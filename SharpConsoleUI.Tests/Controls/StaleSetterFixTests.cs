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
	// Regression tests: these setters previously fired OnPropertyChanged() but forgot to Invalidate(),
	// so the UI went stale on change unless a databinding happened to be attached. Each must now mark
	// the window dirty after a clean render.
	public class StaleSetterFixTests
	{
		private static (ConsoleWindowSystem system, Window window) Host(IWindowControl control)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(60, 16);
			var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16 };
			window.AddControl(control);
			system.AddWindow(window);
			system.Render.UpdateDisplay();
			return (system, window);
		}

		private static void AssertMutationDirties(IWindowControl control, System.Action mutate, string name)
		{
			var (system, window) = Host(control);
			Assert.False(system.WindowStateService.AnyWindowDirty(), $"{name}: clean after render");
			mutate();
			Assert.True(system.WindowStateService.AnyWindowDirty(), $"{name}: mutation must invalidate");
			System.GC.KeepAlive(window);
		}

		[Fact]
		public void TabControl_ShowTabHeader_Invalidates()
		{ var c = new TabControl(); AssertMutationDirties(c, () => c.ShowTabHeader = false, "ShowTabHeader"); }

		[Fact]
		public void MultilineEdit_TabSize_Invalidates()
		{ var c = new MultilineEditControl(); AssertMutationDirties(c, () => c.TabSize = 8, "TabSize"); }

		[Fact]
		public void Table_ColumnSeparator_Invalidates()
		{ var c = new TableControl(); AssertMutationDirties(c, () => c.ColumnSeparator = '|', "ColumnSeparator"); }

		[Fact]
		public void Table_ColumnSeparatorColor_Invalidates()
		{ var c = new TableControl(); AssertMutationDirties(c, () => c.ColumnSeparatorColor = new Color(1, 2, 3), "ColumnSeparatorColor"); }

		[Fact]
		public void Table_ColumnSeparatorPadded_Invalidates()
		{ var c = new TableControl(); AssertMutationDirties(c, () => c.ColumnSeparatorPadded = true, "ColumnSeparatorPadded"); }

		[Fact]
		public void Table_ScrollbarGutter_Invalidates()
		{ var c = new TableControl(); AssertMutationDirties(c, () => c.ScrollbarGutter = true, "ScrollbarGutter"); }

		[Fact]
		public void Table_ColumnResizeEnabled_Invalidates()
		{ var c = new TableControl(); AssertMutationDirties(c, () => c.ColumnResizeEnabled = true, "ColumnResizeEnabled"); }

		[Fact]
		public void Canvas_AutoSize_Invalidates()
		{ var c = new CanvasControl(); AssertMutationDirties(c, () => c.AutoSize = true, "AutoSize"); }
	}
}
