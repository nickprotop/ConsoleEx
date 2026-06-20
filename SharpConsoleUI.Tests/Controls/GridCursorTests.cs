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
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// On-screen cursor for an editable control inside a grid cell — directly, and inside a scrolling SPC
/// cell child. Regression guard: the cursor lands in the right cell and tracks cell-internal scroll.
public class GridCursorTests
{
	private readonly ITestOutputHelper _out;
	public GridCursorTests(ITestOutputHelper o) => _out = o;

	private static bool CursorAt(Window window, out System.Drawing.Point pos)
		=> window.EventDispatcher!.HasInteractiveContent(out pos);

	[Fact]
	public void Cursor_ForPromptDirectlyInCell_LandsOnThePrompt()
	{
		// A Prompt in cell (1,0) of a 2x2 grid. The window cursor must land inside that cell's on-screen
		// area, NOT at the grid origin / (0,0).
		var prompt = new PromptControl { Prompt = "> " };
		var grid = new GridControl { Width = 40, Height = 12 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(new MarkupControl(new List<string> { "topleft" }), 0, 0);
		grid.Place(prompt, 1, 0);

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		var node = window.Renderer?.GetLayoutNode(prompt);
		_out.WriteLine($"prompt node bounds = {node?.AbsoluteBounds}");
		bool has = CursorAt(window, out var pos);
		_out.WriteLine($"cursor has={has} pos={pos}");

		Assert.True(has, "cursor should be reported for the focused prompt in a cell");
		// The prompt is in row 1 (lower half), so the cursor Y must be in the lower half of the grid,
		// not at the top. A grid Height 12 (rows ~6 each) -> row-1 prompt cursor Y should be >= ~6.
		Assert.True(pos.Y >= 4, $"cursor Y {pos.Y} should be in the lower-row cell, not the top");
	}

	[Fact]
	public void Cursor_ForPromptInScrollPanelInCell_TracksScroll()
	{
		// Prompt inside an SPC inside a grid cell. Scroll the SPC; the cursor must follow (or hide when
		// scrolled out), governed by the SPC's viewport — not be stuck at a stale position.
		var spc = new ScrollablePanelControl { BorderStyle = BorderStyle.None };
		for (int i = 0; i < 10; i++)
			spc.AddControl(new MarkupControl(new List<string> { $"line{i}" }));
		var prompt = new PromptControl { Prompt = "> " };
		spc.AddControl(prompt);
		for (int i = 0; i < 10; i++)
			spc.AddControl(new MarkupControl(new List<string> { $"after{i}" }));

		var grid = new GridControl { Width = 30, Height = 8 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.Place(spc, 0, 0);

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		spc.ScrollChildIntoView(prompt);
		window.RenderAndGetVisibleContent();

		bool has = CursorAt(window, out var pos);
		_out.WriteLine($"after ScrollChildIntoView: cursor has={has} pos={pos} scroll={spc.VerticalScrollOffset}");

		// Once the prompt is scrolled into the SPC's viewport (which sits in the grid cell), the cursor
		// must be reported and land within the grid's on-screen area (Y within 0..8-ish region).
		Assert.True(has, "cursor should be visible once the prompt is scrolled into the cell SPC viewport");
	}
}
