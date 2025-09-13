// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SharpConsoleUI.Plugins;

/// <summary>
/// File system-based plugin discovery implementation
/// </summary>
public sealed class FileSystemPluginDiscovery : IPluginDiscovery
{
    private readonly ILogger<FileSystemPluginDiscovery> _logger;
    private readonly PluginOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemPluginDiscovery"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="options">The plugin options</param>
    public FileSystemPluginDiscovery(
        ILogger<FileSystemPluginDiscovery> logger,
        IOptions<PluginOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> DiscoverPluginsAsync(CancellationToken cancellationToken = default)
    {
        var pluginAssemblies = new List<string>();

        if (!Directory.Exists(_options.PluginDirectory))
        {
            _logger.LogWarning("Plugin directory does not exist: {Directory}", _options.PluginDirectory);
            return pluginAssemblies;
        }

        _logger.LogInformation("Discovering plugins in directory: {Directory}", _options.PluginDirectory);

        foreach (var pattern in _options.PluginPatterns)
        {
            try
            {
                var files = Directory.GetFiles(_options.PluginDirectory, pattern, SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (IsValidPluginAssembly(file))
                    {
                        pluginAssemblies.Add(file);
                        _logger.LogDebug("Discovered plugin assembly: {Assembly}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for plugins with pattern {Pattern}", pattern);
            }
        }

        _logger.LogInformation("Discovered {Count} plugin assemblies", pluginAssemblies.Count);
        return pluginAssemblies;
    }

    /// <inheritdoc />
    public IEnumerable<Type> LoadPluginTypes(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path cannot be null or empty", nameof(assemblyPath));

        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}");

        var pluginTypes = new List<Type>();

        try
        {
            _logger.LogDebug("Loading assembly: {AssemblyPath}", assemblyPath);

            // Load the assembly
            var assembly = Assembly.LoadFrom(assemblyPath);

            // Find all types that implement IPlugin
            var types = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IPlugin).IsAssignableFrom(t))
                .ToArray();

            foreach (var type in types)
            {
                if (IsValidPluginType(type))
                {
                    pluginTypes.Add(type);
                    _logger.LogDebug("Found plugin type: {PluginType} in {Assembly}", type.FullName, assemblyPath);
                }
            }

            _logger.LogInformation("Loaded {Count} plugin types from {Assembly}", pluginTypes.Count, assemblyPath);
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger.LogError(ex, "Failed to load types from assembly {Assembly}. Loader exceptions:", assemblyPath);

            foreach (var loaderException in ex.LoaderExceptions)
            {
                if (loaderException != null)
                {
                    _logger.LogError(loaderException, "Loader exception details");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin assembly: {AssemblyPath}", assemblyPath);
        }

        return pluginTypes;
    }

    private bool IsValidPluginAssembly(string assemblyPath)
    {
        try
        {
            // Basic validation - check if file is a valid .NET assembly
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);

            // Additional checks could be added here
            // - Verify assembly is not corrupted
            // - Check digital signature if required
            // - Verify compatibility with current runtime

            return assemblyName != null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Invalid plugin assembly: {Assembly}", assemblyPath);
            return false;
        }
    }

    private bool IsValidPluginType(Type type)
    {
        try
        {
            // Check if type has a public parameterless constructor or a constructor that can be satisfied by DI
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            return constructors.Length > 0 && (
                constructors.Any(c => c.GetParameters().Length == 0) || // Parameterless constructor
                constructors.Any(c => c.GetParameters().All(p => p.ParameterType.IsInterface)) // DI constructor
            );
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Invalid plugin type: {PluginType}", type.FullName);
            return false;
        }
    }
}