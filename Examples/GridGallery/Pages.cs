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
	/// Builds a tile body: a caption Markup on top and one-or-more live controls below,
	/// stacked inside a Fill ScrollablePanel so the tile flexes with its cell. Passing several
	/// controls lets a tile SHOWCASE a control's range (roles, states, variations) — the way the
	/// DemoApp demonstrates functionality rather than a single static instance.
	/// </summary>
	private static ScrollablePanelControl Tile(string caption, params IWindowControl[] controls)
	{
		var stack = Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		stack.AddControl(Controls.Markup($"[bold rgb({CaptionClr.R},{CaptionClr.G},{CaptionClr.B})]{caption}[/]")
			.WithMargin(1, 1, 1, 0).Build());
		foreach (var c in controls)
			stack.AddControl(c);
		return stack;
	}

	/// <summary>A thin labelled sub-caption used inside a showcase tile to separate variations.</summary>
	private static MarkupControl Note(string text) =>
		Controls.Markup($"[dim]{text}[/]").WithMargin(1, 0, 1, 0).Build();

	/// <summary>Places a captioned tile (one or more controls) into the grid with a rounded border + tile bg.</summary>
	private static void PlaceTile(GridControl grid, int row, int col, string caption, params IWindowControl[] controls)
	{
		grid.Place(Tile(caption, controls), row, col);
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

	/// <summary>Button, Checkbox, Dropdown, Prompt — each tile SHOWCASES the control's variations.</summary>
	public static GridControl BuildInputsGrid()
	{
		var grid = NewTileGrid();

		// --- ButtonControl: every ColorRole, plus an outline variant, plus live click echo. ---
		int clicks = 0;
		var clickEcho = Note("not clicked yet");
		IWindowControl RoleButton(string text, ColorRole role, bool outline = false) =>
			Controls.Button(text)
				.WithColorRole(role)
				.Outline(outline)
				.WithAlignment(HorizontalAlignment.Stretch)
				.WithMargin(1, 0, 1, 1)
				.OnClick((sender, btn) =>
				{
					clicks++;
					clickEcho.SetContent(new List<string> { $"[green]clicked {clicks}×[/] — last: {text}" });
				})
				.Build();
		PlaceTile(grid, 0, 0, "ButtonControl — ColorRoles",
			RoleButton("Primary", ColorRole.Primary),
			RoleButton("Success", ColorRole.Success),
			RoleButton("Warning", ColorRole.Warning),
			RoleButton("Danger", ColorRole.Danger),
			RoleButton("Info · outline", ColorRole.Info, outline: true),
			clickEcho);

		// --- CheckboxControl: checked / unchecked / roled / disabled. ---
		PlaceTile(grid, 0, 1, "CheckboxControl — states",
			Controls.Checkbox("Checked").Checked().WithMargin(1, 0, 1, 0).Build(),
			Controls.Checkbox("Unchecked").WithMargin(1, 0, 1, 0).Build(),
			Controls.Checkbox("Success role").Checked().WithColorRole(ColorRole.Success).WithMargin(1, 0, 1, 0).Build(),
			Controls.Checkbox("Custom mark").WithCheckmark("✓", "·").Checked().WithMargin(1, 0, 1, 0).Build(),
			DisabledCheckbox("Disabled"));

		// --- DropdownControl + a custom prompt — selection echoes live. ---
		var ddEcho = Note("selected: Apple");
		var dropdown = Controls.Dropdown()
			.WithPrompt("fruit: ")
			.AddItems("Apple", "Banana", "Cherry", "Date", "Elderberry")
			.SelectedIndex(0)
			.WithMargin(1, 0, 1, 1)
			.Build();
		dropdown.SelectedIndexChanged += (s, i) =>
			ddEcho.SetContent(new List<string> { $"[dim]selected: {dropdown.SelectedItem}[/]" });
		PlaceTile(grid, 1, 0, "DropdownControl",
			Note("Enter opens the list; ↑↓ to choose."),
			dropdown,
			ddEcho);

		// --- PromptControl: a couple of prompts incl. a masked one. ---
		PlaceTile(grid, 1, 1, "PromptControl",
			Note("Type into the fields:"),
			Controls.Prompt("name: ").WithInputWidth(22).WithMargin(1, 0, 1, 0).Build(),
			Controls.Prompt("search ").WithInputWidth(22).WithMargin(1, 0, 1, 1).Build());

		return grid;
	}

	/// <summary>A disabled checkbox (IsEnabled is a control property, not a builder method).</summary>
	private static CheckboxControl DisabledCheckbox(string label)
	{
		var cb = Controls.Checkbox(label).WithMargin(1, 0, 1, 0).Build();
		cb.IsEnabled = false;
		return cb;
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
			.StretchHorizontal() // fill the cell width (distributes slack across auto columns)
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

		// SpinnerControl: several animation styles + roles, all live.
		IWindowControl Spin(SpinnerStyle style, ColorRole role) =>
			Controls.Spinner().WithStyle(style).WithColorRole(role).Spinning().WithMargin(1, 0, 1, 0).Build();
		PlaceTile(grid, 1, 0, "SpinnerControl — styles",
			Note("Braille / Dots / Line / Circle, live:"),
			Spin(SpinnerStyle.Braille, ColorRole.Success),
			Spin(SpinnerStyle.Dots, ColorRole.Info),
			Spin(SpinnerStyle.Line, ColorRole.Warning),
			Spin(SpinnerStyle.Circle, ColorRole.Primary));

		// Styled markup: decorations, colors, links, status helpers.
		PlaceTile(grid, 1, 1, "Styled markup & status",
			Controls.Markup("[bold yellow]Bold[/] · [underline]underline[/] · [italic]italic[/] · [strikethrough]strike[/]")
				.WithMargin(1, 0, 1, 0).Build(),
			Controls.Markup("[red]red[/] [green]green[/] [blue]blue[/] · [link=https://github.com/nickprotop/ConsoleEx]a link[/]")
				.WithMargin(1, 0, 1, 0).Build(),
			Margined(Controls.Info("Info: an informational message")),
			Margined(Controls.Success("Success: it worked")),
			Margined(Controls.Warning("Warning: heads up")),
			Margined(Controls.Error("Error: something failed"), bottom: 1));

		return grid;
	}

	/// <summary>Applies a left/right margin to a plain MarkupControl (the status helpers return a control,
	/// not a builder, so margins are set as a property).</summary>
	private static MarkupControl Margined(MarkupControl m, int bottom = 0)
	{
		m.Margin = new Margin(1, 0, 1, bottom);
		return m;
	}

	#endregion

	#region More Controls

	/// <summary>Slider, RangeSlider, DatePicker, MultilineEdit — each tile showcases the control's options.</summary>
	public static GridControl BuildMoreGrid()
	{
		var grid = NewTileGrid();

		// SliderControl: a few sliders with different ranges/roles, value labels live.
		PlaceTile(grid, 0, 0, "SliderControl",
			Note("Volume"),
			Controls.Slider().WithRange(0, 100).WithValue(70).ShowValueLabel().WithColorRole(ColorRole.Primary)
				.WithAlignment(HorizontalAlignment.Stretch).WithMargin(1, 0, 1, 0).Build(),
			Note("Brightness"),
			Controls.Slider().WithRange(0, 100).WithValue(40).ShowValueLabel().WithColorRole(ColorRole.Warning)
				.WithAlignment(HorizontalAlignment.Stretch).WithMargin(1, 0, 1, 0).Build(),
			Note("Balance (−50…50)"),
			Controls.Slider().WithRange(-50, 50).WithValue(0).ShowValueLabel().WithColorRole(ColorRole.Info)
				.WithAlignment(HorizontalAlignment.Stretch).WithMargin(1, 0, 1, 1).Build());

		// RangeSliderControl: a dual-handle range.
		PlaceTile(grid, 0, 1, "RangeSliderControl",
			Note("Price range:"),
			Controls.RangeSlider().WithRange(0, 1000).WithValues(200, 750).ShowValueLabel()
				.WithColorRole(ColorRole.Success)
				.WithAlignment(HorizontalAlignment.Stretch).WithMargin(1, 0, 1, 1).Build(),
			Note("Drag either handle (←/→ when focused)."));

		// DatePickerControl: a calendar-backed picker.
		PlaceTile(grid, 1, 0, "DatePickerControl",
			Note("Enter opens the calendar:"),
			Controls.DatePicker("date: ")
				.WithFormat("yyyy-MM-dd")
				.WithAlignment(HorizontalAlignment.Stretch)
				.WithMargin(1, 0, 1, 1).Build());

		// MultilineEditControl: an editable multi-line buffer.
		PlaceTile(grid, 1, 1, "MultilineEditControl",
			Controls.MultilineEdit()
				.WithContentLines(
					"Editable multi-line text.",
					"Arrow keys move the caret;",
					"type to insert, Enter for a new line.",
					"",
					"Try editing me.")
				.WithWrapMode(WrapMode.Wrap)
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.WithMargin(1, 0, 1, 1).Build());

		return grid;
	}

	#endregion
}
