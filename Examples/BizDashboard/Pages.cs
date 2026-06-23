// -----------------------------------------------------------------------
// BizDashboard — analytics page builders.
//
// Each returns a Fill+Stretch GridControl that becomes the direct root of a
// NavigationView page (via nav.SetItemContent). The Sales grid's live KPI
// controls are stored in BizRefs so the deterministic Simulation walk can
// touch them each tick.
//
// NOTE on track sizing: never use GridLength.Auto() for a track whose content
// might measure 0 (Markup/Dropdown/Checkbox collapse it to blank). Use
// GridLength.Cells(N) for fixed rows and GridLength.Star(1) for proportional.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace BizDashboard;

/// <summary>
/// Holds the live Sales-page controls the <see cref="Simulation"/> updates each tick.
/// </summary>
internal sealed class BizRefs
{
	// Sales KPI tiles (big number + delta line each).
	public MarkupControl RevenueTile { get; set; } = null!;
	public MarkupControl OrdersTile { get; set; } = null!;
	public MarkupControl ChurnTile { get; set; } = null!;

	// Sales trend chart.
	public LineGraphControl RevenueGraph { get; set; } = null!;
}

internal static class Pages
{
	private static readonly Color BgSlate = new(28, 32, 48);
	private static readonly Color TileBg = new(34, 40, 60);

	/// <summary>Splits a '\n'-joined markup block into per-row markup lines.</summary>
	private static string[] SplitLines(string s) => s.Split('\n');

