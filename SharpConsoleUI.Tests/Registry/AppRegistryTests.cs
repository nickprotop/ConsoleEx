// SharpConsoleUI.Tests/Registry/AppRegistryTests.cs
using System.Threading;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Registry;
using Xunit;

namespace SharpConsoleUI.Tests.Registry;

public class AppRegistryManualFlushTests
{
    private static (AppRegistry registry, MemoryStorage storage) MakeRegistry(bool eagerFlush = false)
    {
        var storage = new MemoryStorage();
        var config = new RegistryConfiguration(EagerFlush: eagerFlush, Storage: storage);
        var registry = new AppRegistry(config);
        return (registry, storage);
    }

    [Fact]
    public void Save_PersistsToStorage()
    {
        var (reg, storage) = MakeRegistry();
        reg.OpenSection("App").SetString("name", "test");
        reg.Save();
        Assert.NotNull(storage.Load());
    }

    [Fact]
    public void LoadAfterSave_RestoresValues()
    {
        var (reg, storage) = MakeRegistry();
        reg.OpenSection("App").SetInt("X", 42);
        reg.Save();

        var reg2 = new AppRegistry(new RegistryConfiguration(Storage: storage));
        reg2.Load();
        Assert.Equal(42, reg2.OpenSection("App").GetInt("X"));
    }

    [Fact]
    public void Load_EmptyStorage_StartsEmpty()
    {
        var (reg, _) = MakeRegistry();
        reg.Load(); // storage has nothing yet — should not throw
        Assert.Equal(0, reg.OpenSection("App").GetInt("X"));
    }

    [Fact]
    public void Load_IsDestructive_DiscardsPendingWrites()
    {
        var (reg, storage) = MakeRegistry();
        reg.OpenSection("App").SetInt("X", 1);
        reg.Save();

        reg.OpenSection("App").SetInt("X", 999); // unsaved write
        reg.Load(); // reload from storage — should discard 999
        Assert.Equal(1, reg.OpenSection("App").GetInt("X"));
    }
}

public class AppRegistryEagerFlushTests
{
    [Fact]
    public void EagerFlush_EachSet_TriggersImmediateSave()
    {
        var saveCount = 0;
        var storage = new CountingStorage(() => saveCount++);
        var config = new RegistryConfiguration(EagerFlush: true, Storage: storage);
        var reg = new AppRegistry(config);

        reg.OpenSection("A").SetInt("x", 1);
        Assert.Equal(1, saveCount);

        reg.OpenSection("A").SetInt("y", 2);
        Assert.Equal(2, saveCount);
    }

    private class CountingStorage(Action onSave) : IRegistryStorage
    {
        public void Save(System.Text.Json.Nodes.JsonNode root) => onSave();
        public System.Text.Json.Nodes.JsonNode? Load() => null;
    }
}

public class AppRegistryLazyFlushTests
{
    [Fact]
    public async Task LazyFlush_TimerFires_PersistsData()
    {
        var storage = new MemoryStorage();
        var config = new RegistryConfiguration(
            FlushInterval: TimeSpan.FromMilliseconds(50),
            Storage: storage);

        using var reg = new AppRegistry(config);
        reg.OpenSection("App").SetInt("X", 77);

        // Poll until the lazy flush timer persists data, with a generous timeout for CI
        var timeout = TimeSpan.FromSeconds(2);
        var start = DateTime.UtcNow;
        System.Text.Json.Nodes.JsonNode? loaded = null;
        while (DateTime.UtcNow - start < timeout)
        {
            loaded = storage.Load();
            if (loaded != null) break;
            await Task.Delay(50);
        }

        Assert.NotNull(loaded);
    }

    [Fact]
    public void Dispose_StopsTimer_NoFurtherSaves()
    {
        var saveCount = 0;
        var storage = new CountingStorage(() => saveCount++);
        var config = new RegistryConfiguration(
            FlushInterval: TimeSpan.FromMilliseconds(50),
            Storage: storage);

        var reg = new AppRegistry(config);
        reg.Dispose();
        var countAfterDispose = saveCount;

        // After dispose, waiting should not cause more saves
        Thread.Sleep(500);
        Assert.Equal(countAfterDispose, saveCount);
    }

    private class CountingStorage(Action onSave) : IRegistryStorage
    {
        public void Save(System.Text.Json.Nodes.JsonNode root) => onSave();
        public System.Text.Json.Nodes.JsonNode? Load() => null;
    }
}

public class RegistryStateServiceTests
{
    [Fact]
    public void Dispose_CallsSave()
    {
        var storage = new MemoryStorage();
        var config = new RegistryConfiguration(Storage: storage);
        var registry = new AppRegistry(config);
        var service = new RegistryStateService(registry);

        service.OpenSection("App").SetInt("X", 55);
        service.Dispose(); // should call Save()

        var loaded = storage.Load();
        Assert.NotNull(loaded);
    }

    [Fact]
    public void OpenSection_DelegatesTo_AppRegistry()
    {
        var storage = new MemoryStorage();
        var registry = new AppRegistry(new RegistryConfiguration(Storage: storage));
        var service = new RegistryStateService(registry);

        service.OpenSection("A").SetString("k", "v");
        Assert.Equal("v", registry.OpenSection("A").GetString("k"));
    }
}
