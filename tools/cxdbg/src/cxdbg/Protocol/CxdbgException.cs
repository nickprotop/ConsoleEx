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

	public CxdbgException(string message, string? code = null, Exception? inner = null)
		: base(message, inner)
	{
		Code = code;
	}
}
