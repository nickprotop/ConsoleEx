using SharpConsoleUI.Rendering;

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration options for Start menu appearance and behavior.
/// Use object initializer syntax to set only the properties you need.
/// </summary>
public class StartMenuOptions
{
	/// <summary>Layout mode: SingleColumn (compact) or TwoColumn (with window list).</summary>
	public StartMenuLayout Layout { get; init; } = StartMenuLayout.TwoColumn;

	/// <summary>Application name shown in the Start menu header. Defaults to "SharpConsoleUI".</summary>
	public string? AppName { get; init; }

	/// <summary>Application version shown in the Start menu header. Defaults to library version.</summary>
	public string? AppVersion { get; init; }

	/// <summary>Whether to show Unicode icons next to headers and exit.</summary>
	public bool ShowIcons { get; init; } = true;

	/// <summary>Icon displayed next to the app name in the header. Defaults to "☰" (U+2630).</summary>
	public string HeaderIcon { get; init; } = "\u2630";

	/// <summary>Show built-in System category (themes, settings, about, performance).</summary>
	public bool ShowSystemCategory { get; init; } = true;

	/// <summary>Show Windows list (right column in TwoColumn, submenu in SingleColumn).</summary>
	public bool ShowWindowList { get; init; } = true;

	/// <summary>Optional gradient background for the Start menu window.</summary>
	public GradientBackground? BackgroundGradient { get; init; }

	// Colors — null means resolve from theme via ColorResolver

	/// <summary>Background color. Null resolves from theme MenuDropdownBackgroundColor.</summary>
	public Color? BackgroundColor { get; init; }

	/// <summary>Foreground color. Null resolves from theme MenuDropdownForegroundColor.</summary>
	public Color? ForegroundColor { get; init; }

	/// <summary>Highlight background color. Null resolves from theme MenuDropdownHighlightBackgroundColor.</summary>
	public Color? HighlightBackgroundColor { get; init; }

	/// <summary>Highlight foreground color. Null resolves from theme MenuDropdownHighlightForegroundColor.</summary>
	public Color? HighlightForegroundColor { get; init; }
}
