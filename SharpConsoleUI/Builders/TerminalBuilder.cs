using SharpConsoleUI.Controls.Terminal;

namespace SharpConsoleUI.Builders;

public class TerminalBuilder
{
    private string _exe = "/bin/bash";
    private string[]? _args;

    public TerminalBuilder WithExe(string exe)         { _exe = exe; return this; }
    public TerminalBuilder WithArgs(params string[] a) { _args = a;  return this; }

    /// <summary>Returns a self-contained TerminalControl (PTY open, shim running, read loop active).</summary>
    public TerminalControl Build() => new TerminalControl(_exe, _args);

    /// <summary>
    /// Convenience: builds a TerminalControl and opens a default centered window.
    /// </summary>
    public void Open(ConsoleWindowSystem ws)
    {
        var t = Build();
        int rows = Math.Max(ws.DesktopDimensions.Height - 6, 20);
        int cols = Math.Max(ws.DesktopDimensions.Width  - 6, 60);
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
