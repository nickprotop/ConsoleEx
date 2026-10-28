// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;

namespace SharpConsoleUI.SiteBuild;

/// <summary>
/// The "on this page" list, built from the headings a document already has.
/// </summary>
/// <remarks>
/// <para>
/// Markdig gives every heading an id, so the links exist whether or not
/// anything points at them. This turns them into a list, and a hover anchor on
/// the heading itself, so a section can be linked to rather than described.
/// </para>
/// <para>
/// Read out of the rendered HTML rather than the markdown: the ids are
/// Markdig's, and regenerating them here would be a second implementation of
/// its slug rules that could only ever drift from the first.
/// </para>
/// </remarks>
internal static partial class Toc
{
	/// <summary>Headings shallow enough to be worth listing.</summary>
	/// <remarks>
	/// Two and three. One is the page title, and four is detail inside a
	/// section - a list that deep stops being a map and becomes the document
	/// again.
	/// </remarks>
	[GeneratedRegex("""<h([23]) id="([^"]+)">(.*?)</h[23]>""", RegexOptions.Singleline)]
	private static partial Regex Heading();

	/// <summary>Anything inside a heading that is markup rather than words.</summary>
	[GeneratedRegex("<.*?>", RegexOptions.Singleline)]
	private static partial Regex Tags();

	/// <summary>How few headings make a list not worth the column.</summary>
	/// <remarks>
	/// Two. A page with one section is its own table of contents, and a list of
	/// one entry is furniture.
	/// </remarks>
	private const int Fewest = 2;

	/// <summary>
	/// The aside for a document, or empty when it has too few headings.
	/// </summary>
	internal static string Render(string html)
	{
		var found = Heading().Matches(html);
		if (found.Count < Fewest) return string.Empty;

		var sb = new StringBuilder();

		sb.Append("<aside class=\"doc-toc\"><div class=\"toc-title\">On this page</div><nav>");

		foreach (Match m in found)
		{
			string level = m.Groups[1].Value;
			string id = m.Groups[2].Value;
			string text = Text(m.Groups[3].Value);

			if (text.Length == 0) continue;

			sb.Append($"<a class=\"lv{level}\" href=\"#{id}\">{text}</a>");
		}

		sb.Append("</nav></aside>");

		return sb.ToString();
	}

	/// <summary>
	/// Adds a hover anchor to every heading that has an id.
	/// </summary>
	/// <remarks>
	/// Inside the heading, after the text, so it inherits the heading's line
	/// box and cannot push the layout around when it appears.
	/// </remarks>
	internal static string Anchor(string html) =>
		Heading().Replace(html, m =>
			$"<h{m.Groups[1].Value} id=\"{m.Groups[2].Value}\">{m.Groups[3].Value}"
			+ $"<a class=\"anchor\" href=\"#{m.Groups[2].Value}\" "
			+ $"aria-label=\"Link to this section\">#</a></h{m.Groups[1].Value}>");

	/// <summary>A heading's words, with its markup and entities taken out.</summary>
	/// <remarks>
	/// Escaped again on the way out: a heading containing <c>&lt;T&gt;</c>
	/// arrives as an entity, and decoding it without re-escaping would put a
	/// raw angle bracket into the list.
	/// </remarks>
	private static string Text(string inner)
	{
		string plain = Tags().Replace(inner, string.Empty);

		return System.Net.WebUtility.HtmlEncode(
			System.Net.WebUtility.HtmlDecode(plain).Trim());
	}
}
