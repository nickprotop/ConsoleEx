// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Cxdbg.Launch;
using Cxdbg.Protocol;
using Spectre.Console;

namespace Cxdbg.Commands;

/// <summary>
/// Starts an application with a socket, instead of attaching to one.
/// </summary>
/// <remarks>
/// <para>
/// Everything after <c>--</c> belongs to the application:
/// <c>cxdbg run ./MyApp -- --headless --size 100x30</c>. cxdbg takes nothing
/// from that list and passes it through unchanged, so an app never has to know
/// it was started by a debugger.
/// </para>
/// <para>
/// There is no "run it again": the shell already remembers the command, and a
/// state file of cxdbg's own would be a worse version of shell history.
/// </para>
/// <para>
/// cxdbg does not supervise what it starts: when run returns, the application
/// is still going. A debugger that killed its subject on exit would make every
/// session end by restarting the thing you were looking at. With
/// <c>--attach</c> the prompt offers to stop it on the way out, since it knows
/// exactly which process it has been talking to - but declining is the default,
/// so the behaviour is unchanged for anyone who just presses return.
/// </para>
/// </remarks>
internal static class RunCommand
{
	/// <summary>Runs <c>run &lt;app&gt; [--attach] [-- app args]</c>.</summary>
	internal static int Execute(CommandLine line)
	{
		try
		{
			if (line.Rest.Count == 0)
			{
				throw CxdbgException.Usage(
					"run needs an application, e.g. `cxdbg run ./MyApp`.");
			}

			string app = line.Rest[0];
			var appArgs = line.AppArgs.ToList();

			bool attach = line.Fields.ContainsKey("attach");
			bool noTerminal = line.Fields.ContainsKey("no-terminal");
			int timeout = line.Fields.TryGetValue("timeout", out var t)
				? (int)(t?.GetValue<long>() ?? 30) : 30;

			// Before anything else: cxdbg's own flags on the wrong side of the
			// separator. Silently doing something else is the failure this
			// catches.
			Misplaced(appArgs);

			// Then: a question about where to run an application that is not
			// there is a question not worth asking, and lines to paste for a
			// missing binary are worse than a refusal.
			AppLauncher.Resolve(app);

			// Named before anything starts, so it can be printed - and asked
			// about - while there is still a choice to make.
			string socket = AppLauncher.NewSocketPath();

			string? terminal = Where(attach, noTerminal, app, appArgs, socket);

			// Said no, or there was nobody to ask: the socket and the two lines
			// are the whole answer, and nothing is started.
			if (terminal == Declined) return 0;

			var result = AppLauncher.Launch(
				app, appArgs, socket, terminal, TimeSpan.FromSeconds(timeout));

			if (!attach)
			{
				Report(result);
				return 0;
			}

			// Launching an application to debug it and then debugging it is
			// one intent. Say what was started before the screen fills, so a
			// session that cannot start still leaves the socket behind.
			Report(result);

			// The prompt makes the offer to stop the application on its own way
			// out, which is where the person actually is. Asking again here
			// would be the same question twice.
			return ShellCommand.Run(result.SocketPath);
		}
		catch (CxdbgException ex)
		{
			AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
			return ex.Code == CxdbgException.BadUsage ? 3 : 1;
		}
	}

	/// <summary>cxdbg's own flags, which do not belong to the application.</summary>
	private static readonly string[] OwnFlags = ["--attach", "--no-terminal", "--timeout"];

	/// <summary>
	/// Refuses one of cxdbg's own flags found on the application's side of the
	/// separator.
	/// </summary>
	/// <param name="appArgs">Everything after <c>--</c>.</param>
	/// <exception cref="CxdbgException">When one of them is there.</exception>
	/// <remarks>
	/// <para>
	/// <c>run ./MyApp -- --attach</c> reads like it should work and silently
	/// does not: everything after the separator belongs to the application by
	/// definition, so cxdbg never sees the flag. What follows is a plausible
	/// successful run that did the wrong thing - the question asked that
	/// <c>--attach</c> would have skipped, no prompt at the end, and an
	/// argument handed to an application that does not understand it.
	/// </para>
	/// <para>
	/// A refusal rather than a quiet reinterpretation. An application may well
	/// have its own <c>--attach</c>, and deciding that this one was meant for
	/// cxdbg would be the same guess about somebody else's CLI that reading
	/// their <c>--headless</c> used to be. Saying what happened, and what to
	/// type instead, leaves the choice where it belongs.
	/// </para>
	/// </remarks>
	private static void Misplaced(IReadOnlyList<string> appArgs)
	{
		foreach (string flag in OwnFlags)
		{
			// Both spellings: --timeout 30 and --timeout=30.
			if (!appArgs.Any(a => a == flag
				|| a.StartsWith(flag + "=", StringComparison.Ordinal)))
			{
				continue;
			}

			throw CxdbgException.Usage(
				$"{flag} after -- belongs to the application, not to cxdbg. "
				+ "Put cxdbg's own flags before the separator: "
				+ $"cxdbg run <app> {flag} -- <the app's own arguments>");
		}
	}

	/// <summary>Stands for "nothing was started", which no terminal name can.</summary>
	private const string Declined = "\u0000declined";

