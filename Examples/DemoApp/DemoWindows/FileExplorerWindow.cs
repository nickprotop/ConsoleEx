using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using DemoApp.Helpers;

namespace DemoApp.DemoWindows;

public static class FileExplorerWindow
{
    private const int WindowWidth = 90;
    private const int WindowHeight = 30;
    private const int TreeColumnWidth = 35;
    private const int MaxDirectoryEntries = 50;
    private const int MaxFileEntries = 100;
    private const string LazyPlaceholder = "[dim]Loading...[/]";
    private const string DateFormat = "yyyy-MM-dd HH:mm";

    public static Window Create(ConsoleWindowSystem ws)
    {
        var startDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!Directory.Exists(startDir))
            startDir = Directory.GetCurrentDirectory();

        var currentDir = startDir;

        var pathBar = Controls.Markup($"[bold] {startDir}[/]")
            .StickyTop()
            .WithName("pathBar")
            .Build();

        var tree = BuildDirectoryTree(startDir);

        var scrollPanel = Controls.ScrollablePanel()
            .AddControl(tree)
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        var fileList = Controls.List("Files")
            .WithName("fileList")
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        PopulateFileList(fileList, startDir);

        var statusBar = Controls.Markup(FormatStatusText(startDir, fileList.Items.Count))
            .StickyBottom()
            .WithName("statusBar")
            .Build();

        tree.SelectedNodeChanged += (sender, args) =>
        {
            if (args.Node?.Tag is string path && Directory.Exists(path))
            {
                currentDir = path;
                pathBar.SetContent(new List<string> { $"[bold] {path}[/]" });
                PopulateFileList(fileList, path);
                statusBar.SetContent(new List<string> { FormatStatusText(path, fileList.Items.Count) });
            }
        };

        tree.NodeExpandCollapse += (sender, args) =>
        {
            if (args.Node is { IsExpanded: true, Tag: string path })
                LoadChildDirectories(args.Node, path);
        };

        var grid = Controls.HorizontalGrid()
            .Column(col => col.Width(TreeColumnWidth).Add(scrollPanel))
            .Column(col => col.Flex().Add(fileList))
            .WithSplitterAfter(0)
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("File Explorer")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .AddControls(pathBar, grid, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
                else if (e.KeyInfo.Key == ConsoleKey.Backspace)
                {
                    var parent = Path.GetDirectoryName(currentDir);
                    if (parent != null && Directory.Exists(parent))
                    {
                        currentDir = parent;
                        pathBar.SetContent(new List<string> { $"[bold] {parent}[/]" });
                        PopulateFileList(fileList, parent);
                        statusBar.SetContent(new List<string> { FormatStatusText(parent, fileList.Items.Count) });
                    }
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }

    private static string FormatStatusText(string dirPath, int fileCount)
    {
        return $"[dim]{dirPath} - {fileCount} file{(fileCount == 1 ? "" : "s")}[/]";
    }

    private static TreeControl BuildDirectoryTree(string rootPath)
    {
        var builder = Controls.Tree()
            .WithGuide(TreeGuide.Line)
            .WithName("dirTree")
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill);

        var rootName = Path.GetFileName(rootPath);
        if (string.IsNullOrEmpty(rootName))
            rootName = rootPath;

        var rootNode = builder.AddRootNode($"[cyan]{rootName}[/]");
        rootNode.Tag = rootPath;
        rootNode.IsExpanded = true;

        AddLazyChildren(rootNode, rootPath);

        return builder.Build();
    }

    private static void AddLazyChildren(TreeNode parentNode, string dirPath)
    {
        try
        {
            var dirs = Directory.GetDirectories(dirPath)
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .Take(MaxDirectoryEntries);

            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith('.'))
                    continue;

                var node = parentNode.AddChild($"[cyan]{name}[/]");
                node.Tag = dir;
                node.IsExpanded = false;

                if (FileHelpers.HasSubdirectories(dir))
                    node.AddChild(LazyPlaceholder);
            }
        }
        catch
        {
            parentNode.AddChild("[red]Access denied[/]");
        }
    }

    private static void LoadChildDirectories(TreeNode parentNode, string dirPath)
    {
        if (parentNode.Children.Count > 0 &&
            !(parentNode.Children.Count == 1 && parentNode.Children[0].Text == LazyPlaceholder))
            return;

        parentNode.ClearChildren();
        AddLazyChildren(parentNode, dirPath);
    }

    private static void PopulateFileList(ListControl fileList, string dirPath)
    {
        fileList.ClearItems();

        try
        {
            var files = Directory.GetFiles(dirPath)
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .Take(MaxFileEntries);

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var name = info.Name;
                var ext = info.Extension;
                var icon = FileHelpers.GetFileIcon(ext);
                var size = FileHelpers.FormatFileSize(info.Length);
                var dateStr = info.LastWriteTime.ToString(DateFormat);
                fileList.AddItem(new ListItem($"{icon} {name}  [dim]{size}  {dateStr}[/]") { Tag = file });
            }

            if (fileList.Items.Count == 0)
                fileList.AddItem(new ListItem("[dim](empty directory)[/]"));
        }
        catch
        {
            fileList.AddItem(new ListItem("[red]Access denied[/]"));
        }
    }
}
