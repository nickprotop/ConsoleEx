using SharpConsoleUI.Registry;

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration for the registry persistence system.
/// </summary>
/// <param name="FilePath">Path to the JSON registry file. Relative paths are resolved from the working directory.</param>
/// <param name="EagerFlush">If true, every Set call immediately writes to disk. Disabled by default.</param>
/// <param name="FlushInterval">If set, a background timer flushes to disk on this interval. Null disables timer-based flushing.</param>
/// <param name="Storage">Custom storage backend. If null, a JsonFileStorage backed by FilePath is used.</param>
public record RegistryConfiguration(
    string FilePath = "registry.json",
    bool EagerFlush = false,
    TimeSpan? FlushInterval = null,
    IRegistryStorage? Storage = null
)
{
    /// <summary>Default configuration using registry.json in the working directory.</summary>
    public static RegistryConfiguration Default => new();

    /// <summary>Creates a configuration that persists to the specified file path.</summary>
    public static RegistryConfiguration ForFile(string filePath) => new(FilePath: filePath);
}
