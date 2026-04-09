// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Events;

/// <summary>
/// Event arguments for when a link is hovered or unhovered.
/// </summary>
public record LinkHoverEventArgs(string? Url, string? Text);
