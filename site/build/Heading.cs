// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace SharpConsoleUI.SiteBuild;

/// <summary>
/// The id Markdig gives a heading, worked out from the heading's text.
/// </summary>
/// <remarks>
/// <para>
/// A second implementation of someone else's slug rules is a thing that can
/// drift, so this exists only because the alternative is worse: the link has
/// to be written on the showcase page, which never parses the rendered
/// EXAMPLES.html and so cannot read the id out of it.
/// </para>
/// <para>
/// Matches Markdig's auto-identifier: lower case, runs of anything that is
/// not a letter or digit collapsed to a single dash, no leading or trailing
/// dash. Verified against the ids in the built page rather than against the
/// documentation of the rule.
/// </para>
/// </remarks>
internal static class Heading
{
	/// <summary>The anchor for a heading's text.</summary>
	internal static string Slug(string text)
	{
		var sb = new StringBuilder(text.Length);

		foreach (char c in text)
		{
			if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
			else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
		}

		return sb.ToString().Trim('-');
	}
}
