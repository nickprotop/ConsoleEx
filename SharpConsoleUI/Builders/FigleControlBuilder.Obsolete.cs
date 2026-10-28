// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Builders;

/// <summary>
/// The former name of <see cref="FigletControlBuilder"/>, kept so existing code
/// keeps compiling.
/// </summary>
/// <remarks>
/// The format is FIGlet; "Figle" was a misspelling that reached the public
/// surface while the factory was always <c>Controls.Figlet()</c>. The docs
/// have always shown <c>new FigleControlBuilder()</c> as a supported way to
/// start a chain, so the constructor stays. Every chained method is inherited
/// and returns <see cref="FigletControlBuilder"/>, which is what a fluent chain
/// needs - only the first link carries the old name.
/// </remarks>
[Obsolete("Renamed to FigletControlBuilder; this name will be removed in a future major version.")]
public sealed class FigleControlBuilder : FigletControlBuilder
{
}
