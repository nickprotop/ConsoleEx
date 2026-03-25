// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Drivers
{
	/// <summary>
	/// Thrown when SharpConsoleUI detects that the terminal environment cannot support
	/// interactive console UI rendering.
	/// </summary>
	/// <remarks>
	/// <para>Common causes:</para>
	/// <list type="bullet">
	///   <item>stdin or stdout is piped/redirected (not connected to a TTY)</item>
	///   <item>The terminal does not respond to ANSI escape sequences (e.g., DSR query)</item>
	///   <item>Running inside an embedded terminal that lacks alternate screen buffer support</item>
	/// </list>
	/// <para>
	/// If the application is stuck and this exception was not caught, press Ctrl+\
	/// (SIGQUIT) to force-exit the process. SIGQUIT is intentionally never suppressed.
	/// </para>
	/// </remarks>
	public class TerminalNotSupportedException : InvalidOperationException
	{
		/// <summary>
		/// Initializes a new instance of <see cref="TerminalNotSupportedException"/>
		/// with the specified error message.
		/// </summary>
		/// <param name="message">A message describing why the terminal is not supported.</param>
		public TerminalNotSupportedException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of <see cref="TerminalNotSupportedException"/>
		/// with the specified error message and inner exception.
		/// </summary>
		/// <param name="message">A message describing why the terminal is not supported.</param>
		/// <param name="innerException">The exception that caused this failure.</param>
		public TerminalNotSupportedException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
