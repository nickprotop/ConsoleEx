// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cxdbg.Format;

namespace Cxdbg.Commands;

/// <summary>
/// Turns a reply into something to read.
/// </summary>
/// <remarks>
/// <para>
/// ONE place, called by the command line and the prompt alike. The rules used
/// to live in three command classes with slightly different switches, which is
/// why `show tree` drew a tree and `show reachable` printed raw JSON for no
/// reason a reader could predict.
/// </para>
/// <para>
/// A formatter is chosen by the SHAPE of the reply, not by a list of verbs,
/// with four exceptions where the shape alone is not enough (a cascade and a
/// tree are both nested, and mean different things). Everything else follows
/// from what came back - so a verb added to the protocol tomorrow is rendered
/// sensibly by a build that has never heard of it.
/// </para>
/// </remarks>
internal static class Output
{
	/// <summary>
	/// Warns about a field that looks like a misspelling of one the verb takes.
	/// </summary>
	/// <remarks>
	/// A warning, never a refusal. Unknown fields are passed through on purpose -
	/// an application newer than its debugger reads fields this build has never
	/// heard of - but that same tolerance means `--compose` for `--composed` is
	/// accepted in silence and answers a different question, which is worse than
	/// either refusing it or saying so.
	/// </remarks>
	internal static void WarnAboutLikelyTypos(string verb, IEnumerable<string> fields)
	{
		foreach (string typed in fields)
		{
			if (Commands.ProtocolVerbs.NearestField(verb, typed) is not { } meant) continue;

			Spectre.Console.AnsiConsole.MarkupLine(
				$"[yellow]'--{typed}' is not a field of '{verb}'; did you mean "
				+ $"[blue]--{meant}[/]?[/] Sending it anyway.");
		}
	}

	/// <summary>Renders a reply for a caller that said how it wants it.</summary>
	/// <param name="verb">The verb that was sent.</param>
	/// <param name="reply">What came back.</param>
	/// <param name="line">The command line, for --json.</param>
	/// <returns>Text to print, newline-terminated.</returns>
	internal static string Render(string verb, JsonNode reply, CommandLine line) =>
		line.Json ? Json(reply) : Pretty(verb, reply, Surface.Colour(line.NoColor));

	/// <summary>The reply as the protocol sent it.</summary>
	internal static string Json(JsonNode reply) =>
		reply.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";

	/// <summary>The reply, rendered to read.</summary>
	internal static string Pretty(string verb, JsonNode reply) => Pretty(verb, reply, colour: false);

	/// <summary>The reply, rendered to read, with colour when it will be seen.</summary>
	/// <param name="verb">The verb that was sent.</param>
	/// <param name="reply">What came back.</param>
	/// <param name="colour">Whether colour reaches a person.</param>
	internal static string Pretty(string verb, JsonNode reply, bool colour)
	{
		if (reply is not JsonObject body) return Json(reply);

		// A refusal is an answer, and burying it in an object with ok:false
		// makes it read like output. Checked first, for every verb.
		if (body["ok"]?.GetValue<bool>() == false) return Error(body);

		// A handful whose answer is a sentence, not a field. {"pong":true}
		// rendered as "pong true" says less than the word it stands for, and
		// {"sent":1} does not say what it sent one of.
		switch (verb.ToLowerInvariant())
		{
			case "ping": return "alive\n";
			case "size": return $"{body["w"]}x{body["h"]}\n";
			case "key": return $"sent {body["sent"]} key\n";
			case "tree": return TreeFormatter.Render(reply, colour);
			case "measure": return CascadeFormatter.RenderSize(reply, colour);
			case "color": return CascadeFormatter.RenderColor(reply, colour);
			case "snapshot": return Snapshot(body);
			case "image": return Image(body);
			case "inspect": return Inspect(body, colour);
			case "control": return ControlReply(body, colour);
			case "focus": return FocusReply(body, colour);
			case "settled": return SettledReply(body, colour);
			case "clock": return ClockReply(body, colour);

			// Grouped, because these replies answer several questions at once
			// and a flat column cannot say which number belongs to which.
			case "metrics":
				return Grouped(body, colour,
					("written", ["bytesWritten", "charactersChanged", "ansiSequences",
						"cursorMovements", "dirtyCellsMarked", "cellsRendered"]),
					("cost", ["layoutMs", "ansiMs", "outputMs"]),
					("frame", ["frame", "efficiency", "staticFrame"]));

			// The configured rate, what was achieved, where the time went, and what
			// went on the wire - four questions, and a flat column of twenty
			// numbers cannot say which answer belongs to which.
			case "performance":
				return Grouped(body, colour,
					("rate", ["achievedFps", "targetFps", "minFrameMs", "bound", "frameRateLimiting"]),
					("frame time", ["minMs", "meanMs", "p95Ms", "maxMs"]),
					("cost per frame", ["layoutMs", "ansiMs", "outputMs", "compareMs", "busyPercent"]),
					("wire", ["bytesPerSecond", "bytesWritten", "cellsRendered", "cellsMarked", "efficiency"]),
					("window", ["frames", "totalFrames", "windowMs",
						"clocksFrozen", "costNote", "note"]));

			case "set":
				return Grouped(body, colour,
					("recording", ["diagnostics", "diagnosticsAvailable", "recordingInput", "layers"]),
					("logging", ["logging", "logLevel", "logCount"]),
					("appearance", ["theme"]));

			case "desktop":
				return Grouped(body, colour,
					("screen", ["width", "height", "originX", "originY"]),
					("panels", ["topPanelRows", "bottomPanelRows", "desktopBottom"]),
					("showing", ["windows", "modals", "portals", "theme", "driver"]));
		}

		// Then the shape. An array of objects is a table; a short flat object
		// is one fact; a longer one is a list of fields.
		// A table builds its own aligned text, where markup would be counted
		// as width and break every column.
		if (Array(body) is { } array)
		{
			string table = Rows.Render(body, array, colour);

			return colour
				? Surface.Render(new Spectre.Console.Markup(table), true)
				: table;
		}

		var scalars = Scalars(body);
		string text = (scalars.Count <= 3 ? Fact(scalars) : Fields(scalars)) + Nested(body);

		return colour
			? Surface.Render(new Spectre.Console.Markup(text), true)
			: Plain(text);
	}