	/// <summary>
	/// Decides where the application runs, asking when it is a real choice.
	/// </summary>
	/// <param name="attach">Whether a prompt follows, which needs this terminal.</param>
	/// <param name="noTerminal">Whether the caller said to run it here.</param>
	/// <param name="app">The application, for the lines to paste.</param>
	/// <param name="appArgs">Its arguments, for the same.</param>
	/// <param name="socket">The socket it will be told to open.</param>
	/// <returns>
	/// An emulator to open it in, null to run it as a child of cxdbg, or
	/// <see cref="Declined"/> when nothing should start.
	/// </returns>
	/// <remarks>
	/// <para>
	/// An application wants a terminal of its own: it asks for consent on the
	/// plain console before it paints, and someone has to be able to read and
	/// answer that. Two programs drawing on one screen produce neither, so
	/// sharing cxdbg's is not an option - which leaves a choice, and a choice
	/// is better asked than guessed. Opening a window somebody did not want is
	/// the kind of surprise a debugger should not spring.
	/// </para>
	/// <para>
	/// The flags take it out of question. <c>--no-terminal</c> says run it
	/// here. <c>--attach</c> says a terminal of its own, necessarily: the
	/// prompt is about to take over this one.
	/// </para>
	/// <para>
	/// Nothing here reads the application's own arguments. Deciding by looking
	/// for <c>--headless</c> in somebody else's command line is a guess about a
	/// CLI cxdbg does not own, and it is wrong for every app that spells it
	/// differently.
	/// </para>
	/// </remarks>
	private static string? Where(
		bool attach, bool noTerminal, string app, IReadOnlyList<string> appArgs, string socket)
	{
		if (noTerminal) return null;

		string? terminal = TerminalFinder.Find();

		// cxdbg is about to fill this terminal with the prompt, so the
		// application needs the other one. No question to ask.
		if (attach)
		{
			if (terminal != null) return terminal;

			HandOff(app, appArgs, socket,
				"no terminal emulator here, and --attach needs one for the application.");

			return Declined;
		}

		// Nobody at the keyboard. A question would block a scripted run, and
		// starting something unasked would be worse, so say what to type.
		if (Console.IsInputRedirected)
		{
			HandOff(app, appArgs, socket, null);
			return Declined;
		}

		var console = AnsiConsole.Console;

		Console.WriteLine($"  socket  {socket}");
		console.WriteLine();

		if (terminal == null)
		{
			console.MarkupLine(
				"[yellow]no terminal emulator here.[/] The application can run in this "
				+ "terminal, but it will paint over what is on it.");
			console.WriteLine();
			console.Markup("start it [blue]h[/]ere, or [blue]n[/]ot at all? [dim](h/N)[/] ");

			string? here = Console.ReadLine();

			if (here != null && here.Trim().StartsWith("h", StringComparison.OrdinalIgnoreCase))
				return null;

			HandOff(app, appArgs, socket, null);
			return Declined;
		}

		console.Markup(
			$"start it in a new [blue]t[/]erminal ({terminal.EscapeMarkup()}), "
			+ "or [blue]h[/]ere? [dim](T/h)[/] ");

		string? answer = Console.ReadLine();

		return answer != null && answer.Trim().StartsWith("h", StringComparison.OrdinalIgnoreCase)
			? null
			: terminal;
	}

	/// <summary>Says what started and how to reach it.</summary>
	private static void Report(LaunchResult result)
	{
		var console = AnsiConsole.Console;

		using var client = DiagnosticsClient.Connect(result.SocketPath);

		console.MarkupLine($"[green]started[/] pid {client.Pid}");

		// Plain writes, not markup: Spectre wraps to the console width, and a
		// command line broken across two lines is one a person cannot paste.
		Console.WriteLine($"  socket  {result.SocketPath}");
		Console.WriteLine($"  ask     cxdbg show tree --socket {result.SocketPath}");
	}

	/// <summary>
	/// Prints the two lines to paste, when cxdbg is not starting anything.
	/// </summary>
	/// <param name="app">The application.</param>
	/// <param name="appArgs">Its own arguments, passed through unchanged.</param>
	/// <param name="socket">The socket named for this run.</param>
	/// <param name="because">Why cxdbg is not starting it, or null when asked.</param>
	/// <remarks>
	/// Over SSH, in a container, in CI there is no emulator and often nobody to
	/// ask. Refusing would be unhelpful and starting something unasked would be
	/// worse, so cxdbg says exactly what to type - the variable and the command
	/// - and the socket it already chose, so the path still works afterwards.
	/// </remarks>
	private static void HandOff(
		string app, IReadOnlyList<string> appArgs, string socket, string? because)
	{
		string command = string.Join(' ', new[] { app }.Concat(appArgs));

		var console = AnsiConsole.Console;

		if (because != null)
		{
			console.MarkupLine($"[yellow]{because.EscapeMarkup()}[/]");
			console.WriteLine();
		}

		console.MarkupLine("Run these two lines in a terminal of its own:");
		console.WriteLine();
		Console.WriteLine($"  export SHARPCONSOLEUI_DIAGNOSTICS_SOCKET={socket}");
		Console.WriteLine($"  {command}");
		console.WriteLine();
		console.MarkupLine("The app will ask you to allow the socket. Then, back here:");
		console.WriteLine();
		Console.WriteLine($"  cxdbg show tree --socket {socket}");
	}
}
