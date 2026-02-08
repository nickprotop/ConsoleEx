using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace TextEditorExample;

class Program
{
    const int ToolbarButtonWidth = 14;

    static ConsoleWindowSystem? _windowSystem;
    static MultilineEditControl? _editor;
    static MarkupControl? _statusBar;
    static string? _currentFilePath;
    static PromptControl? _gotoPrompt;
    static ButtonControl? _wrapButton;
    static ButtonControl? _lineNumButton;
    static ButtonControl? _whitespaceButton;
    static ButtonControl? _highlightButton;

    static readonly string SampleCode = @"using System;

namespace HelloWorld
{
    // A simple greeting program
    class Program
    {
        static readonly int MaxGreetings = 5;
        static string DefaultName = ""World"";

        static void Main(string[] args)
        {
            // Print greeting to the console
            var name = args.Length > 0 ? args[0] : DefaultName;
            for (int i = 0; i < MaxGreetings; i++)
            {
                double progress = (i + 1.0) / MaxGreetings;
                Console.WriteLine($""Hello, {name}! ({progress:P0})"");
            }

            if (name == DefaultName)
            {
                Console.WriteLine(""Pass your name as an argument!"");
            }
        }
    }
}";

    static async Task<int> Main(string[] args)
    {
        try
        {
            _windowSystem = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer),
                options: new ConsoleWindowSystemOptions(
                    StatusBarOptions: new StatusBarOptions(ShowTaskBar: true)));

