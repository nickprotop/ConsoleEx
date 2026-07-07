using System.Collections.Specialized;
using MultiDashboard.Models;
using MultiDashboard.Services;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace MultiDashboard.Data;

/// <summary>
/// A live <see cref="ITableDataSource"/> over <see cref="StockService"/>. Holds a snapshot list of
/// <see cref="StockQuote"/> rows; <see cref="Refresh"/> pulls fresh quotes, re-applies the current
/// sort, and raises a <see cref="NotifyCollectionChangedAction.Reset"/> so the bound
/// <see cref="TableControl"/> re-renders. Mirrors the DemoApp <c>ProductDataSource</c> pattern.
/// </summary>
public class MarketDataSource : ITableDataSource
{
    private static readonly string[] Headers = { "Symbol", "Price", "Change%", "Volume" };
    private static readonly TextJustification[] Alignments =
    {
        TextJustification.Left, TextJustification.Right, TextJustification.Right, TextJustification.Right
    };

    private readonly StockService _stockService;
    private readonly Random _volumeRandom = new(7);
    private List<StockQuote> _rows = new();

    private int _sortColumn = -1;
    private SortDirection _sortDirection = SortDirection.None;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public MarketDataSource(StockService stockService)
    {
        _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
        _rows = _stockService.GetLatestQuotes();
    }

    /// <summary>The current in-memory quote rows (post-sort order). Read by the async feed loop.</summary>
    public IReadOnlyList<StockQuote> Quotes => _rows;

    public int RowCount => _rows.Count;

    public int ColumnCount => Headers.Length;

    public string GetColumnHeader(int columnIndex) => Headers[columnIndex];

    public TextJustification GetColumnAlignment(int columnIndex) => Alignments[columnIndex];

    public string GetCellValue(int rowIndex, int columnIndex)
    {
        var q = _rows[rowIndex];
        return columnIndex switch
        {
            0 => q.Symbol,
            1 => q.Price.ToString("F2"),
            2 => FormatChange(q.ChangePercent),
            3 => EstimateVolume(q).ToString("N0"),
            _ => ""
        };
    }

    /// <summary>Green/red signed percentage change (e.g. <c>+0.42%</c> / <c>-1.10%</c>).</summary>
    private static string FormatChange(double changePercent)
    {
        var color = changePercent >= 0 ? "green" : "red";
        var sign = changePercent >= 0 ? "+" : "";
        return $"[{color}]{sign}{changePercent:F2}%[/]";
    }

    /// <summary>Synthesises a stable-ish volume figure from the symbol + price for display.</summary>
    private long EstimateVolume(StockQuote q)
    {
        // Deterministic-ish per symbol so the column reads like real turnover, scaled by price.
        int seed = Math.Abs(q.Symbol.GetHashCode()) % 900 + 100;
        return (long)(seed * 1000L + q.Price * 250);
    }

    public bool CanSort(int columnIndex) => true;

    public void Sort(int columnIndex, SortDirection direction)
    {
        _sortColumn = columnIndex;
        _sortDirection = direction;
        ApplySort();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private void ApplySort()
    {
        if (_sortDirection == SortDirection.None || _sortColumn < 0)
        {
            // Restore insertion order (StockService sorts by symbol).
            _rows = _rows.OrderBy(q => q.Symbol, StringComparer.Ordinal).ToList();
            return;
        }

        IEnumerable<StockQuote> ordered = _sortColumn switch
        {
            0 => _rows.OrderBy(q => q.Symbol, StringComparer.Ordinal),
            1 => _rows.OrderBy(q => q.Price),
            2 => _rows.OrderBy(q => q.ChangePercent),
            3 => _rows.OrderBy(q => EstimateVolume(q)),
            _ => _rows.AsEnumerable()
        };

        _rows = _sortDirection == SortDirection.Descending
            ? ordered.Reverse().ToList()
            : ordered.ToList();
    }

    /// <summary>
    /// Pulls a fresh set of quotes from <see cref="StockService"/>, re-applies the active sort, and
    /// raises a <see cref="NotifyCollectionChangedAction.Reset"/> so the bound table re-renders.
    /// </summary>
    public void Refresh()
    {
        _rows = _stockService.GetLatestQuotes();
        ApplySort();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
