using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DotNetIDE;

public record BuildDiagnostic(
    string FilePath,
    int Line,
    int Column,
    string Code,
    string Severity,
    string Message);

public class BuildResult
{
    public bool Success { get; init; }
    public List<BuildDiagnostic> Diagnostics { get; init; } = new();
}

public class BuildService
{
    private static readonly Regex DiagnosticPattern = new(
        @"^(.+?)\((\d+),(\d+)\):\s+(error|warning)\s+([A-Z0-9]+):\s+(.+)$",
        RegexOptions.Compiled);

    private Process? _currentProcess;
    private readonly object _lock = new();

    public async Task<BuildResult> BuildAsync(string target, Action<string> onLine, CancellationToken ct)
        => await RunDotnetAsync("build", target, ["--nologo"], onLine, ct);

    public async Task<BuildResult> TestAsync(string target, Action<string> onLine, CancellationToken ct)
        => await RunDotnetAsync("test", target, ["--nologo", "--logger", "console;verbosity=normal"], onLine, ct);

    public async Task RunAsync(string arguments, Action<string> onLine, CancellationToken ct)
    {
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var args = parts.Skip(1).ToArray();
        await RunDotnetAsync(parts[0], null, args, onLine, ct);
    }

    public void Cancel()
    {
        lock (_lock)
        {
            try { _currentProcess?.Kill(entireProcessTree: true); }
            catch { }
        }
    }

    private async Task<BuildResult> RunDotnetAsync(
        string command,
        string? target,
        string[] extraArgs,
        Action<string> onLine,
        CancellationToken ct)
    {
        var allArgs = new List<string> { command };
        if (target != null) allArgs.Add(target);
        allArgs.AddRange(extraArgs);

        var psi = new ProcessStartInfo("dotnet", allArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var diagnostics = new List<BuildDiagnostic>();
        bool success = false;

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        lock (_lock) { _currentProcess = process; }

        try
        {
            process.Start();

            var stdoutTask = ReadStreamAsync(process.StandardOutput, line =>
            {
                onLine(line);
                var match = DiagnosticPattern.Match(line);
                if (match.Success)
                {
                    diagnostics.Add(new BuildDiagnostic(
                        FilePath: match.Groups[1].Value.Trim(),
                        Line: int.TryParse(match.Groups[2].Value, out var l) ? l : 0,
                        Column: int.TryParse(match.Groups[3].Value, out var c) ? c : 0,
                        Severity: match.Groups[4].Value,
                        Code: match.Groups[5].Value,
                        Message: match.Groups[6].Value.Trim()));
                }
            }, ct);

            var stderrTask = ReadStreamAsync(process.StandardError, onLine, ct);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(ct);

            success = process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            success = false;
        }
        finally
        {
            lock (_lock) { _currentProcess = null; }
        }

        return new BuildResult { Success = success, Diagnostics = diagnostics };
    }

    private static async Task ReadStreamAsync(StreamReader reader, Action<string> onLine, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;
                onLine(line);
            }
        }
        catch (OperationCanceledException) { }
    }
}
