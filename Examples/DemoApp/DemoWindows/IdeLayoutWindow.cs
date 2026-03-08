using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class IdeLayoutWindow
{
    #region Constants

    private const int WindowWidth = 95;
    private const int WindowHeight = 32;
    private const int LeftPanelWidth = 25;
    private const int LineNumberColumnWidth = 4;
    private const int EditorHeightOffset = 12;
    private const int LineNumberCount = 15;
    private const int FileStatusColumnWidth = 20;
    private const int PositionColumnWidth = 15;
    private const int EncodingColumnWidth = 12;
    private const int LanguageColumnWidth = 10;
    private const int TimeColumnWidth = 20;
    private const int ThreadUpdateIntervalMs = 1000;
    private const int SaveFeedbackDelayMs = 2000;
    private const int Tab1Width = 15;
    private const int Tab2Width = 12;
    private const int Tab3Width = 14;

    #endregion

    #region State

    private static string _currentFile = "Program.cs";
    private static string _currentLanguage = "C#";
    private static bool _hasUnsavedChanges = true;

    private static MarkupControl? _fileStatus;
    private static MarkupControl? _positionStatus;
    private static MarkupControl? _timeStatus;
    private static MarkupControl? _languageStatus;
    private static MultilineEditControl? _editorContent;

    #endregion

    #region Factory

    public static Window Create(ConsoleWindowSystem ws)
    {
        _currentFile = "Program.cs";
        _currentLanguage = "C#";
        _hasUnsavedChanges = true;

        var treeBuilder = Controls.Tree()
            .WithGuide(TreeGuide.Line)
            .WithHighlightColors(Color.White, Color.Blue)
            .WithMargin(1, 1, 1, 1)
            .WithAlignment(HorizontalAlignment.Left)
            .OnSelectedNodeChanged((sender, args) =>
            {
                if (args.Node != null)
                    UpdateFileStatus($"Selected: [yellow]{args.Node.Text}[/]");
            });

        BuildProjectStructure(treeBuilder);
        var projectTree = treeBuilder.Build();

        _editorContent = Controls.MultilineEdit()
            .WithContent(GetProgramCsContent())
            .WithWrapMode(WrapMode.Wrap)
            .AsReadOnly(false)
            .WithAlignment(HorizontalAlignment.Left)
            .Build();

        _fileStatus = Controls.Markup(GetFileStatusDisplay()).Build();
        _positionStatus = Controls.Markup("Ln 1, Col 1").Build();
        _languageStatus = Controls.Markup(_currentLanguage).Build();
        _timeStatus = Controls.Markup($"{DateTime.Now:HH:mm:ss}")
            .WithAlignment(HorizontalAlignment.Right)
            .Build();

        var lineNumbers = Controls.Markup()
            .WithAlignment(HorizontalAlignment.Right);
        for (int i = 1; i <= LineNumberCount; i++)
            lineNumbers.AddLine($"[dim]{i,3}[/]");
        var lineNumbersControl = lineNumbers.Build();

        var tab1 = Controls.Button("Program.cs x").WithWidth(Tab1Width).OnClick((s, b) => SwitchToFile("Program.cs", "C#")).Build();
        var tab2 = Controls.Button("User.cs").WithWidth(Tab2Width).OnClick((s, b) => SwitchToFile("User.cs", "C#")).Build();
        var tab3 = Controls.Button("README.md").WithWidth(Tab3Width).OnClick((s, b) => SwitchToFile("README.md", "Markdown")).Build();

        var editorTabs = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(c => c.Add(tab1))
            .Column(c => c.Add(tab2))
            .Column(c => c.Add(tab3))
            .Build();

        var editorArea = Controls.HorizontalGrid()
            .Column(c => c.Width(LineNumberColumnWidth).Add(lineNumbersControl))
            .Column(c => c.Add(_editorContent))
            .Build();

        var mainContent = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(c => c.Width(LeftPanelWidth)
                .Add(Controls.Markup("[bold cyan]Project Explorer[/]").Centered().Build())
                .Add(projectTree))
            .Column(c => c
                .Add(editorTabs)
                .Add(Controls.Rule())
                .Add(editorArea))
            .WithSplitterAfter(0)
            .Build();

        var statusBar = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Left)
            .StickyBottom()
            .Column(c => c.Width(FileStatusColumnWidth).Add(_fileStatus))
            .Column(c => c.Width(PositionColumnWidth).Add(_positionStatus))
            .Column(c => c.Width(EncodingColumnWidth).Add(Controls.Label("UTF-8")))
            .Column(c => c.Width(LanguageColumnWidth).Add(_languageStatus))
            .Column(c => c.Add(Controls.Label("")))
            .Column(c => c.Width(TimeColumnWidth).Add(_timeStatus))
            .Build();

        Window? window = null;
        window = new WindowBuilder(ws)
            .WithTitle("IDE Demo - Complete Application UI")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithAsyncWindowThread(WindowThreadAsync)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow(window!);
                    e.Handled = true;
                }
            })
            .OnResize((sender, args) =>
            {
                if (_editorContent != null && window != null)
                    _editorContent.ViewportHeight = window.Height - EditorHeightOffset;
            })
            .AddControl(BuildMenuBar(ws, () => window!))
            .AddControl(new RuleControl { StickyPosition = StickyPosition.Top })
            .AddControl(BuildToolbar(ws, () => window!))
            .AddControl(new RuleControl { StickyPosition = StickyPosition.Top })
            .AddControl(mainContent)
            .AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom })
            .AddControl(statusBar)
            .BuildAndShow();

        return window;
    }

    #endregion

    #region Menu Bar

    private static MenuControl BuildMenuBar(ConsoleWindowSystem ws, Func<Window> getWindow)
    {
        return Controls.Menu()
            .Horizontal()
            .WithName("mainMenu")
            .Sticky()
            .AddItem("File", m => m
                .AddItem("New", "Ctrl+N", () => HandleNewFile())
                .AddItem("Open", "Ctrl+O", () => UpdateFileStatus("Open file dialog..."))
                .AddSeparator()
                .AddItem("Save", "Ctrl+S", () => HandleSaveFile())
                .AddItem("Save As...", "Ctrl+Shift+S", () => UpdateFileStatus("Save As dialog..."))
                .AddSeparator()
                .AddItem("Exit", "Alt+F4", () => ws.CloseWindow(getWindow())))
            .AddItem("Edit", m => m
                .AddItem("Undo", "Ctrl+Z", () => UpdateFileStatus("[blue]<-[/] Undo"))
                .AddItem("Redo", "Ctrl+Y", () => UpdateFileStatus("[blue]->[/] Redo"))
                .AddSeparator()
                .AddItem("Cut", "Ctrl+X", () => UpdateFileStatus("Cut to clipboard"))
                .AddItem("Copy", "Ctrl+C", () => UpdateFileStatus("Copy to clipboard"))
                .AddItem("Paste", "Ctrl+V", () => UpdateFileStatus("Paste from clipboard"))
                .AddSeparator()
                .AddItem("Find", "Ctrl+F", () => UpdateFileStatus("Find..."))
                .AddItem("Replace", "Ctrl+H", () => UpdateFileStatus("Replace...")))
            .AddItem("View", m => m
                .AddItem("Toggle Sidebar", "Ctrl+B", () => UpdateFileStatus("Sidebar toggled"))
                .AddItem("Toggle Panel", "Ctrl+J", () => UpdateFileStatus("Panel toggled"))
                .AddSeparator()
                .AddItem("Zoom In", "Ctrl++", () => UpdateFileStatus("Zoom In"))
                .AddItem("Zoom Out", "Ctrl+-", () => UpdateFileStatus("Zoom Out")))
            .AddItem("Tools", m => m
                .AddItem("Options", () => UpdateFileStatus("Options dialog..."))
                .AddItem("Preferences", () => UpdateFileStatus("Preferences dialog...")))
            .AddItem("Help", m => m
                .AddItem("Documentation", "F1", () => UpdateFileStatus("Documentation..."))
                .AddItem("About", () => UpdateFileStatus("About IDE Demo...")))
            .Build();
    }

    #endregion

    #region Toolbar

    private static ToolbarControl BuildToolbar(ConsoleWindowSystem ws, Func<Window> getWindow)
    {
        return Controls.Toolbar()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Left)
            .AddButton("New", (s, b) => HandleNewFile())
            .AddButton("Open", (s, b) => UpdateFileStatus("Open file dialog..."))
            .AddButton("Save", (s, b) => HandleSaveFile())
            .AddButton("Undo", (s, b) => UpdateFileStatus("[blue]<-[/] Undo"))
            .AddButton("Redo", (s, b) => UpdateFileStatus("[blue]->[/] Redo"))
            .Build();
    }

    #endregion

    #region Window Thread

    private static async Task WindowThreadAsync(Window window, CancellationToken ct)
    {
        var random = new Random();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                int line = random.Next(1, 25);
                int col = random.Next(1, 80);

                _positionStatus?.SetContent(new List<string> { $"Ln {line}, Col {col}" });
                _timeStatus?.SetContent(new List<string> { $"{DateTime.Now:HH:mm:ss}" });

                await Task.Delay(ThreadUpdateIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    #endregion

    #region Event Handlers

    private static void HandleNewFile()
    {
        if (_editorContent != null)
        {
            _editorContent.SetContent("// New file created\n");
            _currentFile = "Untitled";
            _hasUnsavedChanges = true;
            UpdateFileStatus($"[cyan]*[/] {_currentFile}");
        }
    }

    private static void HandleSaveFile()
    {
        _hasUnsavedChanges = false;
        UpdateFileStatus($"[green]+[/] {_currentFile}");

        Task.Delay(SaveFeedbackDelayMs).ContinueWith(_ =>
        {
            _hasUnsavedChanges = true;
            _fileStatus?.SetContent(new List<string> { GetFileStatusDisplay() });
        });
    }

    private static void SwitchToFile(string filename, string language)
    {
        _currentFile = filename;
        _currentLanguage = language;
        _hasUnsavedChanges = true;

        _languageStatus?.SetContent(new List<string> { language });
        UpdateFileStatus(GetFileStatusDisplay());

        if (_editorContent != null)
        {
            _editorContent.SetContent(filename switch
            {
                "User.cs" => GetUserCsContent(),
                "README.md" => GetReadmeContent(),
                _ => GetProgramCsContent()
            });
        }
    }

    private static void UpdateFileStatus(string message)
    {
        _fileStatus?.SetContent(new List<string> { message });
    }

    #endregion

    #region Project Tree

    private static void BuildProjectStructure(TreeControlBuilder builder)
    {
        var projectRoot = builder.AddRootNode("MyProject");
        projectRoot.TextColor = Color.Yellow;
        projectRoot.IsExpanded = true;

        var srcFolder = projectRoot.AddChild("src");
        srcFolder.TextColor = Color.Cyan1;
        srcFolder.IsExpanded = true;

        srcFolder.AddChild("Program.cs").TextColor = Color.Green;

        var modelsFolder = srcFolder.AddChild("Models");
        modelsFolder.TextColor = Color.Cyan1;
        modelsFolder.AddChild("User.cs").TextColor = Color.Green;
        modelsFolder.AddChild("Product.cs").TextColor = Color.Green;

        var testsFolder = projectRoot.AddChild("Tests");
        testsFolder.TextColor = Color.Cyan1;
        testsFolder.AddChild("UserTests.cs").TextColor = Color.Yellow;

        var docsFolder = projectRoot.AddChild("docs");
        docsFolder.TextColor = Color.Cyan1;
        docsFolder.AddChild("README.md").TextColor = Color.Magenta1;
    }

    #endregion

    #region Display Helpers

    private static string GetFileStatusDisplay()
    {
        var indicator = _hasUnsavedChanges ? "[green]*[/]" : "[green] [/]";
        return $"{indicator} {_currentFile}";
    }

    #endregion

    #region File Contents

    private static string GetProgramCsContent() => @"using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace MyProject
{
    /// <summary>
    /// Main program entry point demonstrating modern C# patterns
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine(""Hello, World!"");

            // Modern async patterns
            await ProcessDataAsync();

            Console.WriteLine(""Application completed successfully."");
        }

        private static async Task ProcessDataAsync()
        {
            // Simulate async work
            await Task.Delay(100);
            Console.WriteLine(""Processing complete."");
        }
    }
}";

    private static string GetUserCsContent() => @"using System;

namespace MyProject.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public User()
        {
            CreatedAt = DateTime.UtcNow;
        }
    }
}";

    private static string GetReadmeContent() => @"# MyProject

A comprehensive example demonstrating modern C# patterns and SharpConsoleUI features.

## Features

- Modern async/await patterns
- Dependency injection
- Clean architecture
- Comprehensive testing

## Getting Started

1. Clone the repository
2. Run `dotnet restore`
3. Run `dotnet build`
4. Run `dotnet run`

## Documentation

See the docs folder for detailed documentation.
";

    #endregion
}
