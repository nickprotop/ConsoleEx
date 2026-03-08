using SharpConsoleUI;

namespace DemoApp.Helpers;

public static class FileHelpers
{
    public static string GetFileIcon(string extension) => extension.ToLowerInvariant() switch
    {
        ".cs" => "[green]#[/]",
        ".csproj" or ".sln" => "[blue]P[/]",
        ".json" => "[yellow]{}[/]",
        ".xml" => "[cyan]<>[/]",
        ".md" or ".txt" => "[white]T[/]",
        ".yml" or ".yaml" => "[magenta]Y[/]",
        ".html" or ".htm" => "[red]H[/]",
        ".css" => "[blue]S[/]",
        ".js" or ".ts" => "[yellow]J[/]",
        ".py" => "[blue]Py[/]",
        ".sh" or ".bash" => "[green]$[/]",
        ".png" or ".jpg" or ".gif" or ".svg" => "[magenta]I[/]",
        ".zip" or ".tar" or ".gz" => "[red]Z[/]",
        ".dll" or ".exe" => "[dim]B[/]",
        ".log" => "[dim]L[/]",
        _ => "[dim].[/]"
    };

    public static Color GetFileColor(string extension) => extension.ToLowerInvariant() switch
    {
        ".cs" => Color.Green,
        ".csproj" or ".sln" => Color.Blue,
        ".json" => Color.Yellow,
        ".xml" => Color.Cyan1,
        ".md" or ".txt" => Color.White,
        _ => Color.Grey
    };

    public static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    public static bool HasSubdirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).Any();
        }
        catch
        {
            return false;
        }
    }
}
