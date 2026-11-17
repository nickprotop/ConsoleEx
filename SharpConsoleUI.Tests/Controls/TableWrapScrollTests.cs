// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Moving and scrolling through rows that are not all the same height.
/// </summary>
/// <remarks>
/// Every one of these paths divided available lines by a constant row height.
/// With a wrapping column that constant is a lie: the arrow key would step a
/// line instead of a row, a page would overshoot, and the viewport would claim
/// to hold more rows than it could draw.
/// </remarks>
public class TableWrapScrollTests
{
	private const string LongTitle =
		"The quick brown fox jumps over the lazy dog and keeps running onward";

	/// <summary>Rows alternating tall and short, so no constant divide can be right.</summary>
	private static TableControl Build(bool wrap, int rows = 6, int height = 10)
	{
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
			Height = height,
		};
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4));
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = wrap });
		for (int r = 0; r < rows; r++)
			table.AddRow(r.ToString(), r % 2 == 0 ? LongTitle : "short");

		// A table only handles keys while focused, so a test that skips this measures
		// nothing: every arrow press is simply ignored.
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 60, Height = 20 };
		window.AddControl(table);
		window.FocusManager.SetFocus(table, FocusReason.Programmatic);

		return table;
	}

	private static void Paint(TableControl table, int w = 40, int h = 10)
	{
		var buffer = new CharacterBuffer(w, h);
		var bounds = new LayoutRect(0, 0, w, h);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void DownArrowFromATallRow_SelectsTheNextRow()
	{
		var table = Build(wrap: true);
		Paint(table);
		table.SelectedRowIndex = 0;

		table.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));

		// Row 1, not line 2 of row 0.
		Assert.Equal(1, table.SelectedRowIndex);
	}

	[Fact]
	public void UpArrowFromAShortRow_SelectsTheTallRowAbove()
	{
		var table = Build(wrap: true);
		Paint(table);
		table.SelectedRowIndex = 1;

		table.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false));

		Assert.Equal(0, table.SelectedRowIndex);
	}

	[Fact]
	public void VisibleRowCount_AccountsForTallRows()
	{
		var wrapped = Build(wrap: true);
		var plain = Build(wrap: false);
		Paint(wrapped);
		Paint(plain);

		// The plain table fits one row per line; the wrapping one spends four lines on
		// every other row, so it must report fewer rows in the same height.
		Assert.True(wrapped.GetVisibleRowCount() < plain.GetVisibleRowCount(),
			$"wrapping table claimed {wrapped.GetVisibleRowCount()} rows visible, "
			+ $"the same as the plain table's {plain.GetVisibleRowCount()}");
	}

	[Fact]
	public void VisibleRowCount_IsAtLeastOne()
	{
		// A row taller than the whole viewport must still count as visible, or nothing
		// can ever be drawn or selected.
		var table = Build(wrap: true, rows: 3, height: 3);
		Paint(table, h: 3);

		Assert.True(table.GetVisibleRowCount() >= 1);
	}

	[Fact]
	public void PageDown_AdvancesByTheRowsThatFit()
	{
		var table = Build(wrap: true, rows: 20);
		Paint(table);
		table.SelectedRowIndex = 0;

		table.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.PageDown, false, false, false));

		// It must move, and it must not leap the whole row count the way a line-based
		// page would.
		Assert.True(table.SelectedRowIndex > 0, "PageDown did not move the selection");
		Assert.True(table.SelectedRowIndex < 10,
			$"PageDown jumped to row {table.SelectedRowIndex}, further than the visible rows");
	}

	[Fact]
	public void EnsureSelectedRowVisible_ShowsAWholeTallRow()
	{
		var table = Build(wrap: true, rows: 20);
		Paint(table);

		table.SelectedRowIndex = 10;
		Paint(table);

		// The selected row must be at or after the scroll offset, and the offset must be
		// a row index that exists.
		Assert.True(table.ScrollOffset <= 10, $"offset {table.ScrollOffset} scrolled past the selection");
		Assert.True(table.ScrollOffset >= 0);
	}

	[Fact]
	public void EnsureSelectedRowVisible_RowTallerThanViewport_ShowsItsFirstLine()
	{
		// A four-line row in a three-line viewport cannot fit. The offset must land on
		// that row rather than going negative or scrolling past it.
		var table = Build(wrap: true, rows: 5, height: 3);
		Paint(table, h: 3);

		table.SelectedRowIndex = 2;
		Paint(table, h: 3);

		Assert.True(table.ScrollOffset >= 0, $"offset went negative: {table.ScrollOffset}");
		Assert.True(table.ScrollOffset <= 2, $"offset {table.ScrollOffset} scrolled past row 2");
	}

	[Fact]
	public void ScrollOffset_StaysARowIndex()
	{
		// The anti-goal: a caller that stored ScrollOffset must not find it now counts lines.
		var table = Build(wrap: true, rows: 20);
		Paint(table);

		table.ScrollOffset = 3;
		Paint(table);

		Assert.Equal(3, table.ScrollOffset);
	}

	[Fact]
	public void WrapOff_ScrollingIsUnchanged()
	{
		var table = Build(wrap: false, rows: 20);
		Paint(table);
		table.SelectedRowIndex = 0;

		table.ProcessKey(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));
		Assert.Equal(1, table.SelectedRowIndex);

		int visible = table.GetVisibleRowCount();
		Assert.Equal(10, visible);   // 10 lines of height, no chrome, one row per line
	}
}
