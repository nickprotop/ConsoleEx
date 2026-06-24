// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class GridSplitterTests
{
	private static (ConsoleWindowSystem system, Window window, GridControl grid) BuildGrid(
		GridLength[] cols, GridLength[] rows, int width = 40, int height = 12, int colGap = 0, int rowGap = 0)
	{
		var grid = new GridControl { Width = width - 2, Height = height - 2 };
		foreach (var c in cols) grid.ColumnDefinitions.Add(c);
		foreach (var r in rows) grid.RowDefinitions.Add(r);
		grid.ColumnGap = colGap;
		grid.RowGap = rowGap;
		for (int r = 0; r < rows.Length; r++)
			for (int c = 0; c < cols.Length; c++)
				grid.Place(new MarkupControl(new List<string> { $"r{r}c{c}" }), r, c);

		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		window.AddControl(grid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		return (system, window, grid);
	}

	[Fact]
	public void AddColumnSplitterAfter_DeclaresBoundary()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		grid.AddColumnSplitterAfter(0);
		Assert.True(grid.HasColumnSplitterAfter(0));
	}

	[Fact]
	public void OutOfRangeSplitter_IsInert_NoThrow()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		grid.AddColumnSplitterAfter(99);
		system.Render.UpdateDisplay();
		Assert.True(true);
	}

	[Fact]
	public void ColumnSplitter_WithZeroGap_AutoInsertsAGapAndRenders()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) },
			width: 21, height: 5, colGap: 0);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();
		var text = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("║", text);
	}

	[Fact]
	public void SplitterColorProperties_DefaultNull_RoundTrip()
	{
		var grid = new GridControl();
		Assert.Null(grid.SplitterColor);
		Assert.Null(grid.SplitterFocusedBackground);
		Assert.Null(grid.SplitterDraggingBackground);
		grid.SplitterColor = Color.Grey50;
		grid.SplitterDraggingBackground = Color.Orange1;
		Assert.Equal(Color.Grey50, grid.SplitterColor);
		Assert.Equal(Color.Orange1, grid.SplitterDraggingBackground);
	}

	[Fact]
	public void RealDispatch_DragColumnSplitter_ResizesAdjacentColumns_AndSurvivesRerender()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) },
			width: 44, height: 8, colGap: 1);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();

		double w0Before = grid.ColumnDefinitions[0].Weight;
		double w1Before = grid.ColumnDefinitions[1].Weight;

		int sx = grid.GetColumnSplitterScreenX(0, window);
		int sy = window.Top + 1 + grid.ActualY + 2;

		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(sx, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed, MouseFlags.Button1Dragged }, new Point(sx + 5, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, new Point(sx + 5, sy));
		system.Input.ProcessInput();

		system.Render.UpdateDisplay();

		Assert.True(grid.ColumnDefinitions[0].Weight > w0Before, "col0 should have grown");
		Assert.True(grid.ColumnDefinitions[1].Weight < w1Before, "col1 should have shrunk");
		Assert.Equal(w0Before + w1Before, grid.ColumnDefinitions[0].Weight + grid.ColumnDefinitions[1].Weight, 3);
	}

	[Fact]
	public void RealDispatch_DraggingSplitter_DoesNotFocusOrClickUnderlyingCell()
	{
		var grid = new GridControl();
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.ColumnGap = 1;
		var btnLeft = new ButtonControl { Text = "L" };
		var btnRight = new ButtonControl { Text = "R" };
		int rightClicks = 0;
		btnRight.Click += (_, _) => rightClicks++;
		grid.Place(btnLeft, 0, 0);
		grid.Place(btnRight, 0, 1);
		grid.AddColumnSplitterAfter(0);

		var system = TestWindowSystemBuilder.CreateTestSystem(44, 8);
		var window = new Window(system) { Left = 0, Top = 0, Width = 44, Height = 8 };
		window.AddControl(grid);
		system.AddWindow(window);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.Render.UpdateDisplay();

		int sx = grid.GetColumnSplitterScreenX(0, window);
		int sy = window.Top + 1 + grid.ActualY + 2;
		var driver = (MockConsoleDriver)system.ConsoleDriver;

		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(sx, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed, MouseFlags.Button1Dragged }, new Point(sx + 10, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, new Point(sx + 10, sy));
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();

		Assert.False(window.FocusManager.IsFocused(btnRight), "dragging across a cell must NOT focus it");
		Assert.False(window.FocusManager.IsFocused(btnLeft), "dragging must NOT focus the left cell either");
		Assert.Equal(0, rightClicks);
	}

	[Fact]
	public void Keyboard_NudgeColumnSplitter_ResizesByOneCell()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(20), GridLength.Cells(20) }, new[] { GridLength.Star(1) },
			width: 44, height: 8, colGap: 1);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();

		window.FocusManager.SetFocus(grid, FocusReason.Programmatic);
		grid.FocusColumnSplitter(0);
		system.Render.UpdateDisplay();

		int v0 = grid.ColumnDefinitions[0].Value;

		grid.ProcessKey(new System.ConsoleKeyInfo('\0', System.ConsoleKey.RightArrow, false, false, false));
		system.Render.UpdateDisplay();

		Assert.Equal(v0 + 1, grid.ColumnDefinitions[0].Value);
		Assert.Equal(20 - 1, grid.ColumnDefinitions[1].Value);
	}

	// "Real thing" test: drive the arrow key through the REAL window key path
	// (InputStateService.EnqueueKey + system.Input.ProcessInput), NOT grid.ProcessKey directly.
	// This exercises WindowEventDispatcher.HasActiveInteractiveContent → splitter.ProcessKey, proving
	// the IInteractiveControl routing reaches a focused GridSplitter. With the splitter only an
	// IFocusableControl (the bug), HasActiveInteractiveContent returned false and the column never resized.
	[Fact]
	public void RealWindowKeyPath_NudgeFocusedColumnSplitter_ResizesAndSurvivesRerender()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(20), GridLength.Cells(20) }, new[] { GridLength.Star(1) },
			width: 44, height: 8, colGap: 1);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();

		// Focus the splitter the same way a Tab/click would leave it: it is THE FocusedControl.
		grid.FocusColumnSplitter(0);
		system.Render.UpdateDisplay();
		Assert.Same(grid.GetColumnSplitterForTest(0), window.FocusManager.FocusedControl);

		int v0 = grid.ColumnDefinitions[0].Value;

		// Drive RightArrow through the real input queue → dispatcher → focused splitter's ProcessKey.
		system.InputStateService.EnqueueKey(new System.ConsoleKeyInfo('\0', System.ConsoleKey.RightArrow, false, false, false));
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();

		Assert.Equal(v0 + 1, grid.ColumnDefinitions[0].Value);
		Assert.Equal(20 - 1, grid.ColumnDefinitions[1].Value);

		// State must survive a re-render (CLAUDE.md "real thing" rule).
		system.Render.UpdateDisplay();
		Assert.Equal(v0 + 1, grid.ColumnDefinitions[0].Value);
		Assert.Equal(20 - 1, grid.ColumnDefinitions[1].Value);
	}

	// Real-window-path row splitter resize, mirroring the demo's RowSplitterAfter.
	[Fact]
	public void RealWindowKeyPath_NudgeFocusedRowSplitter_Resizes()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1) }, new[] { GridLength.Cells(5), GridLength.Cells(5) },
			width: 24, height: 16, rowGap: 1);
		grid.AddRowSplitterAfter(0);
		system.Render.UpdateDisplay();

		grid.FocusRowSplitter(0);
		system.Render.UpdateDisplay();
		Assert.Same(grid.GetRowSplitterForTest(0), window.FocusManager.FocusedControl);

		int r0 = grid.RowDefinitions[0].Value;

		system.InputStateService.EnqueueKey(new System.ConsoleKeyInfo('\0', System.ConsoleKey.DownArrow, false, false, false));
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();

		Assert.Equal(r0 + 1, grid.RowDefinitions[0].Value);
		Assert.Equal(5 - 1, grid.RowDefinitions[1].Value);
	}

	// Demo-shaped Tab test: 4-col / 4-row grid, header colSpan=4 at (0,0), focusable controls in body
	// cells, ColumnSplitterAfter(1). After making GridSplitter IInteractiveControl, the column splitter
	// must be a Tab stop (in GetFocusableChildren) AND real-path Tab traversal must be able to land on it.
	[Fact]
	public void DemoShaped_ColumnSplitterAfter1_IsTabStop_AndTabLandsOnIt()
	{
		var grid = new GridControl { Width = 60, Height = 12 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Auto());
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowGap = 1;
		grid.ColumnGap = 2;

		// Header spans all 4 columns at row 0 (so it does NOT cover the column-after-1 boundary in body rows).
		grid.Place(new MarkupControl(new List<string> { "header" }), 0, 0, colSpan: 4);
		// Focusable controls in body cells so Tab has stops to traverse.
		var b00 = new ButtonControl { Text = "b00" };
		var b01 = new ButtonControl { Text = "b01" };
		var b02 = new ButtonControl { Text = "b02" };
		grid.Place(b00, 1, 0);
		grid.Place(b01, 1, 1);
		grid.Place(b02, 1, 2);
		grid.AddColumnSplitterAfter(1);

		var system = TestWindowSystemBuilder.CreateTestSystem(64, 16);
		var window = new Window(system) { Left = 0, Top = 0, Width = 64, Height = 16 };
		window.AddControl(grid);
		system.AddWindow(window);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var splitter = grid.GetColumnSplitterForTest(1);

		// 1) The column splitter must be in the focusable Tab-stop list.
		var focusables = grid.GetFocusableChildrenForTest();
		Assert.Contains(splitter, focusables);

		// 2) Real-path Tab traversal must be able to land focus on the column splitter.
		bool reachedSplitter = false;
		for (int i = 0; i < focusables.Count + 2 && !reachedSplitter; i++)
		{
			system.InputStateService.EnqueueKey(new System.ConsoleKeyInfo('\t', System.ConsoleKey.Tab, false, false, false));
			system.Input.ProcessInput();
			system.Render.UpdateDisplay();
			if (ReferenceEquals(window.FocusManager.FocusedControl, splitter))
				reachedSplitter = true;
		}
		Assert.True(reachedSplitter, "Tab traversal should land focus on the column splitter");
	}

	// Locks the splitter highlight contract on EVERY built-in theme, for a ROLED grid (the real demo case)
	// and a roleless grid:
	//   * FOREGROUND-ONLY highlight — the focused glyph foreground differs from idle (the highlight is visible),
	//   * the BACKGROUND is the SAME idle vs focused (no bg swap / inversion),
	//   * the focused glyph is readable (fg != bg).
	// With a role, idle is the role border SHADED dimmer and focused is the full-bright role border (dim→bright,
	// same hue). The original bug drew the focused glyph in ButtonFocusedForegroundColor (dark text-on-accent) on
	// a transparent background → invisible on every theme but ModernGray; a later pass wrongly swapped the
	// background. This test prevents both regressions across the whole theme registry.
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Splitter_ForegroundOnlyHighlight_SameBackground_OnEveryBuiltInTheme(bool roled)
	{
		foreach (var info in new[] { "ModernGray", "Ocean", "Amber", "Forest", "Crimson", "Slate", "Daylight" })
		{
			var (system, window, grid) = BuildGrid(
				new[] { GridLength.Cells(20), GridLength.Cells(20) }, new[] { GridLength.Star(1) },
				width: 44, height: 8, colGap: 1);
			var theme = system.ThemeRegistryService.GetTheme(info);
			Assert.NotNull(theme);
			system.ThemeStateService.SetTheme(theme!);
			if (roled) grid.ColorRole = ColorRole.Primary;
			grid.AddColumnSplitterAfter(0);
			system.Render.UpdateDisplay();
			var splitter = grid.GetColumnSplitterForTest(0);

			// Idle colours (no splitter focused).
			window.FocusManager.SetFocus(null, FocusReason.Programmatic);
			system.Render.UpdateDisplay();
			var (idleFg, idleBg) = grid.GetSplitterColorsForTest(splitter);

			// Focused colours.
			grid.FocusColumnSplitter(0);
			system.Render.UpdateDisplay();
			var (focFg, focBg) = grid.GetSplitterColorsForTest(splitter);

			string ctx = $"theme '{info}', roled={roled}";
			Assert.True(focBg == idleBg, $"{ctx}: background must NOT change on focus (idle {idleBg} vs focused {focBg})");
			Assert.True(focFg != idleFg, $"{ctx}: focused foreground must differ from idle (both {focFg}) — highlight invisible");
			Assert.True(focFg != focBg, $"{ctx}: focused glyph fg ({focFg}) must differ from bg ({focBg})");
		}
	}

	[Fact]
	public void Builder_DeclaresSplitters()
	{
		var grid = SharpConsoleUI.Builders.Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Star(1), GridLength.Star(1))
			.ColumnSplitterAfter(0)
			.RowSplitterAfter(0)
			.Build();
		Assert.True(grid.HasColumnSplitterAfter(0));
		Assert.True(grid.HasRowSplitterAfter(0));
	}

	public enum TK { Star, Auto, Fixed }
	private static GridLength Mk(TK t) => t switch
	{
		TK.Star => GridLength.Star(1),
		TK.Auto => GridLength.Auto(),
		_ => GridLength.Cells(10),
	};
	// WinUI matrix resulting TYPE: Star stays Star; Auto bakes to Fixed; Fixed stays Fixed.
	private static (GridUnitType a, GridUnitType b) Expected(TK a, TK b)
	{
		GridUnitType T(TK t) => t == TK.Star ? GridUnitType.Star : GridUnitType.Fixed;
		return (T(a), T(b));
	}

	[Theory]
	[InlineData(TK.Star, TK.Star)]
	[InlineData(TK.Star, TK.Auto)]
	[InlineData(TK.Star, TK.Fixed)]
	[InlineData(TK.Auto, TK.Star)]
	[InlineData(TK.Auto, TK.Auto)]
	[InlineData(TK.Auto, TK.Fixed)]
	[InlineData(TK.Fixed, TK.Star)]
	[InlineData(TK.Fixed, TK.Auto)]
	[InlineData(TK.Fixed, TK.Fixed)]
	public void RealMouseDrag_AllColumnCombos_PreserveTypePerMatrix(TK a, TK b)
	{
		var (system, window, grid) = BuildGrid(
			new[] { Mk(a), Mk(b) }, new[] { GridLength.Star(1) }, width: 44, height: 8, colGap: 1);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();

		int sx = grid.GetColumnSplitterScreenX(0, window);
		int sy = window.Top + 1 + grid.ActualY + 2;
		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(sx, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed, MouseFlags.Button1Dragged }, new Point(sx + 3, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, new Point(sx + 3, sy));
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();

		var (ea, eb) = Expected(a, b);
		Assert.Equal(ea, grid.ColumnDefinitions[0].Type);
		Assert.Equal(eb, grid.ColumnDefinitions[1].Type);
		system.Render.UpdateDisplay();
		Assert.Equal(ea, grid.ColumnDefinitions[0].Type);
		Assert.Equal(eb, grid.ColumnDefinitions[1].Type);
	}

	[Theory]
	[InlineData(TK.Star, TK.Star)]
	[InlineData(TK.Star, TK.Auto)]
	[InlineData(TK.Star, TK.Fixed)]
	[InlineData(TK.Auto, TK.Star)]
	[InlineData(TK.Auto, TK.Auto)]
	[InlineData(TK.Auto, TK.Fixed)]
	[InlineData(TK.Fixed, TK.Star)]
	[InlineData(TK.Fixed, TK.Auto)]
	[InlineData(TK.Fixed, TK.Fixed)]
	public void RealKeyboard_AllRowCombos_PreserveTypePerMatrix(TK a, TK b)
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1) }, new[] { Mk(a), Mk(b) }, width: 24, height: 14, rowGap: 1);
		grid.AddRowSplitterAfter(0);
		window.FocusManager.SetFocus(grid, FocusReason.Programmatic);
		grid.FocusRowSplitter(0);
		system.Render.UpdateDisplay();

		grid.ProcessKey(new System.ConsoleKeyInfo('\0', System.ConsoleKey.DownArrow, false, false, false));
		system.Render.UpdateDisplay();

		var (ea, eb) = Expected(a, b);
		Assert.Equal(ea, grid.RowDefinitions[0].Type);
		Assert.Equal(eb, grid.RowDefinitions[1].Type);
	}

	[Fact]
	public void RealMouse_StarSumConserved_ThirdStarUnaffected()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) },
			width: 49, height: 6, colGap: 1);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();
		double sumBefore = grid.ColumnDefinitions[0].Weight + grid.ColumnDefinitions[1].Weight;
		double w2Before = grid.ColumnDefinitions[2].Weight;

		int sx = grid.GetColumnSplitterScreenX(0, window);
		int sy = window.Top + 1 + grid.ActualY + 2;
		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(sx, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed, MouseFlags.Button1Dragged }, new Point(sx + 3, sy));
		system.Input.ProcessInput();
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, new Point(sx + 3, sy));
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();

		Assert.Equal(sumBefore, grid.ColumnDefinitions[0].Weight + grid.ColumnDefinitions[1].Weight, 3);
		Assert.Equal(w2Before, grid.ColumnDefinitions[2].Weight, 3);
	}

	[Fact]
	public void ResizeStarColumnViaSplitter_ThenResizeWindow_StarStillReflows()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) },
			width: 44, height: 8, colGap: 1);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();

		var s = grid.GetColumnSplitterForTest(0);
		grid.ResizeSplitter(s, 4);
		system.Render.UpdateDisplay();
		Assert.Equal(GridUnitType.Star, grid.ColumnDefinitions[0].Type);
		double ratioAfterDrag = grid.ColumnDefinitions[0].Weight / grid.ColumnDefinitions[1].Weight;

		// Unpin the grid's explicit width so it fills the window — this is the WinUI responsive
		// scenario: a Star track resized via splitter must still reflow when the host resizes.
		// (BuildGrid pins Width = window-2; without unpinning the grid never sees the new width.)
		grid.Width = null;
		system.Render.UpdateDisplay();
		int w0Narrow = grid.GetColumnArrangedSizeForTest(0);
		int w1Narrow = grid.GetColumnArrangedSizeForTest(1);

		window.Width = 80;
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int w0Wide = grid.GetColumnArrangedSizeForTest(0);
		int w1Wide = grid.GetColumnArrangedSizeForTest(1);

		Assert.True(w0Wide > w0Narrow, "Star col0 must grow when the window grows (reflow)");
		Assert.True(w1Wide > w1Narrow, "Star col1 must grow too");
		Assert.Equal(ratioAfterDrag, (double)w0Wide / w1Wide, 1);
	}

	[Fact]
	public void ResizeFixedColumnViaSplitter_ThenResizeWindow_FixedHolds()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(12), GridLength.Star(1) }, new[] { GridLength.Star(1) },
			width: 44, height: 8, colGap: 1);
		grid.AddColumnSplitterAfter(0);
		system.Render.UpdateDisplay();

		var s = grid.GetColumnSplitterForTest(0);
		grid.ResizeSplitter(s, 3);
		system.Render.UpdateDisplay();
		Assert.Equal(GridUnitType.Fixed, grid.ColumnDefinitions[0].Type);
		int fixedVal = grid.ColumnDefinitions[0].Value;

		window.Width = 80;
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		Assert.Equal(fixedVal, grid.ColumnDefinitions[0].Value);
		Assert.Equal(fixedVal, grid.GetColumnArrangedSizeForTest(0));
	}

	// ---- FIX #3: a row splitter must not draw '─' across a cell that spans the row boundary ----

	[Fact]
	public void RowSplitter_DoesNotDrawAcrossRowSpanningCell()
	{
		// 2 cols x 2 rows, fixed widths so the spanning column's x-range is deterministic. Col 0 holds a
		// cell that spans BOTH rows (across the row-0/row-1 boundary); col 1 has separate per-row cells.
		var grid = new GridControl { Width = 24, Height = 8 };
		grid.ColumnDefinitions.Add(GridLength.Cells(8));
		grid.ColumnDefinitions.Add(GridLength.Cells(8));
		grid.RowDefinitions.Add(GridLength.Cells(2));
		grid.RowDefinitions.Add(GridLength.Cells(2));
		grid.ColumnGap = 0;
		grid.RowGap = 1;
		grid.Place(new MarkupControl(new List<string> { "LEFT" }), 0, 0, rowSpan: 2); // spans the boundary
		grid.Place(new MarkupControl(new List<string> { "TR" }), 0, 1);
		grid.Place(new MarkupControl(new List<string> { "BR" }), 1, 1);
		grid.AddRowSplitterAfter(0);

		var system = TestWindowSystemBuilder.CreateTestSystem(30, 12);
		var window = new Window(system) { Left = 0, Top = 0, Width = 30, Height = 12 };
		window.AddControl(grid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var lines = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n');

		// The splitter row is the only line that contains the row-splitter glyph at all.
		int boundaryLine = System.Array.FindIndex(lines, l => l.Contains('═'));
		Assert.True(boundaryLine >= 0, "expected a row-splitter line to exist");

		string row = lines[boundaryLine];
		// RenderAndGetVisibleContent returns CONTENT lines (no window border), so x is grid-content-relative:
		// col 0 occupies x in [0, 8); col 1 starts at x = 8 (col-0 width, colGap 0).
		int spanLeftX = grid.ActualX + 0;
		int col1StartX = grid.ActualX + 8;

		// Inside the spanning cell's column range [spanLeftX, col1StartX), there must be NO '═'.
		for (int x = spanLeftX; x < col1StartX && x < row.Length; x++)
			Assert.NotEqual('═', row[x]);

		// In the non-spanning column (col 1), the splitter line MUST be drawn.
		bool drawnInCol1 = false;
		for (int x = col1StartX; x < row.Length; x++)
			if (row[x] == '═') { drawnInCol1 = true; break; }
		Assert.True(drawnInCol1, "row splitter must still draw in the non-spanning column");
	}

	// ---- FIX #2: a '┼' junction is drawn where a column splitter crosses a row splitter ----

	[Fact]
	public void CrossingColumnAndRowSplitters_DrawJunctionGlyph()
	{
		var grid = new GridControl { Width = 24, Height = 10 };
		grid.ColumnDefinitions.Add(GridLength.Cells(8));
		grid.ColumnDefinitions.Add(GridLength.Cells(8));
		grid.RowDefinitions.Add(GridLength.Cells(2));
		grid.RowDefinitions.Add(GridLength.Cells(2));
		grid.ColumnGap = 1;
		grid.RowGap = 1;
		grid.Place(new MarkupControl(new List<string> { "A" }), 0, 0);
		grid.Place(new MarkupControl(new List<string> { "B" }), 0, 1);
		grid.Place(new MarkupControl(new List<string> { "C" }), 1, 0);
		grid.Place(new MarkupControl(new List<string> { "D" }), 1, 1);
		grid.AddColumnSplitterAfter(0);
		grid.AddRowSplitterAfter(0);

		var system = TestWindowSystemBuilder.CreateTestSystem(30, 14);
		var window = new Window(system) { Left = 0, Top = 0, Width = 30, Height = 14 };
		window.AddControl(grid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var text = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("╬", text);
	}

	// ---- FIX #1: a splitter is a real Tab stop and loses focus when a cell takes focus ----

	[Fact]
	public void Splitter_IsTabStop_AndLosesFocusWhenCellFocused()
	{
		var grid = new GridControl { Width = 40, Height = 6 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.ColumnGap = 1;
		var btnLeft = new ButtonControl { Text = "L" };
		var btnRight = new ButtonControl { Text = "R" };
		grid.Place(btnLeft, 0, 0);
		grid.Place(btnRight, 0, 1);
		grid.AddColumnSplitterAfter(0);

		var system = TestWindowSystemBuilder.CreateTestSystem(44, 10);
		var window = new Window(system) { Left = 0, Top = 0, Width = 44, Height = 10 };
		window.AddControl(grid);
		system.AddWindow(window);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var splitter = grid.GetColumnSplitterForTest(0);

		// Tab into the grid: first stop is a cell button, then the splitter is reachable by further Tabs.
		// Drive Tab via the grid's IFocusScope so traversal across cells + splitter is exercised directly.
		window.FocusManager.SetFocus(btnLeft, FocusReason.Programmatic);
		// Step forward across the focusable Tab stops until we land on the splitter.
		IFocusableControl? cur = btnLeft;
		bool reachedSplitter = false;
		for (int i = 0; i < 5 && cur != null; i++)
		{
			cur = ((IFocusScope)grid).GetNextFocus(cur, backward: false);
			if (ReferenceEquals(cur, splitter)) { reachedSplitter = true; break; }
		}
		Assert.True(reachedSplitter, "Tab traversal must reach the splitter");

		// Focus the splitter and confirm it reports focus.
		window.FocusManager.SetFocus(splitter, FocusReason.Keyboard);
		system.Render.UpdateDisplay();
		Assert.True(splitter.HasFocus, "splitter should report HasFocus once focused");
		Assert.True(window.FocusManager.IsFocused(splitter));

		// Arrow nudges the FOCUSED splitter.
		double w0Before = grid.ColumnDefinitions[0].Weight;
		grid.ProcessKey(new System.ConsoleKeyInfo('\0', System.ConsoleKey.RightArrow, false, false, false));
		system.Render.UpdateDisplay();
		Assert.True(grid.ColumnDefinitions[0].Weight > w0Before, "arrow must nudge the focused splitter");

		// Focusing a cell clears the splitter's focus highlight.
		window.FocusManager.SetFocus(btnRight, FocusReason.Mouse);
		system.Render.UpdateDisplay();
		Assert.False(splitter.HasFocus, "splitter must lose focus when a cell is focused");
		Assert.True(window.FocusManager.IsFocused(btnRight));
	}

	// ---- FIX #1: column splitter is INTERLEAVED into the row-0 focus order (mirrors HGrid) ----

	private static (ConsoleWindowSystem system, Window window, GridControl grid, ButtonControl b0, ButtonControl b1, GridSplitter splitter)
		BuildTwoColumnGridWithSplitter()
	{
		var grid = new GridControl { Width = 40, Height = 6 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.ColumnGap = 1;
		var b0 = new ButtonControl { Text = "B0" };
		var b1 = new ButtonControl { Text = "B1" };
		grid.Place(b0, 0, 0);
		grid.Place(b1, 0, 1);
		grid.AddColumnSplitterAfter(0);

		var system = TestWindowSystemBuilder.CreateTestSystem(44, 10);
		var window = new Window(system) { Left = 0, Top = 0, Width = 44, Height = 10 };
		window.AddControl(grid);
		system.AddWindow(window);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, grid, b0, b1, grid.GetColumnSplitterForTest(0));
	}

	[Fact]
	public void GetNextFocus_FromFirstButton_ReturnsColumnSplitter()
	{
		var (_, _, grid, b0, _, splitter) = BuildTwoColumnGridWithSplitter();
		var scope = (IFocusScope)grid;
		Assert.Same(splitter, scope.GetNextFocus(b0, backward: false));
	}

	[Fact]
	public void GetNextFocus_FromColumnSplitter_ReturnsSecondColumnButton()
	{
		var (_, _, grid, _, b1, splitter) = BuildTwoColumnGridWithSplitter();
		var scope = (IFocusScope)grid;
		Assert.Same(b1, scope.GetNextFocus(splitter, backward: false));
	}

	[Fact]
	public void GetNextFocus_Backward_FromSecondButton_ReturnsColumnSplitter()
	{
		var (_, _, grid, _, b1, splitter) = BuildTwoColumnGridWithSplitter();
		var scope = (IFocusScope)grid;
		Assert.Same(splitter, scope.GetNextFocus(b1, backward: true));
	}

	[Fact]
	public void Tab_InterleavesColumnSplitterBetweenCells()
	{
		var (system, window, grid, b0, b1, splitter) = BuildTwoColumnGridWithSplitter();

		// Enter the grid and drive real Tab keys through ProcessKey: B0 → splitter → B1.
		window.FocusManager.SetFocus(b0, FocusReason.Programmatic);
		Tab(grid);
		Assert.True(window.FocusManager.IsFocused(splitter), "first Tab from B0 must land on the splitter");
		Tab(grid);
		Assert.True(window.FocusManager.IsFocused(b1), "second Tab must land on B1");
	}

	[Fact]
	public void RowSplitter_AppearsAfterItsRowCells_InFocusOrder()
	{
		// 1 col, 2 rows; a row splitter after row 0 must sit between row-0's cell and row-1's cell.
		var grid = new GridControl { Width = 30, Height = 10 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowGap = 1;
		var r0 = new ButtonControl { Text = "R0" };
		var r1 = new ButtonControl { Text = "R1" };
		grid.Place(r0, 0, 0);
		grid.Place(r1, 1, 0);
		grid.AddRowSplitterAfter(0);

		var system = TestWindowSystemBuilder.CreateTestSystem(34, 14);
		var window = new Window(system) { Left = 0, Top = 0, Width = 34, Height = 14 };
		window.AddControl(grid);
		system.AddWindow(window);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var splitter = grid.GetRowSplitterForTest(0);
		var scope = (IFocusScope)grid;
		Assert.Same(splitter, scope.GetNextFocus(r0, backward: false));
		Assert.Same(r1, scope.GetNextFocus(splitter, backward: false));
	}

	private static void Tab(GridControl grid)
		=> grid.ProcessKey(new System.ConsoleKeyInfo('\t', System.ConsoleKey.Tab, false, false, false));

	// ---- FIX B: a column splitter fully hidden behind a col-spanning cell is NOT a Tab stop ----

	[Fact]
	public void FullyBlockedColumnSplitter_IsNotAFocusStop_AndTabSkipsIt()
	{
		// 1 row, 3 columns. Cell (0,0) spans columns 0+1 (ColSpan=2), so the boundary AFTER column 0 sits
		// INSIDE that span — the splitter would draw no glyph on any row. It must not become an invisible
		// Tab stop / hit target. A second focusable cell sits in column 2.
		var grid = new GridControl { Width = 36, Height = 5 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.ColumnGap = 1;
		var spanning = new ButtonControl { Text = "SPAN" };
		var c2 = new ButtonControl { Text = "C2" };
		grid.Place(spanning, 0, 0, colSpan: 2); // spans the boundary after column 0
		grid.Place(c2, 0, 2);
		grid.AddColumnSplitterAfter(0);

		var system = TestWindowSystemBuilder.CreateTestSystem(40, 9);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 9 };
		window.AddControl(grid);
		system.AddWindow(window);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var splitter = grid.GetColumnSplitterForTest(0);

		// (a) The fully-blocked splitter has empty Bounds and is excluded from the focus order.
		Assert.True(splitter.Bounds.IsEmpty, "a fully-blocked splitter must have empty Bounds (no glyph drawn)");
		var scope = (IFocusScope)grid;
		// Tab forward from the spanning cell must go straight to C2, never the splitter.
		Assert.Same(c2, scope.GetNextFocus(spanning, backward: false));

		// (b) Real Tab from the only-then-focused spanning cell does NOT land on the splitter.
		window.FocusManager.SetFocus(spanning, FocusReason.Programmatic);
		Tab(grid);
		Assert.False(window.FocusManager.IsFocused(splitter), "Tab must not land on the invisible splitter");
		Assert.True(window.FocusManager.IsFocused(c2), "Tab must reach the next visible cell instead");
	}
}