	/// <summary>The refusal, in the server's own words.</summary>
	private static string Error(JsonObject body)
	{
		string message = body["error"]?.GetValue<string>() ?? "refused, without saying why";
		string? code = body["code"]?.GetValue<string>();

		var sb = new StringBuilder();
		sb.Append(message);
		if (code != null) sb.Append("  [").Append(code).Append(']');
		sb.AppendLine();

		// A hint is the server telling you what to do next, which is the part
		// worth reading twice.
		if (body["hint"]?.GetValue<string>() is { } hint) sb.Append("  ").AppendLine(hint);

		return sb.ToString();
	}

	/// <summary>A snapshot, in whichever form was asked for.</summary>
	private static string Snapshot(JsonObject body)
	{
		// cells: one entry per column, with the widths that text cannot carry.
		if (body["rows"] != null && body["cells"] != null) return CellFormatter.Render(body);
		if (body["rows"] is JsonArray rows && rows.Count > 0 && rows[0] is JsonArray)
			return CellFormatter.Render(body);

		var lines = body["lines"]?.AsArray();
		if (lines != null)
		{
			var sb = new StringBuilder();
			foreach (var l in lines) sb.AppendLine(l?.GetValue<string>() ?? "");
			return sb.ToString();
		}

		return Json(body);
	}

