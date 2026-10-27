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
using Spectre.Console.Cli;

namespace Cxdbg.Commands;

/// <summary>Settings for the questions that have a cascade for an answer.</summary>
public class WhySettings : SocketSettings
{
	[Description("What to explain: size, color, frame, input")]
	[CommandArgument(0, "<WHAT>")]
	public string What { get; set; } = "size";

	[Description("The control id (for size) or role name (for color)")]
	[CommandArgument(1, "[SUBJECT]")]
	public string? Subject { get; set; }

	[Description("Interaction state for a colour: Normal, Focused, Disabled")]
	[CommandOption("--state <STATE>")]
	public string? State { get; set; }

	[Description("Resolve the colour as an outline rather than a fill")]
	[CommandOption("--outline")]
	public bool Outline { get; set; }

	[Description("Print the raw reply instead of formatting it")]
	[CommandOption("--json")]
	public bool Json { get; set; }
}

/// <summary>
/// Explains how something ended up the way it is.
/// </summary>
/// <remarks>
/// These four read as the questions they answer. "Why is this forty wide" has
/// an answer and it is usually a container several levels up; "why is this
/// green" has one and it is usually a contrast adjustment.
/// </remarks>
public sealed class WhyCommand : Command<WhySettings>
{
	protected override int Execute(CommandContext context, WhySettings settings, CancellationToken ct)
		=> CommandRunner.Run(settings, settings.Json, client =>
		{
			switch (settings.What.ToLowerInvariant())
			{
				case "size":
				{
					if (string.IsNullOrEmpty(settings.Subject))
						throw new CxdbgException("why size needs a control id, from `show tree`");

					var reply = client.Send("measure", new Dictionary<string, JsonNode?>
					{
						["control"] = settings.Subject,
					});
					return settings.Json ? CommandRunner.Raw(reply) : CascadeFormatter.RenderSize(reply);
				}

				case "color":
				case "colour":
				{
					var args = new Dictionary<string, JsonNode?>
					{
						["role"] = settings.Subject ?? "Primary",
					};
					if (settings.State != null) args["state"] = settings.State;
					if (settings.Outline) args["outline"] = true;

					var reply = client.Send("color", args);
					return settings.Json ? CommandRunner.Raw(reply) : CascadeFormatter.RenderColor(reply);
				}

				case "frame":
				{
					var reply = client.Send("invalidations");
					return settings.Json ? CommandRunner.Raw(reply) : RenderFrames(reply);
				}

				case "input":
				{
					var reply = client.Send("events");
					return settings.Json ? CommandRunner.Raw(reply) : RenderEvents(reply);
				}

				default:
					throw new CxdbgException(
						$"'{settings.What}' is not something to explain. Try: size, color, frame, input");
			}
		});

	/// <summary>Which control asked for the frames, and how often.</summary>
	private static string RenderFrames(JsonNode reply)
	{
		var windows = reply["windows"]?.AsArray();
		if (windows == null || windows.Count == 0)
		{
			// Empty and "never recording" are different answers, and the second is
			// likelier: invalidations are only kept when diagnostics are enabled.
			return "Nothing recorded. Invalidations are only kept when the application "
				+ "was started with EnableDiagnostics.\n";
		}

		var sb = new System.Text.StringBuilder();
		foreach (var w in windows)
		{
			sb.AppendLine($"{w!["window"]}   {w["total"]} invalidations");
			foreach (var c in w["callers"]?.AsArray() ?? new JsonArray())
			{
				sb.AppendLine($"    {c!["control"],-28} {c["relayouts"]} relayouts, "
					+ $"{c["repaints"]} repaints");
			}
		}
		return sb.ToString();
	}

	/// <summary>What the application was told, as its controls saw it.</summary>
	private static string RenderEvents(JsonNode reply)
	{
		if (reply["recording"]?.GetValue<bool>() == false)
		{
			return "Not recording. Start the application with EnableDiagnostics to tape "
				+ "the input it receives.\n";
		}

		var events = reply["events"]?.AsArray();
		if (events == null || events.Count == 0) return "No input since the last look.\n";

		var sb = new System.Text.StringBuilder();
		foreach (var e in events)
			sb.AppendLine($"    ({e!["x"]},{e["y"]})   {e["flags"]}");

		return sb.ToString();
	}
}
