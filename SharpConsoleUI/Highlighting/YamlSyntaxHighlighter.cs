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

/// <summary>Syntax highlighter for YAML (keys, values, comments, anchors, tags, directives).</summary>
public class YamlSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color KeyColor = Color.Cyan1;
	private static readonly Color StringColor = Color.Orange3;
	private static readonly Color NumberColor = Color.Cyan1;
	private static readonly Color BoolNullColor = Color.DodgerBlue2;
	private static readonly Color CommentColor = Color.Green;
	private static readonly Color AnchorColor = Color.Yellow;
	private static readonly Color TagColor = Color.Grey;
	private static readonly Color DirectiveColor = Color.Grey;

	/// <summary>Tokenizes a single line of YAML for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var tokens = new List<SyntaxToken>();
		int i = 0;

		// Skip leading whitespace
		while (i < line.Length && char.IsWhiteSpace(line[i])) i++;

		if (i >= line.Length)
			return (tokens, SyntaxLineState.Initial);

		// Full-line comment
		if (line[i] == '#')
		{
			tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
			return (tokens, SyntaxLineState.Initial);
		}

		// Directive (e.g., %YAML, %TAG)
		if (line[i] == '%')
		{
			tokens.Add(new SyntaxToken(i, line.Length - i, DirectiveColor));
			return (tokens, SyntaxLineState.Initial);
		}

		// Document markers (--- or ...)
		if (i == 0 && line.Length >= 3 && (line.StartsWith("---") || line.StartsWith("...")))
		{
			tokens.Add(new SyntaxToken(0, 3, DirectiveColor));
			return (tokens, SyntaxLineState.Initial);
		}

		// List item marker - skip the "- " prefix
		int contentStart = i;
		if (line[i] == '-' && i + 1 < line.Length && line[i + 1] == ' ')
			i += 2;

		// Skip whitespace after list marker
		while (i < line.Length && char.IsWhiteSpace(line[i])) i++;

		// Try to find a key (unquoted or quoted)
		int keyStart = i;
		bool foundKey = false;

		if (i < line.Length && (line[i] == '"' || line[i] == '\''))
		{
			// Quoted key
			char quote = line[i];
			int end = FindQuoteEnd(line, i);
			if (end < line.Length && IsFollowedByColon(line, end))
			{
				tokens.Add(new SyntaxToken(i, end - i, KeyColor));
				i = end;
				foundKey = true;
			}
		}
		else
		{
			// Unquoted key - scan to colon
			int colonPos = FindUnquotedColon(line, i);
			if (colonPos > i)
			{
				// Trim trailing whitespace from key
				int keyEnd = colonPos;
				while (keyEnd > i && char.IsWhiteSpace(line[keyEnd - 1])) keyEnd--;
				if (keyEnd > i)
				{
					tokens.Add(new SyntaxToken(i, keyEnd - i, KeyColor));
					foundKey = true;
				}
				i = colonPos;
			}
		}

		if (foundKey)
		{
			// Skip colon and whitespace
			if (i < line.Length && line[i] == ':') i++;
			while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
		}
		else
		{
			// No key found, reset to content start (after list marker)
			i = contentStart;
			if (line[i] == '-' && i + 1 < line.Length && line[i + 1] == ' ')
				i += 2;
			while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
		}

		// Tokenize the value portion
		TokenizeValue(tokens, line, i);

		return (tokens, SyntaxLineState.Initial);
	}

	private void TokenizeValue(List<SyntaxToken> tokens, string line, int start)
	{
		int i = start;
		if (i >= line.Length) return;

		// Inline comment check
		int commentPos = FindInlineComment(line, i);
		int valueEnd = commentPos >= 0 ? commentPos : line.Length;

		// Anchor (&name) or alias (*name)
		if (line[i] == '&' || line[i] == '*')
		{
			int anchorStart = i;
			i++;
			while (i < valueEnd && !char.IsWhiteSpace(line[i]) && line[i] != ',' && line[i] != ']' && line[i] != '}')
				i++;
			tokens.Add(new SyntaxToken(anchorStart, i - anchorStart, AnchorColor));
			while (i < valueEnd && char.IsWhiteSpace(line[i])) i++;
		}

		// Tag (!!type or !tag)
		if (i < valueEnd && line[i] == '!')
		{
			int tagStart = i;
			i++;
			if (i < valueEnd && line[i] == '!') i++;
			while (i < valueEnd && !char.IsWhiteSpace(line[i]))
				i++;
			tokens.Add(new SyntaxToken(tagStart, i - tagStart, TagColor));
			while (i < valueEnd && char.IsWhiteSpace(line[i])) i++;
		}

		if (i >= valueEnd)
		{
			if (commentPos >= 0)
				tokens.Add(new SyntaxToken(commentPos, line.Length - commentPos, CommentColor));
			return;
		}

		// Quoted string value
		if (line[i] == '"' || line[i] == '\'')
		{
			int end = FindQuoteEnd(line, i);
			tokens.Add(new SyntaxToken(i, end - i, StringColor));
			i = end;
		}
		// Block scalar indicators (| or >)
		else if (line[i] == '|' || line[i] == '>')
		{
			// Leave uncolored
		}
		else
		{
			// Unquoted value - check for special types
			string valuePart = line.Substring(i, valueEnd - i).Trim();

			if (valuePart.Length > 0)
			{
				if (IsBoolOrNull(valuePart))
					tokens.Add(new SyntaxToken(i, valuePart.Length, BoolNullColor));
				else if (IsNumber(valuePart))
					tokens.Add(new SyntaxToken(i, valuePart.Length, NumberColor));
				else
					tokens.Add(new SyntaxToken(i, valuePart.Length, StringColor));
			}
		}

		if (commentPos >= 0)
			tokens.Add(new SyntaxToken(commentPos, line.Length - commentPos, CommentColor));
	}

	private static int FindQuoteEnd(string line, int quoteStart)
	{
		char quote = line[quoteStart];
		int i = quoteStart + 1;
		while (i < line.Length)
		{
			if (line[i] == '\\' && quote == '"') { i += 2; continue; }
			if (line[i] == quote) return i + 1;
			i++;
		}
		return line.Length;
	}

	private static bool IsFollowedByColon(string line, int pos)
	{
		while (pos < line.Length && char.IsWhiteSpace(line[pos])) pos++;
		return pos < line.Length && line[pos] == ':' &&
			   (pos + 1 >= line.Length || char.IsWhiteSpace(line[pos + 1]) || line[pos + 1] == '#');
	}

	private static int FindUnquotedColon(string line, int start)
	{
		for (int i = start; i < line.Length; i++)
		{
			if (line[i] == ':' && (i + 1 >= line.Length || char.IsWhiteSpace(line[i + 1])))
				return i;
			if (line[i] == '#' && i > 0 && char.IsWhiteSpace(line[i - 1]))
				return -1;
		}
		return -1;
	}

	private static int FindInlineComment(string line, int start)
	{
		for (int i = start; i < line.Length; i++)
		{
			if (line[i] == '"' || line[i] == '\'')
			{
				i = FindQuoteEnd(line, i) - 1;
				continue;
			}
			if (line[i] == '#' && i > 0 && char.IsWhiteSpace(line[i - 1]))
				return i;
		}
		return -1;
	}

	private static bool IsBoolOrNull(string value)
	{
		return value is "true" or "false" or "True" or "False" or "TRUE" or "FALSE"
			or "yes" or "no" or "Yes" or "No" or "YES" or "NO"
			or "on" or "off" or "On" or "Off" or "ON" or "OFF"
			or "null" or "Null" or "NULL" or "~";
	}

	private static bool IsNumber(string value)
	{
		if (value.Length == 0) return false;
		int i = 0;
		if (value[i] == '-' || value[i] == '+') i++;
		if (i >= value.Length) return false;

		// Hex
		if (value.Length > i + 2 && value[i] == '0' && (value[i + 1] == 'x' || value[i + 1] == 'X'))
			return true;
		// Octal
		if (value.Length > i + 2 && value[i] == '0' && (value[i + 1] == 'o' || value[i + 1] == 'O'))
			return true;

		bool hasDigit = false;
		while (i < value.Length && char.IsDigit(value[i])) { hasDigit = true; i++; }
		if (i < value.Length && value[i] == '.')
		{
			i++;
			while (i < value.Length && char.IsDigit(value[i])) { hasDigit = true; i++; }
		}
		if (i < value.Length && (value[i] == 'e' || value[i] == 'E'))
		{
			i++;
			if (i < value.Length && (value[i] == '+' || value[i] == '-')) i++;
			while (i < value.Length && char.IsDigit(value[i])) i++;
		}
		// Special float values
		if (value is ".inf" or "-.inf" or "+.inf" or ".Inf" or "-.Inf" or "+.Inf"
			or ".INF" or "-.INF" or "+.INF" or ".nan" or ".NaN" or ".NAN")
			return true;

		return hasDigit && i == value.Length;
	}
}
