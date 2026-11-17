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
/// Reaching the wrap setting through the fluent builders.
/// </summary>
public class TableWrapBuilderTests
{
	[Fact]
	public void AddColumn_WithWrap_SetsWrapAndMaxLines()
	{
		var table = TableControl.Create()
			.AddColumn("ID", TextJustification.Left, 4)
			.AddColumn("Title", TextJustification.Left, 20, null, wrap: true, maxLines: 3)
			.Build();

		Assert.False(table.Columns[0].Wrap);
		Assert.True(table.Columns[1].Wrap);
		Assert.Equal(3, table.Columns[1].MaxLines);
	}

	[Fact]
	public void AddColumn_WithWrap_DefaultsToNoLineLimit()
	{
		var table = TableControl.Create()
			.AddColumn("Title", TextJustification.Left, 20, null, wrap: true)
			.Build();

		Assert.True(table.Columns[0].Wrap);
		Assert.Equal(0, table.Columns[0].MaxLines);
	}

	[Fact]
	public void AddColumn_ExistingOverloads_StillDefaultToNoWrap()
	{
		var table = TableControl.Create()
			.AddColumn("A")
			.AddColumn("B", TextJustification.Right, 10)
			.AddColumn("C", TextJustification.Left, 10, 4)
			.AddColumn(new TableColumn("D"))
			.Build();

		Assert.All(table.Columns, c => Assert.False(c.Wrap));
		Assert.All(table.Columns, c => Assert.True(c.NoWrap));
	}

	[Fact]
	public void AddColumn_WithMinWidthAndWrap_KeepsBoth()
	{
		var table = TableControl.Create()
			.AddColumn("Title", TextJustification.Left, 20, 8, wrap: true)
			.Build();

		Assert.Equal(8, table.Columns[0].MinWidth);
		Assert.True(table.Columns[0].Wrap);
	}

	[Fact]
	public void AddColumn_WithValign_SetsIt()
	{
		var table = TableControl.Create()
			.AddColumn("ID", TextJustification.Right, 5, null, wrap: false, valign: VerticalAlignment.Center)
			.AddColumn("Summary", TextJustification.Left, 30, null, wrap: true)
			.Build();

		Assert.Equal(VerticalAlignment.Center, table.Columns[0].VerticalAlignment);
		// Unset, so it reads the table's default.
		Assert.Equal(VerticalAlignment.Top, table.Columns[1].VerticalAlignment);
	}

	[Fact]
	public void AColumnWithNoValign_FollowsTheTableDefault()
	{
		var table = TableControl.Create()
			.AddColumn("ID", TextJustification.Right, 5)
			.Build();
		table.CellVerticalAlignment = VerticalAlignment.Bottom;

		Assert.Equal(VerticalAlignment.Bottom, table.Columns[0].VerticalAlignment);
	}

	[Fact]
	public void TreeTableBuilder_GetsTheSameWrapOverload()
	{
		// The overload lives on TableControlBuilderBase, so both builders have it; a
		// TreeTable-only copy would be the thing that drifts.
		var tree = TreeTableControl.Create()
			.AddColumn("Title", TextJustification.Left, 24, null, wrap: true, maxLines: 2)
			.Build();

		Assert.True(tree.Columns[0].Wrap);
		Assert.Equal(2, tree.Columns[0].MaxLines);
	}
}