	/// <summary>
	/// What is under a point, outermost first.
	/// </summary>
	/// <remarks>
	/// The layering IS the answer here, which is why this one verb has a
	/// formatter of its own: "the click does nothing" is usually "you are
	/// hitting the window border, not the button", and that only reads if the
	/// desktop, the window, the region and the control are each on their own
	/// line in that order.
	/// </remarks>
	private static string Inspect(JsonObject body, bool colour)
	{
		var sb = new StringBuilder();

		sb.Append("[grey]at[/] ").Append(Plainly(body["x"])).Append(',').AppendLine(Plainly(body["y"]));

		if (body["cell"] is JsonObject cell)
		{
			string ch = cell["cont"]?.GetValue<bool>() == true
				? "(second half of a wide character)"
				: $"'{Plainly(cell["ch"])}'";

			sb.Append("[grey]  cell      [/]").Append(Escaped(ch));

			// Whatever shape each colour arrived in, and each INDEPENDENTLY: the
			// pair was printed only when BOTH were rgb triples, so a reply that
			// sent them as hex, or sent only one, showed neither - and a cell's
			// colour is half of what inspect is asked for.
			foreach (var (name, value) in cell)
			{
				if (name is "ch" or "cont") continue;

				sb.Append($"  [grey]{name}[/] ")
				  .Append(value is JsonArray rgb ? Hex(rgb) : Escaped(Readable(value)));
			}

			sb.AppendLine();
		}

		string region = Plainly(body["region"]);

		if (body["window"] is JsonObject w)
		{
			sb.Append("[grey]  window    [/]").Append(Escaped(Plainly(w["title"])));
			if (w["active"]?.GetValue<bool>() == true) sb.Append("  [green][[active]][/]");
			sb.AppendLine();

			sb.Append("[grey]  region    [/][yellow]").Append(region).Append("[/]");
			if (body["resize"] != null) sb.Append("  (").Append(Plainly(body["resize"])).Append(')');

			// What a click here would and would not do. A point on the title
			// of a window that cannot move is a click that goes nowhere.
			if (region == "title" && w["movable"]?.GetValue<bool>() == false)
				sb.Append("  - but the window is not movable");

			if (region == "resize" && w["resizable"]?.GetValue<bool>() == false)
				sb.Append("  - but the window is not resizable");

			sb.AppendLine();
		}
		else
		{
			sb.Append("[grey]  region    [/][yellow]").Append(region).AppendLine("[/]");
		}

		if (body["control"] is JsonObject c)
		{
			// The id, not the bare type: it is what another verb accepts, and
			// a type is ambiguous the moment an application has two of them.
			sb.Append("[grey]  control   [/][cyan]").Append(Escaped(Named(c)))
				.Append("[/]  [grey](").Append(Escaped(Plainly(c["control"]))).AppendLine(")[/]");

			// Size, where it is, and where it is ON SCREEN - the last being what
			// `mouse` takes, so it is the number most likely to be copied out.
			if (c["bounds"] is JsonObject b && Rows.AsRect(b) is { } rect)
			{
				sb.Append("            [white]").Append(rect).Append("[/]");

				if (c["desired"] is JsonObject want)
					sb.Append($"  [grey]wants[/] [white]{want["w"]}x{want["h"]}[/]");

				if (c["absolute"] is JsonObject abs)
					sb.Append($"  [grey]on screen at[/] [white]{abs["x"]},{abs["y"]}[/]");

				sb.AppendLine();
			}

			// What it IS and how it was told to lay out. `control` shows both
			// for the same control; dropping them here meant the devtools-style
			// verb said LESS about what it found than the verb you then had to
			// run by hand with the id it just gave you.
			InspectBlock(sb, "state", c["state"] as JsonObject);
			InspectBlock(sb, "props", c["props"] as JsonObject);

			var about = new List<KeyValuePair<string, JsonNode?>>();
			foreach (var (name, value) in c)
			{
				if (value is JsonArray or JsonObject) continue;
				if (name is "control" or "id" or "name" or "window") continue;

				about.Add(new(name, value));
			}

			if (about.Count > 0)
			{
				sb.Append("[grey]  about     [/]").AppendLine(string.Join("  ", about
					.Select(p => $"[grey]{p.Key}[/] [silver]{Escaped(Readable(p.Value))}[/]")));
			}

			// The containers above it, innermost last, so reading down follows
			// the offer as it narrows.
			if (c["parents"] is JsonArray parents && parents.Count > 0)
			{
				sb.Append("[grey]  inside    [/]");
				sb.Append(Chain(parents
					.Select(p => $"[cyan]{Escaped(Named(p!.AsObject()))}[/]").ToList(),
					indent: 12, leaf: false));
			}
		}
		else if (body["window"] != null)
		{
			sb.AppendLine("[grey]  control   [/][yellow]none - this is window chrome[/]");
		}

		if (body["covered"] is JsonArray covered && covered.Count > 0)
		{
			sb.Append("[grey]  under     [/]").AppendLine(string.Join(", ", covered
				.Select(u => Plainly(u!["title"])
					+ (u["modal"]?.GetValue<bool>() == true ? " [modal]" : ""))));
		}

		return colour
			? Surface.Render(new Spectre.Console.Markup(sb.ToString()), true)
			: Plain(sb.ToString());
	}

