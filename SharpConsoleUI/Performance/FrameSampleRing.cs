// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Performance;

/// <summary>
/// The last few hundred frame intervals, and what each one cost.
/// </summary>
/// <remarks>
/// <para>
/// A rate needs a window. One frame interval says nothing: a mean of 16ms with
/// a worst case of 90ms is a stutter someone can see, and a single sample
/// reports whichever of the two it happened to land on.
/// </para>
/// <para>
/// Separate from the per-frame dictionaries in
/// <see cref="Diagnostics.RenderingDiagnostics"/>, which are keyed by frame and
/// retain one frame by default. Those hold whole buffer snapshots, so keeping
/// three hundred of them to get three hundred frame times would cost megabytes
/// to learn a number that fits in a double. This is a fixed array of small
/// structs: it allocates once and never again, so it can be always-on.
/// </para>
/// <para>
/// Written from the render loop and read from the diagnostics socket thread, so
/// every member locks. The write is a handful of field stores inside the lock,
/// which is cheaper than the frame it is describing.
/// </para>
/// </remarks>
public sealed class FrameSampleRing
{
	/// <summary>One rendered frame.</summary>
	/// <param name="IntervalMs">Since the previous rendered frame.</param>
	/// <param name="LayoutMs">Spent in layout.</param>
	/// <param name="AnsiMs">Spent generating escape sequences.</param>
	/// <param name="OutputMs">Spent writing to the terminal.</param>
	/// <param name="CompareMs">Spent comparing the buffers.</param>
	/// <param name="BytesWritten">Bytes the frame put on the wire.</param>
	/// <param name="CellsRendered">Cells it actually painted.</param>
	/// <param name="CellsMarked">Cells something had marked dirty.</param>
	public readonly record struct Sample(
		double IntervalMs,
		double LayoutMs,
		double AnsiMs,
		double OutputMs,
		double CompareMs,
		int BytesWritten,
		int CellsRendered,
		int CellsMarked);

	/// <summary>How many frames are kept.</summary>
	/// <remarks>
	/// Five seconds at sixty frames a second, which is long enough to show a
	/// stutter and short enough that what it reports is still current.
	/// </remarks>
	public const int Capacity = 300;

	private readonly Sample[] _samples = new Sample[Capacity];
	private readonly object _gate = new();
	private int _next;
	private int _count;
	private long _totalFrames;

	/// <summary>Files one rendered frame, overwriting the oldest once full.</summary>
	public void Add(in Sample sample)
	{
		lock (_gate)
		{
			_samples[_next] = sample;
			_next = (_next + 1) % Capacity;
			if (_count < Capacity) _count++;
			_totalFrames++;
		}
	}

	/// <summary>Forgets every sample, for a measurement that starts here.</summary>
	public void Reset()
	{
		lock (_gate)
		{
			_next = 0;
			_count = 0;
			_totalFrames = 0;
		}
	}

	/// <summary>How many frames have been rendered since the last reset.</summary>
	public long TotalFrames { get { lock (_gate) return _totalFrames; } }

	/// <summary>What the window holds, or null when nothing has been rendered yet.</summary>
	public FrameWindowStats? Stats()
	{
		lock (_gate)
		{
			if (_count == 0) return null;

			var intervals = new double[_count];

			double layout = 0, ansi = 0, output = 0, compare = 0, span = 0;
			long bytes = 0, rendered = 0, marked = 0;
			bool measured = false;

			for (int i = 0; i < _count; i++)
			{
				// Oldest first, so the slice is in time order and a caller can
				// hand it straight to a sparkline.
				var s = _samples[(_next - _count + i + Capacity) % Capacity];

				intervals[i] = s.IntervalMs;
				span += s.IntervalMs;
				layout += s.LayoutMs;
				ansi += s.AnsiMs;
				output += s.OutputMs;
				compare += s.CompareMs;
				bytes += s.BytesWritten;
				rendered += s.CellsRendered;
				marked += s.CellsMarked;

				// Any frame that reported a stage cost or a byte count means the
				// renderer's metrics were on for at least part of the window.
				if (s.LayoutMs > 0 || s.AnsiMs > 0 || s.OutputMs > 0 || s.CompareMs > 0
					|| s.BytesWritten > 0 || s.CellsMarked > 0)
				{
					measured = true;
				}
			}

			// Sorted for the percentile, on the copy rather than the ring.
			var sorted = (double[])intervals.Clone();
			Array.Sort(sorted);

			double seconds = span / 1000.0;

			return new FrameWindowStats
			{
				Frames = _count,
				TotalFrames = _totalFrames,
				SpanMs = span,
				MinMs = sorted[0],
				MeanMs = span / _count,
				P95Ms = Percentile(sorted, 0.95),
				MaxMs = sorted[^1],
				// The achieved rate over the window, which is the frame count over
				// the time it took - not the reciprocal of the mean, which weights
				// a long frame the same as a short one.
				Fps = seconds > 0 ? _count / seconds : 0,
				LayoutMs = layout,
				AnsiMs = ansi,
				OutputMs = output,
				CompareMs = compare,
				BytesWritten = bytes,
				BytesPerSecond = seconds > 0 ? bytes / seconds : 0,
				CellsRendered = rendered,
				CellsMarked = marked,
				Measured = measured,
				Intervals = intervals,
			};
		}
	}

	/// <summary>
	/// The value at a fraction through a sorted set, interpolating between neighbours.
	/// </summary>
	/// <remarks>
	/// Nearest-rank would be enough, but with a short window it jumps: at twenty
	/// samples the 95th percentile is simply the second largest, and it steps
	/// rather than moves as frames arrive.
	/// </remarks>
	private static double Percentile(double[] sorted, double fraction)
	{
		if (sorted.Length == 1) return sorted[0];

		double rank = fraction * (sorted.Length - 1);
		int low = (int)Math.Floor(rank);
		int high = (int)Math.Ceiling(rank);

		if (low == high) return sorted[low];

		return sorted[low] + (sorted[high] - sorted[low]) * (rank - low);
	}
}
