// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using SharpConsoleUI;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering;

/// <summary>
/// Concurrency stress coverage for the lock-free Max-join invalidation accumulator
/// (<see cref="Window.Invalidate(Invalidation, Controls.IWindowControl?)"/> →
/// <c>Request</c> → <c>Interlocked.CompareExchange</c> loop, read via <see cref="Window.PendingWork"/>).
///
/// The accumulator is the one place where "lock-free and never drops intent" is the load-bearing
/// correctness argument: a cross-thread invalidation that races a concurrent consume (or other
/// requests) must fold in via a monotone Max-join (<c>Relayout &gt; Repaint &gt; None</c>) and can never
/// be silently lost or regressed. These tests hammer that path from many threads to flush out a
/// torn read, a lost update, or a non-monotone transition that a single-threaded test cannot reach.
///
/// Determinism: every property asserted here is a TRUE invariant of the Max-join, not a timing
/// accident — so a correct implementation passes every run and a broken one (e.g. a plain
/// non-atomic <c>_pendingWork = max(...)</c>) fails with high probability under contention. The
/// thread counts are sized to oversubscribe typical CI cores so the interleavings actually occur.
/// </summary>
public class MaxJoinConcurrencyStressTests
{
	private static Window NewWindow()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 20);
		var window = new Window(system) { Title = "MaxJoin", Left = 0, Top = 0, Width = 30, Height = 12 };
		system.AddWindow(window);
		return window;
	}

	/// <summary>
	/// Barrier-synchronized fan-out: N threads each fire a burst of <see cref="Invalidation"/> requests.
	/// If ANY thread ever requested <see cref="Invalidation.Relayout"/>, the accumulator MUST settle at
	/// <see cref="FrameWork.Relayout"/> — the dominant value can never be lost to a racing Repaint write.
	/// This is the canonical "lost update" failure of a non-atomic max; a CAS-loop Max-join is immune.
	/// </summary>
	[Fact]
	public void Relayout_RequestedByAnyThread_NeverLostToConcurrentRepaints()
	{
		const int iterations = 500;
		int threads = Math.Max(8, Environment.ProcessorCount * 2);

		for (int iter = 0; iter < iterations; iter++)
		{
			var window = NewWindow();
			// Drain the fresh window's initial Relayout so each iteration starts from a known low state.
			window.RenderAndGetVisibleContent(new List<System.Drawing.Rectangle> { new(0, 0, 30, 12) });
			Assert.Equal(FrameWork.None, window.PendingWork);

			using var start = new Barrier(threads);
			// Exactly one thread fires the dominant Relayout; everyone else floods Repaints around it.
			int relayoutThread = iter % threads;

			Parallel.For(0, threads, t =>
			{
				start.SignalAndWait();
				if (t == relayoutThread)
				{
					window.Invalidate(Invalidation.Relayout);
				}
				else
				{
					for (int k = 0; k < 64; k++)
						window.Invalidate(Invalidation.Repaint);
				}
			});

			// The single Relayout must dominate the 7×64+ concurrent Repaints — every time.
			Assert.Equal(FrameWork.Relayout, window.PendingWork);
		}
	}

	/// <summary>
	/// While only requests are happening (no consume), the accumulator is monotone NON-DECREASING:
	/// a reader thread sampling <see cref="Window.PendingWork"/> in a tight loop must never observe a
	/// value go DOWN (e.g. Relayout→Repaint) nor read a value outside the valid enum set. A torn read
	/// or a non-atomic write would surface here as a regression or a garbage int.
	/// </summary>
	[Fact]
	public void PendingWork_WhileAccumulating_IsMonotoneNonDecreasing_AndNeverTorn()
	{
		int writerThreads = Math.Max(6, Environment.ProcessorCount * 2);
		var window = NewWindow();
		// Consume the initial Relayout so we start at None and can watch it climb.
		window.RenderAndGetVisibleContent(new List<System.Drawing.Rectangle> { new(0, 0, 30, 12) });

		var stop = new ManualResetEventSlim(false);
		var violations = new ConcurrentBag<string>();

		// Reader: continuously samples PendingWork and asserts it never decreases and stays in-range.
		var reader = Task.Run(() =>
		{
			FrameWork high = FrameWork.None;
			while (!stop.IsSet)
			{
				var v = window.PendingWork;
				if (v != FrameWork.None && v != FrameWork.Repaint && v != FrameWork.Relayout)
					violations.Add($"torn read: {(int)v}");
				if ((int)v < (int)high)
					violations.Add($"regressed: {high} -> {v}");
				if ((int)v > (int)high)
					high = v;
			}
			// Final settle: after all writers, the peak must be Relayout (writers requested it below).
			if (window.PendingWork != FrameWork.Relayout)
				violations.Add($"final not Relayout: {window.PendingWork}");
		});

		// Writers: ramp intent UP (Repaint bursts, then a Relayout) so the monotone climb is exercised.
		Parallel.For(0, writerThreads, t =>
		{
			for (int k = 0; k < 200; k++)
				window.Invalidate(Invalidation.Repaint);
			window.Invalidate(Invalidation.Relayout);
			for (int k = 0; k < 50; k++)
				window.Invalidate(Invalidation.Repaint); // must NOT pull the peak back down
		});

		// Give the reader a moment to observe the settled peak, then stop.
		SpinWait.SpinUntil(() => window.PendingWork == FrameWork.Relayout, TimeSpan.FromSeconds(2));
		stop.Set();
		reader.Wait(TimeSpan.FromSeconds(5));

		Assert.True(violations.IsEmpty, "Max-join monotonicity violated: " + string.Join("; ", violations));
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	/// <summary>
	/// The hard race the lock-free design exists for: a thread requesting <see cref="Invalidation.Relayout"/>
	/// CONCURRENTLY with the consume (<c>Interlocked.Exchange</c> inside the render). The Max-join contract is
	/// that a request is never silently LOST — it is folded in either before the consume (cleared by that
	/// frame) or after it (still pending). We drive real renders (the consume) on this thread while a flood
	/// of Relayout requests races them, and assert the accumulator invariants that hold regardless of
	/// interleaving:
	///   (a) <see cref="Window.PendingWork"/> is ALWAYS a valid enum value mid-race (never torn);
	///   (b) after the race, a request issued strictly AFTER a final render is observable as pending
	///       (the consume's <c>Exchange</c> resets to None but never swallows a subsequent request).
	///
	/// NOTE: post-consolidation, PendingWork is the single dirty signal (no _invalidated flag to desync),
	/// so the render-drain coupling that this NOTE used to warn about no longer exists. The quiescent-drain
	/// invariant is now asserted directly in QuiescentRender_LeavesNone_AndPostConsumeRequestIsPending.
	/// </summary>
	[Fact]
	public void RelayoutRequest_RacingConsume_IsNeverDropped()
	{
		const int rounds = 300;
		var region = new List<System.Drawing.Rectangle> { new(0, 0, 30, 12) };

		for (int r = 0; r < rounds; r++)
		{
			var window = NewWindow();
			using var start = new Barrier(2);
			var torn = new ConcurrentBag<int>();

			// Requester thread: fires a burst of Relayout requests timed to overlap the renders.
			var requester = Task.Run(() =>
			{
				start.SignalAndWait();
				for (int k = 0; k < 32; k++)
				{
					window.Invalidate(Invalidation.Relayout);
					var v = window.PendingWork; // sample mid-race
					if (v != FrameWork.None && v != FrameWork.Repaint && v != FrameWork.Relayout)
						torn.Add((int)v);
				}
			});

			// Consumer thread (this one): renders repeatedly, each render Interlocked.Exchanges the work to None.
			start.SignalAndWait();
			for (int k = 0; k < 8; k++)
			{
				window.RenderAndGetVisibleContent(region);
				var v = window.PendingWork; // sample mid-race
				if (v != FrameWork.None && v != FrameWork.Repaint && v != FrameWork.Relayout)
					torn.Add((int)v);
			}

			requester.Wait(TimeSpan.FromSeconds(5));

			Assert.True(torn.IsEmpty, "torn PendingWork read mid-race: " + string.Join(",", torn));

			// Drain whatever the race left, so we start the post-condition from a clean, settled state.
			window.RenderAndGetVisibleContent(region);

			// A request issued strictly AFTER the last consume is observable as pending — the consume's
			// Exchange-to-None never swallows a subsequent request.
			window.Invalidate(Invalidation.Relayout);
			Assert.Equal(FrameWork.Relayout, window.PendingWork);
		}
	}

	/// <summary>
	/// Mixed-intent free-for-all: many threads fire a random-but-deterministic mix of Repaint/Relayout
	/// with no synchronization at all. The only guaranteed invariant is the Max-join's: the final state
	/// equals the maximum intent any thread requested. Since at least one Relayout is always issued, the
	/// settled value is exactly <see cref="FrameWork.Relayout"/>. Runs many times to vary interleavings.
	/// </summary>
	[Fact]
	public void MixedIntentFlood_SettlesAtMaxRequested()
	{
		const int iterations = 400;
		int threads = Math.Max(8, Environment.ProcessorCount * 2);

		for (int iter = 0; iter < iterations; iter++)
		{
			var window = NewWindow();
			window.RenderAndGetVisibleContent(new List<System.Drawing.Rectangle> { new(0, 0, 30, 12) });

			Parallel.For(0, threads, t =>
			{
				// Deterministic per-(iter,thread) pattern — no Random (which is banned and non-repro).
				for (int k = 0; k < 50; k++)
				{
					bool relayout = ((iter + t + k) & 3) == 0; // ~25% Relayout, 75% Repaint
					window.Invalidate(relayout ? Invalidation.Relayout : Invalidation.Repaint);
				}
				// Guarantee at least one Relayout per thread so the expected max is unambiguous.
				window.Invalidate(Invalidation.Relayout);
			});

			Assert.Equal(FrameWork.Relayout, window.PendingWork);
		}
	}

	/// <summary>
	/// Post-consolidation invariant (only assertable once the dual signal is gone): after a render
	/// consumes the frame, the window is clean (PendingWork == None) AND a request issued strictly after
	/// that consume is observable as pending. With the old _invalidated flag, a request racing the
	/// consume could leave PendingWork set but _invalidated clear, so the next render skipped the consume
	/// and PendingWork stayed stuck — meaning "PendingWork == None after a quiescent render" was NOT a
	/// reliable invariant. Now PendingWork is the only signal, so it is.
	/// </summary>
	[Fact]
	public void QuiescentRender_LeavesNone_AndPostConsumeRequestIsPending()
	{
		var window = NewWindow();
		var region = new List<System.Drawing.Rectangle> { new(0, 0, 30, 12) };

		// Dirty, then a quiescent render with no concurrent writers: must end clean.
		window.Invalidate(Invalidation.Relayout);
		window.RenderAndGetVisibleContent(region);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// A request strictly after the consume is pending (the Exchange-to-None never swallows it).
		window.Invalidate(Invalidation.Relayout);
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}
}
