// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace SharpConsoleUI.Parsing
{
	public static partial class MarkupParser
	{
		internal const string MarkdownTagOpen = "[markdown]";

		/// <summary>
		/// Replaces each <c>[markdown]…[/]</c> region with its native-markup translation
		/// (via <see cref="MarkdownToMarkup"/>, using <see cref="Configuration.MarkdownStyle.Default"/>).
		/// Content without the tag is returned unchanged (fast path). An unclosed tag renders
		/// everything after the opening tag as Markdown to end-of-string.
		/// </summary>
		private static string PreProcessMarkdownTags(string markup, Configuration.MarkdownStyle? style = null)
		{
			// Null → the built-in default (preserves prior behavior for callers that don't pass a style).
			style ??= Configuration.MarkdownStyle.Default;

			if (string.IsNullOrEmpty(markup)
				|| markup.IndexOf(MarkdownTagOpen, StringComparison.OrdinalIgnoreCase) < 0)
				return markup;

			var sb = new StringBuilder(markup.Length);
			int i = 0;
			int len = markup.Length;

			while (i < len)
			{
				int open = markup.IndexOf(MarkdownTagOpen, i, StringComparison.OrdinalIgnoreCase);
				if (open < 0)
				{
					sb.Append(markup, i, len - i);
					break;
				}

				sb.Append(markup, i, open - i); // text before the tag, verbatim

				// Escaped [[markdown]] — not a real tag. Emit the literal "[markdown]" and continue.
				if (open > 0 && markup[open - 1] == '[')
				{
					sb.Append(MarkdownTagOpen);          // the literal "[markdown]" text
					i = open + MarkdownTagOpen.Length;   // continue scanning after it
					continue;
				}

				int innerStart = open + MarkdownTagOpen.Length;
				int closeIdx = markup.IndexOf("[/]", innerStart, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					// Unclosed [markdown]: treat everything after the tag as Markdown to end-of-string.
					sb.Append(MarkdownToMarkup.Convert(markup.Substring(innerStart), style));
					break;
				}

				string inner = markup.Substring(innerStart, closeIdx - innerStart);
				sb.Append(MarkdownToMarkup.Convert(inner, style));

				i = closeIdx + 3; // skip past the closing [/]
			}

			return sb.ToString();
		}

		/// <summary>
		/// Returns true if <paramref name="text"/> contains a real <c>[markdown]</c> open whose matching
		/// <c>[/]</c> does not appear within <paramref name="text"/> — i.e. the region continues into
		/// following content. Mirrors <see cref="PreProcessMarkdownTags"/>'s scan: <c>[[markdown]]</c> is an
		/// escaped literal (not a tag) and the first <c>[/]</c> after an open closes it.
		/// </summary>
		internal static bool HasUnclosedMarkdownRegion(string text)
		{
			if (string.IsNullOrEmpty(text)
				|| text.IndexOf(MarkdownTagOpen, StringComparison.OrdinalIgnoreCase) < 0)
				return false;

			int i = 0;
			int len = text.Length;
			bool open = false; // whether we are currently inside an unclosed [markdown] region

			while (i < len)
			{
				int tag = text.IndexOf(MarkdownTagOpen, i, StringComparison.OrdinalIgnoreCase);
				if (tag < 0)
					break;

				// Escaped [[markdown]] — not a real tag.
				if (tag > 0 && text[tag - 1] == '[')
				{
					i = tag + MarkdownTagOpen.Length;
					continue;
				}

				int innerStart = tag + MarkdownTagOpen.Length;
				int closeIdx = text.IndexOf("[/]", innerStart, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					open = true; // this open has no close within the text
					break;
				}

				open = false;     // this open was closed
				i = closeIdx + 3; // continue scanning after the [/]
			}

			return open;
		}
	}
}
