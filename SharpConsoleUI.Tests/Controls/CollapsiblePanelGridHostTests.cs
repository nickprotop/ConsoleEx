// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression coverage for the "Header Styles" demo topology: two CollapsiblePanels placed
/// side-by-side in a HorizontalGrid with two Flex columns (one borderless, one bordered),
/// hosted inside a ScrollablePanel (as the real demo nests them). Reproduces three reported bugs:
///   #1 click-target offset (the clickable header is offset above the painted header by the grid's top margin),
///   #2 cross-panel interference (a second click on the bordered header toggles the borderless panel),
///   #3 collapse -> the panel's WIDTH collapses to chrome-only so its header all but vanishes.
///
/// Bugs #2 and #3 share a root cause: a collapsed panel without an explicit Width reports a
/// chrome-only width, so the sibling flex column grows over the old click position.
/// </summary>
public class CollapsiblePanelGridHostTests
{
	private const int Width = 80;
	private const int Height = 24;

	private static string StripAnsi(IEnumerable<string> lines) =>
		string.Join("\n", lines.Select(l =>
			System.Text.RegularExpressions.Regex.Replace(l, @"\x1b\[[0-9;]*m", "")));

	private sealed class Topo
	{
		public ConsoleWindowSystem System = null!;
		public Window Window = null!;
		public HorizontalGridControl Grid = null!;
		public CollapsiblePanel Borderless = null!;
		public CollapsiblePanel Bordered = null!;
	}

	/// <summary>
	/// Builds the demo topology: two flex columns (borderless + bordered CollapsiblePanel) in a
	/// HorizontalGrid with a top margin, nested in a ScrollablePanel below <paramref name="spacerLines"/>
	/// spacer rows. Neither panel sets an explicit Width — matching the demo (which triggers the bugs).
	/// </summary>
	private static Topo BuildInScrollablePanel(int spacerLines = 6, int gridMarginTop = 1)
	{
		var borderless = ControlsFactory.CollapsiblePanel("Borderless")
			.Expanded()
			.WithHeaderStyle(CollapsibleHeaderStyle.Borderless)
			.AddControl(new MarkupControl(new List<string> { "BORDERLESS-BODY-A" }))
			.Build();

		var bordered = ControlsFactory.CollapsiblePanel("Bordered")
			.Expanded()
			.WithHeaderStyle(CollapsibleHeaderStyle.Bordered)
			.AddControl(new MarkupControl(new List<string> { "BORDERED-BODY-A" }))
			.Build();

		var grid = ControlsFactory.HorizontalGrid()
			.Column(col => col.Flex().Add(borderless))
			.Column(col => col.Flex().Add(bordered))
			.WithMargin(1, gridMarginTop, 1, 0)
			.Build();

		var scroller = ControlsFactory.ScrollablePanel();
		for (int i = 0; i < spacerLines; i++)
			scroller.AddControl(new MarkupControl(new List<string> { $"spacer {i:00}" }));
		scroller.AddControl(grid);
		var root = scroller.WithVerticalAlignment(VerticalAlignment.Fill).Build();

		var system = TestWindowSystemBuilder.CreateTestSystem(Width, Height);
		var window = new Window(system) { Left = 0, Top = 0, Width = Width, Height = Height };
		window.AddControl(root);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return new Topo { System = system, Window = window, Grid = grid, Borderless = borderless, Bordered = bordered };
	}

	private static List<string> RenderLines(Topo t) =>
		StripAnsi(t.Window.RenderAndGetVisibleContent()).Split('\n').ToList();

