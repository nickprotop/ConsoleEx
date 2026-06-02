// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI.Animation;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using System.Drawing;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Controls;

/// <summary>
/// An animated spinner control for indeterminate-progress feedback. Cycles a set of
/// glyph frames on a fixed interval via the window system's animation manager.
/// Frames may contain Spectre-compatible markup (e.g. "[yellow]◐[/]").
/// </summary>
public class SpinnerControl : BaseControl
{
	private SpinnerStyle _style = SpinnerStyle.Braille;
	private string[]? _customFrames;
	private int? _intervalMs;
	private Color? _color;
	private bool _isSpinning = true;
	private int _currentFrameIndex;
	private FrameCycleAnimation? _animation;

	/// <summary>Gets or sets the preset frame style. Default <see cref="SpinnerStyle.Braille"/>.</summary>
	public SpinnerStyle Style
	{
		get => _style;
		set { if (_style == value) return; _style = value; RestartAnimation(); Container?.Invalidate(true); }
	}

	/// <summary>Gets or sets custom frames (overrides <see cref="Style"/> when non-empty). May contain markup.</summary>
	public IReadOnlyList<string>? Frames
	{
		get => _customFrames;
		set
		{
			var arr = value?.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray();
			_customFrames = (arr is { Length: > 0 }) ? arr : null;
			RestartAnimation();
			Container?.Invalidate(true);
		}
	}

	/// <summary>Gets the frame set actually in use (custom frames if set, else the style preset).</summary>
	public IReadOnlyList<string> EffectiveFrames => _customFrames ?? FramesForStyle(_style);

	/// <summary>Gets or sets the per-frame interval in milliseconds. When not explicitly set,
	/// the getter resolves to the per-style default (see <see cref="DefaultIntervalMs"/>).</summary>
	public int IntervalMs
	{
		get => _intervalMs ?? DefaultIntervalMs(_style);
		set { var v = Math.Max(ControlDefaults.AnimationMinIntervalMs, value); if (_intervalMs == v) return; _intervalMs = v; RestartAnimation(); }
	}

	/// <summary>Gets or sets the foreground color for plain (un-marked-up) frames. Theme-resolved when null.</summary>
	public Color? Color
	{
		get => _color;
		set { _color = value; Container?.Invalidate(true); }
	}

	/// <summary>Gets or sets whether the spinner is animating.</summary>
	public bool IsSpinning
	{
		get => _isSpinning;
		set
		{
			if (_isSpinning == value) return;
			_isSpinning = value;
			if (_isSpinning) StartAnimation(); else StopAnimation();
		}
	}

	/// <summary>Gets the current frame index.</summary>
	public int CurrentFrameIndex => _currentFrameIndex;

	/// <summary>
	/// Gets or sets an explicit minimum width (in columns) for the spinner. The control always
	/// reserves at least the widest frame's width, so a smaller value is clamped up and never
	/// clips the glyph; a larger value pads the reserved field (e.g. to align the spinner with
	/// neighbouring controls). Null (the default) reserves exactly the widest frame's width.
	/// </summary>
	public override int? Width
	{
		get => base.Width;
		set => base.Width = value;
	}

	/// <inheritdoc/>
	public override int? ContentWidth => EffectiveContentWidth();

	/// <summary>Initializes a new spinner.</summary>
	public SpinnerControl()
	{
		HorizontalAlignment = Layout.HorizontalAlignment.Left;
	}

	/// <summary>Starts animating.</summary>
	public void Start() => IsSpinning = true;

	/// <summary>Stops animating, freezing the current frame.</summary>
	public void Stop() => IsSpinning = false;

	/// <inheritdoc/>
	public override IContainer? Container
	{
		get => base.Container;
		set
		{
			if (ReferenceEquals(base.Container, value)) { base.Container = value; return; }
			StopAnimation();
			base.Container = value;
			if (value != null && _isSpinning) StartAnimation();
		}
	}

	/// <summary>Resolves the preset frame set for a style.</summary>
	internal static string[] FramesForStyle(SpinnerStyle style) => style switch
	{
		SpinnerStyle.Circle => ControlDefaults.SpinnerCircleFrames,
		SpinnerStyle.Dots => ControlDefaults.SpinnerDotsFrames,
		SpinnerStyle.Line => ControlDefaults.SpinnerLineFrames,
		SpinnerStyle.Arc => ControlDefaults.SpinnerArcFrames,
		SpinnerStyle.Bounce => ControlDefaults.SpinnerBounceFrames,
		SpinnerStyle.Star => ControlDefaults.SpinnerStarFrames,
		SpinnerStyle.GrowVertical => ControlDefaults.SpinnerGrowVerticalFrames,
		SpinnerStyle.GrowHorizontal => ControlDefaults.SpinnerGrowHorizontalFrames,
		SpinnerStyle.Toggle => ControlDefaults.SpinnerToggleFrames,
		SpinnerStyle.Arrow => ControlDefaults.SpinnerArrowFrames,
		SpinnerStyle.BouncingBar => ControlDefaults.SpinnerBouncingBarFrames,
		SpinnerStyle.AestheticBar => ControlDefaults.SpinnerAestheticBarFrames,
		SpinnerStyle.BrailleDots => ControlDefaults.SpinnerBrailleDotsFrames,
		SpinnerStyle.DotsBounce => ControlDefaults.SpinnerDotsBounceFrames,
		_ => ControlDefaults.SpinnerBrailleFrames,
	};

