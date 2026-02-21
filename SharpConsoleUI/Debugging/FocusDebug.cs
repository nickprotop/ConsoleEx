// -----------------------------------------------------------------------
// FocusDebug.cs — opt-in focus system debug logger
// Enable by: touch /tmp/focus_debug_enable
// Logs written to: /tmp/focus_debug.log
// Remove all FocusDebug.Log calls after root causes are confirmed.
// -----------------------------------------------------------------------
namespace SharpConsoleUI.Debugging;

internal static class FocusDebug
{
    private const string EnableFlag = "/tmp/focus_debug_enable";
    private const string LogPath    = "/tmp/focus_debug.log";

    private static readonly bool _enabled = File.Exists(EnableFlag);

    public static void Log(string message)
    {
        if (!_enabled) return;
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n"); }
        catch { /* best-effort */ }
    }
}
