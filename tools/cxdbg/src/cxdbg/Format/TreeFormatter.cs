// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.Json.Nodes;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Cxdbg.Format;

/// <summary>
/// Renders a layout tree as something a person can scan.
/// </summary>
/// <remarks>
/// <para>
/// Drawn with Spectre's tree, so the guides are real: a control's ancestry can
/// be traced down the page by following one line, which indentation alone
/// cannot do once a window is deep.
/// </para>
/// <para>
/// Colour carries meaning rather than decoration. A zero extent and a hidden
/// node are the two states that make a control present in the tree and absent
/// from the screen, and they are red because that is the bug someone is
/// usually looking for. Everything else stays plain so that the red means
/// something.
/// </para>
/// <para>
/// Every line names something another verb accepts, so reading the tree and
/// asking a follow-up question are the same gesture.
/// </para>
/// </remarks>
public static class TreeFormatter
{
	/// <summary>
	/// What each kind of thing is coloured.
	/// </summary>
	/// <remarks>
	/// One colour per kind of fact, so a reader learns the scheme once and
	/// then scans for a colour rather than reading every word: ids are what
	/// another verb accepts, sizes are what layout decided, and the two are
	/// different questions. Red is reserved for the states that make a control
	/// invisible, so red always means "here is your bug".
	/// </remarks>
	private const string Id = "cyan";
	private const string Size = "white";
	private const string LayoutName = "magenta";
	private const string FactName = "grey";
	private const string FactValue = "silver";
	private const string Broken = "red";

	/// <summary>Renders the reply from the <c>tree</c> verb.</summary>
	/// <param name="reply">The reply.</param>
	public static string Render(JsonNode reply) => Render(reply, colour: false);

	/// <summary>Renders the reply, with colour when it will reach a person.</summary>
	/// <param name="reply">The reply.</param>
	/// <param name="colour">Whether to colour it.</param>
	public static string Render(JsonNode reply, bool colour)
	{
		var root = new Tree(Desktop(reply["desktop"] as JsonObject)).Style(Style.Plain);

		if (reply["windows"] is JsonArray windows)
		{
			foreach (var w in windows)
			{
				if (w is JsonObject win) AddWindow(root, win);
			}
		}

		return Surface.Render(root, colour);
	}

	/// <summary>The desktop, as the thing everything else sits on.</summary>
	private static Markup Desktop(JsonObject? desktop)
	{
		if (desktop == null) return new Markup("[bold]desktop[/]");

		var sb = new StringBuilder();
		sb.Append("[bold]desktop[/]  ")
			.Append(desktop["width"]).Append('x').Append(desktop["height"]);

		// A modal takes the keyboard and a portal takes the frame. Both are
		// invisible in a screenshot and decisive for input, so they are said
		// where they cannot be missed.
		if (desktop["modals"]?.GetValue<bool>() == true) sb.Append("  [yellow][[modal open]][/]");
		if (desktop["portals"]?.GetValue<bool>() == true) sb.Append("  [yellow][[portal open]][/]");

		sb.AppendLine();
		sb.Append(Facts(desktop));

		return new Markup(sb.ToString());
	}

	/// <summary>One window and everything under it.</summary>
	private static void AddWindow(Tree root, JsonObject win)
	{
		var sb = new StringBuilder();

		sb.Append("[bold]")
			.Append((win["title"]?.GetValue<string>() ?? "(untitled)").EscapeMarkup())
			.Append("[/]");

		var bounds = win["bounds"] as JsonObject;
		if (bounds != null)
			sb.Append($"  {bounds["w"]}x{bounds["h"]} at {bounds["x"]},{bounds["y"]}");

		// Where that lands on SCREEN, when it differs. A window at y:0 under a
		// top panel has its title bar on row 1, and clicking `bounds` rather
		// than `absolute` is the commonest way to miss.
		if (win["absolute"] is JsonObject a
			&& (a["x"]?.GetValue<int>() != bounds?["x"]?.GetValue<int>()
				|| a["y"]?.GetValue<int>() != bounds?["y"]?.GetValue<int>()))
		{
			sb.Append($"  [dim](screen {a["x"]},{a["y"]})[/]");
		}

		if (win["state"]?["active"]?.GetValue<bool>() == true) sb.Append("  [green][[active]][/]");
		if (win["state"]?["modal"]?.GetValue<bool>() == true) sb.Append("  [yellow][[modal]][/]");

		// Everything the window reports about itself, under its own line
		// rather than as a child of it: whether it can be moved, resized or
		// closed is a fact ABOUT the window, and giving it a tree connector
		// would read as another window inside this one.
		string about = Facts(win["state"] as JsonObject);
		string identity = Identity(win);

		if (identity.Length > 0) about = about.Length > 0 ? $"{identity}  {about}" : identity;

		var node = root.AddNode(Lines(sb.ToString(), about));

		AddNode(node, win["node"]);
	}

