// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using Cxdbg.Format;
using Cxdbg.Protocol;

namespace Cxdbg.Commands;

/// <summary>
/// Explains how something ended up the way it is.
/// </summary>
/// <remarks>
/// These four read as the questions they answer. "Why is this forty wide" has
/// an answer and it is usually a container several levels up; "why is this
/// green" has one and it is usually a contrast adjustment.
/// </remarks>
internal static class WhyCommand
{
	/// <summary>Runs <c>why &lt;what&gt; [subject]</c>.</summary>
	internal static int Execute(CommandLine line)
	{
		if (line.Rest.Count == 0)
		{
			throw CxdbgException.Usage(
				"why needs a question: size, color, frame or input.");
		}

		string what = line.Rest[0].ToLowerInvariant();
		string? subject = line.Rest.Count > 1 ? line.Rest[1] : null;

		return what switch
		{
			"size" => Size(line, subject),
			"color" or "colour" => Color(line, subject),
			"frame" => Ask(line, "invalidations", RenderFrames),
			"input" => Ask(line, "events", RenderEvents),
			_ => throw CxdbgException.Usage(
				$"'{what}' is not something to explain. Try: size, color, frame, input"),
		};
	}

	/// <summary>Which container decided a control's size.</summary>
	private static int Size(CommandLine line, string? subject)
	{
		if (string.IsNullOrEmpty(subject))
			throw CxdbgException.Usage("why size needs a control id, from `cxdbg show tree`");

		var fields = new Dictionary<string, JsonNode?>(line.Fields) { ["control"] = subject };

		return CommandRunner.Run(line, client =>
		{
			var reply = client.Send("measure", fields);
			return line.Json ? Output.Json(reply) : CascadeFormatter.RenderSize(reply, Surface.Colour(line.NoColor));
		});
	}

	/// <summary>How a role's colour was resolved.</summary>
	private static int Color(CommandLine line, string? subject)
	{
		var fields = new Dictionary<string, JsonNode?>(line.Fields)
		{
			["role"] = subject ?? "Primary",
		};

		return CommandRunner.Run(line, client =>
		{
			var reply = client.Send("color", fields);
			return line.Json ? Output.Json(reply) : CascadeFormatter.RenderColor(reply, Surface.Colour(line.NoColor));
		});
	}

	/// <summary>Sends a verb and renders it with one of the two below.</summary>
	private static int Ask(CommandLine line, string verb, Func<JsonNode, string> render) =>
		CommandRunner.Run(line, client =>
		{
			var reply = client.Send(verb, line.Fields);
			return line.Json ? Output.Json(reply) : render(reply);
		});

	/// <summary>Which control asked for the frames, and how often.</summary>
	private static string RenderFrames(JsonNode reply)
	{
		var windows = reply["windows"]?.AsArray();
		if (windows == null || windows.Count == 0)
		{
			// Empty and "never recording" are different answers, and the second
			// is likelier: invalidations are only kept while diagnostics are on.
			return "Nothing recorded. Invalidations are kept only while diagnostics "
				+ "are on - turn them on with `cxdbg set --diagnostics true`.\n";
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
			return "Not recording. Turn input recording on with "
				+ "`cxdbg set --diagnostics true`.\n";
		}

		var events = reply["events"]?.AsArray();
		if (events == null || events.Count == 0) return "No input since the last look.\n";

		var sb = new System.Text.StringBuilder();

		foreach (var e in events)
		{
			// A key carries no point, and "(,)" in front of every one of them
			// is noise where the tape is mostly keys.
			string where = e!["x"] != null ? $"({e["x"]},{e["y"]})" : "";

			sb.Append("    ").Append(where.PadRight(10)).AppendLine(
				e["flags"]?.GetValue<string>() ?? "");
		}

		return sb.ToString();
	}
}
