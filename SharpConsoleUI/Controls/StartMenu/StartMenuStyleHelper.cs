// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Controls.StartMenu
{
	/// <summary>
	/// Provides formatting helpers for Start menu elements.
	/// Icon display is controlled by <see cref="Configuration.StartMenuOptions.ShowIcons"/>.
	/// </summary>
	internal static class StartMenuStyleHelper
	{
		/// <summary>
		/// Formats the application header with name and version.
		/// Returns multiple markup lines for MarkupControl.
		/// </summary>
		public static List<string> FormatAppHeaderLines(string appName, string version, bool showIcons, string headerIcon = "\u2630")
		{
			var icon = showIcons ? $"{headerIcon} " : "";
			return new List<string>
			{
				$"{icon}[bold]{appName}[/]",
				$"  v{version}",
			};
		}

		/// <summary>
		/// Formats the info strip with theme name, window count, and plugin count.
		/// Returns multiple markup lines for MarkupControl.
		/// </summary>
		public static List<string> FormatInfoStripLines(string themeName, int windowCount, int pluginCount)
		{
			var windowLabel = windowCount == 1 ? "Window" : "Windows";
			var pluginLabel = pluginCount == 1 ? "Plugin" : "Plugins";

			return new List<string>
			{
				$"Theme: {themeName}",
				$"{windowLabel}: {windowCount}  {pluginLabel}: {pluginCount}",
			};
		}

		/// <summary>
		/// Formats a window list item with state indicator and optional keyboard shortcut.
		/// </summary>
		public static string FormatWindowItem(string title, int index, bool isMinimized, bool isActive)
		{
			var indicator = isMinimized
				? ControlDefaults.StartMenuMinimizedWindowIndicator
				: ControlDefaults.StartMenuActiveWindowIndicator;
			var shortcut = index < 9 ? $"  Alt+{index + 1}" : "";
			var dimStart = isMinimized ? "[dim]" : "";
			var dimEnd = isMinimized ? "[/]" : "";
			var activeStart = isActive ? "[bold]" : "";
			var activeEnd = isActive ? "[/]" : "";

			return $"{indicator} {dimStart}{activeStart}{title}{activeEnd}{dimEnd}{shortcut}";
		}

	}
}
