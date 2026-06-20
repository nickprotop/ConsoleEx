// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Focus, cursor and re-render conformance for <see cref="GridControl"/>. The grid is a transparent
/// focus scope (it does not take focus itself); controls in its cells must behave exactly like
/// children of any other container. These tests assert Tab/Shift+Tab traversal is row-major, that
/// SavedFocus restores on re-entry, that a nested scope cell is entered (not skipped), that the
/// composed logical cursor is offset by the focused cell's origin, and — per the ScrollLayout
/// post-mortem rule — that focus and cursor state SURVIVE an extra re-render.
/// </summary>
public class GridFocusTests
{
	private static Window NewWindow()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		return new Window(system) { Width = 100, Height = 30 };
	}

	/// <summary>
	/// Builds a 2x2 grid with a focusable button placed in each cell at (row, col):
	/// (0,0)=tl, (0,1)=tr, (1,0)=bl, (1,1)=br. The grid is sized and added to a rendered window so
	/// the cells become real DOM participants with arranged bounds.
	/// </summary>
	private static (Window window, GridControl grid, ButtonControl tl, ButtonControl tr, ButtonControl bl, ButtonControl br) BuildTwoByTwo()
	{
		var window = NewWindow();
		var grid = new GridControl { Width = 60, Height = 10 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));

		var tl = new ButtonControl { Text = "TL" };
		var tr = new ButtonControl { Text = "TR" };
		var bl = new ButtonControl { Text = "BL" };
		var br = new ButtonControl { Text = "BR" };
		grid.Place(tl, 0, 0);
		grid.Place(tr, 0, 1);
		grid.Place(bl, 1, 0);
		grid.Place(br, 1, 1);

		window.AddControl(grid);
		window.RenderAndGetVisibleContent(); // arrange cells into the DOM tree

		return (window, grid, tl, tr, bl, br);
	}

	[Fact]
	public void Tab_MovesRowMajor()
	{
		var (_, grid, tl, tr, bl, br) = BuildTwoByTwo();
		var scope = (IFocusScope)grid;

		// Row-major: TL -> TR -> BL -> BR.
		Assert.Equal(tr, scope.GetNextFocus(tl, backward: false));
		Assert.Equal(bl, scope.GetNextFocus(tr, backward: false));
		Assert.Equal(br, scope.GetNextFocus(bl, backward: false));
		// Past the last cell: traversal exits the scope.
		Assert.Null(scope.GetNextFocus(br, backward: false));
	}

	[Fact]
	public void ShiftTab_Reverses()
	{
		var (_, grid, tl, tr, bl, br) = BuildTwoByTwo();
		var scope = (IFocusScope)grid;

		Assert.Equal(bl, scope.GetNextFocus(br, backward: true));
		Assert.Equal(tr, scope.GetNextFocus(bl, backward: true));
		Assert.Equal(tl, scope.GetNextFocus(tr, backward: true));
		// Before the first cell: traversal exits the scope backward.
		Assert.Null(scope.GetNextFocus(tl, backward: true));
	}

	[Fact]
	public void GetInitialFocus_ReturnsFirstCell()
	{
		var (_, grid, tl, _, _, _) = BuildTwoByTwo();
		Assert.Equal(tl, ((IFocusScope)grid).GetInitialFocus(backward: false));
	}

	[Fact]
	public void GetInitialFocus_Backward_ReturnsLastCell()
	{
		var (_, grid, _, _, _, br) = BuildTwoByTwo();
		Assert.Equal(br, ((IFocusScope)grid).GetInitialFocus(backward: true));
	}

	[Fact]
	public void SavedFocus_RestoresOnReentry()
	{
		var (_, grid, _, _, bl, _) = BuildTwoByTwo();
		var scope = (IFocusScope)grid;

		// Simulate focus leaving the scope from a middle cell.
		scope.SavedFocus = bl;

		// Forward re-entry returns to the saved cell, then clears it.
		Assert.Equal(bl, scope.GetInitialFocus(backward: false));
		Assert.Null(scope.SavedFocus);
	}

	[Fact]
	public void NestedScopeCell_IsEnteredNotSkipped()
	{
		var window = NewWindow();
		var grid = new GridControl { Width = 60, Height = 10 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));

		var leaf = new ButtonControl { Text = "LEAF" };

		// Cell (0,1) holds a nested focus scope (another grid) whose child must be reachable.
		var nested = new GridControl();
		nested.ColumnDefinitions.Add(GridLength.Star(1));
		nested.RowDefinitions.Add(GridLength.Star(1));
		var nestedButton = new ButtonControl { Text = "NESTED" };
		nested.Place(nestedButton, 0, 0);

		grid.Place(leaf, 0, 0);
		grid.Place(nested, 0, 1);

		window.AddControl(grid);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent(); // re-render: traversal must survive (post-mortem discipline)

		var scope = (IFocusScope)grid;

		// The nested scope appears as an opaque single Tab stop (the nested grid itself), reachable
		// after the leaf — not skipped.
		var next = scope.GetNextFocus(leaf, backward: false);
		Assert.Same(nested, next);

		// Entering the nested scope reaches its inner child.
		Assert.Same(nestedButton, ((IFocusScope)nested).GetInitialFocus(backward: false));
	}

	[Fact]
	public void Cursor_EqualsFocusedChildCursorPlusCellOrigin()
	{
		var window = NewWindow();
		var grid = new GridControl { Width = 60, Height = 10 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));

		// A prompt (reports a logical cursor) in a non-(0,0) cell: row 1, col 1.
		var prompt = new PromptControl { Prompt = "> " };
		grid.Place(prompt, 1, 1);

		window.AddControl(grid);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		var childCursor = prompt.GetLogicalCursorPosition();
		Assert.NotNull(childCursor);

		var gridCursor = ((ILogicalCursorProvider)grid).GetLogicalCursorPosition();
		Assert.NotNull(gridCursor);

		// The grid composes child cursor + cell origin. The cell at (1,1) is not at the grid origin,
		// so the composed cursor must be strictly offset from the child-local cursor.
		var promptNode = window.GetLayoutNode(prompt);
		Assert.NotNull(promptNode);
		int expectedOriginX = promptNode!.AbsoluteBounds.X - grid.ActualX;
		int expectedOriginY = promptNode.AbsoluteBounds.Y - grid.ActualY;

		Assert.Equal(childCursor!.Value.X + expectedOriginX, gridCursor!.Value.X);
		Assert.Equal(childCursor.Value.Y + expectedOriginY, gridCursor.Value.Y);

		// Sanity: the (1,1) cell really is offset from the grid origin (not a no-op translation).
		Assert.True(expectedOriginX > 0 || expectedOriginY > 0,
			"cell (1,1) origin should be below/right of the grid origin");
	}

	[Fact]
	public void Focus_SurvivesReRender()
	{
		var (window, grid, _, _, bl, _) = BuildTwoByTwo();

		window.FocusManager.SetFocus(bl, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();
		Assert.True(bl.HasFocus);
		Assert.True(grid.HasFocus); // grid is in the focus path

		// Re-render must not drop focus.
		window.RenderAndGetVisibleContent();
		Assert.True(bl.HasFocus);
		Assert.True(grid.HasFocus);
		Assert.Same(bl, window.FocusManager.FocusedControl);
	}

	[Fact]
	public void CursorState_SurvivesReRender()
	{
		var window = NewWindow();
		var grid = new GridControl { Width = 60, Height = 10 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));

		var prompt = new PromptControl { Prompt = "> " };
		grid.Place(prompt, 0, 1);

		window.AddControl(grid);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		var before = ((ILogicalCursorProvider)grid).GetLogicalCursorPosition();
		Assert.NotNull(before);

		// An extra re-render must leave the composed cursor position stable.
		window.RenderAndGetVisibleContent();
		var after = ((ILogicalCursorProvider)grid).GetLogicalCursorPosition();
		Assert.NotNull(after);
		Assert.Equal(before!.Value, after!.Value);
	}

	[Fact]
	public void CursorShape_ForwardsFromFocusedCellChild()
	{
		var window = NewWindow();
		var grid = new GridControl { Width = 60, Height = 10 };
		grid.ColumnDefinitions.Add(GridLength.Star(1));
		grid.RowDefinitions.Add(GridLength.Star(1));

		// PromptControl reports an I-beam (VerticalBar) cursor shape.
		var prompt = new PromptControl { Prompt = "> " };
		grid.Place(prompt, 0, 0);

		window.AddControl(grid);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		var childShape = ((ICursorShapeProvider)prompt).PreferredCursorShape;
		Assert.NotNull(childShape);

		// The grid forwards the focused cell child's preferred cursor shape unchanged.
		var gridShape = ((ICursorShapeProvider)grid).PreferredCursorShape;
		Assert.Equal(childShape, gridShape);
	}
}
