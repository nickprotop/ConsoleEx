// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Performance;

/// <summary>
/// What a window of rendered frames came to.
/// </summary>
/// <remarks>
/// A snapshot: taken under the ring's lock and immutable afterwards, so a
/// reader can take its time with it while the render loop carries on filling
/// the ring behind it.
/// </remarks>
public sealed class FrameWindowStats
{
	/// <summary>How many frames the window holds.</summary>
	public int Frames { get; init; }

	/// <summary>How many have been rendered in total since the last reset.</summary>
	public long TotalFrames { get; init; }

	/// <summary>How long the window covers.</summary>
	public double SpanMs { get; init; }

	/// <summary>The shortest interval between two rendered frames.</summary>
	public double MinMs { get; init; }

	/// <summary>The mean interval.</summary>
	public double MeanMs { get; init; }

	/// <summary>The interval 95 percent of frames came in under.</summary>
	/// <remarks>
	/// The number a stutter shows up in. A mean can sit at sixty frames a second
	/// while one frame in twenty takes 90ms, and only this says so.
	/// </remarks>
	public double P95Ms { get; init; }

	/// <summary>The longest interval: the worst hitch in the window.</summary>
	public double MaxMs { get; init; }

	/// <summary>Frames a second actually achieved over the window.</summary>
	public double Fps { get; init; }

	/// <summary>Total milliseconds spent in layout.</summary>
	public double LayoutMs { get; init; }

	/// <summary>Total milliseconds spent generating escape sequences.</summary>
	public double AnsiMs { get; init; }

	/// <summary>Total milliseconds spent writing to the terminal.</summary>
	public double OutputMs { get; init; }

	/// <summary>Total milliseconds spent comparing the front and back buffers.</summary>
	public double CompareMs { get; init; }

	/// <summary>Bytes written over the window.</summary>
	public long BytesWritten { get; init; }

	/// <summary>Bytes a second: the number that matters over a remote link.</summary>
	public double BytesPerSecond { get; init; }

	/// <summary>Cells actually painted.</summary>
	public long CellsRendered { get; init; }

	/// <summary>Cells something marked dirty: the status bar's "dirty chars", summed.</summary>
	/// <remarks>
	/// The same number the status bar shows, counted the same way - front buffer
	/// against back - but summed over the window rather than held for a moment so
	/// a person can read it. The tracker's copy smooths a zero into the previous
	/// non-zero value, which is right for a display and wrong for a measurement.
	/// </remarks>
	public long CellsMarked { get; init; }

	/// <summary>
	/// Painted over marked: how much of the work asked for was work needed.
	/// </summary>
	/// <remarks>
	/// Well under one means invalidation is over-firing - something is marking
	/// cells that did not change. Null when nothing was marked, rather than
	/// zero, because no work asked for is not inefficiency.
	/// </remarks>
	public double? Efficiency => CellsMarked > 0 ? (double)CellsRendered / CellsMarked : null;

	/// <summary>Whether any frame in the window carried the renderer's own metrics.</summary>
	/// <remarks>
	/// The interval is measured always; the per-stage costs and the byte counts are
	/// recorded only while diagnostics are on. This distinguishes "cost nothing"
	/// from "nobody was measuring", which the figures alone cannot.
	/// </remarks>
	public bool Measured { get; init; }

	/// <summary>Each interval in the window, oldest first.</summary>
	/// <remarks>For a caller that wants to draw the shape rather than read the summary.</remarks>
	public double[] Intervals { get; init; } = [];
}
