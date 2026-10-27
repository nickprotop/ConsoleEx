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
/// Renders a cascade: the chain of decisions that produced one value.
/// </summary>
/// <remarks>
/// The same shape for size and for colour, because they are the same idea -
/// a browser showing which rules applied and which one won. Outermost first,
/// with the deciding step marked, so reading down the list follows the value
/// to where it was settled.
/// </remarks>
public static class CascadeFormatter
{
	/// <summary>Renders the reply from the <c>measure</c> verb.</summary>
	public static string RenderSize(JsonNode reply)
	{
		var sb = new StringBuilder();
		var controls = reply["controls"]?.AsArray();

		if (controls == null || controls.Count == 0)
		{
			return "No control matched. Ids come from `show tree`, and an id whose "
				+ "control has gone matches nothing.\n";
		}

		foreach (var c in controls)
		{
			if (c is not JsonObject o) continue;

			sb.AppendLine($"{o["control"]}")
			  .AppendLine($"  offered {Extent(o["offeredWidth"])}x{Extent(o["offeredHeight"])}"
				+ $"   wants {o["desiredWidth"]}x{o["desiredHeight"]}")
			  .AppendLine();

			foreach (var link in o["cascade"]?.AsArray() ?? new JsonArray())
			{
				if (link is not JsonObject l) continue;

				string who = l["control"]?.GetValue<string>() ?? "(root)";
				var marks = new List<string>();
				if (l["setsWidth"]?.GetValue<bool>() == true) marks.Add("sets width");
				if (l["setsHeight"]?.GetValue<bool>() == true) marks.Add("sets height");

				sb.Append($"    {who,-24} {l["layout"],-22} offers {l["offeredWidth"]}x{l["offeredHeight"]}");
				if (marks.Count > 0) sb.Append($"   <== {string.Join(" and ", marks)}");
				sb.AppendLine();
			}

			sb.AppendLine();
		}

		return sb.ToString();
	}

	/// <summary>
	/// An offered extent, with "unbounded" spelled out.
	/// </summary>
	/// <remarks>
	/// A scrolling container offers int.MaxValue, because scrolling is how it
	/// accommodates whatever a child asks for. Printed raw it reads as a number
	/// that means nothing; the word is the fact a reader needs, and an
	/// unexpected one is usually the bug.
	/// </remarks>
	private static string Extent(JsonNode? value)
	{
		int n = value?.GetValue<int>() ?? 0;
		return n >= int.MaxValue / 2 ? "unbounded" : n.ToString();
	}

	/// <summary>Renders the reply from the <c>color</c> verb.</summary>
	public static string RenderColor(JsonNode reply)
	{
		var sb = new StringBuilder();

		sb.AppendLine($"{reply["role"]}/{reply["state"]}"
			+ (reply["outline"]?.GetValue<bool>() == true ? " outline" : "")
			+ $"   background {reply["background"]}   text {reply["text"]}")
		  .AppendLine();

		foreach (var step in reply["cascade"]?.AsArray() ?? new JsonArray())
		{
			if (step is not JsonObject s) continue;
			sb.AppendLine($"    {s["color"],-24} {s["source"]}");
		}

		return sb.ToString();
	}
}
