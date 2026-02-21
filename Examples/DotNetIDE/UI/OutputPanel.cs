using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Controls.Terminal;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace DotNetIDE;

public class OutputPanel
{
    private readonly ConsoleWindowSystem _ws;
    private readonly TabControl _tabControl;
    private readonly ScrollablePanelControl _buildPanel;
    private readonly ScrollablePanelControl _testPanel;
    private readonly ListControl _problemsList;
    private readonly LogViewerControl _logViewer;
    private TerminalControl? _shellTerminal;
    private int _shellTabIndex = -1;

    public event EventHandler<BuildDiagnostic>? DiagnosticNavigateRequested;

    public TabControl TabControl => _tabControl;

    public OutputPanel(ConsoleWindowSystem ws)
    {
        _ws = ws;

        _buildPanel = new ScrollablePanelControl
        {
            AutoScroll = true,
            ShowScrollbar = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };

        _testPanel = new ScrollablePanelControl
        {
            AutoScroll = true,
            ShowScrollbar = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };

        _problemsList = new ListControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };
        _problemsList.ItemActivated += OnProblemActivated;

        _logViewer = new LogViewerControl(ws.LogService)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };

        _tabControl = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };
        _tabControl.AddTab("Output", _logViewer);
        _tabControl.AddTab("Build", _buildPanel);
        _tabControl.AddTab("Test", _testPanel);
        _tabControl.AddTab("Problems", _problemsList);
    }

    public void AppendBuildLine(string line)
    {
        string markup;
        if (line.Contains(": error ", StringComparison.OrdinalIgnoreCase))
            markup = $"[red]{Markup.Escape(line)}[/]";
        else if (line.Contains(": warning ", StringComparison.OrdinalIgnoreCase))
            markup = $"[yellow]{Markup.Escape(line)}[/]";
        else if (line.StartsWith("Build succeeded", StringComparison.OrdinalIgnoreCase))
            markup = $"[green]{Markup.Escape(line)}[/]";
        else if (line.StartsWith("Build FAILED", StringComparison.OrdinalIgnoreCase))
            markup = $"[bold red]{Markup.Escape(line)}[/]";
        else
            markup = $"[grey]{Markup.Escape(line)}[/]";

        _buildPanel.AddControl(new MarkupControl(new List<string> { markup }));
        _buildPanel.ScrollToBottom();
    }

    public void AppendTestLine(string line)
    {
        string markup;
        if (line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("FAILED", StringComparison.OrdinalIgnoreCase))
            markup = $"[red]{Markup.Escape(line)}[/]";
        else if (line.Contains("passed", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("PASSED", StringComparison.OrdinalIgnoreCase))
            markup = $"[green]{Markup.Escape(line)}[/]";
        else
            markup = $"[grey]{Markup.Escape(line)}[/]";

        _testPanel.AddControl(new MarkupControl(new List<string> { markup }));
        _testPanel.ScrollToBottom();
    }

    public void ClearBuildOutput()
    {
        _buildPanel.ClearContents();
    }

    public void ClearTestOutput()
    {
        _testPanel.ClearContents();
    }

    public void SwitchToBuildTab() => _tabControl.ActiveTabIndex = 1;
    public void SwitchToTestTab() => _tabControl.ActiveTabIndex = 2;
    public void SwitchToProblemsTab() => _tabControl.ActiveTabIndex = 3;

    public TerminalControl? ShellTerminal => _shellTerminal;
    public bool IsShellTabActive => _shellTabIndex >= 0 && _tabControl.ActiveTabIndex == _shellTabIndex;

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public TerminalControl LaunchShell()
    {
        // If tab exists but the process exited, replace it
        if (_shellTabIndex >= 0 && (_shellTerminal == null || _shellTerminal.IsDisposed))
        {
            _tabControl.RemoveTab(_shellTabIndex);
            _shellTabIndex = -1;
            _shellTerminal = null;
        }

        if (_shellTabIndex >= 0)
        {
            _tabControl.ActiveTabIndex = _shellTabIndex;
            return _shellTerminal!;
        }

        _shellTerminal = Controls.Terminal().Build();
        _shellTerminal.HorizontalAlignment = HorizontalAlignment.Stretch;
        _shellTerminal.VerticalAlignment = VerticalAlignment.Fill;
        _tabControl.AddTab("Shell", _shellTerminal);
        _shellTabIndex = _tabControl.TabCount - 1;
        _tabControl.ActiveTabIndex = _shellTabIndex;
        return _shellTerminal;
    }

    public void PopulateProblems(List<BuildDiagnostic> diagnostics)
    {
        _problemsList.ClearItems();
        foreach (var diag in diagnostics)
        {
            var fileName = Path.GetFileName(diag.FilePath);
            var icon = diag.Severity == "error" ? "[red]E[/]" : "[yellow]W[/]";
            var text = $"{icon} {Markup.Escape(fileName)}({diag.Line},{diag.Column}): {Markup.Escape(diag.Message)}";
            var item = new ListItem(text) { Tag = diag };
            _problemsList.AddItem(item);
        }
    }

    public void PopulateLspDiagnostics(List<BuildDiagnostic> diagnostics)
    {
        PopulateProblems(diagnostics);
    }

    private void OnProblemActivated(object? sender, ListItem item)
    {
        if (item.Tag is BuildDiagnostic diag)
            DiagnosticNavigateRequested?.Invoke(this, diag);
    }
}
