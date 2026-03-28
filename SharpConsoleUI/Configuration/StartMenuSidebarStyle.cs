namespace SharpConsoleUI.Configuration;

/// <summary>
/// Specifies the sidebar display style for the Start menu.
/// </summary>
public enum StartMenuSidebarStyle
{
	/// <summary>Narrow icon-only sidebar (NavigationView Compact mode).</summary>
	IconRail,
	/// <summary>Icon + text label sidebar (NavigationView Expanded mode).</summary>
	IconLabel,
	/// <summary>Text label with selection indicator, no icons (NavigationView Expanded mode).</summary>
	TextLabel
}
