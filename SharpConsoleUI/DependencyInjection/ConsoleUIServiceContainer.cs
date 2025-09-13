// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SharpConsoleUI.Themes;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Core;
using SharpConsoleUI.Plugins;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.DependencyInjection;

/// <summary>
/// Default implementation of the SharpConsoleUI service container
/// </summary>
public sealed class ConsoleUIServiceContainer : IConsoleUIServiceContainer
{
    private readonly ServiceProvider _serviceProvider;
    private bool _disposed;

    internal ConsoleUIServiceContainer(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public IServiceProvider Services => _serviceProvider;

    /// <inheritdoc />
    public T GetService<T>() where T : class => _serviceProvider.GetService<T>()!;

    /// <inheritdoc />
    public T GetRequiredService<T>() where T : class => _serviceProvider.GetRequiredService<T>();

    /// <inheritdoc />
    public IEnumerable<T> GetServices<T>() where T : class => _serviceProvider.GetServices<T>();

    /// <inheritdoc />
    public IServiceScope CreateScope() => _serviceProvider.CreateScope();

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _serviceProvider?.Dispose();
            _disposed = true;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                _serviceProvider?.Dispose();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Builder for configuring SharpConsoleUI services
/// </summary>
public sealed class ConsoleUIServiceBuilder : IConsoleUIServiceBuilder
{
    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleUIServiceBuilder"/> class
    /// </summary>
    public ConsoleUIServiceBuilder() : this(new ServiceCollection())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleUIServiceBuilder"/> class
    /// </summary>
    /// <param name="services">The service collection to use</param>
    public ConsoleUIServiceBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        RegisterCoreServices();
    }

    /// <inheritdoc />
    public IConsoleUIServiceBuilder AddTheme<T>() where T : class, ITheme
    {
        Services.AddSingleton<ITheme, T>();
        return this;
    }

    /// <inheritdoc />
    public IConsoleUIServiceBuilder AddConsoleDriver<T>() where T : class, IConsoleDriver
    {
        Services.AddSingleton<IConsoleDriver, T>();
        return this;
    }

    /// <inheritdoc />
    public IConsoleUIServiceBuilder AddPluginSupport()
    {
        Services.AddSingleton<IPluginManager, PluginManager>();
        Services.AddSingleton<IPluginDiscovery, FileSystemPluginDiscovery>();
        return this;
    }

    /// <inheritdoc />
    public IConsoleUIServiceBuilder AddPluginSupport(Action<IPluginConfiguration> configure)
    {
        AddPluginSupport();
        Services.Configure<PluginOptions>(options => configure(options));
        return this;
    }

    /// <inheritdoc />
    public IConsoleUIServiceContainer Build()
    {
        var serviceProvider = Services.BuildServiceProvider();
        return new ConsoleUIServiceContainer(serviceProvider);
    }

    private void RegisterCoreServices()
    {
        // Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("sharpconsoleui.json", optional: true, reloadOnChange: true)
            .Build();

        Services.AddSingleton<IConfiguration>(configuration);

        // Logging
        Services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddConfiguration(configuration.GetSection("Logging"));
        });

        // Core services
        Services.AddSingleton<InvalidationManager>();
        Services.AddTransient(typeof(ThreadSafeCache<>));

        // Default theme
        Services.AddSingleton<ITheme, Theme>();

        // Configuration options
        Services.Configure<ConsoleUIOptions>(configuration.GetSection("SharpConsoleUI"));
        Services.Configure<ThemeOptions>(configuration.GetSection("SharpConsoleUI:Theme"));
    }
}

/// <summary>
/// Extension methods for easy service container setup
/// </summary>
public static class ConsoleUIServiceExtensions
{
    /// <summary>
    /// Creates a new service builder for SharpConsoleUI
    /// </summary>
    /// <returns>A new service builder instance</returns>
    public static IConsoleUIServiceBuilder CreateBuilder() => new ConsoleUIServiceBuilder();

    /// <summary>
    /// Creates a new service builder with an existing service collection
    /// </summary>
    /// <param name="services">The service collection to use</param>
    /// <returns>A new service builder instance</returns>
    public static IConsoleUIServiceBuilder CreateBuilder(IServiceCollection services) => new ConsoleUIServiceBuilder(services);

    /// <summary>
    /// Adds SharpConsoleUI services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSharpConsoleUI(this IServiceCollection services)
    {
        return new ConsoleUIServiceBuilder(services).Services;
    }

    /// <summary>
    /// Adds SharpConsoleUI services to the service collection with configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSharpConsoleUI(this IServiceCollection services, Action<IConsoleUIServiceBuilder> configure)
    {
        var builder = new ConsoleUIServiceBuilder(services);
        configure(builder);
        return services;
    }
}