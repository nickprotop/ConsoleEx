using TermTunes.Data;
using Xunit;

namespace TermTunes.Tests;

public class SamplePlaylistTests
{
    [Fact]
    public void Build_ReturnsAtLeastFourTracks_WithCoversAndDurations()
    {
        var tracks = SamplePlaylist.Build();
        Assert.True(tracks.Count >= 4);
        Assert.All(tracks, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Title));
            Assert.EndsWith(".png", t.CoverPath);
            Assert.True(t.Duration > TimeSpan.Zero);
        });
    }
}
