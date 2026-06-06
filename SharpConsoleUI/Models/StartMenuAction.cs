// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

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
