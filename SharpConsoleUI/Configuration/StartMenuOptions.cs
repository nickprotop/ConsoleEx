using SharpConsoleUI.Rendering;

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration options for Start menu appearance and behavior.
/// </summary>
public record StartMenuOptions(
	/// <summary>Layout mode: SingleColumn (compact) or TwoColumn (with window list).</summary>
	StartMenuLayout Layout = StartMenuLayout.TwoColumn,

	/// <summary>Application name shown in the Start menu header. Defaults to "SharpConsoleUI".</summary>
	string? AppName = null,

	/// <summary>Application version shown in the Start menu header. Defaults to library version.</summary>
	string? AppVersion = null,

	/// <summary>Whether to show Unicode icons next to categories, headers, and exit.</summary>
	bool ShowIcons = true,

	/// <summary>Icon displayed next to the app name in the header. Defaults to "☰" (U+2630).</summary>
	string HeaderIcon = "\u2630",

	/// <summary>Show built-in System category (themes, settings, about, performance).</summary>
	bool ShowSystemCategory = true,

	/// <summary>Show Windows category with open window list.</summary>
	bool ShowWindowList = true,

	/// <summary>Optional gradient background for the Start menu window.</summary>
	GradientBackground? BackgroundGradient = null
);
