// -----------------------------------------------------------------------
// ConsoleEx - NavigationView direct-IWindowControl content tests
// -----------------------------------------------------------------------
using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using CB = SharpConsoleUI.Builders.Controls;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

public class NavigationViewDirectContentTests
{
	[Fact]
	public void DirectFillGrid_FillsContentArea()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 30);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 100, Height = 30 };

		var grid = CB.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Star(1), GridLength.Star(1))
			.Place(CB.Markup().AddLines("TL").Build(), 0, 0)
			.Place(CB.Markup().AddLines("TR").Build(), 0, 1)
			.Place(CB.Markup().AddLines("BL").Build(), 1, 0)
			.Place(CB.Markup().AddLines("BR").Build(), 1, 1)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		var nav = new NavigationView { VerticalAlignment = VerticalAlignment.Fill };
		var item = nav.AddItem("Page 1");
		nav.SetItemContent(item, grid);   // NEW direct-control overload

		window.AddControl(nav);
		system.AddWindow(window);
		nav.SelectedIndex = 0;

		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		Assert.True(grid.ActualHeight > 15,
			$"direct-hosted Fill grid did not fill: ActualHeight={grid.ActualHeight} (expected >15)");
	}

	[Fact]
	public void SpcPopulatePath_StillWorks_BackCompat()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 30);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 100, Height = 30 };

		var nav = new NavigationView();
		var item = nav.AddItem("Page 1");
		bool populated = false;
		nav.SetItemContent(item, panel => { panel.AddControl(CB.Markup().AddLines("hi").Build()); populated = true; });

		window.AddControl(nav);
		system.AddWindow(window);
		nav.SelectedIndex = 0;
		system.Render.UpdateDisplay();

		Assert.True(populated, "the existing SPC populate path must still run unchanged");
	}
}
