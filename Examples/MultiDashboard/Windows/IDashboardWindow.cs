using SharpConsoleUI;

namespace MultiDashboard.Windows;

/// <summary>
/// Common contract for a dashboard window that the Control Center can toggle on the desktop.
/// Implemented by every toggleable window so <c>Program.ToggleWindow</c> can drive them
/// without reflection.
/// </summary>
public interface IDashboardWindow : IDisposable
{
    /// <summary>The built window (or null if not yet created / disposed).</summary>
    Window? Window { get; }

    /// <summary>Adds the window to the desktop and makes it visible.</summary>
    void Show();

    /// <summary>Removes the window from the desktop.</summary>
    void Hide();
}
