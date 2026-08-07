// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System;
using System.Threading;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;

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

	private static readonly int[] _reservedWidth = new int[Enum.GetValues<SpinnerStyle>().Length];

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

	// --- Repaint driving ---
	//
	// ShouldKeepRendering only makes the main loop CALL UpdateDisplay each tick; it does not make any
	// window dirty, and RenderWindows skips a window whose PendingWork is None. So a window whose only
	// animated content is an inline [spinner] was repainted only when something else dirtied it — a key,
	// a click, or (as in DemoApp's spinner page) a real SpinnerControl beside it ticking its own
	// FrameCycleAnimation. On a window with no such neighbour the glyph simply froze.
	//
	// Controls that paint an inline spinner register here through a WEAK reference, so a control that is
	// removed or a window that is closed needs no deregistration and cannot be kept alive by this list.
	// Tick() then invalidates them, but only when the frame index actually moved on, so an idle repaint
	// of an unchanged glyph costs nothing.
	private static readonly List<WeakReference<IInlineSpinnerHost>> _hosts = new();
	private static readonly object _hostsLock = new();

	/// <summary>A control whose painted content contains inline <c>[spinner]</c> markup.</summary>
	internal interface IInlineSpinnerHost
	{
		/// <summary>Requests a repaint because the inline spinner advanced a frame.</summary>
		void InvalidateForInlineSpinner();
	}

	/// <summary>
	/// Registers a control as displaying an inline spinner. Idempotent: re-registering an
	/// already-registered control is a no-op, so calling this from every paint is safe.
	/// </summary>
	internal static void RegisterHost(IInlineSpinnerHost host)
	{
		lock (_hostsLock)
		{
			for (int i = _hosts.Count - 1; i >= 0; i--)
			{
				if (!_hosts[i].TryGetTarget(out var existing))
					_hosts.RemoveAt(i);
				else if (ReferenceEquals(existing, host))
					return;
			}
			_hosts.Add(new WeakReference<IInlineSpinnerHost>(host));
		}
	}

	// The cadence to tick at: the SHORTEST interval any inline [spinner] tag has been parsed with. An
	// interval is an explicit per-tag argument ([spinner dots 40]) or the style's default, so it cannot
	// be known ahead of time — the parser reports each one it sees and this keeps the minimum. Ticking
	// at the fastest cadence in use over-invalidates a slower spinner (a repaint whose glyph is
	// unchanged, which the parse cache makes cheap) but never under-invalidates a faster one.
	private static long _minIntervalMs = long.MaxValue;

	/// <summary>Reports the interval an inline spinner was parsed with, so Tick can match its cadence.</summary>
	internal static void ReportInterval(int intervalMs)
	{
		if (intervalMs <= 0) intervalMs = ControlDefaults.SpinnerDefaultIntervalMs;
		long current = Interlocked.Read(ref _minIntervalMs);
		while (intervalMs < current)
		{
			long prior = Interlocked.CompareExchange(ref _minIntervalMs, intervalMs, current);
			if (prior == current) break;
			current = prior;
		}
	}

	// The frame slot the registered hosts were last invalidated for; one slot per _minIntervalMs.
	private static long _lastTickSlot = long.MinValue;

	/// <summary>
	/// Advances inline spinners: when the frame slot has moved since the last call, invalidates every
	/// registered host so the render loop repaints their glyph. Called once per main-loop iteration.
	/// Does nothing when animations are disabled or no inline spinner is active.
	/// </summary>
	public static void Tick(bool animationsEnabled)
	{
		if (!ShouldKeepRendering(animationsEnabled))
			return;

		long interval = Interlocked.Read(ref _minIntervalMs);
		if (interval == long.MaxValue) interval = ControlDefaults.SpinnerDefaultIntervalMs;

		long slot = _now() / interval;
		if (slot == Interlocked.Read(ref _lastTickSlot))
			return;
		Interlocked.Exchange(ref _lastTickSlot, slot);

		lock (_hostsLock)
		{
			for (int i = _hosts.Count - 1; i >= 0; i--)
			{
				if (_hosts[i].TryGetTarget(out var host))
					host.InvalidateForInlineSpinner();
				else
					_hosts.RemoveAt(i);
			}
		}
	}

	// --- Test seams (do not use in production code) ---
	/// <summary>Test-only: overrides the time source.</summary>
	public static void SetTimeProviderForTests(Func<long> now) => _now = now;
	/// <summary>Test-only: restores the default monotonic time source.</summary>
	public static void ResetTimeProviderForTests() => _now = () => Environment.TickCount64;
	/// <summary>Test-only: public wrapper around <see cref="MarkParsed"/>.</summary>
	public static void MarkParsedForTests() => MarkParsed();
	/// <summary>Test-only: resets the keep-alive tick so IsActive returns false until the next MarkParsed,
	/// and clears the registered hosts and cadence state.</summary>
	public static void ResetForTests()
	{
		Interlocked.Exchange(ref _lastParsedTick, long.MinValue);
		Interlocked.Exchange(ref _lastTickSlot, long.MinValue);
		Interlocked.Exchange(ref _minIntervalMs, long.MaxValue);
		lock (_hostsLock) _hosts.Clear();
	}
}
