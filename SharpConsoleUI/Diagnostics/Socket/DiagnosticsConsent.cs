// -----------------------------------------------------------------------
using System.IO;
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;

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
	/// <param name="FromEnvironment"></param>
	internal readonly record struct Decision(
		bool Allowed, string? SocketPath, string? DenialReason, bool FromEnvironment = false)
	{
		internal static Decision No(string reason) => new(false, null, reason);

		/// <summary>Allowed, by someone who wrote the path in code.</summary>
		internal static Decision Yes(string path) => new(true, path, null);

		/// <summary>
		/// Allowed, from the environment variable.
		/// </summary>
		/// <remarks>
		/// Tracked separately so the caller can log that route. A socket opened
		/// without a prompt should leave a record; one configured in code is the
		/// author's own decision and needs none.
		/// </remarks>
		internal static Decision YesFromEnvironment(string path) => new(true, path, null, true);
	}

	/// <summary>
	/// Resolves whether to open a diagnostics socket, prompting when the request came from the
	/// environment.
	/// </summary>
	/// <param name="optionPath">
	/// The path from <c>ConsoleWindowSystemOptions.DiagnosticsSocket</c>, or null.
	/// </param>
	/// <param name="driver">
	/// The driver this system will run on, which decides whether there is anyone
	/// to prompt. A headless app has no terminal by design, so the usual
	/// "is stdin interactive?" test says nothing about whether a user is present.
	/// </param>
	/// <returns>What to do.</returns>
	/// <remarks>
	/// MUST be called before the console driver initialises. Once raw mode and the alternate
	/// screen are active, a prompt either scribbles over the UI or is swallowed entirely, and
	/// there is no second chance to ask. This is the one place in a SharpConsoleUI program
	/// where writing directly to the console is correct rather than forbidden.
	/// </remarks>
	internal static Decision Resolve(string? optionPath, IConsoleDriver? driver = null)
	{
		// In code, by someone who wrote it there. No prompt.
		if (!string.IsNullOrWhiteSpace(optionPath))
			return Decision.Yes(ResolvePath(optionPath!));

		var fromEnv = Environment.GetEnvironmentVariable(SocketEnvVar);
		if (string.IsNullOrWhiteSpace(fromEnv))
			return Decision.No(null!);   // nothing was asked for; not a denial worth logging

		string path;
		try
		{
			path = ResolvePath(fromEnv);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
			or ArgumentException or NotSupportedException)
		{
			// Resolve runs inside the constructor. A directory we cannot create is
			// a reason not to open a socket, never a reason to stop the app.
			return Decision.No(
				$"diagnostics socket requested via {SocketEnvVar}, but its directory "
				+ $"could not be prepared: {ex.Message}");
		}

		// A headless app has no user to ask and no screen to expose. It is headless
		// because someone wrote HeadlessConsoleDriver in code, which is the same
		// class of statement as setting DiagnosticsSocket there - a deliberate,
		// compiled-in decision that this program runs unattended. Prompting someone
		// who by construction is not present, and denying when they do not answer,
		// protects nobody; it only makes the variable silently terminal-only.
		//
		// The threat given up is narrow: an attacker who can already set your
		// environment, targeting a headless app, to read a screen nobody is looking
		// at. Someone who can set your environment can usually do worse.
		if (driver is HeadlessConsoleDriver)
			return Decision.YesFromEnvironment(path);

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
			? Decision.YesFromEnvironment(path)
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
			string? runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

			// XDG_RUNTIME_DIR is already owner-only on a proper desktop. Temp is
			// not: the socket file itself stays owner-only there, but every other
			// user can list the directory and learn this app is running and what
			// its process id is. So we make our own owner-only directory inside it
			// rather than dropping the socket in the open.
			//
			// Either way the directory is settled HERE, so a path that cannot work
			// fails now, with a reason, rather than at bind time with a socket
			// error the user has to work backwards from.
			runtime = string.IsNullOrEmpty(runtime)
				? EnsureOwnerOnlyDirectory(Path.Combine(Path.GetTempPath(), "sharpconsoleui"))
				: RequireUsableDirectory(runtime);

			return Path.Combine(runtime,
				$"sharpconsoleui-{Environment.ProcessId}.sock");
		}

		return value;
	}

	/// <summary>
	/// Checks a directory the user nominated is usable, and returns it.
	/// </summary>
	/// <param name="path">The directory from <c>XDG_RUNTIME_DIR</c>.</param>
	/// <returns>The directory.</returns>
	/// <exception cref="IOException">When it does not exist and cannot be made.</exception>
	/// <remarks>
	/// Created if missing, but NOT re-permissioned. This is the user's own
	/// runtime directory, shared with everything else that uses it, and already
	/// owner-only on a system that provides one. Changing its mode would be this
	/// library reaching outside its own business.
	/// </remarks>
	private static string RequireUsableDirectory(string path)
	{
		Directory.CreateDirectory(path);
		return path;
	}

	/// <summary>
	/// Creates a directory only this user can enter, and returns it.
	/// </summary>
	/// <param name="path">Where it should be.</param>
	/// <returns>The directory.</returns>
	/// <exception cref="IOException">When it cannot be created or secured.</exception>
	private static string EnsureOwnerOnlyDirectory(string path)
	{
		Directory.CreateDirectory(path);

		// Best effort, like the socket's own mode: a no-op on Windows, where the
		// containing directory's ACL does this work instead.
		try
		{
			File.SetUnixFileMode(path,
				UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
		catch (PlatformNotSupportedException) { /* Windows */ }
		catch (IOException) { /* filesystem without modes */ }

		return path;
	}
}
