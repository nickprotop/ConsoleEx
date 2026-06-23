// -----------------------------------------------------------------------
// BizDashboard — deterministic analytics simulation.
//
// Drives ONLY the Sales-page KPIs + revenue chart from a single reproducible
// walk (sines + a counter, NO System.Random). Runs on the WINDOW's async
// thread, so control mutations happen on the UI thread and re-render via the
// reactive invalidation path — no manual invalidation, no marshalling.
//
// The Point of Sale page is interaction-driven and is NOT simulated.
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace BizDashboard;

internal static class Simulation
{
	/// <summary>Rolling window length applied to the revenue line graph.</summary>
	public const int MaxPoints = 60;

	private const int TickMs = 600;

	/// <summary>
	/// Runs the analytics walk until the window's cancellation token trips.
	/// All control mutations run on the window thread (UI-thread safe).
	/// </summary>
	public static async Task Run(BizRefs refs, CancellationToken ct)
	{
		long t = 0;

		// Drift state.
		double revenue = 184_200;   // dollars MTD
		double orders = 1_284;      // count MTD
		double churn = 2.4;         // percent

		while (!ct.IsCancellationRequested)
		{
			await Task.Delay(TickMs, ct);
			t++;

			// Smooth deterministic drift.
			revenue = Math.Clamp(revenue + Math.Sin(t * 0.5) * 1400 + Math.Sin(t * 0.13) * 600, 120_000, 260_000);
			orders = Math.Clamp(orders + Math.Sin(t * 0.4 + 1.0) * 9 + Math.Sin(t * 0.11) * 4, 800, 2_000);
			churn = Math.Clamp(churn + Math.Sin(t * 0.23 + 2.0) * 0.06, 1.2, 4.5);

			double revDelta = Math.Sin(t * 0.17) * 6.0;        // +/- % vs last month
			double ordDelta = Math.Sin(t * 0.19 + 0.7) * 5.0;
			double churnDelta = Math.Sin(t * 0.21 + 1.3) * 0.5;

			if (refs.RevenueTile is { } rev)
				rev.SetContent(SplitLines(Pages.FormatRevenue((int)Math.Round(revenue), revDelta)));
			if (refs.OrdersTile is { } ord)
				ord.SetContent(SplitLines(Pages.FormatOrders((int)Math.Round(orders), ordDelta)));
			if (refs.ChurnTile is { } chu)
				chu.SetContent(SplitLines(Pages.FormatChurn(churn, churnDelta)));

			if (refs.RevenueGraph is { } graph)
			{
				// Map revenue into the 0..100 axis window.
				double scaled = Math.Clamp((revenue - 120_000) / (260_000 - 120_000) * 100.0, 0, 100);
				graph.AddDataPoint(scaled);
			}
		}
	}

	private static List<string> SplitLines(string s) =>
		new(s.Split('\n'));
}
