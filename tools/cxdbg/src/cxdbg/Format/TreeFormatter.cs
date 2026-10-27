// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.Json.Nodes;

namespace Cxdbg.Format;

/// <summary>
/// Renders a layout tree as something a person can scan.
/// </summary>
/// <remarks>
/// Indentation carries the structure and the id carries the handle: every line
/// names something another verb will accept, so reading the tree and asking a
/// follow-up question are the same gesture.
/// </remarks>
public static class TreeFormatter
{
	/// <summary>Renders the reply from the <c>tree</c> verb.</summary>
	public static string Render(JsonNode reply)
	{
		var sb = new StringBuilder();

		if (reply["windows"] is JsonArray windows)
		{
			foreach (var w in windows)
			{
				if (w is not JsonObject win) continue;

				sb.Append(win["title"]?.GetValue<string>() ?? "(untitled)");

				if (win["bounds"] is JsonObject b)
					sb.Append($"  {b["w"]}x{b["h"]} at {b["x"]},{b["y"]}");

				if (win["state"]?["active"]?.GetValue<bool>() == true) sb.Append("  [active]");
				sb.AppendLine();

				RenderNode(win["node"], 1, sb);
				sb.AppendLine();
			}
		}

		return sb.ToString();
	}

	private static void RenderNode(JsonNode? node, int depth, StringBuilder sb)
	{
		if (node is not JsonObject o) return;

		string indent = new(' ', depth * 2);
		string name = o["id"]?.GetValue<string>()
			?? o["control"]?.GetValue<string>()
			?? "(node)";

		sb.Append(indent).Append(name);

		// Desired against arranged, because the disagreement between them is
		// where a layout bug shows: a control that asked for one size and got
		// another is the whole question.
		var desired = o["desired"];
		var bounds = o["bounds"];
		if (desired != null && bounds != null)
		{
			sb.Append($"   wants {desired["w"]}x{desired["h"]}")
			  .Append($"  got {bounds["w"]}x{bounds["h"]}");

			if (bounds["w"]?.GetValue<int>() == 0 || bounds["h"]?.GetValue<int>() == 0)
				sb.Append("  <-- zero extent, invisible");
		}

		if (o["layout"]?.GetValue<string>() is { } layout) sb.Append($"   [{layout}]");
		if (o["visible"]?.GetValue<bool>() == false) sb.Append("   (hidden)");

		sb.AppendLine();

		if (o["children"] is JsonArray kids)
			foreach (var k in kids) RenderNode(k, depth + 1, sb);
	}
}
