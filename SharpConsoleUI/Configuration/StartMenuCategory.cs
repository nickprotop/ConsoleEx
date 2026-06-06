// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Defines a custom category for the Start menu sidebar.
/// </summary>
public class StartMenuCategory
{
	/// <summary>Display name for this category.</summary>
	public string Name { get; init; } = "";

	/// <summary>Icon shown in IconRail and IconLabel sidebar modes.</summary>
	public string? Icon { get; init; }

	/// <summary>Factory that populates the content panel when this category is selected.</summary>
	public Action<ScrollablePanelControl>? ContentFactory { get; init; }

	/// <summary>Sort order. Built-in categories use 0-99; custom categories default to 100.</summary>
	public int Order { get; init; } = 100;
}
