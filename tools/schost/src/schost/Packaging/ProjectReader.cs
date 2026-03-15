using System.Xml.Linq;
using ScHost.Models;
using Spectre.Console;

namespace ScHost.Packaging;

/// <summary>
/// Reads project information from .csproj files and merges with schost.json config.
/// </summary>
public static class ProjectReader
{
    /// <summary>
    /// Finds a .csproj file from the given argument or auto-detects in the current directory.
    /// </summary>
    public static string FindCsproj(string? projectArg)
    {
        if (!string.IsNullOrEmpty(projectArg))
        {
            var resolved = Path.GetFullPath(projectArg);
            if (File.Exists(resolved) && resolved.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                return resolved;

            if (Directory.Exists(resolved))
                return FindCsprojInDirectory(resolved);

            throw new FileNotFoundException($"Project not found: {projectArg}");
        }

        return FindCsprojInDirectory(Directory.GetCurrentDirectory());
    }

    private static string FindCsprojInDirectory(string directory)
    {
        var csprojFiles = Directory.GetFiles(directory, "*.csproj");

        return csprojFiles.Length switch
        {
            0 => throw new FileNotFoundException($"No .csproj file found in {directory}"),
            1 => csprojFiles[0],
            _ => Path.Combine(directory, AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Multiple projects found. Which one?")
                    .AddChoices(csprojFiles.Select(f => Path.GetFileName(f)!))))
        };
    }

    /// <summary>
    /// Parses AssemblyName and Version from a .csproj file.
    /// </summary>
    public static (string AssemblyName, string Version) ParseCsproj(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var assemblyName = doc.Descendants(ns + "AssemblyName").FirstOrDefault()?.Value
                           ?? Path.GetFileNameWithoutExtension(csprojPath);

        var version = doc.Descendants(ns + "Version").FirstOrDefault()?.Value ?? "1.0.0";

        return (assemblyName, version);
    }

    /// <summary>
    /// Loads and merges configuration from .csproj and schost.json.
    /// </summary>
    public static SchostConfig LoadConfig(string? projectArg)
    {
        var csprojPath = FindCsproj(projectArg);
        var (assemblyName, version) = ParseCsproj(csprojPath);
        var projectDir = Path.GetDirectoryName(csprojPath)!;
        var configPath = Path.Combine(projectDir, SchostConfig.FileName);

        SchostConfig config;
        if (File.Exists(configPath))
        {
            config = SchostConfig.Load(configPath);
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]No schost.json found, using defaults.[/] Run [blue]schost init[/] to configure.");
            config = new SchostConfig();
        }

        config.CsprojPath = csprojPath;
        config.AssemblyName = assemblyName;
        config.Version = version;
        config.Title ??= assemblyName;

        return config;
    }
}
