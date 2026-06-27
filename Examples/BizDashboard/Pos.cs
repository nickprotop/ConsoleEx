// -----------------------------------------------------------------------
// BizDashboard — Point of Sale (the interactive flagship page).
//
// Three columns:
//   col 0  CATALOG — a nested grid of product buttons. Click → AddToCart.
//   col 1  CART    — a table (Item / Qty / Price / Total) that grows + totals.
//   col 2  TOTALS + KEYPAD — totals markup (Subtotal / Tax / TOTAL) plus a
//          numeric keypad that builds a tender string; CASH shows change due,
//          CARD marks paid, C clears the tender.
//
// All button handlers fire on the UI thread, so cart/table/markup are mutated
// directly. No background work here, no Random — interaction-driven.
//
// Track sizing: fixed rows use GridLength.Cells(N); proportional uses Star(1).
// Auto() is avoided for anything whose content can measure 0.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace BizDashboard;

internal static class Pos
{
	private const double TaxRate = 0.08;

	private static readonly Color BgSlate = new(28, 32, 48);
	private static readonly Color PanelBg = new(34, 40, 60);

	// ── POS state (single-window app → simple static state) ────────────────
	private sealed class CartLine
	{
		public string Name = "";
		public double Price;
		public int Qty;
		public double Total => Price * Qty;
	}

	private static readonly List<CartLine> _cart = new();
	private static readonly System.Text.StringBuilder _tender = new();

	// Live controls the handlers mutate.
	private static TableControl _cartTable = null!;
	private static MarkupControl _totals = null!;

	public static GridControl BuildPosGrid()
	{
		// Reset state each time the page is (re)built.
		_cart.Clear();
		_tender.Clear();

		var grid = Controls.Grid()
			.WithColorRole(ColorRole.Primary)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Cells(26))
			.Rows(GridLength.Star(1))
			.ColumnGap(2)
			.WithPadding(1, 1, 1, 1)
			.Build();

		grid.Place(BuildCatalog(), 0, 0);
		grid.Place(BuildCart(), 0, 1);
		grid.Place(BuildTotalsKeypad(), 0, 2);

		grid.Cell(0, 0).Border = BorderStyle.Rounded;
		grid.Cell(0, 1).Border = BorderStyle.Rounded;
		grid.Cell(0, 2).Border = BorderStyle.Rounded;
		grid.Cell(0, 0).Background = BgSlate;
		grid.Cell(0, 1).Background = BgSlate;
		grid.Cell(0, 2).Background = BgSlate;

