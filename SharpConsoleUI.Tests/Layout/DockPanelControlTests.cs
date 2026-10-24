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
using Xunit.Abstractions;
using Builder = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// <see cref="DockPanelControl"/> as the answer to "can docking be used from everywhere, not just
/// the window root".
/// </summary>
/// <remarks>
/// A <see cref="GridControl"/> cell holds exactly one control, so a cell cannot itself dock
/// several children. It can hold a dock panel, and then the cell docks - which is what these
/// tests drive, in a real grid, in a real window.
/// </remarks>
public class DockPanelControlTests
{
	private readonly ITestOutputHelper _out;

	public DockPanelControlTests(ITestOutputHelper output) => _out = output;

	private static string Render(Window window)
	{
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });
		return GoldenBuffer.Capture(window, "panel").ToText();
	}

	private static IWindowControl Named(string text, Dock dock = Dock.None)
	{
		var c = Builder.Markup(text).Build();
		c.Dock = dock;
		return c;
	}

	[Fact]
	public void APanelDocksItsChildren()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var window = new Window(system) { Title = "Panel", Width = 40, Height = 10, Left = 0, Top = 0 };

		var panel = new DockPanelControl();
		panel.AddControl(Named("TOP", Dock.Top));
		panel.AddControl(Named("BOTTOM", Dock.Bottom));
		panel.AddControl(Named("LEFT", Dock.Left));
		panel.AddControl(Named("fill"));
		window.AddControl(panel);
		system.AddWindow(window);

		var text = Render(window);
		_out.WriteLine(text);

		var lines = text.Split('\n');
		Assert.StartsWith("TOP", lines[0]);
		Assert.StartsWith("LEFT", lines[1]);
		Assert.Contains("fill", lines[1]);
	}

	[Fact]
	public void APanelInAGridCell_MakesTheCellDock()
	{
		// The whole point: a grid cell holds one control, and that one control can be a dock
		// panel. This is how docking reaches a grid without the grid growing a second placement
		// model.
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Title = "Grid", Width = 60, Height = 12, Left = 0, Top = 0 };

		var panel = new DockPanelControl();
		panel.DockTop.Add(Builder.Markup("CELL HEADER").Build());
		panel.DockBody.Add(Builder.Markup("cell body").Build());

		var grid = Builder.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Star(1))
			// A Star row divides the height it is GIVEN; a content-sized grid has none to divide
			// and collapses to nothing. Fill is what gives the grid a height here.
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Place(Builder.Markup("plain cell").Build(), 0, 0)
			.Place(panel, 0, 1)
			.Build();

		window.AddControl(grid);
		system.AddWindow(window);

		var text = Render(window);
		_out.WriteLine(text);

		// The left cell holds a plain control; the right cell docks a header above a body.
		var lines = text.Split('\n');
		Assert.Contains("plain cell", lines[0]);
		Assert.Contains("CELL HEADER", lines[0]);
		Assert.Contains("cell body", lines[1]);
	}

	[Fact]
	public void TheSurfaces_WorkOnAPanelToo()
	{
		var panel = new DockPanelControl();
		var header = Builder.Markup("H").Build();

		panel.DockTop.Add(header);

		Assert.Equal(Dock.Top, header.Dock);
		Assert.Equal(1, panel.DockTop.Count);
		Assert.Contains(header, panel.Children);

		Assert.True(panel.DockTop.Remove(header));
		Assert.Empty(panel.Children);
	}

	[Fact]
	public void LastChildFill_CanBeTurnedOff()
	{
		var panel = new DockPanelControl();
		Assert.True(panel.LastChildFill);

		panel.LastChildFill = false;
		Assert.False(panel.LastChildFill);
	}

	[Fact]
	public void NestedPanels_Work()
	{
		// A dock region containing another dock region - the composition the spec promised.
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Title = "Nested", Width = 50, Height = 10, Left = 0, Top = 0 };

		var inner = new DockPanelControl();
		inner.DockLeft.Add(Builder.Markup("IN-L").Build());
		inner.DockBody.Add(Builder.Markup("inner body").Build());

		var outer = new DockPanelControl();
		outer.DockTop.Add(Builder.Markup("OUTER TOP").Build());
		outer.DockBody.Add(inner);

		window.AddControl(outer);
		system.AddWindow(window);

		var text = Render(window);
		_out.WriteLine(text);

		var lines = text.Split('\n');
		Assert.StartsWith("OUTER TOP", lines[0]);
		Assert.StartsWith("IN-L", lines[1]);
		Assert.Contains("inner body", lines[1]);
	}

	[Fact]
	public void AddAndRemove_KeepChildrenAndSurfacesInStep()
	{
		var panel = new DockPanelControl();
		var a = Named("A", Dock.Left);
		var b = Named("B");

		panel.AddControl(a);
		panel.AddControl(b);

		Assert.Equal(2, panel.Children.Count);
		Assert.Equal(1, panel.DockLeft.Count);
		Assert.Equal(1, panel.DockBody.Count);

		// Re-docking a child moves it between surfaces with nobody notified - the surfaces are
		// views over the child list, not a second registry.
		b.Dock = Dock.Left;
		Assert.Equal(2, panel.DockLeft.Count);
		Assert.Equal(0, panel.DockBody.Count);

		panel.ClearControls();
		Assert.Empty(panel.Children);
		Assert.Equal(0, panel.DockLeft.Count);
	}
}
