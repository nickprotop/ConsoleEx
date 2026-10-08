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
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// <see cref="TableControlBuilderBase{TSelf}"/> lets the builder of a table derived from
/// <see cref="TableControl"/> share every table setting without breaking the fluent chain, and
/// <see cref="TableControlBuilder"/> keeps exactly the surface it had.
/// </summary>
public class TableControlBuilderBaseTests
{
	#region A derived builder

	/// <summary>A table with a row type and a setting of its own.</summary>
	private sealed class NotesTable : TableControl
	{
		public string? Notebook { get; set; }

		protected override TableRow CreateRow(string[] cells) => new NoteRow(cells);
	}

	private sealed class NoteRow : TableRow
	{
		public NoteRow(string[] cells) : base(cells) { }
	}

	/// <summary>Builds a <see cref="NotesTable"/>, adding one method to the table's.</summary>
	private sealed class NotesTableBuilder : TableControlBuilderBase<NotesTableBuilder>
	{
		private string? _notebook;

		public NotesTableBuilder InNotebook(string notebook)
		{
			_notebook = notebook;
			return this;
		}

		public NotesTable Build() => Apply(new NotesTable { Notebook = _notebook });
	}

	[Fact]
	public void ADerivedBuilder_ChainsTheTablesMethodsAndItsOwn()
	{
		// Compiles only because every base method returns the derived builder.
		NotesTable table = new NotesTableBuilder()
			.WithTitle("Notes")
			.AddColumn("Text")
			.InNotebook("Ideas")
			.Rounded()
			.WithSorting()
			.Build();

		Assert.Equal("Notes", table.Title);
		Assert.Equal("Ideas", table.Notebook);
		Assert.Equal(BorderStyle.Rounded, table.BorderStyle);
		Assert.True(table.SortingEnabled);
	}

	[Fact]
	public void RowsGivenAsText_AreCreatedByTheTable()
	{
		var table = new NotesTableBuilder().AddColumn("Text").AddRow("first").Build();

		Assert.IsType<NoteRow>(Assert.Single(table.Rows));
	}

	[Fact]
	public void RowsGivenAsTextAndAsRows_KeepTheirOrder()
	{
		var given = new TableRow("second");

		var table = new NotesTableBuilder()
			.AddColumn("Text")
			.AddRow("first")
			.AddRow(given)
			.WithRows([new TableRow("third")])
			.AddRow("fourth")
			.Build();

		Assert.Equal(["first", "second", "third", "fourth"], table.Rows.Select(r => r.Cells[0]));
		Assert.Same(given, table.Rows[1]);
	}

	[Fact]
	public void BuildingTwice_GivesEachTableItsOwnTextRows()
	{
		var builder = Builders.Controls.Table().AddColumn("Text").AddRow("only");

		var first = builder.Build();
		var second = builder.Build();

		Assert.NotSame(first.Rows[0], second.Rows[0]);
		Assert.Single(first.Rows);
	}

	#endregion

	#region The 2.6.16 surface

