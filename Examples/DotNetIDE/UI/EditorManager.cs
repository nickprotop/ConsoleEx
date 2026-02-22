using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace DotNetIDE;

public class EditorManager
{
    private readonly ConsoleWindowSystem _ws;
    private readonly TabControl _tabControl;
    private readonly Dictionary<string, int> _openFiles = new();
    private readonly Dictionary<int, (string Path, MultilineEditControl Editor, bool IsDirty)> _tabData = new();

    public event EventHandler<(int Line, int Column)>? CursorChanged;
    public event EventHandler<string?>? ActiveFileChanged;

    public string? CurrentFilePath =>
        _tabControl.ActiveTabIndex >= 0 && _tabData.TryGetValue(_tabControl.ActiveTabIndex, out var d) ? d.Path : null;

    public MultilineEditControl? CurrentEditor =>
        _tabControl.ActiveTabIndex >= 0 && _tabData.TryGetValue(_tabControl.ActiveTabIndex, out var d2) ? d2.Editor : null;

    public TabControl TabControl => _tabControl;

    public bool HasOpenFiles => _tabControl.TabCount > 0;
    public event EventHandler? OpenFilesStateChanged;

    public EditorManager(ConsoleWindowSystem ws)
    {
        _ws = ws;
        _tabControl = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill,
            HeaderStyle = TabHeaderStyle.Separator
        };

