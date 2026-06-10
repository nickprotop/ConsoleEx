// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Highlighting;

/// <summary>Per-line parser state for the HTML highlighter (tracks comment/script/style blocks).</summary>
/// <param name="InComment">True when the line begins inside an unterminated HTML comment.</param>
/// <param name="InScript">True when the line begins inside an open &lt;script&gt; block.</param>
/// <param name="InStyle">True when the line begins inside an open &lt;style&gt; block.</param>
public record HtmlLineState(bool InComment = false, bool InScript = false, bool InStyle = false) : SyntaxLineState;

/// <summary>Syntax highlighter for HTML, delegating to JS/CSS inside script/style blocks.</summary>
public class HtmlSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color TagColor = Color.Cyan1;
	private static readonly Color AttrNameColor = Color.DodgerBlue2;
	private static readonly Color AttrValueColor = Color.Orange3;
	private static readonly Color CommentColor = Color.Green;
	private static readonly Color EntityColor = Color.Yellow;
	private static readonly Color DoctypeColor = Color.Grey;
	private static readonly Color ScriptKeywordColor = Color.DodgerBlue2;
	private static readonly Color CssPropertyColor = Color.DodgerBlue2;

	private static readonly JsSyntaxHighlighter JsHighlighter = new();
	private static readonly CssSyntaxHighlighter CssHighlighter = new();

	/// <summary>Tokenizes a single line of HTML for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var state = startState as HtmlLineState ?? new HtmlLineState();
		var tokens = new List<SyntaxToken>();

		// Continue multi-line comment
		if (state.InComment)
		{
			int closeIdx = line.IndexOf("-->", StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				if (line.Length > 0)
					tokens.Add(new SyntaxToken(0, line.Length, CommentColor));
				return (tokens, new HtmlLineState(InComment: true));
			}
			tokens.Add(new SyntaxToken(0, closeIdx + 3, CommentColor));
			return TokenizeFrom(tokens, line, closeIdx + 3, false, state.InScript, state.InStyle);
		}

		// Inside <script> block — delegate to JS highlighter until </script>
		if (state.InScript)
		{
			int closeTag = FindClosingTag(line, 0, "script");
			if (closeTag < 0)
			{
				var (jsTokens, _) = JsHighlighter.Tokenize(line, lineIndex, SyntaxLineState.Initial);
				tokens.AddRange(jsTokens);
				return (tokens, new HtmlLineState(InScript: true));
			}
			if (closeTag > 0)
			{
				string jsPart = line.Substring(0, closeTag);
				var (jsTokens, _) = JsHighlighter.Tokenize(jsPart, lineIndex, SyntaxLineState.Initial);
				tokens.AddRange(jsTokens);
			}
			int tagEnd = TokenizeCloseTag(tokens, line, closeTag);
			return TokenizeFrom(tokens, line, tagEnd, false, false, false);
		}

		// Inside <style> block — delegate to CSS highlighter until </style>
		if (state.InStyle)
		{
			int closeTag = FindClosingTag(line, 0, "style");
			if (closeTag < 0)
			{
				var (cssTokens, _) = CssHighlighter.Tokenize(line, lineIndex, SyntaxLineState.Initial);
				tokens.AddRange(cssTokens);
				return (tokens, new HtmlLineState(InStyle: true));
			}
			if (closeTag > 0)
			{
				string cssPart = line.Substring(0, closeTag);
				var (cssTokens, _) = CssHighlighter.Tokenize(cssPart, lineIndex, SyntaxLineState.Initial);
				tokens.AddRange(cssTokens);
			}
			int tagEnd = TokenizeCloseTag(tokens, line, closeTag);
			return TokenizeFrom(tokens, line, tagEnd, false, false, false);
		}

		return TokenizeFrom(tokens, line, 0, false, false, false);
	}

	private (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		TokenizeFrom(List<SyntaxToken> tokens, string line, int start, bool inComment, bool inScript, bool inStyle)
	{
		int i = start;
		while (i < line.Length)
		{
			// Comment
			if (i + 3 < line.Length && line[i] == '<' && line[i + 1] == '!' && line[i + 2] == '-' && line[i + 3] == '-')
			{
				int closeIdx = line.IndexOf("-->", i + 4, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
					return (tokens, new HtmlLineState(InComment: true));
				}
				tokens.Add(new SyntaxToken(i, closeIdx + 3 - i, CommentColor));
				i = closeIdx + 3;
				continue;
			}

			// DOCTYPE
			if (i + 8 < line.Length && line.AsSpan(i, 9).ToString().Equals("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
			{
				int closeIdx = line.IndexOf('>', i + 9);
				int len = closeIdx >= 0 ? closeIdx + 1 - i : line.Length - i;
				tokens.Add(new SyntaxToken(i, len, DoctypeColor));
				i += len;
				continue;
			}

			// Tags
			if (line[i] == '<')
			{
				bool isCloseTag = i + 1 < line.Length && line[i + 1] == '/';
				int tagNameStart = isCloseTag ? i + 2 : i + 1;

				// Get tag name for script/style detection
				int nameEnd = tagNameStart;
				while (nameEnd < line.Length && !char.IsWhiteSpace(line[nameEnd]) && line[nameEnd] != '>' && line[nameEnd] != '/')
					nameEnd++;
				string tagName = line.Substring(tagNameStart, nameEnd - tagNameStart).ToLowerInvariant();

				int tagEnd = TokenizeTag(tokens, line, i);

				// Check if entering script or style block (opening tag only)
				if (!isCloseTag)
				{
					// Check the character before tagEnd to see if self-closing
					bool selfClosing = tagEnd >= 2 && line[tagEnd - 2] == '/';
					if (!selfClosing)
					{
						if (tagName == "script")
							return TokenizeFrom(tokens, line, tagEnd, false, true, false);
						if (tagName == "style")
							return TokenizeFrom(tokens, line, tagEnd, false, false, true);
					}
				}

				i = tagEnd;
				continue;
			}

			// Entity
			if (line[i] == '&')
			{
				int semiIdx = line.IndexOf(';', i + 1);
				if (semiIdx > i && semiIdx - i <= 10)
				{
					tokens.Add(new SyntaxToken(i, semiIdx + 1 - i, EntityColor));
					i = semiIdx + 1;
					continue;
				}
			}

			i++;
		}

		return (tokens, new HtmlLineState(InScript: inScript, InStyle: inStyle));
	}

	private int TokenizeTag(List<SyntaxToken> tokens, string line, int tagStart)
	{
		int i = tagStart + 1; // skip '<'
		if (i < line.Length && line[i] == '/') i++; // closing tag

		int nameStart = tagStart;
		while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != '>' && line[i] != '/')
			i++;

		tokens.Add(new SyntaxToken(tagStart, i - tagStart, TagColor));

		while (i < line.Length)
		{
			if (char.IsWhiteSpace(line[i])) { i++; continue; }

			if (line[i] == '/' && i + 1 < line.Length && line[i + 1] == '>')
			{
				tokens.Add(new SyntaxToken(i, 2, TagColor));
				return i + 2;
			}
			if (line[i] == '>')
			{
				tokens.Add(new SyntaxToken(i, 1, TagColor));
				return i + 1;
			}

			// Attribute name
			int attrStart = i;
			while (i < line.Length && line[i] != '=' && line[i] != '>' && line[i] != '/' && !char.IsWhiteSpace(line[i]))
				i++;
			if (i > attrStart)
				tokens.Add(new SyntaxToken(attrStart, i - attrStart, AttrNameColor));

			while (i < line.Length && (char.IsWhiteSpace(line[i]) || line[i] == '='))
				i++;

			// Attribute value
			if (i < line.Length && (line[i] == '"' || line[i] == '\''))
			{
				char quote = line[i];
				int valStart = i;
				i++;
				while (i < line.Length && line[i] != quote) i++;
				if (i < line.Length) i++;
				tokens.Add(new SyntaxToken(valStart, i - valStart, AttrValueColor));
			}
		}

		return i;
	}

	private static int FindClosingTag(string line, int start, string tagName)
	{
		int i = start;
		while (i < line.Length)
		{
			int idx = line.IndexOf("</", i, StringComparison.Ordinal);
			if (idx < 0) return -1;

			int nameStart = idx + 2;
			int nameEnd = nameStart;
			while (nameEnd < line.Length && char.IsLetterOrDigit(line[nameEnd])) nameEnd++;
			if (line.AsSpan(nameStart, nameEnd - nameStart).ToString().Equals(tagName, StringComparison.OrdinalIgnoreCase))
				return idx;
			i = nameEnd;
		}
		return -1;
	}

	private int TokenizeCloseTag(List<SyntaxToken> tokens, string line, int start)
	{
		int i = start;
		int nameEnd = i + 2;
		while (nameEnd < line.Length && line[nameEnd] != '>') nameEnd++;
		int tagEnd = nameEnd < line.Length ? nameEnd + 1 : line.Length;
		tokens.Add(new SyntaxToken(i, tagEnd - i, TagColor));
		return tagEnd;
	}
}
