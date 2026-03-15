using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using ScHost.Packaging;
using ScHost.Terminal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScHost.Commands;

public sealed class PackCommandSettings : CommandSettings
{
    [Description("Path to .csproj file or directory")]
    [CommandArgument(0, "[project]")]
    public string? Project { get; set; }

    [Description("Output directory")]
    [CommandOption("-o|--output")]
    public string? Output { get; set; }

    [Description("Runtime identifier (e.g., win-x64, linux-x64)")]
    [CommandOption("-r|--runtime")]
    public string? Runtime { get; set; }

    [Description("Build Inno Setup installer (Windows only)")]
    [CommandOption("--installer")]
    public bool Installer { get; set; }

    [Description("Disable IL trimming")]
    [CommandOption("--no-trim")]
    public bool NoTrim { get; set; }

    [Description("Do not bundle the .NET runtime")]
    [CommandOption("--no-self-contained")]
    public bool NoSelfContained { get; set; }
}

public sealed class PackCommand : Command<PackCommandSettings>
{
    public override int Execute(CommandContext context, PackCommandSettings settings)
    {
        try
        {
            var config = ProjectReader.LoadConfig(settings.Project);
            var projectDir = Path.GetDirectoryName(config.CsprojPath)!;

            // Determine output directory
            var outputDir = settings.Output is not null
                ? Path.GetFullPath(settings.Output)
                : Path.Combine(projectDir, "publish");
            Directory.CreateDirectory(outputDir);

            // Determine runtime
            var runtime = settings.Runtime ?? GetDefaultRuntime();
            var selfContained = !(settings.NoSelfContained || config.SelfContained == false);
            var trim = !settings.NoTrim;

            // Publish
            AnsiConsole.MarkupLine($"[blue]Publishing for {runtime}...[/]");
            var publishDir = Path.Combine(outputDir, "app");

            var publishArgs = $"publish \"{config.CsprojPath}\" " +
                              $"-c Release " +
                              $"-r {runtime} " +
                              $"--self-contained {selfContained.ToString().ToLowerInvariant()} " +
                              $"-p:PublishSingleFile=true " +
                              (trim ? "-p:PublishTrimmed=true " : "") +
                              $"-o \"{publishDir}\"";

            var buildResult = RunProcess("dotnet", publishArgs, projectDir);
            if (buildResult != 0)
            {
                AnsiConsole.MarkupLine("[red]Publish failed.[/]");
                return 1;
            }

            // Find the published executable
            var assemblyName = config.AssemblyName!;
            var exeName = runtime.StartsWith("win", StringComparison.OrdinalIgnoreCase)
                ? $"{assemblyName}.exe"
                : assemblyName;
            var exePath = Path.Combine(publishDir, exeName);

            if (!File.Exists(exePath))
            {
                AnsiConsole.MarkupLine($"[red]Published executable not found:[/] {exePath}");
                return 1;
            }

            // Generate icon
            IconGenerator.GenerateIcon(config, publishDir);

            // Create launcher inside app dir (goes into zip)
            ShortcutCreator.CreateLauncher(config, exePath, publishDir);

            // Install WT fragment
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                WindowsTerminal.InstallFragment(config, exePath);

            // Create portable zip from app dir
            var zipName = $"{config.GetEffectiveTitle()}-{config.Version ?? "1.0.0"}-{runtime}.zip";
            var zipPath = Path.Combine(outputDir, zipName);
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            ZipFile.CreateFromDirectory(publishDir, zipPath);
            AnsiConsole.MarkupLine($"[green]Portable zip:[/] {zipPath}");

            // On Linux, also create .desktop file outside the zip (for install)
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LinuxTerminal.CreateDesktopFile(config, exePath, outputDir);
            }

            // Installer (Windows only, optional)
            string? installerPath = null;
            if (settings.Installer || config.Installer == true)
            {
                installerPath = InstallerBuilder.Build(config, exePath, outputDir);
            }

            // Summary
            AnsiConsole.WriteLine();
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Artifact")
                .AddColumn("Path");

            table.AddRow("Executable", exePath);
            table.AddRow("Portable ZIP", zipPath);
            if (installerPath is not null)
                table.AddRow("Installer", installerPath);

            AnsiConsole.Write(new Panel(table)
            {
                Header = new PanelHeader("Pack Summary"),
                Border = BoxBorder.Rounded
            });

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static string GetDefaultRuntime()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
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
