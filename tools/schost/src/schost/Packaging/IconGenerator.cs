using ScHost.Models;
using Spectre.Console;

namespace ScHost.Packaging;

/// <summary>
/// Generates a simple SVG icon from the app title if no custom icon is provided.
/// </summary>
public static class IconGenerator
{
    /// <summary>
    /// Copies the custom icon or generates an SVG icon in the output directory.
    /// Returns the path to the icon file.
    /// </summary>
    public static string? GenerateIcon(SchostConfig config, string outputDir)
    {
        if (!string.IsNullOrEmpty(config.Icon))
        {
            var projectDir = Path.GetDirectoryName(config.CsprojPath)!;
            var iconSource = Path.GetFullPath(config.Icon, projectDir);

            if (File.Exists(iconSource))
            {
                var iconDest = Path.Combine(outputDir, Path.GetFileName(iconSource));
                File.Copy(iconSource, iconDest, overwrite: true);
                return iconDest;
            }

            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Icon file not found: {config.Icon}");
        }

        // Generate SVG with title initial
        var title = config.GetEffectiveTitle();
        var initial = title.Length > 0 ? char.ToUpperInvariant(title[0]).ToString() : "A";

        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">
              <rect width="64" height="64" rx="12" fill="#00b4d8"/>
              <text x="32" y="44" font-family="sans-serif" font-size="36"
                    font-weight="bold" fill="white" text-anchor="middle">{initial}</text>
            </svg>
            """;

        var svgPath = Path.Combine(outputDir, $"{config.AssemblyName}.svg");
        File.WriteAllText(svgPath, svg);
        return svgPath;
    }
}
