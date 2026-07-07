// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering
{
	// Differential golden-render parity: HorizontalGridControl (grid-backed drop-in) must render IDENTICALLY
	// to the real HorizontalGridControl (HGC) — HGC is the oracle. For each scenario we build the SAME
	// column config into both controls, render each at a fixed 80x24 size via the headless harness, and
	// assert the rendered cell strings are byte-identical. This is the exact test net whose absence let a
	// flex-column oscillation ship + get reverted. Part 2 adds multi-frame stability that static goldens
	// cannot catch.
	public class HorizontalGridControlParityTests
	{
		private const int Width = 80;
		private const int Height = 24;

		// Renders a single control at 80x24 in a fresh headless system and returns the rendered cell string.
		private static string RenderControl(Func<IWindowControl> buildControl)
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(Width, Height);
			var window = new Window(system) { Left = 0, Top = 0, Width = Width, Height = Height };
			window.AddControl(buildControl());
			system.AddWindow(window);
			system.Render.UpdateDisplay();
			var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
			var s = Snapshot(snap);
			GC.KeepAlive(window);
			return s;
		}

		private static string Snapshot(SharpConsoleUI.Diagnostics.Snapshots.CharacterBufferSnapshot snap)
		{
			var sb = new StringBuilder();
			for (int y = 0; y < snap.Height; y++)
			{
				for (int x = 0; x < snap.Width; x++)
					sb.Append(snap.GetCell(x, y).Character.ToString());
				sb.Append('\n');
			}
			return sb.ToString();
		}

		// Helper: assert HGC output == HorizontalGridControl output for two build funcs producing the same config.
		private static void AssertParity(Func<IWindowControl> hgc, Func<IWindowControl> gridBacked)
		{
			var expected = RenderControl(hgc);
			var actual = RenderControl(gridBacked);
			Assert.Equal(expected, actual);
		}

		private static MarkupControl Markup(string text) => new MarkupControl(new List<string> { text });

		private static ButtonControl Button(string label) => new ButtonControl { Text = label };

		private static TableControl BuildTable()
		{
			var table = new TableControl();
			table.AddColumn("Name");
			table.AddColumn("Value");
			table.AddRow("alpha", "one");
			table.AddRow("bravo", "two");
			table.AddRow("charlie", "three");
			return table;
		}

		// ---- Part 1: differential golden-render vs HGC ----

		[Fact]
		public void Parity_ButtonRow()
		{
			AssertParity(
				() => HorizontalGridControl.ButtonRow(Button("OK"), Button("Cancel"), Button("Apply")),
				() => HorizontalGridControl.ButtonRow(Button("OK"), Button("Cancel"), Button("Apply")));
		}

		[Fact]
		public void Parity_FromControls()
		{
			AssertParity(
				() => HorizontalGridControl.FromControls(Markup("left"), Markup("right")),
				() => HorizontalGridControl.FromControls(Markup("left"), Markup("right")));
		}

		[Fact]
		public void Parity_MixedWidthFlexAutoColumns()
		{
			AssertParity(
				() =>
				{
					var grid = new HorizontalGridControl();
					var fixedCol = new ColumnContainer(grid) { Width = 20 };
					fixedCol.AddContent(Markup("fixed20"));
					grid.AddColumn(fixedCol);
					var starCol = new ColumnContainer(grid) { FlexFactor = 1 };
					starCol.AddContent(Markup("flex"));
					grid.AddColumn(starCol);
					var autoCol = new ColumnContainer(grid) { FlexFactor = 0 };
					autoCol.AddContent(Markup("auto"));
					grid.AddColumn(autoCol);
					return grid;
				},
				() =>
				{
					var grid = new HorizontalGridControl();
					var fixedCol = new ColumnContainer(grid) { Width = 20 };
					fixedCol.AddContent(Markup("fixed20"));
					grid.AddColumn(fixedCol);
					var starCol = new ColumnContainer(grid) { FlexFactor = 1 };
					starCol.AddContent(Markup("flex"));
					grid.AddColumn(starCol);
					var autoCol = new ColumnContainer(grid) { FlexFactor = 0 };
					autoCol.AddContent(Markup("auto"));
					grid.AddColumn(autoCol);
					return grid;
				});
		}

		[Fact]
		public void Parity_WithSplitters()
		{
			AssertParity(
				() =>
				{
					var grid = new HorizontalGridControl();
					var c1 = new ColumnContainer(grid) { FlexFactor = 1 };
					c1.AddContent(Markup("first"));
					grid.AddColumn(c1);
					grid.AddSplitterAfter(c1);
					var c2 = new ColumnContainer(grid) { FlexFactor = 1 };
					c2.AddContent(Markup("second"));
					grid.AddColumn(c2);
					return grid;
				},
				() =>
				{
					var grid = new HorizontalGridControl();
					var c1 = new ColumnContainer(grid) { FlexFactor = 1 };
					c1.AddContent(Markup("first"));
					grid.AddColumn(c1);
					grid.AddSplitterAfter(c1);
					var c2 = new ColumnContainer(grid) { FlexFactor = 1 };
					c2.AddContent(Markup("second"));
					grid.AddColumn(c2);
					return grid;
				});
		}

		[Fact]
		public void Parity_HiddenColumn()
		{
			AssertParity(
				() =>
				{
					var grid = new HorizontalGridControl();
					var c1 = new ColumnContainer(grid) { FlexFactor = 1 };
					c1.AddContent(Markup("one"));
					grid.AddColumn(c1);
					var c2 = new ColumnContainer(grid) { FlexFactor = 1 };
					c2.AddContent(Markup("hidden"));
					c2.Visible = false;
					grid.AddColumn(c2);
					var c3 = new ColumnContainer(grid) { FlexFactor = 1 };
					c3.AddContent(Markup("three"));
					grid.AddColumn(c3);
					return grid;
				},
				() =>
				{
					var grid = new HorizontalGridControl();
					var c1 = new ColumnContainer(grid) { FlexFactor = 1 };
					c1.AddContent(Markup("one"));
					grid.AddColumn(c1);
					var c2 = new ColumnContainer(grid) { FlexFactor = 1 };
					c2.AddContent(Markup("hidden"));
					c2.Visible = false;
					grid.AddColumn(c2);
					var c3 = new ColumnContainer(grid) { FlexFactor = 1 };
					c3.AddContent(Markup("three"));
					grid.AddColumn(c3);
					return grid;
				});
		}

		[Fact]
		public void Parity_SingleColumn()
		{
			AssertParity(
				() =>
				{
					var grid = new HorizontalGridControl();
					var c1 = new ColumnContainer(grid) { FlexFactor = 1 };
					c1.AddContent(Markup("only"));
					grid.AddColumn(c1);
					return grid;
				},
				() =>
				{
					var grid = new HorizontalGridControl();
					var c1 = new ColumnContainer(grid) { FlexFactor = 1 };
					c1.AddContent(Markup("only"));
					grid.AddColumn(c1);
					return grid;
				});
		}

		[Fact]
		public void Parity_Empty()
		{
			AssertParity(
				() => new HorizontalGridControl(),
				() => new HorizontalGridControl());
		}

		[Fact]
		public void Parity_NestedInScrollablePanel()
		{
			// The SPC measures its content UNBOUNDED — this drives the ContentSizedStars path, the important parity case.
			AssertParity(
				() =>
				{
					var grid = new HorizontalGridControl();
					var c1 = new ColumnContainer(grid) { FlexFactor = 1 };
					c1.AddContent(Markup("scrolled-left"));
					grid.AddColumn(c1);
					var c2 = new ColumnContainer(grid) { FlexFactor = 0 };
					c2.AddContent(Markup("auto-right"));
					grid.AddColumn(c2);
					var spc = new ScrollablePanelControl();
					spc.AddControl(grid);
					return spc;
				},
				() =>
				{
					var grid = new HorizontalGridControl();
					var c1 = new ColumnContainer(grid) { FlexFactor = 1 };
					c1.AddContent(Markup("scrolled-left"));
					grid.AddColumn(c1);
					var c2 = new ColumnContainer(grid) { FlexFactor = 0 };
					c2.AddContent(Markup("auto-right"));
					grid.AddColumn(c2);
					var spc = new ScrollablePanelControl();
					spc.AddControl(grid);
					return spc;
				});
		}

		[Fact]
		public void Parity_TableInFlexColumn()
		{
			AssertParity(
				() =>
				{
					var grid = new HorizontalGridControl();
					var flexCol = new ColumnContainer(grid) { FlexFactor = 1 };
					flexCol.AddContent(BuildTable());
					grid.AddColumn(flexCol);
					var autoCol = new ColumnContainer(grid) { FlexFactor = 0 };
					autoCol.AddContent(Markup("side"));
					grid.AddColumn(autoCol);
					return grid;
				},
				() =>
				{
					var grid = new HorizontalGridControl();
					var flexCol = new ColumnContainer(grid) { FlexFactor = 1 };
					flexCol.AddContent(BuildTable());
					grid.AddColumn(flexCol);
					var autoCol = new ColumnContainer(grid) { FlexFactor = 0 };
					autoCol.AddContent(Markup("side"));
					grid.AddColumn(autoCol);
					return grid;
				});
		}

		// ---- Part 2: multi-frame stability (the decisive new test the revert lacked) ----

		[Fact]
		public void MultiFrameStability_TableInFlexColumn_InScrollablePanel_ConvergesAcrossReRenders()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(Width, Height);
			var window = new Window(system) { Left = 0, Top = 0, Width = Width, Height = Height };

			var grid = new HorizontalGridControl();
			var flexCol = new ColumnContainer(grid) { FlexFactor = 1 };
			flexCol.AddContent(BuildTable());
			grid.AddColumn(flexCol);
			var autoCol = new ColumnContainer(grid) { FlexFactor = 0 };
			autoCol.AddContent(Markup("side"));
			grid.AddColumn(autoCol);

			// SPC measures content UNBOUNDED — the ContentSizedStars branch that oscillated in the reverted experiment.
			var spc = new ScrollablePanelControl();
			spc.AddControl(grid);

			window.AddControl(spc);
			system.AddWindow(window);

			var frames = new List<string>();
			for (int i = 0; i < 4; i++)
			{
				system.Render.UpdateDisplay();
				var snap = system.RenderingDiagnostics!.LastBufferSnapshot!;
				frames.Add(Snapshot(snap));
			}

			// Every frame after the first must be identical — no oscillation.
			for (int i = 1; i < frames.Count; i++)
				Assert.Equal(frames[0], frames[i]);

			// And the table content must actually be present (the flex column self-sized, did not collapse).
			Assert.Contains("charlie", frames[0]);
			Assert.Contains("side", frames[0]);
			GC.KeepAlive(window);
		}
	}
}
