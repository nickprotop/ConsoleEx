# TableControl

Interactive data grid with virtual data support, sorting, inline editing, and multi-selection.

## Overview

TableControl renders tabular data with full keyboard and mouse interaction. It supports two data modes: **in-memory rows** for simple tables, and **ITableDataSource** for virtual/lazy data binding that handles millions of rows by querying only visible rows on demand.

By default, `ReadOnly = true` preserves backward-compatible static table rendering. Set `.Interactive()` to enable selection, navigation, editing, and all interactive features.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ReadOnly` | `bool` | `true` | When true, table is static display only |
| `SelectedRowIndex` | `int` | `-1` | Index of selected row (-1 = none) |
| `SelectedColumnIndex` | `int` | `-1` | Index of selected column in cell navigation mode |
| `CellNavigationEnabled` | `bool` | `false` | Enable Tab/Arrow cell-level navigation |
| `MultiSelectEnabled` | `bool` | `false` | Enable Ctrl+Click/Shift+Click multi-selection |
| `CheckboxMode` | `bool` | `false` | Show [x]/[ ] checkboxes (implies MultiSelect) |
| `SortingEnabled` | `bool` | `false` | Enable column sorting by clicking headers |
| `ColumnResizeEnabled` | `bool` | `false` | Enable column resize by dragging borders |
| `InlineEditingEnabled` | `bool` | `false` | Enable F2/Enter/DblClick cell editing |
| `DataSource` | `ITableDataSource?` | `null` | Virtual data source for large datasets |
| `BorderStyle` | `BorderStyle` | `Single` | Border style (Single, DoubleLine, Rounded, None) |
| `ShowHeader` | `bool` | `true` | Show column header row |
| `ShowRowSeparators` | `bool` | `false` | Show horizontal lines between rows |
| `Title` | `string?` | `null` | Title text above the table |
| `VerticalScrollbarVisibility` | `ScrollbarVisibility` | `Auto` | When to show vertical scrollbar |
| `HorizontalScrollbarVisibility` | `ScrollbarVisibility` | `Auto` | When to show horizontal scrollbar |
| `BackgroundColor` | `Color?` | `Color.Default` | Background color (null to inherit gradient) |
| `ForegroundColor` | `Color?` | `Color.Default` | Text color |
| `HeaderBackgroundColor` | `Color?` | `Color.Default` | Header row background |
| `HeaderForegroundColor` | `Color?` | `Color.Default` | Header row text color |
| `HasFocus` | `bool` | `false` | Whether table has keyboard focus |
| `IsEditing` | `bool` | `false` | Whether a cell is currently being edited |
| `AutoHighlightOnFocus` | `bool` | `true` | Auto-select first row on focus |

## Events

| Event | Arguments | Description |
|-------|-----------|-------------|
| `SelectedRowChanged` | `EventHandler<int>` | Row selection changed |
| `SelectedRowItemChanged` | `EventHandler<TableRow?>` | Selected row object changed |
| `RowActivated` | `EventHandler<int>` | Row activated (Enter/double-click) |
| `CellActivated` | `EventHandler<(int Row, int Column)>` | Cell activated in cell navigation mode |
| `CellEditCompleted` | `EventHandler<(int Row, int Column, string OldValue, string NewValue)>` | Cell edit committed |
| `CellEditCancelled` | `EventHandler<(int Row, int Column)>` | Cell edit cancelled |
| `MouseRightClick` | `EventHandler<MouseEventArgs>` | Right-click (row/cell selected first) |
| `MouseClick` | `EventHandler<MouseEventArgs>` | Left click |
| `MouseDoubleClick` | `EventHandler<MouseEventArgs>` | Double click |
| `GotFocus` | `EventHandler` | Table received focus |
| `LostFocus` | `EventHandler` | Table lost focus |

## Creating Tables

### Static Table (ReadOnly)

```csharp
var table = Controls.Table()
    .AddColumn("Name")
    .AddColumn("Value", TextJustification.Right)
    .AddRow("CPU", "42%")
    .AddRow("Memory", "8.2 GB")
    .AddRow("Disk", "256 GB")
    .WithHeaderColors(Color.White, Color.DarkBlue)
    .Rounded()
    .Build();
```

### Interactive Table

```csharp
var table = Controls.Table()
    .AddColumn("Name")
    .AddColumn("Status")
    .AddColumn("Priority", TextJustification.Right)
    .AddRow("Task 1", "[green]Done[/]", "High")
    .AddRow("Task 2", "[yellow]In Progress[/]", "Medium")
    .AddRow("Task 3", "[red]Blocked[/]", "Low")
    .Interactive()
    .WithCellNavigation()
    .WithSorting()
    .WithHeaderColors(Color.White, Color.DarkBlue)
    .Rounded()
    .OnRowActivated((sender, rowIdx) =>
    {
        // Handle row activation
    })
    .Build();
