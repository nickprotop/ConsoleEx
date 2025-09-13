// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

namespace SharpConsoleUI.Plugins;

/// <summary>
/// Plugin configuration interface
/// </summary>
public interface IPluginConfiguration
{
    /// <summary>
    /// Gets or sets the plugin directory
    /// </summary>
    string PluginDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether to enable automatic plugin discovery
    /// </summary>
    bool EnableAutoDiscovery { get; set; }

    /// <summary>
    /// Gets or sets the plugin file patterns to search for
    /// </summary>
    IList<string> PluginPatterns { get; }

    /// <summary>
    /// Gets or sets plugins to exclude from loading
    /// </summary>
    IList<string> ExcludedPlugins { get; }
}

/// <summary>
/// Plugin configuration options
/// </summary>
public sealed class PluginOptions : IPluginConfiguration
{
    /// <inheritdoc />
    public string PluginDirectory { get; set; } = "plugins";

    /// <inheritdoc />
    public bool EnableAutoDiscovery { get; set; } = true;

    /// <inheritdoc />
    public IList<string> PluginPatterns { get; } = new List<string> { "*.Plugin.dll", "*Plugin.dll" };

    /// <inheritdoc />
    public IList<string> ExcludedPlugins { get; } = new List<string>();
}

/// <summary>
/// Plugin discovery service interface
/// </summary>
public interface IPluginDiscovery
{
    /// <summary>
    /// Discovers plugins in the configured locations
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of discovered plugin assemblies</returns>
    Task<IEnumerable<string>> DiscoverPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads plugin types from an assembly path
    /// </summary>
    /// <param name="assemblyPath">The assembly path</param>
    /// <returns>A collection of plugin types</returns>
    IEnumerable<Type> LoadPluginTypes(string assemblyPath);
}

/// <summary>
/// Plugin manager interface for managing the lifecycle of plugins
/// </summary>
public interface IPluginManager : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets all loaded plugins
    /// </summary>
    IReadOnlyList<IPlugin> LoadedPlugins { get; }

    /// <summary>
    /// Gets plugins of a specific type
    /// </summary>
    /// <typeparam name="T">The plugin type</typeparam>
    /// <returns>A collection of plugins of the specified type</returns>
    IEnumerable<T> GetPlugins<T>() where T : class, IPlugin;

    /// <summary>
    /// Gets a plugin by name
    /// </summary>
    /// <param name="name">The plugin name</param>
    /// <returns>The plugin instance, or null if not found</returns>
    IPlugin? GetPlugin(string name);

    /// <summary>
    /// Loads and initializes all discovered plugins
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the loading operation</returns>
    Task LoadPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a specific plugin
    /// </summary>
    /// <param name="pluginType">The plugin type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The loaded plugin instance</returns>
    Task<IPlugin> LoadPluginAsync(Type pluginType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads a plugin
    /// </summary>
    /// <param name="plugin">The plugin to unload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the unloading operation</returns>
    Task UnloadPluginAsync(IPlugin plugin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads all plugins
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the reload operation</returns>
    Task ReloadPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when a plugin is loaded
    /// </summary>
    event EventHandler<PluginLoadedEventArgs>? PluginLoaded;

    /// <summary>
    /// Event fired when a plugin is unloaded
    /// </summary>
    event EventHandler<PluginUnloadedEventArgs>? PluginUnloaded;

    /// <summary>
    /// Event fired when a plugin fails to load
    /// </summary>
    event EventHandler<PluginErrorEventArgs>? PluginError;
}

/// <summary>
/// Event arguments for plugin loaded events
/// </summary>
public sealed class PluginLoadedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the loaded plugin
    /// </summary>
    public IPlugin Plugin { get; }

    /// <summary>
    /// Gets the load duration
    /// </summary>
    public TimeSpan LoadDuration { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadedEventArgs"/> class
    /// </summary>
    /// <param name="plugin">The loaded plugin</param>
    /// <param name="loadDuration">The load duration</param>
    public PluginLoadedEventArgs(IPlugin plugin, TimeSpan loadDuration)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        LoadDuration = loadDuration;
    }
}

/// <summary>
/// Event arguments for plugin unloaded events
/// </summary>
public sealed class PluginUnloadedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the plugin name
    /// </summary>
    public string PluginName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUnloadedEventArgs"/> class
    /// </summary>
    /// <param name="pluginName">The plugin name</param>
    public PluginUnloadedEventArgs(string pluginName)
    {
        PluginName = pluginName ?? throw new ArgumentNullException(nameof(pluginName));
    }
}

/// <summary>
/// Event arguments for plugin error events
/// </summary>
public sealed class PluginErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets the plugin name
    /// </summary>
    public string? PluginName { get; }

    /// <summary>
    /// Gets the error that occurred
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the error operation
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginErrorEventArgs"/> class
    /// </summary>
    /// <param name="pluginName">The plugin name</param>
    /// <param name="exception">The error that occurred</param>
    /// <param name="operation">The error operation</param>
    public PluginErrorEventArgs(string? pluginName, Exception exception, string operation)
    {
        PluginName = pluginName;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }
}