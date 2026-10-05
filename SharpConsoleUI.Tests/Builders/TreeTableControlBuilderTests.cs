// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// <see cref="TreeTableControlBuilder"/> builds a <see cref="TreeTableControl"/> with every table
/// setting the table builder has, plus the hierarchy's own.
/// </summary>
public class TreeTableControlBuilderTests
{
	[Fact]
	public void TheFactories_ReturnATreeTableBuilder()
	{
		Assert.IsType<TreeTableControlBuilder>(Builders.Controls.TreeTable());
		Assert.IsType<TreeTableControlBuilder>(TreeTableControl.Create());
	}

	[Fact]
	public void TheHierarchySettings_AreApplied()
	{
		var table = Builders.Controls.TreeTable()
			.AddColumn("Id")
			.AddColumn("Planet")
			.WithGuide(TreeGuide.DoubleLine)
			.WithIndent("    ")
			.WithTreeColumn(1)
			.WithFilterIncludingDescendants()
			.Build();

		Assert.Equal(TreeGuide.DoubleLine, table.Guide);
		Assert.Equal("    ", table.Indent);
		Assert.Equal(1, table.TreeColumnIndex);
		Assert.True(table.FilterIncludesDescendants);
	}

	[Fact]
	public void TheTableSettings_AreApplied_InTheSameChain()
	{
		TreeTableControl table = Builders.Controls.TreeTable()
			.WithTitle("Solar system")
			.AddColumn("Body")
			.Interactive()
			.WithSorting()
			.WithFiltering()
			.WithGuide(TreeGuide.BoldLine)
			.NoBorder();

		Assert.Equal("Solar system", table.Title);
		Assert.False(table.ReadOnly);
		Assert.True(table.SortingEnabled);
		Assert.True(table.FilteringEnabled);
		Assert.Equal(BorderStyle.None, table.BorderStyle);
		Assert.Equal(TreeGuide.BoldLine, table.Guide);
	}

	[Fact]
	public void AddRootRow_ReturnsTheRow_ToNestRowsUnder()
	{
		var builder = Builders.Controls.TreeTable().AddColumn("Body");
		var sun = builder.AddRootRow("Sun");
		sun.AddChild("Earth").AddChild("Moon");
		sun.AddChild("Mars");

		var table = builder.Build();

		Assert.Equal(["Sun", "Earth", "Moon", "Mars"], table.Rows.Select(r => r.Cells[0]));
		Assert.Same(sun, Assert.Single(table.RootRows));
		Assert.Equal(2, table.GetDepth(table.Rows[2]));
	}

	[Fact]
	public void RowsAddedAsText_AreTreeRowRoots()
	{
		var table = Builders.Controls.TreeTable()
			.AddColumn("Body")
			.AddRow("Comet")
			.AddRootRow(new TreeTableRow("Jupiter"))
			.Build();

		Assert.All(table.RootRows, row => Assert.IsType<TreeTableRow>(row));
		Assert.Equal(["Comet", "Jupiter"], table.RootRows.Select(r => r.Cells[0]));
	}

	[Fact]
	public void TheExpansionEvents_AreWired()
	{
		var log = new List<string>();
		var builder = Builders.Controls.TreeTable()
			.AddColumn("Body")
			.OnRowExpansionChanging((_, e) => log.Add($"Changing {e.Row.Cells[0]}"))
			.OnRowExpansionChanged((_, e) => log.Add($"Changed {e.Row.Cells[0]}"));
		builder.AddRootRow("Saturn").AddChild("Titan");
		var table = builder.Build();

		table.Collapse((TreeTableRow)table.Rows[0]);

		Assert.Equal(["Changing Saturn", "Changed Saturn"], log);
	}

	[Fact]
	public void ACancellingHandler_FromTheBuilder_CancelsTheChange()
	{
		var builder = Builders.Controls.TreeTable()
			.AddColumn("Body")
			.OnRowExpansionChanging((_, e) => e.Cancel = true);
		var neptune = builder.AddRootRow("Neptune");
		neptune.AddChild("Triton");
		var table = builder.Build();

		table.Collapse(neptune);

		Assert.True(neptune.IsExpanded);
	}

	[Fact]
	public void TheDefaults_AreTheControlsOwn()
	{
		var built = Builders.Controls.TreeTable().Build();
		var constructed = new TreeTableControl();

		Assert.Equal(constructed.Guide, built.Guide);
		Assert.Equal(constructed.Indent, built.Indent);
		Assert.Equal(constructed.TreeColumnIndex, built.TreeColumnIndex);
		Assert.Equal(constructed.FilterIncludesDescendants, built.FilterIncludesDescendants);
	}
}
