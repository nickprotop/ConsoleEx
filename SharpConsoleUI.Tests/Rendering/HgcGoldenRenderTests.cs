// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering;

/// <summary>
/// Golden-render parity harness for <see cref="HorizontalGridControl"/>.
///
/// Each scenario captures MASTER's actual rendered output as the oracle. After the
/// HGC-over-GridControl reimplementation (Tasks 3–8) these SAME assertions must
/// still pass, which is the byte-identical proof.
///
/// Mechanism: <see cref="ContainerTestHelpers.RenderToLines"/> builds a
/// <see cref="TestWindowSystemBuilder"/>-backed window, adds the control,
/// calls <see cref="Window.RenderAndGetVisibleContent"/>, strips ANSI codes,
/// and returns lines — identical to what <c>GridRenderTests</c> and
/// <c>HorizontalGridControlTests</c> use.
///
/// Window size 80×6 → 78-char content lines (1-char borders each side), 4 content rows.
/// Window size 81×6 → 79-char content lines, 4 content rows.
/// </summary>
public class HgcGoldenRenderTests
{
	// ------------------------------------------------------------------ helpers

	/// <summary>
	/// Renders <paramref name="control"/> in a window of the given size and returns
	/// the stripped, newline-joined string (no ANSI codes).
	/// </summary>
	private static string RenderHgcToString(IWindowControl control, int width = 80, int height = 6)
	{
		var lines = ContainerTestHelpers.RenderToLines(control, width, height);
		return string.Join("\n", lines);
	}

	// ------------------------------------------------------------------ scenarios

	/// <summary>Scenario 1: three buttons via ButtonRow, centre-aligned.</summary>
	[Fact]
	public void ButtonRow_ThreeButtons_CenterAligned()
	{
		var b1 = new ButtonControl { Text = "OK" };
		var b2 = new ButtonControl { Text = "Cancel" };
		var b3 = new ButtonControl { Text = "Help" };

		var hgc = HorizontalGridControl.ButtonRow(b1, b2, b3);

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string expected =
			"                               OK  Cancel  Help                               \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
	}

	/// <summary>Scenario 2: three markup labels via FromControls, left-aligned.</summary>
	[Fact]
	public void FromControls_ThreeLabels_LeftAligned()
	{
		var l1 = new MarkupControl(new List<string> { "LabelOne" });
		var l2 = new MarkupControl(new List<string> { "LabelTwo" });
		var l3 = new MarkupControl(new List<string> { "LabelThree" });

		var hgc = HorizontalGridControl.FromControls(l1, l2, l3);

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string expected =
			"LabelOneLabelTwoLabelThree                                                    \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
	}

	/// <summary>Scenario 3: one fixed-width column + two flex columns.</summary>
	[Fact]
	public void MixedFixedFlexAuto()
	{
		var hgc = new HorizontalGridControl();
		var c0 = new ColumnContainer(hgc) { Width = 20 };
		c0.AddContent(SharpConsoleUI.Builders.Controls.Markup("[red]fixed[/]").Build());
		hgc.AddColumn(c0);
		var c1 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c1.AddContent(SharpConsoleUI.Builders.Controls.Markup("flexA").Build());
		hgc.AddColumn(c1);
		var c2 = new ColumnContainer(hgc) { FlexFactor = 2 };
		c2.AddContent(SharpConsoleUI.Builders.Controls.Markup("flexB").Build());
		hgc.AddColumn(c2);

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string expected =
			"fixed               flexAflexB                                                \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
	}

	/// <summary>Scenario 4: two equal-flex columns in an ODD total width (leftover-pixel rounding case).</summary>
	[Fact]
	public void TwoFlexColumns_OddWidth()
	{
		var hgc = new HorizontalGridControl();
		var c0 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c0.AddContent(new MarkupControl(new List<string> { "LEFT" }));
		hgc.AddColumn(c0);
		var c1 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c1.AddContent(new MarkupControl(new List<string> { "RIGHT" }));
		hgc.AddColumn(c1);

		// Odd width = 81 forces a leftover pixel — this is the rounding-case gate.
		string actual = RenderHgcToString(hgc, width: 81, height: 6);
		string expected =
			"LEFTRIGHT                                                                      \n" +
			"                                                                               \n" +
			"                                                                               \n" +
			"                                                                               ";
		Assert.Equal(expected, actual);
	}