		RefreshTotals();
		return grid;
	}

	#region Catalog (col 0)

	private static readonly (string name, double price)[] _products =
	{
		("Coffee", 3.50),
		("Tea", 2.75),
		("Muffin", 4.00),
		("Bagel", 3.25),
		("Juice", 3.00),
		("Cookie", 2.00),
		("Latte", 4.50),
		("Espresso", 2.50),
	};

	private static GridControl BuildCatalog()
	{
		// 4 rows x 2 cols of product tiles + a header row.
		var grid = Controls.Grid()
			.WithColorRole(ColorRole.Primary)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(
				GridLength.Cells(2),
				GridLength.Star(1), GridLength.Star(1),
				GridLength.Star(1), GridLength.Star(1))
			.RowGap(1)
			.ColumnGap(1)
			.WithPadding(1, 1, 1, 1)
			.Build();

		grid.Place(Controls.Markup("[bold rgb(120,200,255)]CATALOG[/]  [dim]· tap to add[/]")
			.WithMargin(1, 0, 1, 0).Build(), 0, 0, colSpan: 2);

		for (int i = 0; i < _products.Length; i++)
		{
			var (name, price) = _products[i];
			int row = 1 + (i / 2);
			int col = i % 2;

			// ButtonControl renders a single line; an embedded '\n' would show as
			// U+FFFD and truncate, so use a single-line "Name  $Price" label.
			var btn = Controls.Button($"{name}  ${price:0.00}")
				.WithColorRole(ColorRole.Primary)
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.WithAlignment(HorizontalAlignment.Stretch)
				.OnClick((s, b) => AddToCart(name, price))
				.Build();

			grid.Place(btn, row, col);
		}

		return grid;
	}

	#endregion

	#region Cart (col 1)

	private static GridControl BuildCart()
	{
		var grid = Controls.Grid()
			.WithColorRole(ColorRole.Primary)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Cells(2), GridLength.Star(1))
			.RowGap(1)
			.WithPadding(1, 1, 1, 1)
			.Build();

		grid.Place(Controls.Markup("[bold rgb(180,220,140)]CART[/]")
			.WithMargin(1, 0, 1, 0).Build(), 0, 0);

		_cartTable = Controls.Table()
			.WithColorRole(ColorRole.Primary)
			.AddColumn("Item")
			.AddColumn("Qty", TextJustification.Right)
			.AddColumn("Price", TextJustification.Right)
			.AddColumn("Total", TextJustification.Right)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.StretchHorizontal() // fill the cell width (distributes slack across auto columns)
			.WithMargin(1, 0, 1, 0)
			.Build();

		grid.Place(_cartTable, 1, 0);
		return grid;
	}

	private static void AddToCart(string name, double price)
	{
		var line = _cart.Find(l => l.Name == name);
		if (line is null)
		{
			line = new CartLine { Name = name, Price = price, Qty = 1 };
			_cart.Add(line);
			_cartTable.AddRow(name, "1", $"${price:0.00}", $"${price:0.00}");
		}
		else
		{
			line.Qty++;
			int row = _cart.IndexOf(line);
			_cartTable.UpdateCell(row, 1, line.Qty.ToString());
			_cartTable.UpdateCell(row, 3, $"${line.Total:0.00}");
		}

		// A new sale invalidates any pending tender.
		_tender.Clear();
		RefreshTotals();
	}

	#endregion

	#region Totals + Keypad (col 2)

	private static GridControl BuildTotalsKeypad()
	{
		var grid = Controls.Grid()
			.WithColorRole(ColorRole.Primary)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Star(1), GridLength.Cells(14))
			.RowGap(1)
			.WithPadding(1, 1, 1, 1)
			.Build();

		_totals = Controls.Markup("")
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 0)
			.Build();

		grid.Place(_totals, 0, 0);
		grid.Place(BuildKeypad(), 1, 0);
		return grid;
	}

	private static GridControl BuildKeypad()
	{
		var grid = Controls.Grid()
			.WithColorRole(ColorRole.Primary)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Columns(GridLength.Star(1), GridLength.Star(1), GridLength.Star(1))
			.Rows(
				GridLength.Star(1), GridLength.Star(1),
				GridLength.Star(1), GridLength.Star(1),
				GridLength.Cells(3))
			.RowGap(1)
			.ColumnGap(1)
			.Build();

		string[,] pad =
		{
			{ "7", "8", "9" },
			{ "4", "5", "6" },
			{ "1", "2", "3" },
			{ "0", ".", "C" },
		};

		for (int r = 0; r < 4; r++)
		{
			for (int c = 0; c < 3; c++)
			{
				string key = pad[r, c];
				var role = key == "C" ? ColorRole.Warning : ColorRole.Secondary;
				var btn = Controls.Button(key)
					.WithColorRole(role)
					.WithVerticalAlignment(VerticalAlignment.Fill)
					.WithAlignment(HorizontalAlignment.Stretch)
					.OnClick((s, b) => OnKey(key))
					.Build();
				grid.Place(btn, r, c);
			}
		}

		var cash = Controls.Button("CASH")
			.WithColorRole(ColorRole.Success)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.OnClick((s, b) => Pay(cash: true))
			.Build();
		var card = Controls.Button("CARD")
			.WithColorRole(ColorRole.Info)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.OnClick((s, b) => Pay(cash: false))
			.Build();

		grid.Place(cash, 4, 0);
		grid.Place(card, 4, 1, colSpan: 2);
		return grid;
	}

	private static void OnKey(string key)
	{
		if (key == "C")
		{
			_tender.Clear();
		}
		else if (key == ".")
		{
			if (!_tender.ToString().Contains('.'))
				_tender.Append('.');
		}
		else
		{
			_tender.Append(key);
		}

		RefreshTotals();
	}

	private static (double subtotal, double tax, double total) Compute()
	{
		double subtotal = 0;
		foreach (var l in _cart) subtotal += l.Total;
		double tax = subtotal * TaxRate;
		return (subtotal, tax, subtotal + tax);
	}

	private static void Pay(bool cash)
	{
		var (subtotal, tax, total) = Compute();

		if (_cart.Count == 0)
		{
			_totals.SetContent(BuildTotalsLines(subtotal, tax, total,
				"[yellow]Cart is empty[/]"));
			return;
		}

		string status;
		if (cash)
		{
			double tendered = ParseTender();
			if (tendered < total)
			{
				status = $"[yellow]Tender ${tendered:0.00} < total[/]";
				_totals.SetContent(BuildTotalsLines(subtotal, tax, total, status));
				return;
			}
			double change = tendered - total;
			status = $"[bold rgb(120,220,160)]PAID — change ${change:0.00}[/]";
		}
		else
		{
			status = "[bold rgb(120,200,255)]PAID — card approved[/]";
		}

		// Complete the sale: clear cart + tender, show the receipt line.
		_cart.Clear();
		_cartTable.ClearRows();
		_tender.Clear();
		_totals.SetContent(BuildTotalsLines(0, 0, 0, status));
	}

	private static double ParseTender()
	{
		string s = _tender.ToString();
		return double.TryParse(s, System.Globalization.NumberStyles.Any,
			System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0;
	}

	private static void RefreshTotals()
	{
		var (subtotal, tax, total) = Compute();
		string tenderText = _tender.Length > 0
			? $"[dim]Tender[/]   [rgb(200,220,255)]${ParseTender():0.00}[/]"
			: "[dim]Tender[/]   [dim]—[/]";
		_totals.SetContent(BuildTotalsLines(subtotal, tax, total, tenderText));
	}

	private static List<string> BuildTotalsLines(double subtotal, double tax, double total, string footer) =>
		new()
		{
			"[bold rgb(120,200,255)]TOTALS[/]",
			"",
			$"[dim]Subtotal[/] [rgb(220,220,230)]${subtotal,8:0.00}[/]",
			$"[dim]Tax 8%  [/] [rgb(220,220,230)]${tax,8:0.00}[/]",
			$"[bold rgb(120,220,160)]TOTAL[/]    [bold rgb(120,220,160)]${total,8:0.00}[/]",
			"",
			footer,
		};

	#endregion
}
