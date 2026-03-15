using System.Diagnostics;
using ScHost.Models;
using Spectre.Console;

namespace ScHost.Terminal;

/// <summary>
/// Fallback launcher using the default system console (conhost on Windows).
/// </summary>
public static class Conhost
{
    /// <summary>
    /// Launches the app using the default system shell execute behavior.
    /// </summary>
    public static bool Launch(SchostConfig config, string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true
            };

            Process.Start(psi);
            AnsiConsole.MarkupLine($"[green]Launched:[/] {Path.GetFileName(exePath)}");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to launch:[/] {ex.Message}");
            return false;
        }
    }
}
