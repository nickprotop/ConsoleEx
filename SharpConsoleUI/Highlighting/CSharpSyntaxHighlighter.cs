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

/// <summary>Per-line parser state for the C# highlighter (tracks open block comments).</summary>
/// <param name="InBlockComment">True when the line begins inside an unterminated block comment.</param>
public record CSharpLineState(bool InBlockComment = false) : SyntaxLineState;

/// <summary>Regex-based syntax highlighter for C#.</summary>
public class CSharpSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color KeywordColor = Color.DodgerBlue2;
	private static readonly Color TypeKeywordColor = Color.MediumTurquoise;
	private static readonly Color StringColor = Color.Orange3;
	private static readonly Color CommentColor = Color.Green;
	private static readonly Color NumberColor = Color.Cyan1;

	private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
	{
		"abstract", "as", "base", "break", "case", "catch", "checked", "class",
		"const", "continue", "default", "delegate", "do", "else", "enum", "event",
		"explicit", "extern", "false", "finally", "fixed", "for", "foreach", "goto",
		"if", "implicit", "in", "interface", "internal", "is", "lock", "namespace",
		"new", "null", "operator", "out", "override", "params", "private", "protected",
		"public", "readonly", "ref", "return", "sealed", "sizeof", "stackalloc",
		"static", "struct", "switch", "this", "throw", "true", "try", "typeof",
		"unchecked", "unsafe", "using", "virtual", "volatile", "while", "async",
		"await", "yield", "where", "get", "set", "init", "record", "required"
	};

	private static readonly HashSet<string> TypeKeywords = new(StringComparer.Ordinal)
	{
		"bool", "byte", "char", "decimal", "double", "float", "int", "long",
		"object", "sbyte", "short", "string", "uint", "ulong", "ushort", "void",
		"var", "dynamic", "nint", "nuint"
	};

	private static readonly Regex TokenPattern = new(
		@"//.*$" +                           // Line comment
		@"|""(?:[^""\\]|\\.)*""" +           // String literal
		@"|@""(?:[^""]|"""")*""" +           // Verbatim string
		@"|\b\d+(?:\.\d+)?(?:[fFdDmM])?\b" + // Numbers
		@"|\b[a-zA-Z_]\w*\b",               // Identifiers/keywords
		RegexOptions.Compiled | RegexOptions.Multiline);

	/// <summary>Tokenizes a single line of C# source for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var state = startState as CSharpLineState ?? new CSharpLineState();
		var tokens = new List<SyntaxToken>();
		bool inBlockComment = state.InBlockComment;

		if (inBlockComment)
		{
			int closeIdx = line.IndexOf("*/", StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				if (line.Length > 0)
					tokens.Add(new SyntaxToken(0, line.Length, CommentColor));
				return (tokens, new CSharpLineState(InBlockComment: true));
			}
			tokens.Add(new SyntaxToken(0, closeIdx + 2, CommentColor));
			inBlockComment = false;
			TokenizeCodeRange(tokens, line, closeIdx + 2, ref inBlockComment);
			return (tokens, new CSharpLineState(InBlockComment: inBlockComment));
		}

		TokenizeCodeRange(tokens, line, 0, ref inBlockComment);
		return (tokens, new CSharpLineState(InBlockComment: inBlockComment));
	}

	private void TokenizeCodeRange(List<SyntaxToken> tokens, string line, int start, ref bool inBlockComment)
	{
		if (start >= line.Length) return;

		int blockOpen = FindBlockCommentOpen(line, start);
		int lineCommentPos = FindLineComment(line, start);

		if (blockOpen >= 0 && (lineCommentPos < 0 || blockOpen < lineCommentPos))
		{
			// Block comment starts before any line comment
			if (blockOpen > start)
				AddCodeTokens(tokens, line, start, blockOpen);

			int closeIdx = line.IndexOf("*/", blockOpen + 2, StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				tokens.Add(new SyntaxToken(blockOpen, line.Length - blockOpen, CommentColor));
				inBlockComment = true;
			}
			else
			{
				tokens.Add(new SyntaxToken(blockOpen, closeIdx + 2 - blockOpen, CommentColor));
				TokenizeCodeRange(tokens, line, closeIdx + 2, ref inBlockComment);
			}
			return;
		}

		// No block comment (or line comment comes first)
		AddCodeTokens(tokens, line, start, lineCommentPos >= 0 ? lineCommentPos : line.Length);
		if (lineCommentPos >= 0)
			tokens.Add(new SyntaxToken(lineCommentPos, line.Length - lineCommentPos, CommentColor));
	}

	private static int FindBlockCommentOpen(string line, int start)
	{
		bool inString = false;
		for (int i = start; i < line.Length - 1; i++)
		{
			char c = line[i];
			if (!inString)
			{
				if (c == '/' && line[i + 1] == '/') return -1; // line comment comes first
				if (c == '/' && line[i + 1] == '*') return i;
				if (c == '@' && i + 1 < line.Length && line[i + 1] == '"') { inString = true; i++; }
				else if (c == '"') inString = true;
			}
			else
			{
				if (c == '\\') i++;
				else if (c == '"') inString = false;
			}
		}
		return -1;
	}

	private static int FindLineComment(string line, int start)
	{
		bool inString = false;
		for (int i = start; i < line.Length - 1; i++)
		{
			char c = line[i];
			if (!inString)
			{
				if (c == '/' && line[i + 1] == '/') return i;
				if (c == '@' && i + 1 < line.Length && line[i + 1] == '"') { inString = true; i++; }
				else if (c == '"') inString = true;
			}
			else
			{
				if (c == '\\') i++;
				else if (c == '"') inString = false;
			}
		}
		return -1;
	}

	private void AddCodeTokens(List<SyntaxToken> tokens, string line, int segStart, int segEnd)
	{
		int segLength = segEnd - segStart;
		if (segLength <= 0) return;
		string segment = line.Substring(segStart, segLength);

		var matches = TokenPattern.Matches(segment);
		foreach (Match match in matches)
		{
			var text = match.Value;
			if (text.StartsWith("//")) continue;
			else if (text.StartsWith("\"") || text.StartsWith("@\""))
				tokens.Add(new SyntaxToken(segStart + match.Index, match.Length, StringColor));
			else if (char.IsDigit(text[0]))
				tokens.Add(new SyntaxToken(segStart + match.Index, match.Length, NumberColor));
			else if (TypeKeywords.Contains(text))
				tokens.Add(new SyntaxToken(segStart + match.Index, match.Length, TypeKeywordColor));
			else if (Keywords.Contains(text))
				tokens.Add(new SyntaxToken(segStart + match.Index, match.Length, KeywordColor));
		}
	}
}
