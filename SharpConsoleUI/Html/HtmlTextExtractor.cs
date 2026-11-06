// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Turns a run of laid-out lines back into the text the document said.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A page is laid out for a viewport, and the lines on screen are not the
	/// lines the author wrote: a paragraph becomes a dozen of them, broken
	/// wherever the width ran out. Copying those verbatim puts a hard newline
	/// every viewport width through text that was written as one paragraph,
	/// which is the common way a paste is ruined.
	/// </para>
	/// <para>
	/// So a line that says it continues is joined to the next with a space, and
	/// one that does not is joined with a newline. That distinction is recorded
	/// by the layout rather than guessed here: see
	/// <see cref="LayoutLine.ContinuesNextLine"/>.
	/// </para>
	/// </remarks>
	internal static class HtmlTextExtractor
	{
		/// <summary>
		/// Extracts the text of a selection spanning one or more lines.
		/// </summary>
		/// <param name="lines">The laid-out lines, in document order.</param>
		/// <param name="firstLine">Index of the line the selection starts on.</param>
		/// <param name="firstColumn">First selected cell on that line, inclusive.</param>
		/// <param name="lastLine">Index of the line the selection ends on.</param>
		/// <param name="lastColumn">Cell after the last selected one, exclusive.</param>
		/// <returns>
		/// The selected text, with wrapped lines rejoined. Empty when the
		/// selection covers nothing.
		/// </returns>
		internal static string Extract(LayoutLine[] lines, int firstLine, int firstColumn,
			int lastLine, int lastColumn)
		{
			if (lines == null || lines.Length == 0)
				return string.Empty;

			if (firstLine < 0) firstLine = 0;
			if (lastLine > lines.Length - 1) lastLine = lines.Length - 1;
			if (firstLine > lastLine)
				return string.Empty;

			var text = new StringBuilder();

			// What to put before the next line's text, decided by the last line
			// that actually carried any. A picture contributes neither text nor a
			// separator, so a sentence wrapped around one still joins across it.
			string? pending = null;

			for (int i = firstLine; i <= lastLine; i++)
			{
				ref var line = ref lines[i];

				// A rendered picture is pixel cells: copying the glyphs would
				// paste a row of half-blocks, and its alt text was never kept.
				if (line.IsImage)
					continue;

				int start = i == firstLine ? firstColumn : 0;
				int end = i == lastLine ? lastColumn : line.Cells.Length;

				if (start < 0) start = 0;
				if (end > line.Cells.Length) end = line.Cells.Length;

				if (pending != null)
					text.Append(pending);

				for (int x = start; x < end; x++)
				{
					// A wide glyph occupies two cells and the second is a
					// placeholder, not a character of its own.
					if (line.Cells[x].IsWideContinuation)
						continue;

					text.Append(line.Cells[x].Character.ToString());
				}

				if (!line.ContinuesNextLine)
				{
					pending = "\n";
					continue;
				}

				// A break between words consumed a space that belongs to the text,
				// so it is put back. A break inside a token - a code line or a
				// single word too long for the viewport - consumed nothing, and
				// inventing a space there corrupts what is pasted: a command or an
				// identifier comes out with gaps the author never typed.
				if (!line.BrokeBetweenWords)
				{
					pending = string.Empty;
					continue;
				}

				// The wrap often leaves its space on the line already, and adding
				// another would double it in every pasted paragraph.
				pending = text.Length > 0 && text[^1] == ' ' ? string.Empty : " ";
			}

			return text.ToString();
		}
	}
}
