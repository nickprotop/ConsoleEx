using SharpConsoleUI;
using TermTunes.Models;

namespace TermTunes.ViewModels;

/// <summary>Bindable wrapper around a <see cref="Track"/>.</summary>
public sealed class TrackViewModel : ViewModelBase
{
    private readonly Track _model;
    public TrackViewModel(Track model) => _model = model;

    public string Title => _model.Title;
    public string Artist => _model.Artist;
    public string Album => _model.Album;
    public TimeSpan Duration => _model.Duration;
    public string CoverPath => _model.CoverPath;
    public Color Accent => new(_model.Accent.R, _model.Accent.G, _model.Accent.B);

    public string DurationText => FormatTime(_model.Duration);

    /// <summary>"m:ss" formatting shared by the seek-bar time labels.</summary>
    public static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
}
