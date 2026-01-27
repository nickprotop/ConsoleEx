namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration options for status bar behavior and Start menu.
/// </summary>
public record StatusBarOptions(
    // Start button configuration
    bool ShowStartButton = false,
    StatusBarLocation StartButtonLocation = StatusBarLocation.Bottom,
    StartButtonPosition StartButtonPosition = StartButtonPosition.Left,
    string StartButtonText = "☰ Start",
    ConsoleKey StartMenuShortcutKey = ConsoleKey.M,
    ConsoleModifiers StartMenuShortcutModifiers = ConsoleModifiers.Control,

    // Start menu content
    bool ShowSystemMenuCategory = true,
    bool ShowWindowListInMenu = true,

    // Status bar text (existing functionality)
    bool ShowTopStatus = true,
    bool ShowBottomStatus = true,
    bool ShowTaskBar = true
)
{
    /// <summary>
    /// Gets the default status bar options.
    /// </summary>
    public static StatusBarOptions Default => new();

    /// <summary>
    /// Gets status bar options with the Start button disabled.
    /// </summary>
    public static StatusBarOptions WithStartButtonDisabled =>
        new(ShowStartButton: false);

    /// <summary>
    /// Gets status bar options with Start button in top status bar.
    /// </summary>
    public static StatusBarOptions TopStartButton =>
        new(StartButtonLocation: StatusBarLocation.Top);
}

/// <summary>
/// Specifies the location of the status bar.
/// </summary>
public enum StatusBarLocation
{
    /// <summary>Top status bar.</summary>
    Top,
    /// <summary>Bottom status bar.</summary>
    Bottom
}

/// <summary>
/// Specifies the position of the Start button within the status bar.
/// </summary>
public enum StartButtonPosition
{
    /// <summary>Left side of status bar.</summary>
    Left,
    /// <summary>Right side of status bar.</summary>
    Right
}
