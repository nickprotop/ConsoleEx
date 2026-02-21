namespace DotNetIDE;

public record LspServer(string Exe, string[] BaseArgs);

public static class LspDetector
{
    private static readonly string[] OmniSharpSearchPaths =
    [
        "/opt/omnisharp/OmniSharp",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".omnisharp", "OmniSharp"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".omnisharp", "omnisharp", "OmniSharp"),
    ];

    public static LspServer? Find(string workspacePath)
    {
        // 1. Env override
        var env = Environment.GetEnvironmentVariable("DOTNET_IDE_LSP");
        if (env != null && File.Exists(env))
            return MakeOmniSharpServer(env, workspacePath);

        // 2. csharp-ls (dotnet tool install -g csharp-ls)
        if (IsInPath("csharp-ls"))
            return new LspServer("csharp-ls", []);

        // 3. OmniSharp in known locations
        foreach (var path in OmniSharpSearchPaths)
            if (File.Exists(path))
                return MakeOmniSharpServer(path, workspacePath);

        // 4. OmniSharp in PATH
        if (IsInPath("OmniSharp"))
            return MakeOmniSharpServer("OmniSharp", workspacePath);

        return null;
    }

    private static LspServer MakeOmniSharpServer(string exe, string workspacePath) =>
        new(exe, ["-lsp", "-s", workspacePath, "-z"]);

    private static bool IsInPath(string exe)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(2000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }
}
