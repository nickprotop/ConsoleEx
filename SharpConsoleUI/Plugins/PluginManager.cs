// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SharpConsoleUI.Plugins;

/// <summary>
/// Default implementation of the plugin manager
/// </summary>
public sealed class PluginManager : IPluginManager
{
    private readonly IServiceProvider _services;
    private readonly IPluginDiscovery _pluginDiscovery;
    private readonly ILogger<PluginManager> _logger;
    private readonly PluginOptions _options;
    private readonly ConcurrentDictionary<string, IPlugin> _loadedPlugins = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginManager"/> class
    /// </summary>
    /// <param name="services">The service provider</param>
    /// <param name="pluginDiscovery">The plugin discovery service</param>
    /// <param name="logger">The logger</param>
    /// <param name="options">The plugin options</param>
    public PluginManager(
        IServiceProvider services,
        IPluginDiscovery pluginDiscovery,
        ILogger<PluginManager> logger,
        IOptions<PluginOptions> options)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _pluginDiscovery = pluginDiscovery ?? throw new ArgumentNullException(nameof(pluginDiscovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins.Values.ToList();

    /// <inheritdoc />
    public event EventHandler<PluginLoadedEventArgs>? PluginLoaded;

    /// <inheritdoc />
    public event EventHandler<PluginUnloadedEventArgs>? PluginUnloaded;

    /// <inheritdoc />
    public event EventHandler<PluginErrorEventArgs>? PluginError;

    /// <inheritdoc />
    public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin
    {
        return _loadedPlugins.Values.OfType<T>();
    }

    /// <inheritdoc />
    public IPlugin? GetPlugin(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        _loadedPlugins.TryGetValue(name, out var plugin);
        return plugin;
    }

    /// <inheritdoc />
    public async Task LoadPluginsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableAutoDiscovery)
        {
            _logger.LogInformation("Plugin auto-discovery is disabled");
            return;
        }

        _logger.LogInformation("Starting plugin discovery in directory: {Directory}", _options.PluginDirectory);

        try
        {
            var assemblyPaths = await _pluginDiscovery.DiscoverPluginsAsync(cancellationToken);
            var loadTasks = new List<Task>();

            foreach (var assemblyPath in assemblyPaths)
            {
                loadTasks.Add(LoadPluginAssemblyAsync(assemblyPath, cancellationToken));
            }

            await Task.WhenAll(loadTasks);

            _logger.LogInformation("Plugin loading completed. Loaded {Count} plugins", _loadedPlugins.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin discovery and loading");
            OnPluginError(null, ex, "LoadPlugins");
        }
    }

    /// <inheritdoc />
    public async Task<IPlugin> LoadPluginAsync(Type pluginType, CancellationToken cancellationToken = default)
    {
        if (pluginType == null)
            throw new ArgumentNullException(nameof(pluginType));

        if (!typeof(IPlugin).IsAssignableFrom(pluginType))
            throw new ArgumentException($"Type {pluginType.FullName} does not implement IPlugin", nameof(pluginType));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Loading plugin {PluginType}", pluginType.FullName);

            // Create plugin instance
            var plugin = (IPlugin)ActivatorUtilities.CreateInstance(_services, pluginType);

            // Check if already loaded
            if (_loadedPlugins.ContainsKey(plugin.Info.Name))
            {
                _logger.LogWarning("Plugin {PluginName} is already loaded", plugin.Info.Name);
                return _loadedPlugins[plugin.Info.Name];
            }

            // Configure services for the plugin
            var serviceCollection = new ServiceCollection();
            plugin.ConfigureServices(serviceCollection);

            // Initialize the plugin
            await plugin.InitializeAsync(_services, cancellationToken);

            // Add to loaded plugins
            _loadedPlugins.TryAdd(plugin.Info.Name, plugin);

            stopwatch.Stop();
            _logger.LogInformation("Successfully loaded plugin {PluginName} v{Version} in {Duration}ms",
                plugin.Info.Name, plugin.Info.Version, stopwatch.ElapsedMilliseconds);

            OnPluginLoaded(plugin, stopwatch.Elapsed);
            return plugin;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to load plugin {PluginType}", pluginType.FullName);
            OnPluginError(pluginType.FullName, ex, "LoadPlugin");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UnloadPluginAsync(IPlugin plugin, CancellationToken cancellationToken = default)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        try
        {
            _logger.LogInformation("Unloading plugin {PluginName}", plugin.Info.Name);

            // Remove from loaded plugins
            _loadedPlugins.TryRemove(plugin.Info.Name, out _);

            // Dispose the plugin
            if (plugin is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                plugin.Dispose();
            }

            _logger.LogInformation("Successfully unloaded plugin {PluginName}", plugin.Info.Name);
            OnPluginUnloaded(plugin.Info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading plugin {PluginName}", plugin.Info.Name);
            OnPluginError(plugin.Info.Name, ex, "UnloadPlugin");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ReloadPluginsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reloading all plugins");

        // Unload all plugins
        var unloadTasks = _loadedPlugins.Values
            .Select(plugin => UnloadPluginAsync(plugin, cancellationToken))
            .ToArray();

        await Task.WhenAll(unloadTasks);

        // Load all plugins again
        await LoadPluginsAsync(cancellationToken);
    }

    private async Task LoadPluginAssemblyAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Loading plugin types from assembly: {AssemblyPath}", assemblyPath);

            var pluginTypes = _pluginDiscovery.LoadPluginTypes(assemblyPath);
            var loadTasks = pluginTypes
                .Where(type => !_options.ExcludedPlugins.Contains(type.FullName))
                .Select(type => LoadPluginAsync(type, cancellationToken));

            await Task.WhenAll(loadTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugins from assembly: {AssemblyPath}", assemblyPath);
            OnPluginError(assemblyPath, ex, "LoadAssembly");
        }
    }

    private void OnPluginLoaded(IPlugin plugin, TimeSpan loadDuration)
    {
        PluginLoaded?.Invoke(this, new PluginLoadedEventArgs(plugin, loadDuration));
    }

    private void OnPluginUnloaded(string pluginName)
    {
        PluginUnloaded?.Invoke(this, new PluginUnloadedEventArgs(pluginName));
    }

    private void OnPluginError(string? pluginName, Exception exception, string operation)
    {
        PluginError?.Invoke(this, new PluginErrorEventArgs(pluginName, exception, operation));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        var disposeTasks = _loadedPlugins.Values
            .Select(plugin => UnloadPluginAsync(plugin))
            .ToArray();

        try
        {
            Task.WaitAll(disposeTasks, TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin manager disposal");
        }

        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        var disposeTasks = _loadedPlugins.Values
            .Select(plugin => UnloadPluginAsync(plugin))
            .ToArray();

        try
        {
            await Task.WhenAll(disposeTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin manager async disposal");
        }

        _disposed = true;
    }
}