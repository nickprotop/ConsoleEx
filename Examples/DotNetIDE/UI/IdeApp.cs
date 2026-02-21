using System.Collections.Concurrent;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace DotNetIDE;

public class IdeApp : IDisposable
{
    private readonly ConsoleWindowSystem _ws;
    private readonly ProjectService _projectService;
    private readonly BuildService _buildService;
    private readonly GitService _gitService;
    private LspClient? _lsp;

    private Window? _mainWindow;
    private Window? _outputWindow;
    private ExplorerPanel? _explorer;
    private EditorManager? _editorManager;
    private OutputPanel? _outputPanel;

    // Status bar MarkupControls
    private MarkupControl? _gitStatus;
    private MarkupControl? _errorCount;
    private MarkupControl? _cursorStatus;

    // Thread-safe queues for streaming build/test output
    private readonly ConcurrentQueue<string> _buildLines = new();
    private readonly ConcurrentQueue<string> _testLines = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public IdeApp(string projectPath)
    {
        _ws = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
        _projectService = new ProjectService(projectPath);
        _buildService = new BuildService();
        _gitService = new GitService();

        CreateLayout();

        // Async post-init: git status + optional LSP
        _ = PostInitAsync(projectPath);
    }

    public void Run() => _ws.Run();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _ws.ConsoleDriver.ScreenResized -= OnScreenResized;
        _ = _lsp?.DisposeAsync().AsTask();
    }

    // ──────────────────────────────────────────────────────────────
    // Layout
    // ──────────────────────────────────────────────────────────────

    private void CreateLayout()
    {
        var desktop = _ws.DesktopDimensions;
        int mainH = (int)(desktop.Height * 0.68);
        int outH = desktop.Height - mainH;

        _explorer = new ExplorerPanel(_ws, _projectService);
        _editorManager = new EditorManager(_ws);
        _outputPanel = new OutputPanel(_ws);

        BuildMainWindow(desktop.Width, mainH);
        BuildOutputWindow(desktop.Width, outH, mainH);

        _ws.AddWindow(_mainWindow!);
        _ws.AddWindow(_outputWindow!);

        _ws.ConsoleDriver.ScreenResized += OnScreenResized;

        // Open the shell tab at startup so it's ready immediately
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            _outputPanel!.LaunchShell();

        WireEvents();
    }

    private void BuildMainWindow(int width, int height)
    {
        _mainWindow = new WindowBuilder(_ws)
            .HideTitle()
            .Borderless()
            .WithSize(width, height)
            .AtPosition(0, 0)
            .Resizable(false)
            .Movable(false)
            .Minimizable(false)
            .Maximizable(false)
            .WithAsyncWindowThread(MainWindowThreadAsync)
            .Build();

        AddMenuBar();
        AddToolbar();
        _mainWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });
        AddMainContentArea();
        _mainWindow.AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom });
        AddStatusBar();
    }

    private void AddMenuBar()
    {
        var menu = Controls.Menu()
            .Horizontal()
            .Sticky()
            .AddItem("File", m => m
                .AddItem("Save", "Ctrl+S", () => _editorManager?.SaveCurrent())
                .AddItem("Close Tab", "Ctrl+W", () => CloseCurrentTab())
                .AddSeparator()
                .AddItem("Refresh Explorer", () => _explorer?.Refresh())
                .AddSeparator()
                .AddItem("Exit", "Alt+F4", () => _ws.Shutdown(0)))
            .AddItem("Build", m => m
                .AddItem("Build", "F6", () => _ = BuildProjectAsync())
                .AddItem("Test", "F7", () => _ = TestProjectAsync())
                .AddSeparator()
                .AddItem("Clean", () => _ = CleanProjectAsync())
                .AddItem("Stop", "F4", () => _buildService.Cancel()))
            .AddItem("Run", m => m
                .AddItem("Run", "F5", () => RunProject())
                .AddItem("Stop", "F4", () => _buildService.Cancel()))
            .AddItem("Git", m => m
                .AddItem("Refresh Status", () => _ = RefreshGitStatusAsync())
                .AddItem("Pull", () => _ = GitCommandAsync("pull"))
                .AddItem("Push", () => _ = GitCommandAsync("push")))
            .AddItem("Tools", m => m
                .AddItem("Add NuGet Package", () => ShowNuGetDialog())
                .AddSeparator()
                .AddItem("Shell", "F8", () => OpenShell()))
            .Build();

        menu.StickyPosition = StickyPosition.Top;
        _mainWindow!.AddControl(menu);
    }

    private void AddToolbar()
    {
        var toolbar = Controls.Toolbar()
            .AddButton("Run F5", (_, _) => RunProject())
            .AddButton("Build F6", (_, _) => _ = BuildProjectAsync())
            .AddButton("Test F7", (_, _) => _ = TestProjectAsync())
            .AddButton("Stop F4", (_, _) => _buildService.Cancel())
            .AddSeparator()
            .AddButton("Shell F8", (_, _) => OpenShell())
            .StickyTop()
            .Build();

        _mainWindow!.AddControl(toolbar);
    }

    private void AddMainContentArea()
    {
        var mainContent = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };

        var explorerCol = new ColumnContainer(mainContent)
        {
            Width = 26,
            VerticalAlignment = VerticalAlignment.Fill
        };
        explorerCol.AddContent(_explorer!.Control);
        mainContent.AddColumn(explorerCol);

        var editorCol = new ColumnContainer(mainContent)
        {
            VerticalAlignment = VerticalAlignment.Fill
        };
        editorCol.AddContent(_editorManager!.TabControl);
        mainContent.AddColumn(editorCol);

        mainContent.AddSplitter(0, new SplitterControl());

        _mainWindow!.AddControl(mainContent);
    }

    private void AddStatusBar()
    {
        var statusBar = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            StickyPosition = StickyPosition.Bottom
        };

        var gitCol = new ColumnContainer(statusBar) { Width = 40 };
        _gitStatus = new MarkupControl(new List<string> { "[dim] git: --[/]" });
        gitCol.AddContent(_gitStatus);
        statusBar.AddColumn(gitCol);

        var sepCol1 = new ColumnContainer(statusBar) { Width = 2 };
        sepCol1.AddContent(new MarkupControl(new List<string> { "[dim]|[/]" }));
        statusBar.AddColumn(sepCol1);

        var errorCol = new ColumnContainer(statusBar) { Width = 22 };
        _errorCount = new MarkupControl(new List<string> { "" });
        errorCol.AddContent(_errorCount);
        statusBar.AddColumn(errorCol);

        // Spacer
        var spacer = new ColumnContainer(statusBar);
        spacer.AddContent(new MarkupControl(new List<string> { "" }));
        statusBar.AddColumn(spacer);

        var cursorCol = new ColumnContainer(statusBar) { Width = 22 };
        _cursorStatus = new MarkupControl(new List<string> { "Ln 1 Col 1 | UTF-8" })
        {
            HorizontalAlignment = HorizontalAlignment.Right
        };
        cursorCol.AddContent(_cursorStatus);
        statusBar.AddColumn(cursorCol);

        _mainWindow!.AddControl(statusBar);
    }

    private void BuildOutputWindow(int width, int height, int topOffset)
    {
        _outputWindow = new WindowBuilder(_ws)
            .HideTitle()
            .WithBorderStyle(BorderStyle.Single)
            .HideTitleButtons()
            .Closable(false)
            .WithSize(width, height)
            .AtPosition(0, topOffset)
            .Resizable(false)
            .Movable(false)
            .Minimizable(false)
            .Maximizable(false)
            .Build();

        _outputWindow.AddControl(_outputPanel!.TabControl);
    }

    // ──────────────────────────────────────────────────────────────
    // Event wiring
    // ──────────────────────────────────────────────────────────────

    private void WireEvents()
    {
        _explorer!.FileOpenRequested += (_, path) => _editorManager?.OpenFile(path);

        _editorManager!.CursorChanged += (_, pos) =>
        {
            _cursorStatus?.SetContent(new List<string>
            {
                $"Ln {pos.Line} Col {pos.Column} | UTF-8"
            });
        };

        _outputPanel!.DiagnosticNavigateRequested += (_, diag) =>
        {
            _editorManager?.OpenFile(diag.FilePath);
            _editorManager?.GoToLine(diag.Line);
            _outputPanel.SwitchToProblemsTab();
        };

        _mainWindow!.KeyPressed += OnMainWindowKeyPressed;
    }

    private void OnScreenResized(object? sender, SharpConsoleUI.Helpers.Size size)
    {
        var desktop = _ws.DesktopDimensions;
        int mainH = (int)(desktop.Height * 0.68);
        int outH = desktop.Height - mainH;
        _mainWindow?.SetSize(desktop.Width, mainH);
        _outputWindow?.SetSize(desktop.Width, outH);
        _outputWindow?.SetPosition(new Point(0, mainH));
    }

    private void OnMainWindowKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        var key = e.KeyInfo.Key;
        var mods = e.KeyInfo.Modifiers;

        if (key == ConsoleKey.S && mods == ConsoleModifiers.Control)
        {
            _editorManager?.SaveCurrent();
            e.Handled = true;
        }
        else if (key == ConsoleKey.W && mods == ConsoleModifiers.Control)
        {
            CloseCurrentTab();
            e.Handled = true;
        }
        else if (key == ConsoleKey.F5 && mods == 0)
        {
            RunProject();
            e.Handled = true;
        }
        else if (key == ConsoleKey.F6 && mods == 0)
        {
            _ = BuildProjectAsync();
            e.Handled = true;
        }
        else if (key == ConsoleKey.F7 && mods == 0)
        {
            _ = TestProjectAsync();
            e.Handled = true;
        }
        else if (key == ConsoleKey.F8 && mods == 0)
        {
            OpenShell();
            e.Handled = true;
        }
        else if (key == ConsoleKey.F4 && mods == 0)
        {
            _buildService.Cancel();
            e.Handled = true;
        }
        else if (key == ConsoleKey.K && mods == ConsoleModifiers.Control)
        {
            _ = ShowHoverAsync();
            e.Handled = true;
        }
        else if (key == ConsoleKey.Spacebar && mods == ConsoleModifiers.Control)
        {
            _ = ShowCompletionAsync();
            e.Handled = true;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Window thread — drains build/test line queues on UI thread
    // ──────────────────────────────────────────────────────────────

    private async Task MainWindowThreadAsync(Window window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_disposed)
        {
            try
            {
                while (_buildLines.TryDequeue(out var line))
                    _outputPanel?.AppendBuildLine(line);
                while (_testLines.TryDequeue(out var line))
                    _outputPanel?.AppendTestLine(line);

                await Task.Delay(80, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Post-init (async, runs after Run() starts)
    // ──────────────────────────────────────────────────────────────

    private async Task PostInitAsync(string projectPath)
    {
        await RefreshGitStatusAsync();

        var lspServer = LspDetector.Find(projectPath);
        if (lspServer != null)
        {
            _lsp = new LspClient();
            bool started = await _lsp.StartAsync(lspServer, projectPath);
            if (started)
            {
                _lsp.DiagnosticsReceived += OnLspDiagnostics;
                _ws.LogService.LogInfo("LSP server started: " + lspServer.Exe);
            }
            else
            {
                await _lsp.DisposeAsync();
                _lsp = null;
                _ws.LogService.LogInfo("LSP server unavailable — running without IntelliSense");
            }
        }
    }

    private void OnLspDiagnostics(object? sender, (string Uri, List<LspDiagnostic> Diags) args)
    {
        var mapped = args.Diags.Select(d => new BuildDiagnostic(
            FilePath: LspClient.UriToPath(args.Uri),
            Line: d.Range.Start.Line + 1,
            Column: d.Range.Start.Character + 1,
            Code: d.Code ?? "",
            Severity: d.Severity == 1 ? "error" : "warning",
            Message: d.Message)).ToList();

        _outputPanel?.PopulateLspDiagnostics(mapped);
        UpdateErrorCount(mapped);
    }

    // ──────────────────────────────────────────────────────────────
    // Build / Test / Run / Git
    // ──────────────────────────────────────────────────────────────

    private async Task BuildProjectAsync()
    {
        var target = _projectService.FindBuildTarget();
        if (target == null) return;

        _outputPanel?.ClearBuildOutput();
        _outputPanel?.SwitchToBuildTab();

        var result = await _buildService.BuildAsync(
            target,
            line => _buildLines.Enqueue(line),
            _cts.Token);

        _outputPanel?.PopulateProblems(result.Diagnostics);
        UpdateErrorCount(result.Diagnostics);
    }

    private async Task TestProjectAsync()
    {
        var target = _projectService.FindBuildTarget();
        if (target == null) return;

        _outputPanel?.ClearTestOutput();
        _outputPanel?.SwitchToTestTab();

        var result = await _buildService.TestAsync(
            target,
            line => _testLines.Enqueue(line),
            _cts.Token);

        UpdateErrorCount(result.Diagnostics);
    }

    private async Task CleanProjectAsync()
    {
        var target = _projectService.FindBuildTarget();
        if (target == null) return;

        _outputPanel?.ClearBuildOutput();
        _outputPanel?.SwitchToBuildTab();

        await _buildService.RunAsync(
            $"clean {target} --nologo",
            line => _buildLines.Enqueue(line),
            _cts.Token);
    }

    private void RunProject()
    {
        var target = _projectService.FindRunTarget();
        if (target == null)
        {
            _ws.LogService.LogInfo("No runnable project found");
            return;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            Controls.Terminal("dotnet").WithArgs("run", "--project", target).Open(_ws);
    }

    private void OpenShell()
    {
        if (!(OperatingSystem.IsLinux() || OperatingSystem.IsWindows())) return;
        var terminal = _outputPanel?.LaunchShell();
        if (terminal == null || _outputWindow == null) return;
        // Activate the output window so key events flow to it, then focus the terminal
        _ws.SetActiveWindow(_outputWindow);
        _outputWindow.FocusControl(terminal);
    }

    private async Task RefreshGitStatusAsync()
    {
        var branch = await _gitService.GetBranchAsync(_projectService.RootPath);
        var status = await _gitService.GetStatusSummaryAsync(_projectService.RootPath);

        var bar = new IdeStatusBar();

        if (string.IsNullOrEmpty(branch))
        {
            bar.AddSegment("[dim] git: none[/]", " git: none");
        }
        else
        {
            // Truncate very long branch names so they never wrap
            var displayBranch = branch.Length > 22
                ? branch[..19] + "..."
                : branch;

            if (string.IsNullOrEmpty(status))
            {
                bar.AddSegment($"[green] git:{Markup.Escape(displayBranch)}[/]",
                               $" git:{displayBranch}");
            }
            else
            {
                bar.AddSegment($"[yellow] git:{Markup.Escape(displayBranch)}[/]",
                               $" git:{displayBranch}")
                   .AddSegment($"[dim]  {Markup.Escape(status)}[/]",
                               $"  {status}");
            }
        }

        _gitStatus?.SetContent(new List<string> { bar.Render() });
    }

    private async Task GitCommandAsync(string command)
    {
        _outputPanel?.ClearBuildOutput();
        _outputPanel?.SwitchToBuildTab();

        await _buildService.RunAsync(
            $"git -C {_projectService.RootPath} {command}",
            line => _buildLines.Enqueue(line),
            _cts.Token);

        await RefreshGitStatusAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // Dialogs
    // ──────────────────────────────────────────────────────────────

    private void CloseCurrentTab()
    {
        if (_editorManager == null) return;

        // Auto-save if dirty (avoids modal blocking the event loop)
        if (_editorManager.IsCurrentTabDirty())
            _editorManager.SaveCurrent();

        _editorManager.CloseCurrentTab();
    }

    private void ShowNuGetDialog()
    {
        var (packageName, version) = NuGetDialog.Show(_ws);
        if (string.IsNullOrEmpty(packageName)) return;

        var target = _projectService.FindBuildTarget();
        if (target == null || !target.EndsWith(".csproj")) return;

        var cmdArgs = version != null
            ? $"add {target} package {packageName} --version {version}"
            : $"add {target} package {packageName}";

        _ = RunNuGetAsync(cmdArgs);
    }

    private async Task RunNuGetAsync(string args)
    {
        _outputPanel?.ClearBuildOutput();
        _outputPanel?.SwitchToBuildTab();

        await _buildService.RunAsync(
            "dotnet " + args,
            line => _buildLines.Enqueue(line),
            _cts.Token);
    }

    // ──────────────────────────────────────────────────────────────
    // LSP: Hover & Completion
    // ──────────────────────────────────────────────────────────────

    private CompletionPortal? _completionPortal;

    private async Task ShowHoverAsync()
    {
        if (_lsp == null || _editorManager?.CurrentEditor == null) return;
        var editor = _editorManager.CurrentEditor;
        var path = _editorManager.CurrentFilePath;
        if (path == null) return;

        var result = await _lsp.HoverAsync(path, editor.CurrentLine - 1, editor.CurrentColumn - 1);
        if (result == null || string.IsNullOrWhiteSpace(result.Contents)) return;

        // Show hover in a small floating window above the status bar
        var lines = result.Contents.Split('\n').Take(5).Select(Markup.Escape).ToList();
        if (lines.Count == 0) return;

        var desktop = _ws.DesktopDimensions;
        int hoverW = Math.Min(80, lines.Max(l => l.Length) + 4);
        int hoverH = lines.Count + 2;

        var hoverWindow = new WindowBuilder(_ws)
            .WithTitle("Type Info")
            .WithSize(hoverW, hoverH)
            .AtPosition(Math.Max(0, desktop.Width - hoverW - 2),
                        Math.Max(0, desktop.Height - hoverH - 4))
            .Closable(true)
            .Build();

        hoverWindow.AddControl(new MarkupControl(lines.Select(l => "  " + l).ToList()));
        _ws.AddWindow(hoverWindow);
    }

    private async Task ShowCompletionAsync()
    {
        if (_lsp == null || _editorManager?.CurrentEditor == null) return;
        var editor = _editorManager.CurrentEditor;
        var path = _editorManager.CurrentFilePath;
        if (path == null || _mainWindow == null) return;

        var items = await _lsp.CompletionAsync(path, editor.CurrentLine - 1, editor.CurrentColumn - 1);
        if (items.Count == 0) return;

        _completionPortal ??= new CompletionPortal();
        _completionPortal.Show(items);
    }

    // ──────────────────────────────────────────────────────────────
    // Status bar updates
    // ──────────────────────────────────────────────────────────────

    private void UpdateErrorCount(List<BuildDiagnostic> diagnostics)
    {
        int errors = diagnostics.Count(d => d.Severity == "error");
        int warnings = diagnostics.Count(d => d.Severity == "warning");

        string text = "";
        if (errors > 0 && warnings > 0)
            text = $"[red]● {errors} error{(errors != 1 ? "s" : "")}[/]  [yellow]▲ {warnings} warn[/]";
        else if (errors > 0)
            text = $"[red]● {errors} error{(errors != 1 ? "s" : "")}[/]";
        else if (warnings > 0)
            text = $"[yellow]▲ {warnings} warn[/]";
        else
            text = "[green]✓ Build OK[/]";

        _errorCount?.SetContent(new List<string> { text });
    }
}
