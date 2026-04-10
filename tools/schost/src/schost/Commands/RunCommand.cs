using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ScHost.Packaging;
using ScHost.Terminal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScHost.Commands;

public sealed class RunCommandSettings : CommandSettings
{
    [Description("Path to .csproj file or directory")]
    [CommandArgument(0, "[project]")]
    public string? Project { get; set; }

    [Description("Skip building before launch")]
    [CommandOption("--no-build")]
    public bool NoBuild { get; set; }

    [Description("Run in current terminal (for debugging/CI)")]
    [CommandOption("--inline")]
    public bool Inline { get; set; }
}

public sealed class RunCommand : Command<RunCommandSettings>
{
    protected override int Execute(CommandContext context, RunCommandSettings settings, CancellationToken ct)
    {
        return ExecuteSync(settings);
    }

    private static int ExecuteSync(RunCommandSettings settings)
    {
        try
        {
            var config = ProjectReader.LoadConfig(settings.Project);
            var projectDir = Path.GetDirectoryName(config.CsprojPath)!;

            // Build
            if (!settings.NoBuild)
            {
                AnsiConsole.MarkupLine("[blue]Building...[/]");
                var buildResult = RunProcess("dotnet", $"build \"{config.CsprojPath}\" -c Debug", projectDir);
                if (buildResult != 0)
                {
                    AnsiConsole.MarkupLine("[red]Build failed.[/]");
                    return 1;
                }
            }

            // Find the built executable
            var exePath = FindBuiltExecutable(config.CsprojPath!, config.AssemblyName!, projectDir);
            if (exePath is null)
            {
                AnsiConsole.MarkupLine("[red]Could not find built executable.[/]");
                return 1;
            }

            // Inline mode: run in current terminal
            if (settings.Inline)
            {
                AnsiConsole.MarkupLine($"[blue]Running inline:[/] {Path.GetFileName(exePath)}");
                return RunProcess(exePath, "", projectDir);
            }

            // Launch in new terminal window
            AnsiConsole.MarkupLine($"[blue]Launching:[/] {config.GetEffectiveTitle()}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (WindowsTerminal.Launch(config, exePath))
                {
                    AnsiConsole.MarkupLine("[green]Launched in Windows Terminal.[/]");
                    return 0;
                }

                if (Conhost.Launch(config, exePath))
                    return 0;
            }
            else
            {
                if (LinuxTerminal.Launch(config, exePath))
                    return 0;

                // Fallback: run inline
                AnsiConsole.MarkupLine("[yellow]No terminal emulator found. Running inline.[/]");
                return RunProcess(exePath, "", projectDir);
            }

            AnsiConsole.MarkupLine("[red]Failed to launch application.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static string? FindBuiltExecutable(string csprojPath, string assemblyName, string projectDir)
    {
        // Look in typical output paths
        var configurations = new[] { "Debug", "Release" };
        var frameworks = new[] { "net9.0", "net8.0" };

        foreach (var cfg in configurations)
        {
            foreach (var fw in frameworks)
            {
                var binDir = Path.Combine(projectDir, "bin", cfg, fw);
                if (!Directory.Exists(binDir))
                    continue;

                // Try exe (Windows) or plain name (Linux)
                var exeNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new[] { $"{assemblyName}.exe" }
                    : new[] { assemblyName, $"{assemblyName}.exe" };

                foreach (var name in exeNames)
                {
                    var path = Path.Combine(binDir, name);
                    if (File.Exists(path))
                        return path;
                }
            }
        }

        return null;
    }

    private static int RunProcess(string fileName, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        process?.WaitForExit();
        return process?.ExitCode ?? 1;
    }
}