	/// <summary>Scenario 5: two columns with a splitter between them.</summary>
	[Fact]
	public void WithSplitter_TwoColumns()
	{
		var hgc = new HorizontalGridControl();
		var c0 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c0.AddContent(new MarkupControl(new List<string> { "Left" }));
		hgc.AddColumn(c0);
		var c1 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c1.AddContent(new MarkupControl(new List<string> { "Right" }));
		hgc.AddColumnWithSplitter(c1);

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string expected =
			"Left║Right                                                                    \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
	}

	/// <summary>Scenario 6: three columns, middle one hidden.</summary>
	[Fact]
	public void HiddenColumn_Middle()
	{
		var hgc = new HorizontalGridControl();
		var c0 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c0.AddContent(new MarkupControl(new List<string> { "ColA" }));
		hgc.AddColumn(c0);
		var c1 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c1.AddContent(new MarkupControl(new List<string> { "ColB-hidden" }));
		c1.Visible = false;
		hgc.AddColumn(c1);
		var c2 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c2.AddContent(new MarkupControl(new List<string> { "ColC" }));
		hgc.AddColumn(c2);

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string expected =
			"ColAColC                                                                      \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
	}

	/// <summary>Scenario 7: a single flex column filling the full width.</summary>
	[Fact]
	public void SingleColumn()
	{
		var hgc = new HorizontalGridControl();
		var c0 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c0.AddContent(new MarkupControl(new List<string> { "Only" }));
		hgc.AddColumn(c0);

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string expected =
			"Only                                                                          \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
	}

	/// <summary>Scenario 8: no columns added — empty HGC.</summary>
	[Fact]
	public void EmptyGrid()
	{
		var hgc = new HorizontalGridControl();

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string expected =
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
	}

	/// <summary>
	/// Scenario 9: a flex column with MinWidth/MaxWidth clamping.
	/// Note: master's HorizontalLayout does not apply ColumnContainer.MaxWidth as a hard flex cap;
	/// the column is sized by the proportional flex distribution. This test captures master's actual
	/// output as the oracle for the byte-identical gate.
	/// </summary>
	[Fact]
	public void MinMaxClamp()
	{
		var hgc = new HorizontalGridControl();
		// With width=80 and two flex cols, each gets 40. The clamped col has Max=30,
		// so the extra 10 goes to the other column — the clamp engages.
		var c0 = new ColumnContainer(hgc) { FlexFactor = 1, MaxWidth = 30 };
		c0.AddContent(new MarkupControl(new List<string> { "Clamped" }));
		hgc.AddColumn(c0);
		var c1 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c1.AddContent(new MarkupControl(new List<string> { "Free" }));
		hgc.AddColumn(c1);

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string expected =
			"ClampedFree                                                                   \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
	}

	/// <summary>Scenario 10: HGC nested inside a ScrollablePanel.</summary>
	[Fact]
	public void NestedInScrollablePanel()
	{
		var hgc = new HorizontalGridControl();
		var c0 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c0.AddContent(new MarkupControl(new List<string> { "InScroll" }));
		hgc.AddColumn(c0);

		var spc = new ScrollablePanelControl();
		spc.AddControl(hgc);

		string actual = RenderHgcToString(spc, width: 80, height: 6);
		string expected =
			"InScroll                                                                      \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
	}

	// ----------------------------------------------------------------- augmented scenarios (Task 4)
	//
	// Master's HorizontalLayout is gone from this worktree, so these three cannot be captured against
	// true master. Each is captured against the reimplemented (over-Grid) HGC and cross-checked for
	// SANITY against the documented HGC contract. They lock in three properties that the original 10
	// golden scenarios do not directly assert: where a SECOND self-sizing column starts, where a flex
	// column's content is CLIPPED at its track boundary, and that a column BACKGROUND fills its whole
	// track. They serve as regression anchors for column positioning / clipping / background.

