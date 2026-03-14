using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class ContainerBgDemoWindow
{
	private const int WindowWidth = 120;
	private const int WindowHeight = 40;

	private static readonly Color ExplicitBg = new(40, 20, 20);
	private static readonly Color NestedBg = new(20, 40, 20);

	public static Window Create(ConsoleWindowSystem ws)
	{
		var gradient = ColorGradient.FromColors(
			new Color(50, 60, 120),
			new Color(10, 10, 25));

		// === LEFT COLUMN: Direct children of window (via grid) ===
		// === RIGHT COLUMN: Nested containers ===

		var mainGrid = Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 0, 1, 0)
			// LEFT: Simple container cases
			.Column(col => col
				.Add(SectionHeader("[bold cyan]Direct Children[/]  [dim](Grid has no bg)[/]"))

				// Case 1: Markup with no bg → gradient shows through
				.Add(CaseLabel("1. Markup, no bg → [green]gradient preserved[/]"))
				.Add(Controls.Markup("[white]This text should show gradient behind it[/]")
					.WithMargin(1, 0, 0, 0).Build())

				// Case 2: Panel with no bg → gradient shows through border
				.Add(CaseLabel("2. Panel, no bg → [green]gradient in border[/]"))
				.Add(PanelControl.Create()
					.WithHeader("No Background")
					.HeaderCenter()
					.Rounded()
					.WithBorderColor(Color.Cyan1)
					.WithPadding(1, 0, 1, 0)
					.WithContent("[white]Panel border and content\nshould show gradient[/]")
					.Build())

				// Case 3: Panel with explicit bg → solid, no gradient
				.Add(CaseLabel("3. Panel, explicit bg → [red]solid, gradient blocked[/]"))
				.Add(PanelControl.Create()
					.WithHeader("Explicit Background")
					.HeaderCenter()
					.Rounded()
					.WithBorderColor(Color.Yellow)
					.WithBackgroundColor(ExplicitBg)
					.WithPadding(1, 0, 1, 0)
					.WithContent($"[white]Panel bg = ({ExplicitBg.R},{ExplicitBg.G},{ExplicitBg.B})\nGradient should NOT show[/]")
					.Build())

				// Case 4: Rule control → gradient shows through
				.Add(CaseLabel("4. Rule → [green]gradient preserved[/]"))
				.Add(Controls.RuleBuilder().WithColor(Color.Grey50).Build())

				// Case 5: ScrollablePanel with no bg → gradient in border
				.Add(CaseLabel("5. ScrollPanel, no bg → [green]gradient in border[/]"))
				.Add(Controls.ScrollablePanel()
					.WithBorderStyle(BorderStyle.Rounded)
					.WithBorderColor(Color.Green)
					.WithPadding(1, 0, 1, 0)
					.AddControl(Controls.Markup("[white]ScrollablePanel border\nshould show gradient[/]").Build())
					.Build())
			)
			// RIGHT: Nested container cases
			.Column(col => col
				.Add(SectionHeader("[bold yellow]Nested Containers[/]"))

				// Case 6: Grid with explicit bg → children use grid's bg
				.Add(CaseLabel("6. Grid with bg → [yellow]children inherit grid bg[/]"))
				.Add(BuildGridWithExplicitBg())

				// Case 7: ScrollPanel → HorizontalGrid → controls
				.Add(CaseLabel("7. ScrollPanel(no bg) → Grid → Markup"))
				.Add(BuildScrollPanelContainingGrid())

				// Case 8: HorizontalGrid → ScrollPanel → controls
				.Add(CaseLabel("8. Grid(no bg) → ScrollPanel → Markup"))
				.Add(BuildGridContainingScrollPanel())

				// Case 9: ScrollPanel with explicit bg → Grid inside
				.Add(CaseLabel("9. ScrollPanel(bg) → Grid → Markup"))
				.Add(BuildScrollPanelWithBgContainingGrid())

				// Case 10: Grid with bg → ScrollPanel with no bg
				.Add(CaseLabel("10. Grid(bg) → ScrollPanel(no bg)"))
				.Add(BuildGridWithBgContainingScrollPanel())
			)
			.Build();

		var footer = Controls.Markup(
				"[dim]Window has vertical gradient (50,60,120)→(10,10,25)[/]" +
				"  [dim grey50]|[/]  [dim]ESC to close[/]")
			.Centered()
			.StickyBottom()
			.Build();

		return new WindowBuilder(ws)
			.WithTitle("Container Background Demo")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.WithBackgroundGradient(gradient, GradientDirection.Vertical)
			.AddControls(mainGrid, footer)
			.OnKeyPressed((s, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)s!);
					e.Handled = true;
				}
			})
			.BuildAndShow();
	}

	private static MarkupControl SectionHeader(string text)
	{
		return Controls.Markup(text)
			.WithMargin(0, 0, 0, 0)
			.Build();
	}

	private static MarkupControl CaseLabel(string text)
	{
		return Controls.Markup($"[dim]{text}[/]")
			.WithMargin(1, 1, 0, 0)
			.Build();
	}

	// Case 6: HorizontalGrid with explicit bg
	private static HorizontalGridControl BuildGridWithExplicitBg()
	{
		var grid = Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithMargin(1, 0, 0, 0)
			.Column(col => col
				.Add(Controls.Markup($"[white]Grid bg=({ExplicitBg.R},{ExplicitBg.G},{ExplicitBg.B})[/]").Build())
				.Add(Controls.Markup("[white]Gradient should NOT show here[/]").Build())
			)
			.Build();
		grid.BackgroundColor = ExplicitBg;
		return grid;
	}

	// Case 7: ScrollablePanel (no bg) containing HorizontalGrid
	private static ScrollablePanelControl BuildScrollPanelContainingGrid()
	{
		return Controls.ScrollablePanel()
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBorderColor(Color.Cyan1)
			.WithPadding(1, 0, 1, 0)
			.WithMargin(1, 0, 0, 0)
			.AddControl(Controls.HorizontalGrid()
				.WithAlignment(HorizontalAlignment.Stretch)
				.Column(col => col
					.Add(Controls.Markup("[cyan]Left col[/]").Build())
				)
				.Column(col => col
					.Add(Controls.Markup("[cyan]Right col[/]").Build())
				)
				.Build())
			.Build();
	}

	// Case 8: HorizontalGrid (no bg) containing ScrollablePanel
	private static HorizontalGridControl BuildGridContainingScrollPanel()
	{
		return Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithMargin(1, 0, 0, 0)
			.Column(col => col
				.Add(Controls.ScrollablePanel()
					.WithBorderStyle(BorderStyle.Rounded)
					.WithBorderColor(Color.Green)
					.WithPadding(1, 0, 1, 0)
					.AddControl(Controls.Markup("[green]Panel inside grid\nGradient should show[/]").Build())
					.Build())
			)
			.Column(col => col
				.Add(Controls.Markup("[green]Sibling column\nGradient should show[/]")
					.WithMargin(1, 0, 0, 0).Build())
			)
			.Build();
	}

	// Case 9: ScrollablePanel with explicit bg containing HorizontalGrid
	private static ScrollablePanelControl BuildScrollPanelWithBgContainingGrid()
	{
		return Controls.ScrollablePanel()
			.WithBorderStyle(BorderStyle.Rounded)
			.WithBorderColor(Color.Yellow)
			.WithBackgroundColor(NestedBg)
			.WithPadding(1, 0, 1, 0)
			.WithMargin(1, 0, 0, 0)
			.AddControl(Controls.HorizontalGrid()
				.WithAlignment(HorizontalAlignment.Stretch)
				.Column(col => col
					.Add(Controls.Markup($"[yellow]Panel bg=({NestedBg.R},{NestedBg.G},{NestedBg.B})[/]").Build())
					.Add(Controls.Markup("[yellow]No gradient here[/]").Build())
				)
				.Build())
			.Build();
	}

	// Case 10: HorizontalGrid with explicit bg containing ScrollablePanel with no bg
	private static HorizontalGridControl BuildGridWithBgContainingScrollPanel()
	{
		var grid = Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithMargin(1, 0, 0, 0)
			.Column(col => col
				.Add(Controls.ScrollablePanel()
					.WithBorderStyle(BorderStyle.Rounded)
					.WithBorderColor(Color.Magenta1)
					.WithPadding(1, 0, 1, 0)
					.AddControl(Controls.Markup($"[magenta1]Grid bg=({ExplicitBg.R},{ExplicitBg.G},{ExplicitBg.B})[/]").Build())
					.AddControl(Controls.Markup("[magenta1]Panel has no bg\nShould use grid's bg[/]").Build())
					.Build())
			)
			.Build();
		grid.BackgroundColor = ExplicitBg;
		return grid;
	}
}
