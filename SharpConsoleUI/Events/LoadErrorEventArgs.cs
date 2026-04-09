// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Events;

/// <summary>
/// Event arguments for when an error occurs during content loading.
/// </summary>
public record LoadErrorEventArgs(string Url, Exception Error);
