using System.IO;
using System.Collections.Generic;

namespace DotNetIDE;

public record FileNode(string Name, string FullPath, bool IsDirectory, List<FileNode> Children);

public class ProjectService
{
    private string _rootPath;

    private static readonly HashSet<string> SkippedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", "node_modules", ".vs", ".idea", "packages", ".nuget"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".sln", ".json", ".md", ".txt", ".yaml", ".yml",
        ".xml", ".config", ".props", ".targets", ".razor", ".cshtml",
        ".sh", ".bat", ".ps1", ".env", ".gitignore", ".editorconfig"
    };

    public ProjectService(string rootPath)
    {
        _rootPath = rootPath;
    }

    public string RootPath => _rootPath;

    public void ChangeRootPath(string newPath) => _rootPath = newPath;

    public FileNode BuildTree()
    {
        return BuildNodeForDirectory(_rootPath, Path.GetFileName(_rootPath) is { Length: > 0 } n ? n : _rootPath);
    }

    private FileNode BuildNodeForDirectory(string dirPath, string displayName)
    {
        var children = new List<FileNode>();

        try
        {
            // Add subdirectories first
            foreach (var subDir in Directory.GetDirectories(dirPath).OrderBy(d => d))
            {
                var dirName = Path.GetFileName(subDir);
                if (dirName != null && !SkippedDirs.Contains(dirName))
                    children.Add(BuildNodeForDirectory(subDir, dirName));
            }

            // Then files
            foreach (var file in Directory.GetFiles(dirPath).OrderBy(f => f))
            {
                var ext = Path.GetExtension(file);
                if (AllowedExtensions.Contains(ext))
                {
                    var fileName = Path.GetFileName(file);
                    children.Add(new FileNode(fileName, file, false, new List<FileNode>()));
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return new FileNode(displayName, dirPath, true, children);
    }

    public string? FindBuildTarget()
    {
        // Prefer .sln
        var sln = Directory.GetFiles(_rootPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (sln != null) return sln;

        // Fallback to any .csproj
        return Directory.GetFiles(_rootPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
    }

    public string? FindRunTarget()
    {
        // Find a .csproj that has OutputType=Exe
        foreach (var csproj in Directory.GetFiles(_rootPath, "*.csproj", SearchOption.AllDirectories))
        {
            try
            {
                var content = File.ReadAllText(csproj);
                if (content.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase))
                    return csproj;
            }
            catch { }
        }
        return Directory.GetFiles(_rootPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
    }
}
