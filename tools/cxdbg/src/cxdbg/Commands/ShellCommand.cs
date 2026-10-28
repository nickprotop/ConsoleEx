// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Cxdbg.Protocol;
using Spectre.Console;

namespace Cxdbg.Commands;

/// <summary>
/// A prompt on a socket: the protocol, without retyping the path.
/// </summary>
/// <remarks>
/// Every other verb is one question and one answer, which is right for a script
/// and tiring for a person working through a problem. This keeps the socket and
/// stays until you leave.
/// </remarks>
internal static class ShellCommand
{
	/// <summary>Opens the prompt named on the command line.</summary>
	internal static int Execute(CommandLine line)
	{
		string? socket = line.Socket ?? SocketDiscovery.Sole();

		if (string.IsNullOrWhiteSpace(socket))
		{
			AnsiConsole.MarkupLine(
				"[red]this needs a socket.[/] `cxdbg run <app>` prints one, or pass "
				+ "-s with the path of an application that is already running.");
			return 3;
		}

		return Run(socket);
	}

	/// <summary>
	/// Enters the prompt on a socket, for a caller that already has one.
	/// </summary>
	/// <param name="socket">Where the application is listening.</param>
	/// <returns>An exit code.</returns>
	/// <remarks>
	/// Exists so <c>run --attach</c> enters the same prompt this command does,
	/// rather than a second implementation that could drift from it.
	/// </remarks>
	internal static int Run(string socket)
	{
		DiagnosticsClient client;
		try
		{
			client = DiagnosticsClient.Connect(socket);
		}
		catch (CxdbgException ex)
		{
			AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
			return ex.Message.Contains("listening", StringComparison.Ordinal) ? 2 : 1;
		}

		var held = new Held();
		var editor = new LineEditor(Completion.For);

		try
		{
			ClearForSession();
			Connected(client, socket);

			while (true)
			{
				// Not Spectre's TextPrompt: it refuses piped input ("failed to
				// read input in non-interactive mode"), which would make the
				// prompt untestable and unscriptable. The editor reads keys
				// when there is a terminal and falls back to ReadLine when
				// there is a pipe.
				// Written either way, including into a pipe: a transcript
				// without it cannot be read back, because nothing says which
				// reply answered which command.
				const string prompt = "cxdbg> ";

				if (Console.IsInputRedirected) Console.Write(prompt);
				else AnsiConsole.Markup("[blue]cxdbg>[/] ");

				string? input = editor.Read(prompt);
				if (input == null) return Leaving(client);   // end of input: the same as exit

				string line = input.Trim();
				if (line.Length == 0) continue;
				if (line is "exit" or "quit") return Leaving(client);

				var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

				// cxdbg's own layer, told apart by the leading dot.
				if (DotCommands.Run(words, client, held))
				{
					Console.WriteLine();
					continue;
				}

				// `verb > file` sends the reply to a file instead of the
				// screen. Split off before parsing, so a path is never read as
				// a protocol field.
				string? into = null;
				bool append = false;

				int arrow = Array.FindIndex(words, w => w is ">" or ">>");
				if (arrow >= 0)
				{
					if (arrow + 1 >= words.Length)
					{
						AnsiConsole.MarkupLine("[red]> needs a file to write to.[/]");
						continue;
					}

					append = words[arrow] == ">>";
					into = words[arrow + 1];
					words = words.Take(arrow).ToArray();
				}

				// Move to another application without leaving the prompt. One
				// debugging session often spans two processes - a launcher and
				// what it opened - and quitting to type a path again loses
				// whatever you had in your head.
				if (words[0] == "connect")
				{
					if (words.Length < 2)
					{
						AnsiConsole.MarkupLine("[red]connect needs a socket path.[/]");
						continue;
					}

					Swap(ref client, ref socket, words[1]);
					continue;
				}

				// The prompt speaks the PROTOCOL. cxdbg's own readable verbs -
				// send, show, why - are a layer above it that lives in the CLI,
				// so typing one here would otherwise reach the application as
				// an unknown verb and get a list that does not explain why.
				// Watching is cxdbg's own loop, not a protocol verb, but unlike
				// send/show/why it makes sense at the prompt - more sense than on
				// the command line, since the connection is already open. It
				// borrows that connection rather than opening a second one, which
				// the application would refuse.
				if (words[0] == "watch")
				{
					WatchCommand.RunOn(client, words);
					Console.WriteLine();
					continue;
				}

				if (words[0] is "send" or "show" or "why")
				{
					AnsiConsole.MarkupLine(
						$"[yellow]'{words[0]}' is a cxdbg command, not a protocol verb.[/] "
						+ "This prompt speaks the protocol: try [blue]key[/], [blue]mouse[/], "
						+ "[blue]size[/], [blue]tree[/] or [blue]measure[/]. "
						+ "Type [blue]commands[/] for the full list.");
					continue;
				}

				try
				{
					var typed = CommandLineParser.Parse(words);

					Output.WarnAboutLikelyTypos(typed.Verb, typed.Fields.Keys);

					var reply = client.Send(typed.Verb, typed.Fields);

					// Kept whatever happens next: printed, held back, or
					// written out, .last can still render it another way
					// without asking the application again - which matters
					// because by then the screen has moved on.
					held.Keep(typed.Verb, reply);

					if (into != null)
					{
						Console.Write(held.Write(into, append));
						Console.WriteLine();
						continue;
					}

					string text = Output.Render(typed.Verb, reply, typed);

					// A screenshot is 75KB of base64 and takes the scrollback
					// with it. Say what it is instead, and leave it in .last.
					if (held.TooBig(text) is { } summary) text = summary;

					Console.Write(text);

					// A blank line between replies. Without it `focus` and
					// whatever follows run together and you cannot see where
					// one answer ends - which on a prompt you are reading
					// quickly is most of the readability.
					if (!text.EndsWith("\n\n", StringComparison.Ordinal)) Console.WriteLine();
				}
				catch (CxdbgException ex)
				{
					// A refusal is an answer, not the end of the session: the
					// next question may well work.
					AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
				}
			}
		}
		finally
		{
			client.Dispose();
		}
	}

