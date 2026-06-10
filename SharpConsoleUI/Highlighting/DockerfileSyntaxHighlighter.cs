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

/// <summary>Syntax highlighter for Dockerfiles (instructions, flags, variables, strings).</summary>
public class DockerfileSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color InstructionColor = Color.DodgerBlue2;
	private static readonly Color CommentColor = Color.Green;
	private static readonly Color StringColor = Color.Orange3;
	private static readonly Color VariableColor = Color.Yellow;
	private static readonly Color FlagColor = Color.Grey;
	private static readonly Color NumberColor = Color.Cyan1;

	private static readonly HashSet<string> Instructions = new(StringComparer.OrdinalIgnoreCase)
	{
		"FROM", "RUN", "CMD", "LABEL", "MAINTAINER", "EXPOSE", "ENV",
		"ADD", "COPY", "ENTRYPOINT", "VOLUME", "USER", "WORKDIR",
		"ARG", "ONBUILD", "STOPSIGNAL", "HEALTHCHECK", "SHELL"
	};

	/// <summary>Tokenizes a single line of a Dockerfile for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var tokens = new List<SyntaxToken>();
		int i = 0;

		// Skip leading whitespace
		while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
		if (i >= line.Length)
			return (tokens, SyntaxLineState.Initial);

		// Comment
		if (line[i] == '#')
		{
			tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
			return (tokens, SyntaxLineState.Initial);
		}

		// Parser directive (e.g., # syntax=..., # escape=...)
		// Already handled by comment above

		// Instruction keyword
		int wordStart = i;
		while (i < line.Length && char.IsLetter(line[i])) i++;

		if (i > wordStart)
		{
			string word = line.Substring(wordStart, i - wordStart);
			if (Instructions.Contains(word))
			{
				tokens.Add(new SyntaxToken(wordStart, i - wordStart, InstructionColor));

				// Special handling for "AS" in FROM ... AS name
				if (word.Equals("FROM", StringComparison.OrdinalIgnoreCase))
					TokenizeFromLine(tokens, line, i);
				else
					TokenizeArguments(tokens, line, i);
			}
		}

		return (tokens, SyntaxLineState.Initial);
	}

	private void TokenizeFromLine(List<SyntaxToken> tokens, string line, int start)
	{
		int i = start;
		while (i < line.Length)
		{
			if (char.IsWhiteSpace(line[i])) { i++; continue; }

			// Check for "AS" keyword
			if (i + 2 <= line.Length && line.Substring(i, 2).Equals("AS", StringComparison.OrdinalIgnoreCase) &&
				(i + 2 >= line.Length || char.IsWhiteSpace(line[i + 2])))
			{
				tokens.Add(new SyntaxToken(i, 2, InstructionColor));
				i += 2;
				continue;
			}

			// Variable
			if (line[i] == '$')
			{
				i = TokenizeVariable(tokens, line, i);
				continue;
			}

			// Comment
			if (line[i] == '#')
			{
				tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
				return;
			}

			i++;
		}
	}

	private void TokenizeArguments(List<SyntaxToken> tokens, string line, int start)
	{
		int i = start;
		while (i < line.Length)
		{
			char c = line[i];

			if (char.IsWhiteSpace(c)) { i++; continue; }

			// Line continuation
			if (c == '\\' && i + 1 >= line.Length)
				break;

			// Comment (only valid after whitespace in shell form)
			if (c == '#')
			{
				tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
				return;
			}

			// Flags like --from=0, --chown=user
			if (c == '-' && i + 1 < line.Length && line[i + 1] == '-')
			{
				int flagStart = i;
				i += 2;
				while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != '=')
					i++;
				tokens.Add(new SyntaxToken(flagStart, i - flagStart, FlagColor));
				// Skip = and value
				if (i < line.Length && line[i] == '=')
				{
					i++;
					// Value after =
					if (i < line.Length && (line[i] == '"' || line[i] == '\''))
					{
						i = TokenizeString(tokens, line, i);
					}
					else if (i < line.Length && line[i] == '$')
					{
						i = TokenizeVariable(tokens, line, i);
					}
				}
				continue;
			}

			// Quoted strings
			if (c == '"' || c == '\'')
			{
				i = TokenizeString(tokens, line, i);
				continue;
			}

			// Variable substitution
			if (c == '$')
			{
				i = TokenizeVariable(tokens, line, i);
				continue;
			}

			// Numbers (standalone, e.g., EXPOSE 8080)
			if (char.IsDigit(c))
			{
				int numStart = i;
				while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.'))
					i++;
				if (i == line.Length || char.IsWhiteSpace(line[i]) || line[i] == '/' || line[i] == ':')
					tokens.Add(new SyntaxToken(numStart, i - numStart, NumberColor));
				continue;
			}

			i++;
		}
	}

	private int TokenizeString(List<SyntaxToken> tokens, string line, int start)
	{
		char quote = line[start];
		int i = start + 1;

		// For double-quoted strings, look for variables inside
		if (quote == '"')
		{
			int segStart = start;
			while (i < line.Length)
			{
				if (line[i] == '\\') { i += 2; continue; }
				if (line[i] == '$')
				{
					// String portion before variable
					if (i > segStart)
						tokens.Add(new SyntaxToken(segStart, i - segStart, StringColor));
					int varEnd = TokenizeVariable(tokens, line, i);
					segStart = varEnd;
					i = varEnd;
					continue;
				}
				if (line[i] == '"')
				{
					tokens.Add(new SyntaxToken(segStart, i + 1 - segStart, StringColor));
					return i + 1;
				}
				i++;
			}
			tokens.Add(new SyntaxToken(segStart, line.Length - segStart, StringColor));
			return line.Length;
		}

		// Single-quoted: no variable expansion
		while (i < line.Length && line[i] != quote) i++;
		if (i < line.Length) i++;
		tokens.Add(new SyntaxToken(start, i - start, StringColor));
		return i;
	}

	private int TokenizeVariable(List<SyntaxToken> tokens, string line, int start)
	{
		int i = start + 1; // skip $
		if (i >= line.Length) return i;

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

		while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
			i++;

		if (i > start + 1)
			tokens.Add(new SyntaxToken(start, i - start, VariableColor));
		return i;
	}
}
