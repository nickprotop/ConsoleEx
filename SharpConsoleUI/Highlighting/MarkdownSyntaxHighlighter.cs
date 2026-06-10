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

/// <summary>Per-line parser state for the Markdown highlighter (tracks open fenced code blocks).</summary>
/// <param name="InFencedBlock">True when the line begins inside an open fenced code block.</param>
public record MarkdownLineState(bool InFencedBlock = false) : SyntaxLineState;

/// <summary>Syntax highlighter for Markdown source (headings, emphasis, code, links, fences).</summary>
public class MarkdownSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color HeadingColor = new(218, 165, 32);  // Bold gold
	private static readonly Color BoldColor = Color.White;
	private static readonly Color ItalicColor = Color.Silver;
	private static readonly Color CodeColor = Color.Green;
	private static readonly Color FenceColor = Color.Grey;
	private static readonly Color LinkColor = Color.DodgerBlue2;
	private static readonly Color BlockquoteColor = Color.Grey46;
	private static readonly Color ListColor = Color.Cyan1;

	// Patterns applied in order; first match wins for a given span
	private static readonly (Regex Pattern, Color Color)[] SpanPatterns = new[]
	{
		(new Regex(@"\*\*(?:.+?)\*\*|__(?:.+?)__",  RegexOptions.Compiled), BoldColor),
		(new Regex(@"\*(?:[^*]+)\*|_(?:[^_]+)_",     RegexOptions.Compiled), ItalicColor),
		(new Regex(@"`[^`]+`",                        RegexOptions.Compiled), CodeColor),
		(new Regex(@"\[(?:[^\]]*)\]\((?:[^)]*)\)",   RegexOptions.Compiled), LinkColor),
	};

	/// <summary>Tokenizes a single line of Markdown source for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var state = startState as MarkdownLineState ?? new MarkdownLineState();
		var tokens = new List<SyntaxToken>();
		var trimmed = line.TrimStart();

		if (state.InFencedBlock)
		{
			bool isClosingFence = trimmed.StartsWith("```") || trimmed.StartsWith("~~~");
			if (isClosingFence)
			{
				tokens.Add(new SyntaxToken(0, line.Length, FenceColor));
				return (tokens, new MarkdownLineState(InFencedBlock: false));
			}
			tokens.Add(new SyntaxToken(0, line.Length, CodeColor));
			return (tokens, new MarkdownLineState(InFencedBlock: true));
		}

		bool opensBlock = trimmed.StartsWith("```") || trimmed.StartsWith("~~~");
		if (opensBlock)
		{
			tokens.Add(new SyntaxToken(0, line.Length, FenceColor));
			return (tokens, new MarkdownLineState(InFencedBlock: true));
		}

		// Headings: # through ######
		if (trimmed.StartsWith("#"))
		{
			tokens.Add(new SyntaxToken(0, line.Length, HeadingColor));
			return (tokens, new MarkdownLineState(InFencedBlock: false));
		}

		// Blockquotes
		if (trimmed.StartsWith(">"))
		{
			tokens.Add(new SyntaxToken(0, line.Length, BlockquoteColor));
			return (tokens, new MarkdownLineState(InFencedBlock: false));
		}

		// List markers: -, *, or digit followed by dot+space
		var listMatch = Regex.Match(trimmed, @"^(?:[-*]|\d+\.) ");
		if (listMatch.Success)
		{
			int indent = line.Length - trimmed.Length;
			tokens.Add(new SyntaxToken(indent, listMatch.Length, ListColor));
		}

		// Span-level patterns
		foreach (var (pattern, color) in SpanPatterns)
		{
			foreach (Match m in pattern.Matches(line))
				tokens.Add(new SyntaxToken(m.Index, m.Length, color));
		}

		return (tokens, new MarkdownLineState(InFencedBlock: false));
	}
}
