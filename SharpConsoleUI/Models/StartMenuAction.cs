namespace SharpConsoleUI.Models;

/// <summary>
/// Represents a user-defined action in the Start menu.
/// </summary>
public record StartMenuAction(
    string Name,
    Action Callback,
    string? Category = null,
    int Order = 0
);
