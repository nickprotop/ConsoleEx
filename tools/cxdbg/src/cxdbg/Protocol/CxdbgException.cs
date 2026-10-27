// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace Cxdbg.Protocol;

/// <summary>
/// Something went wrong that the user can act on.
/// </summary>
/// <remarks>
/// Carries the server's own message wherever there is one. The server knows
/// why it refused - which driver owns the size, that diagnostics were never
/// enabled - and flattening that into "command failed" throws away the only
/// part worth reading.
/// </remarks>
public sealed class CxdbgException : Exception
{
	/// <summary>The server's error code, when it sent one.</summary>
	/// <remarks>
	/// <c>wrong_driver</c> and <c>diagnostics_disabled</c> today. A caller can
	/// branch on it; the message is what the user sees.
	/// </remarks>
	public string? Code { get; }

	/// <summary>
	/// The code for a command line cxdbg itself rejected, before the wire.
	/// </summary>
	/// <remarks>
	/// Not a server code: it marks the case where the application never saw the
	/// request. Exit 1 would say "it answered and said no", which would be a lie
	/// about where the fault is, and a script branching on exit codes would
	/// retry against an app that is perfectly healthy.
	/// </remarks>
	public const string BadUsage = "cxdbg_bad_usage";

	/// <summary>A command line cxdbg could not make sense of.</summary>
	/// <param name="message">What was wrong, and what to type instead.</param>
	public static CxdbgException Usage(string message) => new(message, BadUsage);

	public CxdbgException(string message, string? code = null, Exception? inner = null)
		: base(message, inner)
	{
		Code = code;
	}
}
