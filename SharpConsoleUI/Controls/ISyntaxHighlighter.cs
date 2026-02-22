// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Represents a colored token span within a source line for syntax highlighting.
	/// </summary>
	/// <param name="StartIndex">The zero-based character index where this token begins in the source line.</param>
	/// <param name="Length">The number of characters this token spans.</param>
	/// <param name="ForegroundColor">The foreground color to use when rendering this token.</param>
	public readonly record struct SyntaxToken(int StartIndex, int Length, Color ForegroundColor);

	/// <summary>
	/// Opaque parser state carried from the end of one line to the start of the next.
	/// Highlighter implementations subclass this to hold language-specific fields.
	/// The control stores and forwards the value; it never inspects its contents.
	/// </summary>
	public record SyntaxLineState
	{
		/// <summary>The default state at the start of a document (no multi-line constructs open).</summary>
		public static readonly SyntaxLineState Initial = new();
	}

	/// <summary>
	/// Provides syntax highlighting by tokenizing source lines into colored spans.
	/// Implement this interface to provide custom syntax coloring for specific languages or formats.
	/// </summary>
	public interface ISyntaxHighlighter
	{
		/// <summary>
		/// Tokenizes a source line given the parse state at its start.
		/// Returns the token list and the parser state at the end of the line
		/// (to be passed as <paramref name="startState"/> for the next line).
		/// </summary>
		/// <param name="line">The source line text to tokenize.</param>
		/// <param name="lineIndex">The zero-based line index within the document.</param>
		/// <param name="startState">The parser state at the start of this line.</param>
		(IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
			Tokenize(string line, int lineIndex, SyntaxLineState startState);
	}
}
