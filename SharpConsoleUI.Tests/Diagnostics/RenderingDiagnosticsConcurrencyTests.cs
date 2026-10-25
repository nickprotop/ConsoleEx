// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Diagnostics;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.DiagnosticsInternals;

/// <summary>
/// <see cref="RenderingDiagnostics"/> is written by the render thread and read by others,
/// so every one of its histories has to tolerate that.
/// </summary>
/// <remarks>
/// <para>
/// This is a regression test for a real failure: a full suite run died inside
/// <c>CleanupOldFrames</c> with a <see cref="InvalidOperationException"/> from
/// <c>Dictionary.KeyCollection.Enumerator.MoveNext()</c>, then passed on the next run. Six
/// plain dictionaries were being enumerated on the render thread while another thread added
/// to them.
/// </para>
/// <para>
/// A single-threaded test cannot catch that, so these drive capture and read concurrently
/// and assert only that nothing throws. They are probabilistic by nature - before the fix
/// the first one failed well within a second; they are not proof of correctness, they are a
/// tripwire that would have caught this.
/// </para>
/// </remarks>
public class RenderingDiagnosticsConcurrencyTests
{
	/// <summary>Long enough to hit the race reliably, short enough not to slow the suite.</summary>
	private const int SoakMilliseconds = 750;

	/// <summary>A retention small enough that cleanup runs on almost every frame.</summary>
	private const int RetainFrames = 2;

	private static RenderingDiagnostics Make(int retain = RetainFrames) =>
		new(ConsoleWindowSystemOptions.Create() with
		{
			EnableDiagnostics = true,
			DiagnosticsRetainFrames = retain,
			DiagnosticsLayers = DiagnosticsLayers.All,
		})
		{ IsEnabled = true };

	/// <summary>Runs several actions at once for a fixed time, surfacing the first failure.</summary>
	private static void Soak(params Action[] workers)
	{
		using var cts = new CancellationTokenSource(SoakMilliseconds);
		var failures = new List<Exception>();

		var tasks = new Task[workers.Length];
		for (int i = 0; i < workers.Length; i++)
		{
			var worker = workers[i];
			tasks[i] = Task.Run(() =>
			{
				try
				{
					while (!cts.IsCancellationRequested) worker();
				}
				catch (OperationCanceledException)
				{
					// The clock ran out mid-iteration; not a failure.
				}
				catch (Exception ex)
				{
					lock (failures) failures.Add(ex);
					cts.Cancel();
				}
			});
		}

		Task.WaitAll(tasks);

		lock (failures)
		{
			if (failures.Count > 0)
				throw new Xunit.Sdk.XunitException($"concurrent access threw: {failures[0]}");
		}
	}

	[Fact]
	public void CapturingWhileReading_DoesNotThrow()
	{
		var diagnostics = Make();
		var buffer = new CharacterBuffer(20, 5);

		// The render thread: advance the frame, which runs cleanup, then file a snapshot.
		// The readers: exactly what the diagnostics socket and the tests do.
		Soak(
			() => { diagnostics.BeginFrame(); diagnostics.CaptureCharacterBuffer(buffer); },
			() => { _ = diagnostics.LastBufferSnapshot; },
			() => { _ = diagnostics.GetAllMetrics(); },
			() => { _ = diagnostics.CurrentFrameNumber; });
	}

	[Fact]
	public void EveryHistoryIsSafeToReadWhileItIsWritten()
	{
		var diagnostics = Make();
		var buffer = new CharacterBuffer(16, 4);
		var front = new ConsoleCell[8, 3];
		var back = new ConsoleCell[8, 3];

		Soak(
			() =>
			{
				diagnostics.BeginFrame();
				diagnostics.CaptureCharacterBuffer(buffer);
				diagnostics.CaptureAnsiLines(new List<string> { "\x1b[31mred\x1b[0m" });
				diagnostics.CaptureConsoleBufferState(front, back, 8, 3);
				diagnostics.CaptureConsoleOutput("\x1b[1;1Hx");
				diagnostics.RecordMetrics(new RenderingMetrics());
			},
			() =>
			{
				_ = diagnostics.LastBufferSnapshot;
				_ = diagnostics.LastAnsiSnapshot;
				_ = diagnostics.LastConsoleSnapshot;
				_ = diagnostics.LastOutputSnapshot;
				_ = diagnostics.LastMetrics;
				_ = diagnostics.LastQualityReport;
			},
			() => { _ = diagnostics.GetAllMetrics(); });
	}

	[Fact]
	public void ClearingWhileCapturing_DoesNotThrow()
	{
		// Clear resets the frame counter under the same lock, so a capture cannot file an
		// entry under a number that has just been reset out from under it.
		var diagnostics = Make();
		var buffer = new CharacterBuffer(10, 3);

		Soak(
			() => { diagnostics.BeginFrame(); diagnostics.CaptureCharacterBuffer(buffer); },
			() => diagnostics.Clear(),
			() => { _ = diagnostics.LastBufferSnapshot; });
	}

	[Fact]
	public void RetentionIsHonoured_SoCleanupIsActuallyRunning()
	{
		// Guards the test above from passing because cleanup never ran: if retention
		// stopped working, the race it guards would no longer be reachable.
		var diagnostics = Make(retain: 3);
		var buffer = new CharacterBuffer(8, 2);

		for (int i = 0; i < 50; i++)
		{
			diagnostics.BeginFrame();
			diagnostics.CaptureCharacterBuffer(buffer);
			diagnostics.RecordMetrics(new RenderingMetrics());
		}

		// 50 frames in, only the retained tail survives.
		Assert.Null(diagnostics.GetSnapshot<CharacterBufferSnapshot>(1));
		Assert.NotNull(diagnostics.GetSnapshot<CharacterBufferSnapshot>(50));
		Assert.True(diagnostics.GetAllMetrics().Count <= 4,
			$"retention of 3 left {diagnostics.GetAllMetrics().Count} metrics");
	}

	[Fact]
	public void ASnapshotIsFiledUnderTheFrameItNames()
	{
		// The frame number is read and the entry written under one lock. If those came
		// apart, a snapshot could claim frame N while sitting under key N+1.
		var diagnostics = Make(retain: 100);
		var buffer = new CharacterBuffer(8, 2);

		Soak(
			() => { diagnostics.BeginFrame(); diagnostics.CaptureCharacterBuffer(buffer); },
			() =>
			{
				var shot = diagnostics.LastBufferSnapshot;
				if (shot == null) return;

				var filed = diagnostics.GetSnapshot<CharacterBufferSnapshot>(shot.FrameNumber);
				if (!ReferenceEquals(filed, shot) && filed != null)
					throw new InvalidOperationException(
						$"snapshot says frame {shot.FrameNumber} but a different entry is filed there");
			});
	}
}
