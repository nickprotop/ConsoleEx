// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using Cxdbg.Protocol;
using Spectre.Console;

namespace Cxdbg.Commands;

/// <summary>
/// Sends one command: cxdbg's own, or a protocol verb.
/// </summary>
/// <remarks>
/// <para>
/// cxdbg's own commands are a short, closed list - <c>run</c>, <c>shell</c>,
/// <c>show</c>, <c>why</c>, <c>send</c>, <c>help</c>. Everything else is a
/// verb and goes to the application, including verbs this build has never
/// heard of: the protocol decides what exists, and an application newer than
/// its debugger answers an unknown verb with the list it knows.
/// </para>
/// <para>
/// That is why nothing is declared in advance. A parser validating against a
/// list of known options would have to be taught every field of every verb,
/// and would refuse the one that was added yesterday.
/// </para>
/// </remarks>
internal static class Dispatch
{
	/// <summary>cxdbg's own commands, which are not verbs.</summary>
	internal static readonly string[] Own =
		["run", "shell", "attach", "show", "why", "send", "help"];

	/// <summary>Runs a command line.</summary>
	/// <param name="args">argv.</param>
	/// <returns>An exit code: 0 answered, 1 refused, 2 nothing listening, 3 bad usage.</returns>
	internal static int Run(string[] args)
	{
		// No arguments prints help rather than guessing at a socket: a debugger
		// that silently attached to whatever it found would be a surprise.
		if (args.Length == 0) return Help.Print(null);

		if (args[0] is "--help" or "-h" or "help")
		{
			return Help.Print(args.Length > 1 ? args[1] : null);
		}

		if (args[0] is "--version" or "-v")
		{
			Console.WriteLine(typeof(Dispatch).Assembly.GetName().Version?.ToString() ?? "1.0.0");
			return 0;
		}

		// `cxdbg tree --help` asks about the verb, not the application. Checked
		// before parsing, because --help would otherwise be read as a protocol
		// field and sent - which answers a question nobody asked.
		if (args.Contains("--help") || args.Contains("-h")) return Help.Print(args[0]);

		// `seq < capture.txt` reads the bytes from a file. Split before parsing,
		// so a path is never mistaken for a protocol field.
		var words = SeqInput.SplitRedirect(args, out string? readFrom);

		CommandLine line;
		try
		{
			line = CommandLineParser.Parse(words);
		}
		catch (CxdbgException ex)
		{
			AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
			return 3;
		}

		if (readFrom != null)
		{
			if (SeqInput.Load(readFrom, (Dictionary<string, JsonNode?>)line.Fields) is { } error)
			{
				AnsiConsole.MarkupLine($"[red]{error.EscapeMarkup()}[/]");
				return 3;
			}
		}
		else if (line.Verb == "seq"
			&& !line.Fields.ContainsKey("text")
			&& !line.Fields.ContainsKey("raw")
			&& Console.IsInputRedirected)
		{
			// A shell eats `< capture.txt` before cxdbg is started, so the file
			// arrives as stdin. Only when nothing else said what to send, and
			// only when something really is piped in - reading an interactive
			// terminal here would hang waiting for a sequence nobody is typing.
			if (SeqInput.LoadStdin((Dictionary<string, JsonNode?>)line.Fields) is { } error)
			{
				AnsiConsole.MarkupLine($"[red]{error.EscapeMarkup()}[/]");
				return 3;
			}
		}

		try
		{
			return line.Verb switch
			{
				"run" => RunCommand.Execute(line),
				"shell" or "attach" => ShellCommand.Execute(line),
				"show" => ShowCommand.Execute(line),
				"why" => WhyCommand.Execute(line),
				"send" => SendCommand.Execute(line),
				"watch" => WatchCommand.Execute(line),
				_ => VerbCommand.Execute(line),
			};
		}
		catch (CxdbgException ex)
		{
			AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
			return ex.Code == CxdbgException.BadUsage ? 3 : 1;
		}
	}
}
