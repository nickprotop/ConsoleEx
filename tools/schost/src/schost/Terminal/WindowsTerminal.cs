using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScHost.Models;
using Spectre.Console;

namespace ScHost.Terminal;

/// <summary>
/// Windows Terminal integration — profile fragments and launch support.
/// </summary>
public static class WindowsTerminal
{
    /// <summary>
    /// Checks if Windows Terminal (wt.exe) is available on PATH.
    /// </summary>
    public static bool IsAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            var psi = new ProcessStartInfo("wt.exe", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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

    /// <summary>
    /// Installs a Windows Terminal fragment profile for the app.
    /// </summary>
    public static void InstallFragment(SchostConfig config, string exePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var assemblyName = config.AssemblyName ?? "App";
        var fragmentDir = Path.Combine(localAppData, "Microsoft", "Windows Terminal",
            "Fragments", assemblyName);
        Directory.CreateDirectory(fragmentDir);

        var profile = new
        {
            profiles = new[]
            {
                new
                {
                    name = config.GetEffectiveTitle(),
                    commandline = exePath,
                    font = config.Font is not null ? new { face = config.Font, size = config.FontSize ?? 12 } : null,
                    colorScheme = config.ColorScheme,
                    startingDirectory = Path.GetDirectoryName(exePath)
                }
            }
        };

        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        var profilePath = Path.Combine(fragmentDir, "profile.json");
        File.WriteAllText(profilePath, json);

        AnsiConsole.MarkupLine($"[green]WT fragment installed:[/] {profilePath}");
    }

    /// <summary>
    /// Removes the Windows Terminal fragment for the app.
    /// </summary>
    public static void UninstallFragment(string assemblyName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var fragmentDir = Path.Combine(localAppData, "Microsoft", "Windows Terminal",
            "Fragments", assemblyName);

        if (Directory.Exists(fragmentDir))
        {
            Directory.Delete(fragmentDir, recursive: true);
            AnsiConsole.MarkupLine($"[green]WT fragment removed:[/] {fragmentDir}");
        }
    }

    /// <summary>
    /// Launches the app in a new Windows Terminal tab with configured size.
    /// </summary>
    public static bool Launch(SchostConfig config, string exePath)
    {
        if (!IsAvailable())
            return false;

        var title = config.GetEffectiveTitle();
        var parts = new List<string> { "new-tab", $"--title \"{title}\"" };

        // WT supports --size columns,rows (added in WT 1.18)
        if (config.Columns.HasValue && config.Rows.HasValue)
            parts.Add($"--size {config.Columns},{config.Rows}");

        // If a fragment profile was installed with font/color settings, use it
        if (config.Font is not null || config.ColorScheme is not null)
            parts.Add($"--profile \"{title}\"");

        parts.Add($"-- \"{exePath}\"");

        var psi = new ProcessStartInfo("wt.exe")
        {
            Arguments = string.Join(' ', parts),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
