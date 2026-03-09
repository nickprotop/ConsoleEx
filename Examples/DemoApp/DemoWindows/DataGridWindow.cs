using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

/// <summary>
/// ITableDataSource implementation for the DataGrid demo.
/// Simulates a product inventory database with 10,000 rows.
/// </summary>
internal class ProductDataSource : ITableDataSource
{
    private readonly List<ProductRecord> _records = new();
    private int[]? _sortIndexMap;
    private int _sortColumn = -1;
    private SortDirection _sortDirection = SortDirection.None;

    public event EventHandler? DataChanged;

    private static readonly string[] Categories = { "Electronics", "Clothing", "Food", "Books", "Tools", "Sports", "Home", "Garden", "Auto", "Toys" };
    private static readonly string[] Statuses = { "[green]In Stock[/]", "[yellow]Low Stock[/]", "[red]Out of Stock[/]", "[cyan]On Order[/]" };
    private static readonly string[] Warehouses = { "NYC", "LAX", "CHI", "HOU", "PHX", "DFW", "SEA", "MIA", "DEN", "ATL" };

    public ProductDataSource(int count = 10000)
    {
        var rng = new Random(42); // Fixed seed for reproducibility
        for (int i = 0; i < count; i++)
        {
            _records.Add(new ProductRecord
            {
                Id = i + 1,
                Name = $"Product {i + 1:D5}",
                Category = Categories[rng.Next(Categories.Length)],
                Price = Math.Round(rng.NextDouble() * 999.99 + 0.01, 2),
                Quantity = rng.Next(0, 5000),
                Status = Statuses[rng.Next(Statuses.Length)],
                Warehouse = Warehouses[rng.Next(Warehouses.Length)],
                Rating = Math.Round(rng.NextDouble() * 4 + 1, 1),
            });
        }
    }

    public int RowCount => _records.Count;
    public int ColumnCount => 8;

    private static readonly string[] Headers = { "ID", "Product Name", "Category", "Price", "Qty", "Status", "Warehouse", "Rating" };
    private static readonly TextJustification[] Alignments =
    {
        TextJustification.Right, TextJustification.Left, TextJustification.Left,
        TextJustification.Right, TextJustification.Right, TextJustification.Center,
        TextJustification.Center, TextJustification.Right
    };

    public string GetColumnHeader(int columnIndex) => Headers[columnIndex];

    public TextJustification GetColumnAlignment(int columnIndex) => Alignments[columnIndex];

    public int? GetColumnWidth(int columnIndex) => columnIndex switch
    {
        0 => 7,   // ID
        1 => null, // Name - auto
        3 => 10,   // Price
        4 => 6,    // Qty
        6 => 10,   // Warehouse
        7 => 8,    // Rating
        _ => null
    };

    public string GetCellValue(int rowIndex, int columnIndex)
    {
        int dataIndex = _sortIndexMap != null ? _sortIndexMap[rowIndex] : rowIndex;
        var r = _records[dataIndex];
        return columnIndex switch
        {
            0 => r.Id.ToString(),
            1 => r.Name,
            2 => r.Category,
            3 => $"${r.Price:F2}",
            4 => r.Quantity.ToString(),
            5 => r.Status,
            6 => r.Warehouse,
            7 => $"{r.Rating:F1} \u2605",
            _ => ""
        };
    }

    public Color? GetRowForegroundColor(int rowIndex)
    {
        int dataIndex = _sortIndexMap != null ? _sortIndexMap[rowIndex] : rowIndex;
        var r = _records[dataIndex];
        if (r.Quantity == 0) return Color.Grey;
        return null;
    }

    public bool IsRowEnabled(int rowIndex) => true;

    public bool CanSort(int columnIndex) => true;

    public void Sort(int columnIndex, SortDirection direction)
    {
        _sortColumn = columnIndex;
        _sortDirection = direction;

        if (direction == SortDirection.None)
        {
            _sortIndexMap = null;
            return;
        }

        var indices = Enumerable.Range(0, _records.Count).ToArray();
        Array.Sort(indices, (a, b) =>
        {
            int result = columnIndex switch
            {
                0 => _records[a].Id.CompareTo(_records[b].Id),
                1 => string.Compare(_records[a].Name, _records[b].Name, StringComparison.Ordinal),
                2 => string.Compare(_records[a].Category, _records[b].Category, StringComparison.Ordinal),
                3 => _records[a].Price.CompareTo(_records[b].Price),
                4 => _records[a].Quantity.CompareTo(_records[b].Quantity),
                5 => string.Compare(_records[a].Status, _records[b].Status, StringComparison.Ordinal),
                6 => string.Compare(_records[a].Warehouse, _records[b].Warehouse, StringComparison.Ordinal),
                7 => _records[a].Rating.CompareTo(_records[b].Rating),
                _ => 0
            };
            return direction == SortDirection.Descending ? -result : result;
        });
        _sortIndexMap = indices;
    }

