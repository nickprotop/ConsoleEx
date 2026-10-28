// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ScHost.Models;
using ScHost.Terminal;
using Xunit;

namespace ScHost.Tests;

/// <summary>
/// Choosing a terminal to open an application in.
/// </summary>
/// <remarks>
/// Two questions that look like one: can a window be opened RIGHT NOW, and
/// which terminal should a .desktop file name for LATER. Answering the second
/// with the first wrote xterm into a permanent file whenever packaging
/// happened on a build machine.
/// </remarks>
public class TerminalDetectionTests
{
	/// <summary>Runs an action with the display variables cleared.</summary>
	private static T WithoutADesktop<T>(Func<T> f)
	{
		string? x = Environment.GetEnvironmentVariable("DISPLAY");
		string? w = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

		Environment.SetEnvironmentVariable("DISPLAY", null);
		Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);

		try { return f(); }
		finally
		{
			Environment.SetEnvironmentVariable("DISPLAY", x);
			Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", w);
		}
	}

	[Fact]
	public void NoDesktopMeansNoTerminalToOpen()
	{
		// Over SSH and in CI the emulators are still on PATH - a package
		// installs them, a session does not - so looking only at PATH made
		// schost announce "Launched in kitty" while the emulator exited with
		// "the DISPLAY environment variable is missing" and the app never ran.
		Assert.Null(WithoutADesktop(() => LinuxTerminal.DetectTerminal()));
	}

	[Fact]
	public void ADesktopFileStillNamesARealTerminalWithoutADesktop()
	{
		// It is written now and run later, possibly on a machine that has one.
		// Asking "is there a display" at packaging time bakes the xterm
		// fallback into a file that outlives the question - so this drives
		// CreateDesktopFile itself and reads what it wrote.
		string? installed = LinuxTerminal.PickTerminal();
		if (installed == null || installed == "xterm") return;   // nothing to tell apart

		string dir = Path.Combine(Path.GetTempPath(), $"schost-{Guid.NewGuid():N}");
		Directory.CreateDirectory(dir);

		try
		{
			string path = WithoutADesktop(() =>
				LinuxTerminal.CreateDesktopFile(new SchostConfig(), "/bin/myapp", dir));

			string written = File.ReadAllText(path);

			Assert.Contains($"Exec={installed}", written, StringComparison.Ordinal);
			Assert.DoesNotContain("Exec=xterm", written, StringComparison.Ordinal);
		}
		finally
		{
			try { Directory.Delete(dir, recursive: true); } catch { }
		}
	}

	[Fact]
	public void AConfiguredWindowNeedsAnEmulatorWhoseFlagsAreKnown()
	{
		// The default-terminal launchers take a command and no geometry, so a
		// configured size would be silently dropped by using one. Styling the
		// window is schost's whole job.
		var styled = new SchostConfig { Columns = 100, Rows = 30 };

		Assert.NotEqual("xdg-terminal-exec", LinuxTerminal.PickTerminal(styled));
	}

	[Fact]
	public void AnUnstyledWindowHonoursTheUsersOwnChoice()
	{
		// With nothing to impose, the terminal the USER picked beats any
		// preference order of ours - which opened kitty on a GNOME session
		// because kitty sorted earlier.
		if (!File.Exists("/usr/bin/xdg-terminal-exec")
			&& !File.Exists("/usr/local/bin/xdg-terminal-exec"))
		{
			return;   // the specification's launcher is not installed here
		}

		Assert.Equal("xdg-terminal-exec", LinuxTerminal.PickTerminal(new SchostConfig()));
	}
}
