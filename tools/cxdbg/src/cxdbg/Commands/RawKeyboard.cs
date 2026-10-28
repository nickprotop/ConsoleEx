// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace Cxdbg.Commands;

/// <summary>
/// Puts the terminal where a single keypress is readable, and puts it back.
/// </summary>
/// <remarks>
/// <para>
/// A terminal starts in canonical mode: the line discipline holds what is typed
/// until Enter, so <see cref="Console.KeyAvailable"/> stays false however many
/// keys are waiting. A loop polling it therefore never sees the key that was
/// meant to stop it - which is exactly how this failed, and it fails silently,
/// because nothing is wrong except that the answer is always no.
/// </para>
/// <para>
/// Setting <see cref="Console.TreatControlCAsInput"/> is what moves .NET out of
/// canonical mode, as a side effect of the tcsetattr it performs. It is a side
/// effect rather than the intent, so it is done here, named, once, instead of
/// appearing as an unexplained line in the middle of a polling loop.
/// </para>
/// <para>
/// Ctrl-c keeps working: the value is restored on the way out, and the watch's
/// own cancel handler is what ends it in the meantime.
/// </para>
/// </remarks>
internal sealed class RawKeyboard : IDisposable
{
	private readonly bool _restore;
	private readonly bool _previous;

	private RawKeyboard(bool restore, bool previous)
	{
		_restore = restore;
		_previous = previous;
	}

	/// <summary>Enters single-key mode, if there is a terminal to put into it.</summary>
	internal static RawKeyboard Enter()
	{
		// Nothing to do without a keyboard, and reading TreatControlCAsInput on a
		// redirected stdin throws on some platforms.
		if (Console.IsInputRedirected) return new RawKeyboard(restore: false, previous: false);

		try
		{
			bool previous = Console.TreatControlCAsInput;

			// Left true for the duration, not toggled: restoring it immediately
			// puts the line discipline back and the keys queue up again. The watch
			// therefore reads ctrl-c as a key rather than a signal, and
			// StopPressed treats it as a stop - so ctrl-c still works, it just
			// arrives through the keyboard instead of the handler.
			Console.TreatControlCAsInput = true;

			return new RawKeyboard(restore: true, previous: previous);
		}
		catch (Exception ex) when (ex is IOException or PlatformNotSupportedException
			or UnauthorizedAccessException)
		{
			// A terminal that will not be reconfigured is not a reason to refuse to
			// watch: the count and ctrl-c still end it.
			return new RawKeyboard(restore: false, previous: false);
		}
	}

	public void Dispose()
	{
		if (!_restore) return;

		try { Console.TreatControlCAsInput = _previous; }
		catch (Exception ex) when (ex is IOException or PlatformNotSupportedException
			or UnauthorizedAccessException)
		{
			// Leaving the terminal as found is best effort on the way out.
		}
	}
}