	/// <summary>
	/// A control reply, with its identity first.
	/// </summary>
	/// <remarks>
	/// The type led the reply and the id sat six lines down, which is the
	/// wrong way round: the id is the thing another verb accepts, and a bare
	/// type cannot be handed back once an application has two of them - which
	/// is most applications.
	/// </remarks>
	private static string ControlReply(JsonObject body, bool colour)
	{
		var sb = new StringBuilder();

		// Identity first, type after it in parentheses. The id is what another
		// verb accepts; the type is what a reader recognises. Both, in that
		// order, because the id alone is a hash and the type alone cannot be
		// handed back.
		sb.Append("[cyan]").Append(Escaped(Named(body)))
			.Append("[/]  [grey](").Append(Escaped(Plainly(body["control"]))).AppendLine(")[/]");

		// The sizes, which are why anyone asks about a control.
		if (body["desired"] is JsonObject want && body["bounds"] is JsonObject got)
		{
			bool zero = got["w"]?.GetValue<int>() == 0 || got["h"]?.GetValue<int>() == 0;

			sb.Append("  [grey]wants[/] [white]").Append($"{want["w"]}x{want["h"]}")
			  .Append("[/]  [grey]got[/] ")
			  .Append(zero ? "[red]" : "[white]").Append($"{got["w"]}x{got["h"]}").Append("[/]")
			  .Append($"  [grey]at {got["x"]},{got["y"]}[/]");

			if (zero) sb.Append("  [red]<-- zero extent, invisible[/]");
			sb.AppendLine();
		}

		// Always, never only when it differs from the window-relative one. The
		// screen coordinate is what `mouse` and `inspect` take, so it is the
		// number most likely to be copied out of this reply - and a formatter
		// that hides a field the protocol sent makes the reader check --json
		// to find out whether it was absent or merely uninteresting.
		if (body["absolute"] is JsonObject abs)
			sb.AppendLine($"  [grey]on screen at[/] [white]{abs["x"]},{abs["y"]}[/]");

		sb.AppendLine();

		// What it IS and how it was asked to lay out, each as its own labelled
		// line rather than a column of names: these are the facts someone came
		// for, and a label says which kind they are reading.
		Block(sb, "state", body["state"] as JsonObject);
		Block(sb, "props", body["props"] as JsonObject);

		var rest = Scalars(body);
		rest.RemoveAll(p => p.Key is "control" or "id" or "name" or "window");

		if (rest.Count > 0)
		{
			sb.Append("  [grey]about[/]  ");
			sb.AppendLine(string.Join("  ", rest.Select(p =>
				$"[grey]{p.Key}[/] [silver]{Escaped(Plainly(p.Value))}[/]")));
		}

		// The containers above it, outermost first, as a chain - the same
		// shape `inspect` shows, because it is the same fact.
		if (body["parents"] is JsonArray parents && parents.Count > 0)
		{
			sb.AppendLine();
			sb.Append("  [grey]inside[/]  ").Append(Chain(parents
				.Select(p => $"[cyan]{Escaped(Named(p!.AsObject()))}[/]").ToList(),
				indent: 10, leaf: false));
		}

		if (body["window"]?.GetValue<string>() is { } window)
			sb.AppendLine($"  [grey]window[/]  {Escaped(window)}");

		return colour
			? Surface.Render(new Spectre.Console.Markup(sb.ToString()), true)
			: Plain(sb.ToString());
	}

	/// <summary>One labelled block of facts, when the control has any.</summary>
	private static void Block(StringBuilder sb, string label, JsonObject? o)
	{
		if (o == null || o.Count == 0) return;

		var parts = new List<string>();

		foreach (var (name, value) in o)
		{
			if (value is JsonArray or JsonObject) continue;

			parts.Add($"[grey]{name}[/] [silver]{Escaped(Readable(value))}[/]");
		}

		if (parts.Count == 0) return;

		sb.Append($"  [grey]{label}[/]").Append(new string(' ', Math.Max(1, 7 - label.Length)))
		  .AppendLine(string.Join("  ", parts));
	}

	/// <summary>A labelled block at inspect's wider gutter.</summary>
	/// <remarks>
	/// Same shape as <see cref="Block"/>, at the ten-column label margin the
	/// rest of an inspect reply uses, so the labels stack in one column.
	/// </remarks>
	private static void InspectBlock(StringBuilder sb, string label, JsonObject? o)
	{
		if (o == null || o.Count == 0) return;

		var parts = new List<string>();

		foreach (var (name, value) in o)
		{
			if (value is JsonArray or JsonObject) continue;

			parts.Add($"[grey]{name}[/] [silver]{Escaped(Readable(value))}[/]");
		}

		if (parts.Count == 0) return;

		sb.Append($"[grey]  {label}[/]").Append(new string(' ', Math.Max(1, 10 - label.Length)))
		  .AppendLine(string.Join("  ", parts));
	}

	/// <summary>A value the way a person reads one.</summary>
	private static string Readable(JsonNode? value)
	{
		if (value == null) return "-";

		if (value is JsonValue v)
		{
			if (v.TryGetValue(out string? t)) return t.Length == 0 ? "\"\"" : t;
			if (v.TryGetValue(out bool b)) return b ? "yes" : "no";
		}

		return value.ToJsonString();
	}

