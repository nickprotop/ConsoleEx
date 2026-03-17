// SharpConsoleUI/Core/RegistryStateService.cs
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Registry;

namespace SharpConsoleUI.Core;

/// <summary>
/// Integrates AppRegistry with the ConsoleWindowSystem lifecycle.
/// Calls Load() during initialization and Save() on Dispose() (shutdown).
/// Exposes all AppRegistry members via explicit delegation so callers
/// don't need to unwrap an inner object.
/// </summary>
public class RegistryStateService : IDisposable
{
    private readonly AppRegistry _registry;

    /// <param name="registry">The AppRegistry to delegate to.</param>
    public RegistryStateService(AppRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Creates and loads a RegistryStateService from the given configuration.
    /// Called by ConsoleWindowSystem during its init cycle.
    /// </summary>
    internal static RegistryStateService Create(RegistryConfiguration config)
    {
        var registry = new AppRegistry(config);
        var service = new RegistryStateService(registry);
        service.Load();
        return service;
    }

    // ── Delegated AppRegistry API ────────────────────────────────────────────

    /// <summary>Opens a section at the given path. Creates intermediate nodes as needed.</summary>
    public RegistrySection OpenSection(string path) => _registry.OpenSection(path);

    /// <summary>Saves the registry to storage.</summary>
    public void Save() => _registry.Save();

    /// <summary>
    /// Reloads from storage, replacing the in-memory tree.
    /// Destructive — discards any unsaved writes.
    /// </summary>
    public void Load() => _registry.Load();

    /// <summary>Saves to storage and disposes the underlying AppRegistry.</summary>
    public void Dispose()
    {
        try { _registry.Save(); }
        finally { _registry.Dispose(); }
    }
}
