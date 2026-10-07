// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// What a <see cref="TreeTableControl"/> draws: guides and expanders in front of the tree column's
/// values, character for character, in every guide style, and how values and columns lay out around
/// them.
/// </summary>
public class TreeTableRenderingTests
{
	#region Helpers

	/// <summary>
	/// A borderless backlog with a title and a points column:
	/// Feature A > (Story A1 > (Task A1a, Task A1b), Story A2); Feature B > (Story B1).
	/// </summary>
	private static TreeTableControl Backlog(TreeGuide guide = TreeGuide.Line)
	{
		var table = new TreeTableControl { BorderStyle = BorderStyle.None, Guide = guide };
		table.AddColumn("Title");
		table.AddColumn("Pts", TextJustification.Right, 3);
		var featureA = table.AddRootRow("Feature A", "13");
		var storyA1 = featureA.AddChild("Story A1", "8");
		storyA1.AddChild("Task A1a", "5");
		storyA1.AddChild("Task A1b", "3");
		featureA.AddChild("Story A2", "5");
		table.AddRootRow("Feature B", "2").AddChild("Story B1", "2");
		return table;
	}

	private static List<string> Lines(TableControl table, int width = 40, int height = 10)
	{
		var buffer = new CharacterBuffer(width, height);
		var bounds = new LayoutRect(0, 0, width, height);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return Enumerable.Range(0, height)
			.Select(y => string.Concat(Enumerable.Range(0, width).Select(x => buffer.GetCell(x, y).Character.ToString())).TrimEnd())
			.Where(line => line.Length > 0)
			.ToList();
	}

	private static TreeTableRow Row(TreeTableControl table, string title)
		=> (TreeTableRow)table.Rows.Single(r => r.Cells[0] == title);

	#endregion

	#region Guides and expanders

	[Fact]
	public void NestedRows_AreDrawnWithGuidesAndExpanders()
	{
		// Rows without children leave the expander's width blank, so values line up with their
		// siblings'; the auto-width column makes room for the deepest row's guides.
		Assert.Equal(
		[
			"Title             Pts",
			"[-] Feature A      13",
			"├─ [-] Story A1     8",
			"│  ├─     Task A1a  5",
			"│  └─     Task A1b  3",
			"└─     Story A2     5",
			"[-] Feature B       2",
			"└─     Story B1     2",
		], Lines(Backlog()));
	}

	[Fact]
	public void ACollapsedRow_ShowsAClosedExpander_AndHidesItsChildren()
	{
		var table = Backlog();

		table.Collapse(Row(table, "Story A1"));

		Assert.Equal(
		[
			"Title          Pts",
			"[-] Feature A   13",
			"├─ [+] Story A1  8",
			"└─     Story A2  5",
			"[-] Feature B    2",
			"└─     Story B1  2",
		], Lines(table));
	}

	[Theory]
	[InlineData(TreeGuide.Ascii, "+- [-] Story A1", "|  \\-     Task A1b")]
	[InlineData(TreeGuide.DoubleLine, "╠═ [-] Story A1", "║  ╚═     Task A1b")]
	[InlineData(TreeGuide.BoldLine, "┣━ [-] Story A1", "┃  ┗━     Task A1b")]
	public void EachGuideStyle_DrawsItsGlyphs(TreeGuide guide, string story, string lastTask)
	{
		var lines = Lines(Backlog(guide));

		Assert.StartsWith(story, lines[2]);
		Assert.StartsWith(lastTask, lines[4]);
	}

	[Fact]
	public void AWiderIndent_WidensEachAncestorLevel()
	{
		var table = Backlog();
		table.Indent = "    ";

		Assert.StartsWith("│    ├─     Task A1a", Lines(table)[3]);
	}

	[Fact]
	public void ARowWithChildrenNotLoaded_ShowsAnExpander()
	{
		var table = new TreeTableControl { BorderStyle = BorderStyle.None };
		table.AddColumn("Title");
		table.AddRootRow("Loaded");
		var lazy = table.AddRootRow("Lazy");

		lazy.IsExpanded = false;
		lazy.HasUnrealizedChildren = true;

		Assert.Equal(["Title", "    Loaded", "[+] Lazy"], Lines(table));
	}

