using TermTunes.Models;
using TermTunes.ViewModels;
using Xunit;

namespace TermTunes.Tests;

public class PlayerViewModelTests
{
    private static List<Track> Three() => new()
    {
        new() { Title = "A", Artist = "x", Album = "z", Duration = TimeSpan.FromSeconds(10), CoverPath = "a.png", Accent = (1,2,3) },
        new() { Title = "B", Artist = "x", Album = "z", Duration = TimeSpan.FromSeconds(10), CoverPath = "b.png", Accent = (1,2,3) },
        new() { Title = "C", Artist = "x", Album = "z", Duration = TimeSpan.FromSeconds(10), CoverPath = "c.png", Accent = (1,2,3) },
    };

    [Fact]
    public void Construction_SelectsFirstTrack_NotPlaying()
    {
        var vm = new PlayerViewModel(Three());
        Assert.Equal("A", vm.CurrentTrack!.Title);
        Assert.False(vm.IsPlaying);
        Assert.Equal(TimeSpan.Zero, vm.Position);
    }

    [Fact]
    public void PlayAt_SetsCurrent_StartsPlaying_ResetsPosition_RaisesChange()
    {
        var vm = new PlayerViewModel(Three());
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.PlayAt(2);

        Assert.Equal("C", vm.CurrentTrack!.Title);
        Assert.True(vm.IsPlaying);
        Assert.Equal(TimeSpan.Zero, vm.Position);
        Assert.Contains(nameof(PlayerViewModel.CurrentTrack), raised);
    }

    [Fact]
    public void Next_And_Prev_WrapAround()
    {
        var vm = new PlayerViewModel(Three());
        vm.PlayAt(2);
        vm.Next();
        Assert.Equal("A", vm.CurrentTrack!.Title); // wrapped 2 -> 0
        vm.Prev();
        Assert.Equal("C", vm.CurrentTrack!.Title); // wrapped 0 -> 2
    }

    [Fact]
    public void TogglePlay_FlipsIsPlaying()
    {
        var vm = new PlayerViewModel(Three());
        Assert.False(vm.IsPlaying);
        vm.TogglePlay();
        Assert.True(vm.IsPlaying);
        vm.TogglePlay();
        Assert.False(vm.IsPlaying);
    }

    [Fact]
    public void Tick_AdvancesPosition_WhenPlaying()
    {
        var vm = new PlayerViewModel(Three());
        vm.PlayAt(0);
        vm.Tick(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(3), vm.Position);
    }

    [Fact]
    public void Tick_AtEndOfTrack_AutoAdvancesToNext_AndResetsPosition()
    {
        var vm = new PlayerViewModel(Three());
        vm.PlayAt(0); // 10s track
        vm.Tick(TimeSpan.FromSeconds(11));
        Assert.Equal("B", vm.CurrentTrack!.Title);
        Assert.True(vm.Position < TimeSpan.FromSeconds(2)); // reset (carry optional)
    }

    [Fact]
    public void Tick_DoesNothing_WhenPaused()
    {
        var vm = new PlayerViewModel(Three());
        // not playing
        vm.Tick(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.Zero, vm.Position);
    }
}