	/// <summary>
	/// What to call a control, preferring what can be handed back.
	/// </summary>
	/// <remarks>
	/// A name if it has one, else the id <c>tree</c> reports, else the bare
	/// type. The point of printing a chain of containers is that you can then
	/// ask about one of them, and a type alone is not something `control` can
	/// find once an application has two of them.
	/// </remarks>
	private static string Named(JsonObject control)
	{
		if (control["name"] is { } name) return Plainly(name);
		if (control["id"] is { } id) return Plainly(id);

		return Plainly(control["control"]);
	}

	/// <summary>Fields in named groups, each under a rule.</summary>
	/// <remarks>
	/// <para>
	/// Some replies are not one list of facts but several. `metrics` answers
	/// with what was written, how efficiently, and how long it took - three
	/// questions whose numbers mean nothing next to each other, run together
	/// into one column of thirteen names. A rule between them says which
	/// question each number answers, which is the thing the column could not.
	/// </para>
	/// <para>
	/// Fields the groups do not claim are printed last, under their own rule,
	/// so a verb that grows a field still shows it rather than dropping it on
	/// the floor.
	/// </para>
	/// </remarks>
	private static string Grouped(JsonObject body, bool colour,
		params (string Title, string[] Fields)[] groups)
	{
		var scalars = Scalars(body);
		var sb = new StringBuilder();
		var shown = new HashSet<string>(StringComparer.Ordinal);
		bool first = true;

		// One width for the whole reply, measured before anything is printed.
		int width = scalars.Count > 0 ? scalars.Max(p => p.Key.Length) : 0;

		// And one number column, for the same reason.
		var column = Widths(scalars);

		foreach (var (title, fields) in groups)
		{
			var mine = scalars.Where(p => fields.Contains(p.Key)).ToList();
			if (mine.Count == 0) continue;

			if (!first) sb.AppendLine();
			first = false;

			sb.AppendLine(Rule(title, width));
			sb.Append(Fields(mine, width, column));

			foreach (var (name, _) in mine) shown.Add(name);
		}

		var rest = scalars.Where(p => !shown.Contains(p.Key)).ToList();

		if (rest.Count > 0)
		{
			if (!first) { sb.AppendLine(); sb.AppendLine(Rule("other", width)); }
			sb.Append(Fields(rest, width, column));
		}

		sb.Append(Nested(body));

		return colour
			? Surface.Render(new Spectre.Console.Markup(sb.ToString()), true)
			: Plain(sb.ToString());
	}

	/// <summary>A group's name, with a hairline carrying it across.</summary>
	/// <remarks>
	/// The line is what makes a rule a rule: a bare word above a block reads as
	/// another field, which is exactly the confusion in `set`, where a group and
	/// a field can share a name. It runs to the width of the name column so the
	/// rules stack into a left margin rather than a ragged edge.
	/// </remarks>
	private static string Rule(string title, int width)
	{
		int run = Math.Max(2, width + 2 - title.Length - 1);

		return $"[grey]{Escaped(title)}[/] [grey35]{new string('─', run)}[/]";
	}

	/// <summary>A descent, inline when it fits and indented when it does not.</summary>
	/// <param name="links">Each step, outermost first, already escaped and styled.</param>
	/// <param name="indent">The margin an indented descent starts at.</param>
	/// <param name="leaf">Whether to mark the last link as the destination.</param>
	/// <remarks>
	/// <para>
	/// A chain of containers is a path, and the only thing it says is depth. On
	/// one line that reads well and costs nothing - until it is longer than the
	/// terminal, at which point the terminal folds it wherever it runs out and
	/// the depth becomes unreadable. `focus` holds six links of forty characters
	/// and wrapped into a block that could not be followed.
	/// </para>
	/// <para>
	/// So: one line when it fits, and a stepped descent when it does not. The
	/// shape changes, what it says does not.
	/// </para>
	/// </remarks>
	private static string Chain(IReadOnlyList<string> links, int indent, bool leaf)
	{
		if (links.Count == 0) return "";

		string inline = string.Join(" [grey]>[/] ", links);

		// Measured on what a person sees, not on the markup: the tags are not
		// printed and counting them would fold a line that fits.
		int width = !Console.IsOutputRedirected && Console.WindowWidth > 20
			? Console.WindowWidth
			: 200;

		if (Plain(inline).Length + indent <= width) return inline + "\n";

		var sb = new StringBuilder();
		sb.AppendLine();

		for (int i = 0; i < links.Count; i++)
		{
			bool last = i == links.Count - 1;

			sb.Append(new string(' ', indent))
			  .Append(new string(' ', i * 2))
			  .Append(last && leaf ? "[grey]`->[/] " : i == 0 ? "" : "[grey]|->[/] ")
			  .AppendLine(links[i]);
		}

		return sb.ToString();
	}