            _windowSystem.StatusBarStateService.TopStatus =
                "Text Editor Example - MultilineEditControl Showcase";
            _windowSystem.StatusBarStateService.BottomStatus =
                "Tab to switch focus | Ctrl+C to Quit";

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _windowSystem?.Shutdown(0);
            };

            CreateEditorWindow(_windowSystem);

            await Task.Run(() => _windowSystem.Run());
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    static void CreateEditorWindow(ConsoleWindowSystem windowSystem)
    {
        // Build the editor
        _editor = Controls.MultilineEdit(SampleCode)
            .WithLineNumbers(true)
            .WithHighlightCurrentLine(true)
            .WithAutoIndent(true)
            .WithSyntaxHighlighter(new CSharpSyntaxHighlighter())
            .WithEditingHints()
            .WrapWords()
            .IsEditing(true)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithName("editor")
            .Build();

        // Menu bar
        var menu = BuildMenu();

        // Toolbar buttons
        _wrapButton = new ButtonControl { Text = "Wrap: Word", Width = ToolbarButtonWidth };
        _lineNumButton = new ButtonControl { Text = "Line #s: On", Width = ToolbarButtonWidth };
        _whitespaceButton = new ButtonControl { Text = "Wspace: Off", Width = ToolbarButtonWidth };
        _highlightButton = new ButtonControl { Text = "HiLine: On", Width = ToolbarButtonWidth };

        _wrapButton.Click += (_, _) => CycleWrapMode();
        _lineNumButton.Click += (_, _) => ToggleLineNumbers();
        _whitespaceButton.Click += (_, _) => ToggleWhitespace();
        _highlightButton.Click += (_, _) => ToggleHighlightLine();

        var toolbar = Controls.HorizontalGrid()
            .Column(col => col.Width(ToolbarButtonWidth).Add(_wrapButton))
            .Column(col => col.Width(ToolbarButtonWidth).Add(_lineNumButton))
            .Column(col => col.Width(ToolbarButtonWidth).Add(_whitespaceButton))
            .Column(col => col.Width(ToolbarButtonWidth).Add(_highlightButton))
            .StickyTop()
            .Build();

        // Status bar
        _statusBar = Controls.Markup("Ln 1, Col 1 | INS | 0 chars | Word Wrap")
            .WithStickyPosition(StickyPosition.Bottom)
            .Build();

        // Go-to-line prompt
        _gotoPrompt = new PromptControl
        {
            Prompt = "Go to line: ",
            StickyPosition = StickyPosition.Bottom,
            UnfocusOnEnter = true
        };
        _gotoPrompt.Entered += OnGotoLineEntered;

        // Build and show window
        new WindowBuilder(windowSystem)
            .WithTitle("Text Editor")
            .Maximized()
            .WithBackgroundColor(Color.Grey15)
            .WithForegroundColor(Color.White)
            .AddControl(menu)
            .AddControl(toolbar)
            .AddControl(_editor)
            .AddControl(_statusBar)
            .AddControl(_gotoPrompt)
            .BuildAndShow();

        // Wire up events
        _editor.ContentChanged += (_, _) => UpdateStatusBar();
        _editor.CursorPositionChanged += (_, _) => UpdateStatusBar();
        _editor.OverwriteModeChanged += (_, _) => UpdateStatusBar();
        _editor.EditingModeChanged += (_, _) => UpdateStatusBar();

        UpdateStatusBar();
    }

    static MenuControl BuildMenu()
    {
        var menu = Controls.Menu()
            .Horizontal()
            .Sticky()
            .WithName("mainMenu")
            .AddItem("File", m => m
                .AddItem("New", "Ctrl+N", () =>
                {
                    if (_editor != null) _editor.Content = string.Empty;
                    _currentFilePath = null;
                    UpdateTitle();
                })
                .AddItem("Open...", "Ctrl+O", () => _ = OpenFileAsync())
                .AddItem("Save", "Ctrl+S", () => _ = SaveFileAsync(false))
                .AddItem("Save As...", "Ctrl+Shift+S", () => _ = SaveFileAsync(true))
                .AddSeparator()
                .AddItem("Exit", "Alt+F4", () => _windowSystem?.Shutdown(0)))
            .AddItem("Edit", m => m
                .AddItem("Undo", "Ctrl+Z", () => { /* handled by editor */ })
                .AddItem("Redo", "Ctrl+Y", () => { /* handled by editor */ })
                .AddSeparator()
                .AddItem("Go to Line...", "Ctrl+G", () => _gotoPrompt?.SetFocus(true)))
            .AddItem("View", m => m
                .AddItem("Toggle Line Numbers", () => ToggleLineNumbers())
                .AddItem("Toggle Whitespace", () => ToggleWhitespace())
                .AddItem("Toggle Highlight Line", () => ToggleHighlightLine())
                .AddSeparator()
                .AddItem("Wrap: None", () => SetWrapMode(WrapMode.NoWrap))
                .AddItem("Wrap: Character", () => SetWrapMode(WrapMode.Wrap))
                .AddItem("Wrap: Word", () => SetWrapMode(WrapMode.WrapWords)))
            .Build();

        menu.StickyPosition = StickyPosition.Top;
        return menu;
    }

    static void OnGotoLineEntered(object? sender, string input)
    {
        if (_editor == null) return;
        if (int.TryParse(input, out int lineNumber) && lineNumber >= 1)
        {
            _editor.GoToLine(lineNumber);
            _editor.SetFocus(true);
        }
        if (_gotoPrompt != null) _gotoPrompt.Input = string.Empty;
    }

    static void CycleWrapMode()
    {
        if (_editor == null) return;
        var next = _editor.WrapMode switch
        {
            WrapMode.NoWrap => WrapMode.Wrap,
            WrapMode.Wrap => WrapMode.WrapWords,
            _ => WrapMode.NoWrap
        };
        SetWrapMode(next);
    }

    static void SetWrapMode(WrapMode mode)
    {
        if (_editor == null) return;
        _editor.WrapMode = mode;
        if (_wrapButton != null)
        {
            _wrapButton.Text = mode switch
            {
                WrapMode.NoWrap => "Wrap: None",
                WrapMode.Wrap => "Wrap: Char",
                _ => "Wrap: Word"
            };
        }
        UpdateStatusBar();
    }

    static void ToggleLineNumbers()
    {
        if (_editor == null) return;
        _editor.ShowLineNumbers = !_editor.ShowLineNumbers;
        if (_lineNumButton != null)
            _lineNumButton.Text = _editor.ShowLineNumbers ? "Line #s: On" : "Line #s: Off";
    }

    static void ToggleWhitespace()
    {
        if (_editor == null) return;
        _editor.ShowWhitespace = !_editor.ShowWhitespace;
        if (_whitespaceButton != null)
            _whitespaceButton.Text = _editor.ShowWhitespace ? "Wspace: On" : "Wspace: Off";
    }

    static void ToggleHighlightLine()
    {
        if (_editor == null) return;
        _editor.HighlightCurrentLine = !_editor.HighlightCurrentLine;
        if (_highlightButton != null)
            _highlightButton.Text = _editor.HighlightCurrentLine ? "HiLine: On" : "HiLine: Off";
    }

    static async Task OpenFileAsync()
    {
        if (_windowSystem == null) return;
        var path = await FileDialogs.ShowFilePickerAsync(_windowSystem, filter: "*.cs;*.txt;*.*");
        if (path != null && _editor != null)
        {
            _editor.Content = File.ReadAllText(path);
            _currentFilePath = path;
            UpdateTitle();
        }
    }

    static async Task SaveFileAsync(bool saveAs)
    {
        if (_windowSystem == null || _editor == null) return;

        string? path = _currentFilePath;
        if (saveAs || path == null)
        {
            var defaultName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "untitled.cs";
            path = await FileDialogs.ShowSaveFileAsync(_windowSystem, defaultFileName: defaultName, filter: "*.cs;*.txt;*.*");
        }

        if (path != null)
        {
            File.WriteAllText(path, _editor.Content ?? string.Empty);
            _currentFilePath = path;
            UpdateTitle();
        }
    }

    static void UpdateTitle()
    {
        var window = _editor?.Container;
        while (window != null && window is not Window)
        {
            window = (window as IWindowControl)?.Container;
        }
        if (window is Window w)
        {
            var name = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "Untitled";
            w.Title = $"Text Editor - {name}";
        }
    }

    static void UpdateStatusBar()
    {
        if (_statusBar == null || _editor == null) return;

        var line = _editor.CurrentLine;
        var col = _editor.CurrentColumn;
        var mode = _editor.IsEditing
            ? (_editor.OverwriteMode ? "OVR" : "INS")
            : "BROWSE";
        var chars = _editor.Content?.Length ?? 0;
        var wrap = _editor.WrapMode switch
        {
            WrapMode.NoWrap => "No Wrap",
            WrapMode.Wrap => "Char Wrap",
            _ => "Word Wrap"
        };
        var hint = _editor.IsEditing ? "Esc to exit" : "Enter to edit";

        _statusBar.SetContent(new List<string>
        {
            $"Ln {line}, Col {col} | {mode} | {chars} chars | {wrap} | {hint}"
        });
    }
}
