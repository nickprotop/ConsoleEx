// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class GridControlTests
	{
		private static MarkupControl NewControl() => new MarkupControl(new List<string> { "x" });

		[Fact]
		public void Place_StoresPlacement()
		{
			var grid = new GridControl();
			var ctrl = NewControl();

			grid.Place(ctrl, 1, 2, rowSpan: 2, colSpan: 3);

			var cells = grid.OrderedCells;
			Assert.Single(cells);
			Assert.Same(ctrl, cells[0].Control);
			Assert.Equal(new GridPlacement(1, 2, 2, 3), cells[0].Placement);
		}

		[Fact]
		public void Place_OutOfRange_Throws()
		{
			var grid = new GridControl();
			grid.ColumnDefinitions.Add(GridLength.Star(1));
			grid.ColumnDefinitions.Add(GridLength.Star(1));
			grid.RowDefinitions.Add(GridLength.Star(1));
			grid.RowDefinitions.Add(GridLength.Star(1));

			Assert.Throws<ArgumentOutOfRangeException>(() => grid.Place(NewControl(), 5, 0));
		}

		[Fact]
		public void Place_NegativeOrZeroSpan_Throws()
		{
			var grid = new GridControl();

			Assert.Throws<ArgumentOutOfRangeException>(() => grid.Place(NewControl(), 0, 0, rowSpan: 0));
		}

		[Fact]
		public void AutoFlow_FillsRowMajor()
		{
			var grid = new GridControl();
			grid.ColumnDefinitions.Add(GridLength.Star(1));
			grid.ColumnDefinitions.Add(GridLength.Star(1));

			IControlHost host = grid;
			var a = NewControl();
			var b = NewControl();
			var c = NewControl();
			host.AddControl(a);
			host.AddControl(b);
			host.AddControl(c);

			var cells = grid.OrderedCells;
			Assert.Equal(3, cells.Count);
			Assert.Equal(new GridPlacement(0, 0), Find(cells, a));
			Assert.Equal(new GridPlacement(0, 1), Find(cells, b));
			Assert.Equal(new GridPlacement(1, 0), Find(cells, c));
			Assert.Equal(2, grid.RowDefinitions.Count);
		}

		[Fact]
		public void AutoFlow_AutoGrowsRows()
		{
			var grid = new GridControl();
			grid.ColumnDefinitions.Add(GridLength.Star(1));
			grid.ColumnDefinitions.Add(GridLength.Star(1));

			IControlHost host = grid;
			IWindowControl? last = null;
			for (int i = 0; i < 5; i++)
			{
				last = NewControl();
				host.AddControl(last);
			}

			Assert.Equal(3, grid.RowDefinitions.Count);
			Assert.Equal(new GridPlacement(2, 0), Find(grid.OrderedCells, last!));
		}

		[Fact]
		public void Overlap_SameCell_Allowed()
		{
			var grid = new GridControl();
			var a = NewControl();
			var b = NewControl();

			grid.Place(a, 0, 0);
			grid.Place(b, 0, 0);

			Assert.Equal(2, grid.OrderedCells.Count);
		}

		[Fact]
		public void GetChildren_RowMajorOrder()
		{
			var grid = new GridControl();
			var atRow1 = NewControl();
			var at00 = NewControl();
			var at01 = NewControl();

			grid.Place(atRow1, 1, 0);
			grid.Place(at00, 0, 0);
			grid.Place(at01, 0, 1);

			var children = grid.GetChildren();
			Assert.Equal(new IWindowControl[] { at00, at01, atRow1 }, children.ToArray());
		}

		[Fact]
		public void RemoveControl_DropsItNoRepack()
		{
			var grid = new GridControl();
			var a = NewControl();
			var b = NewControl();
			grid.Place(a, 0, 0);
			grid.Place(b, 0, 1);

			IControlHost host = grid;
			host.RemoveControl(a);

			Assert.Single(grid.OrderedCells);
			Assert.Equal(new GridPlacement(0, 1), Find(grid.OrderedCells, b));
		}

		[Fact]
		public void ColorRole_RoundTrips()
		{
			var grid = new GridControl
			{
				ColorRole = ColorRole.Primary
			};

			Assert.Equal(ColorRole.Primary, grid.ColorRole);
		}

		[Fact]
		public void Place_SetsChildContainerToGrid()
		{
			var grid = new GridControl();
			var ctrl = NewControl();

			grid.Place(ctrl, 0, 0);

			Assert.Same(grid, ctrl.Container);
		}

		[Fact]
		public void AddControl_SetsChildContainerToGrid()
		{
			var grid = new GridControl();
			grid.ColumnDefinitions.Add(GridLength.Star(1));
			var ctrl = NewControl();

			IControlHost host = grid;
			host.AddControl(ctrl);

			Assert.Same(grid, ctrl.Container);
		}

		private static GridPlacement Find(
			IReadOnlyList<(IWindowControl Control, GridPlacement Placement)> cells, IWindowControl control)
		{
			foreach (var (c, p) in cells)
			{
				if (ReferenceEquals(c, control)) return p;
			}
			throw new InvalidOperationException("control not found in cells");
		}
	}
}
