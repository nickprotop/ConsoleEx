// SharpConsoleUI/Registry/AppRegistry.cs
using System.Text.Json.Nodes;
using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Registry;

/// <summary>
/// The main registry API. Holds an in-memory JsonNode tree and delegates persistence
/// to an IRegistryStorage backend. Supports multiple flush modes: eager (every Set),
/// manual (explicit Save()), lazy (timer-based), and auto-on-shutdown (via RegistryStateService).
///
/// Thread-safety: AppRegistry.OpenSection() and Save()/Load() are thread-safe via
/// ReaderWriterLockSlim. Individual RegistrySection instances are NOT thread-safe —
/// do not share a section instance across threads without external synchronization.
/// </summary>
public class AppRegistry : IDisposable
{
    private readonly IRegistryStorage _storage;
    private readonly RegistryConfiguration _config;
    private JsonObject _root = new();
    private readonly ReaderWriterLockSlim _treeLock = new(LockRecursionPolicy.NoRecursion);
    private readonly object _saveLock = new();
    private int _saving; // Interlocked skip-flag: 0 = idle, 1 = saving
    private Timer? _flushTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new AppRegistry with the given configuration.
    /// Storage must be provided; JsonFileStorage support will be added in a future task.
    /// </summary>
    public AppRegistry(RegistryConfiguration config)
    {
        _config = config;
        _storage = config.Storage ?? new JsonFileStorage(config.FilePath);

        if (config.FlushInterval.HasValue)
        {
            var interval = config.FlushInterval.Value;
            _flushTimer = new Timer(_ => TimerFlush(), null, interval, interval);
        }
    }

    /// <summary>
    /// Opens a section at the given path. Creates intermediate nodes as needed.
    /// '/' is the separator. Leading/trailing slashes are trimmed. Empty path returns the root section.
    /// Uses a write lock because path creation mutates the tree.
    /// </summary>
    public RegistrySection OpenSection(string path)
    {
        _treeLock.EnterWriteLock();
        try
        {
            return new RegistrySection(_root, this).OpenSection(path);
        }
        finally
        {
            _treeLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Saves the registry to the storage backend.
    /// Snapshots the in-memory tree under a read lock, then releases the lock before I/O.
    /// Concurrent Save() calls are serialized via an internal lock.
    /// </summary>
    public void Save()
    {
        // Snapshot tree under read lock
        JsonNode snapshot;
        _treeLock.EnterReadLock();
        try
        {
            snapshot = _root.DeepClone();
        }
        finally
        {
            _treeLock.ExitReadLock();
        }

        // Write snapshot to storage — serialized, no tree lock held
        lock (_saveLock)
        {
            _storage.Save(snapshot);
        }
    }

    /// <summary>
    /// Loads from the storage backend. Replaces the in-memory tree entirely.
    /// Destructive: discards any unsaved writes. Call Save() first if needed.
    /// </summary>
    public void Load()
    {
        var loaded = _storage.Load();
        _treeLock.EnterWriteLock();
        try
        {
            _root = loaded as JsonObject ?? new JsonObject();
        }
        finally
        {
            _treeLock.ExitWriteLock();
        }
    }

    /// <summary>Called internally by RegistrySection.Set* methods to trigger eager flush.</summary>
    internal void OnValueSet()
    {
        if (_config.EagerFlush)
            Save();
    }

    private void TimerFlush()
    {
        // Skip if already saving (Interlocked compare-and-swap prevents re-entrancy)
        if (Interlocked.CompareExchange(ref _saving, 1, 0) != 0)
            return;
        try
        {
            Save();
        }
        finally
        {
            Interlocked.Exchange(ref _saving, 0);
        }
    }

    /// <summary>Stops the flush timer if active and releases the lock.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _flushTimer?.Dispose();
        _flushTimer = null;
        _treeLock.Dispose();
    }
}
