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
/// Gives a blockquote that opens with a label the styling that label implies.
/// </summary>
/// <remarks>
/// <para>
/// The documentation already writes these: <c>&gt; **Note:**</c>,
/// <c>&gt; **Warning:**</c>, and a dozen one-off labels besides. They render as
/// an ordinary quote, so a warning looks exactly like an aside, and a page of
/// reference text gives no clue which paragraphs are the ones that bite.
/// </para>
/// <para>
/// Matched on what is written rather than on GitHub's <c>[!NOTE]</c> syntax,
/// which this documentation does not use. Converting the prose to that syntax
/// would be a larger change than styling what is already there, and would read
/// worse in a plain markdown viewer.
/// </para>
/// </remarks>
internal static partial class Callouts
{
	/// <summary>A blockquote whose first content is a bolded run.</summary>
	[GeneratedRegex(
		"""<blockquote>\s*<p><strong>([^<]{1,40}?):?</strong>""",
		RegexOptions.Singleline)]
	private static partial Regex Labelled();

	/// <summary>
	/// The kinds worth telling apart, and the words that mean each.
	/// </summary>
	/// <remarks>
	/// Deliberately short. A label this does not know keeps the default
	/// blockquote, which is the behaviour every one of them has today - so an
	/// unrecognised label is no worse off for this existing.
	/// </remarks>
	private static readonly (string Kind, string[] Words)[] Kinds =
	[
		("warning", ["warning", "caution", "danger", "important", "breaking"]),
		("tip",     ["tip", "hint", "remember"]),
		("note",    ["note", "platform", "new to sharpconsoleui?"]),
	];

	/// <summary>Classes a labelled blockquote so the stylesheet can colour it.</summary>
	internal static string Apply(string html) =>
		Labelled().Replace(html, m =>
		{
			string label = m.Groups[1].Value.Trim();
			string kind = KindOf(label);

			if (kind.Length == 0) return m.Value;

			// The label becomes its own line, so the first sentence starts where
			// every other callout's does rather than running on from the word.
			return $"""<blockquote class="{kind}"><p><span class="callout-label">{label}</span>""";
		});

	/// <summary>Which kind a label names, or empty when it names none.</summary>
	private static string KindOf(string label)
	{
		string lower = label.ToLowerInvariant();

		foreach (var (kind, words) in Kinds)
		{
			foreach (string word in words)
			{
				if (lower.StartsWith(word, StringComparison.Ordinal)) return kind;
			}
		}

		return string.Empty;
	}
}
