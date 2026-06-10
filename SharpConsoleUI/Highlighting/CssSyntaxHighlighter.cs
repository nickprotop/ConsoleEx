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

/// <summary>Per-line parser state for the CSS highlighter (tracks open block comments).</summary>
/// <param name="InBlockComment">True when the line begins inside an unterminated block comment.</param>
public record CssLineState(bool InBlockComment = false) : SyntaxLineState;

/// <summary>Syntax highlighter for CSS (and common SCSS/Less line comments).</summary>
public class CssSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color SelectorColor = Color.Cyan1;
	private static readonly Color PropertyColor = Color.DodgerBlue2;
	private static readonly Color ValueColor = Color.Orange3;
	private static readonly Color NumberColor = Color.Cyan1;
	private static readonly Color UnitColor = Color.MediumTurquoise;
	private static readonly Color StringColor = Color.Orange3;
	private static readonly Color CommentColor = Color.Green;
	private static readonly Color BraceColor = Color.Grey;
	private static readonly Color AtRuleColor = Color.MediumTurquoise;
	private static readonly Color ColorLiteralColor = Color.Yellow;
	private static readonly Color ImportantColor = Color.Red;
	private static readonly Color PunctuationColor = Color.Grey;

	/// <summary>Tokenizes a single line of CSS for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var state = startState as CssLineState ?? new CssLineState();
		var tokens = new List<SyntaxToken>();
		bool inBlockComment = state.InBlockComment;

		if (inBlockComment)
		{
			int closeIdx = line.IndexOf("*/", StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				if (line.Length > 0)
					tokens.Add(new SyntaxToken(0, line.Length, CommentColor));
				return (tokens, new CssLineState(true));
			}
			tokens.Add(new SyntaxToken(0, closeIdx + 2, CommentColor));
			TokenizeLine(tokens, line, closeIdx + 2, ref inBlockComment);
			return (tokens, new CssLineState(inBlockComment));
		}

		TokenizeLine(tokens, line, 0, ref inBlockComment);
		return (tokens, new CssLineState(inBlockComment));
	}

	private void TokenizeLine(List<SyntaxToken> tokens, string line, int start, ref bool inBlockComment)
	{
		// Determine context: are we inside a declaration block?
		// Simple heuristic: if we see property: value pattern, treat as declaration
		bool inDeclaration = false;
		for (int scan = 0; scan < start; scan++)
		{
			if (line[scan] == '{') inDeclaration = true;
			else if (line[scan] == '}') inDeclaration = false;
		}

		int i = start;
		bool seenColon = false;

		while (i < line.Length)
		{
			char c = line[i];

			// Whitespace
			if (char.IsWhiteSpace(c)) { i++; continue; }

			// Block comment
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

			// Line comment (SCSS/Less style, also common in preprocessors)
			if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
			{
				tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
				return;
			}

			// Braces
			if (c == '{')
			{
				tokens.Add(new SyntaxToken(i, 1, BraceColor));
				inDeclaration = true;
				seenColon = false;
				i++;
				continue;
			}
			if (c == '}')
			{
				tokens.Add(new SyntaxToken(i, 1, BraceColor));
				inDeclaration = false;
				seenColon = false;
				i++;
				continue;
			}

			// Semicolon resets property/value tracking
			if (c == ';')
			{
				tokens.Add(new SyntaxToken(i, 1, PunctuationColor));
				seenColon = false;
				i++;
				continue;
			}

			// Colon in declarations
			if (c == ':' && inDeclaration && !seenColon)
			{
				tokens.Add(new SyntaxToken(i, 1, PunctuationColor));
				seenColon = true;
				i++;
				continue;
			}

			// At-rules (@media, @import, @keyframes, etc.)
			if (c == '@')
			{
				int ruleStart = i;
				i++;
				while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '-'))
					i++;
				tokens.Add(new SyntaxToken(ruleStart, i - ruleStart, AtRuleColor));
				continue;
			}

			// Strings
			if (c == '"' || c == '\'')
			{
				int end = FindStringEnd(line, i);
				tokens.Add(new SyntaxToken(i, end - i, StringColor));
				i = end;
				continue;
			}

			// Color literals (#fff, #aabbcc, #aabbccdd)
			if (c == '#' && i + 1 < line.Length && IsHexChar(line[i + 1]))
			{
				int colorStart = i;
				i++;
				while (i < line.Length && IsHexChar(line[i])) i++;
				tokens.Add(new SyntaxToken(colorStart, i - colorStart, ColorLiteralColor));
				continue;
			}

			// !important
			if (c == '!' && i + 9 <= line.Length && line.AsSpan(i, 10).ToString().Equals("!important", StringComparison.OrdinalIgnoreCase))
			{
				tokens.Add(new SyntaxToken(i, 10, ImportantColor));
				i += 10;
				continue;
			}

			// Numbers with optional units
			if (char.IsDigit(c) || (c == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
			{
				int numStart = i;
				if (c == '-') i++;
				while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.')) i++;
				int numEnd = i;
				// Unit suffix
				int unitStart = i;
				while (i < line.Length && char.IsLetter(line[i])) i++;
				if (i > unitStart)
				{
					tokens.Add(new SyntaxToken(numStart, numEnd - numStart, NumberColor));
					tokens.Add(new SyntaxToken(unitStart, i - unitStart, UnitColor));
				}
				else
				{
					// % unit
					if (i < line.Length && line[i] == '%')
					{
						tokens.Add(new SyntaxToken(numStart, numEnd - numStart, NumberColor));
						tokens.Add(new SyntaxToken(i, 1, UnitColor));
						i++;
					}
					else
					{
						tokens.Add(new SyntaxToken(numStart, numEnd - numStart, NumberColor));
					}
				}
				continue;
			}

			// In declaration context: property names (before colon) vs values (after colon)
			if (inDeclaration)
			{
				if (!seenColon)
				{
					// Property name
					int propStart = i;
					while (i < line.Length && line[i] != ':' && line[i] != ';' && line[i] != '}' && !char.IsWhiteSpace(line[i]))
						i++;
					if (i > propStart)
						tokens.Add(new SyntaxToken(propStart, i - propStart, PropertyColor));
					continue;
				}
				else
				{
					// Value - identifiers
					int valStart = i;
					while (i < line.Length && line[i] != ';' && line[i] != '}' && line[i] != '!' && !char.IsWhiteSpace(line[i])
						   && line[i] != '(' && line[i] != ')' && line[i] != ',')
						i++;
					if (i > valStart)
						tokens.Add(new SyntaxToken(valStart, i - valStart, ValueColor));
					else
						i++; // skip parens, commas
					continue;
				}
			}

			// Outside declaration: selector
			int selStart = i;
			while (i < line.Length && line[i] != '{' && line[i] != '/' && !char.IsWhiteSpace(line[i])
				   && line[i] != ',' && line[i] != '>')
				i++;
			if (i > selStart)
				tokens.Add(new SyntaxToken(selStart, i - selStart, SelectorColor));
			else
				i++; // skip combinators, commas
		}
	}

	private static int FindStringEnd(string line, int quoteStart)
	{
		char quote = line[quoteStart];
		int i = quoteStart + 1;
		while (i < line.Length)
		{
			if (line[i] == '\\') { i += 2; continue; }
			if (line[i] == quote) return i + 1;
			i++;
		}
		return line.Length;
	}

	private static bool IsHexChar(char c) =>
		(c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
