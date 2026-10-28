// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.Json.Nodes;

namespace Cxdbg.Commands;

/// <summary>
/// A list of records, as a table.
/// </summary>
/// <remarks>
/// <para>
/// Six verbs answer with an array of uniform objects - reachable, windows,
/// events, logs, commands, theme - and every one of them used to print as raw
/// JSON. Columns are the union of the records' keys, in the order they first
/// appear, so the protocol decides them rather than a list kept here.
/// </para>
/// <para>
/// No borders. A debugger printing thirty log lines inside a box is harder to
/// read than one printing thirty aligned lines, and the box is most of the
/// ink.
/// </para>
/// </remarks>
internal static class Rows
{
	/// <summary>How wide a cell may be before it is cut.</summary>
	/// <remarks>
	/// A guid or a long log message reflows a table into a paragraph, which is
	/// unreadable. Cut it and say so with an ellipsis; --json gives the whole
	/// value when the whole value is what you want.
	/// </remarks>
	private const int MaxCell = 44;

	/// <summary>The ordinal down the left margin.</summary>
	private const string Ordinal = "grey35";

	/// <summary>The column headings.</summary>
	private const string Heading = "grey";

	/// <summary>A cell that names something another verb accepts.</summary>
	private const string Identifier = "cyan";

	/// <summary>An ordinary value.</summary>
	private const string Value = "silver";

	/// <summary>A heading field above the table, and its value.</summary>
	private const string LabelName = "grey";

	private const string LabelValue = "silver";

	/// <summary>A cell that reads as absent.</summary>
	private const string Absent = "grey35";

	/// <summary>Columns whose cells address a control, and so are handed back.</summary>
	/// <remarks>
	/// The id is the whole point of a diagnostics table: you read one and pass
	/// it to another verb. Colouring it apart from the values around it is what
	/// makes it findable in forty lines of output.
	/// </remarks>
	private static readonly string[] Addressable =
		["id", "control", "name", "title", "window", "role", "verb", "category"];

	/// <summary>Renders the array, under whatever the reply said beside it.</summary>
	/// <param name="body">The whole reply, for the fields around the array.</param>
	/// <param name="array">The records.</param>
	internal static string Render(JsonObject body, JsonArray array) => Render(body, array, colour: false);

