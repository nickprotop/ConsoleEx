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
	internal static (int ExitCode, string Output) Run(params string[] args) =>
		RunIn(null, args);

	/// <summary>Runs cxdbg with its state directory pointed somewhere else.</summary>
	/// <param name="stateDir">
	/// Where cxdbg should keep what it remembers, or null for the real place.
	/// </param>
	/// <param name="args">The command line.</param>
	/// <remarks>
	/// Set on the CHILD's environment, never this process's. Two test classes
	/// mutating process-global variables in parallel is a race this suite has
	/// already paid for once.
	/// </remarks>
	internal static (int ExitCode, string Output) RunIn(string? stateDir, params string[] args) =>
		RunWithEnv(
			stateDir == null
				? null
				: new Dictionary<string, string> { ["CXDBG_STATE_DIR"] = stateDir },
			args);

	/// <summary>Runs cxdbg with extra environment variables, set on the child only.</summary>
	/// <param name="env">Variables for this run, or null for none.</param>
	/// <param name="args">The command line.</param>
	internal static (int ExitCode, string Output) RunWithEnv(
		IReadOnlyDictionary<string, string>? env, params string[] args)
	{
		var psi = new ProcessStartInfo(BinaryPath())
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		if (env != null)
		{
			foreach (var (name, value) in env) psi.Environment[name] = value;
		}

		foreach (var a in args) psi.ArgumentList.Add(a);

		using var p = Process.Start(psi)!;

		// Collect asynchronously rather than ReadToEnd. `run` leaves an
		// application alive, and that application inherits cxdbg's stdout - so
		// reading the pipe to EOF waits for the APPLICATION to die, not for
		// cxdbg to finish. Waiting on the process and then taking what arrived
		// is what the test actually wants to know.
		var text = new System.Text.StringBuilder();
		p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (text) text.AppendLine(e.Data); };
		p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (text) text.AppendLine(e.Data); };
		p.BeginOutputReadLine();
		p.BeginErrorReadLine();

		if (!p.WaitForExit(60_000))
		{
			p.Kill(entireProcessTree: true);
			throw new TimeoutException($"cxdbg did not exit within 60s. It said:\n{text}");
		}

		lock (text) return (p.ExitCode, text.ToString());
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
