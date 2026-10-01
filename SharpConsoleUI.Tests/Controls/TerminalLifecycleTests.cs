// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using SharpConsoleUI.Controls.Terminal;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Reported from cwt-console against 2.6.14: killing a terminal left the child running, the
/// <see cref="TerminalControl.ProcessExited"/> event never fired, and <c>Dispose</c> froze the UI.
/// All three come from one OS fact — on Linux, closing the PTY master does NOT wake a thread
/// already blocked reading it, so the read loop that raises the event never returned.
/// </summary>
/// <remarks>
/// These drive a REAL PTY with a REAL long-running child, which is the only way to reproduce the
/// bug: a child that exits by itself takes the happy path and never exercises the stall. See
/// <see cref="PtyShimHost"/> for why a stand-in shim host is needed to get one.
/// </remarks>
[Collection("TerminalLifecycle")]
public class TerminalLifecycleTests : IDisposable
{
	/// <summary>A child that will not exit on its own, so teardown has to end it.</summary>
	private const string LongRunningCommand = "echo hello; sleep 30";

	/// <summary>Exit status for a process killed by SIGHUP: 128 + signal number.</summary>
	private const int ExitStatusSighup = 129;

	/// <summary>Exit status for a process killed by SIGKILL, if SIGHUP was ignored.</summary>
	private const int ExitStatusSigkill = 137;

	/// <summary>Generous enough that a failure means a real stall, not a slow machine.</summary>
	private const int TeardownTimeoutMs = 5000;

	/// <summary>
	/// What <c>Dispose</c> must stay under. The pre-fix code blocked for the backend's full
	/// 500 ms wait; the bar here is far below that but far above any plausible scheduling blip.
	/// </summary>
	private const int DisposeBudgetMs = 150;

	public TerminalLifecycleTests() => PtyShimHost.Install();

	public void Dispose() => PtyShimHost.Uninstall();

	private static TerminalControl StartLongRunningTerminal()
	{
		var terminal = SharpConsoleUI.Builders.Controls.Terminal("/bin/sh")
			.WithArgs("-c", LongRunningCommand)
			.Build();

		// Wait for real output, which proves the child is up and holding the PTY — the state the
		// bug needs. Without this the test could tear down a child that had not started yet.
		Assert.True(SpinUntil(() => terminal.GetTranscript().Any(l => l.Contains("hello"))),
			"test premise: the child must be running and writing to the PTY before teardown");

		return terminal;
	}

	private static bool SpinUntil(Func<bool> condition, int timeoutMs = 3000)
		=> SpinWait.SpinUntil(condition, timeoutMs);

	private static bool IsAlive(int pid) => Directory.Exists($"/proc/{pid}");

	#region The reported bug

	/// <summary>
	/// The heart of the report: a disposed terminal's child must actually die. It used to survive,
	/// because nothing signalled it and closing the fd alone does not end a process.
	/// </summary>
	[LinuxPtyFact]
	public void DisposingATerminal_EndsTheChildProcess()
	{
		var terminal = StartLongRunningTerminal();
		int pid = terminal.ProcessId;
		Assert.True(IsAlive(pid), "test premise: the child must be alive before disposal");

		terminal.Dispose();
		terminal.WaitForExit(TeardownTimeoutMs);

		Assert.True(SpinUntil(() => !IsAlive(pid)), $"child process {pid} survived disposal");
	}

	/// <summary>
	/// <c>ProcessExited</c> is raised from the read loop, so a read that never wakes means an event
	/// that never fires. This is the symptom the reporter saw as "the terminal just hangs".
	/// </summary>
	[LinuxPtyFact]
	public void DisposingATerminal_RaisesProcessExited()
	{
		var terminal = StartLongRunningTerminal();
		using var fired = new ManualResetEventSlim();
		terminal.ProcessExited += (_, _) => fired.Set();

		terminal.Dispose();

		Assert.True(fired.Wait(TeardownTimeoutMs), "ProcessExited never fired after Dispose");
	}

	/// <summary>
	/// The event fires exactly once, however teardown was reached — a second Dispose must not
	/// produce a second notification.
	/// </summary>
	[LinuxPtyFact]
	public void ProcessExited_FiresExactlyOnce()
	{
		var terminal = StartLongRunningTerminal();
		int count = 0;
		terminal.ProcessExited += (_, _) => Interlocked.Increment(ref count);

		terminal.Dispose();
		terminal.Dispose();
		terminal.WaitForExit(TeardownTimeoutMs);

		Assert.True(SpinUntil(() => Volatile.Read(ref count) > 0), "ProcessExited never fired");
		Thread.Sleep(200);  // leave room for a stray second raise to show up
		Assert.Equal(1, Volatile.Read(ref count));
	}

