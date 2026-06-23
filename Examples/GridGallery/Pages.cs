// -----------------------------------------------------------------------
// GridGallery — "Control Gallery" example for SharpConsoleUI.
//
// Page builders. Each returns a Fill+Stretch 2x2 GridControl that becomes
// the direct root of a NavigationView page (via nav.SetItemContent). Every
// cell is a captioned, bordered tile holding ONE live library control.
//
// Tile tracks use GridLength.Star (proportional, content-independent) so a
// control that momentarily measures zero never collapses its track.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace GridGallery;

/// <summary>
/// Holder for the live control references the <see cref="Simulation"/> animates
/// each tick on the Charts page. Populated by <see cref="Pages.BuildChartsGrid"/>.
/// </summary>
internal sealed class GalleryRefs
{
	public BarGraphControl[] Bars { get; set; } = Array.Empty<BarGraphControl>();
	public LineGraphControl LineGraph { get; set; } = null!;
	public SparklineControl Sparkline { get; set; } = null!;
	public ProgressBarControl Progress { get; set; } = null!;
}

internal static class Pages
{
	private static readonly Color TileBg = new(34, 40, 60);
	private static readonly Color CaptionClr = new(120, 180, 255);

	#region Shared helpers

	/// <summary>A page-root grid: 2x2 Star tiles, gaps, Fill + Stretch so it fills the page.</summary>
	private static GridControl NewTileGrid() =>
		Controls.Grid()
			.WithColorRole(ColorRole.Primary)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Star(1), GridLength.Star(1))
			.RowGap(1)
			.ColumnGap(2)
			.WithPadding(1, 1, 1, 1)
			.Build();

	/// <summary>
	/// Builds a tile body: a caption Markup on top and the live control below,
	/// stacked inside a Fill ScrollablePanel so the tile flexes with its cell.
	/// </summary>
	private static ScrollablePanelControl Tile(string caption, IWindowControl control)
	{
		var stack = Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		stack.AddControl(Controls.Markup($"[bold rgb({CaptionClr.R},{CaptionClr.G},{CaptionClr.B})]{caption}[/]")
			.WithMargin(1, 1, 1, 0).Build());
		stack.AddControl(control);
		return stack;
	}

	/// <summary>Places a captioned tile into the grid and gives the cell a rounded border + tile bg.</summary>
	private static void PlaceTile(GridControl grid, int row, int col, string caption, IWindowControl control)
	{
		grid.Place(Tile(caption, control), row, col);
		grid.Cell(row, col).Border = BorderStyle.Rounded;
		grid.Cell(row, col).Background = TileBg;
	}

	#endregion

	#region Charts

	/// <summary>BarGraph, LineGraph, Sparkline, ProgressBar — all animated by the simulation.</summary>
	public static GridControl BuildChartsGrid(GalleryRefs refs)
	{
		var grid = NewTileGrid();

		// Bar graph: a small cluster of bars (the tile shows one prominent bar).
		var bar = Controls.BarGraph()
			.WithLabel("CPU")
			.WithValue(42)
			.WithMaxValue(100)
			.WithColorRole(ColorRole.Info)
			.ShowValue()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithMargin(1, 0, 1, 1)
			.Build();

		var line = Controls.LineGraph()
			.WithTitle("Throughput")
			.WithMode(LineGraphMode.Braille)
			.WithColorRole(ColorRole.Success)
			.WithMinValue(0)
			.WithMaxValue(100)
			.WithData(new double[] { 0 })
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 1)
			.Build();
		line.MaxDataPoints = Simulation.MaxPoints;

		var spark = Controls.Sparkline()
			.WithData(new double[] { 0 })
			.WithColorRole(ColorRole.Warning)
			.WithBackgroundColor(TileBg)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithMargin(1, 0, 1, 1)
			.Build();
		spark.MaxDataPoints = Simulation.MaxPoints;

		var progress = Controls.ProgressBar()
			.WithValue(30)
			.WithMaxValue(100)
			.ShowPercentage()
			.WithColorRole(ColorRole.Primary)
			.Stretch()
			.WithMargin(1, 0, 1, 1)
			.Build();

		PlaceTile(grid, 0, 0, "BarGraphControl", bar);
		PlaceTile(grid, 0, 1, "LineGraphControl", line);
		PlaceTile(grid, 1, 0, "SparklineControl", spark);
		PlaceTile(grid, 1, 1, "ProgressBarControl", progress);

		refs.Bars = new[] { bar };
		refs.LineGraph = line;
		refs.Sparkline = spark;
		refs.Progress = progress;
		return grid;
	}

	#endregion

	#region Inputs

	/// <summary>Button, Checkbox, Dropdown, Prompt — focusable, live interactive controls.</summary>
	public static GridControl BuildInputsGrid()
	{
		var grid = NewTileGrid();

		int clicks = 0;
		var clickEcho = Controls.Markup("[dim]not clicked yet[/]").WithMargin(1, 0, 1, 0).Build();
		var button = Controls.Button("Click me")
			.WithColorRole(ColorRole.Primary)
			.WithMargin(1, 0, 1, 1)
			.OnClick((sender, btn) =>
			{
				clicks++;
				clickEcho.SetContent(new List<string> { $"[green]clicked {clicks}x[/]" });
			})
			.Build();
		var buttonStack = Controls.ScrollablePanel().WithVerticalAlignment(VerticalAlignment.Fill).Build();
		buttonStack.AddControl(Controls.Markup($"[bold rgb({CaptionClr.R},{CaptionClr.G},{CaptionClr.B})]ButtonControl[/]")
			.WithMargin(1, 1, 1, 0).Build());
		buttonStack.AddControl(button);
		buttonStack.AddControl(clickEcho);
		grid.Place(buttonStack, 0, 0);
		grid.Cell(0, 0).Border = BorderStyle.Rounded;
		grid.Cell(0, 0).Background = TileBg;

		var checkbox = Controls.Checkbox("Enable feature")
			.Checked()
			.WithMargin(1, 0, 1, 1)
			.Build();
		PlaceTile(grid, 0, 1, "CheckboxControl", checkbox);

		var dropdown = Controls.Dropdown()
			.WithPrompt("pick: ")
			.AddItems("Apple", "Banana", "Cherry", "Date")
			.SelectedIndex(0)
			.WithMargin(1, 0, 1, 1)
			.Build();
		PlaceTile(grid, 1, 0, "DropdownControl", dropdown);

		var prompt = Controls.Prompt("> ")
			.WithInputWidth(24)
			.WithMargin(1, 0, 1, 1)
			.Build();
		PlaceTile(grid, 1, 1, "PromptControl", prompt);

		return grid;
	}

	#endregion

	#region Data

	/// <summary>Table, List, Tree, Markup — data-display controls seeded with sample content.</summary>
	public static GridControl BuildDataGrid()
	{
		var grid = NewTileGrid();

		var table = Controls.Table()
			.WithColorRole(ColorRole.Primary)
			.AddColumn("Sym")
			.AddColumn("Price", TextJustification.Right)
			.AddColumn("Chg", TextJustification.Right)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 1)
			.Build();
		table.AddRow("AAPL", "212.40", "+1.2%");
		table.AddRow("MSFT", "438.10", "-0.4%");
		table.AddRow("NVDA", "126.85", "+3.7%");
		table.AddRow("AMZN", "201.55", "+0.9%");
		PlaceTile(grid, 0, 0, "TableControl", table);

		var list = Controls.List()
			.AddItems("Inbox (12)", "Drafts", "Sent", "Spam", "Trash", "Archive")
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 1)
			.Build();
		PlaceTile(grid, 0, 1, "ListControl", list);

		var root = new TreeNode("Solution");
		var src = root.AddChild("src");
		src.AddChild("Program.cs");
		src.AddChild("Pages.cs");
		var docs = root.AddChild("docs");
		docs.AddChild("README.md");
		root.IsExpanded = true;
		src.IsExpanded = true;
		var tree = Controls.Tree()
			.AddRootNode(root)
			.WithColorRole(ColorRole.Info)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 1)
			.Build();
		PlaceTile(grid, 1, 0, "TreeControl", tree);

		var info = Controls.Markup(
				"[dim]These tiles host real, focusable controls.\nTab between them; the Table sorts and\nfilters; the List and Tree navigate with\narrow keys.[/]")
			.WithMargin(1, 0, 1, 1)
			.Build();
		PlaceTile(grid, 1, 1, "MarkupControl", info);

		return grid;
	}

	#endregion

	#region Text & Markup

	/// <summary>Markdown, gradient, Spinner, and styled Markup — the text-rendering showcase.</summary>
	public static GridControl BuildTextGrid()
	{
		var grid = NewTileGrid();

		var markdown = Controls.Markup(
				"[markdown]# Markdown\nInline **bold**, *italic*, `code`.\n\n- bullet one\n- bullet two\n\n> a block quote[/]")
			.WithMargin(1, 0, 1, 1)
			.Build();
		PlaceTile(grid, 0, 0, "Markdown tag", markdown);

		var gradient = Controls.Markup(
				"[gradient rgb(120,180,255) rgb(220,120,200)]Smooth gradient text rendered cell-by-cell across the line.[/]")
			.WithMargin(1, 0, 1, 1)
			.Build();
		PlaceTile(grid, 0, 1, "Gradient markup", gradient);

		var spinner = Controls.Spinner()
			.WithStyle(SpinnerStyle.Braille)
			.WithColorRole(ColorRole.Success)
			.Spinning()
			.WithMargin(1, 0, 1, 1)
			.Build();
		var spinStack = Controls.ScrollablePanel().WithVerticalAlignment(VerticalAlignment.Fill).Build();
		spinStack.AddControl(Controls.Markup($"[bold rgb({CaptionClr.R},{CaptionClr.G},{CaptionClr.B})]SpinnerControl[/]")
			.WithMargin(1, 1, 1, 0).Build());
		spinStack.AddControl(spinner);
		spinStack.AddControl(Controls.Markup("[dim]animating…[/]").WithMargin(1, 0, 1, 0).Build());
		grid.Place(spinStack, 1, 0);
		grid.Cell(1, 0).Border = BorderStyle.Rounded;
		grid.Cell(1, 0).Background = TileBg;

		var styled = Controls.Markup(
				"[bold yellow]Styled[/] markup supports [underline]decorations[/], [red]colors[/], and [link=https://github.com/nickprotop/ConsoleEx]links[/].")
			.WithMargin(1, 0, 1, 1)
			.Build();
		PlaceTile(grid, 1, 1, "Styled markup", styled);

		return grid;
	}

	#endregion
}
