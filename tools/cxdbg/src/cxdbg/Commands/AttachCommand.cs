// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using Cxdbg.Protocol;
using Cxdbg.Session;
using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cxdbg.Commands;

/// <summary>Settings for entering a session on a socket.</summary>
/// <remarks>
/// The socket is REQUIRED here, unlike the one-question verbs. Those print an
/// answer; this takes over the terminal. A debugger that guessed which
/// application you meant and then filled the screen with it would be the
/// surprise that `run` printing its socket exists to avoid.
/// </remarks>
public class AttachSettings : CommandSettings
{
	[Description("tui (the default) or shell")]
	[CommandArgument(0, "[MODE]")]
	public string Mode { get; set; } = "tui";

	[Description("Path to the application's diagnostics socket, as `cxdbg run` printed it")]
	[CommandOption("-s|--socket <PATH>")]
	public string SocketPath { get; set; } = "";

	[Description("Run the tui headless, for tests and CI")]
	[CommandOption("--headless")]
	public bool Headless { get; set; }

	[Description("Screen size for a headless tui, as WxH")]
	[CommandOption("--size <WxH>")]
	public string? Size { get; set; }
}

/// <summary>
/// Enters a session on a socket: the tui, or a prompt.
/// </summary>
/// <remarks>
/// Every other verb is one question and one answer. These two keep the socket
/// and stay until you leave.
/// </remarks>
public sealed class AttachCommand : Command<AttachSettings>
{
	protected override int Execute(CommandContext context, AttachSettings settings, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(settings.SocketPath))
		{
			AnsiConsole.MarkupLine(
				"[red]attach needs a socket.[/] `cxdbg run <app>` prints one, or pass "
				+ "-s with the path of an application that is already running.");
			return 3;
		}

		return settings.Mode.ToLowerInvariant() switch
		{
			"tui" => Tui(settings),
			"shell" => Shell(settings.SocketPath),
			_ => Unknown(settings.Mode),
		};
	}

	private static int Unknown(string mode)
	{
		AnsiConsole.MarkupLine($"[red]no such mode '{mode.EscapeMarkup()}'[/]; try tui or shell.");
		return 3;
	}

	/// <summary>
	/// A prompt on the socket.
	/// </summary>
	/// <param name="socket">Where the application is listening.</param>
	/// <returns>An exit code.</returns>
	/// <remarks>
	/// Type a verb, get its reply. The same verbs the CLI takes, without the
	/// typing and without reconnecting for each one. <c>exit</c> leaves.
	/// </remarks>
	private static int Shell(string socket)
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

		try
		{
			Connected(client, socket);

			while (true)
			{
				// Console.ReadLine, not Spectre's TextPrompt: the prompt refuses
				// piped input ("failed to read input in non-interactive mode"),
				// which would make the shell untestable and unscriptable. A
				// debugger you cannot pipe a command into is less useful than
				// one without colour on its prompt.
				AnsiConsole.Markup("[blue]cxdbg>[/] ");

				string? input = Console.ReadLine();
				if (input == null) return 0;   // end of input: the same as exit

				string line = input.Trim();
				if (line.Length == 0) continue;
				if (line is "exit" or "quit") return 0;

				var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

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
					var reply = client.Send(
						words[0], VerbCommand.ParseFlags(words.Skip(1), out _));
					Console.WriteLine(CommandRunner.Raw(reply));
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

	/// <summary>Says which application the prompt is now talking to.</summary>
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

	/// <summary>The live view on the socket.</summary>
	private static int Tui(AttachSettings settings)
	{
		try
		{
			using var probe = DiagnosticsClient.Connect(settings.SocketPath);
		}
		catch (CxdbgException ex)
		{
			// Reached once BEFORE painting: a session that opened a full-screen
			// view and then said "nothing is there" would have taken over the
			// terminal to deliver a message that belongs on one line.
			AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
			return ex.Message.Contains("listening", StringComparison.Ordinal) ? 2 : 1;
		}

		var (w, h) = ParseSize(settings.Size);

		IConsoleDriver driver = settings.Headless
			? new HeadlessConsoleDriver(w, h)
			: new NetConsoleDriver(RenderMode.Buffer);

		using var poller = new TargetPoller(settings.SocketPath);
		var system = new ConsoleWindowSystem(driver);

		new DebuggerWindow(system, poller).Show();

		return system.Run();
	}

	/// <summary>Reads a WxH, falling back to a size that fits the panes.</summary>
	private static (int Width, int Height) ParseSize(string? size)
	{
		if (!string.IsNullOrWhiteSpace(size))
		{
			var parts = size.Split('x', 'X');
			if (parts.Length == 2
				&& int.TryParse(parts[0], out int w)
				&& int.TryParse(parts[1], out int h)
				&& w > 0 && h > 0)
			{
				return (w, h);
			}
		}

		return (120, 40);
	}
}
