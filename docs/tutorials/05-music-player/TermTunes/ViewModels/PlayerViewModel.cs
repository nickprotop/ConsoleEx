using TermTunes.Models;

namespace TermTunes.ViewModels;

/// <summary>Holds playback state for a fake/simulated player. Tick() is pure and testable.</summary>
public sealed class PlayerViewModel : ViewModelBase
{
    public IReadOnlyList<TrackViewModel> Playlist { get; }

    public PlayerViewModel(IReadOnlyList<Track> tracks)
    {
        Playlist = tracks.Select(t => new TrackViewModel(t)).ToList();
        if (Playlist.Count > 0) { _currentIndex = 0; _currentTrack = Playlist[0]; }
    }

    private int _currentIndex = -1;
    public int CurrentIndex
    {
        get => _currentIndex;
        private set => SetProperty(ref _currentIndex, value);
    }

    private TrackViewModel? _currentTrack;
    public TrackViewModel? CurrentTrack
    {
        get => _currentTrack;
        private set => SetProperty(ref _currentTrack, value);
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    private TimeSpan _position;
    public TimeSpan Position
    {
        get => _position;
        private set => SetProperty(ref _position, value);
    }

    public TimeSpan Duration => CurrentTrack?.Duration ?? TimeSpan.Zero;

    /// <summary>Play the track at <paramref name="index"/> from the start.</summary>
    public void PlayAt(int index)
    {
        if (Playlist.Count == 0) return;
        CurrentIndex = ((index % Playlist.Count) + Playlist.Count) % Playlist.Count;
        CurrentTrack = Playlist[CurrentIndex];
        Position = TimeSpan.Zero;
        IsPlaying = true;
    }

    public void Next() => PlayAt(CurrentIndex + 1);
    public void Prev() => PlayAt(CurrentIndex - 1);

    public void TogglePlay() => IsPlaying = !IsPlaying;

    /// <summary>Advance the simulated clock; auto-advances at end of track.</summary>
    public void Tick(TimeSpan delta)
    {
        if (!IsPlaying || CurrentTrack is null) return;
        var next = Position + delta;
        if (next >= CurrentTrack.Duration)
            Next(); // PlayAt resets Position to zero and keeps IsPlaying true
        else
            Position = next;
    }
}
