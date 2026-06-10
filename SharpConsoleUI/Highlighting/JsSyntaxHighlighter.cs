// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System.Text.RegularExpressions;
using SharpConsoleUI;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Highlighting;

/// <summary>Per-line parser state for the JavaScript/TypeScript highlighter.</summary>
/// <param name="InBlockComment">True when the line begins inside an unterminated block comment.</param>
/// <param name="InTemplateLiteral">True when the line begins inside an unterminated template literal.</param>
public record JsLineState(bool InBlockComment = false, bool InTemplateLiteral = false) : SyntaxLineState;

/// <summary>Syntax highlighter for JavaScript/TypeScript, including template literals.</summary>
public class JsSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color KeywordColor = Color.DodgerBlue2;
	private static readonly Color TypeKeywordColor = Color.MediumTurquoise;
	private static readonly Color StringColor = Color.Orange3;
	private static readonly Color CommentColor = Color.Green;
	private static readonly Color NumberColor = Color.Cyan1;
	private static readonly Color TemplateColor = Color.Orange3;
	private static readonly Color TemplateBraceColor = Color.Yellow;
	private static readonly Color RegexColor = Color.Yellow;

	private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
	{
		"abstract", "arguments", "as", "async", "await", "break", "case", "catch",
		"class", "const", "continue", "debugger", "default", "delete", "do", "else",
		"enum", "export", "extends", "false", "finally", "for", "from", "function",
		"get", "if", "implements", "import", "in", "instanceof", "interface", "let",
		"module", "new", "null", "of", "package", "private", "protected", "public",
		"readonly", "return", "set", "static", "super", "switch", "this", "throw",
		"true", "try", "type", "typeof", "undefined", "var", "void", "while",
		"with", "yield", "namespace", "declare", "keyof", "infer", "satisfies"
	};

	private static readonly HashSet<string> TypeKeywords = new(StringComparer.Ordinal)
	{
		"any", "bigint", "boolean", "never", "number", "object", "string",
		"symbol", "unknown", "void"
	};

	private static readonly Regex TokenPattern = new(
		@"//.*$" +
		@"|""(?:[^""\\]|\\.)*""" +
		@"|'(?:[^'\\]|\\.)*'" +
		@"|\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?n?\b" +
		@"|0[xX][0-9a-fA-F]+n?\b" +
		@"|0[bB][01]+n?\b" +
		@"|0[oO][0-7]+n?\b" +
		@"|\b[a-zA-Z_$]\w*\b",
		RegexOptions.Compiled | RegexOptions.Multiline);

	/// <summary>Tokenizes a single line of JavaScript/TypeScript for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var state = startState as JsLineState ?? new JsLineState();
		var tokens = new List<SyntaxToken>();
		bool inBlockComment = state.InBlockComment;
		bool inTemplateLiteral = state.InTemplateLiteral;

		// Continue template literal from previous line
		if (inTemplateLiteral)
		{
			int pos = TokenizeTemplateLiteralContinuation(tokens, line, 0, out inTemplateLiteral);
			if (pos >= line.Length)
				return (tokens, new JsLineState(InTemplateLiteral: inTemplateLiteral));
			TokenizeCodeRange(tokens, line, pos, ref inBlockComment, ref inTemplateLiteral);
			return (tokens, new JsLineState(inBlockComment, inTemplateLiteral));
		}

		if (inBlockComment)
		{
			int closeIdx = line.IndexOf("*/", StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				if (line.Length > 0)
					tokens.Add(new SyntaxToken(0, line.Length, CommentColor));
				return (tokens, new JsLineState(InBlockComment: true));
			}
			tokens.Add(new SyntaxToken(0, closeIdx + 2, CommentColor));
			TokenizeCodeRange(tokens, line, closeIdx + 2, ref inBlockComment, ref inTemplateLiteral);
			return (tokens, new JsLineState(inBlockComment, inTemplateLiteral));
		}

		TokenizeCodeRange(tokens, line, 0, ref inBlockComment, ref inTemplateLiteral);
		return (tokens, new JsLineState(inBlockComment, inTemplateLiteral));
	}

	private void TokenizeCodeRange(List<SyntaxToken> tokens, string line, int start,
		ref bool inBlockComment, ref bool inTemplateLiteral)
	{
		if (start >= line.Length) return;

		int i = start;
		while (i < line.Length)
		{
			char c = line[i];

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

			// Line comment
			if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
			{
				tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
				return;
			}

			// Template literal
			if (c == '`')
			{
				int pos = TokenizeTemplateLiteral(tokens, line, i, out inTemplateLiteral);
				i = pos;
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

			// Identifiers/keywords
			if (char.IsLetter(c) || c == '_' || c == '$')
			{
				int wordStart = i;
				while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_' || line[i] == '$'))
					i++;
				string word = line.Substring(wordStart, i - wordStart);
				if (TypeKeywords.Contains(word))
					tokens.Add(new SyntaxToken(wordStart, i - wordStart, TypeKeywordColor));
				else if (Keywords.Contains(word))
					tokens.Add(new SyntaxToken(wordStart, i - wordStart, KeywordColor));
				continue;
			}

			// Numbers
			if (char.IsDigit(c) || (c == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
			{
				int numStart = i;
				// Hex/Binary/Octal
				if (c == '0' && i + 1 < line.Length && (line[i + 1] == 'x' || line[i + 1] == 'X' ||
					line[i + 1] == 'b' || line[i + 1] == 'B' || line[i + 1] == 'o' || line[i + 1] == 'O'))
				{
					i += 2;
					while (i < line.Length && IsHexOrDigit(line[i])) i++;
				}
				else
				{
					while (i < line.Length && char.IsDigit(line[i])) i++;
					if (i < line.Length && line[i] == '.')
					{
						i++;
						while (i < line.Length && char.IsDigit(line[i])) i++;
					}
					if (i < line.Length && (line[i] == 'e' || line[i] == 'E'))
					{
						i++;
						if (i < line.Length && (line[i] == '+' || line[i] == '-')) i++;
						while (i < line.Length && char.IsDigit(line[i])) i++;
					}
				}
				if (i < line.Length && line[i] == 'n') i++; // BigInt
				tokens.Add(new SyntaxToken(numStart, i - numStart, NumberColor));
				continue;
			}

			i++;
		}
	}

	private int TokenizeTemplateLiteral(List<SyntaxToken> tokens, string line, int start, out bool stillInTemplate)
	{
		int i = start + 1; // skip opening backtick
		int segStart = start;

		while (i < line.Length)
		{
			if (line[i] == '\\') { i += 2; continue; }
			if (line[i] == '`')
			{
				tokens.Add(new SyntaxToken(segStart, i + 1 - segStart, TemplateColor));
				stillInTemplate = false;
				return i + 1;
			}
			if (line[i] == '$' && i + 1 < line.Length && line[i + 1] == '{')
			{
				tokens.Add(new SyntaxToken(segStart, i - segStart, TemplateColor));
				tokens.Add(new SyntaxToken(i, 2, TemplateBraceColor));
				i += 2;
				// Find matching } (simple, no nested braces)
				int depth = 1;
				int exprStart = i;
				while (i < line.Length && depth > 0)
				{
					if (line[i] == '{') depth++;
					else if (line[i] == '}') depth--;
					if (depth > 0) i++;
				}
				// Tokenize the expression inside ${}
				if (exprStart < i)
				{
					bool bc = false, tl = false;
					TokenizeCodeRange(tokens, line.Substring(0, i), exprStart, ref bc, ref tl);
				}
				if (i < line.Length && line[i] == '}')
				{
					tokens.Add(new SyntaxToken(i, 1, TemplateBraceColor));
					i++;
				}
				segStart = i;
				continue;
			}
			i++;
		}

		// Unterminated — continues on next line
		tokens.Add(new SyntaxToken(segStart, line.Length - segStart, TemplateColor));
		stillInTemplate = true;
		return line.Length;
	}

	private int TokenizeTemplateLiteralContinuation(List<SyntaxToken> tokens, string line, int start, out bool stillInTemplate)
	{
		return TokenizeTemplateLiteral(tokens, line, start > 0 ? start : 0, out stillInTemplate);
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

	private static bool IsHexOrDigit(char c) =>
		char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
