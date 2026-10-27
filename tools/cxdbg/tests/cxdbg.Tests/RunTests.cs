// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// Starting an application under cxdbg, rather than attaching to one.
/// </summary>
/// <remarks>
/// The workflow this exists for: set the variable, start the app, find its
/// socket, attach - four steps a person should not have to do by hand every
/// time. `run` does them.
/// </remarks>
public class RunTests : IDisposable
{
	private readonly List<string> _sockets = new();

	private static string DemoApp()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Examples")))
			dir = dir.Parent;

		return Path.Combine(dir!.FullName,
			"Examples", "DemoApp", "bin", "Debug", "net10.0", "DemoApp");
	}

	/// <summary>
	/// Starts an app and records its socket for cleanup, before asserting anything.
	/// </summary>
	/// <remarks>
	/// Recording comes FIRST, deliberately. An assert that fires before the
	/// socket is written down leaves a headless app running with nothing left
	/// that knows how to reach it - and these apps run until killed.
	/// </remarks>
	private (int Exit, string Output, string Socket) Start(params string[] appArgs)
	{
		var args = new List<string> { "run", DemoApp(), "--" };
		args.AddRange(appArgs);

		var (exit, output) = Cxdbg.Run(args.ToArray());

		var m = Regex.Match(output, @"(/\S+\.sock)");
		if (m.Success) _sockets.Add(m.Groups[1].Value);

		Assert.True(m.Success, $"run did not report a socket path (exit {exit}):\n{output}");
		return (exit, output, m.Groups[1].Value);
	}

	/// <summary>Records a socket from output cxdbg already produced.</summary>
	private string SocketFrom(string output)
	{
		var m = Regex.Match(output, @"(/\S+\.sock)");
		if (m.Success) _sockets.Add(m.Groups[1].Value);

		Assert.True(m.Success, $"run did not report a socket path:\n{output}");
		return m.Groups[1].Value;
	}

	[Fact]
	public void RunStartsAHeadlessAppAndReportsIt()
	{
		var (exit, _, socket) = Start("--headless", "--size", "100x30");

		Assert.Equal(0, exit);

		using var client = Protocol.DiagnosticsClient.Connect(socket);
		Assert.True(client.Pid > 0, "run should reach an app that reports its pid");
	}

	[Fact]
	public void ArgumentsAfterDashDashReachTheApp()
	{
		// Only a headless app accepts a resize. If --headless had not reached the
		// child, this is where it shows: the server refuses with wrong_driver.
		var (exit, _, socket) = Start("--headless", "--size", "100x30");

		Assert.Equal(0, exit);

		var (resizeExit, _) = Cxdbg.Run("send", "resize", "80", "24", "--socket", socket);
		Assert.Equal(0, resizeExit);

		using var client = Protocol.DiagnosticsClient.Connect(socket);
		Assert.Equal(80, client.Send("size")["w"]!.GetValue<int>());
	}

	[Fact]
	public void TheAppKeepsRunningAfterCxdbgExits()
	{
		// The anti-goal: cxdbg is a debugger, not a supervisor. Killing the app
		// when it exits would make every session end by restarting what you were
		// looking at. There is no flag for this - it is simply what run does.
		var (exit, _, socket) = Start("--headless", "--size", "100x30");

		Assert.Equal(0, exit);

		// cxdbg has exited by now - Cxdbg.Run waited for it.
		using var client = Protocol.DiagnosticsClient.Connect(socket);
		Assert.Contains("pong", client.Send("ping").ToJsonString(), StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void RunSaysWhatItStartedAndHowToReachIt()
	{
		var (exit, output, _) = Start("--headless", "--size", "100x30");

		Assert.Equal(0, exit);

		// The line a person needs to carry on by hand, because a tool that
		// starts something and says nothing leaves them hunting for it.
		Assert.Contains("--socket", output);
	}

	[Fact]
	public void ReRunStartsTheSameCommandAgain()
	{
		// A restarted app is a DIFFERENT process at a different socket, and
		// nothing links it to the old one except the command that launched it.
		// So that command is what gets remembered - watching a directory for a
		// new socket and guessing which one is "the same app" would be a
		// heuristic that is wrong sometimes, and wrong silently.
		var (exit, _, first) = Start("--headless", "--size", "100x30");

		Assert.Equal(0, exit);

		var (againExit, againOutput) = Cxdbg.Run("run", "--again");

		Assert.Equal(0, againExit);
		string second = SocketFrom(againOutput);

		// A new process, not the old one handed back.
		Assert.NotEqual(first, second);

		// And it really is the same command: --headless reached it, which only
		// a headless app will accept a resize for.
		var (resizeExit, _) = Cxdbg.Run("send", "resize", "70", "20", "--socket", second);
		Assert.Equal(0, resizeExit);
	}

	[Fact]
	public void ReRunWithNothingRememberedSaysSo()
	{
		string empty = Path.Combine(Path.GetTempPath(), $"cxdbg-state-{Guid.NewGuid():N}");

		var (exit, output) = Cxdbg.RunIn(empty, "run", "--again");

		Assert.Equal(3, exit);
		Assert.Contains("cxdbg run", output);
	}

	[Fact]
	public void NoEmulatorPrintsTheLinesToPaste()
	{
		// Over SSH, in a container, in CI there is no emulator, and a terminal
		// app needs a terminal cxdbg cannot conjure. Refusing would be unhelpful
		// and guessing worse, so it says exactly what to type.

		var (exit, output) = Cxdbg.RunWithEnv(
			new Dictionary<string, string> { ["CXDBG_TERMINAL"] = "none" },
			"run", DemoApp());

		Assert.Equal(0, exit);
		Assert.Contains("SHARPCONSOLEUI_DIAGNOSTICS_SOCKET=", output);
		Assert.Contains("DemoApp", output);
		Assert.Contains("--socket", output);
	}

	[Fact]
	public void RunRefusesAnApplicationThatIsNotThere()
	{
		var (exit, output) = Cxdbg.Run("run", "/nope/not-an-app");

		// 3, not 1: nothing was started, so no application refused anything.
		Assert.Equal(3, exit);
		Assert.Contains("not-an-app", output);
	}

	/// <summary>
	/// Kills the apps these tests started, by the pid the socket reported.
	/// </summary>
	/// <remarks>
	/// There is no shutdown verb, by design: a debugger that could kill the app
	/// it attached to would be a worse thing than a leaked test process. So the
	/// cleanup is the test's own job, through the pid in the greeting - which is
	/// also what TheAppKeepsRunningAfterCxdbgExits is proving, so by the time
	/// Dispose runs the app is alive and the test knows its pid.
	/// </remarks>
	public void Dispose()
	{
		foreach (string socket in _sockets)
		{
			try
			{
				int pid;
				using (var client = Protocol.DiagnosticsClient.Connect(socket))
					pid = client.Pid;

				var app = System.Diagnostics.Process.GetProcessById(pid);
				app.Kill(entireProcessTree: true);

				// Wait for it: these apps run until killed, and a Dispose that
				// returns while the process is still dying leaves the next test
				// counting sockets that are about to vanish.
				app.WaitForExit(5000);
			}
			catch { /* already gone */ }

			try { if (File.Exists(socket)) File.Delete(socket); } catch { /* best effort */ }
		}
	}
}
