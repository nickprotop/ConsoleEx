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
using Cxdbg.Protocol;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cxdbg.Commands;

/// <summary>Settings for a command that reports something.</summary>
public class ShowSettings : SocketSettings
{
	[Description("What to show: tree, snapshot, image, focus, reachable, windows, metrics, logs")]
	[CommandArgument(0, "<WHAT>")]
	public string What { get; set; } = "tree";

	[Description("Print the raw reply instead of formatting it")]
	[CommandOption("--json")]
	public bool Json { get; set; }
}

/// <summary>Asks the application a question and prints the answer.</summary>
public sealed class ShowCommand : Command<ShowSettings>
{
	/// <summary>What `show` will ask for, and the verb each maps to.</summary>
	private static readonly Dictionary<string, string> Verbs = new(StringComparer.OrdinalIgnoreCase)
	{
		["tree"] = "tree",
		["snapshot"] = "snapshot",
		["image"] = "image",
		["focus"] = "focus",
		["reachable"] = "reachable",
		["windows"] = "windows",
		["metrics"] = "metrics",
		["logs"] = "logs",
	};

	protected override int Execute(CommandContext context, ShowSettings settings, CancellationToken ct)
		=> CommandRunner.Run(settings, settings.Json, client =>
		{
			if (!Verbs.TryGetValue(settings.What, out string? verb))
			{
				throw new CxdbgException(
					$"'{settings.What}' is not something to show. Try one of: "
					+ string.Join(", ", Verbs.Keys));
			}

			var reply = client.Send(verb);

			// --json is the escape hatch for scripting: the reply as the protocol
			// sent it, so a caller is never stuck behind our formatting choices.
			if (settings.Json) return CommandRunner.Raw(reply);

			return verb switch
			{
				"tree" => TreeFormatter.Render(reply),
				"snapshot" => string.Join("\n",
					(reply["lines"]?.AsArray() ?? new JsonArray())
						.Select(l => l?.GetValue<string>() ?? "")),
				_ => Plain.Render(reply),
			};
		});
}
