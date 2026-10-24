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
/// Docking at the WINDOW level: the four edge surfaces, each holding a whole panel.
/// </summary>
/// <remarks>
/// <para>
/// A window's root layout is what turns its children into a picture. It used to be hardcoded;
/// now it is <see cref="Window.Root"/>, and the default (<see cref="DockRootLayout"/>) gives
/// four edges around a scrolling middle. <c>StickyTop()</c> / <c>StickyBottom()</c> still work
/// exactly as before, because sticky is now a view over the same Dock value the edges use.
/// </para>
/// <para>
/// Each edge is a real surface, not a slot for a label: it takes any control, including a
/// container. Every edge here holds a bordered <see cref="PanelControl"/> with its own
/// background, which is also what makes the regions visible as regions.
/// </para>
/// </remarks>
public static class DockSurfacesWindow
{
	public static Window Create(ConsoleWindowSystem windowSystem)
	{
		var window = new WindowBuilder(windowSystem)
			.WithTitle("Window Docking")
			.WithSize(84, 26)
			.Centered()
			.Resizable()
			.BuildAndShow();

		// Four subtle shades over the same lifted base, so the edges are distinguishable from
		// each other and from the body without shouting.
		var topBg = Tint(window, new Color(80, 140, 200), 0.10f);     // cool blue
		var leftBg = Tint(window, new Color(80, 200, 160), 0.10f);    // teal
		var rightBg = Tint(window, new Color(190, 120, 200), 0.10f);  // violet
		var bottomBg = Tint(window, new Color(210, 160, 80), 0.10f);  // warm amber

		// ---- Top edge --------------------------------------------------------------------
		// Declared FIRST, so it claims the whole top before the sidebars narrow the rectangle.
		// That ordering is the dock rule: each child shrinks what is left for the rest.
		window.DockTop.Add(Controls.Panel()
			.WithHeader("DockTop")
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBackgroundColor(topBg)
			.AddControl(Controls.Markup()
				.AddLine("[bold]A panel, not a label[/]  [dim]- every edge is a surface that takes any control[/]")
				.Build())
			.Build());

		// ---- Left edge --------------------------------------------------------------------
		window.DockLeft.Add(Controls.Panel()
			.WithHeader("DockLeft")
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBackgroundColor(leftBg)
			// A left dock claims the child's WIDTH. A panel stretches by default, so without an
			// explicit width it would take the whole rectangle and starve the other edges.
			.WithWidth(20)
			.AddControl(Controls.Markup()
				.AddLines(
					"[bold]Edges[/]",
					"",
					"DockTop",
					"DockLeft",
					"DockRight",
					"DockBottom",
					"DockBody",
					"",
					"[dim]Each is a live[/]",
					"[dim]view over the[/]",
					"[dim]child list, not[/]",
					"[dim]a second copy.[/]")
				.Build())
			.Build());

		// ---- Right edge -------------------------------------------------------------------
		window.DockRight.Add(Controls.Panel()
			.WithHeader("DockRight")
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBackgroundColor(rightBg)
			.WithWidth(22)
			.AddControl(Controls.Markup()
				.AddLines(
					"[bold]Also true[/]",
					"",
					"[dim]StickyTop and[/]",
					"[dim]StickyBottom are[/]",
					"[dim]the same Dock[/]",
					"[dim]value, projected.[/]",
					"",
					"[dim]90 call sites[/]",
					"[dim]needed no change.[/]")
				.Build())
			.Build());

		// ---- Bottom edge ------------------------------------------------------------------
		window.DockBottom.Add(Controls.Panel()
			.WithHeader("DockBottom")
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBackgroundColor(bottomBg)
			.AddControl(Controls.Markup()
				.AddLine("[dim]Wheel / arrows / PgUp / PgDn scroll the middle. Every edge stays put. Esc closes.[/]")
				.Build())
			.Build());

		// ---- The body: whatever is left, and the only part that scrolls ---------------------
		for (int i = 1; i <= 40; i++)
		{
			window.DockBody.Add(Controls.Markup()
				.AddLine($"  body line {i,2}")
				.Build());
		}

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

	/// <summary>
	/// A subtle tint of the window's own background, so each edge reads as a distinct region
	/// without any of them fighting the theme.
	/// </summary>
	/// <remarks>
	/// Derived from the live window background rather than hardcoded, so switching theme
	/// recolours the bands with everything else. A light theme blends toward black and a dark
	/// one toward white, which keeps the tint visible either way instead of vanishing into the
	/// background it was supposed to stand out from.
	/// </remarks>
	private static Color Tint(Window window, Color toward, float amount)
	{
		var bg = window.BackgroundColor;
		bool darkTheme = bg.R + bg.G + bg.B < 3 * 128;

		// Lift a dark theme's bands up off the background, settle a light theme's down into it.
		var baseShade = ColorBlendHelper.BlendColor(
			bg, darkTheme ? new Color(255, 255, 255) : new Color(0, 0, 0), 0.07f);

		return ColorBlendHelper.BlendColor(baseShade, toward, amount);
	}
}
