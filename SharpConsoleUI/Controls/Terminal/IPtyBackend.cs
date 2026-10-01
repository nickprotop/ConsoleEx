// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// Abstracts the platform-specific PTY (pseudo-terminal) backend.
/// Implementations: <see cref="LinuxPtyBackend"/> (openpty), <see cref="WindowsPtyBackend"/> (ConPTY).
/// </summary>
internal interface IPtyBackend : IDisposable
{
	/// <summary>Resize the terminal to the given dimensions.</summary>
	void Resize(int rows, int cols);

	/// <summary>
	/// Starts shutting the session down and returns immediately — it must never block on the
	/// child. Its job is to make a thread blocked in <see cref="Read"/> wake up, and to stop
	/// further <see cref="Write"/>/<see cref="Resize"/> calls reaching the OS handle.
	/// <para>
	/// This exists because closing the handle is not enough on Linux: a thread already blocked in
	/// <c>read()</c> on a PTY master stays blocked when the fd is closed (measured — see
	/// <c>docs/superpowers/notes/2026-10-01-terminalcontrol-lifecycle-analysis.md</c>). The child's
	/// process group has to be signalled for the read to return. So the wake is a separate,
	/// non-blocking step, and the blocking reap stays in <see cref="IDisposable.Dispose"/> where
	/// the caller can push it off the UI thread.
	/// </para>
	/// Safe to call repeatedly and from any thread, including after <see cref="IDisposable.Dispose"/>.
	/// </summary>
	void BeginShutdown();

	/// <summary>
	/// Read output bytes from the PTY.
	/// Returns the number of bytes read, or 0 on EOF / backend closed.
	/// </summary>
	int Read(byte[] buf, int count);

	/// <summary>Write keyboard/mouse bytes to the PTY.</summary>
	void Write(byte[] buf, int count);

	/// <summary>The OS process ID of the child process running inside the PTY.</summary>
	int ChildProcessId { get; }

	/// <summary>
	/// The child's exit status once it has exited; null while it is still running or when the
	/// status could not be determined (the post-EOF wait timed out). Both backends learn the
	/// status during <see cref="IDisposable.Dispose"/> — that is where the wait on the child
	/// already lives — so it only becomes non-null after disposal.
	/// <para>
	/// Written by whichever thread runs Dispose and read from others, so implementations must
	/// publish it safely rather than relying on an ordinary field write.
	/// </para>
	/// </summary>
	int? ExitCode { get; }
}
