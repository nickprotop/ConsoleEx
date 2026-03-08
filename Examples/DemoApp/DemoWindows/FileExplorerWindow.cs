using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using DemoApp.Helpers;

namespace DemoApp.DemoWindows;

public static class FileExplorerWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var startDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!Directory.Exists(startDir))
            startDir = Directory.GetCurrentDirectory();

        var tree = BuildDirectoryTree(startDir);
        var fileList = Controls.List("Files")
            .WithName("fileList")
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        PopulateFileList(fileList, startDir);

        tree.SelectedNodeChanged += (sender, args) =>
        {
            if (args.Node?.Tag is string path && Directory.Exists(path))
                PopulateFileList(fileList, path);
        };

        tree.NodeExpandCollapse += (sender, args) =>
        {
            if (args.Node is { IsExpanded: true, Tag: string path })
                LoadChildDirectories(args.Node, path);
        };

        var grid = Controls.HorizontalGrid()
            .Column(col => col.Width(35).Add(tree))
            .Column(col => col.Flex().Add(fileList))
            .WithSplitterAfter(0)
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        var statusBar = Controls.Markup($"[dim]{startDir}[/]")
            .StickyBottom()
            .WithName("statusBar")
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("File Explorer")
            .WithSize(90, 30)
            .Centered()
            .AddControls(grid, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
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

        var rootNode = builder.AddRootNode($"[yellow]{rootName}[/]");
        rootNode.Tag = rootPath;
        rootNode.IsExpanded = true;

        LoadChildDirectories(rootNode, rootPath);

        return builder.Build();
    }

    private static void LoadChildDirectories(TreeNode parentNode, string dirPath)
    {
        // Skip if children already loaded (not just the placeholder)
        if (parentNode.Children.Count > 0 &&
            !(parentNode.Children.Count == 1 && parentNode.Children[0].Text == "[dim]...[/]"))
            return;

        parentNode.ClearChildren();

        try
        {
            var dirs = Directory.GetDirectories(dirPath)
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .Take(50);

            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith('.'))
                    continue;

                var node = parentNode.AddChild($"[yellow]{name}[/]");
                node.Tag = dir;

                if (FileHelpers.HasSubdirectories(dir))
                    node.AddChild("[dim]...[/]");
            }
        }
        catch
        {
            parentNode.AddChild("[red]Access denied[/]");
        }
    }

    private static void PopulateFileList(ListControl fileList, string dirPath)
    {
        fileList.ClearItems();

        try
        {
            var files = Directory.GetFiles(dirPath)
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .Take(100);

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var ext = Path.GetExtension(file);
                var icon = FileHelpers.GetFileIcon(ext);
                var size = FileHelpers.FormatFileSize(new FileInfo(file).Length);
                fileList.AddItem(new ListItem($"{icon} {name}  [dim]{size}[/]") { Tag = file });
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
