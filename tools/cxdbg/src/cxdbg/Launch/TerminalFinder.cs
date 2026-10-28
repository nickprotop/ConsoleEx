// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cxdbg.Launch;

/// <summary>
/// Finds a terminal emulator to give a terminal-driven application.
/// </summary>
/// <remarks>
/// <para>
/// A headless app needs no terminal: it paints into a buffer cxdbg can read
/// over the socket. A terminal-driven one needs a real terminal, and cannot
/// share cxdbg's - two programs drawing on one screen produce neither.
/// </para>
/// <para>
/// The order is schost's, which was arrived at by using them: the modern
/// emulators first, xterm last because it is the one that is always there.
/// Only the finding is borrowed, not schost's argument-building - cxdbg wants
/// a window with the app in it, not a configured geometry.
/// </para>
/// </remarks>
internal static class TerminalFinder
{
	/// <summary>Emulators in order of preference.</summary>
	/// <summary>The variable an application reads its socket path from.</summary>
	private const string SocketVariable = "SHARPCONSOLEUI_DIAGNOSTICS_SOCKET";

	/// <summary>The program that sets a variable for one command.</summary>
	/// <remarks>
	/// On Windows there is no env(1); cmd's own `set X=Y &amp;&amp; app` is the
	/// equivalent, and wt.exe takes a command line rather than an argv.
	/// </remarks>
	private static string EnvProgram =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/usr/bin/env";

	private static readonly string[] LinuxOrder =
	[
		"ghostty", "kitty", "alacritty", "wezterm", "foot",
		"gnome-terminal", "konsole", "xfce4-terminal", "xterm",
	];

	/// <summary>The first emulator on this machine, or null when there is none.</summary>
	/// <remarks>
	/// Null is an ordinary outcome, not a failure: over SSH, in a container, in
	/// CI there is no emulator and the honest answer is the line to paste.
	/// </remarks>
	internal static string? Find()
	{
		// An explicit choice wins. "none" is a real answer a person gives over
		// SSH or in a container: do not open a window, tell me what to paste.
		string? chosen = Environment.GetEnvironmentVariable("CXDBG_TERMINAL");
		if (!string.IsNullOrEmpty(chosen))
			return chosen.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : chosen;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return OnPath("wt.exe") ?? OnPath("conhost.exe");

		// No macOS path. schost has none either, and inventing one that opens
		// Terminal.app through osascript would be a guess nobody has run.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			return null;

		// No display, no window. On Linux a terminal emulator needs an X or
		// Wayland session, and the launchers below are on PATH whether or not
		// one exists - xdg-terminal-exec is installed by the desktop package,
		// not by the running session. Looking only at PATH would make `run` try
		// to open a window over SSH and in CI, where the honest answer is the
		// line to paste.
		if (Environment.GetEnvironmentVariable("DISPLAY") is not { Length: > 0 }
			&& Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not { Length: > 0 })
		{
			return null;
		}

		// The desktop's own answer, before any list of ours. xdg-terminal-exec
		// is the freedesktop Default Terminal Specification: it runs a command
		// in the terminal the USER chose, which on a GNOME desktop is
		// gnome-terminal and on someone else's is whatever they set. A
		// hardcoded preference order cannot know that, and ours got it wrong -
		// it opened kitty on a GNOME session because kitty sorted earlier.
		if (OnPath("xdg-terminal-exec") != null) return "xdg-terminal-exec";

		// Debian and Ubuntu answer the same question through alternatives.
		if (File.Exists("/etc/alternatives/x-terminal-emulator"))
			return "x-terminal-emulator";

		// Only then a list, for a desktop that answers neither. First one
		// found wins, and the order is schost's.
		foreach (string name in LinuxOrder)
		{
			if (OnPath(name) != null) return name;
		}

		return null;
	}

	/// <summary>
	/// Builds the command that opens <paramref name="app"/> in <paramref name="terminal"/>.
	/// </summary>
	/// <param name="terminal">An emulator name from <see cref="Find"/>.</param>
	/// <param name="app">The application to run.</param>
	/// <param name="appArgs">Its own arguments.</param>
	/// <param name="socket">
	/// The socket path, carried in the command rather than the environment.
	/// </param>
	/// <returns>The emulator's arguments, app last.</returns>
	/// <remarks>
	/// The socket travels as <c>env NAME=value app</c> because a terminal
	/// emulator started by the DESKTOP does not necessarily inherit the
	/// environment of whoever asked for it. On a GNOME session
	/// xdg-terminal-exec is activated over D-Bus and the variable arrived
	/// empty, so the application never asked for consent, never opened a
	/// socket, and `run` waited out its timeout with nothing to say. Putting
	/// it in the argv removes the assumption entirely.
	/// </remarks>
	internal static List<string> ArgsFor(
		string terminal, string app, IReadOnlyList<string> appArgs, string socket)
	{
		var args = new List<string>();

		// Every one of these ends with "and here is the command", but they
		// disagree about the word for it. The app and its arguments go last
		// either way, unquoted, because each is passed as its own argv entry.
		//
		// These forms are schost's, which has the same table (with geometry and
		// font besides, which cxdbg does not want - schost owns how an app
		// looks, cxdbg only needs a window to exist). A tenth emulator has to
		// be added in both: tools/schost/src/schost/Terminal/LinuxTerminal.cs.
		switch (Path.GetFileNameWithoutExtension(terminal).ToLowerInvariant())
		{
			case "xdg-terminal-exec":
				// The specification's own form: the command follows the
				// separator, so an argument of the app's is never read as one
				// of the launcher's.
				args.Add("--");
				break;

			case "x-terminal-emulator":
				// Debian's alternatives wrapper takes xterm's spelling, which
				// every alternative is required to accept.
				args.Add("-e");
				break;

			case "wezterm":
				// Not a flag but a subcommand, and then the separator.
				args.Add("start");
				args.Add("--");
				break;

			case "gnome-terminal":
			case "kitty":
				args.Add("--");
				break;

			case "foot":
			case "wt":
				// These take the command directly, with no separator at all.
				break;

			default:
				// ghostty, alacritty, konsole, xfce4-terminal, xterm, and
				// anything unknown: "-e command args" is the common spelling.
				args.Add("-e");
				break;
		}

		// env, then the variable, then the application: the app and its own
		// arguments stay the tail of the command, which is what an emulator
		// that takes "everything after -e" expects to find.
		args.Add(EnvProgram);
		args.Add($"{SocketVariable}={socket}");

		args.Add(app);
		args.AddRange(appArgs);
		return args;
	}

	/// <summary>Whether a command exists, by asking the shell that would run it.</summary>
	private static string? OnPath(string name)
	{
		try
		{
			using var p = Process.Start(new ProcessStartInfo(
				RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which", name)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			});

			if (p == null) return null;
			p.WaitForExit(5000);
			return p.ExitCode == 0 ? name : null;
		}
		catch
		{
			return null;
		}
	}
}
