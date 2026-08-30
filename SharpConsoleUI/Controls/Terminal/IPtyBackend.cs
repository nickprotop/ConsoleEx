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
	/// </summary>
	int? ExitCode { get; }
}