	[Fact]
	public void WithoutAnyRowToExpand_NoGutterIsDrawn()
	{
		var table = new TreeTableControl { BorderStyle = BorderStyle.None };
		table.AddColumn("Title");
		table.AddRootRow("Only");
		table.AddRootRow("Flat");

		Assert.Equal(["Title", "Only", "Flat"], Lines(table));
	}

	#endregion

	#region The tree column

	[Fact]
	public void TheHierarchy_IsDrawnInTheTreeColumn()
	{
		var table = new TreeTableControl { BorderStyle = BorderStyle.None, TreeColumnIndex = 1 };
		table.AddColumn("Id", TextJustification.Left, 3);
		table.AddColumn("Title");
		table.AddRootRow("1", "Parent").AddChild("2", "Child");

		Assert.Equal(["Id Title", "1  [-] Parent", "2  └─     Child"], Lines(table));
	}

	[Fact]
	public void ATreeColumnThatDoesNotExist_DrawsNoHierarchy()
	{
		var table = new TreeTableControl { BorderStyle = BorderStyle.None, TreeColumnIndex = 5 };
		table.AddColumn("Title");
		table.AddRootRow("Parent").AddChild("Child");

		Assert.Equal(["Title", "Parent", "Child"], Lines(table));
	}

	[Fact]
	public void ANegativeTreeColumn_IsRefused()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new TreeTableControl { TreeColumnIndex = -1 });
	}

	[Fact]
	public void ANarrowTreeColumn_CutsTheValue_NotTheGuides()
	{
		var table = new TreeTableControl { BorderStyle = BorderStyle.None };
		table.AddColumn("Title", TextJustification.Left, 12);
		table.AddRootRow("Parent").AddChild("A long child title");

		Assert.Equal("└─     A lon", Lines(table)[2]);
	}

	[Fact]
	public void AWideCharacterValue_IsLaidOutAfterThePrefix()
	{
		var table = new TreeTableControl { BorderStyle = BorderStyle.None };
		table.AddColumn("Title");
		table.AddRootRow("Parent").AddChild("漢字");

		var line = Lines(table)[2];

		Assert.StartsWith("└─     漢", line);
	}

	#endregion

	#region The real thing

	[Fact]
	public void InAWindow_TheTreeSurvivesARerender()
	{
		var table = Backlog();
		var system = ChromeGeometry.CreateSystem(40, 12);
		var window = new WindowBuilder(system).Frameless().Maximized().Build();
		window.AddControl(table);
		system.WindowStateService.AddWindow(window);

		string StoryLine() => ChromeGeometry.Row(ChromeGeometry.Render(system), 2).TrimEnd('\0', ' ');
		string first = StoryLine();

		table.Collapse(Row(table, "Story A1"));
		table.Expand(Row(table, "Story A1"));

		Assert.StartsWith("├─ [-] Story A1", first);
		Assert.Equal(first, StoryLine());
		Assert.Equal(first, StoryLine());
	}

	#endregion

	#region A data source makes the table flat

	/// <summary>Two comets, flat, as a data source reports them.</summary>
	private sealed class CometSource : ITableDataSource
	{
		public event System.Collections.Specialized.NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }

		public int RowCount => 2;

		public int ColumnCount => 1;

		public string GetColumnHeader(int columnIndex) => "Comet";

		public string GetCellValue(int rowIndex, int columnIndex) => rowIndex == 0 ? "Halley" : "Encke";
	}

	[Fact]
	public void WithADataSource_TheTreesGuidesAndExpandersAreNotDrawn()
	{
		var table = Backlog();

		table.DataSource = new CometSource();

		Assert.Equal(["Comet", "Halley", "Encke"], Lines(table));
	}

	[Fact]
	public void RemovingTheDataSource_ShowsTheTreeAsItWas()
	{
		var table = Backlog();
		table.Collapse((TreeTableRow)table.Rows[1]);   // Story A1 hides its tasks
		var expected = Lines(table);
		table.DataSource = new CometSource();
		Lines(table);

		table.DataSource = null;

		Assert.Equal(expected, Lines(table));
	}

	#endregion
}
