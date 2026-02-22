using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using TreeNode = SharpConsoleUI.Controls.TreeNode;

namespace DotNetIDE;

public class ExplorerPanel
{
    private readonly ConsoleWindowSystem _ws;
    private readonly ProjectService _projectService;
    private readonly TreeControl _tree;
    private readonly ScrollablePanelControl _panel;

    public event EventHandler<string>? FileOpenRequested;

    public ExplorerPanel(ConsoleWindowSystem ws, ProjectService projectService)
    {
        _ws = ws;
        _projectService = projectService;

        _tree = new TreeControl
        {
            Guide = TreeGuide.Line,
            HighlightBackgroundColor = Color.SteelBlue,
            HighlightForegroundColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };

        _panel = new ScrollablePanelControl
        {
            ShowScrollbar = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Fill
        };
        _panel.AddControl(_tree);

        _tree.NodeActivated += OnNodeActivated;
        BuildTree();
    }

    public IWindowControl Control => _panel;

    public void Refresh()
    {
        _tree.Clear();
        BuildTree();
    }

    private void BuildTree()
    {
        var root = _projectService.BuildTree();
        AddNode(root, null);
    }

    private void AddNode(FileNode fileNode, TreeNode? parent)
    {
        string icon = GetIcon(fileNode);
        string label = $"{icon} {fileNode.Name}";
        TreeNode treeNode;
        if (parent == null)
        {
            treeNode = _tree.AddRootNode(label);
            treeNode.IsExpanded = true;
        }
        else
        {
            treeNode = parent.AddChild(label);
        }

        treeNode.Tag = fileNode.FullPath;

        if (fileNode.IsDirectory)
        {
            treeNode.TextColor = Color.Cyan1;
            foreach (var child in fileNode.Children)
                AddNode(child, treeNode);
        }
        else
        {
            treeNode.TextColor = GetFileColor(fileNode.Name);
        }
    }

    private static string GetIcon(FileNode node)
    {
        if (node.IsDirectory) return "📁";
        return Path.GetExtension(node.Name).ToLowerInvariant() switch
        {
            ".cs" => "📝",
            ".csproj" => "⚙",
            ".sln" => "🔧",
            ".json" => "📋",
            ".md" => "📖",
            ".yaml" or ".yml" => "📋",
            ".txt" => "📄",
            _ => "📄"
        };
    }

    private static Color GetFileColor(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".cs" => Color.LightGreen,
            ".csproj" => Color.Gold1,
            ".sln" => Color.Orange1,
            ".json" => Color.Yellow,
            ".md" => Color.Magenta1,
            _ => Color.White
        };

    private void OnNodeActivated(object? sender, TreeNodeEventArgs args)
    {
        if (args.Node?.Tag is string path && File.Exists(path))
            FileOpenRequested?.Invoke(this, path);
    }
}
