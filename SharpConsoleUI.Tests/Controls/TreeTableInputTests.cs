// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using MouseEventArgs = SharpConsoleUI.Events.MouseEventArgs;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Opening and closing the rows of a <see cref="TreeTableControl"/> from the keyboard and with the
/// mouse, while every key and click the table already gives a meaning to keeps it.
/// </summary>
public class TreeTableInputTests
{
	#region Helpers

	/// <summary>
	/// Fiction > (Fantasy > (Dragons, Elves), Mystery); Science > (Physics); Poetry. Borderless, so
	/// the header is line 0 and Fiction line 1:
	/// <code>
	/// [-] Fiction
	/// ├─ [-] Fantasy
	/// │  ├─     Dragons
	/// │  └─     Elves
	/// └─     Mystery
	/// [-] Science
	/// └─     Physics
	///     Poetry
	/// </code>
	/// </summary>
	private static TreeTableControl Library(TreeTableControl? table = null)
	{
		table ??= new TreeTableControl();
		table.ReadOnly = false;
		table.FilteringEnabled = true;
		table.BorderStyle = BorderStyle.None;
		table.AddColumn("Shelf");
		table.AddColumn("Books");
		var fiction = table.AddRootRow("Fiction", "120");
		var fantasy = fiction.AddChild("Fantasy", "70");
		fantasy.AddChild("Dragons", "40");
		fantasy.AddChild("Elves", "30");
		fiction.AddChild("Mystery", "50");
		table.AddRootRow("Science", "80").AddChild("Physics", "80");
		table.AddRootRow("Poetry", "15");
		return table;
	}

