// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

/// <summary>
/// Showcases the standalone <see cref="ScrollbarControl"/> (issue #85): a side-by-side diff-style
/// pair of <see cref="ScrollablePanelControl"/>s, each with its own scrollbar hidden
/// (<c>ShowScrollbar = false</c>), sharing a single <see cref="ScrollbarControl"/> that scrolls
/// both panes together. The bar drives the panes through <see cref="ScrollbarControl.UserValueChanged"/>
/// (only user-initiated moves — drag/click/wheel — push out); each pane's own <c>Scrolled</c> event
/// writes back into <see cref="ScrollbarControl.Value"/>, which only raises
/// <see cref="ScrollbarControl.ValueChanged"/> and so never re-enters the user-driven path. No echo,
/// no guard flag.
/// </summary>
public static class ScrollbarDemoWindow
{
	private const int WindowWidth = 96;
	private const int WindowHeight = 30;
	private const int ViewportRows = 20;
	private const int LineCount = 60;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var intro = Controls.Markup(
				"[bold]Shared Scrollbar — Two Panes, One Bar[/]\n" +
				"[dim]Drag the bar's thumb, click its arrows, or wheel-scroll over either pane — both move together.[/]")
			.WithMargin(1, 0, 1, 1)
			.Build();

		// Left pane: an "old" version, right pane: a "new" version — a diff-style pair so it is
		// obvious the two are meant to move in lockstep. Both hide their own scrollbar; the shared
		// bar between them is the only scrollbar on screen.
		var leftPane = Controls.ScrollablePanel()
			.WithHeight(ViewportRows)
			.WithScrollbar(false)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();

		var rightPane = Controls.ScrollablePanel()
			.WithHeight(ViewportRows)
			.WithScrollbar(false)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();

		for (int i = 1; i <= LineCount; i++)
		{
			// A handful of lines differ between the two sides, like a real diff, so it reads as two
			// versions of the same file rather than two unrelated lists.
			bool changed = i % 7 == 0;
			leftPane.AddControl(Controls.Markup(
					changed
						? $"[dim]{i,3}[/]  [red]- old line {i}[/]"
						: $"[dim]{i,3}[/]  line {i}")
				.Build());
			rightPane.AddControl(Controls.Markup(
					changed
						? $"[dim]{i,3}[/]  [green]+ new line {i}[/]"
						: $"[dim]{i,3}[/]  line {i}")
				.Build());
		}

		var bar = Controls.Scrollbar()
			.Vertical()
			.WithMaximum(LineCount)
			.WithViewportLength(ViewportRows)
			.WithHeight(ViewportRows)
			.Build();

		// The bar pushes to both panes only on a user-initiated move (drag, click, wheel) —
		// never on a value the panes themselves wrote back, since that write is code-set.
		bar.UserValueChanged += (_, value) =>
		{
			leftPane.ScrollVerticalBy(value - leftPane.VerticalScrollOffset);
			rightPane.ScrollVerticalBy(value - rightPane.VerticalScrollOffset);
		};

		// Either pane's own scroll (e.g. wheel over the pane itself) writes back to the bar. A
		// code-set Value only raises ValueChanged, so this never re-triggers UserValueChanged.
		leftPane.Scrolled += (_, e) =>
		{
			if (bar.Value != e.VerticalOffset)
				bar.Value = e.VerticalOffset;
			rightPane.ScrollVerticalBy(e.VerticalOffset - rightPane.VerticalScrollOffset);
		};
		rightPane.Scrolled += (_, e) =>
		{
			if (bar.Value != e.VerticalOffset)
				bar.Value = e.VerticalOffset;
			leftPane.ScrollVerticalBy(e.VerticalOffset - leftPane.VerticalScrollOffset);
		};

		var grid = Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Cells(1))
			.Rows(GridLength.Star(1))
			.ColumnGap(1)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Place(leftPane, 0, 0)
			.Place(rightPane, 0, 1)
			.Place(bar, 0, 2)
			.Build();

		var statusBar = Controls.Markup("[dim]Esc: close[/]")
			.StickyBottom()
			.Build();

		var window = new WindowBuilder(ws)
			.WithTitle("Shared Scrollbar")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControls(intro, grid, statusBar)
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)sender!);
					e.Handled = true;
				}
			})
			.BuildAndShow();
		DemoTheme.ApplyThemeGradient(window, ws);
		return window;
	}
}
