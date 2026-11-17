// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// What a wrapping row actually puts on screen.
/// </summary>
/// <remarks>
/// The table drew one line per row for its whole life, and every truncation,
/// wide-glyph and edit-cursor path in the row painter assumes that. These tests
/// pin the two halves of the change: a tall row paints its continuation lines,
/// and a table with no wrapping column paints exactly what it painted before.
/// </remarks>
public class TableWrapRenderingTests
{
	private const string LongTitle =
		"The quick brown fox jumps over the lazy dog and keeps running onward";

	private static List<string> Paint(TableControl table, int w = 40, int h = 12)
	{
		var buffer = new CharacterBuffer(w, h);
		var bounds = new LayoutRect(0, 0, w, h);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		return Enumerable.Range(0, h)
			.Select(y => string.Concat(Enumerable.Range(0, w)
				.Select(x => buffer.GetCell(x, y).Character.ToString())).TrimEnd())
			.ToList();
	}

	private static CharacterBuffer PaintBuffer(TableControl table, int w = 40, int h = 12)
	{
		var buffer = new CharacterBuffer(w, h);
		var bounds = new LayoutRect(0, 0, w, h);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return buffer;
	}

	/// <summary>An ID column and a Title column, the Title optionally wrapping.</summary>
	private static TableControl Build(bool wrap, int maxLines = 0, bool verticalCenter = false)
	{
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
			Height = 10,
		};
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4) { VerticalCenter = verticalCenter });
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = wrap, MaxLines = maxLines });
		table.AddRow("7", LongTitle);
		table.AddRow("8", "second");
		return table;
	}

	[Fact]
	public void TallRow_PaintsWrappedLinesInOrder()
	{
		var lines = Paint(Build(wrap: true));

		// The first line carries the ID and the start of the title.
		Assert.Contains("7", lines[0]);
		Assert.Contains("The quick brown fox", lines[0]);

		// The continuation is on the lines below, and the ID is NOT repeated there.
		Assert.Contains("jumps", lines[1]);
		Assert.DoesNotContain("7", lines[1]);
	}

	[Fact]
	public void TallRow_PlainCellSitsOnFirstLine()
	{
		var lines = Paint(Build(wrap: true));

		Assert.StartsWith("7", lines[0].TrimStart());
		// Lines 2 and 3 of the same row have no ID text at all.
		Assert.DoesNotContain("7", lines[1]);
		Assert.DoesNotContain("7", lines[2]);
	}

	[Fact]
	public void TallRow_IsFollowedByTheNextRowNotOverwrittenByIt()
	{
		var lines = Paint(Build(wrap: true));

		// The 4-line first row must push "second" down to line 5 (index 4).
		int secondRow = lines.FindIndex(l => l.Contains("second"));
		Assert.Equal(4, secondRow);
	}

	[Fact]
	public void TallRow_VerticalCenter_MovesThePlainCellDown()
	{
		var centred = Paint(Build(wrap: true, verticalCenter: true));

		// A 4-line row centres a 1-line cell on its line 1 (0-based), not line 0.
		Assert.DoesNotContain("7", centred[0]);
		Assert.Contains("7", centred[1]);
	}

	[Fact]
	public void TallRow_MaxLinesTwo_PaintsTwoLinesAndAnEllipsis()
	{
		var lines = Paint(Build(wrap: true, maxLines: 2));

		Assert.Contains(ControlDefaults.TableWrapEllipsis.ToString(), lines[1]);
		// The next row follows immediately after the two kept lines.
		Assert.Contains("second", lines[2]);
	}

	[Fact]
	public void TallRow_SelectionFillsEveryLine()
	{
		var table = Build(wrap: true);
		table.SelectedRowIndex = 0;
		var buffer = PaintBuffer(table);

		// Whatever the selected background is, every line of the row's band carries it
		// across the ID column, not just the first.
		var line0 = buffer.GetCell(0, 0).Background;
		for (int y = 1; y < 4; y++)
		{
			Assert.Equal(line0, buffer.GetCell(0, y).Background);
		}

		// And it differs from the unselected row below, or the test proves nothing.
		Assert.NotEqual(line0, buffer.GetCell(0, 4).Background);
	}

	[Fact]
	public void WrapOffTable_RendersExactlyAsBefore()
	{
		// The anti-goal, pinned. A wrap-off table must paint what it always painted:
		// one line per row, the long title clipped at the column edge.
		var lines = Paint(Build(wrap: false));

		Assert.Contains("7", lines[0]);
		Assert.Contains("second", lines[1]);   // row 2 on line 2, not pushed down
		Assert.DoesNotContain("jumps", lines[0]);
		Assert.DoesNotContain("jumps", lines[1]);
	}

	[Fact]
	public void WrapOffTable_IsByteIdenticalToTheSameTableBuiltWithNoWrapSet()
	{
		// Two tables that differ only in having touched the wrap properties must paint
		// identically: setting Wrap=false or NoWrap=true changes nothing.
		var untouched = Build(wrap: false);
		var explicitly = Build(wrap: false);
		explicitly.Columns[1].NoWrap = true;

		Assert.Equal(Paint(untouched), Paint(explicitly));
	}

	[Fact]
	public void WrapOn_WithShortValues_StillPaintsOneLinePerRow()
	{
		// Wrapping enabled but nothing long enough to wrap: the table must not grow rows.
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
			Height = 10,
		};
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4));
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = true });
		table.AddRow("7", "short");
		table.AddRow("8", "also short");

		var lines = Paint(table);

		Assert.Contains("short", lines[0]);
		Assert.Contains("also short", lines[1]);
	}

	[Fact]
	public void TallRow_WithBorders_KeepsItsVerticalsOnEveryLine()
	{
		var table = new TableControl
		{
			BorderStyle = BorderStyle.Single,
			ShowHeader = false,
			Height = 10,
		};
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4));
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = true });
		table.AddRow("7", LongTitle);

		var buffer = PaintBuffer(table);

		// With a border the data starts on line 1. Every line of the tall row needs its
		// left vertical, or the table's frame has a hole in it.
		for (int y = 1; y <= 4; y++)
		{
			int ch = buffer.GetCell(0, y).Character.Value;
			Assert.True(ch != ' ' && ch != 0,
				$"line {y} of a tall row has no left border character, found '{(char)ch}'");
		}
	}
}
