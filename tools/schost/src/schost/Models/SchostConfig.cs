using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScHost.Models;

/// <summary>
/// Configuration model for schost.json.
/// </summary>
public sealed class SchostConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public const string FileName = "schost.json";

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("font")]
    public string? Font { get; set; }

    [JsonPropertyName("fontSize")]
    public int? FontSize { get; set; }

    [JsonPropertyName("columns")]
    public int? Columns { get; set; }

    [JsonPropertyName("rows")]
    public int? Rows { get; set; }

    [JsonPropertyName("colorScheme")]
    public string? ColorScheme { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("selfContained")]
    public bool? SelfContained { get; set; }

    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("installer")]
    public bool? Installer { get; set; }

    // Runtime-only properties (not serialized to JSON)
    [JsonIgnore]
    public string? CsprojPath { get; set; }

    [JsonIgnore]
    public string? AssemblyName { get; set; }

    [JsonIgnore]
    public string? Version { get; set; }

    public static SchostConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SchostConfig>(json, SerializerOptions)
               ?? new SchostConfig();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, SerializerOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Returns the effective title, falling back to AssemblyName.
    /// </summary>
    public string GetEffectiveTitle() => Title ?? AssemblyName ?? "App";
}
