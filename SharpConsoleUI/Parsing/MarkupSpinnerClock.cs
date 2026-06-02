// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using System;
using System.Threading;

namespace SharpConsoleUI.Parsing;

/// <summary>
/// Static, monotonic-time-driven frame source for inline <c>[spinner]</c> markup tags.
/// The current frame is computed purely from elapsed time, so reading is allocation-free
/// and requires no per-frame registration. Reserved width per style is constant, so inline
/// spinners never cause text reflow. <see cref="IsActive"/> keeps the render loop repainting
/// while inline spinners are on screen.
/// </summary>
public static class MarkupSpinnerClock
{
	private static long _lastParsedTick = long.MinValue;
	private static volatile Func<long> _now = () => Environment.TickCount64;

	private static readonly int[] _reservedWidth = new int[Enum.GetValues(typeof(SpinnerStyle)).Length];

	/// <summary>Constant reserved column width for a style = max display width across its frames.</summary>
	public static int ReservedWidth(SpinnerStyle style)
	{
		int idx = (int)style;
		int cached = _reservedWidth[idx];
		if (cached != 0) return cached;

		int max = 0;
		foreach (var f in SpinnerControl.FramesForStyle(style))
			max = Math.Max(max, MarkupParser.StripLength(f));
		if (max < 1) max = 1;
		_reservedWidth[idx] = max;
		return max;
	}

	/// <summary>
	/// Reserved column width for a style honoring an explicit minimum <paramref name="requestedWidth"/>
	/// (e.g. from a <c>[spinner … width:N]</c> tag). The request is a <em>minimum</em>: a value narrower
	/// than the style's natural width is clamped up so the glyph never clips. A non-positive request
	/// means "use the natural width".
	/// </summary>
	public static int ReservedWidth(SpinnerStyle style, int requestedWidth)
	{
		int natural = ReservedWidth(style);
		return requestedWidth > 0 ? Math.Max(requestedWidth, natural) : natural;
	}

	/// <summary>Current 0-based frame index for a style at the given interval, derived from elapsed monotonic time.</summary>
	public static int CurrentFrame(SpinnerStyle style, int intervalMs)
	{
		int frameCount = SpinnerControl.FramesForStyle(style).Length;
		if (frameCount <= 1) return 0;
		long interval = intervalMs > 0 ? intervalMs : ControlDefaults.SpinnerDefaultIntervalMs;
		long frame = (_now() / interval) % frameCount;
		if (frame < 0) frame += frameCount;
		return (int)frame;
	}

	/// <summary>Current 0-based frame index using the style's per-style default interval.</summary>
	public static int CurrentFrame(SpinnerStyle style)
		=> CurrentFrame(style, SpinnerControl.DefaultIntervalMs(style));

	/// <summary>Current frame glyph for a style at the given interval, right-padded to the style's natural reserved width.</summary>
	public static string CurrentGlyph(SpinnerStyle style, int intervalMs)
		=> CurrentGlyph(style, intervalMs, 0);

	/// <summary>
	/// Current frame glyph for a style at the given interval, right-padded to its reserved width.
	/// <paramref name="requestedWidth"/> sets an explicit minimum field width (clamped up to the
	/// natural width so the glyph never clips); a non-positive value uses the natural width.
	/// </summary>
	public static string CurrentGlyph(SpinnerStyle style, int intervalMs, int requestedWidth)
	{
		var frames = SpinnerControl.FramesForStyle(style);
		string g = frames[CurrentFrame(style, intervalMs)]; // index already bounded by CurrentFrame
		int reserved = ReservedWidth(style, requestedWidth);
		int width = MarkupParser.StripLength(g);
		if (width >= reserved) return g;
		var sb = new System.Text.StringBuilder(g, reserved);
		sb.Append(' ', reserved - width);
		return sb.ToString();
	}

	/// <summary>Current frame glyph using the style's per-style default interval.</summary>
	public static string CurrentGlyph(SpinnerStyle style)
		=> CurrentGlyph(style, SpinnerControl.DefaultIntervalMs(style));

	/// <summary>True if an inline spinner was parsed within the keep-alive window.</summary>
	public static bool IsActive
	{
		get
		{
			long last = Interlocked.Read(ref _lastParsedTick);
			return last != long.MinValue &&
				   (_now() - last) <= ControlDefaults.InlineSpinnerKeepAliveMs;
		}
	}

	/// <summary>
	/// Whether the render loop should keep repainting for inline spinners.
	/// True only when animations are enabled AND an inline spinner is active.
	/// </summary>
	public static bool ShouldKeepRendering(bool animationsEnabled) => animationsEnabled && IsActive;

	/// <summary>Marks that an inline spinner was just parsed; refreshes the keep-alive window.</summary>
	internal static void MarkParsed() => Interlocked.Exchange(ref _lastParsedTick, _now());

	// --- Test seams (do not use in production code) ---
	/// <summary>Test-only: overrides the time source.</summary>
	public static void SetTimeProviderForTests(Func<long> now) => _now = now;
	/// <summary>Test-only: restores the default monotonic time source.</summary>
	public static void ResetTimeProviderForTests() => _now = () => Environment.TickCount64;
	/// <summary>Test-only: public wrapper around <see cref="MarkParsed"/>.</summary>
	public static void MarkParsedForTests() => MarkParsed();
	/// <summary>Test-only: resets the keep-alive tick so IsActive returns false until the next MarkParsed.</summary>
	public static void ResetForTests() => Interlocked.Exchange(ref _lastParsedTick, long.MinValue);
}
