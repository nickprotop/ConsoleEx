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
    ConsoleKey StartMenuShortcutKey = ConsoleKey.Spacebar,
    ConsoleModifiers StartMenuShortcutModifiers = ConsoleModifiers.Control,

    // Start menu options
    StartMenuOptions? StartMenu = null,

    // Status bar display
    bool ShowTopStatus = true,
    bool ShowBottomStatus = true,
    bool ShowTaskBar = true
)
{
    private StartMenuOptions? _startMenuConfigCache;

    /// <summary>
    /// Gets the resolved Start menu configuration (uses defaults if not explicitly set).
    /// The instance is cached so that runtime mutations are preserved.
    /// </summary>
    public StartMenuOptions StartMenuConfig => _startMenuConfigCache ??= StartMenu ?? new StartMenuOptions();

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
