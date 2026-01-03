// -----------------------------------------------------------------------
// Comprehensive Layout Window - Complete Application UI Demo
// Demonstrates a complete UI form with proper window class architecture
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Spectre.Console;
using System.Diagnostics;
using SpectreColor = Spectre.Console.Color;

namespace ModernExample;

/// <summary>
/// Comprehensive layout window demonstrating complete application UI patterns
/// Shows how to build a proper window class with:
/// - Control properties for easy access
/// - Background thread for real-time updates
/// - Proper lifecycle management
/// - Event handling and cleanup
/// </summary>
public class ComprehensiveLayoutWindow : IDisposable
{
    private readonly ConsoleWindowSystem _windowSystem;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<ComprehensiveLayoutWindow>? _logger;

    // Window and main container
    private Window? _window;

    // Control properties for easy access throughout the class
    private HorizontalGridControl? _menuBar;
    private HorizontalGridControl? _toolbar;
    private TreeControl? _projectTree;
    private MultilineEditControl? _editorContent;
    private MarkupControl? _fileStatus;
    private MarkupControl? _positionStatus;
    private MarkupControl? _encodingStatus;
    private MarkupControl? _languageStatus;
    private MarkupControl? _timeStatus;
    private MarkupControl? _lineNumbers;

    // Menu and toolbar buttons
    private ButtonControl? _fileMenu;
    private ButtonControl? _editMenu;
    private ButtonControl? _viewMenu;
    private ButtonControl? _toolsMenu;
    private ButtonControl? _helpMenu;
    private ButtonControl? _newBtn;
    private ButtonControl? _openBtn;
    private ButtonControl? _saveBtn;
    private ButtonControl? _undoBtn;
    private ButtonControl? _redoBtn;

    // Tab buttons
    private ButtonControl? _tab1;
    private ButtonControl? _tab2;
    private ButtonControl? _tab3;

    // Background task for real-time updates
    private volatile bool _disposed = false;

    /// <summary>
    /// Current file being edited
    /// </summary>
    public string CurrentFile { get; private set; } = "Program.cs";

    /// <summary>
    /// Current cursor line
    /// </summary>
    public int CurrentLine { get; private set; } = 12;

    /// <summary>
    /// Current cursor column
    /// </summary>
    public int CurrentColumn { get; private set; } = 34;

    /// <summary>
    /// Whether the current file has unsaved changes
    /// </summary>
    public bool HasUnsavedChanges { get; private set; } = true;

    /// <summary>
    /// Initialize the comprehensive layout window
    /// </summary>
    /// <param name="windowSystem">The console window system</param>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    public ComprehensiveLayoutWindow(ConsoleWindowSystem windowSystem, IServiceProvider? serviceProvider = null)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        _serviceProvider = serviceProvider;
        _logger = serviceProvider?.GetService<ILogger<ComprehensiveLayoutWindow>>();

        CreateWindow();
        SetupControls();
        SetupEventHandlers();

