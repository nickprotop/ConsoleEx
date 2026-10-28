// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Spectre.Console;

// Spectre has a TreeNode too, and the gallery wants the library's.
using TreeNode = SharpConsoleUI.Controls.TreeNode;

namespace Gallery;

/// <summary>One screenshot: what to build, how big, and what to call it.</summary>
/// <param name="Control">
/// The control's type name. It is also the documentation page the image
/// belongs beside, and the file it is written to.
/// </param>
/// <param name="Width">Columns.</param>
/// <param name="Height">Rows.</param>
/// <param name="Build">Composes the control.</param>
/// <param name="Then">
/// Keys to send before a SECOND capture, or null for one shot. A control whose
/// point is a state change - a dropdown that opens, a panel that collapses -
/// says nothing in its resting state: a closed dropdown is a text field with
/// an arrow. The second image is written as "<c>Name-Suffix.png</c>".
/// </param>
/// <param name="Suffix">What to call the second image.</param>
/// <param name="SettleMs">
/// How long to wait before the first capture. Two frames is enough for a
/// control that paints from data it already has; a terminal has to spawn a
/// process, and photographing it sooner gets an empty black rectangle.
/// </param>
internal sealed record Shot(
	string Control,
	int Width,
	int Height,
	Func<IWindowControl> Build,
	string[]? Then = null,
	string? Suffix = null,
	int SettleMs = 0);