	/// <summary>
	/// Every public method <see cref="TableControlBuilder"/> declared in 2.6.16, by name and parameter
	/// types. Each must still be found on it with the same return type: code compiled against 2.6.16
	/// calls them by exactly that signature.
	/// </summary>
	public static TheoryData<string, Type[]> Methods2616 => new()
	{
		{ "WithColorRole", [typeof(ColorRole), typeof(ThemeMode?)] },
		{ "Outline", [typeof(bool)] },
		{ "AddColumn", [typeof(string), typeof(TextJustification), typeof(int?)] },
		{ "AddColumn", [typeof(TableColumn)] },
		{ "AddColumn", [typeof(string), typeof(TextJustification), typeof(int?), typeof(int?)] },
		{ "WithColumns", [typeof(string[])] },
		{ "AddRow", [typeof(string[])] },
		{ "AddRow", [typeof(TableRow)] },
		{ "WithRows", [typeof(IEnumerable<TableRow>)] },
		{ "WithBorderStyle", [typeof(BorderStyle)] },
		{ "DoubleLine", [] },
		{ "SingleLine", [] },
		{ "Rounded", [] },
		{ "DoubleLineBorder", [] },
		{ "NoBorder", [] },
		{ "WithBorderColor", [typeof(Color)] },
		{ "WithSafeBorder", [typeof(bool)] },
		{ "ShowHeader", [typeof(bool)] },
		{ "HideHeader", [] },
		{ "WithHeaderColors", [typeof(Color), typeof(Color)] },
		{ "WithTitle", [typeof(string), typeof(TextJustification)] },
		{ "ShowRowSeparators", [typeof(bool)] },
		{ "WithWidth", [typeof(int)] },
		{ "WithHeight", [typeof(int)] },
		{ "WithMargin", [typeof(int)] },
		{ "WithMargin", [typeof(int), typeof(int)] },
		{ "WithMargin", [typeof(int), typeof(int), typeof(int), typeof(int)] },
		{ "Centered", [] },
		{ "CenterHorizontal", [] },
		{ "CenterVertical", [] },
		{ "StretchHorizontal", [] },
		{ "AlignBottom", [] },
		{ "WithHorizontalAlignment", [typeof(HorizontalAlignment)] },
		{ "WithVerticalAlignment", [typeof(VerticalAlignment)] },
		{ "WithStickyPosition", [typeof(StickyPosition)] },
		{ "StickyTop", [] },
		{ "StickyBottom", [] },
		{ "WithBackgroundColor", [typeof(Color?)] },
		{ "WithForegroundColor", [typeof(Color?)] },
		{ "WithColors", [typeof(Color?), typeof(Color?)] },
		{ "WithName", [typeof(string)] },
		{ "WithTag", [typeof(object)] },
		{ "WithVisibility", [typeof(bool)] },
		{ "Hidden", [] },
		{ "Interactive", [] },
		{ "WithCellNavigation", [] },
		{ "WithHoverDisabled", [] },
		{ "WithMultiSelect", [] },
		{ "WithRightClickExtendsSelection", [] },
		{ "WithCheckboxMode", [] },
		{ "WithSorting", [] },
		{ "WithColumnResize", [] },
		{ "WithColumnSeparator", [typeof(char), typeof(Color?), typeof(bool)] },
		{ "ScrollbarGutter", [typeof(bool)] },
		{ "WithInlineEditing", [] },
		{ "WithFiltering", [] },
		{ "WithFuzzyFilter", [] },
		{ "WithVerticalScrollbar", [typeof(ScrollbarVisibility)] },
		{ "WithHorizontalScrollbar", [typeof(ScrollbarVisibility)] },
		{ "WithMinScrollbarThumbSize", [typeof(int)] },
		{ "WithMouseWheelScrollSpeed", [typeof(int)] },
		{ "WithDataSource", [typeof(ITableDataSource)] },
		{ "OnSelectedRowChanged", [typeof(EventHandler<int>)] },
		{ "OnRowActivated", [typeof(EventHandler<int>)] },
		{ "OnCellActivated", [typeof(EventHandler<(int Row, int Column)>)] },
		{ "OnCellEditCompleted", [typeof(EventHandler<(int Row, int Column, string OldValue, string NewValue)>)] },
		{ "OnRightClick", [typeof(EventHandler<MouseEventArgs>)] },
	};

	[Theory]
	[MemberData(nameof(Methods2616))]
	public void Every2616Method_IsStillThere_ReturningTheBuilder(string name, Type[] parameters)
	{
		var method = typeof(TableControlBuilder).GetMethod(name, parameters);

		Assert.NotNull(method);
		Assert.Equal(typeof(TableControlBuilder), method!.ReturnType);
	}

	[Fact]
	public void BuildAndTheConversion_AreStillTheBuildersOwn()
	{
		var build = typeof(TableControlBuilder).GetMethod("Build", Type.EmptyTypes);
		var conversion = typeof(TableControlBuilder).GetMethod("op_Implicit", [typeof(TableControlBuilder)]);

		Assert.Equal(typeof(TableControl), build?.ReturnType);
		Assert.Equal(typeof(TableControl), conversion?.ReturnType);
		Assert.True(typeof(TableControlBuilder).IsSealed);
	}

	#endregion
}