	private static GridBuilder FillGrid() =>
		Controls.Grid()
			.WithColorRole(ColorRole.Primary)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch);

	#region Sales

	/// <summary>
	/// Three KPI tiles (Revenue / Orders / Churn) across the top, a wide
	/// revenue trend line graph below. KPIs + chart are driven live by the sim.
	/// </summary>
	public static GridControl BuildSalesGrid(BizRefs refs)
	{
		var grid = FillGrid()
			.Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Cells(8), GridLength.Star(1))
			.RowGap(1)
			.ColumnGap(2)
			.WithPadding(1, 1, 1, 1)
			.Build();

		// Seed each tile from a SPLIT line list (one markup line per row) so the
		// big-number value is populated and visible immediately on launch — and
		// so the build-time content matches exactly what the sim writes each tick
		// via SetContent(List<string>). A single '\n'-joined string would land as
		// one logical line and hide the value line.
		var revenue = Controls.Markup()
			.AddLines(SplitLines(FormatRevenue(184_200, +4.2)))
			.WithVerticalAlignment(VerticalAlignment.Top)
			.WithMargin(2, 0, 1, 0)
			.Build();
		var orders = Controls.Markup()
			.AddLines(SplitLines(FormatOrders(1_284, +2.8)))
			.WithVerticalAlignment(VerticalAlignment.Top)
			.WithMargin(2, 0, 1, 0)
			.Build();
		var churn = Controls.Markup()
			.AddLines(SplitLines(FormatChurn(2.4, -0.3)))
			.WithVerticalAlignment(VerticalAlignment.Top)
			.WithMargin(2, 0, 1, 0)
			.Build();

		grid.Place(revenue, 0, 0);
		grid.Place(orders, 0, 1);
		grid.Place(churn, 0, 2);
		grid.Cell(0, 0).Border = BorderStyle.Rounded;
		grid.Cell(0, 1).Border = BorderStyle.Rounded;
		grid.Cell(0, 2).Border = BorderStyle.Rounded;
		grid.Cell(0, 0).Background = TileBg;
		grid.Cell(0, 1).Background = TileBg;
		grid.Cell(0, 2).Background = TileBg;

		var graph = Controls.LineGraph()
			.WithTitle("Revenue (last 60 ticks)")
			.WithMode(LineGraphMode.Braille)
			.WithColorRole(ColorRole.Success)
			.WithMinValue(0)
			.WithMaxValue(100)
			.WithData(new double[] { 0 })
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 1)
			.Build();
		graph.MaxDataPoints = Simulation.MaxPoints;

		grid.Place(graph, 1, 0, colSpan: 3);
		grid.Cell(1, 0).Border = BorderStyle.Rounded;
		grid.Cell(1, 0).Background = BgSlate;

		refs.RevenueTile = revenue;
		refs.OrdersTile = orders;
		refs.ChurnTile = churn;
		refs.RevenueGraph = graph;
		return grid;
	}

	internal static string FormatRevenue(int dollars, double deltaPct)
	{
		string arrow = deltaPct >= 0 ? "▲" : "▼";
		string clr = deltaPct >= 0 ? "rgb(120,220,160)" : "rgb(230,120,110)";
		return string.Join('\n', new[]
		{
			"[dim]REVENUE (MTD)[/]",
			$"[bold rgb(120,220,160)]  ${dollars:N0}[/]",
			$"[{clr}]{arrow} {Math.Abs(deltaPct):0.0}% vs last month[/]",
		});
	}

	internal static string FormatOrders(int count, double deltaPct)
	{
		string arrow = deltaPct >= 0 ? "▲" : "▼";
		string clr = deltaPct >= 0 ? "rgb(120,220,160)" : "rgb(230,120,110)";
		return string.Join('\n', new[]
		{
			"[dim]ORDERS (MTD)[/]",
			$"[bold rgb(120,190,255)]  {count:N0}[/]",
			$"[{clr}]{arrow} {Math.Abs(deltaPct):0.0}% vs last month[/]",
		});
	}

	internal static string FormatChurn(double pct, double deltaPct)
	{
		// For churn, DOWN is good — invert the color logic.
		string arrow = deltaPct >= 0 ? "▲" : "▼";
		string clr = deltaPct <= 0 ? "rgb(120,220,160)" : "rgb(230,120,110)";
		return string.Join('\n', new[]
		{
			"[dim]CHURN (30d)[/]",
			$"[bold rgb(230,180,90)]  {pct:0.0}%[/]",
			$"[{clr}]{arrow} {Math.Abs(deltaPct):0.0} pts vs last month[/]",
		});
	}

	#endregion

	#region Customers

	/// <summary>
	/// Left: a customer table (Name / Plan / MRR). Right: a detail markup that
	/// updates as the selected row changes via SelectedRowChanged.
	/// </summary>
	public static GridControl BuildCustomersGrid()
	{
		var grid = FillGrid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.ColumnGap(2)
			.ColumnSplitterAfter(0)
			.WithPadding(1, 1, 1, 1)
			.Build();

		var table = Controls.Table()
			.WithColorRole(ColorRole.Primary)
			.AddColumn("Customer")
			.AddColumn("Plan")
			.AddColumn("MRR", TextJustification.Right)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 1)
			.Build();

		(string name, string plan, string mrr, string detail)[] seed =
		{
			("Acme Corp", "Enterprise", "$4,200", "12 seats · renews Sep · CSM: Dana"),
			("Globex", "Business", "$1,150", "5 seats · renews Jul · trial→paid"),
			("Initech", "Business", "$980", "4 seats · at-risk · low usage"),
			("Umbrella", "Enterprise", "$6,400", "30 seats · expanding · upsell ready"),
			("Hooli", "Starter", "$290", "2 seats · self-serve · healthy"),
			("Stark Ind.", "Enterprise", "$8,900", "44 seats · advocate · case study"),
			("Wayne Ent.", "Business", "$1,320", "6 seats · renews Aug · stable"),
			("Soylent", "Starter", "$190", "1 seat · churned trial last Q"),
		};

		string[] details = new string[seed.Length];
		for (int i = 0; i < seed.Length; i++)
		{
			table.AddRow(seed[i].name, seed[i].plan, seed[i].mrr);
			details[i] = seed[i].detail;
		}

		var detail = Controls.Markup(BuildCustomerDetail(seed[0].name, seed[0].plan, seed[0].mrr, details[0]))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(2, 1, 2, 1)
			.Build();

		table.SelectedRowChanged += (s, idx) =>
		{
			if (idx >= 0 && idx < seed.Length)
			{
				var c = seed[idx];
				detail.SetContent(BuildCustomerDetailLines(c.name, c.plan, c.mrr, details[idx]));
			}
		};

		grid.Place(table, 0, 0);
		grid.Place(detail, 0, 1);
		grid.Cell(0, 0).Border = BorderStyle.Rounded;
		grid.Cell(0, 1).Border = BorderStyle.Rounded;
		grid.Cell(0, 0).Background = BgSlate;
		grid.Cell(0, 1).Background = BgSlate;
		return grid;
	}

	private static List<string> BuildCustomerDetailLines(string name, string plan, string mrr, string detail) =>
		new()
		{
			$"[bold rgb(120,200,255)]{name}[/]",
			"",
			$"[dim]Plan[/]   {plan}",
			$"[dim]MRR [/]   [bold rgb(120,220,160)]{mrr}[/]",
			"",
			$"[dim]{detail}[/]",
		};

	private static string BuildCustomerDetail(string name, string plan, string mrr, string detail) =>
		string.Join('\n', BuildCustomerDetailLines(name, plan, mrr, detail));

	#endregion

	#region Inventory

	/// <summary>A single Fill table with status colored via inline markup.</summary>
	public static GridControl BuildInventoryGrid()
	{
		var grid = FillGrid()
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.WithPadding(1, 1, 1, 1)
			.Build();

		var table = Controls.Table()
			.WithColorRole(ColorRole.Primary)
			.AddColumn("SKU")
			.AddColumn("Item")
			.AddColumn("Stock", TextJustification.Right)
			.AddColumn("Status")
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 1)
			.Build();

		(string sku, string item, int stock)[] seed =
		{
			("CFE-001", "House Blend Coffee", 142),
			("TEA-014", "Green Tea", 6),
			("BAK-220", "Blueberry Muffin", 0),
			("BAK-118", "Sesame Bagel", 38),
			("JUI-007", "Orange Juice", 21),
			("BAK-330", "Choc Chip Cookie", 4),
			("CFE-009", "Decaf Beans", 75),
			("SYR-002", "Vanilla Syrup", 0),
			("CUP-016", "12oz Paper Cups", 410),
			("LID-016", "12oz Lids", 12),
		};

		foreach (var (sku, item, stock) in seed)
			table.AddRow(sku, item, stock.ToString(), StatusMarkup(stock));

		grid.Place(table, 0, 0);
		grid.Cell(0, 0).Border = BorderStyle.Rounded;
		grid.Cell(0, 0).Background = BgSlate;
		return grid;
	}

	private static string StatusMarkup(int stock) => stock switch
	{
		0 => "[red]Out[/]",
		<= 10 => "[yellow]Low[/]",
		_ => "[green]In stock[/]",
	};

	#endregion

	#region Reports

	/// <summary>A single Fill markdown pane — an executive business summary.</summary>
	public static GridControl BuildReportsGrid()
	{
		var grid = FillGrid()
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Star(1))
			.WithPadding(1, 1, 1, 1)
			.Build();

		string md = "[markdown]" + string.Join('\n', new[]
		{
			"# Q2 Executive Summary",
			"",
			"Revenue is **up 4.2%** month-over-month, driven by enterprise expansion",
			"and a healthy trial-to-paid conversion rate.",
			"",
			"## Highlights",
			"",
			"- **Revenue (MTD):** $184,200",
			"- **New logos:** 14 (best quarter on record)",
			"- **Churn (30d):** 2.4% — *down* 0.3 pts",
			"- **NRR:** 118%",
			"",
			"## Watch items",
			"",
			"- *Initech* flagged at-risk — low usage, renews this month",
			"- Two SKUs out of stock (Blueberry Muffin, Vanilla Syrup)",
			"- 12oz Lids running low — reorder before weekend",
			"",
			"## Top accounts by MRR",
			"",
			"1. Stark Industries — $8,900",
			"2. Umbrella — $6,400",
			"3. Acme Corp — $4,200",
			"",
			"> Net: a strong quarter. Prioritize the Initech save and clear the",
			"> two stockouts before the weekend rush.",
		}) + "[/]";

		var report = Controls.Markup(md)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(2, 1, 2, 1)
			.Build();

		grid.Place(report, 0, 0);
		grid.Cell(0, 0).Border = BorderStyle.Rounded;
		grid.Cell(0, 0).Background = BgSlate;
		return grid;
	}

	#endregion
}