	/// <summary>A window's id and root layout, which name it to other verbs.</summary>
	private static string Identity(JsonObject win)
	{
		var parts = new List<string>();

		// Coloured like any other fact, because they ARE facts about the
		// window - and the id is what another verb accepts, so it reads in
		// the same colour an id reads everywhere else.
		if (win["id"]?.GetValue<string>() is { } id)
			parts.Add($"[{FactName}]id[/] [{Id}]{id.EscapeMarkup()}[/]");

		if (win["root"]?.GetValue<string>() is { } root)
			parts.Add($"[{FactName}]root[/] [{LayoutName}]{root.EscapeMarkup()}[/]");

		return string.Join("  ", parts);
	}

	/// <summary>One control, its size, and its own children.</summary>
	private static void AddNode(IHasTreeNodes parent, JsonNode? node)
	{
		if (node is not JsonObject o) return;

		var sb = new StringBuilder();

		// The id, because it is what another verb accepts; the bare type only
		// when there is no id, as for the synthetic root.
		string name = o["id"]?.GetValue<string>() ?? o["control"]?.GetValue<string>() ?? "(node)";
		sb.Append($"[{Id}]{name.EscapeMarkup()}[/]");

		var desired = o["desired"];
		var bounds = o["bounds"];

		if (desired != null && bounds != null)
		{
			bool zero = bounds["w"]?.GetValue<int>() == 0 || bounds["h"]?.GetValue<int>() == 0;

			// The gap between what it asked for and what it got is where a
			// layout bug lives, so both are shown and the words between them
			// stay quiet.
			sb.Append($"   [dim]wants[/] [{Size}]{desired["w"]}x{desired["h"]}[/]")
				.Append("  [dim]got[/] ");

			// The one state that makes a control present in the tree,
			// reachable by focus, and invisible on screen.
			sb.Append(zero
				? $"[{Broken}]{bounds["w"]}x{bounds["h"]}[/]"
				: $"[{Size}]{bounds["w"]}x{bounds["h"]}[/]");

			if (zero) sb.Append($"  [{Broken}]<-- zero extent, invisible[/]");
		}

		if (o["absolute"] is JsonObject abs && bounds is JsonObject rel
			&& (abs["x"]?.GetValue<int>() != rel["x"]?.GetValue<int>()
				|| abs["y"]?.GetValue<int>() != rel["y"]?.GetValue<int>()))
		{
			sb.Append($"  [dim](screen {abs["x"]},{abs["y"]})[/]");
		}

		if (o["layout"]?.GetValue<string>() is { } layout)
			sb.Append($"   [{LayoutName}][[{layout.EscapeMarkup()}]][/]");

		if (o["visible"]?.GetValue<bool>() == false) sb.Append($"   [{Broken}](hidden)[/]");

		// What the control IS, and how it was asked to lay out. Both are sent
		// on every reply and neither used to be shown: "why is my control not
		// responding" is answered by enabled:no.
		var self = parent.AddNode(Lines(
			sb.ToString(),
			Facts(o["state"] as JsonObject),
			Facts(o["props"] as JsonObject)));

		if (o["children"] is JsonArray kids)
		{
			foreach (var k in kids) AddNode(self, k);
		}

		// Say where the walk stopped. Without it a --depth cut looks exactly
		// like a leaf, and a reader concludes a control has no children when
		// it has forty.
		if (o["childrenElided"]?.GetValue<int>() is > 0 and var more)
			self.AddNode(new Markup($"[dim]... {more} more, ask deeper[/]"));
	}

