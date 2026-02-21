using System.Diagnostics;

namespace DotNetIDE;

public class GitService
{
    public async Task<string> GetBranchAsync(string path)
    {
        try
        {
            var output = await RunGitAsync(path, "rev-parse", "--abbrev-ref", "HEAD");
            return output.Trim();
        }
        catch { return ""; }
    }

    public async Task<string> GetStatusSummaryAsync(string path)
    {
        try
        {
            var output = await RunGitAsync(path, "status", "--short");
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return "";
            var modified = lines.Count(l => l.StartsWith("M") || l.StartsWith(" M"));
            var added = lines.Count(l => l.StartsWith("A") || l.StartsWith("?"));
            var deleted = lines.Count(l => l.StartsWith("D") || l.StartsWith(" D"));
            var parts = new List<string>();
            if (modified > 0) parts.Add($"M:{modified}");
            if (added > 0)    parts.Add($"A:{added}");
            if (deleted > 0)  parts.Add($"D:{deleted}");
            return string.Join("  ", parts);
        }
        catch { return ""; }
    }

    private static async Task<string> RunGitAsync(string path, params string[] args)
    {
        var allArgs = new List<string> { "-C", path };
        allArgs.AddRange(args);

        var psi = new ProcessStartInfo("git", allArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return process.ExitCode == 0 ? output : "";
    }
}
