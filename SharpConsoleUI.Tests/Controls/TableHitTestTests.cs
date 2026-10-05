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
/// Where on a <see cref="TableControl"/> the pointer is: which column and row a position falls on,
/// with or without a header, scrolled horizontally or not, and which presses start a column resize.
/// </summary>
public class TableHitTestTests
{
	#region Helpers

	private const int BufferWidth = 40;
	private const int BufferHeight = 10;

	/// <summary>
	/// A borderless interactive table with three five-cell columns, so column <c>c</c> starts at
	/// x = 5·c with no separators to account for.
	/// </summary>
	private static TableControl FixedColumnTable(bool showHeader = true)
	{
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ReadOnly = false,
			ShowHeader = showHeader,
			CellNavigationEnabled = true,
		};
		table.AddColumn("A", TextJustification.Left, 5);
		table.AddColumn("B", TextJustification.Left, 5);
		table.AddColumn("C", TextJustification.Left, 5);
		table.AddRow("a1", "b1", "c1");
		table.AddRow("a2", "b2", "c2");
		return table;
	}

	private static void Paint(TableControl table, int width = BufferWidth)
	{
		var buffer = new CharacterBuffer(width, BufferHeight);
		var bounds = new LayoutRect(0, 0, width, BufferHeight);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var point = new Point(x, y);
		return new MouseEventArgs(new List<MouseFlags>(flags), point, point, point);
	}

	#endregion

	#region Columns are found without a header

	[Fact]
	public void WithoutAHeader_ClickingACell_SelectsItsColumn()
	{
		var table = FixedColumnTable(showHeader: false);
		Paint(table);

		table.ProcessMouseEvent(Mouse(7, table.RowYForTest(1), MouseFlags.Button1Clicked));

		Assert.Equal(1, table.SelectedRowIndex);
		Assert.Equal(1, table.SelectedColumnIndex);
	}

	[Fact]
	public void WithoutAHeader_TheColumnAtAPosition_IsStillKnown()
	{
		var table = FixedColumnTable(showHeader: false);
		Paint(table);

		Assert.Equal(2, table.GetColumnIndexAt(11));
	}

	[Fact]
	public void WithAHeader_TheColumnAtAPosition_IsKnown()
	{
		var table = FixedColumnTable();
		Paint(table);

		Assert.Equal(0, table.GetColumnIndexAt(0));
		Assert.Equal(1, table.GetColumnIndexAt(5));
		Assert.Equal(-1, table.GetColumnIndexAt(15));
	}

	#endregion
}
