// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Resolving a click to a row once rows differ in height.
/// </summary>
/// <remarks>
/// The hit test used to subtract the chrome from the click's y and divide by a
/// constant, which is exact while every row is one line and wrong the moment one
/// is not: a click on the third line of a tall row landed on whichever row the
/// division pointed at, two rows further down.
/// </remarks>
public class TableWrapHitTestTests
{
	private const string LongTitle =
		"The quick brown fox jumps over the lazy dog and keeps running onward";

	/// <summary>Row 0 is four lines tall, rows 1 and 2 are one each.</summary>
	private static TableControl Build(bool wrap)
	{
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
			Height = 10,
		};
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4));
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = wrap });
		table.AddRow("7", LongTitle);
		table.AddRow("8", "second");
		table.AddRow("9", "third");
		return table;
	}

	private static void Paint(TableControl table, int w = 40, int h = 12)
	{
		var buffer = new CharacterBuffer(w, h);
		var bounds = new LayoutRect(0, 0, w, h);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var point = new System.Drawing.Point(x, y);
		return new MouseEventArgs(new List<MouseFlags>(flags), point, point, point);
	}

	/// <summary>Clicks at a control-relative line and reports the row the table picked.</summary>
	private static int RowAt(TableControl table, int y)
	{
		table.ProcessMouseEvent(Mouse(6, y, MouseFlags.Button1Clicked));
		return table.SelectedRowIndex;
	}

	[Fact]
	public void ClickOnAnyLineOfATallRow_SelectsThatRow()
	{
		var table = Build(wrap: true);
		Paint(table);

		// The first row owns lines 0 through 3. Every one of them is that row.
		Assert.Equal(0, RowAt(table, 0));
		Assert.Equal(0, RowAt(table, 1));
		Assert.Equal(0, RowAt(table, 2));
		Assert.Equal(0, RowAt(table, 3));
	}

	[Fact]
	public void ClickOnTheRowAfterATallRow_SelectsTheRightRow()
	{
		var table = Build(wrap: true);
		Paint(table);

		// This is the off-by-N the old divide produced: line 4 is row 1, not row 4.
		Assert.Equal(1, RowAt(table, 4));
		Assert.Equal(2, RowAt(table, 5));
	}

	[Fact]
	public void ClickBelowTheLastRow_SelectsNothingNew()
	{
		var table = Build(wrap: true);
		Paint(table);

		RowAt(table, 5);                       // land on the last row first
		Assert.Equal(2, table.SelectedRowIndex);

		RowAt(table, 9);                       // past the data
		Assert.Equal(2, table.SelectedRowIndex);
	}

	[Fact]
	public void GetRowTagAt_KeepsTakingARowIndexNotALine()
	{
		// GetRowTagAt indexes ROWS and must keep doing so: a caller that stored an index
		// cannot have it start meaning a screen line. The click resolves the line to a row;
		// this takes it from there.
		var table = Build(wrap: true);
		table.Rows[0].Tag = "first";
		table.Rows[1].Tag = "second";
		Paint(table);

		Assert.Equal("first", table.GetRowTagAt(0));
		Assert.Equal("second", table.GetRowTagAt(1));
	}

	[Fact]
	public void ClickOnATallRowsLastLine_ThenGetRowTagAt_GivesThatRowsTag()
	{
		// The pair as an application would use them: click a line, take the selected row
		// index, ask for its tag.
		var table = Build(wrap: true);
		table.Rows[0].Tag = "first";
		table.Rows[1].Tag = "second";
		Paint(table);

		Assert.Equal("first", table.GetRowTagAt(RowAt(table, 3)));
		Assert.Equal("second", table.GetRowTagAt(RowAt(table, 4)));
	}

	[Fact]
	public void WrapOff_HitTestingIsUnchanged()
	{
		var table = Build(wrap: false);
		Paint(table);

		Assert.Equal(0, RowAt(table, 0));
		Assert.Equal(1, RowAt(table, 1));
		Assert.Equal(2, RowAt(table, 2));
	}
}
