using System.Text.Json.Nodes;

namespace SharpConsoleUI.Registry;

/// <summary>
/// In-memory IRegistryStorage implementation for testing. No file I/O.
/// Each Save() stores a deep clone so mutations to the original don't affect stored data.
/// </summary>
public class MemoryStorage : IRegistryStorage
{
    private JsonNode? _stored;

    /// <inheritdoc/>
    public void Save(JsonNode root)
    {
        _stored = root.DeepClone();
    }

    /// <inheritdoc/>
    public JsonNode? Load()
    {
        return _stored?.DeepClone();
    }
}
