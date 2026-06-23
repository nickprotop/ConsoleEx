// -----------------------------------------------------------------------
// GridDashboard — deterministic data simulation.
//
// Drives every live control from a single reproducible walk (sines + a
// counter, NO System.Random). Runs on the WINDOW's async thread, so the
// control mutations happen on the UI thread and re-render via the reactive
// invalidation path — exactly the GridDemoWindow idiom. No manual
// invalidation, no EnqueueOnUIThread needed from here.
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace GridDashboard;

internal static class Simulation
{
	/// <summary>Rolling window length applied to every sparkline / line graph.</summary>
	public const int MaxPoints = 60;

	private const int TickMs = 600;
	private const int MaxLogLines = 200;

	private static readonly (string dot, string text)[] LogFeed =
	{
		("[green]●[/]", "web-{0:00} healthy"),
		("[yellow]●[/]", "api latency {0}ms"),
		("[green]●[/]", "db replica synced"),
		("[red]●[/]", "cache miss {0}%"),
		("[green]●[/]", "queue drained"),
		("[yellow]●[/]", "mem pressure {0}%"),
		("[green]●[/]", "snapshot stored"),
		("[green]●[/]", "autoscale +1 node"),
		("[yellow]●[/]", "gc pause {0}ms"),
		("[green]●[/]", "tls cert renewed"),
	};

	/// <summary>
	/// Runs the dashboard walk until the window's cancellation token trips.
	/// All control mutations run on the window thread (UI-thread safe).
	/// </summary>
	public static async Task Run(DashboardRefs refs, CancellationToken ct)
	{
		long t = 0;
		int logSeq = 1;

		// Per-metric drift state (clamped). Indices: 0=CPU 1=Mem 2=Net 3=Disk.
		double[] metrics = { 41, 67, 52, 38 };

		while (!ct.IsCancellationRequested)
		{
			await Task.Delay(TickMs, ct);
			t++;

			// ── Overview tiles + wide graph ────────────────────────────────────
			for (int i = 0; i < refs.TileNumbers.Length && i < metrics.Length; i++)
			{
				metrics[i] = Wobble(metrics[i], t, phase: i * 17, lo: 3, hi: 98);
				int pct = (int)Math.Round(metrics[i]);

				if (refs.TileNumbers[i] is { } number)
					number.SetContent(new List<string> { FormatTile(i, pct) });
				if (i < refs.TileSparks.Length)
					refs.TileSparks[i].AddDataPoint(metrics[i]);
			}

			if (refs.OverviewGraph is not null)
			{
				double load = Math.Clamp((metrics[0] * 0.5) + (metrics[2] * 0.3) + (metrics[3] * 0.2), 0, 100);
				refs.OverviewGraph.AddDataPoint(load);
			}

			// ── Processes table: churn a couple of CPU% cells ──────────────────
			if (refs.ProcessTable is { } table)
			{
				int rowCount = table.Rows.Count;
				if (rowCount > 0)
				{
					int rA = (int)(t % rowCount);
					int rB = (int)((t * 3 + 1) % rowCount);
					table.UpdateCell(rA, 2, ((int)Math.Clamp(20 + Math.Sin((t + rA) * 0.4) * 18, 0, 99)).ToString());
					table.UpdateCell(rB, 2, ((int)Math.Clamp(15 + Math.Sin((t + rB) * 0.27) * 14, 0, 99)).ToString());
				}
			}

			// ── Network in / out graphs ────────────────────────────────────────
			if (refs.NetIn is not null)
				refs.NetIn.AddDataPoint(Math.Clamp(45 + Math.Sin(t * 0.31) * 30 + Math.Sin(t * 0.07) * 12, 0, 100));
			if (refs.NetOut is not null)
				refs.NetOut.AddDataPoint(Math.Clamp(38 + Math.Sin(t * 0.23 + 1.5) * 28 + Math.Sin(t * 0.05) * 10, 0, 100));

			// ── Logs: append a colored line every other tick, cap the count ────
			if (refs.LogPanel is { } log && t % 2 == 0)
			{
				int fi = (int)((t / 2) % LogFeed.Length);
				var (dot, fmt) = LogFeed[fi];
				int arg = (int)(10 + (t * 7) % 90);
				string line = string.Format(fmt, arg);
				log.AddControl(Controls.Markup($"[dim]{logSeq++,3}[/] {dot} {line}")
					.WithMargin(1, 0, 1, 0).Build());

				// Cap: drop oldest body line (keep the header at index 0).
				if (log.Children.Count > MaxLogLines + 1)
					log.RemoveControl(log.Children[1]);
			}
		}
	}

	/// <summary>Smooth, deterministic drift from two sines, clamped to [lo,hi].</summary>
	private static double Wobble(double v, long t, int phase, double lo, double hi)
	{
		double d = Math.Sin((t + phase) * 0.5) * 6 + Math.Sin((t + phase) * 0.17) * 4;
		return Math.Clamp(v + d * 0.5, lo, hi);
	}

	private static string FormatTile(int index, int pct)
	{
		(byte r, byte g, byte b) = index switch
		{
			0 => ((byte)100, (byte)180, (byte)255),
			1 => ((byte)120, (byte)220, (byte)160),
			2 => ((byte)220, (byte)180, (byte)60),
			_ => ((byte)220, (byte)120, (byte)80),
		};
		return $"[bold rgb({r},{g},{b})]  {pct}%[/]";
	}
}