	/// <summary>Left-clicks a window content cell (content coords -> screen coords) through the real dispatch path.</summary>
	private static void ClickContent(Topo t, int x, int contentY)
	{
		int screenX = t.Window.Left + 1 + x;
		int screenY = t.Window.Top + 1 + contentY;
		var driver = (MockConsoleDriver)t.System.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(screenX, screenY));
		t.System.Input.ProcessInput();
	}

	private static int PaintedRow(List<string> lines, string needle) =>
		lines.FindIndex(l => l.Contains(needle));

	private static int PaintedCol(List<string> lines, int row, string needle) =>
		lines[row].IndexOf(needle, System.StringComparison.Ordinal);

	// ------------------------------------------------------------------
	// Bug #1 (FIXED) — a click on the panel's REAL painted header row toggles it (no offset).
	// The grid now subtracts its Margin.Top from the routed Y, symmetric with Margin.Left on X.
	// ------------------------------------------------------------------
	[Fact]
	public void Bug1_ClickOnPaintedHeaderRow_TogglesPanel_NoOffset()
	{
		const int gridMarginTop = 1;
		var t = BuildInScrollablePanel(gridMarginTop: gridMarginTop);
		var lines = RenderLines(t);
		int paintedRow = PaintedRow(lines, "Borderless");
		int col = PaintedCol(lines, paintedRow, "Borderless");

		// Clicking the row where the header actually paints toggles it (no offset).
		bool before = t.Borderless.IsExpanded;
		ClickContent(t, x: col, contentY: paintedRow);
		Assert.NotEqual(before, t.Borderless.IsExpanded);

		// Clicking 'gridMarginTop' rows ABOVE the painted header (the OLD buggy target) does NOT toggle.
		var t2 = BuildInScrollablePanel(gridMarginTop: gridMarginTop);
		bool before2 = t2.Borderless.IsExpanded;
		ClickContent(t2, x: col, contentY: paintedRow - gridMarginTop);
		Assert.Equal(before2, t2.Borderless.IsExpanded);
	}

	[Fact]
	public void Bug1_ClickableRow_MatchesPaintedRow_ForAnyGridTopMargin()
	{
		foreach (var (spacers, gmt) in new[] { (6, 0), (6, 1), (6, 2) })
		{
			var t = BuildInScrollablePanel(spacers, gmt);
			int paintedRow = PaintedRow(RenderLines(t), "Borderless");

			int? toggleRow = null;
			for (int row = 0; row <= paintedRow + 2 && toggleRow == null; row++)
			{
				var probe = BuildInScrollablePanel(spacers, gmt);
				bool before = probe.Borderless.IsExpanded;
				ClickContent(probe, x: 3, contentY: row);
				if (probe.Borderless.IsExpanded != before) toggleRow = row;
			}

			Assert.NotNull(toggleRow);
			// The clickable header row now equals the painted header row regardless of grid top margin.
			Assert.Equal(paintedRow, toggleRow!.Value);
		}
	}

	// ------------------------------------------------------------------
	// Bug #2 (FIXED) — clicking the bordered header twice toggles ONLY the bordered panel both
	// times; the borderless panel is never affected. The collapsed panel keeps its column width
	// (Bug #3 fix), so the sibling column no longer slides under the cursor between clicks.
	// ------------------------------------------------------------------
	[Fact]
	public void Bug2_SecondClickOnBorderedHeader_OnlyTogglesBorderedPanel()
	{
		var t = BuildInScrollablePanel();

		// Resolve the bordered header's painted position; click the REAL painted row (no offset, bug #1 fixed).
		var lines = RenderLines(t);
		int row = PaintedRow(lines, "Bordered");
		int col = PaintedCol(lines, row, "Bordered");

		// Click #1: collapses the bordered panel; borderless untouched.
		ClickContent(t, x: col, contentY: row);
		t.System.Render.UpdateDisplay();
		Assert.False(t.Bordered.IsExpanded);
		Assert.True(t.Borderless.IsExpanded);

		// Click #2 at the SAME painted column/row re-expands ONLY the bordered panel; the
		// borderless panel's IsExpanded is unchanged (no cross-panel interference).
		bool borderlessBefore = t.Borderless.IsExpanded;
		ClickContent(t, x: col, contentY: row);
		t.System.Render.UpdateDisplay();
		Assert.True(t.Bordered.IsExpanded);
		Assert.Equal(borderlessBefore, t.Borderless.IsExpanded); // borderless never toggled
	}

	// ------------------------------------------------------------------
	// Bug #3 (FIXED) — collapsing a panel with no explicit Width keeps its allocated column width;
	// the header (box frame + title) stays full-width instead of shrinking to a chrome-only stub.
	// ------------------------------------------------------------------
	[Fact]
	public void Bug3_CollapsedPanelWithoutExplicitWidth_KeepsColumnWidth()
	{
		var t = BuildInScrollablePanel();

		// Expanded: the bordered box frames a wide body (top border spans the whole flex column).
		var before = RenderLines(t);
		int row = PaintedRow(before, "Bordered");
		// Width of the top-border run "┌─...─┐" containing the title.
		int expandedBoxWidth = before[row].Count(ch => ch == '─') + before[row].Count(ch => ch == '┌' || ch == '┐');
		Assert.True(expandedBoxWidth > 4, $"Expanded bordered box should be wide (got {expandedBoxWidth}).");

		t.Bordered.Collapse();
		t.System.Render.UpdateDisplay();
		t.System.Render.UpdateDisplay();

		// FIXED: the collapsed panel keeps its allocated flex column width, so the header box stays
		// wide and the title is still visible (no shrink to a "┌┐" stub).
		var after = RenderLines(t);
		int collapsedRow = PaintedRow(after, "Bordered");
		Assert.True(collapsedRow >= 0, "Collapsed header title should still be painted.");
		int collapsedBoxWidth = after[collapsedRow].Count(ch => ch == '─')
			+ after[collapsedRow].Count(ch => ch == '┌' || ch == '┐');
		// The collapsed header keeps (roughly) the expanded box width — not a 2-col chrome stub.
		Assert.True(collapsedBoxWidth >= expandedBoxWidth,
			$"Collapsed box width ({collapsedBoxWidth}) should match the flex column width ({expandedBoxWidth}).");
		Assert.DoesNotContain(after, l => l.Contains("┌┐")); // not a 2-col stub
	}

	[Fact]
	public void Bug3_BorderlessCollapse_HeaderRowStaysVisible()
	{
		var t = BuildInScrollablePanel();
		t.Borderless.Collapse();
		t.System.Render.UpdateDisplay();
		t.System.Render.UpdateDisplay();

		// FIXED: the borderless panel keeps its allocated flex column width when collapsed, so the
		// collapsed header (with its title) still paints.
		var lines = RenderLines(t);
		Assert.Contains(lines, l => l.Contains("Borderless"));
	}
}
