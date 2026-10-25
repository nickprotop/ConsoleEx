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
	/// A console window system running its own loop on its own thread, returned by
	/// <see cref="ConsoleWindowSystem.RunInBackground"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="ConsoleWindowSystem.Run"/> owns the loop and blocks the caller;
	/// <see cref="ConsoleWindowSystem.BeginHosted"/> hands the loop to the caller. This is the
	/// third case: the system owns the loop, and the caller keeps its thread. That is what a
	/// headless host wants - a web server, a test, an agent driving the app from outside -
	/// where something else needs the calling thread.
	/// </para>
	/// <para>
	/// The loop runs on a background thread, so the process will not stay alive for it. Hold
	/// the session for as long as the application should run, and dispose it to stop.
	/// </para>
	/// </remarks>
	public sealed class BackgroundSession : IDisposable
	{
		private readonly ConsoleWindowSystem _system;
		private readonly Thread _thread;
		private readonly TimeSpan _stopTimeout;

		private volatile bool _stopping;
		private bool _disposed;

		internal BackgroundSession(ConsoleWindowSystem system, Thread thread, TimeSpan stopTimeout)
		{
			_system = system;
			_thread = thread;
			_stopTimeout = stopTimeout;
		}

		/// <summary>Whether the loop is still running.</summary>
		public bool IsRunning => _thread.IsAlive && !_stopping;

		/// <summary>
		/// The exception that ended the loop, or <c>null</c> if it is running or stopped cleanly.
		/// </summary>
		/// <remarks>
		/// Nobody is awaiting the loop's thread, so a fault after startup would otherwise be
		/// invisible: the screen simply stops changing. Check this when the application seems
		/// frozen - and note that a startup failure is thrown from
		/// <see cref="ConsoleWindowSystem.RunInBackground"/> instead, where the caller cannot
		/// miss it.
		/// </remarks>
		public Exception? Fault { get; internal set; }

		/// <summary>Whether the loop is signalled to stop.</summary>
		internal bool Stopping => _stopping;

		/// <summary>
		/// Stops the loop and waits briefly for it to finish.
		/// </summary>
		/// <remarks>
		/// Bounded: a loop wedged in application code must not hang the caller's teardown, so
		/// the join gives up after the timeout and leaves the thread to the runtime - it is a
		/// background thread, so it cannot keep the process alive.
		/// </remarks>
		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			_stopping = true;
			_system.Shutdown();

			if (_thread.IsAlive) _thread.Join(_stopTimeout);
		}
	}
}
