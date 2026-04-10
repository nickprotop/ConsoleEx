using System.ComponentModel;
using System.Runtime.InteropServices;
using ScHost.Packaging;
using ScHost.Terminal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScHost.Commands;

public sealed class InstallCommandSettings : CommandSettings
{
    [Description("Path to .csproj file or directory")]
    [CommandArgument(0, "[project]")]
    public string? Project { get; set; }

    [Description("Path to the built executable")]
    [CommandOption("--exe")]
    public string? ExePath { get; set; }

    [Description("Uninstall instead of install")]
    [CommandOption("--uninstall")]
    public bool Uninstall { get; set; }
}

public sealed class InstallCommand : Command<InstallCommandSettings>
{
    protected override int Execute(CommandContext context, InstallCommandSettings settings, CancellationToken ct)
    {
        try
        {
            var config = ProjectReader.LoadConfig(settings.Project);

            if (settings.Uninstall)
                return Uninstall(config);

            return Install(config, settings.ExePath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static int Install(Models.SchostConfig config, string? exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            AnsiConsole.MarkupLine("[red]--exe is required and must point to an existing executable.[/]");
            return 1;
        }

        exePath = Path.GetFullPath(exePath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsTerminal.InstallFragment(config, exePath);
            AnsiConsole.MarkupLine("[green]Windows Terminal profile installed.[/]");
        }
        else
        {
            var outputDir = Path.GetDirectoryName(exePath)!;
            LinuxTerminal.CreateDesktopFile(config, exePath, outputDir);
            AnsiConsole.MarkupLine("[green]Desktop file created.[/]");
        }

        return 0;
    }

    private static int Uninstall(Models.SchostConfig config)
    {
        var assemblyName = config.AssemblyName ?? "App";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsTerminal.UninstallFragment(assemblyName);
            AnsiConsole.MarkupLine("[green]Windows Terminal profile removed.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]On Linux, manually remove the .desktop file from ~/.local/share/applications/[/]");
        }

        return 0;
    }
}