```

### Interactive DataGrid with Virtual Data

```csharp
// Implement ITableDataSource for large datasets
public class ProductDataSource : ITableDataSource
{
    private readonly List<Product> _products;

    public int RowCount => _products.Count;
    public int ColumnCount => 4;

    public string GetColumnHeader(int col) => col switch
    {
        0 => "ID", 1 => "Name", 2 => "Price", 3 => "Status", _ => ""
    };

    public string GetCellValue(int row, int col) => col switch
    {
        0 => _products[row].Id.ToString(),
        1 => _products[row].Name,
        2 => $"${_products[row].Price:F2}",
        3 => _products[row].Status,
        _ => ""
    };

    public TextJustification GetColumnAlignment(int col) => col switch
    {
        0 => TextJustification.Right,
        2 => TextJustification.Right,
        _ => TextJustification.Left
    };

    // Optional: fixed column widths (null = auto)
    public int? GetColumnWidth(int col) => col switch
    {
        0 => 6, _ => null
    };

    // Optional: sorting support
    public bool CanSort(int col) => true;
    public void Sort(int col, SortDirection direction) { /* sort _products */ }

    public event EventHandler? DataChanged;
}

// Use it
var dataSource = new ProductDataSource(products);

var grid = Controls.Table()
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
    .OnCellEditCompleted((sender, e) =>
    {
        dataSource.UpdateRecord(e.Row, e.Column, e.NewValue);
    })
    .Build();
```

## Keyboard Support

### Row Navigation

| Key | Action |
|-----|--------|
| **Up/Down Arrow** | Move selection up/down |
| **Page Up/Down** | Jump by visible row count |
| **Home** | Select first row |
| **End** | Select last row |
| **Enter** | Activate row (or edit cell if cell nav enabled) |
| **Escape** | Deselect cell (back to row mode) |

### Cell Navigation (when enabled)

| Key | Action |
|-----|--------|
| **Tab** | Move to next cell (wraps to next row) |
| **Shift+Tab** | Move to previous cell (wraps to previous row) |
| **Left/Right Arrow** | Move between cells in current row |
| **Enter** | Activate cell / begin edit |

### Inline Editing (when enabled)

| Key | Action |
|-----|--------|
| **F2** | Begin editing selected cell |
| **Enter** | Commit edit |
| **Escape** | Cancel edit |
| **Left/Right** | Move cursor within edit buffer |
| **Home/End** | Move cursor to start/end |
| **Backspace/Delete** | Delete character |

### Multi-Select

| Key | Action |
|-----|--------|
| **Ctrl+A** | Select all rows |
| **Space** | Toggle checkbox (checkbox mode) |

## Mouse Support

| Action | Result |
|--------|--------|
| **Left Click** | Select row (and cell if cell nav enabled) |
| **Double Click** | Activate row / begin cell edit |
| **Right Click** | Select row/cell, then fire MouseRightClick |
| **Ctrl+Click** | Toggle row selection (multi-select) |
| **Shift+Click** | Select range (multi-select) |
| **Click Header** | Sort by column (toggles Asc/Desc/None) |
| **Mouse Wheel** | Vertical scroll |
| **Shift+Wheel** | Horizontal scroll |
| **Drag Column Border** | Resize column |
| **Click Scrollbar Track** | Page up/down or left/right |
| **Drag Scrollbar Thumb** | Smooth scroll |
| **Click Scrollbar Arrow** | Scroll by one row/column |

## ITableDataSource

The `ITableDataSource` interface enables virtual data binding for large datasets. Only visible rows are queried — the control never loads all data into memory.

```csharp
public interface ITableDataSource
{
    int RowCount { get; }
    int ColumnCount { get; }
    string GetColumnHeader(int columnIndex);
    string GetCellValue(int rowIndex, int columnIndex);

    // Optional (default interface methods):
    TextJustification GetColumnAlignment(int columnIndex) => TextJustification.Left;
    int? GetColumnWidth(int columnIndex) => null;
    Color? GetRowBackgroundColor(int rowIndex) => null;
    Color? GetRowForegroundColor(int rowIndex) => null;
    bool IsRowEnabled(int rowIndex) => true;
    object? GetRowTag(int rowIndex) => null;
    bool CanSort(int columnIndex) => false;
    void Sort(int columnIndex, SortDirection direction) { }

