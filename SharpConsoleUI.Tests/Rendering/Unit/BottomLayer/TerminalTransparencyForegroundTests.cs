// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Byte-level tests for the foreground SGR that ConsoleBuffer actually emits.
///
/// Two behaviours are locked here:
///
/// 1. The FormatCellAnsi cache must not treat its own uninitialised state as a real colour.
///    Color is a readonly struct of (R, G, B, A), so default(Color) is byte-identical to
///    Color.Transparent: a first cell of Transparent-on-Transparent used to match the empty
///    cache and emit no SGR at all, and since the hit path returns before writing the cache,
///    every later transparent cell did the same. Latent whenever an opaque cell is formatted
///    first (which primes the cache); fatal for an all-transparent theme.
///
/// 2. In PreserveTerminalTransparency, a blank cell emits a terminal-default foreground.
///    A space paints no glyph, so a concrete 38;2;R;G;B for it is a colour nobody can see,
///    and blank cells pick up an opaque White from several layers that cannot see the
///    transparency mode. Underline / Strikethrough / Invert DO paint a blank in its
///    foreground, so they keep their colour, as does any non-blank glyph and any other mode.
/// </summary>
public class TerminalTransparencyForegroundTests
{
	private readonly ITestOutputHelper _output;

	public TerminalTransparencyForegroundTests(ITestOutputHelper output)
	{
		_output = output;
	}

	// ---------------------------------------------------------------------
	// 1. Cache priming
	// ---------------------------------------------------------------------

	/// <summary>
	/// A FRESH buffer whose very first formatted cell is Transparent-on-Transparent must still emit a
	/// real SGR. This is the regression lock: the uninitialised cache is exactly that colour
	/// combination, so the cell used to come back with an empty escape and render no attributes.
	/// </summary>
	[Fact]
	public void FirstCell_TransparentOnTransparent_EmitsTerminalDefaultSgr_NotAnEmptyEscape()
	{
		// No opaque cell is written first: nothing primes the cache, which is the whole point.
		var ansi = Render(TerminalTransparencyMode.PreserveTerminalTransparency, buffer =>
			buffer.SetNarrowCell(0, 0, 'X', Color.Transparent, Color.Transparent));

		Assert.Contains("X", ansi, StringComparison.Ordinal); // non-vacuous: the cell rendered
		Assert.Contains(";39", ansi, StringComparison.Ordinal); // foreground SGR present, not swallowed
		Assert.Contains(";49", ansi, StringComparison.Ordinal); // background SGR present
	}

	/// <summary>
	/// The same first-cell case, stated as the cache invariant: a transparent-first buffer and an
	/// opaque-first buffer must agree on the SGR they emit for an identical transparent cell.
	/// Before the fix the transparent-first buffer emitted nothing while the opaque-first one was correct.
	/// </summary>
	[Fact]
	public void TransparentFirst_AndOpaqueFirst_AgreeOnTheSgrForTheSameCell()
	{
		var transparentFirst = Render(TerminalTransparencyMode.PreserveTerminalTransparency, buffer =>
			buffer.SetNarrowCell(0, 0, 'X', Color.Transparent, Color.Transparent));

		// An opaque cell elsewhere primes the cache, which is what used to hide the bug.
		var opaqueFirst = Render(TerminalTransparencyMode.PreserveTerminalTransparency, buffer =>
		{
			buffer.SetNarrowCell(5, 0, 'P', Color.Red, Color.Blue);
			buffer.SetNarrowCell(0, 0, 'X', Color.Transparent, Color.Transparent);
		});

		Assert.Equal(GoverningSgr(opaqueFirst, "X"), GoverningSgr(transparentFirst, "X"));
	}

	// ---------------------------------------------------------------------
	// 2. The blank-cell rule
	// ---------------------------------------------------------------------

	/// <summary>A blank cell carrying an opaque foreground emits terminal-default in no-colour mode.</summary>
	[Fact]
	public void Blank_WithOpaqueForeground_EmitsTerminalDefault_InPreserveTerminalTransparency()
	{
		var ansi = Render(TerminalTransparencyMode.PreserveTerminalTransparency, buffer =>
			buffer.FillCells(0, 0, 4, ' ', Color.White, Color.Transparent));

		Assert.DoesNotContain("38;2", ansi, StringComparison.Ordinal); // the White never reaches the terminal
		Assert.Contains(";39", ansi, StringComparison.Ordinal);
	}

