// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Plugins;

/// <summary>
/// Metadata about a plugin
/// </summary>
/// <param name="Name">The plugin name</param>
/// <param name="Version">The plugin version</param>
/// <param name="Author">The plugin author</param>
/// <param name="Description">A description of what the plugin provides</param>
public record PluginInfo(string Name, string Version, string Author, string Description);

/// <summary>
/// A theme contribution from a plugin
/// </summary>
/// <param name="Name">The unique name for the theme</param>
/// <param name="Description">A description of the theme</param>
/// <param name="Theme">The theme instance</param>
public record PluginTheme(string Name, string Description, ITheme Theme);

/// <summary>
/// A control factory contribution from a plugin
/// </summary>
/// <param name="Name">The unique name for the control</param>
/// <param name="Factory">A factory function that creates control instances</param>
public record PluginControl(string Name, Func<IWindowControl> Factory);

/// <summary>
/// A window/dialog factory contribution from a plugin
/// </summary>
/// <param name="Name">The unique name for the window</param>
/// <param name="Factory">A factory function that creates window instances</param>
public record PluginWindow(string Name, Func<ConsoleWindowSystem, Window> Factory);

/// <summary>
/// A service contribution from a plugin
/// </summary>
/// <param name="ServiceType">The service type (usually an interface)</param>
/// <param name="Instance">The service instance</param>
public record PluginService(Type ServiceType, object Instance);

/// <summary>
/// Simple plugin interface. Plugins return what they provide via Get* methods.
/// </summary>
public interface IPlugin : IDisposable
{
	/// <summary>
	/// Gets the plugin metadata
	/// </summary>
	PluginInfo Info { get; }

	/// <summary>
	/// Gets the themes provided by this plugin
	/// </summary>
	/// <returns>A list of themes, or empty if the plugin provides no themes</returns>
	IReadOnlyList<PluginTheme> GetThemes();

	/// <summary>
	/// Gets the control factories provided by this plugin
	/// </summary>
	/// <returns>A list of control factories, or empty if the plugin provides no controls</returns>
	IReadOnlyList<PluginControl> GetControls();

	/// <summary>
	/// Gets the window/dialog factories provided by this plugin
	/// </summary>
	/// <returns>A list of window factories, or empty if the plugin provides no windows</returns>
	IReadOnlyList<PluginWindow> GetWindows();

	/// <summary>
	/// Gets the services provided by this plugin
	/// </summary>
	/// <returns>A list of services, or empty if the plugin provides no services</returns>
	IReadOnlyList<PluginService> GetServices();

	/// <summary>
	/// Called when the plugin is loaded, before Get* methods are called.
	/// Use this for any initialization that requires access to the window system.
	/// </summary>
	/// <param name="windowSystem">The window system that loaded the plugin</param>
	void Initialize(ConsoleWindowSystem windowSystem);
}

/// <summary>
/// Base class for plugins with default empty implementations.
/// Inherit from this class and override only the methods you need.
/// </summary>
public abstract class PluginBase : IPlugin
{
	/// <inheritdoc/>
	public abstract PluginInfo Info { get; }

	/// <inheritdoc/>
	public virtual IReadOnlyList<PluginTheme> GetThemes() => Array.Empty<PluginTheme>();

	/// <inheritdoc/>
	public virtual IReadOnlyList<PluginControl> GetControls() => Array.Empty<PluginControl>();

	/// <inheritdoc/>
	public virtual IReadOnlyList<PluginWindow> GetWindows() => Array.Empty<PluginWindow>();

	/// <inheritdoc/>
	public virtual IReadOnlyList<PluginService> GetServices() => Array.Empty<PluginService>();

	/// <inheritdoc/>
	public virtual void Initialize(ConsoleWindowSystem windowSystem) { }

	/// <inheritdoc/>
	public virtual void Dispose() { }
}