	/// <summary>Resolves the per-style default animation interval (ms). Used when no explicit interval is set.</summary>
	internal static int DefaultIntervalMs(SpinnerStyle style) => style switch
	{
		SpinnerStyle.Dots => ControlDefaults.SpinnerDotsIntervalMs,
		SpinnerStyle.DotsBounce => ControlDefaults.SpinnerDotsBounceIntervalMs,
		SpinnerStyle.BrailleDots => ControlDefaults.SpinnerBrailleDotsIntervalMs,
		SpinnerStyle.Star => ControlDefaults.SpinnerStarIntervalMs,
		SpinnerStyle.GrowVertical => ControlDefaults.SpinnerMediumIntervalMs,
		SpinnerStyle.GrowHorizontal => ControlDefaults.SpinnerMediumIntervalMs,
		SpinnerStyle.Toggle => ControlDefaults.SpinnerToggleIntervalMs,
		SpinnerStyle.Arrow => ControlDefaults.SpinnerMediumIntervalMs,
		SpinnerStyle.Line => ControlDefaults.SpinnerMediumIntervalMs,
		SpinnerStyle.Circle => ControlDefaults.SpinnerMediumIntervalMs,
		SpinnerStyle.BouncingBar => ControlDefaults.SpinnerBarIntervalMs,
		SpinnerStyle.AestheticBar => ControlDefaults.SpinnerBarIntervalMs,
		SpinnerStyle.Arc => ControlDefaults.SpinnerArcIntervalMs,
		_ => ControlDefaults.SpinnerDefaultIntervalMs,
	};

	/// <summary>
	/// Resolves the animation manager robustly by walking the full container chain up to the
	/// parent window. In nested containers the spinner's direct <see cref="Container"/> may not
	/// have its <c>GetConsoleWindowSystem</c> wired yet, so resolving one level is unreliable.
	/// </summary>
	private AnimationManager? GetAnimationManager()
		=> (this as IWindowControl).GetParentWindow()?.GetConsoleWindowSystem?.Animations;

	private void StartAnimation()
	{
		var manager = GetAnimationManager();
		if (manager == null || !manager.IsEnabled) return;
		StopAnimation();
		_animation = new FrameCycleAnimation(
			EffectiveFrames.Count,
			TimeSpan.FromMilliseconds(IntervalMs),
			i => { _currentFrameIndex = i; Container?.Invalidate(true); });
		manager.Add(_animation);
	}

	private void StopAnimation()
	{
		if (_animation == null) return;
		var manager = GetAnimationManager();
		if (manager != null)
			manager.Cancel(_animation);  // removes from manager (also cancels under lock)
		else
			_animation.Cancel();         // manager unreachable; mark cancelled directly
		_animation = null;
	}

	private void RestartAnimation()
	{
		if (_currentFrameIndex >= EffectiveFrames.Count) _currentFrameIndex = 0;
		if (_animation == null) return;
		StartAnimation();
	}

	/// <summary>Computes the widest frame's display width in columns.</summary>
	private int MaxFrameWidth()
	{
		int width = 0;
		foreach (var f in EffectiveFrames)
			width = Math.Max(width, MarkupParser.StripLength(f));
		return width;
	}

	/// <summary>
	/// The reserved content width: the widest frame's width, raised to <see cref="Width"/> when an
	/// explicit (larger) minimum is set. Never smaller than the glyph, so the spinner never clips.
	/// </summary>
	private int EffectiveContentWidth()
	{
		int natural = MaxFrameWidth();
		int requested = Width ?? 0;
		return requested > natural ? requested : natural;
	}

	/// <inheritdoc/>
	public override Size GetLogicalContentSize()
	{
		int width = EffectiveContentWidth();
		int height = 1 + Margin.Top + Margin.Bottom;
		return new Size(width + Margin.Left + Margin.Right, height);
	}

	/// <inheritdoc/>
	public override LayoutSize MeasureDOM(LayoutConstraints constraints)
	{
		int width = EffectiveContentWidth() + Margin.Left + Margin.Right;
		int height = 1 + Margin.Top + Margin.Bottom;
		return new LayoutSize(
			Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
			Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
		);
	}

	/// <inheritdoc/>
	public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
	{
		SetActualBounds(bounds);

		// Lazily ensure the animation is registered: in nested containers the parent
		// chain (and thus the window system) may not be wired when Container is first set.
		if (_isSpinning && _animation == null)
			StartAnimation();

		var frames = EffectiveFrames;
		if (frames.Count == 0) return;
		int idx = _currentFrameIndex % frames.Count;
		string frame = frames[idx];

		Color fg = _color ?? defaultFg;
		Color bg = Container?.BackgroundColor ?? defaultBg;

		int x = bounds.X + Margin.Left;
		int y = bounds.Y + Margin.Top;
		if (y < clipRect.Y || y >= clipRect.Bottom || y >= bounds.Bottom) return;

		var cells = MarkupParser.Parse(frame, fg, bg);
		foreach (var cell in cells)
		{
			if (x >= bounds.Right || x >= clipRect.Right) break;
			if (x >= clipRect.X)
				buffer.SetCell(x, y, cell);
			x++;
		}
	}
}
