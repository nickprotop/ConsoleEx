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
	/// <summary>What each kind of thing is coloured. Matches the tree's.</summary>
	private const string Id = "cyan";
	private const string Size = "white";
	private const string LayoutName = "magenta";
	private const string Quiet = "grey";
	private const string Culprit = "yellow";

	/// <summary>Why a control is the size it is.</summary>
	/// <param name="reply">The reply.</param>
	public static string RenderSize(JsonNode reply) => RenderSize(reply, colour: false);

	/// <summary>Why a control is the size it is, coloured when it will be seen.</summary>
	/// <param name="reply">The reply.</param>
	/// <param name="colour">Whether colour reaches a person.</param>
	public static string RenderSize(JsonNode reply, bool colour)
	{
		var text = BuildSize(reply);
		return colour ? Surface.Render(new Spectre.Console.Markup(text), true) : Strip(text);
	}

	/// <summary>The cascade as markup, coloured or not.</summary>
	private static string BuildSize(JsonNode reply)
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

			// The id, because it is what another verb accepts; the name is
			// only what it is called.
			sb.AppendLine($"[{Id}]{Safe(o["id"] ?? o["control"])}[/]")
			  .AppendLine($"  [{Quiet}]offered[/] [{Size}]{Extent(o["offeredWidth"])}x{Extent(o["offeredHeight"])}[/]"
				+ $"   [{Quiet}]wants[/] [{Size}]{o["desiredWidth"]}x{o["desiredHeight"]}[/]")
			  .AppendLine();

			// Measured across the cascade rather than fixed: an id is a type
			// name plus a hash, so a long control name overran a hardcoded 24
			// and swallowed the gap that separated it from the layout column -
			// "HorizontalGridControl#21093c0 GridLayout", read as one word.
			var links = (o["cascade"]?.AsArray() ?? new JsonArray())
				.OfType<JsonObject>().ToList();

			int whoWidth = 0, layoutWidth = 0;

			foreach (var l in links)
			{
				whoWidth = Math.Max(whoWidth, Safe(Who(l)).Length);
				layoutWidth = Math.Max(layoutWidth, Safe(l["layout"]).Length);
			}

			foreach (var link in links)
			{
				if (link is not JsonObject l) continue;

				// Each link names the container that narrowed the offer, and
				// that container is the next thing a reader asks about - so it
				// is named by what `control` and `measure` will accept.
				string who = Who(l);
				var marks = new List<string>();
				if (l["setsWidth"]?.GetValue<bool>() == true) marks.Add("sets width");
				if (l["setsHeight"]?.GetValue<bool>() == true) marks.Add("sets height");

				// The container that set the size is the ANSWER - the one line
				// someone ran this verb to find - so it is the one thing that
				// stands out.
				bool decides = marks.Count > 0;

				sb.Append("    ")
				  .Append($"[{Id}]{Safe(who).PadRight(whoWidth)}[/]  ")
				  .Append($"[{LayoutName}]{Safe(l["layout"]).PadRight(layoutWidth)}[/]  ")
				  .Append($"[{Quiet}]offers[/] ")
				  .Append(decides
					  ? $"[{Culprit}]{l["offeredWidth"]}x{l["offeredHeight"]}[/]"
					  : $"[{Size}]{l["offeredWidth"]}x{l["offeredHeight"]}[/]");

				if (decides)
					sb.Append($"   [{Culprit}]<== {string.Join(" and ", marks)}[/]");

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
	public static string RenderColor(JsonNode reply) => RenderColor(reply, colour: false);

	/// <summary>How a role's colour was resolved, coloured when it will be seen.</summary>
	/// <param name="reply">The reply.</param>
	/// <param name="colour">Whether colour reaches a person.</param>
	public static string RenderColor(JsonNode reply, bool colour)
	{
		string text = BuildColor(reply);
		return colour ? Surface.Render(new Spectre.Console.Markup(text), true) : Strip(text);
	}

	/// <summary>The colour cascade as markup.</summary>
	private static string BuildColor(JsonNode reply)
	{
		var sb = new StringBuilder();

		sb.Append($"[{Id}]{Safe(reply["role"])}/{Safe(reply["state"])}[/]")
		  .Append(reply["outline"]?.GetValue<bool>() == true ? $" [{Quiet}]outline[/]" : "")
		  .Append($"   [{Quiet}]background[/] {Swatch(reply["background"])}")
		  .AppendLine($"   [{Quiet}]text[/] {Swatch(reply["text"])}")
		  .AppendLine();

		foreach (var step in reply["cascade"]?.AsArray() ?? new JsonArray())
		{
			if (step is not JsonObject s) continue;

			// Padded by what is SEEN, not by the markup's length: the tags
			// are invisible and counting them puts every column somewhere
			// different.
			string swatch = Swatch(s["color"]);
			int visible = Strip(swatch).Length;

			sb.Append("    ").Append(swatch)
			  .Append(new string(' ', Math.Max(1, 32 - visible)))
			  .AppendLine($"[{Quiet}]{Safe(s["source"])}[/]");
		}

		return sb.ToString();
	}

	/// <summary>
	/// A colour shown IN that colour, beside its numbers.
	/// </summary>
	/// <remarks>
	/// The one verb whose answer can be shown rather than described: a block
	/// painted in the resolved colour says in one glance what
	/// <c>Color(185, 114, 114)</c> takes a moment to picture. The numbers stay,
	/// because a debugger that only showed the swatch could not be compared
	/// against a theme file.
	/// </remarks>
	private static string Swatch(JsonNode? value)
	{
		string text = Safe(value);

		var m = System.Text.RegularExpressions.Regex.Match(
			text, @"(\d{1,3})\D+(\d{1,3})\D+(\d{1,3})");

		if (!m.Success) return text;

		string rgb = $"rgb({m.Groups[1].Value},{m.Groups[2].Value},{m.Groups[3].Value})";

		// Two blocks, enough to judge a colour without taking a column.
		return $"[{rgb}]\u2588\u2588[/] [{Quiet}]{text}[/]";
	}

	/// <summary>A value that cannot be read as markup.</summary>
	/// <summary>What a cascade link is called, by something another verb accepts.</summary>
	/// <remarks>
	/// The id first: it is what `control` and `measure` take. The bare type is
	/// a fallback for a link that has none, and "(root)" for the window itself.
	/// </remarks>
	private static string Who(JsonObject link) =>
		link["id"]?.GetValue<string>()
			?? link["control"]?.GetValue<string>()
			?? "(root)";

	private static string Safe(JsonNode? value) =>
		(value is JsonValue v && v.TryGetValue(out string? t) ? t : value?.ToJsonString() ?? "")
			.Replace("[", "[[").Replace("]", "]]");

	/// <summary>The same text with the markup taken back out.</summary>
	/// <remarks>
	/// One template, rendered or stripped, so the coloured and the plain
	/// output cannot say different things.
	/// </remarks>
	private static string Strip(string markup) =>
		// Doubled brackets are protected first: "[[active]]" is the literal
		// text "[active]", and a tag-stripping pass cannot tell it from a
		// style without that. Restored last, so nothing it protects is seen
		// by the stripper.
		System.Text.RegularExpressions.Regex
			.Replace(
				markup.Replace("[[", "\u0001").Replace("]]", "\u0002"),
				@"\[/\]|\[[a-z][^\]]*\]", "")
			.Replace("\u0001", "[").Replace("\u0002", "]");
}
