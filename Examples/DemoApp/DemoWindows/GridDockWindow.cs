// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

/// <summary>
/// Docking at the CONTROL level: a <see cref="GridControl"/> with both placements at once.
/// </summary>
/// <remarks>
/// <para>
/// A grid child is normally placed in a cell (<c>Place(control, row, col)</c>). A docked child
/// instead claims a strip off the grid's rectangle, and the tracks are sized against what is
/// left. The two coexist in one container: edges outside, cells inside.
/// </para>
/// <para>
/// The key property is that docking does NOT disturb cell coordinates. A docked child claims no
/// row and no column, so <c>grid[1, 0]</c> means the same cell however many children are
/// docked. The tints here make that visible: the four cells keep their positions as the edges
/// close in around them.
/// </para>
/// </remarks>
public static class GridDockWindow
{
	public static Window Create(ConsoleWindowSystem windowSystem)
	{
		var window = new WindowBuilder(windowSystem)
			.WithTitle("Grid Docking")
			.WithSize(84, 26)
			.Centered()
			.Resizable()
			.BuildAndShow();

		// Docked edges share the warm/cool split used by the Window Docking demo, so the two
		// pages read as the same idea at two levels.
		var topBg = Tint(window, new Color(80, 140, 200), 0.10f);     // cool blue
		var leftBg = Tint(window, new Color(80, 200, 160), 0.10f);    // teal
		var bottomBg = Tint(window, new Color(210, 160, 80), 0.10f);  // warm amber

		// Cells get a quieter tint still: they are inside the edges, so they should recede.
		// Alternating it per cell makes the 2x2 track structure visible even where a cell's
		// text does not reach, which is the point of showing cells at all here.
		var cellBg = Tint(window, new Color(150, 150, 190), 0.10f);
		var cellAltBg = Tint(window, new Color(150, 150, 190), 0.18f);

		window.AddControl(Controls.Markup()
			.AddLines(
				"[bold]One container, two placements[/]",
				"[dim]Docked panels claim the edges. The 2x2 track grid lives inside what is left.[/]")
			.WithMargin(1, 1, 1, 0)
			.StickyTop()
			.Build());

		var grid = new GridControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			// Default is Left, which sizes the grid to its content and leaves the rest of the
			// window empty. Stretch is what makes the Star tracks divide the real width.
			HorizontalAlignment = HorizontalAlignment.Stretch,
			ColumnGap = 1,
			RowGap = 1,
			Margin = new Margin(1, 1, 1, 1)
		};

		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));

		// ---- Docked: outside the tracks -----------------------------------------------------
		// Declared first, so the toolbar spans the full grid width and the sidebar starts below.
		grid.Dock(Controls.Panel()
			.WithHeader("Dock.Top")
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBackgroundColor(topBg)
			.AddControl(Controls.Markup()
				.AddLine("[bold]TOOLBAR[/]  [dim]- declared first, so it spans the whole grid[/]")
				.Build())
			.Build(), Dock.Top);

		// A left dock claims the child's WIDTH. A panel stretches by default, so without an
		// explicit width it would take the whole rectangle and starve the cells.
		grid.Dock(Controls.Panel()
			.WithHeader("Dock.Left")
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBackgroundColor(leftBg)
			.WithWidth(20)
			.AddControl(Controls.Markup()
				.AddLines(
					"[bold]Sidebar[/]",
					"",
					"[dim]Claims a strip[/]",
					"[dim]off the left,[/]",
					"[dim]outside the[/]",
					"[dim]track area.[/]",
					"",
					"[dim]No row, no[/]",
					"[dim]column.[/]")
				.Build())
			.Build(), Dock.Left);

		// Two bottom bands: the rule is declared first and still paints above the status line.
		grid.Dock(Controls.Rule(), Dock.Bottom);
		grid.Dock(Controls.Panel()
			.WithHeader("Dock.Bottom")
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBackgroundColor(bottomBg)
			.AddControl(Controls.Markup()
				.AddLine("[dim]STATUS  - two bands on this edge, painted in the order written[/]")
				.Build())
			.Build(), Dock.Bottom);

		// ---- Placed: inside the tracks, coordinates unaffected by the docks above ------------
		grid.Place(Cell("grid[0,0]", "still row 0, col 0"), 0, 0);
		grid.Place(Cell("grid[0,1]", "docking shifted nothing"), 0, 1);
		grid.Place(Cell("grid[1,0]", "tracks size to what"), 1, 0);
		grid.Place(Cell("grid[1,1]", "the edges left over"), 1, 1);

		// Per-cell backgrounds are a grid feature, not something the content paints: the tint
		// fills the whole cell, so the track rectangles are visible even where text is not.
		for (int r = 0; r < 2; r++)
		{
			for (int c = 0; c < 2; c++)
			{
				// GridCell is a readonly struct handle, so it needs a local to be assigned
				// through; it writes back to the grid's cell store either way.
				var cell = grid[r, c];
				cell.Background = ((r + c) % 2 == 0) ? cellBg : cellAltBg;
			}
		}

		window.AddControl(grid);

		window.AddControl(RuleWith(StickyPosition.Bottom));
		window.AddControl(Controls.Markup()
			.AddLine("[dim]Esc closes.  Resize the window to watch the tracks reflow inside the edges.[/]")
			.WithMargin(1, 0, 1, 0)
			.StickyBottom()
			.Build());

		window.KeyPressed += (_, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				window.Close();
				e.Handled = true;
			}
		};

		return window;
	}

	/// <summary>A rule carrying a sticky position (Controls.Rule returns the control, not a builder).</summary>
	private static IWindowControl RuleWith(StickyPosition sticky)
	{
		var rule = Controls.Rule();
		rule.StickyPosition = sticky;
		return rule;
	}

	private static IWindowControl Cell(string title, string note) =>
		Controls.Markup()
			.AddLines($"[bold]{title}[/]", $"[dim]{note}[/]")
			.WithMargin(1, 0, 1, 0)
			.Build();

	/// <summary>
	/// A subtle tint of the window's own background, so each region reads as distinct without
	/// fighting the theme. Mirrors the Window Docking demo so the two pages match.
	/// </summary>
	private static Color Tint(Window window, Color toward, float amount)
	{
		var bg = window.BackgroundColor;
		bool darkTheme = bg.R + bg.G + bg.B < 3 * 128;

		var baseShade = ColorBlendHelper.BlendColor(
			bg, darkTheme ? new Color(255, 255, 255) : new Color(0, 0, 0), 0.07f);

		return ColorBlendHelper.BlendColor(baseShade, toward, amount);
	}
}
