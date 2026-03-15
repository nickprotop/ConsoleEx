using System.Runtime.InteropServices;
using ScHost.Models;
using ScHost.Terminal;
using Spectre.Console;

namespace ScHost.Packaging;

/// <summary>
/// Creates launcher scripts (.cmd on Windows, .desktop on Linux).
/// </summary>
public static class ShortcutCreator
{
    /// <summary>
    /// Creates a platform-appropriate launcher in the output directory.
    /// Returns the path to the created launcher.
    /// </summary>
    public static string CreateLauncher(SchostConfig config, string exePath, string outputDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CreateWindowsLauncher(config, exePath, outputDir);

        return CreateLinuxLauncher(config, exePath, outputDir);
    }

    private static string CreateWindowsLauncher(SchostConfig config, string exePath, string outputDir)
    {
        var title = config.GetEffectiveTitle();
        var exeName = Path.GetFileName(exePath);
        var launcherName = $"{config.AssemblyName}.cmd";
        var launcherPath = Path.Combine(outputDir, launcherName);

        // CMD launcher that tries Windows Terminal first, falls back to direct exe
        var cmd = $"""
            @echo off
            where wt >nul 2>nul
            if %errorlevel% equ 0 (
                start "" wt.exe new-tab --title "{title}" -- "%~dp0{exeName}"
            ) else (
                start "" "%~dp0{exeName}"
            )
            """;

        File.WriteAllText(launcherPath, cmd);
        AnsiConsole.MarkupLine($"[green]Created launcher:[/] {launcherPath}");
        return launcherPath;
    }

    private static string CreateLinuxLauncher(SchostConfig config, string exePath, string outputDir)
    {
        return LinuxTerminal.CreateDesktopFile(config, exePath, outputDir);
    }
}
