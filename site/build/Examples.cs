// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace SharpConsoleUI.SiteBuild;

/// <summary>One application or example, as EXAMPLES.md describes it.</summary>
/// <param name="Name">Its heading.</param>
/// <param name="Tagline">The line under the heading.</param>
/// <param name="Hero">Its first screenshot, relative to the site root.</param>
/// <param name="Project">Where it lives, or null for an in-repository example.</param>
/// <param name="Body">The prose paragraphs, as plain text.</param>
/// <param name="Shows">What of SharpConsoleUI it demonstrates.</param>
/// <param name="More">Its other screenshots.</param>
internal sealed record Example(
	string Name,
	string Tagline,
	string? Hero,
	string? Project,
	IReadOnlyList<string> Body,
	IReadOnlyList<string> Shows,
	IReadOnlyList<string> More);

/// <summary>
/// Reads the showcase out of <c>docs/EXAMPLES.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Parsed rather than retyped. The first version of the showcase page held
/// twelve hardcoded names with a one-line caption each, beside a 904-line
/// document carrying taglines, project links, feature lists, and several
/// screenshots per application. The page was a worse copy of a better source,
/// and it would have gone stale the first time either was edited.
/// </para>
/// <para>
/// The structure it relies on is the one EXAMPLES.md already uses: a level-3
/// heading, a tagline, an image, a bold Project link, prose, and a bold
/// "features on show" list. Anything it cannot find is left out rather than
/// guessed, so a section written differently degrades to less detail instead
/// of breaking the build.
/// </para>
/// </remarks>
internal static partial class Examples
{
	[GeneratedRegex(@"!\[[^\]]*\]\(([^)\s]+)")]
	private static partial Regex Image();

	[GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
	private static partial Regex Link();

	/// <summary>Everything under a given level-2 heading.</summary>
	/// <param name="root">The repository root.</param>
	/// <param name="section">The heading to read, e.g. "Real-World Applications".</param>
	internal static List<Example> Read(string root, string section)
	{
		string path = Path.Combine(root, "docs", "EXAMPLES.md");
		var found = new List<Example>();

		if (!File.Exists(path)) return found;

		string[] lines = File.ReadAllLines(path);

		int start = Array.FindIndex(lines, l =>
			l.StartsWith("## ", StringComparison.Ordinal)
			&& l[3..].Trim().Equals(section, StringComparison.OrdinalIgnoreCase));

		if (start < 0) return found;

		int end = Array.FindIndex(lines, start + 1, l => l.StartsWith("## ", StringComparison.Ordinal));
		if (end < 0) end = lines.Length;

		// Each level-3 heading starts an entry.
		for (int i = start + 1; i < end; i++)
		{
			if (!lines[i].StartsWith("### ", StringComparison.Ordinal)) continue;

			int next = i + 1;
			while (next < end && !lines[next].StartsWith("### ", StringComparison.Ordinal)) next++;

			var entry = Parse(lines, i, next);
			if (entry != null) found.Add(entry);

			i = next - 1;
		}

		return found;
	}

	private static Example? Parse(string[] lines, int from, int to)
	{
		string name = lines[from][4..].Trim();

		// A heading like "WebHost — a terminal UI in a browser" carries its own
		// tagline; the name is the part before the dash.
		string? inlineTag = null;
		int dash = name.IndexOf(" — ", StringComparison.Ordinal);

		if (dash > 0)
		{
			inlineTag = name[(dash + 3)..].Trim();
			name = name[..dash].Trim();
		}

		string? tagline = inlineTag;
		string? hero = null;
		string? project = null;
		var body = new List<string>();
		var shows = new List<string>();
		var more = new List<string>();

		bool inShows = false;
		bool inFence = false;

		for (int i = from + 1; i < to; i++)
		{
			string raw = lines[i];
			string t = raw.Trim();

			// Code blocks are install instructions; the site links to the
			// project instead of repeating them.
			if (t.StartsWith("```", StringComparison.Ordinal)) { inFence = !inFence; continue; }
			if (inFence || t.Length == 0 || t == "---") continue;

			if (t.StartsWith("####", StringComparison.Ordinal)) { inShows = false; continue; }

			// Images: the first is the hero, the rest are extra angles.
			var img = Image().Match(t);
			if (img.Success)
			{
				// A table row can hold several.
				foreach (Match m in Image().Matches(t))
				{
					string src = m.Groups[1].Value;

					if (hero == null) { hero = src; continue; }

					// Not the hero again: a section commonly shows its main
					// screenshot once at the top and once more inside the table
					// of views, which made the first two thumbnails identical.
					if (string.Equals(src, hero, StringComparison.OrdinalIgnoreCase)) continue;
					if (more.Contains(src)) continue;

					more.Add(src);
				}

				continue;
			}

			if (t.StartsWith("**Project:**", StringComparison.Ordinal))
			{
				var link = Link().Match(t);
				if (link.Success) project = link.Groups[2].Value;
				continue;
			}

			if (t.StartsWith("**SharpConsoleUI features on show", StringComparison.Ordinal))
			{
				inShows = true;
				continue;
			}

			// Any other bold lead-in ends the features list.
			if (t.StartsWith("**", StringComparison.Ordinal) && t.Contains(":**", StringComparison.Ordinal))
			{
				inShows = false;
				continue;
			}

			if (t.StartsWith("- ", StringComparison.Ordinal))
			{
				if (inShows) shows.Add(Plain(t[2..]));
				continue;
			}

			// A table of screenshots: its rows are captions, already covered by
			// the images pulled out above.
			if (t.StartsWith('|')) continue;

			string text = Plain(t);
			if (text.Length == 0) continue;

			if (tagline == null) tagline = text;
			else body.Add(text);
		}

		if (tagline == null) return null;

		return new Example(name, tagline, Site(hero), project, body, shows,
			[.. more.Select(Site).OfType<string>()]);
	}

	/// <summary>An image path as the site serves it.</summary>
	private static string? Site(string? src) =>
		src == null ? null
			: src.StartsWith("images/", StringComparison.OrdinalIgnoreCase) ? src
			: src.TrimStart('.', '/');

	/// <summary>Markdown's emphasis and links, as the text they carry.</summary>
	private static string Plain(string md)
	{
		string t = Link().Replace(md, "$1");
		t = t.Replace("**", "").Replace("`", "");

		// Italics, without eating a lone asterisk in prose.
		t = Regex.Replace(t, @"(?<!\w)\*([^*]+)\*(?!\w)", "$1");

		return t.Trim();
	}
}

/// <summary>One control, as CONTROLS.md lists it.</summary>
/// <param name="Name">Its type name, which is also its page.</param>
/// <param name="What">What it is, in a few words.</param>
/// <param name="Detail">What it can do.</param>
internal sealed record ControlRow(string Name, string What, string Detail);

/// <summary>A named group of controls.</summary>
internal sealed record ControlGroup(string Name, string Summary, IReadOnlyList<ControlRow> Rows);

/// <summary>
/// Reads the control catalogue out of <c>docs/CONTROLS.md</c>.
/// </summary>
/// <remarks>
/// The categories, their order and their one-line summaries are editorial
/// decisions already made in that document. Globbing docs/controls/*.md gives
/// forty-seven names in alphabetical order and throws all of it away - which
/// is the same mistake the forty-link footer made.
/// </remarks>
internal static partial class ControlCatalogue
{
	[GeneratedRegex(@"^\|\s*\*\*\[([^\]]+)\]\([^)]+\)\*\*\s*\|([^|]*)\|([^|]*)\|")]
	private static partial Regex Row();