	/// <summary>A library hosted in a window and given the focus, so keys reach it.</summary>
	private static TreeTableControl FocusedLibrary(Action<TreeTableControl>? configure = null)
	{
		var table = Library();
		configure?.Invoke(table);
		var system = TestWindowSystemBuilder.CreateTestSystem(60, 20);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 14 };
		window.AddControl(table);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.FocusControl(table);
		return table;
	}

	private static TreeTableControl Painted(TreeTableControl table)
	{
		var buffer = new CharacterBuffer(40, 12);
		var bounds = new LayoutRect(0, 0, 40, 12);
		table.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return table;
	}

	private static TreeTableRow Row(TreeTableControl table, string shelf)
		=> (TreeTableRow)table.Rows.Single(r => r.Cells[0] == shelf);

	private static string? Selected(TableControl table) => table.SelectedRow?.Cells[0];

	private static ConsoleKeyInfo Press(ConsoleKey key, char ch = '\0', bool control = false)
		=> new(ch, key, false, false, control);

	private static void ClickAt(TableControl table, int x, int y, MouseFlags flag = MouseFlags.Button1Clicked)
	{
		var point = new System.Drawing.Point(x, y);
		table.ProcessMouseEvent(new MouseEventArgs(new List<MouseFlags> { flag }, point, point, point));
	}

	#endregion

	#region Keys

	[Fact]
	public void Right_OpensAClosedRow()
	{
		var table = FocusedLibrary();
		table.Collapse(Row(table, "Fantasy"));
		table.SelectRow(Row(table, "Fantasy"));

		Assert.True(table.ProcessKey(Press(ConsoleKey.RightArrow)));

		Assert.True(Row(table, "Fantasy").IsExpanded);
		Assert.Equal("Fantasy", Selected(table));
	}

	[Fact]
	public void Right_OnAnOpenRow_MovesToItsFirstChild()
	{
		var table = FocusedLibrary();
		table.SelectRow(Row(table, "Fantasy"));

		table.ProcessKey(Press(ConsoleKey.RightArrow));

		Assert.Equal("Dragons", Selected(table));
	}

	[Fact]
	public void Right_OnALeaf_IsLeftToTheTable()
	{
		var table = FocusedLibrary();
		table.SelectRow(Row(table, "Dragons"));

		Assert.False(table.ProcessKey(Press(ConsoleKey.RightArrow)));
		Assert.Equal("Dragons", Selected(table));
	}

	[Fact]
	public void Left_ClosesAnOpenRow()
	{
		var table = FocusedLibrary();
		table.SelectRow(Row(table, "Fantasy"));

		Assert.True(table.ProcessKey(Press(ConsoleKey.LeftArrow)));

		Assert.False(Row(table, "Fantasy").IsExpanded);
		Assert.Equal("Fantasy", Selected(table));
	}

	[Fact]
	public void Left_OnALeafOrAClosedRow_MovesToTheParent()
	{
		var table = FocusedLibrary();
		table.SelectRow(Row(table, "Elves"));

		table.ProcessKey(Press(ConsoleKey.LeftArrow));
		Assert.Equal("Fantasy", Selected(table));

		table.ProcessKey(Press(ConsoleKey.LeftArrow));
		table.ProcessKey(Press(ConsoleKey.LeftArrow));
		Assert.Equal("Fiction", Selected(table));
	}

	[Fact]
	public void Left_OnAClosedRoot_IsLeftToTheTable()
	{
		var table = FocusedLibrary();
		table.SelectRow(Row(table, "Poetry"));

		Assert.False(table.ProcessKey(Press(ConsoleKey.LeftArrow)));
	}

	[Fact]
	public void Space_TogglesTheRow()
	{
		var table = FocusedLibrary();
		table.SelectRow(Row(table, "Science"));

		table.ProcessKey(Press(ConsoleKey.Spacebar, ' '));
		Assert.False(Row(table, "Science").IsExpanded);

		table.ProcessKey(Press(ConsoleKey.Spacebar, ' '));
		Assert.True(Row(table, "Science").IsExpanded);
	}

	[Fact]
	public void WithMultiSelect_SpaceSelectsRows_AsInAnyTable()
	{
		var table = FocusedLibrary(t => t.MultiSelectEnabled = true);
		table.SelectRow(Row(table, "Science"));

		table.ProcessKey(Press(ConsoleKey.Spacebar, ' '));

		Assert.True(Row(table, "Science").IsExpanded);
		Assert.Equal([table.SelectedRowIndex], table.GetSelectedIndices());
	}

	[Theory]
	[InlineData(ConsoleKey.OemPlus, '+', ConsoleKey.OemMinus, '-', ConsoleKey.D8, '*')]
	[InlineData(ConsoleKey.Add, '+', ConsoleKey.Subtract, '-', ConsoleKey.Multiply, '*')]
	public void PlusMinusAndStar_Open_Close_AndOpenEverythingUnder(ConsoleKey plus, char plusChar, ConsoleKey minus, char minusChar, ConsoleKey star, char starChar)
	{
		var table = FocusedLibrary();
		table.CollapseAll();
		table.SelectRow(Row(table, "Fiction"));

		table.ProcessKey(Press(star, starChar));
		Assert.True(Row(table, "Fiction").IsExpanded);
		Assert.True(Row(table, "Fantasy").IsExpanded);

		table.ProcessKey(Press(minus, minusChar));
		Assert.False(Row(table, "Fiction").IsExpanded);

		table.ProcessKey(Press(plus, plusChar));
		Assert.True(Row(table, "Fiction").IsExpanded);
	}

	[Fact]
	public void WithCellNavigation_LeftAndRightMoveBetweenCells()
	{
		var table = FocusedLibrary(t => t.CellNavigationEnabled = true);
		table.SelectRow(Row(table, "Fantasy"));
		table.SelectedColumnIndex = 0;

		table.ProcessKey(Press(ConsoleKey.RightArrow));

		Assert.Equal(1, table.SelectedColumnIndex);
		Assert.Equal("Fantasy", Selected(table));
		Assert.True(Row(table, "Fantasy").IsExpanded);
	}

	[Fact]
	public void KeysHeldWithCtrl_AreLeftAlone()
	{
		var table = FocusedLibrary();
		table.SelectRow(Row(table, "Fantasy"));

		table.ProcessKey(Press(ConsoleKey.LeftArrow, control: true));

		Assert.True(Row(table, "Fantasy").IsExpanded);
	}

	[Fact]
	public void KeysWhileFiltered_ChangeOnlyTheFilteredView()
	{
		var table = FocusedLibrary();
		table.ApplyFilter("Dragons");
		table.SelectRow(Row(table, "Fantasy"));

		table.ProcessKey(Press(ConsoleKey.LeftArrow));

		Assert.False(table.IsRowExpanded(Row(table, "Fantasy")));
		Assert.True(Row(table, "Fantasy").IsExpanded);
	}

	#endregion

	#region Clicks

	[Fact]
	public void ClickingTheExpander_TogglesAndSelectsTheRow()
	{
		var table = Painted(Library());

		ClickAt(table, 4, 2);                           // Fantasy's [-]

		Assert.False(Row(table, "Fantasy").IsExpanded);
		Assert.Equal("Fantasy", Selected(table));
	}

	[Theory]
	[InlineData(1)]                                     // the guide
	[InlineData(7)]                                     // the value
	public void ClickingBesideTheExpander_OnlySelects(int x)
	{
		var table = Painted(Library());

		ClickAt(table, x, 2);

		Assert.True(Row(table, "Fantasy").IsExpanded);
		Assert.Equal("Fantasy", Selected(table));
	}

	[Fact]
	public void TwoQuickClicksOnTheExpander_ToggleTwice_AndDoNotActivate()
	{
		var table = Painted(Library());
		var activated = new List<int>();
		table.RowActivated += (_, row) => activated.Add(row);

		ClickAt(table, 1, 1);
		ClickAt(table, 1, 1);

		Assert.True(Row(table, "Fiction").IsExpanded);
		Assert.Empty(activated);
	}

	[Fact]
	public void DoubleClickingTheValue_StillActivatesTheRow()
	{
		var table = Painted(Library());
		var activated = new List<int>();
		table.RowActivated += (_, row) => activated.Add(row);

		ClickAt(table, 8, 1);
		ClickAt(table, 8, 1);

		Assert.Equal([0], activated);
		Assert.True(Row(table, "Fiction").IsExpanded);
	}

	[Fact]
	public void ARowWithoutChildren_HasNoExpanderToClick()
	{
		var table = Painted(Library());

		ClickAt(table, 1, 8);                           // Poetry's blank gutter

		Assert.Equal("Poetry", Selected(table));
	}

	[Fact]
	public void TheExpander_IsFoundInTheTreeColumn_PastTheCheckboxes()
	{
		var table = new TreeTableControl { CheckboxMode = true, TreeColumnIndex = 1 };
		table.ReadOnly = false;
		table.BorderStyle = BorderStyle.None;
		table.AddColumn("Id", TextJustification.Left, 3);
		table.AddColumn("Shelf");
		var fiction = table.AddRootRow("1", "Fiction");
		fiction.AddChild("2", "Fantasy");
		Painted(table);

		int treeColumnStart = FindColumnStart(table, 1, 1);

		ClickAt(table, treeColumnStart + 1, 1);

		Assert.Equal(TableHitZone.Checkbox, table.HitTest(0, 1).Zone);
		Assert.True(treeColumnStart > 3);
		Assert.False(fiction.IsExpanded);
	}

	[Fact]
	public void TheClickableSpan_IsTheWidthTheExpanderOverrideDraws()
	{
		var table = Painted(Library(new ArrowTable()));

		ClickAt(table, 5, 2);                           // past Fantasy's two-cell arrow
		Assert.True(Row(table, "Fantasy").IsExpanded);

		ClickAt(table, 4, 2);                           // on it
		Assert.False(Row(table, "Fantasy").IsExpanded);
	}

	/// <summary>Draws a two-cell arrow for an expander.</summary>
	private sealed class ArrowTable : TreeTableControl
	{
		protected override string GetExpanderMarkup(in TreeTableRowContext context)
		{
			if (context.HasChildren)
				return MarkupParser.Escape(context.IsExpanded ? "v " : "> ");
			return context.ShowsExpanderGutter ? "  " : string.Empty;
		}
	}

	private static int FindColumnStart(TableControl table, int column, int y)
	{
		for (int x = 0; x < 40; x++)
		{
			var hit = table.HitTest(x, y);
			if (hit.ColumnIndex == column && hit.CellOffset == 0)
				return x;
		}
		return -1;
	}

	#endregion
}
