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

/// <summary>Regex-free syntax highlighter for JSON (and JSONC comments).</summary>
public class JsonSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color KeyColor = Color.Cyan1;
	private static readonly Color StringColor = Color.Orange3;
	private static readonly Color NumberColor = Color.Cyan1;
	private static readonly Color BoolNullColor = Color.DodgerBlue2;
	private static readonly Color BraceColor = Color.Grey;
	private static readonly Color CommentColor = Color.Green;

	/// <summary>Tokenizes a single line of JSON for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var tokens = new List<SyntaxToken>();
		bool inBlockComment = startState is JsonLineState js && js.InBlockComment;

		if (inBlockComment)
		{
			int closeIdx = line.IndexOf("*/", StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				if (line.Length > 0)
					tokens.Add(new SyntaxToken(0, line.Length, CommentColor));
				return (tokens, new JsonLineState(true));
			}
			tokens.Add(new SyntaxToken(0, closeIdx + 2, CommentColor));
			TokenizeRange(tokens, line, closeIdx + 2, ref inBlockComment);
			return (tokens, new JsonLineState(inBlockComment));
		}

		TokenizeRange(tokens, line, 0, ref inBlockComment);
		return (tokens, new JsonLineState(inBlockComment));
	}

	private void TokenizeRange(List<SyntaxToken> tokens, string line, int start, ref bool inBlockComment)
	{
		bool seenColon = false;
		int i = start;

		while (i < line.Length)
		{
			char c = line[i];

			// Skip whitespace
			if (char.IsWhiteSpace(c)) { i++; continue; }

			// Line comment (JSONC)
			if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
			{
				tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
				return;
			}

			// Block comment (JSONC)
			if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
			{
				int closeIdx = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
					inBlockComment = true;
					return;
				}
				tokens.Add(new SyntaxToken(i, closeIdx + 2 - i, CommentColor));
				i = closeIdx + 2;
				continue;
			}

			// Strings
			if (c == '"')
			{
				int end = FindStringEnd(line, i);
				bool isKey = !seenColon && IsFollowedByColon(line, end);
				tokens.Add(new SyntaxToken(i, end - i, isKey ? KeyColor : StringColor));
				i = end;
				continue;
			}

			// Colon separator
			if (c == ':') { seenColon = true; i++; continue; }

			// Comma resets key/value tracking for next pair
			if (c == ',') { seenColon = false; i++; continue; }

			// Braces and brackets
			if (c == '{' || c == '}' || c == '[' || c == ']')
			{
				tokens.Add(new SyntaxToken(i, 1, BraceColor));
				i++;
				continue;
			}

			// Numbers
			if (c == '-' || char.IsDigit(c))
			{
				int numStart = i;
				if (c == '-') i++;
				while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.' || line[i] == 'e' || line[i] == 'E' || line[i] == '+' || line[i] == '-'))
				{
					// Avoid consuming '-' or '+' unless preceded by e/E
					if ((line[i] == '+' || line[i] == '-') && i > 0 && line[i - 1] != 'e' && line[i - 1] != 'E')
						break;
					i++;
				}
				tokens.Add(new SyntaxToken(numStart, i - numStart, NumberColor));
				continue;
			}

			// Keywords: true, false, null
			if (TryMatchKeyword(line, i, "true", out int len) ||
				TryMatchKeyword(line, i, "false", out len) ||
				TryMatchKeyword(line, i, "null", out len))
			{
				tokens.Add(new SyntaxToken(i, len, BoolNullColor));
				i += len;
				continue;
			}

			i++;
		}
	}

	private static int FindStringEnd(string line, int quoteStart)
	{
		int i = quoteStart + 1;
		while (i < line.Length)
		{
			if (line[i] == '\\') { i += 2; continue; }
			if (line[i] == '"') return i + 1;
			i++;
		}
		return line.Length;
	}

	private static bool IsFollowedByColon(string line, int pos)
	{
		for (int i = pos; i < line.Length; i++)
		{
			if (char.IsWhiteSpace(line[i])) continue;
			return line[i] == ':';
		}
		return false;
	}

	private static bool TryMatchKeyword(string line, int pos, string keyword, out int length)
	{
		length = keyword.Length;
		if (pos + length > line.Length) return false;
		if (!line.AsSpan(pos, length).SequenceEqual(keyword.AsSpan())) return false;
		// Ensure not part of a larger identifier
		if (pos + length < line.Length && char.IsLetterOrDigit(line[pos + length])) return false;
		return true;
	}
}

/// <summary>Per-line parser state for the JSON highlighter (tracks open block comments).</summary>
/// <param name="InBlockComment">True when the line begins inside an unterminated block comment.</param>
public record JsonLineState(bool InBlockComment = false) : SyntaxLineState;
