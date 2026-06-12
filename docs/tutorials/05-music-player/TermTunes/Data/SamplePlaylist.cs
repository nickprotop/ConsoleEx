using TermTunes.Models;

namespace TermTunes.Data;

/// <summary>In-memory playlist (no audio). Cover PNGs are generated into assets/covers/.</summary>
public static class SamplePlaylist
{
    public static List<Track> Build() => new()
    {
        new() { Title = "Midnight Drive", Artist = "The Synthwaves", Album = "Retrowave",
                Duration = TimeSpan.FromSeconds(228), CoverPath = "assets/covers/midnight.png", Accent = (255, 80, 160) },
        new() { Title = "Neon Rain", Artist = "Violet Static", Album = "City Lights",
                Duration = TimeSpan.FromSeconds(195), CoverPath = "assets/covers/neon.png", Accent = (80, 200, 255) },
        new() { Title = "Cassette Dreams", Artist = "Lo-Fi Cartel", Album = "Tape Hiss",
                Duration = TimeSpan.FromSeconds(174), CoverPath = "assets/covers/cassette.png", Accent = (255, 180, 60) },
        new() { Title = "Afterglow", Artist = "Aurora Skies", Album = "Northern",
                Duration = TimeSpan.FromSeconds(243), CoverPath = "assets/covers/afterglow.png", Accent = (130, 255, 150) },
        new() { Title = "Velvet Static", Artist = "The Synthwaves", Album = "Retrowave",
                Duration = TimeSpan.FromSeconds(210), CoverPath = "assets/covers/velvet.png", Accent = (190, 130, 255) },
    };
}
