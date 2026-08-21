// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI
{
	/// <summary>
	/// A running console window system whose frames the caller drives, returned by
	/// <see cref="ConsoleWindowSystem.BeginHosted"/>.
	/// </summary>
	/// <remarks>
	/// <see cref="ConsoleWindowSystem.Run"/> owns its loop and blocks the calling thread. A host that
	/// already has an event loop — a browser, a game loop, a GUI application embedding a console
	/// surface — uses this instead and calls <see cref="Tick"/> once per frame.
	///
	/// <para>Disposing the session performs the same teardown <see cref="ConsoleWindowSystem.Run"/>
	/// does on exit, so the terminal (or host) is restored even if the loop throws. The
	/// <c>using</c> form makes that structural:</para>
	///
	/// <example>
	/// <code>
	/// using var session = ws.BeginHosted();
	/// while (session.Tick())
	/// {
	///     // Pump the host's own work here — its message queue, its render callback.
	/// }
	/// </code>
	/// </example>
	///
	/// <para>Do not add a sleep of your own. Each <see cref="Tick"/> ends with the system's own
	/// adaptive idle wait, which shortens when there is work and lengthens when there is not. A host
	/// that must not block inside <see cref="Tick"/> — a browser frame callback, a game loop — sets
	/// <see cref="ConsoleWindowSystem.BlockWhenIdle"/> to <c>false</c> and paces the calls with its
	/// own scheduler instead.</para>
	///
	/// <para>Not thread-safe, and deliberately so: call <see cref="Tick"/> on the same thread that
	/// called <see cref="ConsoleWindowSystem.BeginHosted"/>, which is captured as the UI thread.</para>
	/// </remarks>
	public sealed class HostedSession : IDisposable
	{
		private readonly ConsoleWindowSystem _system;
		private bool _disposed;

		internal HostedSession(ConsoleWindowSystem system) => _system = system;

		/// <summary>
		/// Runs one iteration of the main loop: input, queued UI actions, animation advance, render
		/// when dirty, then the idle wait (see <see cref="ConsoleWindowSystem.BlockWhenIdle"/>).
		/// </summary>
		/// <remarks>
		/// Exceptions propagate to the caller, which owns its own error policy — unlike
		/// <see cref="ConsoleWindowSystem.Run"/>, which catches them and sets an exit code.
		/// </remarks>
		/// <returns><c>true</c> while the system is running; <c>false</c> once it should stop.</returns>
		public bool Tick()
		{
			if (_disposed) return false;
			return _system.TickCore();
		}

		/// <summary>
		/// Stops the system and restores what <see cref="ConsoleWindowSystem.BeginHosted"/> established.
		/// Safe to call more than once.
		/// </summary>
		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			_system.StopHostedCore();
		}
	}
}
