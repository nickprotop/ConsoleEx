using SharpConsoleUI.Controls;
using SharpConsoleUI.Builders;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class TableShowHeaderTests
{
    [Fact]
    public void ShowHeader_DefaultsToTrue()
    {
        var table = SharpConsoleUI.Builders.Controls.Table().AddColumn("A").Build();
        Assert.True(table.ShowHeader);
    }

    [Fact]
    public void ShowHeader_CanBeSetToFalse()
    {
        var table = SharpConsoleUI.Builders.Controls.Table().AddColumn("A").Build();
        table.ShowHeader = false;
        Assert.False(table.ShowHeader);
    }

    [Fact]
    public void ShowHeader_SettingToSameValue_DoesNotThrow()
    {
        var table = SharpConsoleUI.Builders.Controls.Table().AddColumn("A").Build();
        table.ShowHeader = true; // same as default
        Assert.True(table.ShowHeader);
    }
}
