using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace DotNetIDE;

/// <summary>
/// LSP code completion popup - shows a list of completion items.
/// The panel is added to / removed from a ScrollablePanelControl in the owner window
/// to simulate an overlay dropdown near the cursor.
/// </summary>
public class CompletionPortal
{
    private readonly ListControl _list;
    private readonly ScrollablePanelControl _panel;
    private List<CompletionItem> _currentItems = new();
    private bool _isVisible;

    public bool IsVisible => _isVisible;

    public IWindowControl Panel => _panel;

    public CompletionPortal()
    {
        _list = new ListControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _panel = new ScrollablePanelControl
        {
            ShowScrollbar = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            Visible = false
        };
        _panel.AddControl(_list);
    }

    public void Show(List<CompletionItem> items)
    {
        _currentItems = items;
        _list.ClearItems();

        int maxWidth = 40;
        foreach (var item in items.Take(20))
        {
            string icon = GetKindIcon(item.Kind);
            string label = $"{icon} {item.Label}";
            if (item.Detail != null) label += $"  {item.Detail}";
            if (label.Length > maxWidth) maxWidth = Math.Min(label.Length, 60);
            _list.AddItem(new ListItem(label) { Tag = item });
        }

        _panel.Width = maxWidth + 2;
        _panel.Height = Math.Min(10, items.Count) + 1;
        _panel.Visible = true;
        _isVisible = true;
    }

    public void Dismiss()
    {
        _panel.Visible = false;
        _isVisible = false;
        _currentItems.Clear();
        _list.ClearItems();
    }

    public CompletionItem? GetSelected()
    {
        return _list.SelectedItem?.Tag as CompletionItem;
    }

    private static string GetKindIcon(int kind) => kind switch
    {
        2 or 3 => "f()",
        4 => "new",
        5 or 6 => "var",
        7 => "cls",
        8 => "ifc",
        10 => "prp",
        _ => "   "
    };
}
