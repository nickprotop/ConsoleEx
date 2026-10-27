// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// The tool reaches a real application.
/// </summary>
/// <remarks>
/// Runs the built binary rather than calling a command class. The deliverable
/// is a tool that runs; a test that calls a class proves the class.
/// </remarks>
public class PingTests
{
	[Fact]
	public void ACxdbgPingAgainstALiveAppSucceeds()
	{
		using var app = new LiveAppFixture();

		var (exitCode, output) = Cxdbg.Run("ping", "--socket", app.SocketPath);

		Assert.Equal(0, exitCode);

		// `ping` is a protocol verb like any other now, so it answers with the
		// protocol's own reply rather than a sentence of cxdbg's.
		Assert.Contains("pong", output, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void BareCxdbgPrintsHelpRatherThanGuessing()
	{
		// A debugger that silently connected to whatever socket it found would be
		// a surprise. `cxdbg attach` with no path is how to ask for the obvious
		// one; bare cxdbg says what it can do.
		var (exitCode, output) = Cxdbg.Run();

		Assert.Equal(0, exitCode);
		Assert.Contains("ping", output, StringComparison.OrdinalIgnoreCase);
	}
}

/// <summary>Runs the built cxdbg binary and captures what it said.</summary>
internal static class Cxdbg
{
	internal static (int ExitCode, string Output) Run(params string[] args)
	{
		var psi = new ProcessStartInfo(BinaryPath())
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var a in args) psi.ArgumentList.Add(a);

		using var p = Process.Start(psi)!;
		string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
		p.WaitForExit(30_000);
		return (p.ExitCode, output);
	}

	private static string BinaryPath()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Examples")))
			dir = dir.Parent;

		string path = Path.Combine(dir!.FullName,
			"tools", "cxdbg", "src", "cxdbg", "bin", "Debug", "net10.0", "cxdbg");

		if (!File.Exists(path))
			throw new InvalidOperationException($"cxdbg is not built at {path}");

		return path;
	}
}