	/// <summary>What has focus, and the descent that reaches it.</summary>
	/// <remarks>
	/// The chain is the answer: focus is a path from the window down, and which
	/// container it passes through is what you are checking. Rendered as a run
	/// of comma-separated names it read as a list of unrelated controls.
	/// </remarks>
	private static string FocusReply(JsonObject body, bool colour)
	{
		var sb = new StringBuilder();

		if (body["window"]?.GetValue<string>() is { } window)
			sb.AppendLine($"[grey]window[/]   [silver]{Escaped(window)}[/]");

		string? focused = body["focused"] is { } f ? Plainly(f) : null;

		sb.Append("[grey]focused[/]  ")
		  .AppendLine(focused is null or "" or "null"
			? "[yellow]nothing has focus[/]"
			: $"[cyan]{Escaped(focused)}[/]");

		if (body["chain"] is JsonArray chain && chain.Count > 0)
		{
			var links = chain
				.Select(l => Plainly(l))
				.Select(l => l == focused
					? $"[cyan]{Escaped(l)}[/]"
					: $"[silver]{Escaped(l)}[/]")
				.ToList();

			sb.AppendLine();
			sb.Append("[grey]chain[/]    ").Append(Chain(links, indent: 2, leaf: true));
		}

		// Anything the reply carries that is not part of the chain story.
		var rest = Scalars(body);
		rest.RemoveAll(p => p.Key is "window" or "focused");

		if (rest.Count > 0)
		{
			sb.AppendLine();
			sb.Append(Fields(rest));
		}

		return colour
			? Surface.Render(new Spectre.Console.Markup(sb.ToString()), true)
			: Plain(sb.ToString());
	}

	/// <summary>Text that cannot be read as markup.</summary>
	/// <remarks>
	/// A window title or a control's name is the APPLICATION's, and this
	/// formatter uses the same bracket syntax for its own colours.
	/// </remarks>
	/// <summary>How wide the whole and fractional parts of a column need to be.</summary>
	/// <remarks>
	/// Non-numeric values are ignored rather than counted: "ModernGray" in a
	/// column of frame times would set a width no number needs, and a theme name
	/// does not line up on a decimal point anyway.
	/// </remarks>
	private static (int Whole, int Fraction) Widths(
		List<KeyValuePair<string, JsonNode?>> scalars)
	{
		int whole = 0, fraction = 0;

		foreach (var (name, value) in scalars)
		{
			string text = Number(name, value);

			int mark = text.IndexOf(Deltas.Marker);
			if (mark >= 0) text = text[..mark];

			if (!double.TryParse(text,
				System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out _)) continue;

			int dot = text.IndexOf('.');

			whole = Math.Max(whole, dot < 0 ? text.Length : dot);
			fraction = Math.Max(fraction, dot < 0 ? 0 : text.Length - dot - 1);
		}

		return (whole, fraction);
	}

	/// <summary>A number padded so its decimal point sits in the column's place.</summary>
	/// <remarks>
	/// Anything that is not a number is returned untouched: padding a word to a
	/// numeric column would indent it for no reason a reader could explain.
	/// </remarks>
	private static string Aligned(string text, (int Whole, int Fraction) column)
	{
		if (column.Whole == 0) return text;

		if (!double.TryParse(text,
			System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture, out _)) return text;

		int dot = text.IndexOf('.');
		int whole = dot < 0 ? text.Length : dot;

		string lead = new(' ', Math.Max(0, column.Whole - whole));

		// A whole number is padded on the right to where the fractions end, so
		// the next column does not step left and right down the page.
		if (dot < 0 && column.Fraction > 0)
			return lead + text + new string(' ', column.Fraction + 1);

		return lead + text;
	}

	private static string Escaped(string text) =>
		text.Replace("[", "[[").Replace("]", "]]");

	/// <summary>The same text with the markup taken back out.</summary>
	/// <remarks>
	/// One template, rendered or stripped, so the coloured output and the
	/// plain one cannot drift into saying different things.
	/// </remarks>
	private static string Plain(string markup) =>
		// Doubled brackets are protected first: "[[active]]" is the literal
		// text "[active]", and a tag-stripping pass cannot tell it from a
		// style without that. Restored last, so nothing it protects is seen
		// by the stripper.
		System.Text.RegularExpressions.Regex
			.Replace(
				markup.Replace("[[", "\u0001").Replace("]]", "\u0002"),
				@"\[/\]|\[[a-z][^\]]*\]", "")
			.Replace("\u0001", "[").Replace("\u0002", "]");

