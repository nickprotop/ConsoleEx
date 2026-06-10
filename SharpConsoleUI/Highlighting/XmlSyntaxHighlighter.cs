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

/// <summary>Per-line parser state for the XML highlighter (tracks comment/CDATA blocks).</summary>
/// <param name="InComment">True when the line begins inside an unterminated XML comment.</param>
/// <param name="InCData">True when the line begins inside an unterminated CDATA section.</param>
public record XmlLineState(bool InComment = false, bool InCData = false) : SyntaxLineState;

/// <summary>Syntax highlighter for XML (tags, attributes, comments, CDATA, entities).</summary>
public class XmlSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color TagColor = Color.Cyan1;
	private static readonly Color AttrNameColor = Color.DodgerBlue2;
	private static readonly Color AttrValueColor = Color.Orange3;
	private static readonly Color CommentColor = Color.Green;
	private static readonly Color CDataColor = Color.Grey;
	private static readonly Color EntityColor = Color.Yellow;
	private static readonly Color PiColor = Color.Grey;

	/// <summary>Tokenizes a single line of XML for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var state = startState as XmlLineState ?? new XmlLineState();
		var tokens = new List<SyntaxToken>();
		int i = 0;

		// Continue multi-line comment
		if (state.InComment)
		{
			int closeIdx = line.IndexOf("-->", StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				if (line.Length > 0)
					tokens.Add(new SyntaxToken(0, line.Length, CommentColor));
				return (tokens, new XmlLineState(InComment: true));
			}
			tokens.Add(new SyntaxToken(0, closeIdx + 3, CommentColor));
			i = closeIdx + 3;
			return TokenizeFrom(tokens, line, i, false, false);
		}

		// Continue multi-line CDATA
		if (state.InCData)
		{
			int closeIdx = line.IndexOf("]]>", StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				if (line.Length > 0)
					tokens.Add(new SyntaxToken(0, line.Length, CDataColor));
				return (tokens, new XmlLineState(InCData: true));
			}
			tokens.Add(new SyntaxToken(0, closeIdx + 3, CDataColor));
			i = closeIdx + 3;
			return TokenizeFrom(tokens, line, i, false, false);
		}

		return TokenizeFrom(tokens, line, 0, false, false);
	}

	private (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		TokenizeFrom(List<SyntaxToken> tokens, string line, int start, bool inComment, bool inCData)
	{
		int i = start;

		while (i < line.Length)
		{
			// Comment start
			if (i + 3 < line.Length && line[i] == '<' && line[i + 1] == '!' && line[i + 2] == '-' && line[i + 3] == '-')
			{
				int closeIdx = line.IndexOf("-->", i + 4, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
					return (tokens, new XmlLineState(InComment: true));
				}
				tokens.Add(new SyntaxToken(i, closeIdx + 3 - i, CommentColor));
				i = closeIdx + 3;
				continue;
			}

			// CDATA start
			if (i + 8 < line.Length && line.AsSpan(i, 9).SequenceEqual("<![CDATA[".AsSpan()))
			{
				int closeIdx = line.IndexOf("]]>", i + 9, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					tokens.Add(new SyntaxToken(i, line.Length - i, CDataColor));
					return (tokens, new XmlLineState(InCData: true));
				}
				tokens.Add(new SyntaxToken(i, closeIdx + 3 - i, CDataColor));
				i = closeIdx + 3;
				continue;
			}

			// Processing instruction <?...?>
			if (i + 1 < line.Length && line[i] == '<' && line[i + 1] == '?')
			{
				int closeIdx = line.IndexOf("?>", i + 2, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					tokens.Add(new SyntaxToken(i, line.Length - i, PiColor));
					return (tokens, new XmlLineState());
				}
				tokens.Add(new SyntaxToken(i, closeIdx + 2 - i, PiColor));
				i = closeIdx + 2;
				continue;
			}

			// Tag start
			if (line[i] == '<')
			{
				i = TokenizeTag(tokens, line, i);
				continue;
			}

			// Entity reference in text content
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

		return (tokens, new XmlLineState());
	}

	private int TokenizeTag(List<SyntaxToken> tokens, string line, int tagStart)
	{
		int i = tagStart;

		// Find end of tag name: <tagname or </tagname
		i++; // skip '<'
		if (i < line.Length && line[i] == '/') i++; // closing tag

		int nameStart = tagStart;
		while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != '>' && line[i] != '/')
			i++;

		// Color the tag name portion (includes < and optional /)
		tokens.Add(new SyntaxToken(tagStart, i - tagStart, TagColor));

		// Parse attributes until '>' or '/>'
		while (i < line.Length)
		{
			if (char.IsWhiteSpace(line[i])) { i++; continue; }

			// Self-closing or end of tag
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

			// Skip whitespace and '='
			while (i < line.Length && (char.IsWhiteSpace(line[i]) || line[i] == '='))
				i++;

			// Attribute value (quoted string)
			if (i < line.Length && (line[i] == '"' || line[i] == '\''))
			{
				char quote = line[i];
				int valStart = i;
				i++; // skip opening quote
				while (i < line.Length && line[i] != quote)
					i++;
				if (i < line.Length) i++; // skip closing quote
				tokens.Add(new SyntaxToken(valStart, i - valStart, AttrValueColor));
			}
		}

		return i;
	}
}
