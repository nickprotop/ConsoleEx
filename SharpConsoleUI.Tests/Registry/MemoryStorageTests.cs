using System.Text.Json.Nodes;
using SharpConsoleUI.Registry;
using Xunit;

namespace SharpConsoleUI.Tests.Registry;

public class MemoryStorageTests
{
    [Fact]
    public void Load_BeforeSave_ReturnsNull()
    {
        var storage = new MemoryStorage();
        Assert.Null(storage.Load());
    }

    [Fact]
    public void SaveThenLoad_ReturnsCloneOfSavedData()
    {
        var storage = new MemoryStorage();
        var root = new JsonObject { ["key"] = "value" };
        storage.Save(root);

        var loaded = storage.Load();
        Assert.NotNull(loaded);
        Assert.Equal("value", loaded!["key"]!.GetValue<string>());
    }

    [Fact]
    public void Save_StoresClone_MutatingOriginalDoesNotAffectLoad()
    {
        var storage = new MemoryStorage();
        var root = new JsonObject { ["key"] = "original" };
        storage.Save(root);

        // Mutate original — loaded copy should be unaffected
        root["key"] = "mutated";
        var loaded = storage.Load();
        Assert.Equal("original", loaded!["key"]!.GetValue<string>());
    }
}
