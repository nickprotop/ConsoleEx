using System.Text;
using System.Timers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Core;

/// <summary>
/// Core service that owns a cached CharacterBuffer and renders the desktop background into it.
/// The main render loop blits from this cache instead of flat-filling.
/// Renders three composable layers: base fill (from theme) -> gradient overlay -> pattern overlay.
/// For animated backgrounds, a PaintCallback takes full control and is driven by a timer.
/// </summary>
public class DesktopBackgroundService : IDisposable
{
    private CharacterBuffer? _buffer;
    private int _width;
    private int _height;
    private DesktopBackgroundConfig? _config;
    private System.Timers.Timer? _animationTimer;
    private DateTime _animationStart;
    private bool _disposed;
    private readonly Func<ITheme> _getTheme;
    private readonly Action _onDesktopDirty;

    /// <summary>
    /// Gets the cached desktop background buffer.
    /// </summary>
    public CharacterBuffer? Buffer => _buffer;

    /// <summary>
    /// Gets whether a cached buffer exists.
    /// </summary>
    public bool HasBuffer => _buffer != null;

    /// <summary>
    /// Gets or sets the desktop background configuration.
    /// Setting this invalidates the cache and manages the animation timer.
    /// </summary>
    public DesktopBackgroundConfig? Config
    {
        get => _config;
        set
        {
            _config = value;
            StopAnimationTimer();
            StartAnimationTimerIfNeeded();
            Invalidate();
        }
    }

    /// <summary>
    /// Creates a new DesktopBackgroundService.
    /// </summary>
    /// <param name="getTheme">Accessor to get the current theme.</param>
    /// <param name="onDesktopDirty">Callback to signal that the desktop needs a redraw.</param>
    public DesktopBackgroundService(Func<ITheme> getTheme, Action onDesktopDirty)
    {
        _getTheme = getTheme ?? throw new ArgumentNullException(nameof(getTheme));
        _onDesktopDirty = onDesktopDirty ?? throw new ArgumentNullException(nameof(onDesktopDirty));
    }

    /// <summary>
    /// Renders the desktop background into the cached buffer.
    /// Creates or resizes the buffer if needed, then applies layers:
    /// 1. Base fill (theme char + colors)
    /// 2. PaintCallback (if set, takes full control and returns)
    /// 3. Gradient overlay (if configured)
    /// 4. Pattern overlay (if configured)
    /// </summary>
    /// <param name="width">Screen width in columns.</param>
    /// <param name="height">Screen height in rows.</param>
    public void Render(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        _width = width;
        _height = height;

        // Create or resize buffer if needed
        if (_buffer == null || _buffer.Width != width || _buffer.Height != height)
        {
            _buffer = new CharacterBuffer(width, height);
        }

        var theme = _getTheme();

        // Layer 1: Base fill from theme
        var bgChar = theme.DesktopBackgroundChar;
        var bgColor = theme.DesktopBackgroundColor;
        var fgColor = theme.DesktopForegroundColor;
        _buffer.FillRect(new LayoutRect(0, 0, width, height), bgChar, fgColor, bgColor);

        // If PaintCallback is set, it takes full control
        if (_config?.PaintCallback != null)
        {
            var elapsed = DateTime.UtcNow - _animationStart;
            _config.PaintCallback(_buffer, width, height, elapsed);
            return;
        }

        // Layer 2: Gradient overlay
        // TODO: Also check theme.DesktopBackgroundGradient when available (Task 5)
        if (_config?.Gradient != null)
        {
            GradientRenderer.FillGradientBackground(
                _buffer,
                new LayoutRect(0, 0, width, height),
                _config.Gradient.Gradient,
                _config.Gradient.Direction);
        }

        // Layer 3: Pattern overlay
        if (_config?.Pattern != null)
        {
            RenderPattern(_config.Pattern);
        }
    }

    /// <summary>
    /// Copies a rectangular region from the cached buffer to a target buffer.
    /// Used by the renderer to restore desktop areas when windows move or close.
    /// </summary>
    /// <param name="target">The target buffer to copy into.</param>
    /// <param name="srcX">Source X position in the cached buffer.</param>
    /// <param name="srcY">Source Y position in the cached buffer.</param>
    /// <param name="width">Width of the region to copy.</param>
    /// <param name="height">Height of the region to copy.</param>
    public void BlitRegion(CharacterBuffer target, int srcX, int srcY, int width, int height)
    {
        if (_buffer == null || target == null)
            return;

        var sourceRect = new LayoutRect(srcX, srcY, width, height);
        target.CopyFrom(_buffer, sourceRect, srcX, srcY);
    }

    /// <summary>
    /// Re-renders the desktop if dimensions are known and signals dirty.
    /// </summary>
    public void Invalidate()
    {
        if (_width > 0 && _height > 0)
        {
            Render(_width, _height);
            _onDesktopDirty();
        }
    }

    /// <summary>
    /// Called when the theme changes. Invalidates and re-renders the desktop.
    /// </summary>
    public void OnThemeChanged()
    {
        Invalidate();
    }

    /// <summary>
    /// Tiles the pattern's character grid across the entire buffer.
    /// For each cell, computes tile coordinates and sets the character.
    /// Uses pattern's per-cell colors if set, otherwise keeps existing buffer colors.
    /// </summary>
    private void RenderPattern(DesktopPattern pattern)
    {
        if (_buffer == null)
            return;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int px = x % pattern.Width;
                int py = y % pattern.Height;

                char patternChar = pattern.Characters[py, px];
                var existing = _buffer.GetCell(x, y);

                var fg = existing.Foreground;
                var bg = existing.Background;

                if (pattern.ForegroundColors != null)
                {
                    var patternFg = pattern.ForegroundColors[py, px];
                    if (patternFg.HasValue)
                        fg = patternFg.Value;
                }

                if (pattern.BackgroundColors != null)
                {
                    var patternBg = pattern.BackgroundColors[py, px];
                    if (patternBg.HasValue)
                        bg = patternBg.Value;
                }

                _buffer.SetNarrowCell(x, y, patternChar, fg, bg);
            }
        }
    }

    /// <summary>
    /// Starts the animation timer if the config has a PaintCallback.
    /// </summary>
    private void StartAnimationTimerIfNeeded()
    {
        if (_config?.PaintCallback == null)
            return;

        _animationStart = DateTime.UtcNow;
        _animationTimer = new System.Timers.Timer(_config.AnimationIntervalMs);
        _animationTimer.Elapsed += OnAnimationTick;
        _animationTimer.AutoReset = true;
        _animationTimer.Start();
    }

    /// <summary>
    /// Stops and disposes the animation timer.
    /// </summary>
    private void StopAnimationTimer()
    {
        if (_animationTimer != null)
        {
            _animationTimer.Stop();
            _animationTimer.Elapsed -= OnAnimationTick;
            _animationTimer.Dispose();
            _animationTimer = null;
        }
    }

    /// <summary>
    /// Animation timer tick handler. Re-renders the desktop and signals dirty.
    /// </summary>
    private void OnAnimationTick(object? sender, ElapsedEventArgs e)
    {
        if (_disposed || _width <= 0 || _height <= 0)
            return;

        Render(_width, _height);
        _onDesktopDirty();
    }

    /// <summary>
    /// Disposes the service, stopping the animation timer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAnimationTimer();
    }
}