    public void UpdateRecord(int displayIndex, int columnIndex, string newValue)
    {
        int dataIndex = _sortIndexMap != null ? _sortIndexMap[displayIndex] : displayIndex;
        var r = _records[dataIndex];
        switch (columnIndex)
        {
            case 1: r.Name = newValue; break;
            case 2: r.Category = newValue; break;
            case 3: if (double.TryParse(newValue.TrimStart('$'), out double p)) r.Price = p; break;
            case 4: if (int.TryParse(newValue, out int q)) r.Quantity = q; break;
            case 6: r.Warehouse = newValue; break;
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddRecord()
    {
        var rng = new Random();
        int nextId = _records.Max(r => r.Id) + 1;
        _records.Add(new ProductRecord
        {
            Id = nextId,
            Name = $"New Product {nextId:D5}",
            Category = Categories[rng.Next(Categories.Length)],
            Price = Math.Round(rng.NextDouble() * 999.99 + 0.01, 2),
            Quantity = rng.Next(0, 5000),
            Status = Statuses[rng.Next(Statuses.Length)],
            Warehouse = Warehouses[rng.Next(Warehouses.Length)],
            Rating = Math.Round(rng.NextDouble() * 4 + 1, 1),
        });
        // Re-apply sort if active
        if (_sortDirection != SortDirection.None)
            Sort(_sortColumn, _sortDirection);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteRecord(int displayIndex)
    {
        if (displayIndex < 0 || displayIndex >= _records.Count) return;
        int dataIndex = _sortIndexMap != null ? _sortIndexMap[displayIndex] : displayIndex;
        _records.RemoveAt(dataIndex);
        // Rebuild sort index map
        if (_sortDirection != SortDirection.None)
            Sort(_sortColumn, _sortDirection);
        else
            _sortIndexMap = null;
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private class ProductRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = "";
        public string Warehouse { get; set; } = "";
        public double Rating { get; set; }
    }
}

public static class DataGridWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var dataSource = new ProductDataSource(10000);

        // --- Left panel: Interactive DataGrid with ITableDataSource ---
        var dataGrid = Controls.Table()
            .WithTitle("Product Inventory (10,000 rows)")
            .WithDataSource(dataSource)
            .Interactive()
            .WithCellNavigation()
            .WithSorting()
            .WithColumnResize()
            .WithInlineEditing()
            .WithHeaderColors(Color.White, Color.DarkBlue)
            .Rounded()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
            .WithName("dataGrid")
            .OnCellEditCompleted((sender, e) =>
            {
                // Write the edit back to the data source
                dataSource.UpdateRecord(e.Row, e.Column, e.NewValue);
            })
            .Build();

        // --- Right panel: Info + static table ---
        var infoMarkup = Controls.Markup()
            .AddLines(
                "[bold cyan]DataGrid Features[/]",
                "",
                "[dim]Navigation:[/]",
                "  [yellow]\u2191\u2193[/] Row navigation",
                "  [yellow]Tab/Shift+Tab[/] Cell navigation",
                "  [yellow]PgUp/PgDn[/] Page scroll",
                "  [yellow]Home/End[/] First/Last row",
                "",
                "[dim]Interaction:[/]",
                "  [yellow]Click header[/] Sort column",
                "  [yellow]F2/Enter/DblClick[/] Edit cell",
                "  [yellow]Enter[/] Commit, [yellow]Esc[/] Cancel",
                "  [yellow]Esc[/] Deselect cell (row mode)",
                "  [yellow]Drag border[/] Resize column",
                "",
                "[dim]Scrolling:[/]",
                "  [yellow]Mouse wheel[/] Vertical scroll",
                "  [yellow]Shift+wheel[/] Horizontal scroll",
                "  [yellow]Drag scrollbar[/] Smooth scroll",
                "",
                "[dim]Data Source:[/]",
                "  [green]10,000 rows[/] virtual rendering",
                "  Only visible rows are queried",
                "  Sort delegates to data source"
            )
            .WithMargin(1, 0, 1, 0)
            .Build();

        // Small static table showing features checklist
        var featureTable = Controls.Table()
            .WithTitle("Feature Checklist")
            .AddColumn("Feature")
            .AddColumn("Status")
            .AddRow("ITableDataSource", "[green]\u2713 Virtual[/]")
            .AddRow("Keyboard Nav", "[green]\u2713 Enabled[/]")
            .AddRow("Cell Navigation", "[green]\u2713 Tab/Arrow[/]")
            .AddRow("Column Sorting", "[green]\u2713 Click Header[/]")
            .AddRow("Inline Editing", "[green]\u2713 F2 to Edit[/]")
            .AddRow("Column Resize", "[green]\u2713 Drag Border[/]")
            .AddRow("Scrollbar Drag", "[green]\u2713 Smooth[/]")
            .AddRow("Markup Support", "[green]\u2713 Rich Text[/]")
            .AddRow("ReadOnly Mode", "[green]\u2713 Configurable[/]")
            .DoubleLine()
            .WithHeaderColors(Color.White, Color.DarkGreen)
            .WithMargin(1, 1, 1, 0)
            .Build();

        // Status bar
        var statusBar = Controls.Markup("[dim]Esc: Close | \u2191\u2193: Navigate | Tab: Cell Nav | F2: Edit | Click Header: Sort | Drag Scrollbar[/]")
            .StickyBottom()
            .Build();

        // Layout: left = data grid, right = info panel
        var grid = Controls.HorizontalGrid()
            .Column(col => col.Add(dataGrid))
            .Column(col => col.Width(38).Add(infoMarkup).Add(featureTable))
            .WithSplitterAfter(0)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        // Wire up selection event to update status
        dataGrid.SelectedRowChanged += (sender, rowIdx) =>
        {
            if (rowIdx >= 0)
            {
                string name = dataSource.GetCellValue(rowIdx, 1);
                string price = dataSource.GetCellValue(rowIdx, 3);
                statusBar.SetContent(new List<string> { $"[dim]Row {rowIdx + 1}: {name} ({price}) | Esc: Close | F2: Edit | Click Header: Sort[/]" });
            }
        };

        var gradient = ColorGradient.FromColors(
            new Color(10, 10, 50),
            new Color(0, 50, 80),
            new Color(40, 10, 60));

        return new WindowBuilder(ws)
            .WithTitle("Interactive DataGrid Demo")
            .WithSize(130, 35)
            .Centered()
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
            .AddControls(grid, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    // Don't close if table just handled Escape (cancel edit / deselect cell)
                    if (e.AlreadyHandled)
                        return;
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }
}