        _logger?.LogInformation("Comprehensive layout window initialized with proper window thread");
    }

    /// <summary>
    /// Show the window
    /// </summary>
    public void Show()
    {
        if (_window != null)
        {
            _windowSystem.AddWindow(_window);
            _logger?.LogInformation("Comprehensive layout window shown");
        }
    }

    /// <summary>
    /// Create the main window with builder pattern
    /// </summary>
    private void CreateWindow()
    {
        _window = new Window(_windowSystem, WindowThreadMethod)
        {
            Title = "IDE Demo - Complete Application UI",
            Width = 95,
            Height = 32,
            Left = (Console.WindowWidth - 95) / 2,
            Top = (Console.WindowHeight - 32) / 2
        };
    }

    /// <summary>
    /// Window thread method - runs as long as the window is active
    /// This is the proper SharpConsoleUI pattern for window background tasks
    /// </summary>
    public void WindowThreadMethod(Window window)
    {
        var random = new Random();
        
        while (window.GetIsActive() && !_disposed)
        {
            try
            {
                // Update cursor position simulation
                CurrentLine = random.Next(1, 25);
                CurrentColumn = random.Next(1, 80);
                
                // Update position display
                if (_positionStatus != null)
                {
                    _positionStatus.SetContent(new List<string> { GetPositionDisplay() });
                }
                
                // Update time
                if (_timeStatus != null)
                {
                    _timeStatus.SetContent(new List<string> { $"{DateTime.Now:HH:mm:ss}" });
                }

                Thread.Sleep(1000); // Use Thread.Sleep in window thread
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in window thread");
                break;
            }
        }
        
        _logger?.LogInformation("Window thread completed for comprehensive layout window");
    }

    /// <summary>
    /// Setup all controls with proper hierarchy
    /// </summary>
    private void SetupControls()
    {
        if (_window == null) return;

        CreateMenuBar();
        CreateToolbar();
        CreateMainContentArea();
        CreateStatusBar();
    }

    /// <summary>
    /// Create the top menu bar (sticky top)
    /// </summary>
    private void CreateMenuBar()
    {
        if (_window == null) return;

        _menuBar = new HorizontalGridControl
        {
            Alignment = Alignment.Left,
            StickyPosition = StickyPosition.Top
        };

        // Create menu buttons
        _fileMenu = new ButtonControl { Text = " File ", Width = 8 };
        _editMenu = new ButtonControl { Text = " Edit ", Width = 8 };
        _viewMenu = new ButtonControl { Text = " View ", Width = 8 };
        _toolsMenu = new ButtonControl { Text = " Tools ", Width = 8 };
        _helpMenu = new ButtonControl { Text = " Help ", Width = 8 };

        // Add menu buttons to columns
        var fileMenuCol = new ColumnContainer(_menuBar);
        fileMenuCol.AddContent(_fileMenu);
        _menuBar.AddColumn(fileMenuCol);

        var editMenuCol = new ColumnContainer(_menuBar);
        editMenuCol.AddContent(_editMenu);
        _menuBar.AddColumn(editMenuCol);

        var viewMenuCol = new ColumnContainer(_menuBar);
        viewMenuCol.AddContent(_viewMenu);
        _menuBar.AddColumn(viewMenuCol);

        var toolsMenuCol = new ColumnContainer(_menuBar);
        toolsMenuCol.AddContent(_toolsMenu);
        _menuBar.AddColumn(toolsMenuCol);

        var helpMenuCol = new ColumnContainer(_menuBar);
        helpMenuCol.AddContent(_helpMenu);
        _menuBar.AddColumn(helpMenuCol);

        _window.AddControl(_menuBar);
        _window.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });
    }

    /// <summary>
    /// Create the toolbar (sticky top)
    /// </summary>
    private void CreateToolbar()
    {
        if (_window == null) return;

        _toolbar = new HorizontalGridControl
        {
            Alignment = Alignment.Left,
            StickyPosition = StickyPosition.Top
        };

        // Create toolbar buttons
        _newBtn = new ButtonControl { Text = "New", Width = 10 };
        _openBtn = new ButtonControl { Text = "Open", Width = 10 };
        _saveBtn = new ButtonControl { Text = "Save", Width = 10 };
        _undoBtn = new ButtonControl { Text = "Undo", Width = 10 };
        _redoBtn = new ButtonControl { Text = "Redo", Width = 10 };

        // Add toolbar buttons to columns
        var newBtnCol = new ColumnContainer(_toolbar);
        newBtnCol.AddContent(_newBtn);
        _toolbar.AddColumn(newBtnCol);

        var openBtnCol = new ColumnContainer(_toolbar);
        openBtnCol.AddContent(_openBtn);
        _toolbar.AddColumn(openBtnCol);

        var saveBtnCol = new ColumnContainer(_toolbar);
        saveBtnCol.AddContent(_saveBtn);
        _toolbar.AddColumn(saveBtnCol);

        var undoBtnCol = new ColumnContainer(_toolbar);
        undoBtnCol.AddContent(_undoBtn);
        _toolbar.AddColumn(undoBtnCol);

        var redoBtnCol = new ColumnContainer(_toolbar);
        redoBtnCol.AddContent(_redoBtn);
        _toolbar.AddColumn(redoBtnCol);

        _window.AddControl(_toolbar);
        _window.AddControl(new RuleControl { StickyPosition = StickyPosition.Top });
    }

    /// <summary>
    /// Create the main content area with splitter
    /// </summary>
    private void CreateMainContentArea()
    {
        if (_window == null) return;

        var mainContentArea = new HorizontalGridControl
        {
            Alignment = Alignment.Stretch  // Fill available space when window resizes
        };

        CreateLeftPanel(mainContentArea);
        CreateRightPanel(mainContentArea);

        // Add splitter between panels
        mainContentArea.AddSplitter(0, new SplitterControl());

        _window.AddControl(mainContentArea);
    }

    /// <summary>
    /// Create the left panel with project explorer
    /// </summary>
    private void CreateLeftPanel(HorizontalGridControl mainContentArea)
    {
        var leftPanel = new ColumnContainer(mainContentArea) { Width = 25 };

        leftPanel.AddContent(new MarkupControl(new List<string> { "[bold cyan]Project Explorer[/]" })
        {
            Alignment = Alignment.Center
        });

        _projectTree = new TreeControl
        {
            Margin = new Margin(1, 1, 1, 1),
            Alignment = Alignment.Left,
            Guide = TreeGuide.Line,
            HighlightBackgroundColor = SpectreColor.Blue,
            HighlightForegroundColor = SpectreColor.White
        };

        BuildProjectStructure();
        leftPanel.AddContent(_projectTree);
        mainContentArea.AddColumn(leftPanel);
    }

    /// <summary>
    /// Build the sample project structure in the tree
    /// </summary>
    private void BuildProjectStructure()
    {
        if (_projectTree == null) return;

        var projectRoot = _projectTree.AddRootNode("MyProject");
        projectRoot.TextColor = SpectreColor.Yellow;
        projectRoot.IsExpanded = true;

        var srcFolder = projectRoot.AddChild("src");
        srcFolder.TextColor = SpectreColor.Cyan1;
        srcFolder.IsExpanded = true;

        var mainFile = srcFolder.AddChild("Program.cs");
        mainFile.TextColor = SpectreColor.Green;

        var modelsFolder = srcFolder.AddChild("Models");
        modelsFolder.TextColor = SpectreColor.Cyan1;
        modelsFolder.AddChild("User.cs").TextColor = SpectreColor.Green;
        modelsFolder.AddChild("Product.cs").TextColor = SpectreColor.Green;

        var testsFolder = projectRoot.AddChild("Tests");
        testsFolder.TextColor = SpectreColor.Cyan1;
        testsFolder.AddChild("UserTests.cs").TextColor = SpectreColor.Yellow;

        var docsFolder = projectRoot.AddChild("docs");
        docsFolder.TextColor = SpectreColor.Cyan1;
        docsFolder.AddChild("README.md").TextColor = SpectreColor.Magenta1;
    }

    /// <summary>
    /// Create the right panel with editor area
    /// </summary>
    private void CreateRightPanel(HorizontalGridControl mainContentArea)
    {
        var rightPanel = new ColumnContainer(mainContentArea);

        CreateEditorTabs(rightPanel);
        CreateEditorArea(rightPanel);

        mainContentArea.AddColumn(rightPanel);
    }

    /// <summary>
    /// Create the editor tabs
    /// </summary>
    private void CreateEditorTabs(ColumnContainer rightPanel)
    {
        var editorTabs = new HorizontalGridControl { Alignment = Alignment.Stretch };

        _tab1 = new ButtonControl { Text = "Program.cs x", Width = 15 };
        _tab2 = new ButtonControl { Text = "User.cs", Width = 12 };
        _tab3 = new ButtonControl { Text = "README.md", Width = 14 };

        var tab1Col = new ColumnContainer(editorTabs);
        tab1Col.AddContent(_tab1);
        editorTabs.AddColumn(tab1Col);

        var tab2Col = new ColumnContainer(editorTabs);
        tab2Col.AddContent(_tab2);
        editorTabs.AddColumn(tab2Col);

        var tab3Col = new ColumnContainer(editorTabs);
        tab3Col.AddContent(_tab3);
        editorTabs.AddColumn(tab3Col);

        rightPanel.AddContent(editorTabs);
        rightPanel.AddContent(new RuleControl());
    }

    /// <summary>
    /// Create the editor area with line numbers and content
    /// </summary>
    private void CreateEditorArea(ColumnContainer rightPanel)
    {
        var editorArea = new HorizontalGridControl();

        // Line numbers column
        var lineNumbersCol = new ColumnContainer(editorArea) { Width = 4 };
        _lineNumbers = new MarkupControl(GenerateLineNumbers())
        {
            Alignment = Alignment.Right
        };
        lineNumbersCol.AddContent(_lineNumbers);
        editorArea.AddColumn(lineNumbersCol);

        // Editor content column
        var editorCol = new ColumnContainer(editorArea);
        _editorContent = new MultilineEditControl
        {
            ViewportHeight = _window?.Height - 12 ?? 20, // Account for menus and status
            WrapMode = WrapMode.Wrap,
            ReadOnly = false,
            Alignment = Alignment.Left
        };

        LoadSampleCode();
        editorCol.AddContent(_editorContent);
        editorArea.AddColumn(editorCol);

        rightPanel.AddContent(editorArea);
    }

    /// <summary>
    /// Generate line numbers for the editor
    /// </summary>
    private List<string> GenerateLineNumbers()
    {
        var lines = new List<string>();
        for (int i = 1; i <= 15; i++)
        {
            lines.Add($"[dim]{i,3}[/]");
        }
        return lines;
    }

    /// <summary>
    /// Load sample C# code into the editor
    /// </summary>
    private void LoadSampleCode()
    {
        if (_editorContent == null) return;

        _editorContent.SetContent(@"using System;
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
}");
    }

    /// <summary>
    /// Create the bottom status bar (sticky bottom)
    /// </summary>
    private void CreateStatusBar()
    {
        if (_window == null) return;

        _window.AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom });

        var statusBar = new HorizontalGridControl
        {
            Alignment = Alignment.Left,
            StickyPosition = StickyPosition.Bottom
        };

        var fileStatusCol = new ColumnContainer(statusBar) { Width = 20 };
        _fileStatus = new MarkupControl(new List<string> { GetFileStatusDisplay() });
        fileStatusCol.AddContent(_fileStatus);
        statusBar.AddColumn(fileStatusCol);

        var positionCol = new ColumnContainer(statusBar) { Width = 15 };
        _positionStatus = new MarkupControl(new List<string> { GetPositionDisplay() });
        positionCol.AddContent(_positionStatus);
        statusBar.AddColumn(positionCol);

        var encodingCol = new ColumnContainer(statusBar) { Width = 12 };
        _encodingStatus = new MarkupControl(new List<string> { "UTF-8" });
        encodingCol.AddContent(_encodingStatus);
        statusBar.AddColumn(encodingCol);

        var languageCol = new ColumnContainer(statusBar) { Width = 10 };
        _languageStatus = new MarkupControl(new List<string> { "C#" });
        languageCol.AddContent(_languageStatus);
        statusBar.AddColumn(languageCol);

        var spacerCol = new ColumnContainer(statusBar);
        spacerCol.AddContent(new MarkupControl(new List<string> { "" }));
        statusBar.AddColumn(spacerCol);

        var timeCol = new ColumnContainer(statusBar) { Width = 20 };
        _timeStatus = new MarkupControl(new List<string> { $"{DateTime.Now:HH:mm:ss}" })
        {
            Alignment = Alignment.Right
        };
        timeCol.AddContent(_timeStatus);
        statusBar.AddColumn(timeCol);

        _window.AddControl(statusBar);
    }

    /// <summary>
    /// Setup all event handlers
    /// </summary>
    private void SetupEventHandlers()
    {
        if (_window == null) return;

        SetupMenuHandlers();
        SetupToolbarHandlers();
        SetupTabHandlers();
        SetupTreeHandlers();
        SetupWindowHandlers();
    }

    /// <summary>
    /// Setup menu click handlers
    /// </summary>
    private void SetupMenuHandlers()
    {
        if (_fileMenu != null) _fileMenu.Click += (s, e) => UpdateFileStatus("File menu clicked");
        if (_editMenu != null) _editMenu.Click += (s, e) => UpdateFileStatus("Edit menu clicked");
        if (_viewMenu != null) _viewMenu.Click += (s, e) => UpdateFileStatus("View menu clicked");
        if (_toolsMenu != null) _toolsMenu.Click += (s, e) => UpdateFileStatus("Tools menu clicked");
        if (_helpMenu != null) _helpMenu.Click += (s, e) => UpdateFileStatus("Help menu clicked");
    }

    /// <summary>
    /// Setup toolbar click handlers
    /// </summary>
    private void SetupToolbarHandlers()
    {
        if (_newBtn != null) _newBtn.Click += HandleNewFile;
        if (_openBtn != null) _openBtn.Click += (s, e) => UpdateFileStatus("Open file dialog...");
        if (_saveBtn != null) _saveBtn.Click += HandleSaveFile;
        if (_undoBtn != null) _undoBtn.Click += (s, e) => UpdateFileStatus("[blue]<-[/] Undo");
        if (_redoBtn != null) _redoBtn.Click += (s, e) => UpdateFileStatus("[blue]->[/] Redo");
    }

    /// <summary>
    /// Setup tab click handlers
    /// </summary>
    private void SetupTabHandlers()
    {
        if (_tab1 != null) _tab1.Click += (s, e) => SwitchToFile("Program.cs", "C#");
        if (_tab2 != null) _tab2.Click += (s, e) => SwitchToFile("User.cs", "C#");
        if (_tab3 != null) _tab3.Click += (s, e) => SwitchToFile("README.md", "Markdown");
    }

    /// <summary>
    /// Setup project tree handlers
    /// </summary>
    private void SetupTreeHandlers()
    {
        if (_projectTree == null) return;

        _projectTree.OnSelectedNodeChanged = (tree, node) =>
        {
            if (node != null)
            {
                UpdateFileStatus($"Selected: [yellow]{node.Text}[/]");
                _logger?.LogDebug("Project tree node selected: {NodeText}", node.Text);
            }
        };
    }

    /// <summary>
    /// Setup window-level handlers
    /// </summary>
    private void SetupWindowHandlers()
    {
        if (_window == null) return;

        // Window resize handler
        _window.OnResize += (sender, args) =>
        {
            if (_editorContent != null)
            {
                _editorContent.ViewportHeight = _window.Height - 12;
            }
        };

        // ESC key handler
        _window.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    /// <summary>
    /// Handle new file creation
    /// </summary>
    private void HandleNewFile(object? sender, ButtonControl button)
    {
        if (_editorContent != null)
        {
            _editorContent.SetContent("// New file created\n");
            CurrentFile = "Untitled";
            HasUnsavedChanges = true;
            UpdateFileStatus($"[cyan]*[/] {CurrentFile}");
        }
    }

    /// <summary>
    /// Handle file save
    /// </summary>
    private void HandleSaveFile(object? sender, ButtonControl button)
    {
        HasUnsavedChanges = false;
        UpdateFileStatus($"[green]+[/] {CurrentFile}");
        
        Task.Delay(2000).ContinueWith(_ => 
        {
            HasUnsavedChanges = true;
            if (_fileStatus != null)
            {
                _fileStatus.SetContent(new List<string> { GetFileStatusDisplay() });
            }
        });
    }

    /// <summary>
    /// Switch to a different file tab
    /// </summary>
    private void SwitchToFile(string filename, string language)
    {
        CurrentFile = filename;
        HasUnsavedChanges = true;

        if (_languageStatus != null)
        {
            _languageStatus.SetContent(new List<string> { language });
        }

        UpdateFileStatus(GetFileStatusDisplay());

        // Load different content based on file
        if (_editorContent != null)
        {
            switch (filename)
            {
                case "User.cs":
                    _editorContent.SetContent(GetUserClassContent());
                    break;
                case "README.md":
                    _editorContent.SetContent(GetReadmeContent());
                    break;
                default:
                    LoadSampleCode();
                    break;
            }
        }

        _logger?.LogInformation("Switched to file: {FileName}", filename);
    }

    /// <summary>
    /// Get User.cs file content
    /// </summary>
    private string GetUserClassContent()
    {
        return @"using System;

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
    }

    /// <summary>
    /// Get README.md file content
    /// </summary>
    private string GetReadmeContent()
    {
        return @"# MyProject

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
    }

    /// <summary>
    /// Get the current position display
    /// </summary>
    private string GetPositionDisplay()
    {
        return $"Ln {CurrentLine}, Col {CurrentColumn}";
    }

    /// <summary>
    /// Update file status display
    /// </summary>
    private void UpdateFileStatus(string message)
    {
        if (_fileStatus != null)
        {
            _fileStatus.SetContent(new List<string> { message });
        }
    }

    /// <summary>
    /// Get the current file status display
    /// </summary>
    private string GetFileStatusDisplay()
    {
        var indicator = HasUnsavedChanges ? "[green]*[/]" : "[green] [/]";
        return $"{indicator} {CurrentFile}";
    }

    /// <summary>
    /// Close the window
    /// </summary>
    public void Close()
    {
        if (_window != null)
        {
            _windowSystem?.CloseWindow(_window);
            _logger?.LogInformation("Comprehensive layout window closed");
        }
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _logger?.LogInformation("Comprehensive layout window disposed");
    }
}