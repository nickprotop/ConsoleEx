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
	/// Provides syntax highlighting by tokenizing source lines into colored spans.
	/// Implement this interface to provide custom syntax coloring for specific languages or formats.
	/// </summary>
	public interface ISyntaxHighlighter
	{
		/// <summary>
		/// Tokenizes a source line into colored spans for rendering.
		/// Tokens may overlap or leave gaps; gaps use the default foreground color.
		/// </summary>
		/// <param name="line">The source line text to tokenize.</param>
		/// <param name="lineIndex">The zero-based line index within the document.</param>
		/// <returns>A list of syntax tokens defining colored spans within the line.</returns>
		IReadOnlyList<SyntaxToken> Tokenize(string line, int lineIndex);
	}
}
