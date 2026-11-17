// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Where a short cell sits in a row made tall by another column's wrapping.
/// </summary>
/// <remarks>
/// Only meaningful once rows can be taller than one line. The alignment is the
/// library's own VerticalAlignment, so a table expresses this the way every other
/// control does.
/// </remarks>
public class TableCellVerticalAlignmentTests
{
	private const string LongTitle =
		"The quick brown fox jumps over the lazy dog and keeps running onward";

	/// <summary>A 4-line wrapping Title beside a 1-line ID whose alignment varies.</summary>
	private static TableControl Build(VerticalAlignment? columnAlign = null,
		VerticalAlignment? tableDefault = null)
	{
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
			Height = 10,
		};
		if (tableDefault.HasValue) table.CellVerticalAlignment = tableDefault.Value;

		var id = new TableColumn("ID", TextJustification.Left, 4);
		if (columnAlign.HasValue) id.VerticalAlignment = columnAlign.Value;
		table.AddColumn(id);
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = true });
		table.AddRow("7", LongTitle);
		return table;
	}

	/// <summary>The line of the row the ID landed on, 0-based within the row.</summary>
	private static int IdLine(TableControl table)
	{
		var buffer = new CharacterBuffer(40, 10);
		var bounds = new LayoutRect(0, 0, 40, 10);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		for (int y = 0; y < 10; y++)
		{
			string line = string.Concat(Enumerable.Range(0, 4)
				.Select(x => buffer.GetCell(x, y).Character.ToString()));
			if (line.Contains('7')) return y;
		}
		return -1;
	}

	[Fact]
	public void DefaultIsTop()
	{
		Assert.Equal(VerticalAlignment.Top, new TableColumn("ID").VerticalAlignment);
		Assert.Equal(VerticalAlignment.Top, new TableControl().CellVerticalAlignment);
		Assert.Equal(0, IdLine(Build()));
	}

	[Fact]
	public void Center_PutsTheCellInTheMiddleOfTheRow()
	{
		// A 4-line row centres a 1-line cell on its line 1: (4 - 1) / 2.
		Assert.Equal(1, IdLine(Build(VerticalAlignment.Center)));
	}

	[Fact]
	public void Bottom_PutsTheCellOnTheRowsLastLine()
	{
		Assert.Equal(3, IdLine(Build(VerticalAlignment.Bottom)));
	}

	[Fact]
	public void Fill_IsTreatedAsTop()
	{
		// A cell's text cannot stretch to fill a height, so Fill has no distinct meaning
		// here. Top is the honest answer rather than throwing on a valid enum value.
		Assert.Equal(0, IdLine(Build(VerticalAlignment.Fill)));
	}

	[Fact]
	public void TheTableDefault_AppliesToEveryColumn()
	{
		Assert.Equal(1, IdLine(Build(tableDefault: VerticalAlignment.Center)));
	}

	[Fact]
	public void AColumn_OverridesTheTableDefault()
	{
		// Table says Center, the column says Bottom: the column wins.
		Assert.Equal(3, IdLine(Build(
			columnAlign: VerticalAlignment.Bottom,
			tableDefault: VerticalAlignment.Center)));
	}

	[Fact]
	public void TheWrappingColumnItself_StartsAtTheTop()
	{
		// Centring a cell that fills the row has nowhere to move it to; the wrapped
		// column must still start on line 0 whatever the default says.
		var table = Build(tableDefault: VerticalAlignment.Bottom);
		var buffer = new CharacterBuffer(40, 10);
		var bounds = new LayoutRect(0, 0, 40, 10);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		string firstLine = string.Concat(Enumerable.Range(0, 40)
			.Select(x => buffer.GetCell(x, 0).Character.ToString()));

		Assert.Contains("The quick", firstLine);
	}

	[Fact]
	public void SettingTheColumnAlignment_RequestsOnlyARepaint()
	{
		// It moves a cell inside a height already decided, so it cannot change layout.
		var (window, col, _) = BuildWindow();

		col.VerticalAlignment = VerticalAlignment.Center;

		Assert.Equal(FrameWork.Repaint, window.PendingWork);
	}

	[Fact]
	public void SettingTheTableDefault_RequestsOnlyARepaint()
	{
		var (window, _, table) = BuildWindow();

		table.CellVerticalAlignment = VerticalAlignment.Bottom;

		Assert.Equal(FrameWork.Repaint, window.PendingWork);
	}

	[Fact]
	public void AWrapOffTable_IsUnaffectedByTheAlignment()
	{
		// Every row is one line, so there is nowhere to align to.
		var table = new TableControl
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
			Height = 10,
			CellVerticalAlignment = VerticalAlignment.Bottom,
		};
		table.AddColumn(new TableColumn("ID", TextJustification.Left, 4));
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20));
		table.AddRow("7", LongTitle);

		Assert.Equal(0, IdLine(table));
	}

	private static (Window window, TableColumn col, TableControl table) BuildWindow()
	{
		var system = Infrastructure.TestWindowSystemBuilder.CreateTestSystem(100, 24);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 80, Height = 20 };
		var table = new TableControl();
		var col = new TableColumn("ID");
		table.AddColumn(col);
		table.AddColumn(new TableColumn("Title", TextJustification.Left, 20) { Wrap = true });
		table.AddRow("7", LongTitle);
		window.AddControl(table);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(new List<System.Drawing.Rectangle> { new(0, 0, 80, 20) });
		return (window, col, table);
	}
}
