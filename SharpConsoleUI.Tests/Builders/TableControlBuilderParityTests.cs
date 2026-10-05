// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using System.Reflection;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Pins what <see cref="TableControlBuilder"/> produces, property by property, so that reshaping the
/// builder cannot quietly drop or reorder a setting. Every assertion here was true of 2.6.16 before
/// any of the builder's code moved.
/// </summary>
/// <remarks>
/// Order matters in <c>Build()</c> as well as values: <see cref="TableControl.MultiSelectEnabled"/>
/// set to false clears <see cref="TableControl.CheckboxMode"/>, and the column and row adds only
/// happen without a data source. The tests below therefore assert combinations, not just setters.
/// </remarks>
public class TableControlBuilderParityTests
{
	#region Helpers

	/// <summary>The library's factory, named in full because <c>Controls</c> is also a namespace here.</summary>
	private static TableControlBuilder Table() => SharpConsoleUI.Builders.Controls.Table();

	/// <summary>A data source with one column and one row, enough for the builder to attach it.</summary>
	private sealed class OneCellSource : ITableDataSource
	{
		public event NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }

		public int RowCount => 1;
		public int ColumnCount => 1;
		public string GetColumnHeader(int columnIndex) => "Only";
		public string GetCellValue(int rowIndex, int columnIndex) => "cell";
	}

	/// <summary>The delegate behind a field-like event, so wiring can be asserted without raising it.</summary>
	private static Delegate? HandlerOf(TableControl table, string eventName)
	{
		var field = typeof(TableControl).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(field);
		return (Delegate?)field!.GetValue(table);
	}

	#endregion

	#region Every setting

	[Fact]
	public void AFullyConfiguredBuilder_SetsEveryProperty()
	{
		var tag = new object();

		var table = Table()
			.WithColorRole(ColorRole.Success, ThemeMode.Light)
			.Outline()
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBorderColor(Color.Red)
			.WithSafeBorder()
			.WithHeaderColors(Color.Yellow, Color.Blue)
			.WithTitle("Title", TextJustification.Right)
			.ShowRowSeparators()
			.WithWidth(40)
			.WithHeight(12)
			.WithMargin(1, 2, 3, 4)
			.WithHorizontalAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithStickyPosition(StickyPosition.Top)
			.WithColors(Color.Green, Color.Black)
			.WithName("orders")
			.WithTag(tag)
			.Interactive()
			.WithCellNavigation()
			.WithHoverDisabled()
			.WithRightClickExtendsSelection()
			.WithCheckboxMode()
			.WithSorting()
			.WithColumnResize()
			.WithColumnSeparator('|', Color.Grey, padded: true)
			.ScrollbarGutter()
			.WithInlineEditing()
			.WithFuzzyFilter()
			.WithVerticalScrollbar(ScrollbarVisibility.Always)
			.WithHorizontalScrollbar(ScrollbarVisibility.Never)
			.WithMinScrollbarThumbSize(3)
			.WithMouseWheelScrollSpeed(5)
			.Build();

		Assert.Equal(ColorRole.Success, table.ColorRole);
		Assert.Equal(ThemeMode.Light, table.ColorRoleMode);
		Assert.True(table.Outline);
		Assert.Equal(BorderStyle.Rounded, table.BorderStyle);
		Assert.Equal(Color.Red, table.BorderColor);
		Assert.True(table.UseSafeBorder);
		Assert.Equal(Color.Yellow, table.HeaderForegroundColor);
		Assert.Equal(Color.Blue, table.HeaderBackgroundColor);
		Assert.Equal("Title", table.Title);
		Assert.Equal(TextJustification.Right, table.TitleAlignment);
		Assert.True(table.ShowRowSeparators);
		Assert.Equal(40, table.Width);
		Assert.Equal(12, table.Height);
		Assert.Equal((1, 2, 3, 4), (table.Margin.Left, table.Margin.Top, table.Margin.Right, table.Margin.Bottom));
		Assert.Equal(HorizontalAlignment.Stretch, table.HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Fill, table.VerticalAlignment);
		Assert.Equal(StickyPosition.Top, table.StickyPosition);
		Assert.Equal(Color.Green, table.ForegroundColor);
		Assert.Equal(Color.Black, table.BackgroundColor);
		Assert.Equal("orders", table.Name);
		Assert.Same(tag, table.Tag);
		Assert.False(table.ReadOnly);
		Assert.True(table.CellNavigationEnabled);
		Assert.False(table.HoverEnabled);
		Assert.True(table.RightClickExtendsSelection);
		Assert.True(table.MultiSelectEnabled);
		Assert.True(table.CheckboxMode);
		Assert.True(table.SortingEnabled);
		Assert.True(table.ColumnResizeEnabled);
		Assert.Equal('|', table.ColumnSeparator);
		Assert.Equal(Color.Grey, table.ColumnSeparatorColor);
		Assert.True(table.ColumnSeparatorPadded);
		Assert.True(table.ScrollbarGutter);
		Assert.True(table.InlineEditingEnabled);
		Assert.True(table.FilteringEnabled);
		Assert.True(table.FuzzyFilterEnabled);
		Assert.Equal(ScrollbarVisibility.Always, table.VerticalScrollbarVisibility);
		Assert.Equal(ScrollbarVisibility.Never, table.HorizontalScrollbarVisibility);
		Assert.Equal(3, table.MinScrollbarThumbSize);
		Assert.Equal(5, table.MouseWheelScrollSpeed);
		Assert.True(table.Visible);
	}

	[Fact]
	public void AnUnconfiguredBuilder_KeepsTheTableDefaults()
	{
		var table = Table().Build();

		Assert.Equal(BorderStyle.Single, table.BorderStyle);
		Assert.Null(table.BorderColor);
		Assert.Equal(Color.Default, table.HeaderBackgroundColor);
		Assert.Equal(Color.Default, table.HeaderForegroundColor);
		Assert.True(table.ShowHeader);
		Assert.False(table.ShowRowSeparators);
		Assert.False(table.UseSafeBorder);
		Assert.Null(table.Title);
		Assert.Equal(TextJustification.Center, table.TitleAlignment);
		Assert.True(table.ReadOnly);
		Assert.False(table.CellNavigationEnabled);
		Assert.False(table.MultiSelectEnabled);
		Assert.True(table.HoverEnabled);
		Assert.False(table.CheckboxMode);
		Assert.False(table.RightClickExtendsSelection);
		Assert.False(table.SortingEnabled);
		Assert.False(table.ColumnResizeEnabled);
		Assert.Null(table.ColumnSeparator);
		Assert.Null(table.ColumnSeparatorColor);
		Assert.False(table.ColumnSeparatorPadded);
		Assert.False(table.ScrollbarGutter);
		Assert.False(table.InlineEditingEnabled);
		Assert.False(table.FilteringEnabled);
		Assert.False(table.FuzzyFilterEnabled);
		Assert.Equal(ScrollbarVisibility.Auto, table.VerticalScrollbarVisibility);
		Assert.Equal(ScrollbarVisibility.Auto, table.HorizontalScrollbarVisibility);
		Assert.Equal(HorizontalAlignment.Left, table.HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Top, table.VerticalAlignment);
		Assert.Equal((0, 0, 0, 0), (table.Margin.Left, table.Margin.Top, table.Margin.Right, table.Margin.Bottom));
		Assert.True(table.Visible);
		Assert.Null(table.Width);
		Assert.Null(table.Height);
		Assert.Null(table.Name);
		Assert.Null(table.Tag);
		Assert.Equal(StickyPosition.None, table.StickyPosition);
		Assert.Equal(Color.Default, table.BackgroundColor);
		Assert.Equal(Color.Default, table.ForegroundColor);
		Assert.Equal(ColorRole.Default, table.ColorRole);
		Assert.Null(table.ColorRoleMode);
		Assert.False(table.Outline);
	}

	#endregion

	#region Shortcuts that share a setting

	[Fact]
	public void TheBorderShortcuts_EachSetTheirStyle()
	{
		Assert.Equal(BorderStyle.DoubleLine, Table().DoubleLine().Build().BorderStyle);
		Assert.Equal(BorderStyle.DoubleLine, Table().DoubleLineBorder().Build().BorderStyle);
		Assert.Equal(BorderStyle.Single, Table().Rounded().SingleLine().Build().BorderStyle);
		Assert.Equal(BorderStyle.Rounded, Table().Rounded().Build().BorderStyle);
		Assert.Equal(BorderStyle.None, Table().NoBorder().Build().BorderStyle);
	}

	[Fact]
	public void TheHeaderShortcuts_HideAndShowTheHeader()
	{
		Assert.False(Table().HideHeader().Build().ShowHeader);
		Assert.False(Table().ShowHeader(false).Build().ShowHeader);
		Assert.True(Table().HideHeader().ShowHeader().Build().ShowHeader);
	}

	[Fact]
	public void TheAlignmentShortcuts_EachSetTheirAxis()
	{
		var centered = Table().Centered().Build();
		Assert.Equal(HorizontalAlignment.Center, centered.HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Center, centered.VerticalAlignment);

		Assert.Equal(HorizontalAlignment.Center, Table().CenterHorizontal().Build().HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Center, Table().CenterVertical().Build().VerticalAlignment);
		Assert.Equal(HorizontalAlignment.Stretch, Table().StretchHorizontal().Build().HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Bottom, Table().AlignBottom().Build().VerticalAlignment);
		Assert.Equal(StickyPosition.Top, Table().StickyTop().Build().StickyPosition);
		Assert.Equal(StickyPosition.Bottom, Table().StickyBottom().Build().StickyPosition);
	}

	[Fact]
	public void TheMarginOverloads_SpreadTheirValues()
	{
		var uniform = Table().WithMargin(2).Build().Margin;
		Assert.Equal((2, 2, 2, 2), (uniform.Left, uniform.Top, uniform.Right, uniform.Bottom));

		var pair = Table().WithMargin(3, 1).Build().Margin;
		Assert.Equal((3, 1, 3, 1), (pair.Left, pair.Top, pair.Right, pair.Bottom));
	}

	[Fact]
	public void TheColorSetters_CanEachBeUsedAlone()
	{
		var table = Table().WithForegroundColor(Color.Aqua).WithBackgroundColor(null).Build();

		Assert.Equal(Color.Aqua, table.ForegroundColor);
		Assert.Null(table.BackgroundColor);
	}

	[Fact]
	public void TheVisibilitySetters_HideTheTable()
	{
		Assert.False(Table().Hidden().Build().Visible);
		Assert.False(Table().WithVisibility(false).Build().Visible);
	}

	[Fact]
	public void TheMouseWheelSpeed_IsClampedToOne()
	{
		Assert.Equal(1, Table().WithMouseWheelScrollSpeed(0).Build().MouseWheelScrollSpeed);
	}

	#endregion

	#region What each feature implies

	[Fact]
	public void FeaturesThatNeedInput_MakeTheTableInteractive()
	{
		Assert.False(Table().WithColumnResize().Build().ReadOnly);
		Assert.False(Table().WithInlineEditing().Build().ReadOnly);
		Assert.False(Table().WithFiltering().Build().ReadOnly);
		Assert.False(Table().WithFuzzyFilter().Build().ReadOnly);
		Assert.True(Table().WithSorting().Build().ReadOnly);
	}

	[Fact]
	public void InlineEditing_ImpliesCellNavigation()
	{
		var table = Table().WithInlineEditing().Build();

		Assert.True(table.CellNavigationEnabled);
		Assert.True(table.InlineEditingEnabled);
	}

	[Fact]
	public void FuzzyFilter_ImpliesFiltering()
	{
		var table = Table().WithFuzzyFilter().Build();

		Assert.True(table.FilteringEnabled);
		Assert.True(table.FuzzyFilterEnabled);
	}

	[Fact]
	public void CheckboxModeAndRightClickExtension_ImplyMultiSelect()
	{
		var checkboxes = Table().WithCheckboxMode().Build();
		Assert.True(checkboxes.MultiSelectEnabled);
		Assert.True(checkboxes.CheckboxMode);

		var rightClick = Table().WithRightClickExtendsSelection().Build();
		Assert.True(rightClick.MultiSelectEnabled);
		Assert.True(rightClick.RightClickExtendsSelection);
		Assert.False(rightClick.CheckboxMode);

		Assert.True(Table().WithMultiSelect().Build().MultiSelectEnabled);
	}

	#endregion

	#region Columns, rows and the data source

	[Fact]
	public void ColumnsAndRows_AreAddedInOrder()
	{
		var custom = new TableColumn("Custom");
		var row = new TableRow("r2a", "r2b");

		var table = Table()
			.AddColumn("Id", TextJustification.Right, 4)
			.AddColumn(custom)
			.AddColumn("Floor", TextJustification.Center, null, 6)
			.WithColumns("X", "Y")
			.AddRow("r1a", "r1b")
			.AddRow(row)
			.WithRows(new[] { new TableRow("r3a"), new TableRow("r4a") })
			.Build();

		Assert.Equal(new[] { "Id", "Custom", "Floor", "X", "Y" }, table.Columns.Select(c => c.Header));
		Assert.Equal(TextJustification.Right, table.Columns[0].Alignment);
		Assert.Equal(4, table.Columns[0].Width);
		Assert.Same(custom, table.Columns[1]);
		Assert.Equal(TextJustification.Center, table.Columns[2].Alignment);
		Assert.Null(table.Columns[2].Width);
		Assert.Equal(6, table.Columns[2].MinWidth);

		Assert.Equal(new[] { "r1a", "r2a", "r3a", "r4a" }, table.Rows.Select(r => r.Cells[0]));
		Assert.Same(row, table.Rows[1]);
	}

	[Fact]
	public void ADataSource_WinsOverColumnsAndRows()
	{
		var source = new OneCellSource();

		var table = Table()
			.AddColumn("Ignored")
			.AddRow("ignored")
			.WithDataSource(source)
			.Build();

		Assert.Same(source, table.DataSource);
		Assert.Empty(table.Columns);
		Assert.Empty(table.Rows);
		Assert.Equal(1, table.ColumnCount);
		Assert.Equal(1, table.RowCount);
	}

	#endregion

	#region Events, bindings and conversions

	[Fact]
	public void EveryHandler_IsWiredToItsEvent()
	{
		EventHandler<int> selected = (_, _) => { };
		EventHandler<int> activated = (_, _) => { };
		EventHandler<(int Row, int Column)> cellActivated = (_, _) => { };
		EventHandler<(int Row, int Column, string OldValue, string NewValue)> edited = (_, _) => { };
		EventHandler<MouseEventArgs> rightClick = (_, _) => { };

		var table = Table()
			.OnSelectedRowChanged(selected)
			.OnRowActivated(activated)
			.OnCellActivated(cellActivated)
			.OnCellEditCompleted(edited)
			.OnRightClick(rightClick)
			.Build();

		Assert.Same(selected, HandlerOf(table, nameof(TableControl.SelectedRowChanged)));
		Assert.Same(activated, HandlerOf(table, nameof(TableControl.RowActivated)));
		Assert.Same(cellActivated, HandlerOf(table, nameof(TableControl.CellActivated)));
		Assert.Same(edited, HandlerOf(table, nameof(TableControl.CellEditCompleted)));
		Assert.Same(rightClick, HandlerOf(table, nameof(TableControl.MouseRightClick)));
	}

	[Fact]
	public void DeferredBindings_AreAppliedToTheBuiltTable()
	{
		var builder = Table();
		BaseControl? boundTo = null;
		BindingHelper.AddDeferredBinding(builder, control => boundTo = control);

		var table = builder.Build();

		Assert.Same(table, boundTo);
	}

	[Fact]
	public void TheImplicitConversion_BuildsATable()
	{
		TableControl table = Table().WithTitle("Converted");

		Assert.Equal("Converted", table.Title);
	}

	[Fact]
	public void EveryEntryPoint_ReturnsTheSameBuilderType()
	{
		Assert.IsType<TableControlBuilder>(SharpConsoleUI.Builders.Controls.Table());
		Assert.IsType<TableControlBuilder>(TableControl.Create());
		Assert.IsAssignableFrom<IControlBuilder<TableControl>>(SharpConsoleUI.Builders.Controls.Table());
	}

	#endregion
}
