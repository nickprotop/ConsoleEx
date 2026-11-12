// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A grid reports the height of the CELL a control occupies, not the height of the whole grid.
/// </summary>
/// <remarks>
/// GetVisibleHeightForControl is how a scrolling control learns how many rows it actually has.
/// GridControl answered with its own ActualHeight regardless of which child asked, so a list in
/// a short row was told it had the entire grid's height, concluded that all its items fitted,
/// and never scrolled - while the same list inside a ScrollablePanelControl worked, because that
/// container reports its own viewport.
///
/// Found in LazyDotIDE: the output panel stopped scrolling when the window moved from stacked
/// window children to a grid. Nothing about the list changed; only what its container told it.
/// </remarks>
public class GridVisibleHeightTests
{
	[Fact]
	public void GridReportsTheCellHeight_NotItsOwn()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 30);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 30 };

		var grid = new GridControl
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill
		};
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));   // tall row
		grid.RowDefinitions.Add(GridLength.Cells(5));  // short row

		var tall = new ListControl { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Fill };
		var shortOne = new ListControl { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Fill };
		grid.Place(tall, 0, 0);
		grid.Place(shortOne, 1, 0);

		window.AddControl(grid);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });

		int? tallHeight = ((IContainer)grid).GetVisibleHeightForControl(tall);
		int? shortHeight = ((IContainer)grid).GetVisibleHeightForControl(shortOne);

		Assert.NotNull(tallHeight);
		Assert.NotNull(shortHeight);

		// The short row is 5 cells; the tall one takes the rest. They must not be equal, and the
		// short one must not be reported as the whole grid.
		Assert.True(shortHeight < tallHeight,
			$"short row reported {shortHeight}, tall row {tallHeight} - the grid answered with its own height");
		Assert.True(shortHeight <= 5,
			$"short row is 5 cells but reported {shortHeight}");
	}

	/// <summary>
	/// A tab reports the rows its CONTENT gets, which is its own height less the header - not
	/// whatever its parent would say about a control the parent has never seen.
	/// </summary>
	[Fact]
	public void TabInAGridCell_ReportsItsContentHeight()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 30);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 30 };

		var grid = new GridControl
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill
		};
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Cells(8));

		var filler = new ListControl { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Fill };
		var tabs = new TabControl { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Fill };
		var inner = new ListControl { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Fill };
		tabs.AddTab("One", inner);

		grid.Place(filler, 0, 0);
		grid.Place(tabs, 1, 0);

		window.AddControl(grid);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });

		int? contentHeight = ((IContainer)tabs).GetVisibleHeightForControl(inner);

		Assert.NotNull(contentHeight);
		Assert.True(contentHeight <= 8,
			$"the tab sits in an 8-cell row but reported {contentHeight} for its content");
		Assert.True(contentHeight > 0, "a visible tab must report some content height");
	}
}
