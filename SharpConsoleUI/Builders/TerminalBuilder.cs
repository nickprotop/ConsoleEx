using System.Runtime.Versioning;
using SharpConsoleUI.Controls.Terminal;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating a <see cref="TerminalControl"/>.
/// Supported platforms: Linux (openpty), Windows 10 1809+ (ConPTY).
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
public class TerminalBuilder
{
    // Default shell: bash on Linux/macOS, cmd.exe on Windows.
    private string _exe = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash";
    private string[]? _args;
    private string? _workingDirectory;

    /// <summary>Sets the executable to launch inside the terminal.</summary>
    public TerminalBuilder WithExe(string exe)         { _exe = exe; return this; }
    /// <summary>Sets the arguments passed to the executable.</summary>
    public TerminalBuilder WithArgs(params string[] a) { _args = a;  return this; }
    /// <summary>Sets the working directory for the spawned process.</summary>
    public TerminalBuilder WithWorkingDirectory(string? dir) { _workingDirectory = dir; return this; }

    /// <summary>Returns a self-contained TerminalControl (PTY open, shim running, read loop active).</summary>
    public TerminalControl Build() => new TerminalControl(_exe, _args, _workingDirectory);

    /// <summary>
    /// Convenience: builds a TerminalControl and opens a default centered window.
    /// </summary>
    /// <param name="ws">The window system to open the terminal in.</param>
    /// <param name="width">Terminal columns. Defaults to desktop width minus 6, minimum 60.</param>
    /// <param name="height">Terminal rows. Defaults to desktop height minus 6, minimum 20.</param>
    public void Open(ConsoleWindowSystem ws, int? width = null, int? height = null)
    {
        var t = Build();
        int cols = width  ?? Math.Max(ws.DesktopDimensions.Width  - 6, 60);
        int rows = height ?? Math.Max(ws.DesktopDimensions.Height - 6, 20);
        var window = new WindowBuilder(ws)
            .WithTitle(t.Title)
            .WithSize(cols + 2, rows + 2)
            .Centered()
            .Closable(true)
            .AddControl(t)
            .Build();
        ws.AddWindow(window);
    }
}
