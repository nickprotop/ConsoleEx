// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace Cxdbg.Commands;

/// <summary>
/// What cxdbg can be asked, and how to ask it.
/// </summary>
/// <remarks>
/// Written out rather than generated from the commands, because the material
/// worth reading is already written: <see cref="ProtocolVerbs.All"/> carries a
/// sentence and an example for every verb. Generated help listed the names and
/// said less.
/// </remarks>
internal static class Help
{
	/// <summary>Prints the help, for everything or for one verb.</summary>
	/// <param name="topic">A verb or command to explain, or null for all.</param>
	/// <returns>0, since asking for help is not a failure.</returns>
	internal static int Print(string? topic)
	{
		if (topic != null) return One(topic);

		var console = AnsiConsole.Console;

		console.MarkupLine("[blue]cxdbg[/] - ask a running SharpConsoleUI application what it is doing.");
		console.WriteLine();
		console.MarkupLine("[yellow]Syntax[/]");
		Console.WriteLine("  cxdbg <verb> [--field value ...] [-s <socket>]");
		console.WriteLine();
		Console.WriteLine("  Field names are the protocol's own, so there is nothing to learn twice.");
		Console.WriteLine("  A bare --field is true; values are read as numbers, booleans or text.");
		console.WriteLine();
		Console.WriteLine("    cxdbg tree --depth 2");
		Console.WriteLine("    cxdbg snapshot --format cells --y 7 --h 1");
		Console.WriteLine("    cxdbg set --logLevel Trace");
		console.WriteLine();

		console.MarkupLine("[yellow]Starting and attaching[/]");
		Row("run <app>", "Start an application with a diagnostics socket");
		Row("shell", "Open a prompt on a running application");
		console.WriteLine();

		console.MarkupLine("[yellow]Reading, formatted[/]");
		Row("show <verb>", "The same verbs, rendered to read");
		Row("why size <control>", "Which container decided a size, and where");
		Row("why color <role>", "How a colour was resolved");
		Row("send key <spec>", "Act on the application the way a user would");
		console.WriteLine();

		console.MarkupLine("[yellow]Protocol verbs[/]");
		foreach (var (verb, what, _, _) in ProtocolVerbs.All) Row(verb, what);
		console.WriteLine();

		console.MarkupLine("[yellow]Options[/]");
		Row("-s, --socket <path>", "Which application. Found automatically when only one is running");
		Row("--json", "The reply as the protocol sent it, instead of formatted");
		Row("--no-color", "No colour, however the output is going");
		console.WriteLine();

		Console.WriteLine("  cxdbg help <verb>   for one verb and its fields.");

		return 0;
	}

	/// <summary>Help for one verb or command.</summary>
	private static int One(string topic)
	{
		foreach (var (verb, what, fields, example) in ProtocolVerbs.All)
		{
			if (!string.Equals(verb, topic, StringComparison.OrdinalIgnoreCase)) continue;

			var console = AnsiConsole.Console;

			console.MarkupLine($"[blue]{verb.EscapeMarkup()}[/] - {what.EscapeMarkup()}");
			console.WriteLine();
			Console.WriteLine($"  cxdbg {verb}" + (fields.Length > 0 ? " [--field value ...]" : ""));
			console.WriteLine();

			if (fields.Length > 0)
			{
				// One column, as wide as the widest option - computed from the
				// rendered text rather than from its parts, which is what made
				// a long one run into its own description.
				var rendered = fields
					.Select(f => f.Takes == null ? $"--{f.Name}" : $"--{f.Name} <{f.Takes}>")
					.ToList();

				// Capped, because one field listing eight actions should not
				// push every other description seventy columns to the right.
				// A field wider than the cap takes its own line.
				const int cap = 30;
				int width = Math.Min(cap, rendered.Max(r => r.Length));

				for (int i = 0; i < fields.Length; i++)
				{
					if (rendered[i].Length > width)
					{
						Console.WriteLine($"  {rendered[i]}");
						Console.WriteLine($"  {new string(' ', width + 2)}{fields[i].What}");
						continue;
					}

					Console.WriteLine($"  {rendered[i].PadRight(width + 2)}{fields[i].What}");
				}

				console.WriteLine();
			}

			console.MarkupLine("[dim]  example[/]");
			Console.WriteLine("    cxdbg " + string.Join(' ', example));
			console.WriteLine();

			// Not a closed list: the protocol decides what exists, and an
			// application newer than this build may read fields nothing here
			// mentions. They still reach it.
			Console.WriteLine("  Fields not listed here still reach the application.");

			return 0;
		}

		// cxdbg's own commands, which have arguments of their own rather than
		// protocol fields. Telling someone to run the command they just ran
		// would be the alternative.
		switch (topic.ToLowerInvariant())
		{
			case "run":
				Section("run <app> [options] [-- <the app's own arguments>]",
					"Start an application with a diagnostics socket.",
					("--attach", "Open a prompt on it once it is up"),
					("--no-terminal", "Run it as a child of cxdbg, not in a terminal of its own"),
					("--timeout <n>", "Seconds to wait for its socket (default 30)"));
				Console.WriteLine();
				Console.WriteLine("  Everything after -- belongs to the application and is passed");
				Console.WriteLine("  through unchanged. With no --no-terminal, cxdbg asks where to");
				Console.WriteLine("  run it.");
				return 0;

			case "shell":
			case "attach":
				Section("shell -s <socket>",
					"Open a prompt on a running application. `attach` is the same command.",
					("-s, --socket", "Which application"));
				Console.WriteLine();
				Console.WriteLine("  At the prompt, .help lists what can be typed there.");
				return 0;

			case "show":
				Section("show <verb> [--field value]",
					"Ask the application and render the reply to read.",
					("--json", "The reply as the protocol sent it"));
				return 0;

			case "why":
				Section("why <question> [subject]",
					"Explain how something ended up the way it is.",
					("size <control>", "Which container decided a size, and where"),
					("color <role>", "How a colour was resolved"),
					("frame", "Which control asked for the last frames"),
					("input", "What the application was told"));
				return 0;

			case "send":
				Section("send <action> [arguments]",
					"Act on the application the way a user would.",
					("key <spec>", "A key, in tmux notation: Tab, Down, C-c"),
					("click <x> <y>", "A click at a point"),
					("resize <w> <h>", "A new screen size, on a headless application"));
				return 0;
		}

		AnsiConsole.MarkupLine(
			$"[red]no help for '{topic.EscapeMarkup()}'.[/] "
			+ "Run [blue]cxdbg help[/] for the list, or ask the application "
			+ "with [blue]cxdbg commands[/].");

		return 3;
	}

	/// <summary>A command, what it does, and its own options.</summary>
	private static void Section(string usage, string what, params (string Name, string What)[] options)
	{
		var console = AnsiConsole.Console;

		console.MarkupLine($"[blue]cxdbg {usage.EscapeMarkup()}[/]");
		console.WriteLine();
		Console.WriteLine("  " + what);

		if (options.Length == 0) return;

		console.WriteLine();
		foreach (var (name, text) in options) Row(name, text);
	}

	/// <summary>One name and what it does, aligned.</summary>
	private static void Row(string name, string what) =>
		Console.WriteLine($"  {name,-22}  {what}");
}
