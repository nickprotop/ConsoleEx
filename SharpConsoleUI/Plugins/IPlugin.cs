// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@Gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Plugins;

/// <summary>
/// Plugin information record
/// </summary>
/// <param name="Name">The plugin name</param>
/// <param name="Version">The plugin version</param>
/// <param name="Author">The plugin author</param>
/// <param name="Description">The plugin description</param>
/// <param name="Dependencies">Plugin dependencies</param>
public sealed record PluginInfo(
    string Name,
    string Version,
    string Author,
    string Description,
    IReadOnlyList<string> Dependencies
);

/// <summary>
/// Base interface for all SharpConsoleUI plugins
/// </summary>
public interface IPlugin : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the plugin information
    /// </summary>
    PluginInfo Info { get; }

    /// <summary>
    /// Initializes the plugin
    /// </summary>
    /// <param name="services">The service provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the initialization operation</returns>
    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures services for the plugin
    /// </summary>
    /// <param name="services">The service collection</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Gets whether the plugin is initialized
    /// </summary>
    bool IsInitialized { get; }
}

/// <summary>
/// Interface for control plugins that provide custom controls
/// </summary>
public interface IControlPlugin : IPlugin
{
    /// <summary>
    /// Gets the types of controls provided by this plugin
    /// </summary>
    IReadOnlyList<Type> ControlTypes { get; }

    /// <summary>
    /// Creates a control instance of the specified type
    /// </summary>
    /// <param name="controlType">The control type</param>
    /// <param name="services">The service provider</param>
    /// <returns>The created control instance</returns>
    IWIndowControl CreateControl(Type controlType, IServiceProvider services);
}

/// <summary>
/// Interface for theme plugins that provide custom themes
/// </summary>
public interface IThemePlugin : IPlugin
{
    /// <summary>
    /// Gets the themes provided by this plugin
    /// </summary>
    IReadOnlyDictionary<string, Type> ThemeTypes { get; }

    /// <summary>
    /// Creates a theme instance of the specified type
    /// </summary>
    /// <param name="themeName">The theme name</param>
    /// <param name="services">The service provider</param>
    /// <returns>The created theme instance</returns>
    ITheme CreateTheme(string themeName, IServiceProvider services);
}

/// <summary>
/// Base abstract plugin class providing common functionality
/// </summary>
public abstract class PluginBase : IPlugin
{
    private bool _disposed;

    /// <inheritdoc />
    public abstract PluginInfo Info { get; }

    /// <inheritdoc />
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the service provider after initialization
    /// </summary>
    protected IServiceProvider? Services { get; private set; }

    /// <inheritdoc />
    public virtual async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
            return;

        Services = services ?? throw new ArgumentNullException(nameof(services));

        await OnInitializingAsync(cancellationToken);
        IsInitialized = true;
        await OnInitializedAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual void ConfigureServices(IServiceCollection services)
    {
        // Base implementation does nothing
    }

    /// <summary>
    /// Called during plugin initialization before IsInitialized is set to true
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the initialization operation</returns>
    protected virtual Task OnInitializingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called after plugin initialization when IsInitialized is set to true
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the initialization operation</returns>
    protected virtual Task OnInitializedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called during disposal
    /// </summary>
    protected virtual void OnDisposing() { }

    /// <summary>
    /// Called during async disposal
    /// </summary>
    protected virtual ValueTask OnDisposingAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        OnDisposing();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await OnDisposingAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}