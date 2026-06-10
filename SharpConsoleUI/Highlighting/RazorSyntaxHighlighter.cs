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

/// <summary>Per-line parser state for the Razor highlighter (tracks HTML comment, code block, and brace depth).</summary>
/// <param name="InHtmlComment">True when the line begins inside an unterminated HTML/Razor comment.</param>
/// <param name="InCodeBlock">True when the line begins inside an open C# code block.</param>
/// <param name="InBlockComment">True when the line begins inside an unterminated C# block comment.</param>
/// <param name="BraceDepth">The open brace nesting depth carried into the line.</param>
public record RazorLineState(
	bool InHtmlComment = false,
	bool InCodeBlock = false,
	bool InBlockComment = false,
	int BraceDepth = 0) : SyntaxLineState;

/// <summary>Syntax highlighter for Razor/.cshtml, mixing HTML and embedded C#.</summary>
public class RazorSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color DirectiveColor = Color.MediumTurquoise;
	private static readonly Color TransitionColor = Color.Yellow;

	private static readonly HtmlSyntaxHighlighter HtmlHighlighter = new();
	private static readonly CSharpSyntaxHighlighter CSharpHighlighter = new();

	private static readonly HashSet<string> RazorDirectives = new(StringComparer.Ordinal)
	{
		"page", "model", "namespace", "inject", "inherits", "implements",
		"layout", "section", "functions", "code", "attribute", "typeparam",
		"using", "addTagHelper", "removeTagHelper", "tagHelperPrefix",
		"rendermode", "preservewhitespace"
	};

	private static readonly HashSet<string> RazorKeywords = new(StringComparer.Ordinal)
	{
		"if", "else", "for", "foreach", "while", "do", "switch",
		"try", "catch", "finally", "lock", "using", "await"
	};

	/// <summary>Tokenizes a single line of Razor markup for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var state = startState as RazorLineState ?? new RazorLineState();
		var tokens = new List<SyntaxToken>();

		// Continue multi-line HTML comment
		if (state.InHtmlComment)
		{
			int closeIdx = line.IndexOf("-->", StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				if (line.Length > 0)
					tokens.Add(new SyntaxToken(0, line.Length, Color.Green));
				return (tokens, state);
			}
			tokens.Add(new SyntaxToken(0, closeIdx + 3, Color.Green));
			return TokenizeRazorHtml(tokens, line, closeIdx + 3, state with { InHtmlComment = false });
		}

		// Continue C# code block (@code {}, @functions {}, @{ }, control structures)
		if (state.InCodeBlock)
		{
			return TokenizeCSharpBlock(tokens, line, 0, state);
		}

		// Continue C# block comment inside code
		if (state.InBlockComment)
		{
			int closeIdx = line.IndexOf("*/", StringComparison.Ordinal);
			if (closeIdx < 0)
			{
				if (line.Length > 0)
					tokens.Add(new SyntaxToken(0, line.Length, Color.Green));
				return (tokens, state);
			}
			tokens.Add(new SyntaxToken(0, closeIdx + 2, Color.Green));
			return TokenizeCSharpBlock(tokens, line, closeIdx + 2, state with { InBlockComment = false });
		}

		return TokenizeRazorHtml(tokens, line, 0, state);
	}

	private (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		TokenizeRazorHtml(List<SyntaxToken> tokens, string line, int start, RazorLineState state)
	{
		int i = start;

		while (i < line.Length)
		{
			// Razor comment @* ... *@
			if (i + 1 < line.Length && line[i] == '@' && line[i + 1] == '*')
			{
				int closeIdx = line.IndexOf("*@", i + 2, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					tokens.Add(new SyntaxToken(i, line.Length - i, Color.Green));
					return (tokens, state with { InHtmlComment = true });
				}
				tokens.Add(new SyntaxToken(i, closeIdx + 2 - i, Color.Green));
				i = closeIdx + 2;
				continue;
			}

			// Razor transition: @@  (escaped @)
			if (i + 1 < line.Length && line[i] == '@' && line[i + 1] == '@')
			{
				i += 2;
				continue;
			}

			// Razor directives: @page, @model, @using, etc.
			if (line[i] == '@' && i + 1 < line.Length && char.IsLetter(line[i + 1]))
			{
				int wordStart = i + 1;
				int wordEnd = wordStart;
				while (wordEnd < line.Length && (char.IsLetterOrDigit(line[wordEnd]) || line[wordEnd] == '_'))
					wordEnd++;
				string word = line.Substring(wordStart, wordEnd - wordStart);

				if (RazorDirectives.Contains(word))
				{
					tokens.Add(new SyntaxToken(i, 1, TransitionColor));
					tokens.Add(new SyntaxToken(wordStart, wordEnd - wordStart, DirectiveColor));

					// @code and @functions open a C# block
					if (word == "code" || word == "functions")
					{
						// Find the opening brace
						int bracePos = line.IndexOf('{', wordEnd);
						if (bracePos >= 0)
						{
							tokens.Add(new SyntaxToken(bracePos, 1, Color.Grey));
							return TokenizeCSharpBlock(tokens, line, bracePos + 1,
								state with { InCodeBlock = true, BraceDepth = 1 });
						}
					}
					i = wordEnd;
					continue;
				}

				// Razor control flow: @if, @for, @foreach, @while, etc.
				if (RazorKeywords.Contains(word))
				{
					tokens.Add(new SyntaxToken(i, 1, TransitionColor));
					tokens.Add(new SyntaxToken(wordStart, wordEnd - wordStart, Color.DodgerBlue2));

					// Find the opening brace to enter code block
					int bracePos = line.IndexOf('{', wordEnd);
					if (bracePos >= 0)
					{
						// Tokenize condition between keyword and brace as C#
						if (bracePos > wordEnd)
						{
							string condPart = line.Substring(wordEnd, bracePos - wordEnd);
							var (condTokens, _) = CSharpHighlighter.Tokenize(condPart, 0, SyntaxLineState.Initial);
							foreach (var t in condTokens)
								tokens.Add(new SyntaxToken(wordEnd + t.StartIndex, t.Length, t.ForegroundColor));
						}
						tokens.Add(new SyntaxToken(bracePos, 1, Color.Grey));
						return TokenizeCSharpBlock(tokens, line, bracePos + 1,
							state with { InCodeBlock = true, BraceDepth = 1 });
					}

					i = wordEnd;
					continue;
				}

				// Inline C# expression: @variable, @Model.Property, @method()
				tokens.Add(new SyntaxToken(i, 1, TransitionColor));
				i = TokenizeInlineExpression(tokens, line, wordStart);
				continue;
			}

			// Explicit expression: @(...)
			if (line[i] == '@' && i + 1 < line.Length && line[i + 1] == '(')
			{
				tokens.Add(new SyntaxToken(i, 2, TransitionColor));
				i += 2;
				int depth = 1;
				int exprStart = i;
				while (i < line.Length && depth > 0)
				{
					if (line[i] == '(') depth++;
					else if (line[i] == ')') depth--;
					if (depth > 0) i++;
				}
				if (exprStart < i)
				{
					string expr = line.Substring(exprStart, i - exprStart);
					var (exprTokens, _) = CSharpHighlighter.Tokenize(expr, 0, SyntaxLineState.Initial);
					foreach (var t in exprTokens)
						tokens.Add(new SyntaxToken(exprStart + t.StartIndex, t.Length, t.ForegroundColor));
				}
				if (i < line.Length)
				{
					tokens.Add(new SyntaxToken(i, 1, TransitionColor));
					i++;
				}
				continue;
			}

			// Razor block: @{ ... }
			if (line[i] == '@' && i + 1 < line.Length && line[i + 1] == '{')
			{
				tokens.Add(new SyntaxToken(i, 1, TransitionColor));
				tokens.Add(new SyntaxToken(i + 1, 1, Color.Grey));
				return TokenizeCSharpBlock(tokens, line, i + 2,
					state with { InCodeBlock = true, BraceDepth = 1 });
			}

			// HTML comment <!-- -->
			if (i + 3 < line.Length && line[i] == '<' && line[i + 1] == '!' && line[i + 2] == '-' && line[i + 3] == '-')
			{
				int closeIdx = line.IndexOf("-->", i + 4, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					tokens.Add(new SyntaxToken(i, line.Length - i, Color.Green));
					return (tokens, state with { InHtmlComment = true });
				}
				tokens.Add(new SyntaxToken(i, closeIdx + 3 - i, Color.Green));
				i = closeIdx + 3;
				continue;
			}

			// HTML tags — delegate to HTML highlighter's tag parsing
			if (line[i] == '<' && i + 1 < line.Length && (char.IsLetter(line[i + 1]) || line[i + 1] == '/'))
			{
				int tagEnd = TokenizeHtmlTag(tokens, line, i);
				i = tagEnd;
				continue;
			}

			// Entity references
			if (line[i] == '&')
			{
				int semiIdx = line.IndexOf(';', i + 1);
				if (semiIdx > i && semiIdx - i <= 10)
				{
					tokens.Add(new SyntaxToken(i, semiIdx + 1 - i, Color.Yellow));
					i = semiIdx + 1;
					continue;
				}
			}

			i++;
		}

		return (tokens, state);
	}

	private (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		TokenizeCSharpBlock(List<SyntaxToken> tokens, string line, int start, RazorLineState state)
	{
		int i = start;
		int braceDepth = state.BraceDepth;
		bool inString = false;
		char stringChar = '"';
		bool inVerbatim = false;

		while (i < line.Length)
		{
			char c = line[i];

			// Block comment
			if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '*')
			{
				int closeIdx = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
				if (closeIdx < 0)
				{
					tokens.Add(new SyntaxToken(i, line.Length - i, Color.Green));
					return (tokens, state with { InBlockComment = true, BraceDepth = braceDepth });
				}
				tokens.Add(new SyntaxToken(i, closeIdx + 2 - i, Color.Green));
				i = closeIdx + 2;
				continue;
			}

			// Line comment
			if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
			{
				tokens.Add(new SyntaxToken(i, line.Length - i, Color.Green));
				return (tokens, state with { InCodeBlock = true, BraceDepth = braceDepth });
			}

			// Track strings
			if (!inString)
			{
				if (c == '@' && i + 1 < line.Length && line[i + 1] == '"')
				{
					inString = true; stringChar = '"'; inVerbatim = true;
					i += 2;
					continue;
				}
				if (c == '"' || c == '\'')
				{
					inString = true; stringChar = c; inVerbatim = false;
					i++;
					continue;
				}
			}
			else
			{
				if (inVerbatim)
				{
					if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { i += 2; continue; }
					if (c == '"') { inString = false; i++; continue; }
				}
				else
				{
					if (c == '\\') { i += 2; continue; }
					if (c == stringChar) { inString = false; i++; continue; }
				}
				i++;
				continue;
			}

			if (c == '{')
			{
				braceDepth++;
				i++;
				continue;
			}

			if (c == '}')
			{
				braceDepth--;
				if (braceDepth <= 0)
				{
					// End of code block — tokenize the C# portion
					string csPart = line.Substring(start, i - start);
					var (csTokens, _) = CSharpHighlighter.Tokenize(csPart, 0, SyntaxLineState.Initial);
					foreach (var t in csTokens)
						tokens.Add(new SyntaxToken(start + t.StartIndex, t.Length, t.ForegroundColor));
					tokens.Add(new SyntaxToken(i, 1, Color.Grey));
					return TokenizeRazorHtml(tokens, line, i + 1,
						state with { InCodeBlock = false, BraceDepth = 0 });
				}
				i++;
				continue;
			}

			i++;
		}

		// Line ended while still in code block — tokenize what we have as C#
		if (i > start)
		{
			string csPart = line.Substring(start, i - start);
			var (csTokens, _) = CSharpHighlighter.Tokenize(csPart, 0, SyntaxLineState.Initial);
			foreach (var t in csTokens)
				tokens.Add(new SyntaxToken(start + t.StartIndex, t.Length, t.ForegroundColor));
		}

		return (tokens, state with { InCodeBlock = true, BraceDepth = braceDepth });
	}

	private int TokenizeInlineExpression(List<SyntaxToken> tokens, string line, int start)
	{
		int i = start;

		// Walk dotted member access: identifier(.identifier)*
		while (i < line.Length)
		{
			int wordStart = i;
			while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
				i++;

			if (i > wordStart)
			{
				var (csTokens, _) = CSharpHighlighter.Tokenize(
					line.Substring(wordStart, i - wordStart), 0, SyntaxLineState.Initial);
				foreach (var t in csTokens)
					tokens.Add(new SyntaxToken(wordStart + t.StartIndex, t.Length, t.ForegroundColor));
			}

			// Method call parens
			if (i < line.Length && line[i] == '(')
			{
				int depth = 1;
				i++;
				while (i < line.Length && depth > 0)
				{
					if (line[i] == '(') depth++;
					else if (line[i] == ')') depth--;
					i++;
				}
				continue;
			}

			// Dot accessor
			if (i < line.Length && line[i] == '.')
			{
				i++;
				continue;
			}

			// Indexer
			if (i < line.Length && line[i] == '[')
			{
				int depth = 1;
				i++;
				while (i < line.Length && depth > 0)
				{
					if (line[i] == '[') depth++;
					else if (line[i] == ']') depth--;
					i++;
				}
				continue;
			}

			break;
		}

		return i;
	}

	private int TokenizeHtmlTag(List<SyntaxToken> tokens, string line, int tagStart)
	{
		int i = tagStart + 1;
		if (i < line.Length && line[i] == '/') i++;

		while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != '>' && line[i] != '/')
			i++;
		tokens.Add(new SyntaxToken(tagStart, i - tagStart, Color.Cyan1));

		while (i < line.Length)
		{
			if (char.IsWhiteSpace(line[i])) { i++; continue; }

			if (line[i] == '/' && i + 1 < line.Length && line[i + 1] == '>')
			{
				tokens.Add(new SyntaxToken(i, 2, Color.Cyan1));
				return i + 2;
			}
			if (line[i] == '>')
			{
				tokens.Add(new SyntaxToken(i, 1, Color.Cyan1));
				return i + 1;
			}

			// Razor attribute: @bind, @onclick, etc.
			if (line[i] == '@')
			{
				int attrStart = i;
				i++;
				while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '-' || line[i] == '_' || line[i] == ':'))
					i++;
				tokens.Add(new SyntaxToken(attrStart, i - attrStart, DirectiveColor));

				while (i < line.Length && (char.IsWhiteSpace(line[i]) || line[i] == '=')) i++;

				if (i < line.Length && line[i] == '"')
				{
					int valStart = i;
					i++;
					while (i < line.Length && line[i] != '"') i++;
					if (i < line.Length) i++;
					tokens.Add(new SyntaxToken(valStart, i - valStart, Color.Orange3));
				}
				continue;
			}

			// Regular attribute
			int aStart = i;
			while (i < line.Length && line[i] != '=' && line[i] != '>' && line[i] != '/' && !char.IsWhiteSpace(line[i]))
				i++;
			if (i > aStart)
				tokens.Add(new SyntaxToken(aStart, i - aStart, Color.DodgerBlue2));

			while (i < line.Length && (char.IsWhiteSpace(line[i]) || line[i] == '=')) i++;

			if (i < line.Length && (line[i] == '"' || line[i] == '\''))
			{
				char quote = line[i];
				int valStart = i;
				i++;
				while (i < line.Length && line[i] != quote) i++;
				if (i < line.Length) i++;
				tokens.Add(new SyntaxToken(valStart, i - valStart, Color.Orange3));
			}
		}

		return i;
	}
}
