// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Layout
{
	// Gate #2: the reverted experiment oscillated a table's width frame-to-frame because ContentSizedStars
	// measured Star tracks as Auto (content width) rather than at their provisional allocation. This asserts
	// the fix converges: a table in a Star column of a ContentSizedStars grid renders IDENTICALLY across
	// re-renders (no oscillation), and the width is stable.
	public class ContentSizedStarConvergenceTests
	{
		[Fact]
		public void TableInStarColumn_ContentSizedStars_ConvergesAcrossReRenders()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
			var window = new Window(system) { Left = 0, Top = 0, Width = 80, Height = 20 };

			var grid = new GridControl { ContentSizedStars = true, VerticalAlignment = VerticalAlignment.Fill };
			grid.ColumnDefinitions.Add(GridLength.Star());  // the flex column
			grid.ColumnDefinitions.Add(GridLength.Auto());  // a content-sized sibling
			grid.RowDefinitions.Add(GridLength.Star());

			var table = new TableControl();
			table.AddColumn("Name"); table.AddColumn("Value");
			table.AddRow("alpha", "one"); table.AddRow("bravo", "two"); table.AddRow("charlie", "three");
			grid.Place(table, 0, 0);
			grid.Place(new MarkupControl(new System.Collections.Generic.List<string> { "side" }), 0, 1);

			window.AddControl(grid);
			system.AddWindow(window);

			// Render several frames; capture each. If the width oscillates, frames differ.
			var frames = new System.Collections.Generic.List<string>();
			for (int i = 0; i < 4; i++)
			{
				system.Render.UpdateDisplay();
				var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
				var sb = new System.Text.StringBuilder();
				for (int y = 0; y < snap.Height; y++)
				{
					for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
					sb.Append('\n');
				}
				frames.Add(sb.ToString());
			}

			// Every frame after the first must be identical to the first — no oscillation.
			for (int i = 1; i < frames.Count; i++)
				Assert.Equal(frames[0], frames[i]);
			System.GC.KeepAlive(window);
		}

		// "Real thing" gate: the top-level test above measures the grid with a BOUNDED height (the window
		// interior), so the collapse-to-0 / content-sized-Star branch never actually fires — the plain
		// bounded Star split handles it. This test nests the grid inside a ScrollablePanelControl, which
		// measures its content with an UNBOUNDED height (int.MaxValue). That is the real
		// HorizontalGridControl usage path and the ONLY one that exercises the ContentSizedStars measure branch:
		// with the flag on, the Star ROW self-sizes to the table's content height (measured at its
		// provisional allocation) instead of collapsing to 0. This asserts that path CONVERGES — the table
		// renders identically across re-renders (measure height == arrange height == content, a fixed point),
		// and that the table's rows survive (a collapsed-to-0 Star row would leave the viewport blank).
		[Fact]
		public void TableInStarRow_InScrollablePanel_ContentSizedStars_ConvergesAndTableSurvives()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);
			var window = new Window(system) { Left = 0, Top = 0, Width = 80, Height = 20 };

			var grid = new GridControl { ContentSizedStars = true, VerticalAlignment = VerticalAlignment.Fill };
			grid.ColumnDefinitions.Add(GridLength.Star());  // the flex column
			grid.ColumnDefinitions.Add(GridLength.Auto());  // a content-sized sibling
			grid.RowDefinitions.Add(GridLength.Star());     // the flex row — measured UNBOUNDED inside the SPC

			var table = new TableControl();
			table.AddColumn("Name"); table.AddColumn("Value");
			table.AddRow("alpha", "one"); table.AddRow("bravo", "two"); table.AddRow("charlie", "three");
			grid.Place(table, 0, 0);
			grid.Place(new MarkupControl(new System.Collections.Generic.List<string> { "side" }), 0, 1);

			// The ScrollablePanel measures its content with an unbounded height — this is what drives the
			// grid's Star row through the ContentSizedStars branch under test.
			var spc = new ScrollablePanelControl();
			spc.AddControl(grid);

			window.AddControl(spc);
			system.AddWindow(window);

			var frames = new System.Collections.Generic.List<string>();
			for (int i = 0; i < 4; i++)
			{
				system.Render.UpdateDisplay();
				var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
				var sb = new System.Text.StringBuilder();
				for (int y = 0; y < snap.Height; y++)
				{
					for (int x = 0; x < snap.Width; x++) sb.Append(snap.GetCell(x, y).Character.ToString());
					sb.Append('\n');
				}
				frames.Add(sb.ToString());
			}

			// The unbounded-Star fix must not oscillate: every frame identical to the first.
			for (int i = 1; i < frames.Count; i++)
				Assert.Equal(frames[0], frames[i]);

			// And the table content must actually be present (the Star row self-sized to it, did not collapse).
			Assert.Contains("charlie", frames[0]);
			Assert.Contains("side", frames[0]);
			System.GC.KeepAlive(window);
		}
	}
}
