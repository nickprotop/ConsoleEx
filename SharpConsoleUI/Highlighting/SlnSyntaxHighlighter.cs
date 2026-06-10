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

/// <summary>Syntax highlighter for Visual Studio solution (.sln) files.</summary>
public class SlnSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color KeywordColor = Color.DodgerBlue2;
	private static readonly Color GuidColor = Color.Yellow;
	private static readonly Color StringColor = Color.Orange3;
	private static readonly Color CommentColor = Color.Green;
	private static readonly Color SectionColor = Color.Cyan1;
	private static readonly Color VersionColor = Color.Cyan1;

	private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
	{
		"Project", "EndProject", "Global", "EndGlobal",
		"GlobalSection", "EndGlobalSection",
		"ProjectSection", "EndProjectSection"
	};

	/// <summary>Tokenizes a single line of a .sln file for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var tokens = new List<SyntaxToken>();
		int i = 0;

		// Skip leading whitespace
		while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
		if (i >= line.Length)
			return (tokens, SyntaxLineState.Initial);

		// Comment line (# is used in .sln)
		if (line[i] == '#')
		{
			tokens.Add(new SyntaxToken(i, line.Length - i, CommentColor));
			return (tokens, SyntaxLineState.Initial);
		}

		// Header lines (Microsoft Visual Studio Solution File, etc.)
		if (i == 0 && line.Length > 0 && char.IsLetter(line[0]))
		{
			// Check for keyword at start
			int wordEnd = i;
			while (wordEnd < line.Length && (char.IsLetterOrDigit(line[wordEnd]) || line[wordEnd] == '_'))
				wordEnd++;

			string word = line.Substring(i, wordEnd - i);

			if (Keywords.Contains(word))
			{
				tokens.Add(new SyntaxToken(i, wordEnd - i, KeywordColor));
				TokenizeRemainder(tokens, line, wordEnd);
				return (tokens, SyntaxLineState.Initial);
			}
		}

		// Indented lines (inside sections)
		// Check for keyword
		int kwStart = i;
		int kwEnd = i;
		while (kwEnd < line.Length && (char.IsLetterOrDigit(line[kwEnd]) || line[kwEnd] == '_'))
			kwEnd++;

		if (kwEnd > kwStart)
		{
			string word = line.Substring(kwStart, kwEnd - kwStart);
			if (Keywords.Contains(word))
			{
				tokens.Add(new SyntaxToken(kwStart, kwEnd - kwStart, KeywordColor));
				TokenizeRemainder(tokens, line, kwEnd);
				return (tokens, SyntaxLineState.Initial);
			}
		}

		// Regular line — tokenize for GUIDs, strings, section names
		TokenizeRemainder(tokens, line, i);
		return (tokens, SyntaxLineState.Initial);
	}

	private void TokenizeRemainder(List<SyntaxToken> tokens, string line, int start)
	{
		int i = start;
		while (i < line.Length)
		{
			char c = line[i];

			// Parenthesized section names like (SolutionConfigurationPlatforms)
			if (c == '(')
			{
				int close = line.IndexOf(')', i + 1);
				if (close > i)
				{
					tokens.Add(new SyntaxToken(i + 1, close - i - 1, SectionColor));
					i = close + 1;
					continue;
				}
			}

			// GUIDs: {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
			if (c == '{')
			{
				int close = line.IndexOf('}', i + 1);
				if (close > i && close - i <= 40)
				{
					tokens.Add(new SyntaxToken(i, close + 1 - i, GuidColor));
					i = close + 1;
					continue;
				}
			}

			// Quoted strings
			if (c == '"')
			{
				int end = line.IndexOf('"', i + 1);
				if (end > i)
				{
					tokens.Add(new SyntaxToken(i, end + 1 - i, StringColor));
					i = end + 1;
					continue;
				}
			}

			// Version-like numbers (e.g., 12.0.31101.0)
			if (char.IsDigit(c))
			{
				int numStart = i;
				while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.'))
					i++;
				if (i - numStart > 1 && line.Substring(numStart, i - numStart).Contains('.'))
					tokens.Add(new SyntaxToken(numStart, i - numStart, VersionColor));
				continue;
			}

			// = sign context: key = value pairs
			if (c == '=')
			{
				// Try to color the key before =
				// (already handled above in most cases)
				i++;
				continue;
			}

			i++;
		}
	}
}
