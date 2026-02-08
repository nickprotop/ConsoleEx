// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Plugins;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Immutable record representing the current state of the plugin system.
	/// </summary>
	public record PluginState(
		int LoadedPluginCount,
		int RegisteredServiceCount,
		int RegisteredControlCount,
		int RegisteredWindowCount,
		IReadOnlyList<string> PluginNames,
		bool AutoLoadEnabled,
		string? PluginsDirectory
	);

	/// <summary>
	/// Event arguments for plugin state changes.
	/// </summary>
	public class PluginStateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the previous plugin state.
		/// </summary>
		public PluginState PreviousState { get; }

		/// <summary>
		/// Gets the new plugin state.
		/// </summary>
		public PluginState NewState { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PluginStateChangedEventArgs"/> class.
		/// </summary>
		/// <param name="previousState">The previous state.</param>
		/// <param name="newState">The new state.</param>
		public PluginStateChangedEventArgs(PluginState previousState, PluginState newState)
		{
			PreviousState = previousState;
			NewState = newState;
		}
	}

	/// <summary>
	/// Event arguments for plugin load/unload events.
	/// </summary>
	public class PluginEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the plugin instance.
		/// </summary>
		public IPlugin Plugin { get; }

		/// <summary>
		/// Gets the plugin information.
		/// </summary>
		public PluginInfo Info { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PluginEventArgs"/> class.
		/// </summary>
		/// <param name="plugin">The plugin instance.</param>
		public PluginEventArgs(IPlugin plugin)
		{
			Plugin = plugin;
			Info = plugin.Info;
		}
	}

	/// <summary>
	/// Event arguments for service registration events.
	/// </summary>
	public class ServiceRegisteredEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the service name.
		/// </summary>
		public string ServiceName { get; }

		/// <summary>
		/// Gets the service instance.
		/// </summary>
		public IPluginService Service { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceRegisteredEventArgs"/> class.
		/// </summary>
		/// <param name="serviceName">The service name.</param>
		/// <param name="service">The service instance.</param>
		public ServiceRegisteredEventArgs(string serviceName, IPluginService service)
		{
			ServiceName = serviceName;
			Service = service;
		}
	}

	/// <summary>
	/// Event arguments for service unregistration events.
	/// </summary>
	public class ServiceUnregisteredEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the service name.
		/// </summary>
		public string ServiceName { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceUnregisteredEventArgs"/> class.
		/// </summary>
		/// <param name="serviceName">The service name.</param>
		public ServiceUnregisteredEventArgs(string serviceName)
		{
			ServiceName = serviceName;
		}
	}

	/// <summary>
	/// Service that manages the plugin system state, including plugin loading, service registration,
	/// and factory management. This service follows the established manager pattern for state management.
	/// </summary>
	public class PluginStateService : IDisposable
	{
		private readonly ConsoleWindowSystem _windowSystem;
		private readonly ILogService? _logService;
		private readonly object _lock = new();
		private PluginConfiguration _configuration;

		// Plugin storage
		private readonly List<IPlugin> _loadedPlugins = new();
		private readonly Dictionary<string, Func<IWindowControl>> _controlFactories = new();
		private readonly Dictionary<string, Func<ConsoleWindowSystem, Window>> _windowFactories = new();
		private readonly Dictionary<Type, object> _legacyServices = new(); // Legacy type-based services
		private readonly Dictionary<string, IPluginService> _services = new(); // New name-based service plugins
		private readonly Dictionary<string, IPluginActionProvider> _actionProviders = new(); // Action providers for Start menu

		private bool _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="PluginStateService"/> class.
		/// </summary>
		/// <param name="windowSystem">The console window system instance.</param>
		/// <param name="logService">Optional log service for diagnostics.</param>
		/// <param name="configuration">Optional plugin configuration.</param>
		public PluginStateService(
			ConsoleWindowSystem windowSystem,
			ILogService? logService = null,
			PluginConfiguration? configuration = null)
		{
			_windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
			_logService = logService;
			_configuration = configuration ?? PluginConfiguration.Default;
		}

		/// <summary>
		/// Gets the current state of the plugin system.
		/// </summary>
		public PluginState CurrentState
		{
			get
			{
				lock (_lock)
				{
					return new PluginState(
						LoadedPluginCount: _loadedPlugins.Count,
						RegisteredServiceCount: _services.Count,
						RegisteredControlCount: _controlFactories.Count,
						RegisteredWindowCount: _windowFactories.Count,
						PluginNames: _loadedPlugins.Select(p => p.Info.Name).ToList().AsReadOnly(),
						AutoLoadEnabled: _configuration.AutoLoad,
						PluginsDirectory: _configuration.PluginsDirectory
					);
				}
			}
		}

		/// <summary>
		/// Gets the plugin configuration.
		/// </summary>
		public PluginConfiguration Configuration
		{
			get
			{
				lock (_lock)
				{
					return _configuration;
				}
			}
		}

		/// <summary>
		/// Gets the list of loaded plugins.
		/// </summary>
		public IReadOnlyList<IPlugin> LoadedPlugins
		{
			get
			{
				lock (_lock)
				{
					return _loadedPlugins.AsReadOnly();
				}
			}
		}

		/// <summary>
		/// Gets the names of all registered plugin controls.
		/// </summary>
		public IReadOnlyCollection<string> RegisteredControlNames
		{
			get
			{
				lock (_lock)
				{
					return _controlFactories.Keys.ToList().AsReadOnly();
				}
			}
		}

		/// <summary>
		/// Gets the names of all registered plugin windows.
		/// </summary>
		public IReadOnlyCollection<string> RegisteredWindowNames
		{
			get
			{
				lock (_lock)
				{
					return _windowFactories.Keys.ToList().AsReadOnly();
				}
			}
		}

		/// <summary>
		/// Gets the names of all registered action providers.
		/// </summary>
		public IReadOnlyCollection<string> RegisteredActionProviderNames
		{
			get
			{
				lock (_lock)
				{
					return _actionProviders.Keys.ToList().AsReadOnly();
				}
			}
		}

		/// <summary>
		/// Gets the types of all registered plugin services (legacy pattern).
		/// </summary>
		[Obsolete("Legacy type-based services are deprecated. Use RegisteredServiceNames instead.")]
		public IReadOnlyCollection<Type> RegisteredLegacyServiceTypes
		{
			get
			{
				lock (_lock)
				{
					return _legacyServices.Keys.ToList().AsReadOnly();
				}
			}
		}

		/// <summary>
		/// Gets the names of all registered service plugins.
		/// </summary>
		public IReadOnlyCollection<string> RegisteredServiceNames
		{
			get
			{
				lock (_lock)
				{
					return _services.Keys.ToList().AsReadOnly();
				}
			}
		}

		/// <summary>
		/// Gets all registered service plugin instances.
		/// </summary>
		public IReadOnlyCollection<IPluginService> RegisteredServices
		{
			get
			{
				lock (_lock)
				{
					return _services.Values.ToList().AsReadOnly();
				}
			}
		}

		/// <summary>
		/// Event raised when the plugin state changes.
		/// </summary>
		public event EventHandler<PluginStateChangedEventArgs>? StateChanged;

		/// <summary>
		/// Event raised when a plugin is loaded.
		/// </summary>
		public event EventHandler<PluginEventArgs>? PluginLoaded;

		/// <summary>
		/// Event raised when a plugin is unloaded.
		/// </summary>
		public event EventHandler<PluginEventArgs>? PluginUnloaded;

		/// <summary>
		/// Event raised when a service is registered.
		/// </summary>
		public event EventHandler<ServiceRegisteredEventArgs>? ServiceRegistered;

		/// <summary>
		/// Event raised when a service is unregistered.
		/// </summary>
#pragma warning disable CS0067 // Event is never used (reserved for future plugin unloading support)
		public event EventHandler<ServiceUnregisteredEventArgs>? ServiceUnregistered;
#pragma warning restore CS0067

		/// <summary>
		/// Updates the plugin configuration.
		/// </summary>
		/// <param name="configuration">The new configuration.</param>
		public void UpdateConfiguration(PluginConfiguration configuration)
		{
			lock (_lock)
			{
				var previousState = CurrentState;
				_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
				var newState = CurrentState;
				StateChanged?.Invoke(this, new PluginStateChangedEventArgs(previousState, newState));
			}
		}

		/// <summary>
		/// Loads plugins from the specified directory.
		/// If no path is specified, uses the "plugins" subdirectory of the application's base directory.
		/// </summary>
		/// <param name="pluginsPath">Optional path to the plugins directory.</param>
		[RequiresUnreferencedCode("Plugin loading from files uses Assembly.LoadFrom which is not compatible with trimming.")]
		public void LoadPluginsFromDirectory(string? pluginsPath = null)
		{
			pluginsPath ??= Path.Combine(AppContext.BaseDirectory, "plugins");
			if (!Directory.Exists(pluginsPath))
			{
				_logService?.LogDebug($"Plugin directory not found: {pluginsPath}", "Plugins");
				return;
			}

			foreach (var dll in Directory.GetFiles(pluginsPath, "*.dll"))
			{
				LoadPluginFromFile(dll);
			}
		}

		/// <summary>
		/// Loads a plugin from a specific DLL file path (agnostic loading).
		/// </summary>
		/// <param name="dllPath">The path to the plugin DLL file.</param>
		/// <exception cref="ArgumentNullException">Thrown if dllPath is null or empty.</exception>
		[RequiresUnreferencedCode("Plugin loading from files uses Assembly.LoadFrom which is not compatible with trimming.")]
		public void LoadPlugin(string dllPath)
		{
			if (string.IsNullOrWhiteSpace(dllPath))
				throw new ArgumentNullException(nameof(dllPath));

			LoadPluginFromFile(dllPath);
		}

		/// <summary>
		/// Internal helper method to load plugins from a DLL file using the convention-based entry point.
		/// The plugin assembly must contain a public static class named "PluginEntry" with a static
		/// method "CreatePlugins()" returning IEnumerable&lt;IPlugin&gt;.
		/// </summary>
		/// <param name="dllPath">The path to the DLL file.</param>
		[RequiresUnreferencedCode("Plugin loading from files uses Assembly.LoadFrom which is not compatible with trimming.")]
		private void LoadPluginFromFile(string dllPath)
		{
			try
			{
				var assembly = System.Reflection.Assembly.LoadFrom(dllPath);
				var entryType = assembly.GetType(PluginEntryConvention.EntryClassName);
				if (entryType == null)
				{
					_logService?.LogWarning($"No {PluginEntryConvention.EntryClassName} class found in {Path.GetFileName(dllPath)}", "Plugins");
					return;
				}

				var method = entryType.GetMethod(PluginEntryConvention.FactoryMethodName,
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
				if (method == null)
				{
					_logService?.LogWarning($"{PluginEntryConvention.EntryClassName}.{PluginEntryConvention.FactoryMethodName}() not found in {Path.GetFileName(dllPath)}", "Plugins");
					return;
				}

				if (method.Invoke(null, null) is IEnumerable<IPlugin> plugins)
				{
					foreach (var plugin in plugins)
					{
						LoadPlugin(plugin);
						_logService?.LogInfo($"Loaded plugin: {plugin.Info.Name} v{plugin.Info.Version} from {Path.GetFileName(dllPath)}", "Plugins");
					}
				}
			}
			catch (Exception ex)
			{
				_logService?.LogError($"Failed to load plugin from {dllPath}: {ex.Message}", ex, "Plugins");
			}
		}

		/// <summary>
		/// Loads a plugin instance and registers its contributions.
		/// </summary>
		/// <param name="plugin">The plugin to load.</param>
		public void LoadPlugin(IPlugin plugin)
		{
			if (plugin == null)
				throw new ArgumentNullException(nameof(plugin));

			lock (_lock)
			{
				var previousState = CurrentState;

				// Initialize the plugin
				plugin.Initialize(_windowSystem);

				// Register themes to ThemeRegistry
				foreach (var theme in plugin.GetThemes())
				{
					ThemeRegistry.RegisterTheme(theme.Name, theme.Description, () => theme.Theme);
					_logService?.LogDebug($"Registered theme: {theme.Name}", "Plugins");
				}

				// Register control factories
				foreach (var control in plugin.GetControls())
				{
					_controlFactories[control.Name] = control.Factory;
					_logService?.LogDebug($"Registered control: {control.Name}", "Plugins");
				}

				// Register window factories
				foreach (var window in plugin.GetWindows())
				{
					_windowFactories[window.Name] = window.Factory;
					_logService?.LogDebug($"Registered window: {window.Name}", "Plugins");
				}

				// Register legacy type-based services
#pragma warning disable CS0618 // Type or member is obsolete
				foreach (var service in plugin.GetServices())
				{
					_legacyServices[service.ServiceType] = service.Instance;
					_logService?.LogDebug($"Registered legacy service: {service.ServiceType.Name}", "Plugins");
				}
#pragma warning restore CS0618 // Type or member is obsolete

				// Register new name-based service plugins
				foreach (var servicePlugin in plugin.GetServicePlugins())
				{
					_services[servicePlugin.ServiceName] = servicePlugin;
					_logService?.LogDebug($"Registered service plugin: {servicePlugin.ServiceName}", "Plugins");
					ServiceRegistered?.Invoke(this, new ServiceRegisteredEventArgs(servicePlugin.ServiceName, servicePlugin));
				}

				// Register action providers
				foreach (var actionProvider in plugin.GetActionProviders())
				{
					if (_actionProviders.ContainsKey(actionProvider.ProviderName))
					{
						_logService?.LogWarning($"Action provider '{actionProvider.ProviderName}' already registered, skipping", "Plugins");
						continue;
					}

					_actionProviders[actionProvider.ProviderName] = actionProvider;
					_logService?.LogDebug($"Registered action provider: {actionProvider.ProviderName}", "Plugins");
				}

				_loadedPlugins.Add(plugin);

				var newState = CurrentState;
				PluginLoaded?.Invoke(this, new PluginEventArgs(plugin));
				StateChanged?.Invoke(this, new PluginStateChangedEventArgs(previousState, newState));
			}
		}

		/// <summary>
		/// Loads a plugin of the specified type.
		/// </summary>
		/// <typeparam name="T">The plugin type to instantiate and load.</typeparam>
		public void LoadPlugin<T>() where T : IPlugin, new()
		{
			LoadPlugin(new T());
		}

		/// <summary>
		/// Unloads a plugin and removes its contributions.
		/// Note: This is a stub implementation. Full unloading support will be added in a future version.
		/// </summary>
		/// <param name="plugin">The plugin to unload.</param>
		[Obsolete("Plugin unloading is not fully implemented yet. This method disposes the plugin but does not remove its registered contributions.")]
		public void UnloadPlugin(IPlugin plugin)
		{
			if (plugin == null)
				throw new ArgumentNullException(nameof(plugin));

			lock (_lock)
			{
				var previousState = CurrentState;

				if (_loadedPlugins.Remove(plugin))
				{
					plugin.Dispose();

					var newState = CurrentState;
					PluginUnloaded?.Invoke(this, new PluginEventArgs(plugin));
					StateChanged?.Invoke(this, new PluginStateChangedEventArgs(previousState, newState));

					_logService?.LogInfo($"Unloaded plugin: {plugin.Info.Name}", "Plugins");
				}
			}
		}

		/// <summary>
		/// Gets a plugin by name.
		/// </summary>
		/// <param name="name">The plugin name to search for.</param>
		/// <returns>The plugin instance, or null if not found.</returns>
		public IPlugin? GetPlugin(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException(nameof(name));

			lock (_lock)
			{
				return _loadedPlugins.FirstOrDefault(p => p.Info.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			}
		}

		/// <summary>
		/// Checks if a plugin with the specified name is loaded.
		/// </summary>
		/// <param name="name">The plugin name to check.</param>
		/// <returns>True if the plugin is loaded, false otherwise.</returns>
		public bool IsPluginLoaded(string name)
		{
			return GetPlugin(name) != null;
		}

		/// <summary>
		/// Creates a control instance from a plugin-registered factory.
		/// </summary>
		/// <param name="name">The name of the control to create.</param>
		/// <returns>The created control instance, or null if not found.</returns>
		public IWindowControl? CreateControl(string name)
		{
			lock (_lock)
			{
				return _controlFactories.TryGetValue(name, out var factory) ? factory() : null;
			}
		}

		/// <summary>
		/// Creates a window instance from a plugin-registered factory.
		/// </summary>
		/// <param name="name">The name of the window to create.</param>
		/// <returns>The created window instance, or null if not found.</returns>
		public Window? CreateWindow(string name)
		{
			lock (_lock)
			{
				return _windowFactories.TryGetValue(name, out var factory) ? factory(_windowSystem) : null;
			}
		}

		/// <summary>
		/// Gets a service instance registered by a plugin (legacy type-based pattern).
		/// This method is obsolete. Use GetService(serviceName) instead.
		/// </summary>
		/// <typeparam name="T">The service type to retrieve.</typeparam>
		/// <returns>The service instance, or null if not found.</returns>
		[Obsolete("Use GetService(serviceName) instead. Type-based service lookup will be removed in a future version.")]
		public T? GetService<T>() where T : class
		{
			lock (_lock)
			{
				return _legacyServices.TryGetValue(typeof(T), out var service) ? service as T : null;
			}
		}

		/// <summary>
		/// Gets a service plugin by name using the reflection-free pattern.
		/// </summary>
		/// <param name="serviceName">The unique name of the service to retrieve.</param>
		/// <returns>The service plugin instance, or null if not found.</returns>
		public IPluginService? GetService(string serviceName)
		{
			lock (_lock)
			{
				return _services.TryGetValue(serviceName, out var service) ? service : null;
			}
		}

		/// <summary>
		/// Checks if a service plugin with the specified name is registered.
		/// </summary>
		/// <param name="serviceName">The service name to check.</param>
		/// <returns>True if the service is registered, false otherwise.</returns>
		public bool HasService(string serviceName)
		{
			lock (_lock)
			{
				return _services.ContainsKey(serviceName);
			}
		}

		/// <summary>
		/// Gets an action provider by name.
		/// </summary>
		/// <param name="providerName">The unique name of the action provider.</param>
		/// <returns>The action provider instance, or null if not found.</returns>
		public IPluginActionProvider? GetActionProvider(string providerName)
		{
			lock (_lock)
			{
				return _actionProviders.TryGetValue(providerName, out var provider) ? provider : null;
			}
		}

		/// <summary>
		/// Executes a plugin action using the agnostic pattern.
		/// </summary>
		/// <param name="providerName">The name of the action provider.</param>
		/// <param name="actionName">The name of the action to execute.</param>
		/// <param name="context">Optional execution context (will include WindowSystem automatically).</param>
		public void ExecutePluginAction(string providerName, string actionName, Dictionary<string, object>? context = null)
		{
			IPluginActionProvider? provider;
			lock (_lock)
			{
				if (!_actionProviders.TryGetValue(providerName, out provider))
				{
					throw new InvalidOperationException($"Action provider '{providerName}' not found");
				}
			}

			// Add ConsoleWindowSystem to context for plugin use
			context ??= new Dictionary<string, object>();
			if (!context.ContainsKey("WindowSystem"))
				context["WindowSystem"] = _windowSystem;

			provider.ExecuteAction(actionName, context);
		}

		/// <summary>
		/// Disposes the plugin state service and all loaded plugins.
		/// </summary>
		public void Dispose()
		{
			if (_disposed)
				return;

			lock (_lock)
			{
				foreach (var plugin in _loadedPlugins)
				{
					try
					{
						plugin.Dispose();
					}
					catch (Exception ex)
					{
						_logService?.LogError($"Error disposing plugin {plugin.Info.Name}: {ex.Message}", ex, "Plugins");
					}
				}

				_loadedPlugins.Clear();
				_controlFactories.Clear();
				_windowFactories.Clear();
				_legacyServices.Clear();
				_services.Clear();

				_disposed = true;
			}
		}
	}
}
