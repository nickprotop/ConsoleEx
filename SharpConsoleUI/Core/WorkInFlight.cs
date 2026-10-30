// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Core;

/// <summary>
/// Counts work that is RUNNING, as opposed to work that is waiting.
/// </summary>
/// <remarks>
/// <para>
/// A queue answers what is waiting. It says nothing about what has left it and
/// is now executing, and that gap is where "has the system finished reacting"
/// gets the wrong answer: the queue is empty, truthfully, while the work it
/// held is halfway done.
/// </para>
/// <para>
/// Two places need the same answer. A mouse event is dispatched synchronously
/// on whichever thread raised it and never enters a queue at all; a queued UI
/// action is dequeued before it runs, so between those two moments it exists
/// nowhere the system can see. One type for both, because they are the same
/// question and a second copy is where a counter stops being incremented.
/// </para>
/// </remarks>
internal sealed class WorkInFlight
{
	private int _running;

	/// <summary>Whether any such work is executing, on any thread.</summary>
	internal bool IsRunning => Volatile.Read(ref _running) > 0;

	/// <summary>Runs the work, counted for as long as it takes.</summary>
	/// <param name="work">The call, and everything it calls.</param>
	/// <remarks>
	/// A count rather than a flag: the work can nest, and an inner piece
	/// finishing must not report the outer one as done.
	/// </remarks>
	internal void While(Action work)
	{
		Interlocked.Increment(ref _running);

		try
		{
			work();
		}
		finally
		{
			// In a finally, so work that throws cannot leave the count
			// permanently above zero - which would make the system never read
			// as settled again, a worse fault than the one this closes.
			Interlocked.Decrement(ref _running);
		}
	}
}
