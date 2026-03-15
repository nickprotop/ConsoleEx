using System.Diagnostics;
using System.Runtime.InteropServices;
using ScHost.Models;
using Spectre.Console;

namespace ScHost.Packaging;

/// <summary>
/// Generates and builds Inno Setup installers for Windows distribution.
/// </summary>
public static class InstallerBuilder
{
    private const string InnoSetupVersion = "6.2.2";

    /// <summary>
    /// Builds an Inno Setup installer. Returns the path to the setup exe, or null on failure.
    /// </summary>
    public static string? Build(SchostConfig config, string exePath, string outputDir)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Inno Setup installer is only available on Windows. Skipping.");
            return null;
        }

        var isccPath = FindOrDownloadIscc();
        if (isccPath is null)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Could not find or download Inno Setup. Skipping installer.");
            return null;
        }

        var title = config.GetEffectiveTitle();
        var version = config.Version ?? "1.0.0";
        var assemblyName = config.AssemblyName ?? "App";
        var exeDir = Path.GetDirectoryName(exePath)!;
        var exeName = Path.GetFileName(exePath);
        var setupName = $"{title}-{version}-setup";
        var issPath = Path.Combine(outputDir, $"{assemblyName}.iss");

        var iss = $$"""
            [Setup]
            AppName={{title}}
            AppVersion={{version}}
            AppPublisher={{title}}
            DefaultDirName={localappdata}\{{assemblyName}}
            DefaultGroupName={{title}}
            OutputDir={{outputDir}}
            OutputBaseFilename={{setupName}}
            Compression=lzma2
            SolidCompression=yes
            PrivilegesRequired=lowest
            DisableProgramGroupPage=yes

            [Files]
            Source: "{{exeDir}}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

            [Icons]
            Name: "{group}\{{title}}"; Filename: "{app}\{{exeName}}"
            Name: "{group}\Uninstall {{title}}"; Filename: "{uninstallexe}"

            [Tasks]
            Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

            [Icons]
            Name: "{userdesktop}\{{title}}"; Filename: "{app}\{{exeName}}"; Tasks: desktopicon

            [Run]
            Filename: "{app}\{{exeName}}"; Description: "Launch {{title}}"; Flags: nowait postinstall skipifsilent
            """;

        File.WriteAllText(issPath, iss);

        try
        {
            var psi = new ProcessStartInfo(isccPath, $"\"{issPath}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(psi);
            process?.WaitForExit();

            if (process?.ExitCode == 0)
            {
                var setupPath = Path.Combine(outputDir, $"{setupName}.exe");
                AnsiConsole.MarkupLine($"[green]Installer created:[/] {setupPath}");
                return setupPath;
            }

            var stderr = process?.StandardError.ReadToEnd();
            AnsiConsole.MarkupLine($"[red]Inno Setup failed:[/] {stderr}");
            return null;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to run Inno Setup: {ex.Message}");
            return null;
        }
    }

    private static string? FindOrDownloadIscc()
    {
        // Check common install locations
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var systemIscc = Path.Combine(programFiles, "Inno Setup 6", "ISCC.exe");
        if (File.Exists(systemIscc))
            return systemIscc;

        // Check local cache
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cachedIscc = Path.Combine(localAppData, "schost", "tools", "innosetup", "ISCC.exe");
        if (File.Exists(cachedIscc))
            return cachedIscc;

        // Try to download
        return TryDownloadInnoSetup(cachedIscc);
    }

    private static string? TryDownloadInnoSetup(string targetPath)
    {
        try
        {
            AnsiConsole.MarkupLine("[blue]Downloading Inno Setup...[/]");
            var url = $"https://files.jrsoftware.org/is/6/innosetup-{InnoSetupVersion}.exe";
            var tempDir = Path.Combine(Path.GetTempPath(), "schost-inno-download");
            Directory.CreateDirectory(tempDir);
            var installerPath = Path.Combine(tempDir, "innosetup.exe");

            using var client = new HttpClient();
            using var response = client.GetAsync(url).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to download Inno Setup (HTTP {response.StatusCode})");
                return null;
            }

            using (var fs = File.Create(installerPath))
            {
                response.Content.CopyTo(fs, null, CancellationToken.None);
            }

            // Run silent install to local cache directory
            var targetDir = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(targetDir);

            var psi = new ProcessStartInfo(installerPath,
                $"/VERYSILENT /SUPPRESSMSGBOXES /DIR=\"{targetDir}\" /CURRENTUSER /NOICONS")
            {
                UseShellExecute = false
            };

            var process = Process.Start(psi);
            process?.WaitForExit();

            if (File.Exists(targetPath))
            {
                AnsiConsole.MarkupLine("[green]Inno Setup installed successfully.[/]");
                return targetPath;
            }

            return null;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not download Inno Setup: {ex.Message}");
            return null;
        }
    }
}
