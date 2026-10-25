// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Diagnostics.Socket;

/// <summary>
/// Decides whether a diagnostics socket may open, asking the user when the request came from
/// the environment.
/// </summary>
/// <remarks>
/// <para>
/// An environment variable is not consent. It can be inherited from a shell profile, baked
/// into a <c>.desktop</c> file or a systemd unit, or left over in a terminal from hours ago.
/// Any of those would otherwise silently open a socket that exposes the user's screen and
/// hands over control of the application. So the environment route asks, out loud, first.
/// </para>
/// <para>
/// A socket requested through <c>ConsoleWindowSystemOptions.DiagnosticsSocket</c> does not
/// prompt. That is not a loophole: a property cannot be inherited from any of those routes,
/// so it can only be set by someone writing it into the program.
/// </para>
/// </remarks>
internal static class DiagnosticsConsent
{
	internal const string SocketEnvVar = "SHARPCONSOLEUI_DIAGNOSTICS_SOCKET";

	/// <summary>The outcome of resolving whether a socket should open.</summary>
	/// <param name="Allowed">Whether the socket may open.</param>
	/// <param name="SocketPath">Where it should listen, when allowed.</param>
	/// <param name="DenialReason">Why it may not, for the log, when denied.</param>
	internal readonly record struct Decision(bool Allowed, string? SocketPath, string? DenialReason)
	{
		internal static Decision No(string reason) => new(false, null, reason);
		internal static Decision Yes(string path) => new(true, path, null);
	}

	/// <summary>
	/// Resolves whether to open a diagnostics socket, prompting when the request came from the
	/// environment.
	/// </summary>
	/// <param name="optionPath">
	/// The path from <c>ConsoleWindowSystemOptions.DiagnosticsSocket</c>, or null.
	/// </param>
	/// <returns>What to do.</returns>
	/// <remarks>
	/// MUST be called before the console driver initialises. Once raw mode and the alternate
	/// screen are active, a prompt either scribbles over the UI or is swallowed entirely, and
	/// there is no second chance to ask. This is the one place in a SharpConsoleUI program
	/// where writing directly to the console is correct rather than forbidden.
	/// </remarks>
	internal static Decision Resolve(string? optionPath)
	{
		// In code, by someone who wrote it there. No prompt.
		if (!string.IsNullOrWhiteSpace(optionPath))
			return Decision.Yes(ResolvePath(optionPath!));

		var fromEnv = Environment.GetEnvironmentVariable(SocketEnvVar);
		if (string.IsNullOrWhiteSpace(fromEnv))
			return Decision.No(null!);   // nothing was asked for; not a denial worth logging

		string path = ResolvePath(fromEnv);

		// Nobody to ask. Deny - this is exactly the case a hostile variable would exploit,
		// so "nobody is watching" must mean no, never "assume yes".
		if (Console.IsInputRedirected || Console.IsOutputRedirected)
		{
			return Decision.No(
				$"diagnostics socket requested via {SocketEnvVar}, but stdin is not interactive " +
				"so consent cannot be obtained; socket NOT opened. To enable it without a " +
				"prompt, set ConsoleWindowSystemOptions.DiagnosticsSocket in code.");
		}

		return Prompt(path)
			? Decision.Yes(path)
			: Decision.No("diagnostics socket declined by the user at the consent prompt.");
	}

	/// <summary>
	/// Asks the user, on the plain console, before anything has been painted.
	/// </summary>
	private static bool Prompt(string path)
	{
		// Leads with READ and ends with CONTROL because that is the order of escalation, and
		// the reader has to reach the second before the question. "Allow diagnostics?" would
		// be technically true and practically useless.
		Console.WriteLine();
		Console.WriteLine("  SharpConsoleUI diagnostics");
		Console.WriteLine();
		Console.WriteLine($"  This program was started with {SocketEnvVar} set.");
		Console.WriteLine();
		Console.WriteLine("  It will open a local socket. For as long as this program runs,");
		Console.WriteLine("  another program on this machine can:");
		Console.WriteLine();
		Console.WriteLine("    - READ what this application displays, plus its window layout,");
		Console.WriteLine("      focus state and render metrics");
		Console.WriteLine("    - CONTROL it: press keys, click, resize, switch windows and");
		Console.WriteLine("      invoke actions, exactly as if you were doing it yourself");
		Console.WriteLine();
		Console.WriteLine("  Allow this only if you started a debugger yourself.");
		Console.WriteLine();
		Console.WriteLine($"    socket:  {path}   (owner-only)");
		Console.WriteLine();
		Console.Write("  Allow?  [y/N] ");

		string? answer;
		try
		{
			answer = Console.ReadLine();
		}
		catch (IOException)
		{
			// stdin vanished between the redirect check and the read.
			Console.WriteLine();
			return false;
		}

		Console.WriteLine();

		// Bare Enter is No. Someone who really started a debugger types 'y' without
		// resentment; someone who inherited a stray variable gets the safe outcome from the
		// key they were going to press anyway.
		return answer != null
			&& (answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)
			 || answer.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Turns the configured value into a socket path. "1" or "true" means "pick one for me",
	/// which lands in the user's runtime directory alongside the pid.
	/// </summary>
	private static string ResolvePath(string configured)
	{
		string value = configured.Trim();

		if (value is "1" or "true" or "TRUE" or "True")
		{
			string runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
				?? Path.GetTempPath();
			return Path.Combine(runtime,
				$"sharpconsoleui-{Environment.ProcessId}.sock");
		}

		return value;
	}
}
