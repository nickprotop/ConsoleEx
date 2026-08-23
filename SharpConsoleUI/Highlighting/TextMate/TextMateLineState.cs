// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using TextMateSharp.Grammars;

namespace SharpConsoleUI.Highlighting.TextMate
{
	/// <summary>
	/// Carries TextMate's parser state from the end of one line to the start of the next, so
	/// constructs that span lines (block comments, here-docs) highlight correctly.
	/// </summary>
	/// <param name="Stack">The TextMate rule stack at the end of the line, or null at document start.</param>
	public sealed record TextMateLineState(IStateStack? Stack) : SyntaxLineState;
}
