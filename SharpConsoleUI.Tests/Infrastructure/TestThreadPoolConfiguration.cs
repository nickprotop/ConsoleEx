// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// Raises the thread pool's minimum worker count for the test assembly.
/// <para>
/// <b>Why this exists.</b> The suite runs its collections in parallel, and a great many tests queue
/// work to the thread pool and then wait for it — a UI action marshalled through
/// <c>EnqueueOnUIThread</c>, an <c>await</c> continuation, an event raised from a queued work item.
/// Once the pool is saturated, .NET injects additional worker threads at roughly one or two per
/// second, so a queued item can sit for seconds before it starts running. A test that waits a fixed
/// five seconds for such an item is not measuring whether the product works; it is measuring whether
/// the thread pool grew in time, and it loses that race intermittently, on a machine-dependent
/// schedule, in whichever test happened to be unlucky.
/// </para>
/// <para>
/// That is the shape every intermittent failure in this suite has had: a wall-clock budget for work
/// queued behind a contended pool. Raising the floor means the threads already exist when the work is
/// queued, so the waits measure the product again. It does not paper over a hang — genuinely blocked
/// work still exhausts its timeout and fails.
/// </para>
/// <para>
/// A module initializer rather than a fixture: this must apply before the first test in any
/// collection starts, including tests that do not opt into any fixture.
/// </para>
/// </summary>
internal static class TestThreadPoolConfiguration
{
	[ModuleInitializer]
	internal static void RaiseMinimumWorkerThreads()
	{
		ThreadPool.GetMinThreads(out int workerMin, out int completionPortMin);

		// Four per core comfortably covers the queued-work fan-out of a parallel run without being so
		// large that the pool wastes threads; the floor of 64 keeps small CI runners honest.
		int target = Math.Max(64, Environment.ProcessorCount * 4);

		ThreadPool.SetMinThreads(
			Math.Max(workerMin, target),
			Math.Max(completionPortMin, target));
	}
}
