// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression test for issue #61: when a ScrollablePanel nested inside another ScrollablePanel is scrolled
/// so that the inner panel is partly above the outer panel's content viewport, the inner panel's vertical
/// scrollbar leaked UP through the OUTER panel's top border — drawing a '│' (and arrow caps) over the
/// border's '─'. Root cause: DrawVerticalScrollbar/DrawHorizontalScrollbar wrote scrollbar cells without
/// honoring the paint clipRect, unlike every other ScrollablePanel draw method (top/bottom/side borders all
/// clip). The fix clips scrollbar cell writes to clipRect. This test renders the full system and asserts the
/// outer panel's top border row stays an intact horizontal rule (no vertical-stroke intrusion).
/// </summary>
public class NestedScrollablePanelClipTests
{
	[Fact]
	public void InnerScrollbar_DoesNotLeakThroughOuterTopBorder_WhenScrolled()
	{
		var system = ChromeGeometry.CreateSystem(60, 16);

		// Outer bordered scroll panel filling the window.
		var outer = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithName("Outer")
			.WithBorderStyle(BorderStyle.Rounded)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// Several inner bordered scroll panels, each taller than its slot so each shows its own scrollbar.
		// Enough total height that the outer panel itself must scroll.
		for (int p = 0; p < 4; p++)
		{
			var inner = SharpConsoleUI.Builders.Controls.ScrollablePanel()
				.WithBorderStyle(BorderStyle.Rounded)
				.WithHeight(6)
				.WithAutoScroll()
				.Build();
			var lines = new List<string>();
			for (int i = 0; i < 12; i++) lines.Add($"panel {p} line {i}");
			inner.AddControl(new MarkupControl(lines));
			outer.AddControl(inner);
		}

		var window = new WindowBuilder(system)
			.Frameless()
			.Maximized()
			.Build();
		window.AddControl(outer);
		system.WindowStateService.AddWindow(window);

		// Render once to lay out, then scroll the OUTER panel down so the first inner panel is partly clipped
		// off the top, and render again.
		ChromeGeometry.Render(system);
		outer.ScrollVerticalBy(3);
		var snap = ChromeGeometry.Render(system);

		// The outer panel fills the (frameless, maximized) window, so its top border is window row 0.
		// Assert that row is an unbroken horizontal border — no inner scrollbar '│'/'▲'/'▼'/'█' leaked onto it.
		var origin = system.DesktopUpperLeft;
		int topRow = origin.Y + window.Top;
		int leftX = origin.X + window.Left;
		int rightX = leftX + window.Width - 1;
		ChromeGeometry.AssertHorizontalBorderIntact(snap, topRow, leftX, rightX);
	}
}
