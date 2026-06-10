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

/// <summary>
/// Stateless syntax highlighter for Bash / POSIX shell scripts. Colors comments,
/// single- and double-quoted strings, variable expansions, control keywords,
/// common builtins, numbers, and operators. Strings are treated as line-local
/// (no multi-line state), and a leading <c>#</c> only starts a comment when it
/// is at the start of a token (index 0 or preceded by whitespace), so that
/// parameter expansions such as <c>${VAR#prefix}</c> are not mistaken for comments.
/// </summary>
public class BashSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color CommentColor = Color.Green;
	private static readonly Color StringColor = Color.Orange3;
	private static readonly Color KeywordColor = Color.DodgerBlue2;
	private static readonly Color BuiltinColor = Color.MediumTurquoise;
	private static readonly Color VariableColor = Color.Cyan1;
	private static readonly Color NumberColor = Color.Cyan1;
	private static readonly Color OperatorColor = Color.Grey;

	// Shell control words.
	private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
	{
		"if", "then", "else", "elif", "fi", "for", "while", "until",
		"do", "done", "case", "esac", "function", "in", "select", "time"
	};

	// Common builtins / commands (modest set).
	private static readonly HashSet<string> Builtins = new(StringComparer.Ordinal)
	{
		"echo", "cd", "export", "source", "local", "readonly", "declare",
		"unset", "return", "exit", "eval", "exec", "set", "shift", "printf",
		"read", "pwd", "alias", "test", "trap", "wait", "kill"
	};

	/// <summary>Tokenizes a single line of a Bash/shell script for syntax highlighting.</summary>
	/// <param name="line">The line text to tokenize.</param>
	/// <param name="lineIndex">The zero-based index of the line within the document.</param>
	/// <param name="startState">The incoming per-line state (unused; this highlighter is stateless).</param>
	/// <returns>The tokens for the line and the (unchanged) end state.</returns>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var tokens = new List<SyntaxToken>();
		int i = 0;

		while (i < line.Length)
		{
			char c = line[i];

			if (char.IsWhiteSpace(c)) { i++; continue; }

			// Comment: '#' at start-of-token (index 0 or preceded by whitespace).
			// This avoids treating '#' inside ${VAR#...} or words like a#b as a comment.
			if (c == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
			{
				tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
				break;
			}

			// Strings.
			if (c == '"' || c == '\'')
			{
				int end = FindStringEnd(line, i, c);
				tokens.Add(new SyntaxToken(i, end - i, StringColor));
				i = end;
				continue;
			}

			// Variables.
			if (c == '$')
			{
				i = TokenizeVariable(tokens, line, i);
				continue;
			}

			// Operators.
			if (TryMatchOperator(line, i, out int opLen))
			{
				tokens.Add(new SyntaxToken(i, opLen, OperatorColor));
				i += opLen;
				continue;
			}

			// Numbers (standalone integer/float).
			if (char.IsDigit(c) && IsTokenStart(line, i))
			{
				int numStart = i;
				while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.'))
					i++;
				// Only color as a number if bounded by a non-identifier char.
				if (i >= line.Length || !IsIdentChar(line[i]))
					tokens.Add(new SyntaxToken(numStart, i - numStart, NumberColor));
				continue;
			}

			// Words: keywords / builtins (whole-word only).
			if (IsIdentChar(c) && IsTokenStart(line, i))
			{
				int wordStart = i;
				while (i < line.Length && IsIdentChar(line[i]))
					i++;
				string word = line.Substring(wordStart, i - wordStart);
				if (Keywords.Contains(word))
					tokens.Add(new SyntaxToken(wordStart, word.Length, KeywordColor));
				else if (Builtins.Contains(word))
					tokens.Add(new SyntaxToken(wordStart, word.Length, BuiltinColor));
				continue;
			}

			i++;
		}

		return (tokens, startState);
	}

	/// <summary>Finds the index just past the closing quote of a quoted string.</summary>
	private static int FindStringEnd(string line, int quoteStart, char quote)
	{
		int i = quoteStart + 1;
		while (i < line.Length)
		{
			// Double quotes honor backslash escapes; single quotes do not.
			if (quote == '"' && line[i] == '\\') { i += 2; continue; }
			if (line[i] == quote) return i + 1;
			i++;
		}
		return line.Length;
	}

	/// <summary>Tokenizes a variable expansion starting at the '$' and returns the index past it.</summary>
	private static int TokenizeVariable(List<SyntaxToken> tokens, string line, int start)
	{
		int i = start + 1; // skip '$'
		if (i >= line.Length)
		{
			tokens.Add(new SyntaxToken(start, 1, VariableColor));
			return i;
		}

		// ${...} expansion.
		if (line[i] == '{')
		{
			int braceEnd = line.IndexOf('}', i + 1);
			if (braceEnd >= 0)
			{
				tokens.Add(new SyntaxToken(start, braceEnd + 1 - start, VariableColor));
				return braceEnd + 1;
			}
			tokens.Add(new SyntaxToken(start, line.Length - start, VariableColor));
			return line.Length;
		}

		// Special single-char parameters: $?, $@, $#, $$, $!, $*, $0-$9, $-
		char n = line[i];
		if (n == '?' || n == '@' || n == '#' || n == '$' || n == '!' ||
			n == '*' || n == '-' || char.IsDigit(n))
		{
			tokens.Add(new SyntaxToken(start, 2, VariableColor));
			return i + 1;
		}

		// $NAME identifier.
		if (IsIdentChar(n))
		{
			while (i < line.Length && IsIdentChar(line[i]))
				i++;
			tokens.Add(new SyntaxToken(start, i - start, VariableColor));
			return i;
		}

		// Lone '$'.
		tokens.Add(new SyntaxToken(start, 1, VariableColor));
		return start + 1;
	}

	/// <summary>Matches a shell operator at <paramref name="pos"/>; returns its length via <paramref name="length"/>.</summary>
	private static bool TryMatchOperator(string line, int pos, out int length)
	{
		char c = line[pos];
		char next = pos + 1 < line.Length ? line[pos + 1] : '\0';

		// Two-char operators first.
		if ((c == '|' && next == '|') || (c == '&' && next == '&') ||
			(c == '>' && next == '>') || (c == '<' && next == '<'))
		{
			length = 2;
			return true;
		}

		if (c == '|' || c == '&' || c == '>' || c == '<' || c == ';')
		{
			length = 1;
			return true;
		}

		length = 0;
		return false;
	}

	/// <summary>True if a token at <paramref name="pos"/> begins fresh (start of line, after whitespace, or after an operator/quote).</summary>
	private static bool IsTokenStart(string line, int pos)
	{
		if (pos == 0) return true;
		char p = line[pos - 1];
		return !IsIdentChar(p) && p != '$' && p != '.';
	}

	/// <summary>True for characters that may appear in an identifier/word.</summary>
	private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
