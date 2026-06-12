using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using TermTunes.ViewModels;

namespace TermTunes.UI;

/// <summary>Builds and owns the full-screen player window.</summary>
public sealed class PlayerWindow
{
    private readonly ConsoleWindowSystem _ws;
    private readonly PlayerViewModel _player;

    private Window _window = null!;
    private ListControl _playlist = null!;
    private MarkupControl _trackTitle = null!;
    private MarkupControl _trackMeta = null!;
    private ImageControl _albumArt = null!;
    private SeekBar _seekBar = null!;
    private MarkupControl _timeLabel = null!;
    private Visualizer _visualizer = null!;
    private ButtonControl _playBtn = null!;
    private HorizontalGridControl _transport = null!;

    // Frame loop state
    private readonly double[] _levels = new double[28];
    private readonly Random _rng = new(7);
    private long _lastTick;
    private int _marqueeOffset;
    private int _frame;

    public PlayerWindow(ConsoleWindowSystem ws, PlayerViewModel player)
    {
        _ws = ws;
        _player = player;
    }

    public Window Create()
    {
        BuildControls();
        var grid = BuildGrid();

        _window = new WindowBuilder(_ws)
            .WithTitle("TermTunes")
            .Maximized()
            .HideTitleButtons()
            .Movable(false).Resizable(false).Minimizable(false).Maximizable(false)
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBackgroundGradient(ColorScheme.WindowGradient, GradientDirection.Vertical)
            .AddControl(grid)
            .WithAsyncWindowThread(FrameLoopAsync)
            .BuildAndShow();

        // Global keys: space toggles play/pause, 'q' quits.
        _window.KeyPressed += (s, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Spacebar)
            {
                _player.TogglePlay();
                e.Handled = true;
            }
            else if (char.ToLowerInvariant(e.KeyInfo.KeyChar) == 'q')
            {
                _ws.Shutdown(0);
                e.Handled = true;
            }
        };

