using System.Text.Json.Nodes;

namespace SharpConsoleUI.Registry;

/// <summary>
/// Abstraction for registry persistence. The contract uses JsonNode because the
/// registry's in-memory model IS a JsonNode tree. Custom backends may serialize
/// it to any wire format internally.
/// </summary>
public interface IRegistryStorage
{
    /// <summary>Persists the registry tree.</summary>
    void Save(JsonNode root);

    /// <summary>Loads the registry tree. Returns null if no data exists yet.</summary>
    JsonNode? Load();
}
