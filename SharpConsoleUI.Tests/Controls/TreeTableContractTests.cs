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

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A <see cref="TreeTableControl"/> without nesting behaves exactly as a <see cref="TableControl"/>:
/// each scenario runs on both, and what they show, hold and select must match.
/// </summary>
/// <remarks>
/// This is what makes a tree table usable wherever a table is: code written against the table's
/// members, given a tree table, sees no difference until it nests a row.
/// </remarks>
public class TreeTableContractTests
{
	#region Scenarios

	private static readonly string[][] People =
	[
		["Alice", "Athens", "34"],
		["Bob", "Berlin", "27"],
		["Carol", "Cairo", "41"],
		["Dave", "Dublin", "27"],
		["Eve", "Essen", "52"],
	];

	private static void Fill(TableControl table)
	{
		table.AddColumn("Name");
		table.AddColumn("City");
		table.AddColumn("Age", TextJustification.Right);
		foreach (var person in People)
			table.AddRow(person);
	}

	public static TheoryData<string> Scenarios => new()
	{
		"filled",
		"inserted",
		"removed",
		"sorted",
		"sorted descending",
		"filtered",
		"filtered and sorted",
		"selection kept across a removal",
		"selection kept across a sort",
		"replaced",
		"cleared and refilled",
		"cell updated",
	};

	private static void Run(string scenario, TableControl table)
	{
		table.SortingEnabled = true;
		table.ReadOnly = false;
		Fill(table);

		switch (scenario)
		{
			case "inserted":
				table.InsertRow(2, "Zed", "Zagreb", "19");
				table.InsertRows(0, [new TableRow("Ann", "Ankara", "60")]);
				break;
			case "removed":
				table.RemoveRow(1);
				table.RemoveRow(3);
				break;
			case "sorted":
				table.SortByColumn(2);
				break;
			case "sorted descending":
				table.SortByColumn(0);
				table.SortByColumn(0);
				break;
			case "filtered":
				table.ApplyFilter("e");
				break;
			case "filtered and sorted":
				table.ApplyFilter("a|e");
				table.SortByColumn(1);
				break;
			case "selection kept across a removal":
				table.SelectedRowIndex = 3;
				table.RemoveRow(0);
				break;
			case "selection kept across a sort":
				table.SelectedRowIndex = 1;
				table.SortByColumn(2);
				break;
			case "replaced":
				table.SetData([new TableRow("Fay", "Florence", "30"), new TableRow("Gus", "Graz", "22")]);
				break;
			case "cleared and refilled":
				table.SortByColumn(0);
				table.ClearRows();
				table.AddRow("Mia", "Milan", "33");
				table.AddRow("Leo", "Lyon", "29");
				break;
			case "cell updated":
				table.UpdateCell(2, 1, "Copenhagen");
				break;
		}
	}

	#endregion

	#region The comparison

	private static List<string> Render(TableControl table)
	{
		var buffer = new CharacterBuffer(40, 12);
		var bounds = new LayoutRect(0, 0, 40, 12);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return Enumerable.Range(0, 12)
			.Select(y => string.Concat(Enumerable.Range(0, 40).Select(x => buffer.GetCell(x, y).Character.ToString())))
			.ToList();
	}

	[Theory]
	[MemberData(nameof(Scenarios))]
	public void AFlatTreeTable_BehavesAsATable(string scenario)
	{
		var table = new TableControl();
		var tree = new TreeTableControl();

		Run(scenario, table);
		Run(scenario, tree);

		Assert.Equal(table.Rows.Select(r => string.Join("|", r.Cells)), tree.Rows.Select(r => string.Join("|", r.Cells)));
		Assert.Equal(table.RowCount, tree.RowCount);
		Assert.Equal(table.SelectedRowIndex, tree.SelectedRowIndex);
		Assert.Equal(table.SelectedRow?.Cells[0], tree.SelectedRow?.Cells[0]);
		Assert.Equal(Render(table), Render(tree));
	}

	#endregion
}