	/// <summary>Renders the array, with colour when it will be seen.</summary>
	/// <param name="body">The whole reply, for the fields around the array.</param>
	/// <param name="array">The records.</param>
	/// <param name="colour">Whether colour reaches a person.</param>
	/// <remarks>
	/// Colour is applied to each cell AFTER it is padded, never before. The
	/// widths are computed from the text a person sees; wrapping a cell in
	/// markup first would add its tags to the measured length and tear every
	/// column to the right of it out of alignment.
	/// </remarks>
	internal static string Render(JsonObject body, JsonArray array, bool colour)
	{
		// An array of bare values - the verbs a server knows, a list of names -
		// is a list, not a table. Columns would be one unnamed column.
		if (array.Count > 0 && array[0] is not JsonObject) return List(body, array, Name(body, array), colour);

		var sb = new StringBuilder();

		// What the reply said beside the list - which window, how many, how big
		// - reads as a heading for it. Nested objects included: `control` keeps
		// its bounds and props in them, and dropping them left the verb that
		// describes a control saying nothing about its size.
		foreach (var (name, value) in body)
		{
			if (name == "ok" || value is JsonArray) continue;

			if (value is JsonObject o)
			{
				if (o.Count == 0) continue;

				sb.Append(Tint(name, LabelName, colour)).Append(' ');

				if (Rect(o) is { } rect) { sb.AppendLine(Tint(rect, LabelValue, colour)); continue; }

				bool first = true;
				foreach (var (field, v) in o)
				{
					if (v is JsonArray or JsonObject) continue;

					sb.Append(first ? "" : "  ")
					  .Append(Tint(field, LabelName, colour)).Append(' ')
					  .Append(Tint(Output.Plainly(v), LabelValue, colour));
					first = false;
				}

				sb.AppendLine();
				continue;
			}

			sb.Append(Tint(name, LabelName, colour)).Append(' ')
			  .AppendLine(Tint(Output.Plainly(value), LabelValue, colour));
		}

		if (sb.Length > 0) sb.AppendLine();

		var columns = Columns(array);
		if (columns.Count == 0) return sb.ToString();

		Redundant(array, columns);

		// Written out rather than rendered through Spectre's Table: that one
		// fits itself to a terminal width, which is right for a report and
		// wrong here - piped, it assumed eighty columns and squeezed a guid
		// into four-character slices. A debugger's table is read and grepped,
		// so the columns are as wide as the widest cell and the line is as
		// long as it needs to be.
		var cells = new List<string[]>();

		// Which row the reply says is the one in force. `theme` names the active
		// theme above a list that then gave no sign of which it was, so reading
		// it meant scanning the list for a string from the heading.
		int current = -1;

		foreach (var row in array)
		{
			if (row is not JsonObject record) continue;

			var line = new string[columns.Count + 1];
			line[0] = (cells.Count + 1).ToString();
			for (int i = 0; i < columns.Count; i++) line[i + 1] = Cell(record, columns[i]);

			cells.Add(line);
			if (Current(body, record)) current = cells.Count - 1;
		}

		var widths = new int[columns.Count + 1];
		widths[0] = cells.Count.ToString().Length;
		for (int i = 0; i < columns.Count; i++) widths[i + 1] = columns[i].Length;

		foreach (var line in cells)
		{
			for (int i = 0; i < line.Length; i++) widths[i] = Math.Max(widths[i], line[i].Length);
		}

		sb.Append(new string(' ', widths[0] + 3));

		for (int i = 0; i < columns.Count; i++)
		{
			sb.Append(i == 0 ? "" : "  ")
			  .Append(Tint(columns[i].PadRight(widths[i + 1]), Heading, colour));
		}
		sb.AppendLine();

		int at = 0;
		foreach (var line in cells)
		{
			bool here = at == current;

			sb.Append(here ? Tint("*", Identifier, colour) : " ")
			  .Append(Tint(line[0].PadLeft(widths[0]), here ? Identifier : Ordinal, colour))
			  .Append("  ");
			for (int i = 1; i < line.Length; i++)
			{
				string style = line[i] == "-" ? Absent
					: Addressable.Contains(columns[i - 1]) ? Identifier
					: Value;

				sb.Append(i == 1 ? "" : "  ")
				  .Append(Tint(line[i].PadRight(widths[i]), style, colour));
			}
			sb.AppendLine();

			// Whatever this record kept in an array of its own, indented
			// beneath it: the detail a summary row cannot carry.
			if (array[at] is JsonObject record) Nested(sb, record, colour);
			at++;
		}

		return TrimRight(sb.ToString());
	}

	/// <summary>Drops the padding that fell off the end of each line.</summary>
	/// <remarks>
	/// Coloured, a line ends in "[/]" rather than in the spaces that padded its
	/// last cell, so a plain TrimEnd finds nothing to take and leaves the
	/// padding sitting INSIDE the tag. The close tag is lifted off, the text
	/// trimmed, and the tag put back.
	/// </remarks>
	private static string TrimRight(string text) =>
		string.Join('\n', text.Split('\n').Select(l =>
			l.EndsWith("[/]", StringComparison.Ordinal)
				? l[..^3].TrimEnd() + "[/]"
				: l.TrimEnd()));

	/// <summary>A list of plain values, wrapped to the terminal.</summary>
	/// <remarks>
	/// Twenty-three verb names down the left margin is a column of waste; they
	/// read as a paragraph, the way `ls` prints names.
	/// </remarks>
	private static string List(JsonObject body, JsonArray array, string? label, bool colour)
	{
		var sb = new StringBuilder();

		foreach (var (name, value) in body)
		{
			if (name == "ok" || value is JsonArray or JsonObject) continue;
			sb.Append(Tint(name, LabelName, colour)).Append(' ')
			  .AppendLine(Tint(Output.Plainly(value), LabelValue, colour));
		}

		if (label != null) sb.Append(Tint(label, Heading, colour)).AppendLine();

		int width = 0;
		foreach (var item in array)
		{
			string text = Output.Plainly(item);

			// 76 so it still fits an eighty-column terminal with the indent.
			if (width > 0 && width + text.Length + 2 > 76)
			{
				sb.AppendLine();
				width = 0;
			}

			if (width == 0) { sb.Append("  "); width = 2; }
			else { sb.Append(", "); width += 2; }

			sb.Append(Tint(text, Identifier, colour));
			width += text.Length;
		}

		if (width > 0) sb.AppendLine();
		return sb.ToString();
	}