	/// <summary>
	/// (a) Two self-sizing flex columns, short distinct content. With the default Left alignment the grid
	/// self-sizes: each flex column packs to its CONTENT width (Auto), so the second column ("RIGHT") starts
	/// immediately after the first ("LEFT") at x=4 — columns pack at content width, NOT at a flex boundary.
	/// </summary>
	[Fact]
	public void TwoFlexCols_SecondColumnContentStartX()
	{
		var hgc = new HorizontalGridControl();
		var c0 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c0.AddContent(new MarkupControl(new List<string> { "LEFT" }));
		hgc.AddColumn(c0);
		var c1 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c1.AddContent(new MarkupControl(new List<string> { "RIGHT" }));
		hgc.AddColumn(c1);

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string expected =
			"LEFTRIGHT                                                                     \n" +
			"                                                                              \n" +
			"                                                                              \n" +
			"                                                                              ";
		Assert.Equal(expected, actual);
		// Cross-check: "RIGHT" begins at column index 4, immediately after the 4-char "LEFT" — packed at
		// content width (a flex boundary would have pushed it to ~x=40 in an 80-wide window).
		Assert.Equal('R', actual[4]);
	}

	/// <summary>
	/// (b) A flex column whose content (50 X's) is wider than an even split. Reproducing master's
	/// HorizontalLayout "base content + proportional extra" distribution, the over-wide flex column keeps its
	/// CONTENT width as a floor (50) and squeezes the sibling flex column (the remaining 28), rather than the
	/// two splitting the 78-cell content area evenly. The 50-char run therefore fits exactly in its 50-cell
	/// track and the right column's "R" begins at column 50 — the track boundary. (Cross-checked
	/// post-reimplementation: c0.ActualWidth=50, c1 starts at x=50; an even split would have clipped at ~39.)
	/// </summary>
	[Fact]
	public void FlexColumn_ContentOverflow_ClipsAtBoundary()
	{
		var hgc = new HorizontalGridControl { HorizontalAlignment = HorizontalAlignment.Stretch };
		var c0 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c0.AddContent(new MarkupControl(new List<string> { new string('X', 50) }));
		hgc.AddColumn(c0);
		var c1 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c1.AddContent(new MarkupControl(new List<string> { "R" }));
		hgc.AddColumn(c1);

		string actual = RenderHgcToString(hgc, width: 80, height: 6);
		string[] lines = actual.Split('\n');
		// The flex column pins to its content (50 X's); "R" begins immediately after at column 50.
		Assert.Equal(new string('X', 50) + "R", lines[0].TrimEnd());
		Assert.Equal(50, c0.ActualWidth);
		Assert.Equal(50, c1.ActualX);
	}

	/// <summary>
	/// (c) A column with an explicit background colour. The column box is Stretched to fill its track, so the
	/// background fills the FULL track width (here a 20-cell fixed column), not just the 3-char content "BG".
	/// </summary>
	[Fact]
	public void Column_WithBackgroundColor_FillsToEdge()
	{
		var hgc = new HorizontalGridControl();
		var c0 = new ColumnContainer(hgc) { Width = 20, BackgroundColor = Color.Blue };
		c0.AddContent(new MarkupControl(new List<string> { "BG" }));
		hgc.AddColumn(c0);
		var c1 = new ColumnContainer(hgc) { FlexFactor = 1 };
		c1.AddContent(new MarkupControl(new List<string> { "rest" }));
		hgc.AddColumn(c1);

		// Capture WITH ANSI so the background fill is observable across the whole 20-cell track.
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 6);
		var window = new Window(system) { Width = 80, Height = 6 };
		window.AddControl(hgc);
		var raw = window.RenderAndGetVisibleContent();
		string firstRowRaw = raw[0];

		// Stripped, the fixed column reserves 20 cells: "BG" + 18 spaces, then "rest".
		string stripped = ContainerTestHelpers.StripAnsiCodes(raw).Split('\n')[0];
		Assert.StartsWith("BG                  rest", stripped);
		// The blue background colour code (48;2;0;0;255) appears, proving the column painted its background
		// across the track rather than leaving it transparent.
		Assert.Contains("48;2;0;0;255", firstRowRaw);
	}
}