        return _window;
    }

    private void BuildControls()
    {
        var listBuilder = Controls.List()
            .WithTitle("")
            .WithColors(ColorScheme.Primary, ColorScheme.SidebarBg)
            .WithHighlightForegroundColor(Color.White)
            .WithHighlightBackgroundColor(Color.Grey35);
        foreach (var t in _player.Playlist)
            listBuilder.AddItem($"{t.Title}");
        _playlist = listBuilder.Build();

        _trackTitle = Controls.Markup("[grey93 bold]—[/]").Build();
        _trackMeta = Controls.Markup("[grey50]Select a track[/]").Build();

        _albumArt = Controls.Image()
            .WithScaleMode(ImageScaleMode.Fit)
            .Build();
        _albumArt.Height = 12; // constrain to ~12 rows so other controls stay visible

        _seekBar = new SeekBar();
        _timeLabel = Controls.Markup("[grey50]0:00 / 0:00[/]").Build();
        _visualizer = new Visualizer();

        // Transport buttons
        _playBtn = Controls.Button("⏯")
            .OnClick((_, _) => _player.TogglePlay())
            .Build();
        var prevBtn = Controls.Button("⏮")
            .OnClick((_, _) => { _player.Prev(); LoadCover(_player.CurrentTrack?.CoverPath ?? ""); })
            .Build();
        var nextBtn = Controls.Button("⏭")
            .OnClick((_, _) => { _player.Next(); LoadCover(_player.CurrentTrack?.CoverPath ?? ""); })
            .Build();
        _transport = Controls.HorizontalGrid()
            .Column(c => c.Add(prevBtn))
            .Column(c => c.Add(_playBtn))
            .Column(c => c.Add(nextBtn))
            .Build();

        // Track title/meta follow the current track.
        _trackTitle.Bind(_player, p => p.CurrentTrack, c => c.Text,
            t => t is null ? "[grey50]—[/]" : $"[grey93 bold]{t.Title}[/]");
        _trackMeta.Bind(_player, p => p.CurrentTrack, c => c.Text,
            t => t is null ? "" : $"[grey70]{t.Artist}[/]  [grey50]·  {t.Album} · {t.DurationText}[/]");

        // Selecting a track plays it (and loads its cover).
        _playlist.SelectedIndexChanged += (_, index) =>
        {
            if (index >= 0 && index < _player.Playlist.Count)
            {
                _player.PlayAt(index);
                LoadCover(_player.CurrentTrack!.CoverPath);
            }
        };

        // Initial cover.
        if (_player.CurrentTrack is not null) LoadCover(_player.CurrentTrack.CoverPath);

        // Auto-play the first track on launch.
        if (_player.Playlist.Count > 0) { _player.PlayAt(0); }
    }

    private void LoadCover(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                _albumArt.Source = PixelBuffer.FromFile(path);
                _albumArt.InvalidateImageCache();
            }
        }
        catch { /* fallback: leave previous art */ }
    }

    private HorizontalGridControl BuildGrid()
    {
        return Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Column(col =>
            {
                col.Width(26);
                col.Add(Header("Playlist"));
                col.Add(_playlist);
            })
            .Column(col =>
            {
                col.Flex(3.0);
                col.Add(Header("Now Playing"));
                col.Add(_albumArt);
                col.Add(_trackTitle);
                col.Add(_trackMeta);
                col.Add(_visualizer.Control);
                col.Add(_seekBar.Control);
                col.Add(_timeLabel);
                col.Add(_transport);
            })
            .Build();
    }

    private static MarkupControl Header(string text)
    {
        var h = Controls.Markup($"[bold grey85]{text}[/]").Build();
        h.BackgroundColor = ColorScheme.PanelHeaderBg;
        return h;
    }

    // -----------------------------------------------------------------------
    // Frame loop (~14 fps)
    // -----------------------------------------------------------------------

    private async Task FrameLoopAsync(Window window, CancellationToken ct)
    {
        _lastTick = Environment.TickCount64;
        while (!ct.IsCancellationRequested)
        {
            long now = Environment.TickCount64;
            var delta = TimeSpan.FromMilliseconds(now - _lastTick);
            _lastTick = now;
            _frame++;

            if (_player.IsPlaying)
            {
                var before = _player.CurrentTrack;
                _player.Tick(delta);
                if (!ReferenceEquals(before, _player.CurrentTrack))
                    LoadCover(_player.CurrentTrack!.CoverPath); // auto-advanced to next track
            }

            UpdateLevels();
            var accent = _player.CurrentTrack?.Accent ?? Color.Grey50;
            _visualizer.Render(_levels, accent);
            _seekBar.Render(_player.Position, _player.Duration, accent);
            UpdateTimeLabel();
            UpdateMarquee();
            UpdatePulse();

            await Task.Delay(70, ct); // ~14 fps
        }
    }

    private void UpdateLevels()
    {
        double energy = _player.IsPlaying ? 1.0 : 0.0;
        double t = _frame * 0.12;
        for (int i = 0; i < _levels.Length; i++)
        {
            // Simulated spectrum: layered sines + noise, smoothed toward target
            double target = energy * (0.45 + 0.45 * Math.Abs(Math.Sin(t * (0.6 + i * 0.07) + i)))
                            * (0.7 + 0.3 * _rng.NextDouble());
            _levels[i] += (target - _levels[i]) * 0.35; // smoothing / decay
        }
    }

    private void UpdateTimeLabel()
    {
        var pos = TrackViewModel.FormatTime(_player.Position);
        var dur = _player.CurrentTrack is null ? "0:00" : _player.CurrentTrack.DurationText;
        _timeLabel.Text = $"[grey50]{pos} / {dur}[/]";
    }

    private void UpdateMarquee()
    {
        var track = _player.CurrentTrack;
        if (track is null) return;
        const int width = 28;
        string title = track.Title;
        if (title.Length <= width)
        {
            _trackTitle.Text = $"[grey93 bold]{title}[/]";
            return;
        }
        string padded = title + "    ";
        if (_frame % 3 == 0) _marqueeOffset = (_marqueeOffset + 1) % padded.Length;
        string rotated = padded.Substring(_marqueeOffset) + padded.Substring(0, _marqueeOffset);
        _trackTitle.Text = $"[grey93 bold]{rotated.Substring(0, width)}[/]";
    }

    private void UpdatePulse()
    {
        if (_player.IsPlaying)
        {
            double pulse = 0.5 + 0.5 * Math.Sin(_frame * 0.3);
            var accent = _player.CurrentTrack?.Accent ?? Color.Grey93;
            _playBtn.ForegroundColor = new Color(
                (byte)Math.Clamp((int)(120 + (accent.R - 120) * pulse), 0, 255),
                (byte)Math.Clamp((int)(120 + (accent.G - 120) * pulse), 0, 255),
                (byte)Math.Clamp((int)(120 + (accent.B - 120) * pulse), 0, 255));
            _playBtn.Text = "⏸";
        }
        else
        {
            _playBtn.ForegroundColor = ColorScheme.Muted;
            _playBtn.Text = "⏯";
        }
    }
}