	/// <summary>Prints a record's own list of records under it.</summary>
	/// <remarks>
	/// One level only. Deeper than that is a structure to read with --json,
	/// not a table, and the indentation stops meaning anything.
	/// </remarks>
	private static void Nested(StringBuilder sb, JsonObject record, bool colour)
	{
		foreach (var (name, value) in record)
		{
			if (value is not JsonArray inner || inner.Count == 0) continue;
			if (inner[0] is not JsonObject) continue;

			// The detail rows share one set of widths, so the nested block reads
			// as a table under its row rather than a run of ragged lines. The
			// values differ in length far more than the field names do, and an
			// unaligned second column was the whole complaint.
			var names = new List<string>();
			foreach (var item in inner)
			{
				if (item is not JsonObject fields) continue;

				foreach (var (field, v) in fields)
				{
					if (v is JsonArray or JsonObject) continue;
					if (!names.Contains(field)) names.Add(field);
				}
			}

			if (names.Count == 0) continue;

			var widths = new int[names.Count];
			for (int i = 0; i < names.Count; i++) widths[i] = names[i].Length;

			foreach (var item in inner)
			{
				if (item is not JsonObject fields) continue;

				for (int i = 0; i < names.Count; i++)
				{
					if (!fields.TryGetPropertyValue(names[i], out var v) || v is JsonArray or JsonObject)
						continue;

					widths[i] = Math.Max(widths[i], Output.Plainly(v).Length);
				}
			}

			foreach (var item in inner)
			{
				if (item is not JsonObject fields) continue;

				sb.Append("     ");

				for (int i = 0; i < names.Count; i++)
				{
					string text = fields.TryGetPropertyValue(names[i], out var v)
						&& v is not (JsonArray or JsonObject) && v != null
							? Output.Plainly(v)
							: "-";

					string style = text == "-" ? Absent
						: Addressable.Contains(names[i]) ? Identifier
						: Value;

					// The last column is not padded: there is nothing to its right
					// to line up with, and the padding is trailing whitespace.
					bool last = i == names.Count - 1;

					sb.Append(i == 0 ? "" : "  ")
					  .Append(Tint(names[i], Heading, colour)).Append(' ')
					  .Append(Tint(last ? text : text.PadRight(widths[i]), style, colour));
				}

				sb.AppendLine();
			}
		}
	}

	/// <summary>Wraps text in a style, or leaves it alone.</summary>
	/// <remarks>
	/// The text is escaped first: a control named "[x]" or a log message with
	/// a bracket in it is DATA, and Spectre would read it as a tag and either
	/// swallow it or fail the whole render.
	/// </remarks>
	private static string Tint(string text, string style, bool colour) =>
		colour ? $"[{style}]{text.Replace("[", "[[").Replace("]", "]]")}[/]" : text;

	/// <summary>A rectangle written the way people say one, or null.</summary>
	/// <remarks>
	/// w and h are the question; x and y are where it is. "90x30 at 0,0" says
	/// both in less room than one of the four columns it replaces.
	/// </remarks>
	/// <summary>A rectangle as text, for a caller outside this class.</summary>
	internal static string? AsRect(JsonObject o) => Rect(o);

	private static string? Rect(JsonObject o)
	{
		if (o.Count != 4) return null;

		foreach (string key in (string[])["x", "y", "w", "h"])
		{
			if (!o.TryGetPropertyValue(key, out var v) || v is not JsonValue) return null;
		}

		return $"{o["w"]}x{o["h"]} at {o["x"]},{o["y"]}";
	}

	/// <summary>The field an array came from, when it reads as a label.</summary>
	private static string? Name(JsonObject body, JsonArray array)
	{
		foreach (var (name, value) in body)
		{
			if (ReferenceEquals(value, array)) return name;
		}

		return null;
	}

	/// <summary>Whether this record is the one the reply named as current.</summary>
	/// <remarks>
	/// Matched structurally rather than by a list of verb names: a scalar
	/// beside the array whose value equals one of this record's values means
	/// the reply is pointing at it. `theme` says theme "ModernGray" above a
	/// list of themes; `windows` marks the active window the same way without
	/// anything here knowing either verb.
	/// </remarks>
	private static bool Current(JsonObject body, JsonObject record)
	{
		foreach (var (name, value) in body)
		{
			if (name == "ok" || value is JsonArray or JsonObject) continue;

			string text = Output.Plainly(value);

			// Only a name-like value identifies a row. A count or a flag may
			// equal a cell by coincidence and means nothing.
			if (text.Length < 2 || bool.TryParse(text, out _) || double.TryParse(text, out _))
				continue;

			foreach (var (field, v) in record)
			{
				if (v is JsonArray or JsonObject) continue;
				if (Output.Plainly(v) == text) return true;
			}
		}

		return false;
	}

