// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using CB = SharpConsoleUI.Builders.Controls;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// RENDER-LEVEL test that paints a <see cref="MarkupControl"/> built exactly like the
/// DemoApp Markdown page (via <c>Controls.Markdown(...)</c>) and verifies that a fenced
/// C# code block actually produces highlighted cells in the <see cref="CharacterBuffer"/>.
/// This validates the full path: MarkupControl -> MarkupParser -> CharacterBuffer cells.
/// </summary>
[Collection("EnvSerial")]
public class MarkdownRenderHighlightTests
{
	private const int Width = 60;
	private const int Height = 20;

	// The shaded code-block background emitted by MarkdownToMarkup.Convert.
	private static readonly Color CodeBackground = SharpConsoleUI.Configuration.MarkdownStyle.Default.CodeBackground;

	// The default (unhighlighted) code foreground emitted by MarkdownToMarkup.Convert.
	private static readonly Color CodeDefaultForeground = SharpConsoleUI.Configuration.MarkdownStyle.Default.CodeForeground;

	private static readonly Color White = new Color(255, 255, 255);
	private static readonly Color Black = new Color(0, 0, 0);

	private static CharacterBuffer Paint(MarkupControl control)
	{
		var buffer = new CharacterBuffer(Width, Height);
		var bounds = new LayoutRect(0, 0, Width, Height);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return buffer;
	}

	private static string RowString(CharacterBuffer buffer, int y)
	{
		var sb = new StringBuilder();
		for (int x = 0; x < Width; x++)
		{
			var ch = buffer.GetCell(x, y).Character;
			sb.Append(ch.Value == 0 ? ' ' : ch.ToString());
		}
		return sb.ToString();
	}

	private static List<string> NonEmptyRows(CharacterBuffer buffer)
	{
		var rows = new List<string>();
		for (int y = 0; y < Height; y++)
		{
			string row = RowString(buffer, y);
			if (!string.IsNullOrWhiteSpace(row))
				rows.Add($"y={y,2}: |{row.TrimEnd()}|");
		}
		return rows;
	}

	private static bool ColorsEqual(Color a, Color b)
		=> a.R == b.R && a.G == b.G && a.B == b.B;

	[Fact]
	public void MarkdownCodeBlock_RendersHighlightedCells()
	{
		// 1. Build exactly as the DemoApp Markdown page does.
		var control = CB.Markdown("## Code Block\n\n```csharp\nvar x = 1;\n```\n").Build();

		// 2. Paint to a buffer.
		var buffer = Paint(control);

		// 3. Find the row containing "var x = 1".
		int targetRow = -1;
		int varStartX = -1;
		for (int y = 0; y < Height; y++)
		{
			string row = RowString(buffer, y);
			int idx = row.IndexOf("var x = 1", StringComparison.Ordinal);
			if (idx >= 0)
			{
				targetRow = y;
				varStartX = idx;
				break;
			}
		}

		string dump = string.Join("\n", NonEmptyRows(buffer));

		Assert.True(
			targetRow >= 0,
			$"Did not find 'var x = 1' in any painted row. Non-empty rows:\n{dump}");

		// 4. Inspect the three cells forming "var".
		var v = buffer.GetCell(varStartX + 0, targetRow);
		var a = buffer.GetCell(varStartX + 1, targetRow);
		var r = buffer.GetCell(varStartX + 2, targetRow);

		Assert.Equal('v', v.Character.Value == 0 ? '\0' : (char)v.Character.Value);
		Assert.Equal('a', a.Character.Value == 0 ? '\0' : (char)a.Character.Value);
		Assert.Equal('r', r.Character.Value == 0 ? '\0' : (char)r.Character.Value);

		string varColors =
			$"v fg=({v.Foreground.R},{v.Foreground.G},{v.Foreground.B}) bg=({v.Background.R},{v.Background.G},{v.Background.B}); " +
			$"a fg=({a.Foreground.R},{a.Foreground.G},{a.Foreground.B}) bg=({a.Background.R},{a.Background.G},{a.Background.B}); " +
			$"r fg=({r.Foreground.R},{r.Foreground.G},{r.Foreground.B}) bg=({r.Background.R},{r.Background.G},{r.Background.B})";

		// 5a. At least one 'var' cell must have a NON-default, non-white, non-black foreground
		//     (a real syntax-highlight color).
		bool anyHighlighted = false;
		foreach (var cell in new[] { v, a, r })
		{
			Color fg = cell.Foreground;
			bool isDefaultCode = ColorsEqual(fg, CodeDefaultForeground);
			bool isWhite = ColorsEqual(fg, White);
			bool isBlack = ColorsEqual(fg, Black);
			if (!isDefaultCode && !isWhite && !isBlack)
			{
				anyHighlighted = true;
				break;
			}
		}

		Assert.True(
			anyHighlighted,
			$"'var' keyword cells have no highlight color. Colors: {varColors}\nRows:\n{dump}");

		// 5b. At least one painted code cell on the row must carry the shaded code background.
		bool anyShaded = false;
		for (int x = 0; x < Width; x++)
		{
			var cell = buffer.GetCell(x, targetRow);
			if (ColorsEqual(cell.Background, CodeBackground))
			{
				anyShaded = true;
				break;
			}
		}

		Assert.True(
			anyShaded,
			$"No code cell on the 'var' row had the shaded background (40,40,40). Colors: {varColors}\nRows:\n{dump}");
	}
}
