// SharpConsoleUI.Tests/Registry/RegistryIntegrationTests.cs
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Registry;

public class RegistryIntegrationTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(
        Path.GetTempPath(), $"registry_integration_{Guid.NewGuid()}.json");

    [Fact]
    public void WindowSystem_RegistryStateService_Null_WhenNoConfigProvided()
    {
        var ws = TestWindowSystemBuilder.CreateTestSystem();
        Assert.Null(ws.RegistryStateService);
    }

    [Fact]
    public void WindowSystem_RegistryStateService_NotNull_WhenConfigProvided()
    {
        var mockDriver = new MockConsoleDriver();
        var config = RegistryConfiguration.ForFile(_tempFile);
        var ws = new ConsoleWindowSystem(mockDriver, registryConfiguration: config);
        Assert.NotNull(ws.RegistryStateService);
    }

    [Fact]
    public void WriteDispose_ThenRestore_ValuesAreRecovered()
    {
        var config = RegistryConfiguration.ForFile(_tempFile);

        // Session 1: write and dispose (triggers save)
        {
            var mockDriver = new MockConsoleDriver();
            var ws = new ConsoleWindowSystem(mockDriver, registryConfiguration: config);
            ws.RegistryStateService!.OpenSection("App/Windows/Main").SetInt("X", 42);
            ws.RegistryStateService.Dispose();
        }

        // Session 2: create new instance and restore
        {
            var mockDriver = new MockConsoleDriver();
            var ws = new ConsoleWindowSystem(mockDriver, registryConfiguration: config);
            var x = ws.RegistryStateService!.OpenSection("App/Windows/Main").GetInt("X");
            Assert.Equal(42, x);
            ws.RegistryStateService.Dispose();
        }
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }
}
