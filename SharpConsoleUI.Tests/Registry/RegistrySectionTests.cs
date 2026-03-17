// SharpConsoleUI.Tests/Registry/RegistrySectionTests.cs
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SharpConsoleUI.Registry;
using Xunit;

namespace SharpConsoleUI.Tests.Registry;

public class RegistrySectionPrimitiveTests
{
    private static RegistrySection MakeRoot()
    {
        var root = new JsonObject();
        return new RegistrySection(root, null!); // null AppRegistry — flush tested separately
    }

    [Fact]
    public void GetString_MissingKey_ReturnsDefault()
    {
        var s = MakeRoot();
        Assert.Equal("hello", s.GetString("missing", "hello"));
    }

    [Fact]
    public void SetGetString_RoundTrips()
    {
        var s = MakeRoot();
        s.SetString("name", "world");
        Assert.Equal("world", s.GetString("name"));
    }

    [Fact]
    public void SetGetInt_RoundTrips()
    {
        var s = MakeRoot();
        s.SetInt("x", 42);
        Assert.Equal(42, s.GetInt("x"));
    }

    [Fact]
    public void SetGetBool_RoundTrips()
    {
        var s = MakeRoot();
        s.SetBool("flag", true);
        Assert.True(s.GetBool("flag"));
    }

    [Fact]
    public void SetGetDouble_RoundTrips()
    {
        var s = MakeRoot();
        s.SetDouble("ratio", 3.14);
        Assert.Equal(3.14, s.GetDouble("ratio"), precision: 10);
    }

    [Fact]
    public void SetGetDateTime_RoundTrips()
    {
        var s = MakeRoot();
        var dt = new DateTime(2026, 3, 17, 12, 0, 0, DateTimeKind.Utc);
        s.SetDateTime("ts", dt);
        Assert.Equal(dt, s.GetDateTime("ts"));
    }

    [Fact]
    public void GetInt_MissingKey_ReturnsDefault()
    {
        var s = MakeRoot();
        Assert.Equal(99, s.GetInt("missing", 99));
    }
}

public class RegistrySectionPathTests
{
    private static RegistrySection MakeRoot()
    {
        var root = new JsonObject();
        return new RegistrySection(root, null!);
    }

    [Fact]
    public void OpenSection_CreatesIntermediateNodes()
    {
        var root = MakeRoot();
        var section = root.OpenSection("App/Windows/Main");
        section.SetInt("X", 10);
        Assert.Equal(10, root.OpenSection("App/Windows/Main").GetInt("X"));
    }

    [Fact]
    public void TwoHandles_SamePath_ShareState()
    {
        var root = MakeRoot();
        var a = root.OpenSection("App/Settings");
        var b = root.OpenSection("App/Settings");
        a.SetString("theme", "Dark");
        Assert.Equal("Dark", b.GetString("theme"));
    }

    [Fact]
    public void LeadingSlash_TrimmedToSamePath()
    {
        var root = MakeRoot();
        root.OpenSection("/App").SetInt("val", 7);
        Assert.Equal(7, root.OpenSection("App").GetInt("val"));
    }

    [Fact]
    public void TrailingSlash_TrimmedToSamePath()
    {
        var root = MakeRoot();
        root.OpenSection("App/").SetInt("val", 8);
        Assert.Equal(8, root.OpenSection("App").GetInt("val"));
    }

    [Fact]
    public void EmptyPath_ReturnsRootSection()
    {
        var root = MakeRoot();
        var r1 = root.OpenSection("");
        var r2 = root.OpenSection("/");
        r1.SetInt("topLevel", 1);
        Assert.Equal(1, r2.GetInt("topLevel"));
    }

    [Fact]
    public void DoubleSlash_ThrowsArgumentException()
    {
        var root = MakeRoot();
        Assert.Throws<ArgumentException>(() => root.OpenSection("App//Windows"));
    }
}

public class RegistrySectionKeyManagementTests
{
    private static RegistrySection MakeRoot()
        => new RegistrySection(new JsonObject(), null!);

    [Fact]
    public void GetKeys_ReturnsOnlyLeafValues()
    {
        var s = MakeRoot();
        s.SetInt("x", 1);
        s.SetString("name", "hi");
        s.OpenSection("child"); // sub-section — should not appear in GetKeys
        var keys = s.GetKeys();
        Assert.Contains("x", keys);
        Assert.Contains("name", keys);
        Assert.DoesNotContain("child", keys);
    }

    [Fact]
    public void GetSubSectionNames_ReturnsOnlyChildSections()
    {
        var s = MakeRoot();
        s.SetInt("val", 1);
        s.OpenSection("childA");
        s.OpenSection("childB");
        var subs = s.GetSubSectionNames();
        Assert.Contains("childA", subs);
        Assert.Contains("childB", subs);
        Assert.DoesNotContain("val", subs);
    }

    [Fact]
    public void HasKey_TrueForLeaf_FalseForMissing_FalseForSection()
    {
        var s = MakeRoot();
        s.SetInt("leaf", 5);
        s.OpenSection("sub");
        Assert.True(s.HasKey("leaf"));
        Assert.False(s.HasKey("missing"));
        Assert.False(s.HasKey("sub"));
    }

    [Fact]
    public void DeleteKey_RemovesKey_NoOpIfMissing()
    {
        var s = MakeRoot();
        s.SetInt("x", 1);
        s.DeleteKey("x");
        Assert.False(s.HasKey("x"));
        s.DeleteKey("nonexistent"); // should not throw
    }

    [Fact]
    public void DeleteSection_RemovesChildSection_NoOpIfMissing()
    {
        var s = MakeRoot();
        s.OpenSection("child").SetInt("v", 1);
        s.DeleteSection("child");
        Assert.Empty(s.GetSubSectionNames());
        s.DeleteSection("nonexistent"); // should not throw
    }

    [Fact]
    public void DeleteSection_NestedPath_NoOpIfIntermediateMissing()
    {
        var s = MakeRoot();
        s.DeleteSection("App/Windows/Main"); // does not exist — should not throw
    }
}

// ── Helpers for generic type tests ───────────────────────────────────────────
internal record RegistryTestPoint(int X, int Y);

[JsonSerializable(typeof(RegistryTestPoint))]
internal partial class RegistryTestJsonContext : JsonSerializerContext { }

// ── Test class ────────────────────────────────────────────────────────────────
public class RegistrySectionGenericTests
{
    private static RegistrySection MakeRoot()
        => new RegistrySection(new JsonObject(), null!);

    [Fact]
    public void SetGet_CustomType_RoundTrips()
    {
        var s = MakeRoot();
        var point = new RegistryTestPoint(10, 20);
        s.Set("pos", point, RegistryTestJsonContext.Default.RegistryTestPoint);
        var loaded = s.Get("pos", new RegistryTestPoint(0, 0), RegistryTestJsonContext.Default.RegistryTestPoint);
        Assert.Equal(10, loaded.X);
        Assert.Equal(20, loaded.Y);
    }

    [Fact]
    public void Get_MissingKey_ReturnsDefault()
    {
        var s = MakeRoot();
        var def = new RegistryTestPoint(99, 99);
        var result = s.Get("missing", def, RegistryTestJsonContext.Default.RegistryTestPoint);
        Assert.Equal(def, result);
    }
}