	/// <summary>
	/// On the way out, offers to stop the application the prompt was talking to.
	/// </summary>
	/// <param name="client">The connection, which carries the pid.</param>
	/// <returns>The prompt's exit code, always zero: leaving is not a failure.</returns>
	/// <remarks>
	/// <para>
	/// An offer rather than a kill. cxdbg does not supervise what it attaches
	/// to, and a debugger that ended its subject on exit would make every
	/// session end by restarting the thing you were looking at. But the prompt
	/// knows exactly which process it has been talking to, and a headless app
	/// with no screen left running behind you is its own small mess.
	/// </para>
	/// <para>
	/// Declining is the default, including when there is nobody to ask: a
	/// redirected standard input is not asked at all, because reading it would
	/// block on a pipe that may never carry an answer, and a prompt that hung a
	/// scripted run would be worse than not offering. End-of-input at a real
	/// terminal is taken as no.
	/// </para>
	/// </remarks>
	private static int Leaving(DiagnosticsClient client)
	{
		int pid = client.Pid;
		if (pid <= 0) return 0;

		// Nobody at the keyboard: say what was left running rather than asking
		// a question that would block on a pipe.
		if (Console.IsInputRedirected)
		{
			Console.WriteLine($"pid {pid} is still running; stop it with: kill {pid}");
			return 0;
		}

		AnsiConsole.Markup($"stop pid {pid} as well? [dim](y/N)[/] ");

		string? answer = Console.ReadLine();
		if (answer == null || !answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
			return 0;

		try
		{
			System.Diagnostics.Process.GetProcessById(pid).Kill(entireProcessTree: true);
			AnsiConsole.MarkupLine($"[green]stopped[/] pid {pid}");
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
			or System.ComponentModel.Win32Exception)
		{
			AnsiConsole.MarkupLine(
				$"[yellow]could not stop pid {pid}:[/] {ex.Message.EscapeMarkup()}");
		}

		return 0;
	}

	/// <summary>Says which application the prompt is now talking to.</summary>
	/// <summary>
	/// Clears the screen as the prompt opens, so a session starts on a clean page.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A debugger is usually launched after a run of build output, and the first
	/// thing wanted is the application's state, not the last compiler warning.
	/// </para>
	/// <para>
	/// Scrollback is kept, not discarded: the screen is cleared and the cursor
	/// homed, but what scrolled past is still there to scroll back to. Skipped
	/// when the output is a pipe or a file, where a clear is a handful of escape
	/// bytes at the top of a transcript and nothing else.
	/// </para>
	/// </remarks>
	private static void ClearForSession()
	{
		if (Console.IsOutputRedirected) return;

		try { AnsiConsole.Write("\u001b[H\u001b[2J"); }
		catch (IOException) { /* a terminal that will not clear is not worth failing over */ }
	}

	private static void Connected(DiagnosticsClient client, string socket) =>
		AnsiConsole.MarkupLine(
			$"[green]connected[/] to pid {client.Pid} [dim]({Path.GetFileName(socket).EscapeMarkup()})[/]. "
			+ "[dim]exit to leave.[/]");

	/// <summary>
	/// Points the prompt at another socket.
	/// </summary>
	/// <param name="client">The current connection, replaced on success.</param>
	/// <param name="socket">The current path, replaced on success.</param>
	/// <param name="to">Where to connect instead.</param>
	/// <remarks>
	/// The old connection is kept until the new one is open. A typo in a path
	/// would otherwise leave the prompt attached to nothing, which is a worse
	/// outcome than not having moved.
	/// </remarks>
	private static void Swap(ref DiagnosticsClient client, ref string socket, string to)
	{
		DiagnosticsClient next;
		try
		{
			next = DiagnosticsClient.Connect(to);
		}
		catch (CxdbgException ex)
		{
			AnsiConsole.MarkupLine(
				$"[red]{ex.Message.EscapeMarkup()}[/] [dim]still on {Path.GetFileName(socket).EscapeMarkup()}.[/]");
			return;
		}

		client.Dispose();
		client = next;
		socket = to;

		Connected(client, socket);
	}
}