	/// <summary>
	/// A heading and the facts under it, as one tree node.
	/// </summary>
	/// <remarks>
	/// The facts belong to the node, not beneath it: rendered as children they
	/// took a tree connector each and read as further controls inside the one
	/// being described.
	/// </remarks>
	private static IRenderable Lines(string heading, params string[] details)
	{
		var rows = new List<IRenderable> { new Markup(heading) };

		foreach (string detail in details)
		{
			if (detail.Length > 0) rows.Add(new Markup(detail));
		}

		return rows.Count == 1 ? rows[0] : new Rows(rows);
	}

	/// <summary>
	/// Everything a block carries, as one line.
	/// </summary>
	/// <remarks>
	/// EVERYTHING. A formatter that decides which half of a reply is
	/// interesting is a formatter that hides the half you needed - this one
	/// dropped the whole desktop, every window flag and every control's state,
	/// which made `tree --props` look broken rather than this.
	/// </remarks>
	private static string Facts(JsonObject? o)
	{
		if (o == null || o.Count == 0) return "";

		var parts = new List<string>();

		foreach (var (name, value) in o)
		{
			// A nested structure belongs to --json; a line of it inside a tree
			// would be unreadable either way.
			if (value is JsonArray or JsonObject) continue;

			// Name and value in different colours, so a row of a dozen facts
			// can be scanned for the one that matters rather than read.
			string shown = Readable(value).EscapeMarkup();

			string style = value is JsonValue b && b.TryGetValue(out bool flag) && !flag
				? FactName          // a "no" is quiet unless something else says otherwise
				: FactValue;

			parts.Add($"[{FactName}]{name.EscapeMarkup()}[/] [{style}]{shown}[/]");
		}

		return string.Join("  ", parts);
	}

	/// <summary>
	/// A value the way a person reads one.
	/// </summary>
	/// <remarks>
	/// Quotes belong to JSON, not to someone scanning a tree, and a null that
	/// means "not set" reads better as a dash. Nothing is hidden: an unset
	/// width is still shown, because "it has no explicit width" is an answer
	/// to why it is the size it is.
	/// </remarks>
	private static string Readable(JsonNode? value)
	{
		if (value == null) return "-";

		if (value is JsonValue v)
		{
			if (v.TryGetValue(out string? text)) return text.Length == 0 ? "\"\"" : OneLine(text);
			if (v.TryGetValue(out bool b)) return b ? "yes" : "no";
		}

		return value.ToJsonString();
	}

	/// <summary>
	/// A value on one line, with nothing in it read as markup.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A control's <c>text</c> is its SOURCE, markup and all: a label reads
	/// <c>[bold rgb(0,255,255)]  [[-]] Layout[/]</c> in the reply. Those tags
	/// are the APPLICATION's, and they appeared as stray <c>[/]</c> in a tree
	/// that uses the same syntax for its own colouring.
	/// </para>
	/// <para>
	/// Escaped rather than stripped. Stripping needs a parser that can tell a
	/// style tag from literal text, and it cannot: <c>[[-]]</c> is the glyph a
	/// collapsed node really shows, and a regex ate it. Escaping needs to know
	/// nothing - what the application wrote is what appears, and the tree's own
	/// colours stay the tree's.
	/// </para>
	/// <para>
	/// Newlines become a glyph: a control whose text has three lines must not
	/// take three lines of a tree, and dropping them silently would hide that
	/// the text is multi-line at all.
	/// </para>
	/// </remarks>
	private static string OneLine(string text)
	{
		string flat = text
			.Replace("\r\n", "\u00b6")
			.Replace('\n', '\u00b6')
			.Replace('\t', ' ');

		// Cut rather than wrap: a paragraph inside a tree node is unreadable,
		// and --json has the whole of it.
		if (flat.Length > 60) flat = flat[..59] + "\u2026";

		return flat;
	}
}
