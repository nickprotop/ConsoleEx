// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Events;

/// <summary>
/// Event arguments for when a link is clicked.
/// </summary>
public record LinkClickedEventArgs(string Url, string Text, MouseEventArgs Mouse);