	/// <summary>
	/// Hosts dispose terminals from window-close and key handlers, i.e. on the UI thread. Waiting
	/// for the child there froze the whole application, so Dispose now signals and returns while
	/// the read thread does the blocking reap.
	/// </summary>
	[LinuxPtyFact]
	public void Dispose_ReturnsWithoutWaitingForTheChild()
	{
		var terminal = StartLongRunningTerminal();

		var stopwatch = Stopwatch.StartNew();
		terminal.Dispose();
		stopwatch.Stop();

		terminal.WaitForExit(TeardownTimeoutMs);
		Assert.True(stopwatch.ElapsedMilliseconds < DisposeBudgetMs,
			$"Dispose blocked for {stopwatch.ElapsedMilliseconds}ms, budget is {DisposeBudgetMs}ms");
	}

	/// <summary>
	/// Teardown must finish, not merely start: the read thread has to wake, reap, and exit. A
	/// failure here is the original hang, just moved off the caller's thread.
	/// </summary>
	[LinuxPtyFact]
	public void TeardownCompletes_TheReadThreadFinishes()
	{
		var terminal = StartLongRunningTerminal();

		terminal.Dispose();

		Assert.True(terminal.WaitForExit(TeardownTimeoutMs), "the PTY read thread never finished");
	}

	/// <summary>
	/// <c>ExitCode</c> is how a host learns the command's fate, and it was always null because the
	/// reap never happened. A terminal we had to kill reports the killing signal.
	/// </summary>
	[LinuxPtyFact]
	public void AKilledChild_ReportsItsExitStatus()
	{
		var terminal = StartLongRunningTerminal();

		terminal.Dispose();
		terminal.WaitForExit(TeardownTimeoutMs);

		Assert.NotNull(terminal.ExitCode);
		Assert.Contains(terminal.ExitCode!.Value, new[] { ExitStatusSighup, ExitStatusSigkill });
	}

	#endregion

	#region The whole thing, through a real window

	/// <summary>
	/// The real usage path: a terminal inside a window inside a window system, torn down by
	/// closing the window rather than by calling Dispose directly. This is what cwt-console does,
	/// and the path that has to work — <c>Window.CompleteClose</c> disposes its controls, so the
	/// fix has to hold when the call arrives from there.
	/// </summary>
	[LinuxPtyFact]
	public void ClosingTheWindow_EndsTheChildAndFreesTheUiThread()
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(100, 30),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));

		var terminal = StartLongRunningTerminal();
		int pid = terminal.ProcessId;

		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 60,
			Height = 16,
			Title = "terminal",
		};
		window.AddControl(terminal);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// Closing is what a user does, and it runs the same disposal the UI thread would.
		var stopwatch = Stopwatch.StartNew();
		system.CloseWindow(window, force: true);
		stopwatch.Stop();

		Assert.True(stopwatch.ElapsedMilliseconds < DisposeBudgetMs,
			$"closing the window blocked for {stopwatch.ElapsedMilliseconds}ms");

		Assert.True(terminal.WaitForExit(TeardownTimeoutMs), "the PTY read thread never finished");
		Assert.True(SpinUntil(() => !IsAlive(pid)), $"child process {pid} survived the window close");
	}

	#endregion

	#region A disposed terminal stays usable as a transcript

	/// <summary>
	/// Disposal tears down the PTY only. The VT machine and its buffers survive by design, so the
	/// final screen stays readable — the behaviour <c>KeepOpenOnExit</c> exists to expose. The
	/// guards added around the PTY writes must not have cost us this.
	/// </summary>
	[LinuxPtyFact]
	public void ADisposedTerminal_StillReturnsItsTranscript()
	{
		var terminal = StartLongRunningTerminal();

		terminal.Dispose();
		terminal.WaitForExit(TeardownTimeoutMs);

		Assert.Contains(terminal.GetTranscript(), line => line.Contains("hello"));
	}

	/// <summary>
	/// Typing into a dead terminal is a no-op rather than a crash or a write to a closed — and
	/// possibly recycled — descriptor.
	/// </summary>
	[LinuxPtyFact]
	public void ADisposedTerminal_SwallowsInputWithoutThrowing()
	{
		var terminal = StartLongRunningTerminal();
		terminal.Dispose();
		terminal.WaitForExit(TeardownTimeoutMs);

		var exception = Record.Exception(() =>
			terminal.ProcessKey(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false)));

		Assert.Null(exception);
	}

	[LinuxPtyFact]
	public void ADisposedTerminal_ReportsItself()
	{
		var terminal = StartLongRunningTerminal();
		Assert.False(terminal.IsDisposed);

		terminal.Dispose();

		Assert.True(terminal.IsDisposed);
	}

	#endregion
}

/// <summary>
/// Collection marker: these tests set the process-wide
/// <see cref="LinuxPtyBackend.ShimHostPath"/>, so they must not run beside each other.
/// </summary>
[CollectionDefinition("TerminalLifecycle", DisableParallelization = true)]
public class TerminalLifecycleCollection { }
