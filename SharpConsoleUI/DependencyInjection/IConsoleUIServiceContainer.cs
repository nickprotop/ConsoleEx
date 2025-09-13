// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using SharpConsoleUI.Themes;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Plugins;

namespace SharpConsoleUI.DependencyInjection;

/// <summary>
/// Service container interface for SharpConsoleUI dependency injection
/// </summary>
public interface IConsoleUIServiceContainer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the underlying service provider
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets a service of the specified type
    /// </summary>
    /// <typeparam name="T">The type of service to get</typeparam>
    /// <returns>The service instance</returns>
    T GetService<T>() where T : class;

    /// <summary>
    /// Gets a required service of the specified type
    /// </summary>
    /// <typeparam name="T">The type of service to get</typeparam>
    /// <returns>The service instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not registered</exception>
    T GetRequiredService<T>() where T : class;

    /// <summary>
    /// Gets all services of the specified type
    /// </summary>
    /// <typeparam name="T">The type of services to get</typeparam>
    /// <returns>An enumerable of service instances</returns>
    IEnumerable<T> GetServices<T>() where T : class;

    /// <summary>
    /// Creates a new scope for scoped services
    /// </summary>
    /// <returns>A new service scope</returns>
    IServiceScope CreateScope();
}

/// <summary>
/// Builder interface for configuring SharpConsoleUI services
/// </summary>
public interface IConsoleUIServiceBuilder
{
    /// <summary>
    /// Gets the service collection being configured
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Adds a theme to the service container
    /// </summary>
    /// <typeparam name="T">The theme type</typeparam>
    /// <returns>The service builder for chaining</returns>
    IConsoleUIServiceBuilder AddTheme<T>() where T : class, ITheme;

    /// <summary>
    /// Adds a console driver to the service container
    /// </summary>
    /// <typeparam name="T">The console driver type</typeparam>
    /// <returns>The service builder for chaining</returns>
    IConsoleUIServiceBuilder AddConsoleDriver<T>() where T : class, IConsoleDriver;

    /// <summary>
    /// Adds plugin support to the service container
    /// </summary>
    /// <returns>The service builder for chaining</returns>
    IConsoleUIServiceBuilder AddPluginSupport();

    /// <summary>
    /// Adds plugin support with configuration
    /// </summary>
    /// <param name="configure">Plugin configuration action</param>
    /// <returns>The service builder for chaining</returns>
    IConsoleUIServiceBuilder AddPluginSupport(Action<IPluginConfiguration> configure);

    /// <summary>
    /// Builds the service container
    /// </summary>
    /// <returns>The configured service container</returns>
    IConsoleUIServiceContainer Build();
}