	/// <summary>Three numbers as a colour someone can read.</summary>
	private static string Hex(JsonArray rgb) =>
		rgb.Count < 3 ? "?" : $"#{rgb[0]!.GetValue<int>():x2}{rgb[1]!.GetValue<int>():x2}{rgb[2]!.GetValue<int>():x2}";

	/// <summary>An image, described rather than printed.</summary>
	/// <remarks>
	/// Base64 of a screenshot is tens of kilobytes and no use on a terminal.
	/// The metadata says what was captured; the bytes are written to a file.
	/// </remarks>
	/// <summary>
	/// Whether the application has finished reacting, and what is holding it.
	/// </summary>
	/// <remarks>
	/// The blockers are the point. "working" alone tells a script nothing it can
	/// act on; "working" with "2 window(s) with work pending" says whether to keep
	/// waiting or to go and find out why a window never paints.
	/// </remarks>
	/// <summary>The clocks, or the one that was just changed.</summary>
	/// <remarks>
	/// A frozen clock is the interesting state, so it is the one that gets a colour:
	/// a reader scanning three lines should see at a glance which is not moving.
	/// </remarks>
	private static string ClockReply(JsonObject body, bool colour)
	{
		var sb = new StringBuilder();

		if (body["clocks"] is JsonObject all)
		{
			foreach (var (_, entry) in all)
			{
				if (entry is JsonObject one) sb.AppendLine(ClockLine(one));
			}
		}
		else
		{
			sb.AppendLine(ClockLine(body));
		}

		// Trailing newline kept: every other reply ends a line, and without it two
		// clock calls in a row run together on one.
		string text = sb.ToString().TrimEnd() + "\n";

		return colour
			? Surface.Render(new Spectre.Console.Markup(text), true)
			: Plain(text);
	}

	private static string ClockLine(JsonObject clock)
	{
		string name = clock["clock"]?.GetValue<string>() ?? "?";
		string kind = clock["kind"]?.GetValue<string>() ?? "?";
		string now = clock["now"]?.GetValue<string>() ?? "";

		// The time without the date: a clock is read here for its movement, not
		// for what day it thinks it is.
		int t = now.IndexOf('T');
		string shown = t >= 0 && now.Length > t + 13 ? now.Substring(t + 1, 12) : now;

		return $"[cyan]{Escaped(name),-10}[/] "
			+ (kind == "system" ? "[grey]system[/]" : "[yellow]frozen[/]")
			+ $"  [white]{Escaped(shown)}[/]";
	}

	private static string SettledReply(JsonObject body, bool colour)
	{
		bool settled = body["settled"]?.GetValue<bool>() ?? false;
		long frame = body["frame"]?.GetValue<long>() ?? 0;
		string? blockers = body["blockers"]?.GetValue<string>();

		var sb = new StringBuilder();

		sb.Append(settled ? "[green]settled[/]" : "[yellow]working[/]")
		  .Append("  [grey]frame[/] [white]").Append(frame).Append("[/]");

		if (!string.IsNullOrEmpty(blockers))
			sb.Append("  [grey]").Append(Escaped(blockers)).Append("[/]");

		// Markup is a request, not output: it has to be rendered, or stripped when
		// the caller is a pipe. Returning the string raw prints the tags.
		string text = sb.ToString();

		return colour
			? Surface.Render(new Spectre.Console.Markup(text), true)
			: Plain(text);
	}

	private static string Image(JsonObject body)
	{
		if (body["path"] != null)
		{
			return $"wrote {body["bytes"]} bytes to {body["path"]}  "
				+ $"({body["w"]}x{body["h"]} {body["format"]})\n";
		}

		if (body["data"] != null)
		{
			return $"{body["bytes"]} bytes of {body["format"]} ({body["w"]}x{body["h"]}), not printed.\n"
				+ "  Give --path to write it, or redirect it at the prompt.\n";
		}

		return Fields(Scalars(body));
	}

	/// <summary>One line, for a reply that is one answer.</summary>
	private static string Fact(List<KeyValuePair<string, JsonNode?>> scalars)
	{
		if (scalars.Count == 0) return "ok\n";

		var sb = new StringBuilder();

		foreach (var (name, value) in scalars)
		{
			sb.Append($"[grey]{name}[/] [silver]{Escaped(Plainly(value))}[/]").AppendLine();
		}

		return sb.ToString();
	}

