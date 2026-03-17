using System.Text.Json.Nodes;
using SharpConsoleUI.Registry;
using Xunit;

namespace SharpConsoleUI.Tests.Registry;

public class JsonFileStorageTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"registry_test_{Guid.NewGuid()}.json");

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var storage = new JsonFileStorage(_tempFile);
        Assert.Null(storage.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var storage = new JsonFileStorage(_tempFile);
        var root = new JsonObject { ["key"] = "value" };
        storage.Save(root);

        var loaded = storage.Load();
        Assert.NotNull(loaded);
        Assert.Equal("value", loaded!["key"]!.GetValue<string>());
    }

    [Fact]
    public void Save_WritesValidJsonFile()
    {
        var storage = new JsonFileStorage(_tempFile);
        storage.Save(new JsonObject { ["x"] = 42 });

        var json = File.ReadAllText(_tempFile);
        Assert.Contains("\"x\"", json);
        Assert.Contains("42", json);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }
}
