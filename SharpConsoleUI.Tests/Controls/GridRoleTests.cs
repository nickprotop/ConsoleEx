// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Coverage proving <see cref="GridControl.ColorRole"/> actually reaches the grid's cell-chrome painting
/// (the previous API-lie: the role properties existed but were never read during paint). The role drives
/// cell BORDER colour and the surface fill of chrome cells that have no explicit background; explicit
/// per-cell colours still win, and a Default role leaves today's foreground-based behaviour unchanged.
/// Render-based, mirroring <see cref="GridCellTests"/>: the grid is added to a window so its container
/// chain reaches the active theme and the role resolves to a concrete truecolor.
/// </summary>
public class GridRoleTests
{
	private static GridControl NewGrid(int cols = 1, int rows = 1)
	{
		var grid = new GridControl { Width = 40, Height = 10 };
		for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(GridLength.Star(1));
		for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(GridLength.Star(1));
		return grid;
	}

	private static MarkupControl Label(string text) => new(new List<string> { text });

	// Adds the grid to a fresh test window and returns the raw (ANSI-bearing) render plus the window so
	// callers can resolve role colours against the same container chain the paint pass used.
	private static (string raw, Window window) RenderRaw(GridControl grid)
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(grid);
		var raw = string.Join("\n", window.RenderAndGetVisibleContent());
		return (raw, window);
	}

	private static string Fg(Color c) => $"38;2;{c.R};{c.G};{c.B}";

	private static string Bg(Color c) => $"48;2;{c.R};{c.G};{c.B}";

	[Fact]
	public void CellBorder_UsesRoleColor_WhenRoleSet()
	{
		var grid = NewGrid();
		grid.ColorRole = ColorRole.Primary;
		var cell = grid[0, 0];
		cell.Content = Label("A");
		cell.Border = BorderStyle.Single;

		var (raw, _) = RenderRaw(grid);

		// Expected border colour resolved through the SAME container chain the paint used.
		var roleBorder = ColorResolver.ColorRoleBorder(ColorRole.Primary, grid.Container, grid.Outline);
		Assert.NotNull(roleBorder);
		Assert.Contains(Fg(roleBorder!.Value), raw);
	}

	[Fact]
	public void CellBorder_FallsBackToForeground_WhenNoRole()
	{
		var grid = NewGrid();
		// No role set: ColorRole.Default → ColorRoleBorder returns null → border uses fgColor (legacy).
		var cell = grid[0, 0];
		cell.Content = Label("A");
		cell.Border = BorderStyle.Single;

		var (raw, _) = RenderRaw(grid);

		// Border glyphs still render (legacy behaviour preserved).
		var stripped = ContainerTestHelpers.StripAnsiCodes(raw.Split('\n'));
		Assert.True(stripped.Any(ch => "┌┐└┘─│".Contains(ch)), "border should render with no role set");

		// And the role colour must NOT appear — the no-role path is untouched.
		Assert.Null(ColorResolver.ColorRoleBorder(ColorRole.Default, grid.Container, grid.Outline));
		var roleBorder = ColorResolver.ColorRoleBorder(ColorRole.Primary, grid.Container, grid.Outline);
		Assert.NotNull(roleBorder);
		Assert.DoesNotContain(Fg(roleBorder!.Value), raw);
	}

	[Fact]
	public void ExplicitCellBackground_WinsOverRole()
	{
		var grid = NewGrid();
		grid.ColorRole = ColorRole.Primary;
		var explicitBg = new Color(11, 222, 33); // distinctive truecolor
		var cell = grid[0, 0];
		cell.Content = Label("A");
		cell.Background = explicitBg;

		var (raw, _) = RenderRaw(grid);

		// The explicit cell background must be present...
		Assert.Contains(Bg(explicitBg), raw);

		// ...and it must not have been overridden by the role surface.
		var roleBg = ColorResolver.ColorRoleBackground(ColorRole.Primary, grid.Container, grid.Outline);
		Assert.NotNull(roleBg);
		if (roleBg!.Value.R != explicitBg.R || roleBg.Value.G != explicitBg.G || roleBg.Value.B != explicitBg.B)
			Assert.DoesNotContain(Bg(roleBg.Value), raw);
	}

	[Fact]
	public void RoleBackground_FillsBorderedCell_WhenNoExplicitBg()
	{
		var grid = NewGrid();
		grid.ColorRole = ColorRole.Primary;
		var cell = grid[0, 0];
		cell.Content = Label("A");
		cell.Border = BorderStyle.Single; // chrome, but no explicit background

		var (raw, _) = RenderRaw(grid);

		// The cell interior is filled with the role surface background.
		var roleBg = ColorResolver.ColorRoleBackground(ColorRole.Primary, grid.Container, grid.Outline);
		Assert.NotNull(roleBg);
		Assert.Contains(Bg(roleBg!.Value), raw);
	}

	[Fact]
	public void Outline_Mode_AffectsRoleColors()
	{
		// Outline must be honoured: the resolved role border/background differ between outline on/off, and
		// the grid renders the outline-specific colours when Outline is set.
		var theme = new ModernGrayTheme();
		var filled = ColorRoleResolver.Resolve(ColorRole.Primary, theme, outline: false);
		var outlined = ColorRoleResolver.Resolve(ColorRole.Primary, theme, outline: true);

		// Sanity: outline changes the resolved set (otherwise this test proves nothing).
		Assert.NotEqual(filled.Background, outlined.Background);

		var grid = NewGrid();
		grid.ColorRole = ColorRole.Primary;
		grid.Outline = true;
		var cell = grid[0, 0];
		cell.Content = Label("A");
		cell.Border = BorderStyle.Single;

		var (raw, _) = RenderRaw(grid);

		// The grid resolves through Outline=true, so the outline-mode background fills the cell.
		var roleBgOutline = ColorResolver.ColorRoleBackground(ColorRole.Primary, grid.Container, outline: true);
		var roleBgFilled = ColorResolver.ColorRoleBackground(ColorRole.Primary, grid.Container, outline: false);
		Assert.NotNull(roleBgOutline);
		Assert.NotNull(roleBgFilled);
		Assert.NotEqual(roleBgFilled!.Value, roleBgOutline!.Value);
		Assert.Contains(Bg(roleBgOutline.Value), raw);
	}
}
