using System.Diagnostics;
using ScHost.Models;
using Spectre.Console;

namespace ScHost.Terminal;

/// <summary>
/// Linux terminal emulator detection and launch support.
/// </summary>
public static class LinuxTerminal
{
    /// <summary>
    /// Terminal emulators in order of preference.
    /// </summary>
    private static readonly string[] TerminalOrder =
    [
        "ghostty", "kitty", "alacritty", "wezterm", "foot",
        "gnome-terminal", "konsole", "xfce4-terminal", "xterm"
    ];

    /// <summary>
    /// Detects the first available terminal emulator.
    /// </summary>
    public static string? DetectTerminal()
    {
        foreach (var name in TerminalOrder)
        {
            if (IsCommandAvailable(name))
                return name;
        }

        return null;
    }

    /// <summary>
    /// Launches the app in a detected terminal emulator with configured geometry.
    /// </summary>
    public static bool Launch(SchostConfig config, string exePath)
    {
        var terminal = DetectTerminal();
        if (terminal is null)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] No supported terminal emulator found.");
            return false;
        }

        var args = BuildLaunchArgs(terminal, config, exePath);

        try
        {
            var psi = new ProcessStartInfo(terminal)
            {
                Arguments = args,
                UseShellExecute = false
            };

            Process.Start(psi);
            AnsiConsole.MarkupLine($"[green]Launched in {terminal}:[/] {Path.GetFileName(exePath)}");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to launch in {terminal}:[/] {ex.Message}");
            return false;
        }
    }

    private static string BuildLaunchArgs(string terminal, SchostConfig config, string exePath)
    {
        var title = config.GetEffectiveTitle();
        var cols = config.Columns;
        var rows = config.Rows;
        var font = config.Font;
        var fontSize = config.FontSize;

        return terminal switch
        {
            "ghostty" => BuildGhosttyArgs(title, cols, rows, font, fontSize, exePath),
            "kitty" => BuildKittyArgs(title, cols, rows, font, fontSize, exePath),
            "alacritty" => BuildAlacrittyArgs(title, cols, rows, exePath),
            "wezterm" => BuildWeztermArgs(title, cols, rows, font, fontSize, exePath),
            "foot" => BuildFootArgs(title, cols, rows, font, fontSize, exePath),
            "gnome-terminal" => BuildGnomeTerminalArgs(title, cols, rows, exePath),
            "konsole" => BuildKonsoleArgs(title, exePath),
            "xfce4-terminal" => BuildXfce4Args(title, cols, rows, font, fontSize, exePath),
            "xterm" => BuildXtermArgs(title, cols, rows, font, fontSize, exePath),
            _ => $"-e \"{exePath}\""
        };
    }

    private static string BuildGhosttyArgs(string title, int? cols, int? rows,
        string? font, int? fontSize, string exePath)
    {
        var parts = new List<string>();
        parts.Add($"--title=\"{title}\"");
        if (cols.HasValue && rows.HasValue)
            parts.Add($"--window-width={cols} --window-height={rows}");
        if (font is not null)
            parts.Add($"--font-family=\"{font}\"");
        if (fontSize.HasValue)
            parts.Add($"--font-size={fontSize}");
        parts.Add($"-e \"{exePath}\"");
        return string.Join(' ', parts);
    }

    private static string BuildKittyArgs(string title, int? cols, int? rows,
        string? font, int? fontSize, string exePath)
    {
        var parts = new List<string>();
        parts.Add($"--title=\"{title}\"");
        if (font is not null)
            parts.Add($"--override font_family=\"{font}\"");
        if (fontSize.HasValue)
            parts.Add($"--override font_size={fontSize}");
        if (cols.HasValue && rows.HasValue)
            parts.Add($"--override initial_window_width={cols}c --override initial_window_height={rows}c");
        parts.Add($"-- \"{exePath}\"");
        return string.Join(' ', parts);
    }

    private static string BuildAlacrittyArgs(string title, int? cols, int? rows, string exePath)
    {
        var parts = new List<string>();
        parts.Add($"--title \"{title}\"");
        if (cols.HasValue && rows.HasValue)
            parts.Add($"--option \"window.dimensions.columns={cols}\" --option \"window.dimensions.lines={rows}\"");
        parts.Add($"-e \"{exePath}\"");
        return string.Join(' ', parts);
    }

    private static string BuildWeztermArgs(string title, int? cols, int? rows,
        string? font, int? fontSize, string exePath)
    {
        var parts = new List<string> { "start" };
        if (cols.HasValue && rows.HasValue)
            parts.Add($"--width {cols} --height {rows}");
        parts.Add($"-- \"{exePath}\"");
        return string.Join(' ', parts);
    }

    private static string BuildFootArgs(string title, int? cols, int? rows,
        string? font, int? fontSize, string exePath)
    {
        var parts = new List<string>();
        parts.Add($"--title=\"{title}\"");
        if (cols.HasValue && rows.HasValue)
            parts.Add($"--window-size-chars={cols}x{rows}");
        if (font is not null && fontSize.HasValue)
            parts.Add($"--font=\"{font}:size={fontSize}\"");
        else if (font is not null)
            parts.Add($"--font=\"{font}\"");
        parts.Add($"\"{exePath}\"");
        return string.Join(' ', parts);
    }

    private static string BuildGnomeTerminalArgs(string title, int? cols, int? rows, string exePath)
    {
        var parts = new List<string>();
        parts.Add($"--title=\"{title}\"");
        if (cols.HasValue && rows.HasValue)
            parts.Add($"--geometry={cols}x{rows}");
        parts.Add($"-- \"{exePath}\"");
        return string.Join(' ', parts);
    }

    private static string BuildKonsoleArgs(string title, string exePath)
    {
        return $"--title \"{title}\" -e \"{exePath}\"";
    }

    private static string BuildXfce4Args(string title, int? cols, int? rows,
        string? font, int? fontSize, string exePath)
    {
        var parts = new List<string>();
        parts.Add($"--title=\"{title}\"");
        if (cols.HasValue && rows.HasValue)
            parts.Add($"--geometry={cols}x{rows}");
        if (font is not null && fontSize.HasValue)
            parts.Add($"--font=\"{font} {fontSize}\"");
        parts.Add($"-e \"{exePath}\"");
        return string.Join(' ', parts);
    }

    private static string BuildXtermArgs(string title, int? cols, int? rows,
        string? font, int? fontSize, string exePath)
    {
        var parts = new List<string>();
        parts.Add($"-title \"{title}\"");
        if (cols.HasValue && rows.HasValue)
            parts.Add($"-geometry {cols}x{rows}");
        if (font is not null)
            parts.Add($"-fa \"{font}\"");
        if (fontSize.HasValue)
            parts.Add($"-fs {fontSize}");
        parts.Add($"-e \"{exePath}\"");
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Creates a .desktop file for Linux application launchers.
    /// </summary>
    public static string CreateDesktopFile(SchostConfig config, string exePath, string outputDir)
    {
        var title = config.GetEffectiveTitle();
        var assemblyName = config.AssemblyName ?? "app";
        var terminal = DetectTerminal() ?? "xterm";
        var desktopPath = Path.Combine(outputDir, $"{assemblyName}.desktop");

        var terminalCmd = BuildLaunchArgs(terminal, config, exePath);

        var desktop = $"""
            [Desktop Entry]
            Type=Application
            Name={title}
            Exec={terminal} {terminalCmd}
            Terminal=false
            Categories=Utility;
            """;

        File.WriteAllText(desktopPath, desktop);

        // chmod +x
        try
        {
            var psi = new ProcessStartInfo("chmod", $"+x \"{desktopPath}\"")
            {
                UseShellExecute = false
            };
            Process.Start(psi)?.WaitForExit();
        }
        catch
        {
            // Best effort
        }

        AnsiConsole.MarkupLine($"[green]Created .desktop file:[/] {desktopPath}");
        return desktopPath;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo("which", command)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
