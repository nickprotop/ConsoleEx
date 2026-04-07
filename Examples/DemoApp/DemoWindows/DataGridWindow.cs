using System.Collections.Specialized;
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
    private int[]? _filterIndexMap;
    private int _sortColumn = -1;
    private SortDirection _sortDirection = SortDirection.None;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

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

    public int RowCount => _filterIndexMap?.Length ?? _records.Count;
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

    private int MapToDataIndex(int rowIndex)
    {
        int idx = rowIndex;
        if (_filterIndexMap != null)
            idx = _filterIndexMap[idx];
        if (_sortIndexMap != null && _filterIndexMap == null)
            idx = _sortIndexMap[idx];
        return idx;
    }

    public string GetCellValue(int rowIndex, int columnIndex)
    {
        int dataIndex = MapToDataIndex(rowIndex);
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
        int dataIndex = MapToDataIndex(rowIndex);
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
            // Re-apply filter if active
            if (_filterIndexMap != null)
                RecomputeFilterWithSort();
            return;
        }

        if (_filterIndexMap != null)
        {
            // Sort within filtered set
            RecomputeFilterWithSort();
        }
        else
        {
            var indices = Enumerable.Range(0, _records.Count).ToArray();
            SortIndicesArray(indices);
            _sortIndexMap = indices;
        }
    }

    private void SortIndicesArray(int[] indices)
    {
        int columnIndex = _sortColumn;
        var direction = _sortDirection;
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
    }

    public bool CanFilter => false; // Let the control handle filtering internally

    public void ApplyFilter(string filterText, string? columnName, FilterOperator op) { }

    public void ClearFilter() { }

    private void RecomputeFilterWithSort()
    {
        // _filterIndexMap contains data indices; re-sort them
        if (_filterIndexMap != null && _sortDirection != SortDirection.None)
        {
            SortIndicesArray(_filterIndexMap);
        }
    }

    public void UpdateRecord(int displayIndex, int columnIndex, string newValue)
    {
        int dataIndex = MapToDataIndex(displayIndex);
        var r = _records[dataIndex];
        switch (columnIndex)
        {
            case 1: r.Name = newValue; break;
            case 2: r.Category = newValue; break;
            case 3: if (double.TryParse(newValue.TrimStart('$'), out double p)) r.Price = p; break;
            case 4: if (int.TryParse(newValue, out int q)) r.Quantity = q; break;
            case 6: r.Warehouse = newValue; break;
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newValue, newValue, displayIndex));
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
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void DeleteRecord(int displayIndex)
    {
        if (displayIndex < 0 || displayIndex >= RowCount) return;
        int dataIndex = MapToDataIndex(displayIndex);
        _records.RemoveAt(dataIndex);
        // Rebuild sort index map
        if (_sortDirection != SortDirection.None)
            Sort(_sortColumn, _sortDirection);
        else
        {
            _sortIndexMap = null;
            _filterIndexMap = null;
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void DeleteRecords(List<int> displayIndices)
    {
        // Convert display indices to data indices, sort descending to remove from end first
        var dataIndices = displayIndices
            .Where(i => i >= 0 && i < RowCount)
            .Select(MapToDataIndex)
            .Distinct()
            .OrderByDescending(i => i)
            .ToList();

        foreach (var idx in dataIndices)
            _records.RemoveAt(idx);

        if (_sortDirection != SortDirection.None)
            Sort(_sortColumn, _sortDirection);
        else
        {
            _sortIndexMap = null;
            _filterIndexMap = null;
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public bool MoveRecord(int displayIndex, int direction)
    {
        int targetIndex = displayIndex + direction;
        if (displayIndex < 0 || displayIndex >= RowCount) return false;
        if (targetIndex < 0 || targetIndex >= RowCount) return false;

        int dataA = MapToDataIndex(displayIndex);
        int dataB = MapToDataIndex(targetIndex);

        // Swap in underlying list
        (_records[dataA], _records[dataB]) = (_records[dataB], _records[dataA]);

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        return true;
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

        // --- Delete button (created early so we can update its text) ---
        var deleteButton = Controls.Button()
            .WithText("\u2715 Delete")
            .WithForegroundColor(Color.IndianRed)
            .OnClick((sender, e) => { }) // Wired below after dataGrid exists
            .Build();

        // --- Left panel: Interactive DataGrid with ITableDataSource ---
        var dataGrid = Controls.Table()
            .WithTitle("Product Inventory (10,000 rows)")
            .WithDataSource(dataSource)
            .Interactive()
            .WithCellNavigation()
            .WithSorting()
            .WithFiltering()
            .WithColumnResize()
            .WithInlineEditing()
            .WithMultiSelect()
            .WithCheckboxMode()
            .WithHeaderColors(Color.White, Color.DarkBlue)
            .Rounded()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
            .WithName("dataGrid")
            .OnCellEditCompleted((sender, e) =>
            {
                dataSource.UpdateRecord(e.Row, e.Column, e.NewValue);
            })
            .Build();

        // Update delete button text and flash row when checkbox toggled
        dataGrid.MultiSelectionChanged += (sender, count) =>
        {
            deleteButton.Text = count > 0 ? $"\u2715 Delete ({count})" : "\u2715 Delete";
            int row = dataGrid.SelectedRowIndex;
            if (row >= 0 && dataGrid.IsRowSelected(row))
                dataGrid.FlashRow(row, Color.CornflowerBlue, TimeSpan.FromMilliseconds(250));
        };

        // --- Toolbar ---
        var toolbar = Controls.Toolbar()
            .AddButton(Controls.Button()
                .WithText("\uff0b Add")
                .OnClick((sender, e) =>
                {
                    dataSource.AddRecord();
                    int newRow = dataSource.RowCount - 1;
                    dataGrid.SelectedRowIndex = newRow;
                    dataGrid.HighlightRow(newRow, Color.Green, TimeSpan.FromMilliseconds(600));
                }))
            .AddButton(deleteButton)
            .AddButton(Controls.Button()
                .WithText("\u25b2 Move Up")
                .OnClick((sender, e) =>
                {
                    int idx = dataGrid.SelectedRowIndex;
                    if (idx <= 0) return;
                    if (dataSource.MoveRecord(idx, -1))
                    {
                        dataGrid.SelectedRowIndex = idx - 1;
                        dataGrid.FlashRow(idx - 1, Color.Cyan, TimeSpan.FromMilliseconds(300));
                        dataGrid.FlashRow(idx, Color.Cyan, TimeSpan.FromMilliseconds(300));
                    }
                }))
            .AddButton(Controls.Button()
                .WithText("\u25bc Move Down")
                .OnClick((sender, e) =>
                {
                    int idx = dataGrid.SelectedRowIndex;
                    if (idx < 0 || idx >= dataSource.RowCount - 1) return;
                    if (dataSource.MoveRecord(idx, 1))
                    {
                        dataGrid.SelectedRowIndex = idx + 1;
                        dataGrid.FlashRow(idx, Color.Cyan, TimeSpan.FromMilliseconds(300));
                        dataGrid.FlashRow(idx + 1, Color.Cyan, TimeSpan.FromMilliseconds(300));
                    }
                }))
            .WithSpacing(1)
            .StickyTop()
            .WithBelowLine()
            .Build();

        // Wire delete button click (needs dataGrid reference)
        deleteButton.Click += (sender, e) =>
        {
            var selectedIndices = dataGrid.GetSelectedIndices();
            if (selectedIndices.Count > 1)
            {
                // Bulk delete: flash red, then remove from data source
                foreach (var idx in selectedIndices)
                    dataGrid.FlashRow(idx, Color.Red, TimeSpan.FromMilliseconds(300));
                dataSource.DeleteRecords(selectedIndices);
                dataGrid.ClearSelection();
            }
            else
            {
                // Single delete: flash red, then remove from data source
                int idx = dataGrid.SelectedRowIndex;
                if (idx < 0) return;
                dataGrid.FlashRow(idx, Color.Red, TimeSpan.FromMilliseconds(300));
                dataSource.DeleteRecord(idx);
            }
        };

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
                "  [yellow]Space[/] Toggle checkbox",
                "  [yellow]Ctrl+A[/] Select all",
                "",
                "[dim]Filtering:[/]",
                "  [yellow]/[/] Enter filter mode",
                "  [yellow]text[/] Filter all columns",
                "  [yellow]col:value[/] Filter specific column",
                "  [yellow]col>value[/] Numeric comparison",
                "",
                "[dim]Animations:[/]",
                "  [green]Add[/] \u2192 Green highlight pulse",
                "  [red]Delete[/] \u2192 Fade-out removal",
                "  [cyan]Move[/] \u2192 Cyan flash on swap",
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
            .AddRow("Inline Filter", "[green]\u2713 / to Filter[/]")
            .AddRow("Scrollbar Drag", "[green]\u2713 Smooth[/]")
            .AddRow("Checkbox Mode", "[green]\u2713 Multi-Select[/]")
            .AddRow("Row Animations", "[green]\u2713 Flash/Fade[/]")
            .DoubleLine()
            .WithHeaderColors(Color.White, Color.DarkGreen)
            .WithMargin(1, 1, 1, 0)
            .Build();

        // Status bar
        var statusBar = Controls.Markup("[dim]Esc: Close | \u2191\u2193: Navigate | Space: Check | Ctrl+A: Select All | /: Filter[/]")
            .StickyBottom()
            .Build();

        // Right panel: scrollable info + feature table
        var infoPanel = Controls.ScrollablePanel()
            .AddControl(infoMarkup)
            .AddControl(featureTable)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        // Layout: left = data grid, right = info panel
        var grid = Controls.HorizontalGrid()
            .Column(col => col.Add(dataGrid))
            .Column(col => col.Width(38).Add(infoPanel))
            .WithSplitterAfter(0)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        // Wire up selection event to update status
        dataGrid.SelectedRowChanged += (sender, rowIdx) =>
        {
            if (rowIdx >= 0 && rowIdx < dataSource.RowCount)
            {
                string name = dataSource.GetCellValue(rowIdx, 1);
                string price = dataSource.GetCellValue(rowIdx, 3);
                statusBar.SetContent(new List<string> { $"[dim]Row {rowIdx + 1}: {name} ({price}) | Space: Check | /: Filter[/]" });
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
            .AddControls(toolbar, grid, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    if (e.AlreadyHandled)
                        return;
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }
}