        _tabControl.TabChanged += OnTabChanged;
        _tabControl.TabCloseRequested += OnTabCloseRequested;
    }

    public void OpenFile(string path)
    {
        // Already open — just switch to it
        if (_openFiles.TryGetValue(path, out var existingIdx))
        {
            _tabControl.ActiveTabIndex = existingIdx;
            return;
        }

        IWindowControl content;
        bool isBinary = FileService.IsBinaryFile(path);

        if (isBinary)
        {
            var binaryPanel = new ScrollablePanelControl { HorizontalAlignment = HorizontalAlignment.Stretch };
            binaryPanel.AddControl(new MarkupControl(new List<string>
            {
                $"[yellow]Binary file: {Markup.Escape(Path.GetFileName(path))}[/]",
                "[dim]Cannot display binary content in text editor.[/]"
            }));
            content = binaryPanel;
            AddTab(path, content, editor: null, isDirty: false);
            return;
        }

        string fileContent;
        try
        {
            fileContent = FileService.ReadFile(path);
        }
        catch (Exception ex)
        {
            var errorPanel = new ScrollablePanelControl { HorizontalAlignment = HorizontalAlignment.Stretch };
            errorPanel.AddControl(new MarkupControl(new List<string>
                { $"[red]Error reading file: {Markup.Escape(ex.Message)}[/]" }));
            AddTab(path, errorPanel, editor: null, isDirty: false);
            return;
        }

        var editor = CreateEditor(path, fileContent);
        AddTab(path, editor, editor: editor, isDirty: false);
    }

    private void AddTab(string path, IWindowControl content, MultilineEditControl? editor, bool isDirty)
    {
        bool wasEmpty = _tabControl.TabCount == 0;
        var tabTitle = Path.GetFileName(path);
        _tabControl.AddTab(tabTitle, content, isClosable: true);
        var tabIndex = _tabControl.TabCount - 1;
        _openFiles[path] = tabIndex;

        // Now that the editor has a Container, sync FocusedBackgroundColor to match view-mode bg
        if (editor != null)
            editor.FocusedBackgroundColor = editor.BackgroundColor;

        var storedEditor = editor ?? new MultilineEditControl();
        _tabData[tabIndex] = (path, storedEditor, isDirty);

        _tabControl.ActiveTabIndex = tabIndex;

        if (wasEmpty)
            OpenFilesStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private MultilineEditControl CreateEditor(string path, string content)
    {
        var editor = new MultilineEditControl
        {
            ShowLineNumbers = true,
            HighlightCurrentLine = true,
            WrapMode = WrapMode.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };

        editor.Content = content;

        var ext = FileService.GetExtension(path);
        if (ext == ".cs")
            editor.SyntaxHighlighter = new CSharpSyntaxHighlighter();

        editor.CursorPositionChanged += (_, pos) =>
        {
            CursorChanged?.Invoke(this, pos);
        };

        editor.ContentChanged += (_, _) =>
        {
            // Mark current tab as dirty
            if (_tabControl.ActiveTabIndex >= 0 && _tabData.TryGetValue(_tabControl.ActiveTabIndex, out var data))
            {
                if (!data.IsDirty)
                {
                    _tabData[_tabControl.ActiveTabIndex] = (data.Path, data.Editor, true);
                    _tabControl.SetTabTitle(_tabControl.ActiveTabIndex, Path.GetFileName(data.Path) + " *");
                }
            }
        };

        return editor;
    }

    public void SaveCurrent()
    {
        if (_tabControl.ActiveTabIndex < 0) return;
        if (!_tabData.TryGetValue(_tabControl.ActiveTabIndex, out var data)) return;

        var dir = Path.GetDirectoryName(data.Path);
        if (dir != null && !Directory.Exists(dir)) return;

        try
        {
            FileService.WriteFile(data.Path, data.Editor.Content);
            _tabData[_tabControl.ActiveTabIndex] = (data.Path, data.Editor, false);
            _tabControl.SetTabTitle(_tabControl.ActiveTabIndex, Path.GetFileName(data.Path));
        }
        catch (Exception ex)
        {
            _ws.LogService.LogError($"Failed to save {data.Path}: {ex.Message}");
        }
    }

    public bool IsCurrentTabDirty()
    {
        if (_tabControl.ActiveTabIndex < 0) return false;
        return _tabData.TryGetValue(_tabControl.ActiveTabIndex, out var data) && data.IsDirty;
    }

    public void CloseCurrentTab()
    {
        if (_tabControl.ActiveTabIndex < 0) return;
        var idx = _tabControl.ActiveTabIndex;

        if (_tabData.TryGetValue(idx, out var data))
            _openFiles.Remove(data.Path);

        _tabData.Remove(idx);
        _tabControl.RemoveTab(idx);

        // Re-index remaining tabs after removal
        var newOpenFiles = new Dictionary<string, int>();
        var newTabData = new Dictionary<int, (string Path, MultilineEditControl Editor, bool IsDirty)>();

        foreach (var (path, oldIdx) in _openFiles)
        {
            var newIdx = oldIdx > idx ? oldIdx - 1 : oldIdx;
            newOpenFiles[path] = newIdx;
        }
        foreach (var (oldIdx, tdata) in _tabData)
        {
            var newIdx = oldIdx > idx ? oldIdx - 1 : oldIdx;
            newTabData[newIdx] = tdata;
        }

        _openFiles.Clear();
        foreach (var kv in newOpenFiles) _openFiles[kv.Key] = kv.Value;
        _tabData.Clear();
        foreach (var kv in newTabData) _tabData[kv.Key] = kv.Value;

        if (_tabControl.TabCount == 0)
            OpenFilesStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CloseAll()
    {
        while (_tabControl.TabCount > 0)
            CloseCurrentTab();
    }

    public void CloseTabAt(int index)
    {
        if (index < 0 || index >= _tabControl.TabCount) return;
        _tabControl.ActiveTabIndex = index;
        CloseCurrentTab();
    }

    private void OnTabCloseRequested(object? sender, SharpConsoleUI.Events.TabEventArgs args)
    {
        CloseTabAt(args.Index);
    }

    public void GoToLine(int line)
    {
        CurrentEditor?.GoToLine(line);
    }

    public LayoutRect GetCursorBounds()
    {
        var editor = CurrentEditor;
        if (editor == null) return new LayoutRect(30, 5, 1, 1);
        int col = Math.Max(0, editor.CurrentColumn - 1);
        int line = Math.Max(0, editor.CurrentLine - 1);
        return new LayoutRect(col + 30, line + 5, 1, 1);
    }

    private void OnTabChanged(object? sender, TabChangedEventArgs args)
    {
        ActiveFileChanged?.Invoke(this, CurrentFilePath);
    }
}