    event EventHandler? DataChanged;
}
```

When `DataSource` is set:
- Internal `_rows`/`_columns` lists are ignored
- Only visible rows are queried via `GetCellValue()`
- Column widths auto-measure from visible rows
- Sorting delegates to `DataSource.Sort()` if `CanSort()` returns true
- `DataChanged` event triggers re-measure and re-render
- `AddRow()`/`ClearRows()` throw if DataSource is set

## Builder Methods

### Data

| Method | Description |
|--------|-------------|
| `.AddColumn(header, alignment?, width?)` | Add a column |
| `.WithColumns(headers...)` | Add multiple columns by name |
| `.AddRow(cells...)` | Add a data row |
| `.WithDataSource(source)` | Set virtual data source |

### Interactive Features

| Method | Description |
|--------|-------------|
| `.Interactive()` | Enable interactive mode (ReadOnly = false) |
| `.WithCellNavigation()` | Enable cell-level Tab/Arrow navigation |
| `.WithMultiSelect()` | Enable Ctrl+Click/Shift+Click multi-select |
| `.WithCheckboxMode()` | Enable checkbox multi-select |
| `.WithSorting()` | Enable click-header sorting |
| `.WithColumnResize()` | Enable drag-to-resize columns |
| `.WithInlineEditing()` | Enable F2/Enter cell editing |

### Appearance

| Method | Description |
|--------|-------------|
| `.WithTitle(text, alignment?)` | Set title above table |
| `.WithHeaderColors(fg, bg)` | Set header row colors |
| `.Rounded()` / `.DoubleLine()` / `.SingleLine()` / `.NoBorder()` | Border style |
| `.WithBorderColor(color)` | Set border color |
| `.ShowRowSeparators()` | Show lines between rows |
| `.WithVerticalScrollbar(visibility)` | Scrollbar visibility (Auto/Always/Never) |
| `.WithHorizontalScrollbar(visibility)` | Scrollbar visibility (Auto/Always/Never) |

### Events

| Method | Description |
|--------|-------------|
| `.OnSelectedRowChanged(handler)` | Wire selection event |
| `.OnRowActivated(handler)` | Wire row activation event |
| `.OnCellActivated(handler)` | Wire cell activation event |
| `.OnCellEditCompleted(handler)` | Wire cell edit commit event |
| `.OnRightClick(handler)` | Wire right-click event |

## Examples

### File Explorer Table

```csharp
var table = Controls.Table()
    .AddColumn("Name")
    .AddColumn("Size", TextJustification.Right, 10)
    .AddColumn("Modified", TextJustification.Right, 20)
    .Interactive()
    .WithSorting()
    .Rounded()
    .WithHeaderColors(Color.White, Color.DarkBlue)
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithHorizontalAlignment(HorizontalAlignment.Stretch)
    .OnRowActivated((sender, rowIdx) =>
    {
        var row = sender.GetRow(rowIdx);
        var path = row?.Tag as string;
        if (path != null && Directory.Exists(path))
            NavigateTo(path);
    })
    .OnRightClick((sender, args) =>
    {
        ShowContextMenu(sender, args);
    })
    .Build();
```

### Editable Spreadsheet

```csharp
var table = Controls.Table()
    .AddColumn("A")
    .AddColumn("B")
    .AddColumn("C")
    .AddRow("1", "2", "3")
    .AddRow("4", "5", "6")
    .Interactive()
    .WithCellNavigation()
    .WithInlineEditing()
    .WithColumnResize()
    .OnCellEditCompleted((sender, e) =>
    {
        // e.Row, e.Column, e.OldValue, e.NewValue
        RecalculateFormulas();
    })
    .Build();
```

### Multi-Select with Checkboxes

```csharp
var table = Controls.Table()
    .AddColumn("Task")
    .AddColumn("Status")
    .AddRow("Write tests", "[yellow]Pending[/]")
    .AddRow("Code review", "[green]Done[/]")
    .AddRow("Deploy", "[red]Blocked[/]")
    .Interactive()
    .WithCheckboxMode()
    .Build();

// Later: get checked rows
var checked = table.GetCheckedRows();
```

### Gradient Background

TableControl preserves gradient backgrounds from parent windows when no explicit background color is set:

```csharp
var gradient = ColorGradient.FromColors(
    new Color(10, 10, 50),
    new Color(0, 50, 80),
    new Color(40, 10, 60));

var window = new WindowBuilder(ws)
    .WithBackgroundGradient(gradient, GradientDirection.Vertical)
    .AddControls(table)
    .BuildAndShow();
```

## Sorting

When `SortingEnabled = true`, clicking a column header cycles through: Ascending -> Descending -> None. A sort indicator (up/down triangle) appears in the header.

For in-memory rows, sorting creates an internal index map. For `ITableDataSource`, sorting delegates to `DataSource.Sort()` if `CanSort()` returns true.

## Virtual Rendering

Regardless of data mode or ReadOnly state, TableControl always uses virtual rendering: only visible rows are measured and painted. This means 10,000+ row tables render instantly with no performance penalty.

Column widths are computed using sample-based measurement (header + visible rows + a small buffer), cached and invalidated on significant scroll or data changes.

## Scrollbars

- **Vertical scrollbar**: Appears when rows exceed viewport. Shows up/down arrows, draggable thumb, and clickable track for page scrolling.
- **Horizontal scrollbar**: Appears when total column width exceeds viewport. Same interaction model.
- Both support `ScrollbarVisibility.Auto` (default), `Always`, or `Never`.

## See Also

- [ListControl](ListControl.md) - For simple item lists
- [Controls Reference](../CONTROLS.md) - All controls overview

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
