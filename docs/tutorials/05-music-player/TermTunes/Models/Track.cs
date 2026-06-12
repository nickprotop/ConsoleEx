namespace TermTunes.Models;

/// <summary>A track in the playlist (plain data; no audio).</summary>
public sealed class Track
{
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public required string Album { get; init; }
    public required TimeSpan Duration { get; init; }
    /// <summary>Path to the cover image (PNG) relative to the working dir.</summary>
    public required string CoverPath { get; init; }
    /// <summary>Accent color used to theme the visualizer/seek bar for this track.</summary>
    public (byte R, byte G, byte B) Accent { get; init; }
}
