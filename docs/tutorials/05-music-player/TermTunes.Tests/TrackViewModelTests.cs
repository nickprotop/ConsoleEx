using TermTunes.Models;
using TermTunes.ViewModels;
using Xunit;

namespace TermTunes.Tests;

public class TrackViewModelTests
{
    private static Track Sample() => new()
    {
        Title = "Midnight Drive", Artist = "The Synthwaves", Album = "Retrowave",
        Duration = TimeSpan.FromSeconds(228), CoverPath = "assets/covers/a.png",
        Accent = (255, 80, 160),
    };

    [Fact]
    public void ExposesTrackFields()
    {
        var vm = new TrackViewModel(Sample());
        Assert.Equal("Midnight Drive", vm.Title);
        Assert.Equal("The Synthwaves", vm.Artist);
        Assert.Equal("Retrowave", vm.Album);
    }

    [Fact]
    public void DurationText_IsMinutesSeconds()
    {
        var vm = new TrackViewModel(Sample());
        Assert.Equal("3:48", vm.DurationText); // 228s = 3:48
    }
}
