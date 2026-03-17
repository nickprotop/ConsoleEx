// SharpConsoleUI/Registry/RegistrySection.cs
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace SharpConsoleUI.Registry;

/// <summary>
/// A live view of a node in the registry tree. Provides typed get/set for primitive types,
/// AOT-safe generic types, and sub-section navigation.
/// RegistrySection is a lightweight wrapper — it holds no independent state beyond a
/// reference to its JsonObject node and the parent AppRegistry (for flush callbacks).
///
/// Thread-safety note: RegistrySection instances are NOT individually thread-safe.
/// Do not share a single RegistrySection instance across threads without external synchronization.
/// </summary>
public class RegistrySection
{
    private readonly JsonObject _node;
    private readonly AppRegistry _registry;

    internal RegistrySection(JsonObject node, AppRegistry registry)
    {
        _node = node;
        _registry = registry;
    }

    // ── Path resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Opens a sub-section by relative path. Creates intermediate nodes as needed.
    /// '/' is the path separator. Leading/trailing slashes are trimmed.
    /// Empty path or "/" returns this section. Double slashes throw ArgumentException.
    /// </summary>
    public RegistrySection OpenSection(string path)
    {
        var segments = SplitPath(path);
        var current = _node;
        foreach (var segment in segments)
        {
            if (current[segment] is JsonObject child)
            {
                current = child;
            }
            else
            {
                current.Remove(segment);
                var newNode = new JsonObject();
                current[segment] = newNode;
                current = newNode;
            }
        }
        return new RegistrySection(current, _registry);
    }

    private static string[] SplitPath(string path)
    {
        path = path.Trim('/');
        if (path.Length == 0) return Array.Empty<string>();

        var segments = path.Split('/');
        foreach (var seg in segments)
        {
            if (seg.Length == 0)
                throw new ArgumentException($"Registry path contains an empty segment: '{path}'", nameof(path));
        }
        return segments;
    }

    // ── Primitive types ───────────────────────────────────────────────────────

    /// <summary>Gets a string value, returning <paramref name="defaultValue"/> if the key is absent.</summary>
    public string GetString(string key, string defaultValue = "")
    {
        if (_node[key] is JsonValue v && v.TryGetValue<string>(out var s)) return s;
        return defaultValue;
    }

    /// <summary>Sets a string value.</summary>
    public void SetString(string key, string value)
    {
        _node[key] = JsonValue.Create(value);
        _registry?.OnValueSet();
    }

    /// <summary>Gets an int value, returning <paramref name="defaultValue"/> if the key is absent.</summary>
    public int GetInt(string key, int defaultValue = 0)
    {
        if (_node[key] is JsonValue v && v.TryGetValue<int>(out var i)) return i;
        return defaultValue;
    }

    /// <summary>Sets an int value.</summary>
    public void SetInt(string key, int value)
    {
        _node[key] = JsonValue.Create(value);
        _registry?.OnValueSet();
    }

    /// <summary>Gets a bool value, returning <paramref name="defaultValue"/> if the key is absent.</summary>
    public bool GetBool(string key, bool defaultValue = false)
    {
        if (_node[key] is JsonValue v && v.TryGetValue<bool>(out var b)) return b;
        return defaultValue;
    }

    /// <summary>Sets a bool value.</summary>
    public void SetBool(string key, bool value)
    {
        _node[key] = JsonValue.Create(value);
        _registry?.OnValueSet();
    }

    /// <summary>Gets a double value, returning <paramref name="defaultValue"/> if the key is absent.</summary>
    public double GetDouble(string key, double defaultValue = 0.0)
    {
        if (_node[key] is JsonValue v && v.TryGetValue<double>(out var d)) return d;
        return defaultValue;
    }

    /// <summary>Sets a double value.</summary>
    public void SetDouble(string key, double value)
    {
        _node[key] = JsonValue.Create(value);
        _registry?.OnValueSet();
    }

    /// <summary>Gets a DateTime value stored as ISO 8601, returning <paramref name="defaultValue"/> if the key is absent.</summary>
    public DateTime GetDateTime(string key, DateTime defaultValue = default)
    {
        if (_node[key] is JsonValue v && v.TryGetValue<string>(out var s)
            && DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt;
        return defaultValue;
    }

    /// <summary>Sets a DateTime value, stored as ISO 8601 round-trip format.</summary>
    public void SetDateTime(string key, DateTime value)
    {
        _node[key] = JsonValue.Create(value.ToString("O"));
        _registry?.OnValueSet();
    }

    // ── AOT-safe generic types ────────────────────────────────────────────────

    /// <summary>
    /// Gets a value of type T using a source-generated JsonTypeInfo (AOT-safe).
    /// Returns <paramref name="defaultValue"/> if the key is absent or deserialization fails.
    /// </summary>
    public T Get<T>(string key, T defaultValue, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            var node = _node[key];
            if (node is null) return defaultValue;
            var result = System.Text.Json.JsonSerializer.Deserialize(node, typeInfo);
            return result ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>Sets a value of type T using a source-generated JsonTypeInfo (AOT-safe).</summary>
    public void Set<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        _node[key] = System.Text.Json.JsonSerializer.SerializeToNode(value, typeInfo);
        _registry?.OnValueSet();
    }

    // ── Key management ────────────────────────────────────────────────────────

    /// <summary>Returns the direct (non-recursive) leaf value key names of this section node.</summary>
    public IReadOnlyList<string> GetKeys()
    {
        return _node
            .Where(kv => kv.Value is not JsonObject)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>Returns the direct child section names of this section node.</summary>
    public IReadOnlyList<string> GetSubSectionNames()
    {
        return _node
            .Where(kv => kv.Value is JsonObject)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>Returns true if the given key exists as a direct leaf value in this section.</summary>
    public bool HasKey(string key)
    {
        return _node.ContainsKey(key) && _node[key] is not JsonObject;
    }

    /// <summary>Removes a leaf value key. No-op if the key does not exist.</summary>
    public void DeleteKey(string key)
    {
        _node.Remove(key);
    }

    /// <summary>
    /// Removes a direct child section subtree at the given relative path.
    /// No-op if any part of the path does not exist.
    /// </summary>
    public void DeleteSection(string path)
    {
        var segments = SplitPath(path);
        if (segments.Length == 0) return;

        var current = _node;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (current[segments[i]] is JsonObject next)
                current = next;
            else
                return;
        }
        current.Remove(segments[^1]);
    }
}

