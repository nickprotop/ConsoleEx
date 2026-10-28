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
/// Puts a control's screenshot in the same terminal frame the rest of the
/// site uses for one.
/// </summary>
/// <remarks>
/// <para>
/// The home and showcase pages frame every screenshot: a title bar, three
/// dots, the image below. The forty-odd control pages did not, so the same
/// picture of the same terminal looked like a bordered image in one place and
/// a terminal in another.
/// </para>
/// <para>
/// Only screenshots of controls. A diagram, a logo or a photograph in a
/// document is not a terminal, and putting a title bar on one would be a lie
/// about what is being shown.
/// </para>
/// </remarks>
internal static partial class Shots
{
	/// <summary>An image whose source is a control screenshot.</summary>
	/// <remarks>
	/// Matched on the path rather than on the page it appears in, so a
	/// screenshot referenced from somewhere other than its own control page is
	/// framed the same way.
	/// </remarks>
	[GeneratedRegex(
		"""<p><img src="([^"]*images/controls/([^"/]+)\.png)" alt="([^"]*)"\s*/?></p>""",
		RegexOptions.Singleline)]
	private static partial Regex ControlShot();

    /// <summary>Wraps each control screenshot in a terminal frame.</summary>
    /// <param name="html">The rendered document.</param>
    internal static string Frame(string html) =>
		ControlShot().Replace(html, m =>
		{
			string src = m.Groups[1].Value;
			string name = m.Groups[2].Value;
			string alt = m.Groups[3].Value;

			// The alt text is already escaped - it came out of Markdig - so it
			// is used as it stands rather than escaped a second time.
			return "<div class=\"term term-shot\"><div class=\"term-bar\">"
				+ "<span class=\"term-dot r\"></span><span class=\"term-dot y\"></span>"
				+ "<span class=\"term-dot g\"></span>"
				+ $"<span class=\"term-title\">{name}</span></div>"
				+ $"<img src=\"{src}\" alt=\"{alt}\" loading=\"lazy\"></div>";
		});
}
