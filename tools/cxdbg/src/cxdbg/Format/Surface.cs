// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using Spectre.Console.Rendering;

namespace Cxdbg.Format;

/// <summary>
/// Where a formatter draws, and whether it may use colour.
/// </summary>
/// <remarks>
/// <para>
/// Rendering into a string rather than onto the console, because the caller
/// decides where the text goes: a terminal, a file after <c>&gt;</c>, or a
/// prompt that holds it until asked. A formatter that wrote to stdout itself
/// could not be redirected or held.
/// </para>
/// <para>
/// The width is set explicitly and generously. Spectre fits its output to the
/// console it believes it has, and a redirected one reports eighty columns -
/// which sliced a guid across four lines and is why an earlier attempt at
/// this gave up on Spectre's table entirely. The fault was never Spectre's.
/// </para>
/// </remarks>
internal static class Surface
{
	/// <summary>How wide a rendered block may be.</summary>
	/// <remarks>
	/// The real terminal when there is one, so a tree wraps where the reader
	/// can see it; a generous fixed width when the output is going somewhere
	/// else, so nothing is folded on the way to a file.
	/// </remarks>
	private static int Width =>
		!Console.IsOutputRedirected && Console.WindowWidth > 20 ? Console.WindowWidth : 200;

	/// <summary>Whether colour would reach a person.</summary>
	/// <param name="refused">Whether the caller passed --no-color.</param>
	/// <remarks>
	/// Colour goes to a terminal and nowhere else. In a file or a pipe it is
	/// noise that breaks grep and diff, so this asks the destination rather
	/// than offering a flag to get it wrong with. NO_COLOR is honoured by the
	/// parser, which is where the convention belongs.
	/// </remarks>
	internal static bool Colour(bool refused) => !refused && !Console.IsOutputRedirected;

	/// <summary>Renders a widget to text.</summary>
	/// <param name="widget">What to draw.</param>
	/// <param name="colour">Whether to include colour.</param>
	internal static string Render(IRenderable widget, bool colour)
	{
		var writer = new StringWriter();

		var console = AnsiConsole.Create(new AnsiConsoleSettings
		{
			Ansi = colour ? AnsiSupport.Yes : AnsiSupport.No,
			// TrueColor, because `color` paints a swatch in the resolved RGB
			// and sixteen colours turn two distinct theme colours into the
			// same grey - which is exactly the comparison someone ran the
			// verb to make. A terminal without it degrades on its own.
			ColorSystem = colour ? ColorSystemSupport.TrueColor : ColorSystemSupport.NoColors,
			Out = new AnsiConsoleOutput(writer),
		});

		console.Profile.Width = Width;
		console.Write(widget);

		return writer.ToString();
	}
}