	/// <summary>A timestamp as the time of day, or the text unchanged.</summary>
	/// <remarks>
	/// Log entries carry a full round-trip timestamp - date, seven fractional
	/// digits and an offset - which is 33 characters saying the same date on
	/// every row. What a debugger reads is the time of day and the gap between
	/// two lines, so that is what is shown, to milliseconds. --json still
	/// carries the whole thing.
	/// </remarks>
	private static string Clock(string text)
	{
		// Cheap reject first: only an ISO-8601-looking string is worth parsing,
		// and a table of ordinary values should not pay for a date parse per
		// cell.
		if (text.Length < 19 || text[4] != '-' || text[10] != 'T') return text;

		return DateTimeOffset.TryParse(text,
			System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.None, out var when)
				? when.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture)
				: text;
	}

	/// <summary>Drops a column that says exactly what an earlier one says.</summary>
	/// <remarks>
	/// `reachable` answers with control, id and type, where control is
	/// "Name ?? TypeName". Most controls are unnamed, so control and type held
	/// the same string in every row and a third of the table was a copy of
	/// another third. Compared across the WHOLE array rather than per row: a
	/// column that differs anywhere is carrying information and stays. --json
	/// is unaffected, which is where the protocol's own shape belongs.
	/// </remarks>
	private static void Redundant(JsonArray array, List<string> columns)
	{
		for (int i = columns.Count - 1; i > 0; i--)
		{
			for (int j = 0; j < i; j++)
			{
				bool same = true;

				foreach (var row in array)
				{
					if (row is not JsonObject record) continue;
					if (Cell(record, columns[i]) == Cell(record, columns[j])) continue;

					same = false;
					break;
				}

				if (!same) continue;

				columns.RemoveAt(i);
				break;
			}
		}
	}

	/// <summary>Every key the records use, in the order they first appear.</summary>
	private static List<string> Columns(JsonArray array)
	{
		var columns = new List<string>();

		foreach (var row in array)
		{
			if (row is not JsonObject record) continue;

			foreach (var (name, value) in record)
			{
				// A nested object of plain values flattens into its own
				// columns: `windows` keeps a window's bounds in one, and
				// dropping it hid the most useful fact about a window - its
				// size - from the verb that exists to report windows.
				if (value is JsonObject nested)
				{
					// A rectangle reads as one thing, not four columns of
					// numbers: `windows` carries two of them and the row was
					// 190 characters before this.
					if (Rect(nested) != null)
					{
						if (!columns.Contains(name)) columns.Add(name);
						continue;
					}

					foreach (var (inner, innerValue) in nested)
					{
						if (innerValue is JsonArray or JsonObject) continue;

						string column = $"{name}.{inner}";
						if (!columns.Contains(column)) columns.Add(column);
					}

					continue;
				}

				// An array of RECORDS in a cell is a table inside a table, which
				// is printed under its row instead - `invalidations` keeps the
				// controls that asked for a frame in one, and dropping it left
				// "total 26" without the answer to which control.
				if (value is JsonArray) continue;
				if (!columns.Contains(name)) columns.Add(name);
			}
		}

		return columns;
	}

	/// <summary>One cell, cut to width and safe to print as markup.</summary>
	private static string Cell(JsonObject record, string column)
	{
		// A rectangle, as one cell.
		if (record[column] is JsonObject box && Rect(box) is { } rect) return rect;

		// A flattened column, as "bounds.w".
		int dot = column.IndexOf('.', StringComparison.Ordinal);
		if (dot > 0)
		{
			return record[column[..dot]] is JsonObject nested
				&& nested.TryGetPropertyValue(column[(dot + 1)..], out var inner)
				&& inner != null
					? Output.Plainly(inner)
					: "-";
		}

		if (!record.TryGetPropertyValue(column, out var value) || value == null) return "-";

		string text = Clock(Output.Plainly(value));
		return text.Length > MaxCell ? text[..(MaxCell - 1)] + "…" : text;
	}
}
