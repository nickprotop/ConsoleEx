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
/// Syntax highlighter for unified diff output.
/// Colors +lines green, -lines red, @@hunks cyan, and diff headers bold.
/// </summary>
public class DiffSyntaxHighlighter : ISyntaxHighlighter
{
	private static readonly Color AddedColor = Color.Green;
	private static readonly Color DeletedColor = Color.Red;
	private static readonly Color HunkColor = Color.Cyan1;
	private static readonly Color HeaderColor = Color.Yellow;
	private static readonly Color IndexColor = Color.Grey;

	/// <summary>Tokenizes a single line of unified diff output for syntax highlighting.</summary>
	public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
		Tokenize(string line, int lineIndex, SyntaxLineState startState)
	{
		var tokens = new List<SyntaxToken>();

		if (line.Length == 0)
			return (tokens, SyntaxLineState.Initial);

		if (line.StartsWith("+++") || line.StartsWith("---"))
		{
			tokens.Add(new SyntaxToken(0, line.Length, HeaderColor));
		}
		else if (line.StartsWith("@@"))
		{
			tokens.Add(new SyntaxToken(0, line.Length, HunkColor));
		}
		else if (line.StartsWith("+"))
		{
			tokens.Add(new SyntaxToken(0, line.Length, AddedColor));
		}
		else if (line.StartsWith("-"))
		{
			tokens.Add(new SyntaxToken(0, line.Length, DeletedColor));
		}
		else if (line.StartsWith("diff ") || line.StartsWith("index ") ||
				 line.StartsWith("new file") || line.StartsWith("deleted file") ||
				 line.StartsWith("similarity") || line.StartsWith("rename"))
		{
			tokens.Add(new SyntaxToken(0, line.Length, IndexColor));
		}

		return (tokens, SyntaxLineState.Initial);
	}
}
