// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace SharpConsoleUI.SiteBuild;

/// <summary>
/// Turns links that work on GitHub into links that work on the site.
/// </summary>
/// <remarks>
/// <para>
/// The markdown is written to be read on GitHub, where a link to
/// <c>THEMES.md</c> resolves to the file beside it. On the site that file is
/// <c>THEMES.html</c>, and an image at <c>images/x.png</c> sits at a different
/// depth. Both are mechanical to fix, and fixing them is what lets the markdown
/// stay untouched.
/// </para>
/// <para>
/// Only relative links are touched. An absolute URL is someone else's and is
/// left exactly as written.
/// </para>
/// </remarks>
internal static partial class Links
{
	[GeneratedRegex(@"(href|src)=""([^""]+)""")]
	private static partial Regex Attr();

	/// <summary>Rewrites every relative link in a rendered page.</summary>
	/// <param name="html">The rendered markdown.</param>
	/// <param name="sourceRel">The source file's path under <c>docs/</c>.</param>
	/// <param name="depth">How deep the OUTPUT page sits, for <c>../</c> counting.</param>
	internal static string Rewrite(string html, string sourceRel, int depth)
	{
		string dir = Path.GetDirectoryName(sourceRel)?.Replace('\\', '/') ?? "";

		return Attr().Replace(html, m =>
		{
			string attr = m.Groups[1].Value;
			string url = m.Groups[2].Value;

			if (Absolute(url)) return m.Value;

			// A fragment alone stays where it points.
			if (url.StartsWith('#')) return m.Value;

			string path = url;
			string frag = "";

			int hash = path.IndexOf('#', StringComparison.Ordinal);
			if (hash >= 0) { frag = path[hash..]; path = path[..hash]; }

			if (path.Length == 0) return m.Value;

			// Resolve against the SOURCE file's directory, so a link written
			// from docs/controls/X.md to ../THEMES.md lands in the right place.
			string resolved = Normalize(dir.Length == 0 ? path : dir + "/" + path);

			// A path that walked above docs/ is not on the site at all - it is
			// a file in the repository - so it goes to GitHub whatever its
			// extension. Checked BEFORE the .md rule, which would otherwise
			// turn "../Examples/WebHost/README.md" into a site page that does
			// not exist.
			if (resolved.StartsWith("..", StringComparison.Ordinal))
			{
				string outside = resolved.TrimStart('.', '/');
				return $"{attr}=\"https://github.com/nickprotop/ConsoleEx/blob/master/{outside}{frag}\"";
			}

			// A link to another document becomes a link to its page.
			if (resolved.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
			{
				resolved = resolved[..^3] + ".html";
				return $"{attr}=\"{Up(depth)}docs/{resolved}{frag}\"";
			}

			// Images and other assets under docs/ are copied to the site root.
			if (resolved.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
				return $"{attr}=\"{Up(depth)}{resolved}{frag}\"";

			// Anything else - a link up into the repository, say - points at
			// GitHub, because the site does not carry the source tree.
			if (resolved.StartsWith("../", StringComparison.Ordinal) || path.StartsWith("../", StringComparison.Ordinal))
			{
				string clean = resolved.TrimStart('.', '/');
				return $"{attr}=\"https://github.com/nickprotop/ConsoleEx/blob/master/{clean}{frag}\"";
			}

			return m.Value;
		});
	}

	/// <summary>Whether a URL belongs to someone else.</summary>
	private static bool Absolute(string url) =>
		url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
		|| url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
		|| url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
		|| url.StartsWith("//", StringComparison.Ordinal)
		|| url.StartsWith('/');

	/// <summary>How far back to the site root.</summary>
	private static string Up(int depth) => depth == 0 ? "" : string.Concat(Enumerable.Repeat("../", depth));

	/// <summary>Collapses <c>.</c> and <c>..</c> without touching the filesystem.</summary>
	private static string Normalize(string path)
	{
		var parts = new List<string>();

		foreach (string seg in path.Split('/'))
		{
			if (seg.Length == 0 || seg == ".") continue;

			if (seg == "..")
			{
				// A ".." that walks above docs/ is kept, so the caller can see
				// it points outside the site and send it to GitHub instead.
				if (parts.Count > 0 && parts[^1] != "..") parts.RemoveAt(parts.Count - 1);
				else parts.Add("..");

				continue;
			}

			parts.Add(seg);
		}

		return string.Join('/', parts);
	}
}
