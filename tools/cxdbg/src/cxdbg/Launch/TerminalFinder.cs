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
	/// <returns>The emulator's arguments, app last.</returns>
	internal static List<string> ArgsFor(string terminal, string app, IReadOnlyList<string> appArgs)
	{
		var args = new List<string>();

		// Every one of these ends with "and here is the command", but they
		// disagree about the word for it. The app and its arguments go last
		// either way, unquoted, because each is passed as its own argv entry.
		switch (Path.GetFileNameWithoutExtension(terminal).ToLowerInvariant())
		{
			case "gnome-terminal":
			case "xfce4-terminal":
				args.Add("--");
				break;

			case "konsole":
			case "xterm":
				args.Add("-e");
				break;

			case "wt":
				// Windows Terminal takes the command directly.
				break;

			default:
				// ghostty, kitty, alacritty, wezterm, foot: "-e command args".
				args.Add("-e");
				break;
		}

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