	/// <summary>
	/// A reply's nested objects, each as one line.
	/// </summary>
	/// <remarks>
	/// A rectangle reads as "90x30 at 0,0"; anything else as its own fields.
	/// Dropping them entirely is what hid a window's bounds from `windows` and
	/// a control's size from `control` - in both cases the fact the verb exists
	/// to report.
	/// </remarks>
	private static string Nested(JsonObject body)
	{
		var sb = new StringBuilder();

		foreach (var (name, value) in body)
		{
			if (value is not JsonObject o || o.Count == 0) continue;

			sb.Append($"[grey]{name}[/] ");

			if (Rows.AsRect(o) is { } rect)
			{
				sb.AppendLine($"[silver]{rect}[/]");
				continue;
			}

			bool first = true;
			foreach (var (field, v) in o)
			{
				if (v is JsonArray or JsonObject) continue;

				sb.Append(first ? "" : "  ")
				  .Append($"[grey]{field}[/] [silver]{Escaped(Plainly(v))}[/]");

				first = false;
			}

			sb.AppendLine();
		}

		return sb.ToString();
	}

	/// <summary>A name column and a value column.</summary>
	private static string Fields(List<KeyValuePair<string, JsonNode?>> scalars) =>
		Fields(scalars, 0);

	/// <summary>A name column and a value column, at a width set from outside.</summary>
	/// <param name="scalars">The fields to print.</param>
	/// <param name="width">
	/// The name column's width, or 0 to take it from these fields alone. Grouped
	/// output passes the width of EVERY group, so the value column does not step
	/// left and right as the reader moves down past each rule.
	/// </param>
	private static string Fields(List<KeyValuePair<string, JsonNode?>> scalars, int width,
		(int Whole, int Fraction)? column = null)
	{
		if (scalars.Count == 0) return "ok\n";

		if (width <= 0) width = scalars.Max(p => p.Key.Length);

		// Numbers line up on the decimal point, so a column of them can be read
		// down rather than one at a time. Grouped output passes the measurement
		// for the WHOLE reply, exactly as it does the name width - measuring per
		// group would make each group agree with itself and with nothing else.
		var values = column ?? Widths(scalars);

		var sb = new StringBuilder();

		foreach (var (name, value) in scalars)
		{
			string text = Number(name, value);

			// A watch marks "value<marker>change". The value keeps the colour
			// every other value has and the change is dimmed beside it, so a
			// column of numbers still scans as numbers rather than parentheses.
			// Deliberately not green-up/red-down: faster is better, more bytes
			// is not, and nothing here knows which way is good.
			int mark = text.IndexOf(Deltas.Marker);

			sb.Append($"[grey]{name.PadRight(width + 2)}[/]");

			if (mark < 0)
			{
				sb.AppendLine($"[silver]{Escaped(Aligned(text, values))}[/]");
				continue;
			}

			string number = text[..mark];
			string change = text[(mark + 1)..];

			// Nothing after the marker means it was asked and did not move. Dimmed
			// to the colour of the labels, so a watch reads as a quiet page with
			// the few numbers that changed standing out of it.
			sb.Append(change.Length == 0
				? $"[grey42]{Escaped(Aligned(number, values))}[/]"
				: $"[silver]{Escaped(Aligned(number, values))}[/]");

			sb.AppendLine(change.Length == 0 ? "" : $" [grey42]({Escaped(change)})[/]");
		}

		return sb.ToString();
	}

	/// <summary>A value, with a ratio cut to something a person reads.</summary>
	/// <remarks>
	/// "efficiency 1.1363636363636365" is seventeen digits of float noise
	/// answering a question that wanted two. Only a non-integer number is
	/// touched, and only for display - --json carries what the app sent.
	/// </remarks>
	private static string Number(string name, JsonNode? value)
	{
		if (value is JsonValue v && v.TryGetValue(out double d) && d != Math.Floor(d))
			return d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

		return Plainly(value);
	}

	/// <summary>The first array of objects in a reply, if it has one.</summary>
	private static JsonArray? Array(JsonObject body)
	{
		foreach (var (_, value) in body)
		{
			// Any non-empty array: records become a table, plain values a list.
			// commands answers with an array of verb NAMES, and matching only
			// arrays of objects left it rendering as "ok".
			if (value is JsonArray a && a.Count > 0) return a;
		}

		return null;
	}

	/// <summary>The reply's own fields, without ok and without the array.</summary>
	private static List<KeyValuePair<string, JsonNode?>> Scalars(JsonObject body)
	{
		var list = new List<KeyValuePair<string, JsonNode?>>();

		foreach (var (name, value) in body)
		{
			// ok:true is the envelope, not the answer.
			if (name == "ok") continue;
			if (value is JsonArray or JsonObject) continue;

			list.Add(new(name, value));
		}

		return list;
	}

	/// <summary>A value without JSON's quotes.</summary>
	internal static string Plainly(JsonNode? value) =>
		value is JsonValue v && v.TryGetValue(out string? s) ? s : value?.ToJsonString() ?? "";
}
