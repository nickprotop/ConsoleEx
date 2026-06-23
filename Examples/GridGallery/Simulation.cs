// -----------------------------------------------------------------------
// GridGallery — deterministic Charts-page animation.
//
// Drives the Charts tiles from a single reproducible walk (sines + a
// counter, NO System.Random). Runs on the WINDOW's async thread, so the
// control mutations happen on the UI thread and re-render via the reactive
// invalidation path. No manual invalidation, no EnqueueOnUIThread needed.
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace GridGallery;

internal static class Simulation
{
	/// <summary>Rolling window length applied to the sparkline / line graph.</summary>
	public const int MaxPoints = 60;

	private const int TickMs = 600;

	/// <summary>
	/// Animates the Charts page until the window's cancellation token trips.
	/// All control mutations run on the window thread (UI-thread safe).
	/// </summary>
	public static async Task Run(GalleryRefs refs, CancellationToken ct)
	{
		long t = 0;

		while (!ct.IsCancellationRequested)
		{
			await Task.Delay(TickMs, ct);
			t++;

			// Bar(s): smooth deterministic drift, clamped to [3, 98].
			for (int i = 0; i < refs.Bars.Length; i++)
			{
				if (refs.Bars[i] is { } bar)
					bar.Value = Math.Clamp(50 + Math.Sin((t + i * 11) * 0.4) * 35 + Math.Sin(t * 0.13) * 10, 3, 98);
			}

			// Line graph: a composite throughput signal.
			if (refs.LineGraph is not null)
				refs.LineGraph.AddDataPoint(Math.Clamp(48 + Math.Sin(t * 0.27) * 30 + Math.Sin(t * 0.06) * 14, 0, 100));

			// Sparkline: a faster oscillation for visual contrast.
			if (refs.Sparkline is not null)
				refs.Sparkline.AddDataPoint(Math.Clamp(50 + Math.Sin(t * 0.55) * 40, 0, 100));

			// Progress bar: a slow saw that wraps at 100.
			if (refs.Progress is not null)
				refs.Progress.Value = (t * 3) % 101;
		}
	}
}
