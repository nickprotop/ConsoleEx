// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration options for the plugin system in ConsoleWindowSystem.
/// </summary>
/// <param name="AutoLoad">
/// Whether to automatically load plugins from the plugins directory on startup.
/// Defaults to false for security and performance reasons.
/// </param>
/// <param name="PluginsDirectory">
/// Path to the directory containing plugin DLLs. Can be absolute or relative to
/// the application's base directory. If null, uses the default "plugins" directory.
/// </param>
/// <param name="SearchSubdirectories">
/// Whether to search subdirectories when auto-loading plugins.
/// Defaults to true to support organized plugin structures.
/// </param>
/// <param name="FailOnLoadError">
/// Whether to throw an exception if a plugin fails to load during auto-load.
/// If false, failed plugins are logged and skipped. Defaults to false for resilience.
/// </param>
/// <param name="LoadOnlyPluginPattern">
/// Optional file pattern to filter which DLLs are loaded (e.g., "*.Plugin.dll").
/// If null, all DLLs in the plugins directory are scanned. Defaults to null.
/// </param>
public record PluginConfiguration(
	bool AutoLoad = false,
	string? PluginsDirectory = null,
	bool SearchSubdirectories = true,
	bool FailOnLoadError = false,
	string? LoadOnlyPluginPattern = null
)
{
	/// <summary>
	/// Gets the effective plugins directory path, resolving relative paths and
	/// using the default "plugins" directory if not specified.
	/// </summary>
	/// <returns>Absolute path to the plugins directory</returns>
	public string GetEffectivePluginsDirectory()
	{
		var dir = PluginsDirectory ?? "plugins";

		// If relative, make it relative to the application base directory
		if (!Path.IsPathRooted(dir))
		{
			var baseDir = AppContext.BaseDirectory;
			dir = Path.Combine(baseDir, dir);
		}

		return Path.GetFullPath(dir);
	}

	/// <summary>
	/// Default configuration with auto-loading disabled (safest option).
	/// </summary>
	public static PluginConfiguration Default => new();

	/// <summary>
	/// Configuration with auto-loading enabled from the default "plugins" directory.
	/// </summary>
	public static PluginConfiguration AutoLoadDefault => new(AutoLoad: true);

	/// <summary>
	/// Configuration for auto-loading from a custom directory.
	/// </summary>
	/// <param name="pluginsDirectory">Custom plugins directory path</param>
	/// <returns>Configuration with auto-loading enabled</returns>
	public static PluginConfiguration AutoLoadFrom(string pluginsDirectory) =>
		new(AutoLoad: true, PluginsDirectory: pluginsDirectory);

	/// <summary>
	/// Configuration for auto-loading with strict error handling (throws on load failure).
	/// </summary>
	public static PluginConfiguration AutoLoadStrict => new(AutoLoad: true, FailOnLoadError: true);
}
