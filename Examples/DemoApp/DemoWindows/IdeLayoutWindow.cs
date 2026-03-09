using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class IdeLayoutWindow
{
    #region Constants

    private const int WindowWidth = 95;
    private const int WindowHeight = 32;
    private const int ExplorerColumnWidth = 26;
    private const int SidePanelColumnWidth = 28;
    private const int FileStatusColumnWidth = 20;
    private const int PositionColumnWidth = 15;
    private const int EncodingColumnWidth = 10;
    private const int LanguageColumnWidth = 12;
    private const int TimeColumnWidth = 18;
    private const int ThreadUpdateIntervalMs = 1000;
    private const int TreeMargin = 1;

    #endregion

    #region Factory

    public static Window Create(ConsoleWindowSystem ws)
    {
        // Local state - no static mutable fields
        var currentFile = "Program.cs";

        // Status bar controls
        var fileStatus = Controls.Markup(GetFileStatusDisplay(true, "Program.cs")).Build();
        var positionStatus = Controls.Markup("Ln 1, Col 1").Build();
        var languageStatus = Controls.Markup("C#").Build();
        var timeStatus = Controls.Markup($"{DateTime.Now:HH:mm:ss}")
            .WithAlignment(HorizontalAlignment.Right)
            .Build();

        // Helper closures over local state
        void UpdateFileStatus(string message) =>
            fileStatus.SetContent(new List<string> { message });

        void HandleNewFile()
        {
            currentFile = "Untitled";
            UpdateFileStatus($"[cyan]*[/] {currentFile}");
        }

        void HandleSaveFile()
        {
            UpdateFileStatus($"[green]+[/] {currentFile}");
            ws.NotificationStateService.ShowNotification(
                "Saved", $"{currentFile} saved successfully", NotificationSeverity.Success);
        }

        // Build editor tab contents
        var editor1 = Controls.MultilineEdit()
            .WithContent(GetProgramCsContent())
            .WithWrapMode(WrapMode.Wrap)
            .AsReadOnly(false)
            .WithAlignment(HorizontalAlignment.Left)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var editor2 = Controls.MultilineEdit()
            .WithContent(GetUserCsContent())
            .WithWrapMode(WrapMode.Wrap)
            .AsReadOnly(false)
            .WithAlignment(HorizontalAlignment.Left)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var editor3 = Controls.MultilineEdit()
            .WithContent(GetReadmeContent())
            .WithWrapMode(WrapMode.Wrap)
            .AsReadOnly(false)
            .WithAlignment(HorizontalAlignment.Left)
            .WithVerticalAlignment(VerticalAlignment.Fill)            
            .Build();

        var editorTabs = Controls.TabControl()
            .AddTab("Program.cs", editor1)
            .AddTab("User.cs", editor2)
            .AddTab("README.md", editor3)
            .WithHeaderStyle(TabHeaderStyle.AccentedSeparator)
            .Fill()
            .Build();

        // Project tree in scrollable panel
        var treeBuilder = Controls.Tree()
            .WithGuide(TreeGuide.Line)
            .WithHighlightColors(Color.White, Color.Blue)
            .WithMargin(TreeMargin, TreeMargin, TreeMargin, TreeMargin)
            .WithAlignment(HorizontalAlignment.Left)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .OnSelectedNodeChanged((sender, args) =>
            {
                if (args.Node != null)
                    UpdateFileStatus($"Selected: [yellow]{args.Node.Text}[/]");
            });
        BuildProjectStructure(treeBuilder);
        var projectTree = treeBuilder.Build();

        var explorerPanel = Controls.ScrollablePanel()
            .AddControl(projectTree)
            .WithScrollbar(true)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        // Side panel content (outline / properties)
        var outlineTree = Controls.Tree()
            .WithGuide(TreeGuide.Line)
            .WithMargin(TreeMargin, TreeMargin, TreeMargin, TreeMargin)
            .WithAlignment(HorizontalAlignment.Left);
        BuildOutlineStructure(outlineTree);

        var sidePanelTabs = Controls.TabControl()
            .AddTab("Outline", outlineTree.Build())
            .AddTab("Properties", Controls.Markup("[dim]No properties to show[/]").Build())
            .WithHeaderStyle(TabHeaderStyle.AccentedSeparator)
            .Fill()
            .Build();

        // 3-column layout with splitters (lazydotide pattern)
        var mainContent = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };

        var explorerCol = new ColumnContainer(mainContent)
        {
            Width = ExplorerColumnWidth,
            VerticalAlignment = VerticalAlignment.Fill
        };
        explorerCol.AddContent(explorerPanel);
        mainContent.AddColumn(explorerCol);

        var editorCol = new ColumnContainer(mainContent)
        {
            VerticalAlignment = VerticalAlignment.Fill
        };
        editorCol.AddContent(editorTabs);
        mainContent.AddColumn(editorCol);

        var sidePanelCol = new ColumnContainer(mainContent)
        {
            Width = SidePanelColumnWidth,
            VerticalAlignment = VerticalAlignment.Fill,
            Visible = false
        };
        sidePanelCol.AddContent(sidePanelTabs);
        mainContent.AddColumn(sidePanelCol);

        var explorerSplitter = new SplitterControl();
        mainContent.AddSplitter(0, explorerSplitter);

        var sidePanelSplitter = new SplitterControl { Visible = false };
        mainContent.AddSplitter(1, sidePanelSplitter);

        // Side panel toggle
        var sidePanelVisible = false;
        void ToggleSidePanel(Window w)
        {
            sidePanelVisible = !sidePanelVisible;
            sidePanelCol.Visible = sidePanelVisible;
            sidePanelSplitter.Visible = sidePanelVisible;
            w.ForceRebuildLayout();
            w.Invalidate(true);
        }

        // Status bar
        var statusBar = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Left)
            .StickyBottom()
            .Column(c => c.Width(FileStatusColumnWidth).Add(fileStatus))
            .Column(c => c.Width(PositionColumnWidth).Add(positionStatus))
            .Column(c => c.Width(EncodingColumnWidth).Add(Controls.Label("UTF-8")))
            .Column(c => c.Width(LanguageColumnWidth).Add(languageStatus))
            .Column(c => c.Add(Controls.Label("")))
            .Column(c => c.Width(TimeColumnWidth).Add(timeStatus))
            .Build();

        // Build window
        Window? window = null;
        window = new WindowBuilder(ws)
            .WithTitle("IDE Demo - Complete Application UI")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithAsyncWindowThread(async (w, ct) =>
            {
                var random = new Random();
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        int line = random.Next(1, 25);
                        int col = random.Next(1, 80);
                        positionStatus.SetContent(new List<string> { $"Ln {line}, Col {col}" });
                        timeStatus.SetContent(new List<string> { $"{DateTime.Now:HH:mm:ss}" });
                        await Task.Delay(ThreadUpdateIntervalMs, ct);
                    }
                    catch (OperationCanceledException) { break; }
                }
            })
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow(window!);
                    e.Handled = true;
                }
            })
            .AddControl(BuildMenuBar(ws, () => window!,
                HandleNewFile: HandleNewFile,
                HandleSaveFile: HandleSaveFile,
                UpdateFileStatus: UpdateFileStatus,
                ToggleSidePanel: () => ToggleSidePanel(window!)))
            .AddControl(new RuleControl { StickyPosition = StickyPosition.Top })
            .AddControl(BuildToolbar(
                HandleNewFile: HandleNewFile,
                HandleSaveFile: HandleSaveFile,
                HandleOpen: () => { _ = FileDialogs.ShowFilePickerAsync(ws); },
                UpdateFileStatus: UpdateFileStatus))
            .AddControl(new RuleControl { StickyPosition = StickyPosition.Top })
            .AddControl(mainContent)
            .AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom })
            .AddControl(statusBar)
            .BuildAndShow();

        return window;
    }

    #endregion

    #region Menu Bar

    private static MenuControl BuildMenuBar(
        ConsoleWindowSystem ws,
        Func<Window> getWindow,
        Action HandleNewFile,
        Action HandleSaveFile,
        Action<string> UpdateFileStatus,
        Action ToggleSidePanel)
    {
        MenuControl menuControl = Controls.Menu()
            .Horizontal()
            .WithName("mainMenu")
            .Sticky()
            .AddItem("File", m => m
                .AddItem("New", "Ctrl+N", HandleNewFile)
                .AddItem("Open", "Ctrl+O", () => { _ = FileDialogs.ShowFilePickerAsync(ws); })
                .AddSeparator()
                .AddItem("Save", "Ctrl+S", HandleSaveFile)
                .AddItem("Save As...", "Ctrl+Shift+S", () => { _ = FileDialogs.ShowSaveFileAsync(ws); })
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
                .AddItem("Toggle Side Panel", "Ctrl+B", ToggleSidePanel)
                .AddSeparator()
                .AddItem("Zoom In", "Ctrl++", () => UpdateFileStatus("Zoom In"))
                .AddItem("Zoom Out", "Ctrl+-", () => UpdateFileStatus("Zoom Out")))
            .AddItem("Tools", m => m
                .AddItem("Options", () => UpdateFileStatus("Options dialog..."))
                .AddItem("Preferences", () => UpdateFileStatus("Preferences dialog...")))
            .AddItem("Help", m => m
                .AddItem("Documentation", "F1", () => UpdateFileStatus("Documentation..."))
                .AddItem("About", () => AboutDialog.Show(ws)))
            .Build();
            
       menuControl.StickyPosition = StickyPosition.Top;
                
        return menuControl;
    }

    #endregion

    #region Toolbar

    private static ToolbarControl BuildToolbar(
        Action HandleNewFile,
        Action HandleSaveFile,
        Action HandleOpen,
        Action<string> UpdateFileStatus)
    {
        return Controls.Toolbar()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Left)
            .AddButton("New", (s, b) => HandleNewFile())
            .AddButton("Open", (s, b) => HandleOpen())
            .AddButton("Save", (s, b) => HandleSaveFile())
            .AddButton("Undo", (s, b) => UpdateFileStatus("[blue]<-[/] Undo"))
            .AddButton("Redo", (s, b) => UpdateFileStatus("[blue]->[/] Redo"))
            .StickyTop()
            .Build();
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

        var servicesFolder = srcFolder.AddChild("Services");
        servicesFolder.TextColor = Color.Cyan1;
        servicesFolder.AddChild("UserService.cs").TextColor = Color.Green;
        servicesFolder.AddChild("ProductService.cs").TextColor = Color.Green;

        var configFolder = projectRoot.AddChild("config");
        configFolder.TextColor = Color.Cyan1;
        configFolder.AddChild("appsettings.json").TextColor = Color.Yellow;
        configFolder.AddChild("launch.json").TextColor = Color.Yellow;

        var testsFolder = projectRoot.AddChild("Tests");
        testsFolder.TextColor = Color.Cyan1;
        testsFolder.AddChild("UserTests.cs").TextColor = Color.Green;
        testsFolder.AddChild("ProductTests.cs").TextColor = Color.Green;

        var docsFolder = projectRoot.AddChild("docs");
        docsFolder.TextColor = Color.Cyan1;
        docsFolder.AddChild("README.md").TextColor = Color.Magenta1;
        docsFolder.AddChild("CHANGELOG.md").TextColor = Color.Magenta1;
    }

    #endregion

    #region Outline Tree

    private static void BuildOutlineStructure(TreeControlBuilder builder)
    {
        var classNode = builder.AddRootNode("Program");
        classNode.TextColor = Color.Yellow;
        classNode.IsExpanded = true;

        classNode.AddChild("Main(string[])").TextColor = Color.Cyan1;
        classNode.AddChild("ProcessDataAsync()").TextColor = Color.Cyan1;

        var usingsNode = builder.AddRootNode("Usings");
        usingsNode.TextColor = Color.Grey;
        usingsNode.AddChild("System").TextColor = Color.Grey;
        usingsNode.AddChild("System.Threading.Tasks").TextColor = Color.Grey;
        usingsNode.AddChild("Microsoft.Extensions.DependencyInjection").TextColor = Color.Grey;
    }

    #endregion

    #region Display Helpers

    private static string GetFileStatusDisplay(bool hasUnsavedChanges, string currentFile)
    {
        var indicator = hasUnsavedChanges ? "[green]*[/]" : "[green] [/]";
        return $"{indicator} {currentFile}";
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
