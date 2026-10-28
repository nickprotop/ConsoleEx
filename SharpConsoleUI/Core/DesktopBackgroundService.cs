// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

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
	/// Set when the cached buffer has been re-rendered and exposed desktop regions
	/// need to be blitted to the screen. Cleared by the render coordinator after blitting.
	/// This is set on: config change, theme change, and animation tick.
	/// </summary>
	public volatile bool NeedsScreenUpdate;

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
	/// <param name="animations">
	/// Advances the background. Supplied rather than reached for through a system,
	/// because this service deliberately holds no system reference.
	/// </param>
	public DesktopBackgroundService(
		Func<ITheme> getTheme,
		Action onDesktopDirty,
		Animation.AnimationManager? animations = null)
	{
		_getTheme = getTheme ?? throw new ArgumentNullException(nameof(getTheme));
		_onDesktopDirty = onDesktopDirty ?? throw new ArgumentNullException(nameof(onDesktopDirty));
		_animations = animations;
	}

	private readonly Animation.AnimationManager? _animations;
	private DesktopAnimation? _animation;

	/// <summary>How far the background animation has advanced.</summary>
	/// <remarks>
	/// Animation time, not wall-clock: it accumulates the CAPPED deltas
	/// <see cref="Animation.AnimationManager"/> hands out, so a busy loop cannot
	/// run it fast and a stalled one cannot make it jump. Dragging a window
	/// renders at full rate, and a background that redrew per frame visibly sped
	/// up while the mouse moved.
	/// </remarks>
	public TimeSpan LastAnimationElapsed => _animation?.Elapsed ?? TimeSpan.Zero;

	/// <summary>The desktop background, as something the animator can advance.</summary>
	/// <remarks>
	/// Never completes: a background runs for as long as it is configured, and is
	/// removed by cancelling it rather than by finishing.
	/// </remarks>
	private sealed class DesktopAnimation : Animation.IAnimation
	{
		private readonly Action _onTick;

		internal DesktopAnimation(Action onTick) => _onTick = onTick;

		internal TimeSpan Elapsed { get; private set; }

		public bool IsComplete { get; private set; }

		public void Update(TimeSpan deltaTime)
		{
			Elapsed += deltaTime;
			_onTick();
		}

		public void Cancel() => IsComplete = true;
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

		// Layer 1: Base fill — config color overrides theme
		var bgChar = theme.DesktopBackgroundChar;
		var bgColor = _config?.BackgroundColor ?? theme.DesktopBackgroundColor ?? theme.WindowBackgroundColor;
		var fgColor = theme.DesktopForegroundColor ?? theme.WindowForegroundColor;
		// Use Clear for transparent bg — FillRect uses Color.Blend which is a no-op for A=0
		if (bgColor.A == 0)
			_buffer.Clear(bgColor);
		else
			_buffer.FillRect(new LayoutRect(0, 0, width, height), bgChar, fgColor, bgColor);

		// If PaintCallback is set, it takes full control
		if (_config?.PaintCallback != null)
		{
			var elapsed = _animation?.Elapsed ?? TimeSpan.Zero;
			_config.PaintCallback(_buffer, width, height, elapsed);
			return;
		}

		// Layer 2: Gradient overlay (config overrides theme default)
		var gradient = _config?.Gradient ?? theme.DesktopBackgroundGradient;
		if (gradient != null)
		{
			GradientRenderer.FillGradientBackground(
				_buffer,
				new LayoutRect(0, 0, width, height),
				gradient.Gradient,
				gradient.Direction);
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
			NeedsScreenUpdate = true;
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

		if (_animations == null) return;

		_animation = new DesktopAnimation(MarkDesktopDirty);
		_animations.Add(_animation);
	}

	/// <summary>
	/// Stops and disposes the animation timer.
	/// </summary>
	private void StopAnimationTimer()
	{
		if (_animation != null)
		{
			_animations?.Cancel(_animation);
			_animation = null;
		}
	}

	/// <summary>
	/// Animation timer tick handler. Signals the render thread to re-render
	/// on the next frame. Does NOT render here — rendering on a thread pool
	/// thread races with the render thread reading the buffer, causing tearing.
	/// The render coordinator calls Render() synchronously before blitting.
	/// </summary>
	private void MarkDesktopDirty()
	{
		if (_disposed || _width <= 0 || _height <= 0)
			return;

		NeedsScreenUpdate = true;
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
