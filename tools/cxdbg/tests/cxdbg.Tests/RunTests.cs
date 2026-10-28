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

	private static string DemoApp() =>
		LiveAppFixture.BuiltBinary("DemoApp", "Examples", "DemoApp", "DemoApp");

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
		// --no-terminal, because these tests want the app STARTED. Without it
		// cxdbg asks where to run it, and with a closed stdin that means the
		// hand-off: a socket printed and nothing launched. The flag is how a
		// caller says "as your child" - nothing is inferred from the app's own
		// arguments below.
		var args = new List<string> { "run", DemoApp(), "--no-terminal", "--" };
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
	public void RunWithoutAttachStillJustPrintsTheSocket()
	{
		// The flag is an addition, not a change: run on its own does what it
		// always did, and says where the application is.
		var (exit, output, _) = Start("--headless", "--size", "100x30");

		Assert.Equal(0, exit);
		Assert.Contains("socket", output, StringComparison.OrdinalIgnoreCase);
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
	[Fact]
	public void RunWithAttachOpensThePrompt()
	{
		// Launching an application to debug it and then debugging it is one
		// intent. --attach says so, and run stays what it was without it: the
		// flag asks for the extra thing rather than the plain case needing a
		// flag to opt out of it.
		var (exit, output) = TypeIntoRun("ping", "exit");

		Assert.Equal(0, exit);

		// It got to the prompt, and the prompt reached the application it had
		// just started - which is the whole claim of the flag.
		Assert.Contains("cxdbg>", output, StringComparison.Ordinal);
		Assert.Contains("alive", output, StringComparison.Ordinal);
	}

	[Fact]
	public void TheRunPromptOffersToStopWhatItStarted()
	{
		// The prompt knows the pid from the socket's greeting, so on the way
		// out it can name the application it is offering to stop. Standard
		// input is a pipe here, so nothing is asked and nothing is killed -
		// the hint says what was left behind instead.
		var (exit, output) = TypeIntoRun("exit");

		Assert.Equal(0, exit);
		Assert.Matches(@"pid \d+ is still running", output);
	}

	[Fact]
	public void CxdbgsOwnFlagsAfterTheSeparatorAreRefused()
	{
		// `run <app> -- --attach` reads like it should work, and silently does
		// not: everything after -- belongs to the application, so cxdbg never
		// sees the flag. What you get is a plausible successful run that did
		// the wrong thing - no prompt, and the app handed an argument it does
		// not understand. A refusal naming the fix beats that.
		var (exit, output) = Run(DemoApp(), "--", "--attach");

		Assert.Equal(3, exit);
		Assert.Contains("--attach", output, StringComparison.Ordinal);
		Assert.DoesNotContain("started pid", output, StringComparison.Ordinal);
	}

	[Fact]
	public void TheRefusalSaysWhereTheFlagGoes()
	{
		// A refusal that only says "no" leaves you to guess. This one shows the
		// line to type.
		var (_, output) = Run(DemoApp(), "--", "--no-terminal");

		Assert.Contains("before", output, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("--no-terminal --", output, StringComparison.Ordinal);
	}

	[Fact]
	public void AnAppsOwnFlagsAfterTheSeparatorArePassedThrough()
	{
		// The refusal is for cxdbg's OWN three flags, by name. Everything else
		// is the application's business and goes through untouched - that is
		// what the separator is for.
		var (exit, output) = Run(
			DemoApp(), "--no-terminal", "--", "--headless", "--size", "80x24");

		Assert.Equal(0, exit);
		Assert.Contains("started pid", output, StringComparison.Ordinal);

		StopWhateverStarted(output);
	}

	[Fact]
	public void NoTestEverOpensATerminalWindow()
	{
		// This suite ran kitty windows onto a real desktop once. `run` opens a
		// terminal for an application that wants a screen, and --attach takes
		// that branch without asking - so a test that forgets the guard spawns
		// a window on whoever is running the tests.
		//
		// Every helper that starts cxdbg sets CXDBG_TERMINAL=none. This asserts
		// the guard is actually in force, rather than trusting that each new
		// test remembers: with no emulator, `run` hands off instead of opening
		// anything, and says so.
		var (exit, output) = Run(DemoApp(), "--attach", "--", "--headless");

		Assert.Equal(0, exit);
		Assert.Contains("no terminal emulator here", output, StringComparison.Ordinal);
		Assert.DoesNotContain("started pid", output, StringComparison.Ordinal);
	}

	[Fact]
	public void PipedInputIsNotAskedAndNothingIsStarted()
	{
		// Nobody at the keyboard: a question would block a scripted run, and
		// starting something unasked would be worse. cxdbg prints the socket it
		// named and the two lines to run it, and starts nothing.
		var (exit, output) = Run("run", DemoApp(), "--", "--headless", "--size", "80x24");

		Assert.Equal(0, exit);
		Assert.Contains("SHARPCONSOLEUI_DIAGNOSTICS_SOCKET=", output, StringComparison.Ordinal);
		Assert.DoesNotContain("started pid", output, StringComparison.Ordinal);
	}

	[Fact]
	public void TheSocketInTheLinesIsTheOneCxdbgNamed()
	{
		// The whole point of naming it first: the path printed is the path that
		// works, so `cxdbg show tree --socket <that>` is a line you can paste
		// after starting the app yourself.
		var (_, output) = Run("run", DemoApp(), "--", "--headless");

		var exported = System.Text.RegularExpressions.Regex.Match(
			output, @"SHARPCONSOLEUI_DIAGNOSTICS_SOCKET=(\S+)");
		var asked = System.Text.RegularExpressions.Regex.Match(
			output, @"--socket (\S+)");

		Assert.True(exported.Success, $"no socket to export in:\n{output}");
		Assert.True(asked.Success, $"no socket to ask on in:\n{output}");
		Assert.Equal(exported.Groups[1].Value, asked.Groups[1].Value);
	}

	[Fact]
	public void NoTerminalRunsItHereWithoutAsking()
	{
		// The flag takes the choice out of question, which is what makes cxdbg
		// scriptable: --no-terminal is "run it as my child", and it does.
		var (exit, output) = Run(
			"run", DemoApp(), "--no-terminal", "--", "--headless", "--size", "80x24");

		Assert.Equal(0, exit);
		Assert.Contains("started pid", output, StringComparison.Ordinal);

		StopWhateverStarted(output);
	}

	[Fact]
	public void TheAppsOwnArgumentsDecideNothing()
	{
		// cxdbg reads none of them. Deciding by looking for "--headless" in
		// somebody else's command line is a guess about a CLI cxdbg does not
		// own: an app spelling it --no-ui or -H would get the wrong answer, and
		// one whose --headless means something else would too.
		//
		// Same app, same arguments, both times. Only cxdbg's own flag differs,
		// and only it changes the outcome.
		var (_, asked) = Run(DemoApp(), "--", "--headless");
		var (_, told) = Run(DemoApp(), "--no-terminal", "--", "--headless");

		Assert.DoesNotContain("started pid", asked, StringComparison.Ordinal);
		Assert.Contains("started pid", told, StringComparison.Ordinal);

		StopWhateverStarted(told);
	}

	/// <summary>Runs cxdbg with a closed stdin, and returns what it said.</summary>
	/// <remarks>
	/// Collected as it arrives, never ReadToEnd: an app started as a child of
	/// cxdbg inherits these pipes, so reading to end waits for the application
	/// to exit - and `run` leaves it running by design.
	/// </remarks>
	private static (int Exit, string Output) Run(params string[] args)
	{
		var psi = new System.Diagnostics.ProcessStartInfo(Cxdbg.BinaryPathForTests())
		{
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		Cxdbg.NoTerminals(psi);

		if (args[0] != "run") psi.ArgumentList.Add("run");
		foreach (string a in args) psi.ArgumentList.Add(a);

		using var cxdbg = System.Diagnostics.Process.Start(psi)!;

		var collected = new System.Text.StringBuilder();
		cxdbg.OutputDataReceived += (_, e) => { if (e.Data != null) lock (collected) collected.AppendLine(e.Data); };
		cxdbg.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (collected) collected.AppendLine(e.Data); };
		cxdbg.BeginOutputReadLine();
		cxdbg.BeginErrorReadLine();

		cxdbg.StandardInput.Close();
		cxdbg.WaitForExit(60_000);

		lock (collected) return (cxdbg.ExitCode, collected.ToString());
	}

	/// <summary>Stops an application `run` left behind, as it is meant to.</summary>
	private static void StopWhateverStarted(string output)
	{
		var started = System.Text.RegularExpressions.Regex.Match(output, @"started pid (\d+)");
		if (!started.Success) return;

		try
		{
			System.Diagnostics.Process.GetProcessById(int.Parse(started.Groups[1].Value))
				.Kill(entireProcessTree: true);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { }
	}

	/// <summary>
	/// Runs `run --attach`, types the lines at its prompt, and returns what it said.
	/// </summary>
	/// <remarks>
	/// The application is left running by this, so the caller gets its pid from
	/// the output and stops it: `run` does not supervise what it starts.
	/// </remarks>
	private static (int Exit, string Output) TypeIntoRun(params string[] lines)
	{
		var psi = new System.Diagnostics.ProcessStartInfo(Cxdbg.BinaryPathForTests())
		{
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		Cxdbg.NoTerminals(psi);

		foreach (string a in new[]
		{
			// --no-terminal, because a test must never open a window: the app
			// runs as a child of cxdbg and the prompt opens on this pipe.
			"run", DemoApp(), "--attach", "--no-terminal", "--", "--headless", "--size", "100x30",
		})
		{
			psi.ArgumentList.Add(a);
		}

		using var cxdbg = System.Diagnostics.Process.Start(psi)!;

		// Collected as it arrives, never ReadToEnd: a headless application runs
		// as a child of cxdbg and INHERITS these pipes, so reading to end waits
		// for the application to exit - and the whole point here is that it
		// does not. That hangs the test rather than failing it.
		var collected = new System.Text.StringBuilder();
		cxdbg.OutputDataReceived += (_, e) => { if (e.Data != null) lock (collected) collected.AppendLine(e.Data); };
		cxdbg.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (collected) collected.AppendLine(e.Data); };
		cxdbg.BeginOutputReadLine();
		cxdbg.BeginErrorReadLine();

		foreach (string line in lines) cxdbg.StandardInput.WriteLine(line);
		cxdbg.StandardInput.Close();

		cxdbg.WaitForExit(60_000);

		string output;
		lock (collected) output = collected.ToString();

		// Whatever it started is still going, by design. Stop it rather than
		// leaving a headless application behind for the rest of the suite.
		var started = System.Text.RegularExpressions.Regex.Match(output, @"started pid (\d+)");
		if (started.Success)
		{
			try
			{
				System.Diagnostics.Process.GetProcessById(int.Parse(started.Groups[1].Value))
					.Kill(entireProcessTree: true);
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { }
		}

		return (cxdbg.ExitCode, output);
	}


}
