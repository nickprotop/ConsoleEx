// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json.Nodes;
using Cxdbg.Format;
using Spectre.Console.Cli;

namespace Cxdbg.Commands;

/// <summary>Settings for sending one protocol verb.</summary>
public class VerbSettings : SocketSettings
{
	/// <summary>
	/// The protocol's own fields, passed as options.
	/// </summary>
	/// <remarks>
	/// <c>cxdbg tree --depth 2</c> sends <c>{"cmd":"tree","depth":2}</c>. The
	/// names are the protocol's, so there is nothing to learn twice and nothing
	/// to keep in step: a verb that gains a field gains an option.
	/// <para>
	/// Collected by Spectre as the remaining arguments, which is what lets one
	/// command class serve every verb without declaring each verb's fields.
	/// </para>
	/// </remarks>
	[Description("Protocol fields for the verb, e.g. --depth 2 --window Settings")]
	[CommandOption("--arg <NAME=VALUE>")]
	public string[] Args { get; set; } = [];

	[Description("Print the raw reply even when a formatter exists")]
	[CommandOption("--json")]
	public bool Json { get; set; }
}

/// <summary>
/// Sends one protocol verb and prints what came back.
/// </summary>
/// <remarks>
/// <para>
/// Every verb the protocol has is a cxdbg command, registered from one list.
/// Hand-writing a class per verb would drift the day the protocol grows one,
/// and the protocol is the thing that decides what exists - so cxdbg follows
/// it rather than duplicating it.
/// </para>
/// <para>
/// <c>show</c> and <c>why</c> are the readable layer over the same verbs, not
/// the only way in. <c>cxdbg tree</c> gives the reply; <c>cxdbg show tree</c>
/// gives it formatted.
/// </para>
/// </remarks>
public sealed class VerbCommand : Command<VerbSettings>
{
	/// <summary>The verb this instance was registered under.</summary>
	/// <remarks>
	/// Spectre builds the command from its name, so the name IS the verb. Set
	/// through the command's data rather than a constructor parameter, which
	/// the resolver does not supply.
	/// </remarks>
	private static string VerbOf(CommandContext context) =>
		context.Name;

	protected override int Execute(CommandContext context, VerbSettings settings, CancellationToken ct)
		=> CommandRunner.Run(settings, settings.Json, client =>
		{
			var reply = client.Send(VerbOf(context),
				ParseArgs(settings.Args, context.Remaining));

			// A formatted reading where one exists, because a tree printed as JSON
			// is a wall. --json opts back out for scripting.
			if (!settings.Json)
			{
				switch (VerbOf(context))
				{
					case "tree": return TreeFormatter.Render(reply);
					case "measure": return CascadeFormatter.RenderSize(reply);
					case "color": return CascadeFormatter.RenderColor(reply);
					case "snapshot":
						// Several replies wear this verb. Cells are a grid and
						// read column by column; everything else is lines, and
						// markup lines are printed as they are so they can be
						// piped into a file and parsed back later.
						return reply["rows"] != null
							? CellFormatter.Render(reply)
							: string.Join("\n",
								(reply["lines"]?.AsArray() ?? [])
									.Select(l => l?.GetValue<string>() ?? "")) + "\n";
				}
			}

			return CommandRunner.Raw(reply) + "\n";
		});

	/// <summary>
	/// Sends a verb this build does not know, because the application might.
	/// </summary>
	/// <param name="args">The whole command line, verb first.</param>
	/// <returns>An exit code.</returns>
	/// <remarks>
	/// cxdbg ships with a list of verbs; an application can be newer than the
	/// debugger attached to it. Refusing would make cxdbg the thing that has to
	/// be upgraded first. The application answers an unknown verb with the list
	/// it knows, which is better discovery than anything hard-coded here.
	/// </remarks>
	public static int PassThrough(string[] args)
	{
		var fields = ParseFlags(args.Skip(1), out string? socket);
		var settings = new VerbSettings { SocketPath = socket };

		return CommandRunner.Run(settings, json: true, client =>
			CommandRunner.Raw(client.Send(args[0], fields)) + "\n");
	}

	/// <summary>
	/// Reads <c>--name value</c> flags into request fields.
	/// </summary>
	/// <param name="words">The words after the verb.</param>
	/// <param name="socket">Set when the words carried a socket.</param>
	/// <returns>The protocol fields.</returns>
	/// <remarks>
	/// Shared by the pass-through path and the shell, which both see a verb
	/// and a bare word list rather than something Spectre has parsed. A flag
	/// with no value is <c>true</c>, so <c>--outline</c> says what it reads as.
	/// </remarks>
	internal static Dictionary<string, JsonNode?> ParseFlags(
		IEnumerable<string> words, out string? socket)
	{
		socket = null;
		var fields = new Dictionary<string, JsonNode?>();
		var list = words.ToList();

		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] is "--socket" or "-s")
			{
				if (++i < list.Count) socket = list[i];
				continue;
			}

			if (!list[i].StartsWith("--")) continue;

			string name = list[i][2..];
			string? value = i + 1 < list.Count && !list[i + 1].StartsWith("--")
				? list[++i]
				: null;

			fields[name] = value == null ? true
				: int.TryParse(value, out int n) ? n
				: bool.TryParse(value, out bool b) ? b
				: value;
		}

		return fields;
	}

	/// <summary>Turns <c>name=value</c> pairs into request fields.</summary>
	/// <remarks>
	/// Numbers and booleans are sent as themselves: the protocol distinguishes
	/// <c>2</c> from <c>"2"</c>, and a depth sent as a string is rejected.
	/// </remarks>
	internal static Dictionary<string, JsonNode?> ParseArgs(
		IEnumerable<string> pairs, IRemainingArguments? remaining = null)
	{
		var args = new Dictionary<string, JsonNode?>();

		// Anything Spectre did not recognise is a protocol field: --depth 2
		// becomes depth:2. One command class therefore serves every verb,
		// including verbs and fields that did not exist when this was written.
		if (remaining != null)
		{
			foreach (var option in remaining.Parsed)
			{
				string name = option.Key.TrimStart('-');
				string? value = option.FirstOrDefault();

				args[name] = value == null ? true
					: int.TryParse(value, out int rn) ? rn
					: bool.TryParse(value, out bool rb) ? rb
					: value;
			}
		}

		foreach (string pair in pairs)
		{
			int eq = pair.IndexOf('=');
			if (eq <= 0)
				throw Protocol.CxdbgException.Usage($"--arg wants name=value, got '{pair}'");

			string name = pair[..eq];
			string value = pair[(eq + 1)..];

			args[name] = int.TryParse(value, out int n) ? n
				: bool.TryParse(value, out bool b) ? b
				: value;
		}

		return args;
	}
}
