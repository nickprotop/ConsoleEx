using System.Collections.ObjectModel;
using System.Collections.Specialized;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using TerminalMail.ViewModels;

namespace TerminalMail.UI;

/// <summary>
/// Adapts the mailbox's ObservableCollection&lt;MessageViewModel&gt; to the TableControl's
/// virtual data-source API. When the collection changes (e.g. the user switches folders),
/// the table refreshes automatically — no imperative row rebuilding.
/// </summary>
public sealed class MessageTableDataSource : ITableDataSource
{
    private readonly ObservableCollection<MessageViewModel> _items;

    public MessageTableDataSource(ObservableCollection<MessageViewModel> items)
    {
        _items = items;
        _items.CollectionChanged += (s, e) => CollectionChanged?.Invoke(this, e);
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int RowCount => _items.Count;
    public int ColumnCount => 4;

    private static readonly string[] Headers = { " ", "From", "Subject", "Date" };

    public string GetColumnHeader(int columnIndex) => Headers[columnIndex];

    public TextJustification GetColumnAlignment(int columnIndex) => columnIndex switch
    {
        0 => TextJustification.Center,
        3 => TextJustification.Right,
        _ => TextJustification.Left,
    };

    public int? GetColumnWidth(int columnIndex) => columnIndex switch
    {
        0 => 2,    // flag/unread dot
        1 => 22,   // From
        3 => 8,    // Date
        _ => null, // Subject - auto
    };

    public string GetCellValue(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= _items.Count) return "";
        var m = _items[rowIndex];
        return columnIndex switch
        {
            0 => m.IsFlagged ? "[yellow]★[/]" : (m.IsRead ? " " : "[cyan1]●[/]"),
            1 => m.From,
            2 => m.Subject,
            3 => m.ShortDate,
            _ => "",
        };
    }

    public Color? GetRowForegroundColor(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _items.Count) return null;
        return _items[rowIndex].IsRead ? Color.Grey50 : Color.Grey93;
    }

    /// <summary>Maps a table row back to its view model (for selection handling).</summary>
    public MessageViewModel GetMessage(int rowIndex) => _items[rowIndex];
}
