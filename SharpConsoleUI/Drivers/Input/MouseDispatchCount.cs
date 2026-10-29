// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Drivers.Input;

/// <summary>
/// Counts the mouse handlers running right now, across every thread.
/// </summary>
/// <remarks>
/// <para>
/// A mouse event is dispatched synchronously, on whichever thread raised it,
/// so a handler can be running while another thread asks whether the system
/// has finished reacting. Without this, "nothing is queued" reads as settled
/// at the moment a control is halfway through handling a click.
/// </para>
/// <para>
/// One type rather than the same three members in each driver: they would be
/// the same code twice, and the second copy is where a counter stops being
/// incremented.
/// </para>
/// </remarks>
internal sealed class MouseDispatchCount
{
	private int _running;

	/// <summary>Whether a mouse handler is executing on any thread.</summary>
	internal bool IsDispatching => Volatile.Read(ref _running) > 0;

	/// <summary>Runs a dispatch, counted for as long as it takes.</summary>
	/// <param name="dispatch">The raise, and everything it calls.</param>
	internal void While(Action dispatch)
	{
		Interlocked.Increment(ref _running);

		try
		{
			dispatch();
		}
		finally
		{
			// In a finally, so a handler that throws cannot leave the count
			// permanently above zero - which would make the system never
			// read as settled again.
			Interlocked.Decrement(ref _running);
		}
	}
}
