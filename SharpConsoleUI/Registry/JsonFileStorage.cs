using System.Text.Json;
using System.Text.Json.Nodes;

namespace SharpConsoleUI.Registry;

/// <summary>
/// Default IRegistryStorage implementation. Persists the registry as a pretty-printed
/// UTF-8 JSON file. Writes directly to the target path (no atomic rename — acceptable
/// for user preferences and window state where data loss on crash is tolerable).
/// Load() returns null if the file does not exist.
/// </summary>
public class JsonFileStorage : IRegistryStorage
{
    private readonly string _filePath;

    /// <summary>Initializes a new JsonFileStorage targeting the given file path.</summary>
    public JsonFileStorage(string filePath)
    {
        _filePath = filePath;
    }

    /// <inheritdoc/>
    public void Save(JsonNode root)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var stream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        root.WriteTo(writer);
    }

    /// <inheritdoc/>
    public JsonNode? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        var json = File.ReadAllText(_filePath);
        return JsonNode.Parse(json);
    }
}
