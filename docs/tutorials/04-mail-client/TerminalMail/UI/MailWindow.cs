using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using TerminalMail.ViewModels;

namespace TerminalMail.UI;

/// <summary>Builds and owns the full-screen mail window.</summary>
public sealed class MailWindow
{
    private readonly ConsoleWindowSystem _ws;
    private readonly MailboxViewModel _mailbox;

    private Window _window = null!;
    private ListControl _folderList = null!;
    private TableControl _messageTable = null!;
    private MessageTableDataSource _messageSource = null!;
    private MarkupControl _readingHeader = null!;
    private MarkupControl _readingBody = null!;
    private MarkupControl _breadcrumb = null!;

    public MailWindow(ConsoleWindowSystem ws, MailboxViewModel mailbox)
    {
        _ws = ws;
        _mailbox = mailbox;
    }

    public Window Create()
    {
        BuildControls();
        var grid = BuildGrid();

        _window = new WindowBuilder(_ws)
            .WithTitle("TerminalMail")
            .Maximized()
            .WithBorderStyle(BorderStyle.Rounded)
            .WithActiveBorderColor(ColorScheme.ActiveBorder)
            .WithBackgroundGradient(ColorScheme.WindowGradient, GradientDirection.Vertical)
            .AddControl(BuildBreadcrumb())
            .AddControl(grid)
            .AddControl(BuildHelpBar())
            .BuildAndShow();

        _window.KeyPressed += OnKeyPressed;

        return _window;
    }

    private void BuildControls()
    {
        // Folder list (left) — suppress default "List" title
        var folderBuilder = Controls.List()
            .WithTitle("")
            .WithColors(ColorScheme.Body, ColorScheme.SidebarBg)
            .WithHighlightForegroundColor(Color.White)
            .WithHighlightBackgroundColor(Color.SteelBlue);
        foreach (var f in _mailbox.Folders)
        {
            var badge = f.UnreadCount > 0 ? $"  [cyan1]({f.UnreadCount})[/]" : "";
            folderBuilder.AddItem($"{f.Name}{badge}");
        }
        _folderList = folderBuilder.Build();

        // Message table (middle), driven by the data source
        _messageSource = new MessageTableDataSource(_mailbox.Messages);
        _messageTable = Controls.Table()
            .WithDataSource(_messageSource)
            .Build();

        // Reading pane (right)
        _readingHeader = Controls.Markup("[grey50]Select a message[/]").Build();
        _readingHeader.BackgroundColor = ColorScheme.PanelHeaderBg;
        _readingBody = new MarkupControl(new List<string> { "" }) { Wrap = true };

        // Folder selection → switch folder (refills the bound ObservableCollection,
        // which the data source forwards to the table automatically).
        _folderList.SelectedIndexChanged += (_, index) =>
        {
            if (index >= 0 && index < _mailbox.Folders.Count)
                _mailbox.SelectFolder(_mailbox.Folders[index]);
        };

        // Message row selection → set SelectedMessage on the view model.
        _messageTable.SelectedRowChanged += (_, rowIndex) =>
        {
            if (rowIndex >= 0 && rowIndex < _mailbox.Messages.Count)
                _mailbox.SelectMessage(_messageSource.GetMessage(rowIndex));
        };

        // One-way bindings: SelectedMessage → reading pane header + body.
        _readingHeader.Bind(_mailbox,
            m => m.SelectedMessage,
            c => c.Text,
            msg => msg is null ? "[grey50]Select a message[/]" : msg.HeaderText);

        _readingBody.Bind(_mailbox,
            m => m.SelectedMessage,
            c => c.Text,
            msg => msg is null ? "" : msg.Body);
    }

    private HorizontalGridControl BuildGrid()
    {
        return Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Column(col =>
            {
                col.Width(22);
                col.Add(MakeHeader("Folders"));
                col.Add(_folderList);
            })
            .Column(col =>
            {
                col.Flex(2.0);
                col.Add(MakeHeader("Messages"));
                col.Add(_messageTable);
            })
            .Column(col =>
            {
                col.Flex(3.0);
                col.Add(_readingHeader);
                col.AsScrollable();
                col.Add(_readingBody);
            })
            .Build();
    }

    private MarkupControl BuildBreadcrumb()
    {
        _breadcrumb = Controls.Markup(BreadcrumbText()).Build();
        _breadcrumb.BackgroundColor = ColorScheme.PanelHeaderBg;
        // Keep breadcrumb in sync with folder changes.
        _mailbox.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MailboxViewModel.SelectedFolder))
                _breadcrumb.Text = BreadcrumbText();
        };
        return _breadcrumb;
    }

    private string BreadcrumbText()
    {
        var folder = _mailbox.SelectedFolder?.Name ?? "—";
        return $"[bold {ColorScheme.PrimaryMarkup}]TerminalMail[/] [grey50]·[/] [grey85]{folder}[/]";
    }

    private static MarkupControl BuildHelpBar()
    {
        var bar = Controls.Markup(
            "[grey50]↑↓[/] navigate   [grey50]c[/] compose   [grey50]s[/] settings   [grey50]q[/] quit")
            .Build();
        bar.BackgroundColor = ColorScheme.PanelHeaderBg;
        return bar;
    }

    private void OnKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        switch (char.ToLowerInvariant(e.KeyInfo.KeyChar))
        {
            case 'c':
                ShowCompose();
                e.Handled = true;
                break;
            case 's':
                ShowSettings();
                e.Handled = true;
                break;
            case 'q':
                _ws.Shutdown(0);
                e.Handled = true;
                break;
        }
    }

    // Tasks 13 and 14: real modal implementations.
    private void ShowCompose() => Dialogs.ShowCompose(_ws);
    private void ShowSettings() => SettingsView.Show(_ws);

    private static MarkupControl MakeHeader(string text)
    {
        var h = Controls.Markup($"[bold grey85]{text}[/]").Build();
        h.BackgroundColor = ColorScheme.PanelHeaderBg;
        return h;
    }
}