	/// <summary>
	/// Blast-radius lock: outside the opt-in mode the very same blank keeps its opaque foreground.
	/// The rule must not leak into a normal colour render.
	/// </summary>
	[Fact]
	public void Blank_WithOpaqueForeground_KeepsItsColour_InOtherModes()
	{
		var ansi = Render(TerminalTransparencyMode.PreserveWindowColor, buffer =>
			buffer.FillCells(0, 0, 4, ' ', Color.White, Color.Black));

		Assert.Contains("38;2;255;255;255", ansi, StringComparison.Ordinal);
	}

	/// <summary>
	/// Scope lock: a real glyph is visible, so it keeps its foreground even in no-colour mode.
	/// Only blanks are normalised; this is not a blanket foreground suppressor.
	/// </summary>
	[Fact]
	public void NonBlank_KeepsItsForeground_InPreserveTerminalTransparency()
	{
		var ansi = Render(TerminalTransparencyMode.PreserveTerminalTransparency, buffer =>
			buffer.SetNarrowCell(0, 0, 'A', Color.White, Color.Transparent));

		Assert.Contains("38;2;255;255;255", ansi, StringComparison.Ordinal);
	}

	/// <summary>
	/// Underline paints a blank cell in its foreground, so an underlined space is NOT invisible and
	/// keeps its colour. Same for Strikethrough and Invert.
	/// </summary>
	[Theory]
	[InlineData(TextDecoration.Underline)]
	[InlineData(TextDecoration.Strikethrough)]
	[InlineData(TextDecoration.Invert)]
	public void Blank_WithForegroundPaintingDecoration_KeepsItsColour(TextDecoration decoration)
	{
		var ansi = RenderDecoratedBlank(decoration, Color.White);

		Assert.Contains("38;2;255;255;255", ansi, StringComparison.Ordinal);
	}

	/// <summary>A decoration that does NOT paint the blank (Bold) leaves the normalisation in place.</summary>
	[Fact]
	public void Blank_WithNonPaintingDecoration_StillEmitsTerminalDefault()
	{
		var ansi = RenderDecoratedBlank(TextDecoration.Bold, Color.White);

		Assert.DoesNotContain("38;2", ansi, StringComparison.Ordinal);
	}

	// ---------------------------------------------------------------------
	// Harness
	// ---------------------------------------------------------------------

	/// <summary>
	/// Renders one frame of a directly-constructed <see cref="ConsoleBuffer"/> and returns the ANSI it wrote.
	/// Render() targets Console.Out, so the capture is the production byte stream.
	/// </summary>
	private static string Render(TerminalTransparencyMode mode, Action<ConsoleBuffer> paint)
	{
		var buffer = new ConsoleBuffer(20, 2, new ConsoleWindowSystemOptions { TerminalTransparencyMode = mode });
		paint(buffer);

		var originalOut = Console.Out;
		var capture = new StringWriter();
		try
		{
			Console.SetOut(capture);
			buffer.Render();
		}
		finally
		{
			Console.SetOut(originalOut);
		}

		return capture.ToString();
	}

	/// <summary>
	/// Renders a single blank cell carrying <paramref name="decoration"/> and <paramref name="foreground"/>.
	/// Decorations survive only via the cell-copy path (SetNarrowCell clears them), so the blank is staged
	/// in a CharacterBuffer and copied in.
	/// </summary>
	private static string RenderDecoratedBlank(TextDecoration decoration, Color foreground)
	{
		var source = new CharacterBuffer(4, 1, Color.Transparent);
		source.SetCell(0, 0, new Cell(new Rune(' '), foreground, Color.Transparent, decoration));

		return Render(TerminalTransparencyMode.PreserveTerminalTransparency, buffer =>
			buffer.SetCellsFromBuffer(0, 0, source, 0, 0, 1, Color.Transparent));
	}

	/// <summary>The parameters of the last <c>ESC[0...m</c> SGR emitted before <paramref name="glyph"/>.</summary>
	private static string GoverningSgr(string ansi, string glyph)
	{
		var at = ansi.IndexOf(glyph, StringComparison.Ordinal);
		Assert.True(at >= 0, $"'{glyph}' did not render.");

		var governing = string.Empty;
		foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(ansi, "\\[0([0-9;]*)m"))
		{
			if (m.Index >= at)
				break;
			governing = m.Groups[1].Value;
		}
		return governing;
	}
}
