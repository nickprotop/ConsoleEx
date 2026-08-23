// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting.TextMate;

namespace SharpConsoleUI.Highlighting;

/// <summary>Syntax highlighter for unified diffs.</summary>
/// <remarks>
/// Retained for source compatibility. Highlighting is now performed by the TextMate engine;
/// resolve highlighters through <see cref="SyntaxHighlighters.For(string)"/> instead.
/// </remarks>
[Obsolete("Regex highlighting is superseded by the TextMate engine. Use SyntaxHighlighters.For(\"diff\") instead.")]
public class DiffSyntaxHighlighter : ISyntaxHighlighter
{
	private readonly ISyntaxHighlighter? _inner = TextMateHighlighting.For("diff");

	/// <inheritdoc/>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
		=> _inner?.Tokenize(line, lineIndex, startState)
		   ?? (Array.Empty<SyntaxToken>(), SyntaxLineState.Initial);
}
