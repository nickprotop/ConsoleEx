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
/// A row of cells, column by column.
/// </summary>
/// <remarks>
/// The question this answers is "where did my columns go". Printing the row as
/// text cannot answer it - text is where the information was lost - so every
/// column gets a line of its own, and a wide glyph is visibly two.
/// </remarks>
internal static class CellFormatter
{
	internal static string Render(JsonNode reply)
	{
		var rows = reply["rows"]?.AsArray();
		if (rows == null || rows.Count == 0) return "no cells\n";

		var sb = new StringBuilder();

		int originX = reply["x"]?.GetValue<int>() ?? 0;
		int originY = reply["y"]?.GetValue<int>() ?? 0;

		for (int r = 0; r < rows.Count; r++)
		{
			var cells = rows[r]?.AsArray();
			if (cells == null) continue;

			if (rows.Count > 1 || originY != 0)
				sb.Append("row ").Append(originY + r).Append('\n');

			Row(sb, cells, originX);

			int wide = cells.Count(c => (c?["w"]?.GetValue<int>() ?? 1) == 2);
			int glyphs = cells.Count(c => c?["ch"] != null);

			sb.Append('\n')
				.Append(glyphs).Append(" glyphs in ").Append(cells.Count).Append(" columns");

			if (wide > 0)
				sb.Append("  (").Append(wide).Append(" wide, taking ").Append(wide * 2).Append(')');

			sb.Append('\n');
			if (r < rows.Count - 1) sb.Append('\n');
		}

		return sb.ToString();
	}

	/// <summary>One line per column: what is there, how wide, what colour.</summary>
	private static void Row(StringBuilder sb, JsonArray cells, int originX)
	{
		for (int i = 0; i < cells.Count; i++)
		{
			var c = cells[i];
			if (c == null) continue;

			sb.Append(string.Format("{0,5}  ", originX + i));

			// A continuation cell has no character of its own. Saying so, rather
			// than repeating the glyph, is the whole point of looking here.
			if (c["cont"]?.GetValue<bool>() == true)
			{
				sb.Append("  <- the column before this one is two wide");
			}
			else
			{
				string ch = c["ch"]?.GetValue<string>() ?? "";
				int w = c["w"]?.GetValue<int>() ?? 1;

				sb.Append(Visible(ch));
				sb.Append(w == 2 ? "  width 2" : "  width 1");

				if (c["comb"]?.GetValue<string>() is { Length: > 0 } comb)
					sb.Append("  + ").Append(comb.Length).Append(" combining");

				if (c["dec"]?.GetValue<string>() is { } dec && dec != "None")
					sb.Append("  ").Append(dec.ToLowerInvariant());
			}

			sb.Append('\n');
		}
	}

	/// <summary>A character that can be read on a terminal line.</summary>
	/// <remarks>
	/// A space is indistinguishable from the padding around it, and a control
	/// character would move the cursor of the terminal doing the printing.
	/// </remarks>
	private static string Visible(string ch)
	{
		if (ch.Length == 0) return "   ";
		if (ch == " ") return "' '";

		char first = ch[0];
		return char.IsControl(first)
			? $"U+{(int)first:X4}"
			: $" {ch} ";
	}
}
