// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json.Nodes;
using Spectre.Console.Cli;

namespace Cxdbg.Commands;

/// <summary>Settings for reading or flipping a runtime switch.</summary>
public class SetSettings : SocketSettings
{
	[Description("The switch: diagnostics, logging, logLevel, clearLogs. Omit to read them all")]
	[CommandArgument(0, "[SWITCH]")]
	public string? Switch { get; set; }

	[Description("Its new value. Omit to read the current one")]
	[CommandArgument(1, "[VALUE]")]
	public string? Value { get; set; }

	[Description("Print the raw reply instead of the switches, one per line")]
	[CommandOption("--json")]
	public bool Json { get; set; }
}

/// <summary>
/// Reads the application's runtime switches, or changes one.
/// </summary>
/// <remarks>
/// A bare <c>cxdbg set</c> is a read: the protocol answers every switch with
/// its current value whether or not anything changed, so the write and the
/// read are the same verb and a write always reports what it produced.
/// </remarks>
public sealed class SetCommand : Command<SetSettings>
{
	protected override int Execute(CommandContext context, SetSettings settings, CancellationToken ct)
		=> CommandRunner.Run(settings, settings.Json, client =>
		{
			var args = new Dictionary<string, JsonNode?>();

			if (!string.IsNullOrEmpty(settings.Switch))
			{
				// A switch named with no value is "turn it on": `set logging` reads
				// as the sentence it is. Only a bare `set` is the read.
				string raw = settings.Value ?? "true";

				args[settings.Switch] = bool.TryParse(raw, out bool b) ? b
					: int.TryParse(raw, out int n) ? n
					: raw;
			}

			var reply = client.Send("set", args);

			if (settings.Json) return CommandRunner.Raw(reply) + "\n";

			var lines = new List<string>();
			foreach (var (name, value) in reply.AsObject())
			{
				// "ok" is the transport saying the request succeeded, not a switch
				// anything can be asked to flip. Listing it among the switches
				// invites someone to try.
				if (name is "changed" or "ok") continue;
				lines.Add($"{name,-22}{value?.ToJsonString() ?? "null"}");
			}

			var changed = reply["changed"]?.AsArray();
			if (changed is { Count: > 0 })
				lines.Add($"\nchanged: {string.Join(", ", changed.Select(c => c?.GetValue<string>()))}");

			return string.Join("\n", lines) + "\n";
		});
}