/// <summary>
/// Every control worth a picture, and the data that shows it at its best.
/// </summary>
/// <remarks>
/// <para>
/// Not every control earns one. A button is better explained by the three
/// lines that make it than by a photograph of a button, and forty-seven
/// near-identical rectangles would say less than fifteen chosen ones. The
/// rule: a screenshot is worth taking when the control's value is visual or
/// spatial - it draws something, arranges something, or has a shape prose has
/// to work to describe.
/// </para>
/// <para>
/// The data matters as much as the control. A table of lorem ipsum proves the
/// table renders; a table of plausible data shows what it is FOR. Each entry
/// is sized to its subject rather than to a house style, because a progress
/// bar and a file tree want different rectangles.
/// </para>
/// <para>
/// Built through the public builders, which is what a reader of the
/// documentation would write. A gallery assembled through internals would
/// photograph a control nobody can construct the same way.
/// </para>
/// </remarks>
internal static class Shots
{
	internal static readonly Shot[] All =
	[
		// ── Data and structure ───────────────────────────────────────────

		new("TableControl", 76, 11, () => Controls.Table()
			.AddColumn("Service", TextJustification.Left, 18)
			.AddColumn("Status", TextJustification.Left, 12)
			.AddColumn("Uptime", TextJustification.Right, 10)
			.AddColumn("Memory", TextJustification.Right, 10)
			.AddColumn("Requests", TextJustification.Right, 11)
			.AddRow("api-gateway", "[green]healthy[/]", "41d 6h", "284 MB", "1.2M")
			.AddRow("auth-service", "[green]healthy[/]", "41d 6h", "96 MB", "840k")
			.AddRow("image-worker", "[yellow]degraded[/]", "2h 14m", "1.9 GB", "12k")
			.AddRow("postgres", "[green]healthy[/]", "128d", "4.2 GB", "-")
			.AddRow("redis", "[green]healthy[/]", "128d", "512 MB", "9.8M")
			.AddRow("mailer", "[red]stopped[/]", "-", "-", "0")
			.AddRow("search-index", "[green]healthy[/]", "17d 3h", "2.1 GB", "430k")
			.Rounded()
			.Build()),

		new("TreeControl", 50, 10, () =>
		{
			// Children are attached before the node is handed over, and
			// IsExpanded is set AFTER the tree is built: expanding a node that
			// is not in a control yet has nothing to tell.
			var ui = new TreeNode("SharpConsoleUI/");
			ui.AddChild(new TreeNode("Controls/"));
			ui.AddChild(new TreeNode("Layout/"));
			ui.AddChild(new TreeNode("Themes/"));

			var src = new TreeNode("src/");
			src.AddChild(ui);

			var tests = new TreeNode("tests/");
			tests.AddChild(new TreeNode("Controls/"));
			tests.AddChild(new TreeNode("Layout/"));

			var docs = new TreeNode("docs/");
			docs.AddChild(new TreeNode("controls/"));

			var tree = Controls.Tree().AddRootNodes(src, tests, docs).Build();

			src.IsExpanded = true;
			ui.IsExpanded = true;
			tests.IsExpanded = true;
			docs.IsExpanded = true;

			return tree;
		}),

		new("ListControl", 42, 10, () => Controls.List("Commands")
			.AddItems(
				"Open recent project", "Clone repository", "New solution",
				"Run tests", "Publish NativeAOT", "Attach debugger", "Settings")
			.Build()),

		new("TreeTableControl", 74, 9, () => Controls.TreeTable()
			.AddColumn("Package", TextJustification.Left, 30)
			.AddColumn("Version", TextJustification.Left, 14)
			.AddColumn("Size", TextJustification.Right, 10)
			.AddRow("SharpConsoleUI", "2.10.1", "1.8 MB")
			.AddRow("  Markdig", "0.41.3", "412 KB")
			.AddRow("  Spectre.Console", "0.55.2", "1.1 MB")
			.AddRow("  TextMateSharp", "2.0.4", "684 KB")
			.AddRow("Microsoft.Extensions.Logging", "10.0.0", "96 KB")
			.Rounded()
			.Build()),

		// ── Drawing ──────────────────────────────────────────────────────

		new("ChartControl", 74, 18, () => Controls.Chart()
			.WithTitle("Request latency")
			.WithSubtitle("p50 against p99, the last hour")
			.WithPlotHeight(11)
			.WithYRange(0, 240)
			.WithLegend()
			.AddSeries("p50", Color.Cyan1, Wave(60, 48, 16, 3))
			.AddSeries("p99", Color.Orange1, Wave(60, 150, 52, 7))
			.Build()),

		new("LineGraphControl", 66, 14, () =>
		{
			var graph = Controls.LineGraph().WithTitle("Throughput").Build();
			graph.AddSeries("MB/s", Color.SpringGreen2);

			foreach (double v in Wave(50, 62, 22, 11)) graph.AddDataPoint("MB/s", v);

			return graph;
		}),

		new("BarGraphControl", 50, 3, () => Controls.BarGraph()
			.WithLabel("net10.0")
			.WithValue(128)
			.WithMaxValue(160)
			.WithBarWidth(30)
			.Build()),

		new("SparklineControl", 48, 3, () => Controls.Sparkline()
			.WithData(Wave(40, 50, 30, 5))
			.Build()),

		new("ProgressBarControl", 52, 3, () => Controls.ProgressBar()
			.WithValue(68)
			.Build()),

		new("FigletControl", 58, 7, () => Controls.Figlet("Sharp").Build()),

		// ── Text ─────────────────────────────────────────────────────────

		// The showcase page's own vocabulary: named colours, hex, rgb(), the
		// greyscale ramp, decorations and gradients. Twenty-six controls parse
		// this, so it is worth showing what "this" actually is.
		new("MarkupControl", 66, 17, () => Controls.Markup()
			.AddLine("[bold underline cyan]Markup syntax[/]")
			.AddLine("")
			.AddLine("[dim]named[/]      [cyan]cyan[/]  [magenta]magenta[/]  [yellow]yellow[/]  [springgreen1]springgreen1[/]")
			.AddLine("[dim]hex[/]        [#FF8000]#FF8000[/]  [#8040FF]#8040FF[/]  [#00CED1]#00CED1[/]  [#FF69B4]#FF69B4[/]")
			.AddLine("[dim]rgb()[/]      [rgb(0,120,255)]rgb(0,120,255)[/]  [rgb(0,206,209)]rgb(0,206,209)[/]")
			.AddLine("[dim]greyscale[/]  [grey42]grey42[/]  [grey54]grey54[/]  [grey66]grey66[/]  [grey78]grey78[/]  [grey89]grey89[/]")
			.AddLine("")
			.AddLine("[dim]weight[/]     [bold]bold[/]  [italic]italic[/]  [underline]underline[/]  [strikethrough]struck[/]")
			.AddLine("[dim]combined[/]   [bold italic green]bold italic green[/]  [bold underline hotpink]bold underline[/]")
			.AddLine("")
			.AddLine("[dim]gradient[/]   [gradient=spectrum]a gradient across a run of text[/]")
			.AddLine("[dim]link[/]       [link=https://github.com/nickprotop/ConsoleEx]a link that opens[/]")
			.AddLine("")
			.AddLine("[dim]Anything a control can show, it can show styled.[/]")
			.Build()),

		// Markdown is not a control: Controls.Markdown() returns a MarkupBuilder
		// seeded with a [markdown] tag, so this is MarkupControl rendering
		// markdown. Named for the page it belongs beside.
		new("MarkupControl-markdown", 64, 11, () => Controls.Markdown(
			"""
			# Release notes

			**v2.10.1** ships the diagnostics socket and `cxdbg`.

			- Attach to a running application over a Unix socket
			- Read the screen as text, cells, or a PNG
			- Ask *why* a control is the size it is

			> A terminal app is the one program you cannot printf-debug.
			""").Build()),

		new("MultilineEditControl", 64, 13, () => Controls.MultilineEdit(
			"""
			public override void PaintDOM(CharacterBuffer buffer,
			    LayoutRect bounds, LayoutRect clip,
			    Color foreground, Color background)
			{
			    var cells = MarkupParser.Parse(_text, foreground, background);

			    for (int i = 0; i < cells.Count; i++)
			    {
			        buffer.SetCell(bounds.X + i, bounds.Y, cells[i]);
			    }
			}
			""").Build()),

		// ── Input ────────────────────────────────────────────────────────

		// Several prompts rather than one: the styled prompt string, the
		// placeholder and the read-only state are what distinguish this control
		// from a text box, and one empty field shows none of them.
		new("PromptControl", 54, 10, () =>
		{
			var panel = Controls.Panel().Build();

			panel.AddControl(Controls.Prompt("> ").WithInputWidth(28)
				.WithPlaceholder("type a command").Build());
			panel.AddControl(Controls.Prompt("[bold]λ[/] ").WithInputWidth(28)
				.WithPlaceholder("the prompt is markup too").Build());
			panel.AddControl(Controls.Prompt("[green]$[/] ").WithInputWidth(28)
				.WithPlaceholder("history with the arrow keys").Build());
			panel.AddControl(Controls.Prompt("Branch: ").WithInputWidth(28)
				.ReadOnly().Build());

			return panel;
		}),

		// Several dropdowns, the first one open. Coloured icons, emoji and CJK,
		// because an item is not just a string - and the wide characters double
		// as proof the cell measurement handles them.
		new("DropdownControl", 48, 26, () =>
		{
			var panel = Controls.Panel().Build();

			panel.AddControl(Controls.Markup()
				.AddLine("[bold yellow]Meal Planner[/]")
				.AddLine("")
				.Build());

			panel.AddControl(Controls.Markup().AddLine("[bold]Cuisine[/]").Build());
			panel.AddControl(Controls.Dropdown("Choose...")
				.AddItem(new DropdownItem("\U0001F363 日本料理 Japanese", "\u25cf", Color.Red))
				.AddItem(new DropdownItem("\U0001F355 Italian", "\u25cf", Color.Green))
				.AddItem(new DropdownItem("\U0001F32E Mexican", "\u25cf", Color.Orange1))
				.AddItem(new DropdownItem("\U0001F35B Indian", "\u25cf", Color.Yellow))
				.AddItem(new DropdownItem("\U0001F95F 中華料理 Chinese", "\u25cf", Color.Red))
				.SelectedIndex(0)
				.Build());

			panel.AddControl(Controls.Markup().AddLine("[bold]Dietary[/]").Build());
			panel.AddControl(Controls.Dropdown("Choose...")
				.AddItems("None", "Vegetarian", "Vegan", "Gluten-free", "Keto")
				.SelectedIndex(1)
				.Build());

			panel.AddControl(Controls.Markup().AddLine("[bold]Budget[/]").Build());
			panel.AddControl(Controls.Dropdown("Choose...")
				.AddItems("$ Budget", "$$ Moderate", "$$$ Premium", "$$$$ Luxury")
				.SelectedIndex(2)
				.Build());

			return panel;
		},
			Then: ["Tab", "Enter"], Suffix: null),

		// ── Roles and states ─────────────────────────────────────────────

		// One button says "this is a button". Seven roles, solid and outline,
		// say what the role system IS - which is the thing worth a picture.
		new("ButtonControl", 50, 7, () =>
		{
			var panel = Controls.Panel().Build();

			(ColorRole Role, string Name)[] roles =
			[
				(ColorRole.Primary, "Primary"),
				(ColorRole.Success, "Success"),
				(ColorRole.Warning, "Warning"),
				(ColorRole.Danger, "Danger"),
			];

			foreach (var (role, name) in roles)
			{
				panel.AddControl(Controls.Toolbar()
					.AddButton(Controls.Button($"  {name}  ").WithColorRole(role))
					.AddButton(Controls.Button($"  {name}  ").WithColorRole(role).Outline())
					.Build());
			}

			panel.AddControl(Controls.Toolbar()
				.AddButton(Controls.Button("  Disabled  ").Disabled())
				.Build());

			return panel;
		}),

		new("CheckboxControl", 40, 6, () =>
		{
			var panel = Controls.Panel().Build();

			panel.AddControl(Controls.Checkbox("Ship it").Checked().WithColorRole(ColorRole.Success).Build());
			panel.AddControl(Controls.Checkbox("Needs review").Checked().WithColorRole(ColorRole.Warning).Build());
			panel.AddControl(Controls.Checkbox("Blocked").WithColorRole(ColorRole.Danger).Build());
			panel.AddControl(Controls.Checkbox("Not available").Build());

			return panel;
		}),

		new("RadioControl", 36, 5, () =>
		{
			var panel = Controls.Panel().Build();
			var group = Controls.RadioGroup<string>().Build();

			panel.AddControl(Controls.Radio(group, "Light").Build());
			panel.AddControl(Controls.Radio(group, "Dark").Selected().Build());
			panel.AddControl(Controls.Radio(group, "Follow the system").Build());

			return panel;
		}),

		new("SliderControl", 46, 4, () =>
		{
			var panel = Controls.Panel().Build();

			panel.AddControl(Controls.Slider().WithRange(0, 100).WithValue(72).ShowValueLabel().Build());
			panel.AddControl(Controls.Slider().WithRange(0, 100).WithValue(28).ShowValueLabel().Build());

			return panel;
		}),

		new("RangeSliderControl", 48, 4, () => Controls.RangeSlider()
			.WithRange(0, 1000)
			.WithValues(200, 650)
			.Build()),

		new("ToolbarControl", 52, 4, () => Controls.Toolbar()
			.AddButton(Controls.Button(" Open "))
			.AddButton(Controls.Button(" Save ").WithColorRole(ColorRole.Primary))
			.AddSeparator()
			.AddButton(Controls.Button(" Run ").WithColorRole(ColorRole.Success))
			.AddButton(Controls.Button(" Stop ").WithColorRole(ColorRole.Danger).Outline())
			.Build()),

		// Several pickers, one of them open. A row of closed date fields says
		// nothing a text box would not; the calendar beside them is the control,
		// and the formats show what the option is for.
		new("DatePickerControl", 48, 24, () =>
		{
			var panel = Controls.Panel().Build();
			var when = new DateTime(2026, 10, 28);

			panel.AddControl(Controls.DatePicker("ISO:  ").WithFormat("yyyy-MM-dd").WithSelectedDate(when).Build());
			panel.AddControl(Controls.DatePicker("US:   ").WithFormat("MM/dd/yyyy").WithSelectedDate(when).Build());
			panel.AddControl(Controls.DatePicker("EU:   ").WithFormat("dd.MM.yyyy").WithSelectedDate(when).Build());
			panel.AddControl(Controls.DatePicker("Long: ").WithFormat("d MMM yyyy").WithSelectedDate(when).Build());

			return panel;
		},
			// Tab onto the first picker and open its calendar.
			Then: ["Tab", "Enter"], Suffix: null),

		new("TimePickerControl", 46, 7, () =>
		{
			var panel = Controls.Panel().Build();
			var at = new TimeSpan(14, 30, 45);

			panel.AddControl(Controls.TimePicker("24h:    ").WithSelectedTime(at).Build());
			panel.AddControl(Controls.TimePicker("24h+s:  ").WithSelectedTime(at).WithSeconds().Build());
			panel.AddControl(Controls.TimePicker("Range:  ").WithSelectedTime(at)
				.WithMinTime(new TimeSpan(9, 0, 0))
				.WithMaxTime(new TimeSpan(17, 0, 0))
				.Build());

			return panel;
		}),

		// ── Feedback and chrome ──────────────────────────────────────────

		// One spinner says "a spinner spins". The styles table says what the
		// control IS - twelve of them, each named, the way DemoApp's showcase
		// page lays them out.
		new("SpinnerControl", 58, 15, () =>
		{
			var panel = Controls.Panel().Build();

			(SpinnerStyle Style, Color Colour, string Name, string What)[] styles =
			[
				(SpinnerStyle.Braille, Color.Yellow, "Braille", "the default, reliably narrow"),
				(SpinnerStyle.Circle, Color.Cyan1, "Circle", "quarter-circle rotation"),
				(SpinnerStyle.Line, Color.White, "Line", "ASCII, the classic sequence"),
				(SpinnerStyle.Dots, Color.SpringGreen2, "Dots", "fixed three columns"),
				(SpinnerStyle.Arc, Color.Orange1, "Arc", "arc rotation"),
				(SpinnerStyle.Bounce, Color.Magenta1, "Bounce", "bouncing braille dot"),
				(SpinnerStyle.Star, Color.Yellow, "Star", "twinkling star"),
				(SpinnerStyle.GrowVertical, Color.Cyan1, "GrowVertical", "pulsing vertical bar"),
				(SpinnerStyle.Toggle, Color.Magenta1, "Toggle", "empty and filled square"),
				(SpinnerStyle.Arrow, Color.Orange1, "Arrow", "rotating arrow"),
				(SpinnerStyle.AestheticBar, Color.SpringGreen2, "AestheticBar", "progress bar"),
			];

			foreach (var (style, colour, name, what) in styles)
			{
				panel.AddControl(Controls.HorizontalGrid()
					.Column(c => c.Width(4).Add(Controls.Spinner()
						.WithStyle(style).WithColor(colour).WithMargin(1, 0, 1, 0).Build()))
					.Column(c => c.Flex().Add(Controls.Markup()
						.AddLine($"[bold]{name}[/]  [dim]{what}[/]").Build()))
					.Build());
			}

			return panel;
		}),

		new("ChatTranscriptControl", 64, 14, () =>
		{
			var chat = Controls.ChatTranscript().Build();

			chat.AddMessage(ChatRole.User, "Why is the chart only two rows tall?");
			chat.AddMessage(ChatRole.Assistant,
				"Its parent offered an unbounded height, so the chart measured itself "
				+ "at int.MaxValue and arranged to what was left. Ask `measure` and the "
				+ "cascade names the container that decided it.");
			chat.AddMessage(ChatRole.User, "Show me.");

			return chat;
		}),

		new("MenuControl", 50, 8, () => Controls.Menu()
			.Vertical()
			.AddItem("New window", "Ctrl+N", () => { })
			.AddItem("Open…", "Ctrl+O", () => { })
			.AddItem("Save", "Ctrl+S", () => { })
			.AddItem("Close", "Ctrl+W", () => { })
			.Build()),

		new("ScrollbarControl", 20, 10, () => Controls.Scrollbar()
			.Vertical().WithMaximum(100).WithViewportLength(30).WithValue(40)
			.Build()),

		new("StatusBarControl", 64, 3, () => Controls.StatusBar()
			.AddLeft("Ctrl+P", "Command")
			.AddLeft("F5", "Run")
			.AddCenter("main", "branch")
			.AddRight("Ln 42, Col 8", "")
			.Build()),

		new("FormControl", 56, 14, () => Controls.Form()
			.AddText("name", "Service", "api-gateway")
			.AddDropdown("env", "Environment", ["development", "staging", "production"], "production")
			.AddSlider("replicas", "Replicas", 1, 10, 3)
			.AddCheckbox("tls", "Require TLS", true)
			.Build()),

		new("HtmlControl", 78, 26, () =>
			{
				var html = HtmlBuilder.Create()
					.WithHorizontalAlignment(SharpConsoleUI.Layout.HorizontalAlignment.Stretch)
					.WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
					.Build();

				html.SetContent("""
					<h1>HtmlControl</h1>
					<p>A <b>native</b> HTML widget. AngleSharp parses the markup, a custom
					engine lays out <i>block</i>, <i>inline</i> and <i>table</i>, and it
					renders inside a composited terminal UI.</p>
					<p>Point it at <a href="https://en.wikipedia.org/wiki/Cat">Wikipedia</a>
					and it fetches the article and renders it - infobox, nested tables and
					photographs, the images drawn as <b>half-block pixels</b>.</p>
					<h2>It handles</h2>
					<ul>
					  <li><b>Formatting</b> &mdash; <b>bold</b>, <i>italic</i>,
					      <u>underline</u>, <code>inline code</code> and
					      <a href="https://github.com/nickprotop/ConsoleEx">links</a></li>
					  <li>Nested lists
					    <ol><li>ordered inside unordered</li><li>and back again</li></ol>
					  </li>
					</ul>
					<table>
					  <thead><tr><th>Element</th><th>Rendered as</th></tr></thead>
					  <tbody>
					    <tr><td>table</td><td>a real grid with borders</td></tr>
					    <tr><td>img</td><td>half-block pixels</td></tr>
					  </tbody>
					</table>
					""");

				return html;
			}),

		// ── Containers ───────────────────────────────────────────────────

		new("CollapsiblePanel", 54, 6, () =>
		{
			var panel = Controls.CollapsiblePanel()
				.WithTitle("Connection")
				.Expanded()
				.Rounded()
				.Build();

			panel.AddControl(Controls.Markup()
				.AddLine("[dim]host[/]      db.internal")
				.AddLine("[dim]port[/]      5432")
				.AddLine("[dim]database[/]  production")
				.AddLine("[dim]ssl[/]       required")
				.Build());

			return panel;
		}, Then: ["Tab", "Enter"], Suffix: "collapsed"),

		new("PanelControl", 52, 8, () =>
		{
			var panel = Controls.Panel().WithHeader("Build").Rounded().Build();

			panel.AddControl(Controls.Markup()
				.AddLine("[green]✔[/] restore      [dim]1.2s[/]")
				.AddLine("[green]✔[/] compile      [dim]8.4s[/]")
				.AddLine("[green]✔[/] test         [dim]41 passed[/]")
				.AddLine("[yellow]●[/] publish      [dim]running…[/]")
				.Build());

			return panel;
		}),

		new("ScrollablePanelControl", 46, 9, () =>
		{
			var panel = Controls.ScrollablePanel().WithScrollbar().Build();

			for (int i = 1; i <= 14; i++)
			{
				panel.AddControl(Controls.Markup()
					.AddLine($"[dim]{i,2}[/]  line {i} of a longer document")
					.Build());
			}

			return panel;
		}),

		new("GridControl", 60, 10, () =>
		{
			// Auto rows, not Star: a Star row divides a height the grid has been
			// given, and the gallery's window gives it none - so two Star rows
			// of nothing rendered an empty rectangle.
			var grid = Controls.Grid()
				.Columns(GridLength.Star(1), GridLength.Star(1))
				.Rows(GridLength.Auto(), GridLength.Auto())
				.ColumnGap(2)
				.RowGap(1)
				.Build();

			grid.Place(Cell("CPU", "34%", "green"), 0, 0);
			grid.Place(Cell("Memory", "6.1 GB", "cyan"), 0, 1);
			grid.Place(Cell("Disk", "812 GB", "yellow"), 1, 0);
			grid.Place(Cell("Network", "12 MB/s", "magenta"), 1, 1);

			return grid;
		}),

		new("HorizontalGridControl", 60, 7, () => Controls.HorizontalGrid()
			.Column(c => c.Width(18).Add(Controls.Markup()
				.AddLine("[bold]Sidebar[/]").AddLine("[dim]fixed, 18 wide[/]").Build()))
			.Column(c => c.Flex().Add(Controls.Markup()
				.AddLine("[bold]Content[/]").AddLine("[dim]takes what is left[/]").Build()))
			.Build()),

		new("CanvasControl", 50, 12, () =>
		{
			var canvas = Controls.Canvas(48, 11).Build();
			var g = canvas.BeginPaint();

			g.Clear(Color.Black);
			g.DrawBox(0, 0, 47, 10, BoxChars.Rounded, Color.Cyan1, Color.Black);
			g.DrawLine(2, 8, 45, 2, '·', Color.SpringGreen2, Color.Black);
			g.DrawCircle(12, 5, 3, '*', Color.Yellow, Color.Black);
			g.FillRect(30, 4, 8, 3, '▒', Color.Magenta1, Color.Black);
			g.WriteString(3, 1, "CanvasGraphics", Color.White, Color.Black);

			canvas.EndPaint();
			return canvas;
		}),

		new("TabControl", 62, 9, () =>
		{
			var tabs = Controls.TabControl().Build();

			tabs.AddTab("Overview", Controls.Markup()
				.AddLine("[bold]SharpConsoleUI[/]")
				.AddLine("")
				.AddLine("A terminal GUI framework for .NET.")
				.Build());

			tabs.AddTab("Metrics", Controls.Markup()
				.AddLine("[dim]frames[/]   1,284")
				.AddLine("[dim]written[/]  96 KB")
				.Build());

			tabs.AddTab("Logs", Controls.Markup().AddLine("[dim]nothing to report[/]").Build());

			return tabs;
		}),
	];

	/// <summary>A labelled figure, for the grid shot.</summary>
	private static IWindowControl Cell(string label, string value, string colour) =>
		Controls.Markup().AddLine($"[dim]{label}[/]").AddLine($"[{colour}]{value}[/]").Build();

	/// <summary>A plausible series, so a chart shows what a chart is for.</summary>
	/// <remarks>
	/// Seeded, because the gallery's whole value is that a rebuild produces the
	/// same bytes. Random data would make every regeneration a diff.
	/// </remarks>
	private static double[] Wave(int n, double mid, double swing, int seed)
	{
		var rng = new Random(seed);
		var values = new double[n];
		double at = mid;

		for (int i = 0; i < n; i++)
		{
			at += (rng.NextDouble() - 0.5) * swing * 0.5;
			at = Math.Clamp(at, mid - swing, mid + swing);
			values[i] = Math.Round(at, 1);
		}

		return values;
	}
}
