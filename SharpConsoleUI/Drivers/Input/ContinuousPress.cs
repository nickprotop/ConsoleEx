// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;

namespace SharpConsoleUI.Drivers.Input;

/// <summary>
/// Repeats a held mouse button, which the terminal does not do for us.
/// </summary>
/// <remarks>
/// <para>
/// A terminal reports a press and a release and nothing in between, so a button
/// held down on a scrollbar arrow would move one line and stop. This supplies
/// the auto-repeat, and it is the one piece of the mouse path that genuinely
/// belongs to whoever is reading input: only that code knows a button is still
/// physically down.
/// </para>
/// <para>
/// It existed twice, once per platform, and only one copy was correct. The
/// Windows copy grew a cancellation token after three ways of leaking it were
/// found; the Unix copy kept the original bare <c>Task.Run</c> and all three.
/// One copy, instance-scoped, so a second system in the same process cannot
/// inherit a live loop from the first.
/// </para>
/// </remarks>
internal sealed class ContinuousPress : IDisposable
{
	private readonly object _gate = new();
	private CancellationTokenSource? _running;
	private bool _disposed;

	/// <summary>Whether a button is being repeated right now.</summary>
	/// <remarks>Volatile: written by the input thread, read by the repeat loop.</remarks>
	private volatile bool _held;

	/// <summary>Which button is being repeated, so a release can match it.</summary>
	/// <remarks>
	/// A terminal reports one button at a time, but a release for a button that
	/// is NOT the held one still arrives - a chord, or a release the press of
	/// which was missed. Stopping on any release at all would end a repeat the
	/// user is still holding.
	/// </remarks>
	private MouseFlags _button;

	/// <summary>Where the pointer was when it was last seen.</summary>
	/// <remarks>
	/// Kept current by motion, so a held button repeats where the pointer IS
	/// rather than where it went down - which is what makes dragging a scrollbar
	/// past its track keep scrolling.
	/// </remarks>
	private Point _point;

	/// <summary>Starts repeating a button, replacing any repeat already running.</summary>
	/// <param name="button">The button being held.</param>
	/// <param name="at">Where it went down.</param>
	/// <param name="repeat">Called once per interval while it is held.</param>
	/// <remarks>
	/// A press of a button already held is the terminal restating a drag rather
	/// than a new press, and one loop is enough - but the previous one is
	/// cancelled regardless, so a missed release cannot stack two.
	/// </remarks>
	internal void Begin(MouseFlags button, Point at, Action<MouseFlags, Point> repeat)
	{
		CancellationToken token;

		lock (_gate)
		{
			// Checked INSIDE the lock. Outside it, a Begin that had already
			// passed the check could finish after Dispose had run: it would
			// start a loop whose token nothing cancels and whose _held nothing
			// clears, against a driver that is gone. That is the leak this
			// class exists to close, reintroduced by the check's position.
			if (_disposed) return;

			_point = at;
			_held = true;
			_button = button;

			// Cancelled but NOT disposed. The loop it belongs to may still be
			// waking from that cancel and about to read its own token, and
			// reading a token whose source has been disposed throws. The source
			// is small and the loop drops it the moment it exits, so letting the
			// collector have it is cheaper than the race.
			_running?.Cancel();

			_running = new CancellationTokenSource();
			token = _running.Token;
		}

		_ = Task.Run(() => RepeatAsync(button, repeat, token));
	}

	/// <summary>Stops repeating, on release.</summary>
	/// <summary>Stops repeating, on release of the button being repeated.</summary>
	/// <param name="button">
	/// The button that was released, as a PRESS flag. A release for a different
	/// button leaves the repeat alone.
	/// </param>
	internal void End(MouseFlags button)
	{
		lock (_gate)
		{
			if (!_held || _button != button) return;

			_held = false;

			// Cancel only, for the same reason as Begin: the loop may still be
			// reading its token as it unwinds.
			_running?.Cancel();
		}
	}

	/// <summary>The press flag a release flag corresponds to.</summary>
	/// <remarks>
	/// The repeat is keyed by the PRESS flag it was started with; a release
	/// arrives as a different bit of the same enum, so the two have to be
	/// matched rather than compared.
	/// </remarks>
	internal static MouseFlags PressFor(MouseFlags released) => released switch
	{
		MouseFlags.Button1Released => MouseFlags.Button1Pressed,
		MouseFlags.Button2Released => MouseFlags.Button2Pressed,
		MouseFlags.Button3Released => MouseFlags.Button3Pressed,
		MouseFlags.Button4Released => MouseFlags.Button4Pressed,
		_ => 0,
	};

	/// <summary>Keeps the repeat position current as the pointer moves.</summary>
	internal void Moved(Point to)
	{
		lock (_gate) _point = to;
	}

	private async Task RepeatAsync(
		MouseFlags button, Action<MouseFlags, Point> repeat, CancellationToken token)
	{
		try
		{
			// The token as well as the flag. The flag is cleared only by a release,
			// so a driver shutting down mid-press, or never seeing the release,
			// would otherwise leave this running against a dead handler.
			while (_held && !token.IsCancellationRequested)
			{
				await Task.Delay(Configuration.ControlDefaults.ContinuousPressIntervalMs, token)
					.ConfigureAwait(false);

				if (!_held || token.IsCancellationRequested) break;

				Point at;
				lock (_gate) at = _point;

				repeat(button, at);
			}
		}
		catch (OperationCanceledException)
		{
			// Expected: the press ended, or the owner was disposed.
		}
		catch (Exception)
		{
			// The handler threw. Nothing awaits this task, so an escaping exception
			// would be unobserved - swallowed rather than faulting a task nobody
			// is watching.
		}
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		_held = false;

		lock (_gate)
		{
			// Cancel only, for the same reason as Begin: the loop may still be
			// reading its token as it unwinds.
			_running?.Cancel();
			_running = null;
		}
	}
}
