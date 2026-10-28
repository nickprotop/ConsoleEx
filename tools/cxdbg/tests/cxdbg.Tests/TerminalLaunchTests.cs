// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using Cxdbg.Launch;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// Opening an application in a terminal of its own.
/// </summary>
/// <remarks>
/// The socket path reaches the application through its ENVIRONMENT, and a
/// terminal emulator started by the desktop does not necessarily inherit the
/// environment of whoever asked for it. xdg-terminal-exec on a GNOME session
/// is activated over D-Bus, so the variable arrived empty, the application
/// never asked for consent, never opened a socket, and `run` waited out its
/// timeout with nothing to say.
/// </remarks>
public class TerminalLaunchTests
{
	[Fact]
	public void TheSocketTravelsInTheCommandNotTheEnvironment()
	{
		// env NAME=value <app>, so the value is in the argv the emulator
		// passes on rather than in an environment it may not carry.
		var args = TerminalFinder.ArgsFor(
			"xdg-terminal-exec", "/bin/myapp", [], "/run/user/1000/x.sock");

		Assert.Contains("SHARPCONSOLEUI_DIAGNOSTICS_SOCKET=/run/user/1000/x.sock", args);
		Assert.Contains("/bin/myapp", args);
	}

	[Fact]
	public void TheApplicationStillComesLast()
	{
		// Whatever wrapping is needed, the app and its own arguments are the
		// tail of the command - an emulator that takes "everything after -e"
		// must not find env's arguments there instead.
		var args = TerminalFinder.ArgsFor(
			"xterm", "/bin/myapp", ["--port", "80"], "/tmp/x.sock");

		Assert.Equal(["--port", "80"], args.TakeLast(2));
		Assert.Equal("/bin/myapp", args[^3]);
	}

	[Fact]
	public void EveryEmulatorCarriesIt()
	{
		// Not only xdg-terminal-exec: any of them may be started by the
		// desktop rather than by this process.
		foreach (string terminal in new[]
			{ "xdg-terminal-exec", "x-terminal-emulator", "gnome-terminal", "kitty", "foot", "wt" })
		{
			var args = TerminalFinder.ArgsFor(terminal, "/bin/myapp", [], "/tmp/s.sock");

			Assert.Contains(args,
				a => a.Contains("SHARPCONSOLEUI_DIAGNOSTICS_SOCKET=/tmp/s.sock", StringComparison.Ordinal));
		}
	}
}
