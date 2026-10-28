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
/// The prompt's own commands, told apart by a leading dot.
/// </summary>
/// <remarks>
/// <para>
/// The prompt speaks the protocol: <c>tree</c>, <c>key</c> and <c>measure</c>
/// go to the application. cxdbg's own layer needs to be distinguishable from
/// that, and one character does it - no application verb can ever collide with
/// a dot command, however the protocol grows.
/// </para>
/// <para>
/// It also answers what `show` and `why` should do here. They used to be
/// refused with an explanation; now they are <c>.show</c> and <c>.why</c>, so
/// the readable layer is available at the prompt rather than described there.
/// </para>
/// </remarks>
internal static class DotCommands
{
	/// <summary>Runs one, if the line is one.</summary>
	/// <param name="words">The line, split.</param>
	/// <param name="client">The connection, for the ones that ask something.</param>
	/// <param name="held">The last reply.</param>
	/// <returns>True when it was a dot command and has been dealt with.</returns>
	internal static bool Run(string[] words, DiagnosticsClient client, Held held)
	{
		if (words.Length == 0 || !words[0].StartsWith('.')) return false;

		string name = words[0][1..].ToLowerInvariant();
		var rest = words.Skip(1).ToArray();

		switch (name)
		{
			case "help":
			case "?":
				Help(rest);
				return true;

			case "last":
				Last(rest, held);
				return true;

			case "socket":
				AnsiConsole.MarkupLine(
					$"pid [blue]{client.Pid}[/]  protocol {client.Protocol}");
				return true;

			case "show":
			case "why":
				Readable(name, rest, client, held);
				return true;

			default:
				AnsiConsole.MarkupLine(
					$"[yellow]no .{name.EscapeMarkup()}[/]. Try [blue].help[/].");
				return true;
		}
	}

	/// <summary>What can be typed here.</summary>
	private static void Help(string[] rest)
	{
		if (rest.Length > 0)
		{
			Commands.Help.Print(rest[0]);
			return;
		}

		var console = AnsiConsole.Console;

		console.MarkupLine("[yellow]At this prompt[/]");
		Console.WriteLine("  <verb> [--field value]   ask the application; names are the protocol's");
		Console.WriteLine("  <verb> > <file>          write the reply to a file instead");
		Console.WriteLine("  .<command>               cxdbg's own, below");
		console.WriteLine();

		console.MarkupLine("[yellow]cxdbg's own[/]");
		Console.WriteLine("  .help [verb]             this, or one verb in detail");
		Console.WriteLine("  .show <verb>             the reply, rendered to read");
		Console.WriteLine("  .why size <control>      which container decided a size");
		Console.WriteLine("  .last [show|> file]      the reply that was too big to print");
		Console.WriteLine("  .socket                  which application this is");
		Console.WriteLine("  connect <path>           move to another application");
		Console.WriteLine("  exit                     leave, offering to stop the application");
		console.WriteLine();

		console.MarkupLine("[yellow]The application knows[/]");

		// Wrapped: twenty-three names on one line is a line nobody reads to
		// the end of, and it is the list people scan for a verb.
		int width = 0;
		foreach (var (verb, _, _, _) in ProtocolVerbs.All)
		{
			if (width == 0) { Console.Write("  "); width = 2; }
			else if (width + verb.Length + 2 > 74) { Console.WriteLine(); Console.Write("  "); width = 2; }
			else { Console.Write(", "); width += 2; }

			Console.Write(verb);
			width += verb.Length;
		}

		Console.WriteLine();
		console.WriteLine();
		Console.WriteLine("  .help <verb> for what one takes.");
	}

	/// <summary>The reply the prompt held back.</summary>
	private static void Last(string[] rest, Held held)
	{
		if (held.Reply == null)
		{
			AnsiConsole.MarkupLine("[yellow]nothing held yet.[/]");
			return;
		}

		// .last > file, and .last >> file
		if (rest.Length >= 2 && rest[0] is ">" or ">>")
		{
			Console.Write(held.Write(rest[1], rest[0] == ">>"));
			return;
		}

		string what = rest.Length > 0 ? rest[0].ToLowerInvariant() : "show";

		Console.Write(what switch
		{
			"json" => Output.Json(held.Reply),
			_ => Output.Pretty(held.Verb, held.Reply),
		});
	}

	/// <summary>cxdbg's readable layer, at the prompt.</summary>
	private static void Readable(
		string name, string[] rest, DiagnosticsClient client, Held held)
	{
		if (rest.Length == 0)
		{
			AnsiConsole.MarkupLine(
				$"[yellow].{name} needs something[/], e.g. [blue].{name} "
				+ (name == "why" ? "size <control>" : "tree") + "[/].");
			return;
		}

		var line = CommandLineParser.Parse(rest);

		string verb = name == "why" ? Why(line.Verb) : line.Verb;
		var fields = new Dictionary<string, System.Text.Json.Nodes.JsonNode?>(line.Fields);

		// why size <control> and why color <role> name their subject
		// positionally, the way the command line does.
		if (name == "why" && line.Rest.Count > 0)
		{
			fields[line.Verb == "color" ? "role" : "control"] = line.Rest[0];
		}

		var reply = client.Send(verb, fields);
		held.Keep(verb, reply);

		Console.Write(Output.Pretty(verb, reply));
	}

	/// <summary>The verb behind a why question.</summary>
	private static string Why(string what) => what.ToLowerInvariant() switch
	{
		"size" => "measure",
		"color" or "colour" => "color",
		"frame" => "invalidations",
		"input" => "events",
		_ => throw CxdbgException.Usage(
			$"'{what}' is not something to explain. Try: size, color, frame, input"),
	};
}