	/// <summary>The groups, in the order the document puts them.</summary>
	internal static List<ControlGroup> Read(string root)
	{
		string path = Path.Combine(root, "docs", "CONTROLS.md");
		var groups = new List<ControlGroup>();

		if (!File.Exists(path)) return groups;

		string[] lines = File.ReadAllLines(path);

		string? name = null;
		string summary = "";
		var rows = new List<ControlRow>();

		void Flush()
		{
			if (name != null && rows.Count > 0)
				groups.Add(new ControlGroup(name, summary, [.. rows]));

			rows.Clear();
			summary = "";
		}

		foreach (string line in lines)
		{
			if (line.StartsWith("## ", StringComparison.Ordinal))
			{
				Flush();

				string heading = line[3..].Trim();

				// Only the catalogue sections; the surrounding prose is not one.
				name = heading.EndsWith(" Controls", StringComparison.OrdinalIgnoreCase)
					? heading[..^9]
					: null;

				continue;
			}

			if (name == null) continue;

			var m = Row().Match(line);

			if (m.Success)
			{
				rows.Add(new ControlRow(
					m.Groups[1].Value.Trim(),
					Clean(m.Groups[2].Value),
					Clean(m.Groups[3].Value)));

				continue;
			}

			// The sentence under a heading, before its table starts.
			string t = line.Trim();

			if (summary.Length == 0 && rows.Count == 0 && t.Length > 0
				&& !t.StartsWith('|') && !t.StartsWith('#'))
			{
				summary = Clean(t);
			}
		}

		Flush();

		return groups;
	}

	/// <summary>A table cell as plain text.</summary>
	private static string Clean(string cell)
	{
		string t = Regex.Replace(cell, @"\[([^\]]+)\]\([^)]+\)", "$1");
		return t.Replace("**", "").Replace("`", "").Trim();
	}
}
