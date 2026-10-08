// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;
using Build = SharpConsoleUI.Builders.Controls;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// The Quick Start in <c>docs/controls/GridControl.md</c>, run as written.
/// </summary>
/// <remarks>
/// It used to render only its header: the content row is a <c>Star</c> track, and a grid left on
/// its default Left/Top alignment is sized to its content, so there was no leftover space for the
/// star row to take and the two panels in it were never painted. Nothing threw. A sample whose
/// main content silently disappears is worse than no sample, so it is pinned here.
/// </remarks>
public class GridDocSampleTests
{
	private static string Render(out int gridHeight)
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(100, 30),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system) { Left = 0, Top = 0, Width = 50, Height = 12 };

		var leftPanel = Build.Markup("LEFT").Build();
		var rightPanel = Build.Markup("RIGHT").Build();

		var grid = Build.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Auto(), GridLength.Star(1))
			.RowGap(1)
			.ColumnGap(2)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Place(Build.Markup("[bold]Header[/]").Build(), 0, 0, colSpan: 2)
			.Place(leftPanel, 1, 0)
			.Place(rightPanel, 1, 1)
			.Build();

		window.AddControl(grid);
		system.AddWindow(window);
		var lines = window.RenderAndGetVisibleContent();
		gridHeight = grid.ActualHeight;

		return string.Join("\n", lines.Select(l => Regex.Replace(l, @"\x1b\[[0-9;?]*[ -/]*[@-~]", "")));
	}

	[Fact]
	public void TheQuickStart_PaintsItsHeader()
		=> Assert.Contains("Header", Render(out _));

	/// <summary>The part that used to vanish: both panels live in the Star row.</summary>
	[Fact]
	public void TheQuickStart_PaintsTheContentInItsStarRow()
	{
		string text = Render(out _);

		Assert.Contains("LEFT", text);
		Assert.Contains("RIGHT", text);
	}

	/// <summary>
	/// The grid fills its window rather than shrinking to the header. Two rows would mean the star
	/// track collapsed again, with the panels gone and no other sign of it.
	/// </summary>
	[Fact]
	public void TheQuickStart_FillsItsWindow()
	{
		Render(out int height);

		Assert.True(height > 2, $"the grid is {height} rows tall, so its Star row collapsed");
	}
}
