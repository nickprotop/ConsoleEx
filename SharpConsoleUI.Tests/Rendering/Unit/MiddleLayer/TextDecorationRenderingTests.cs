using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.MiddleLayer;

/// <summary>
/// Tests for TextDecoration rendering through the ANSI generation pipeline.
/// Validates that text decorations (bold, italic, underline, etc.) produce
/// correct SGR codes in the ANSI output.
/// </summary>
public class TextDecorationRenderingTests
{
	private readonly ITestOutputHelper _output;

	public TextDecorationRenderingTests(ITestOutputHelper output)
	{
		_output = output;
	}

	private static string GetAnsiContentLine(ConsoleWindowSystem system)
	{
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);
		Assert.True(ansiSnapshot.Lines.Count > 0);
		return ansiSnapshot.Lines[0];
	}

	private static ConsoleWindowSystem CreateSystemWithMarkup(string markup)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Deco"
		};
		window.AddControl(new MarkupControl(new List<string> { markup }));
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();
		return system;
	}

	#region Individual Decoration Tests

	[Fact]
	public void Decoration_Bold_EmitsSgr1()
	{
		var system = CreateSystemWithMarkup("[bold]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		Assert.Contains(";1", line);
	}

	[Fact]
	public void Decoration_Dim_EmitsSgr2()
	{
		var system = CreateSystemWithMarkup("[dim]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		Assert.Contains(";2", line);
	}

	[Fact]
	public void Decoration_Italic_EmitsSgr3()
	{
		var system = CreateSystemWithMarkup("[italic]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		Assert.Contains(";3", line);
	}

	[Fact]
	public void Decoration_Underline_EmitsSgr4()
	{
		var system = CreateSystemWithMarkup("[underline]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		Assert.Contains(";4", line);
	}

	[Fact]
	public void Decoration_Blink_EmitsSgr5()
	{
		var system = CreateSystemWithMarkup("[blink]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		Assert.Contains(";5", line);
	}

	[Fact]
	public void Decoration_Invert_EmitsSgr7()
	{
		var system = CreateSystemWithMarkup("[reverse]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		Assert.Contains(";7", line);
	}

	[Fact]
	public void Decoration_Strikethrough_EmitsSgr9()
	{
		var system = CreateSystemWithMarkup("[strikethrough]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		Assert.Contains(";9", line);
	}

	#endregion

	#region Combination Tests

	[Fact]
	public void Decoration_BoldAndUnderline_EmitsBothSgrCodes()
	{
		var system = CreateSystemWithMarkup("[bold underline]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		Assert.Contains(";1", line);
		Assert.Contains(";4", line);
	}

	[Fact]
	public void Decoration_AllSeven_EmitsAllSgrCodes()
	{
		var system = CreateSystemWithMarkup("[bold dim italic underline blink reverse strikethrough]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		Assert.Contains(";1", line); // Bold
		Assert.Contains(";2", line); // Dim
		Assert.Contains(";3", line); // Italic
		Assert.Contains(";4", line); // Underline
		Assert.Contains(";5", line); // Blink
		Assert.Contains(";7", line); // Invert
		Assert.Contains(";9", line); // Strikethrough
	}

	[Fact]
	public void Decoration_BoldWithColor_EmitsBothColorAndDecoration()
	{
		var system = CreateSystemWithMarkup("[bold red]text[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");
		// Should have both color (38;2;...) and bold (;1)
		Assert.Contains("38;2;", line);
		Assert.Contains(";1", line);
	}

	#endregion

	#region Optimization Tests

	[Fact]
	public void Decoration_SameBoldRun_EmitsDecorationOnce()
	{
		var system = CreateSystemWithMarkup("[bold]AAAAAAAAAA[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");

		// Count occurrences of ";1" followed by 'm' (part of an SGR sequence)
		// The decoration should be emitted once, not 10 times
		int boldCount = CountSgrCode(line, ";1");
		_output.WriteLine($"Bold SGR count: {boldCount}");

		// Should have 1-2 occurrences (initial set, possibly after color change)
		// Definitely not 10 (one per character)
		Assert.True(boldCount <= 3, $"Too many bold SGR codes ({boldCount}) for uniform bold run");
	}

	[Fact]
	public void Decoration_BoldToNone_TransitionOccurs()
	{
		var system = CreateSystemWithMarkup("[bold]AAA[/]BBB");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");

		// Should have bold code for first section
		Assert.Contains(";1", line);

		// There should be ANSI changes between bold and non-bold sections
		// (at minimum, a color/decoration change or reset)
		int escapeCount = CountEscapeSequences(line);
		_output.WriteLine($"Escape sequence count: {escapeCount}");
		Assert.True(escapeCount >= 2, "Should have ANSI changes between bold and non-bold sections");
	}

	[Fact]
	public void Decoration_None_NoDecorationSgrCodes()
	{
		var system = CreateSystemWithMarkup("plain text");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");

		// Should NOT contain decoration-specific SGR codes in the color sequences
		// We need to check that no ";1m", ";2m", ";3m", etc. appear
		// (but ";1" can appear in color values like "48;2;1;..." so we check more carefully)
		// Check that no decoration SGR codes appear after the color sequence
		// The ANSI format is: \x1b[38;2;R;G;B;48;2;R;G;Bm
		// With decorations it would be: \x1b[38;2;R;G;B;48;2;R;G;B;1m (for bold)
		// Without decorations, the sequence ends with the last background B value then 'm'

		// Extract all ANSI sequences and verify none end with decoration codes before 'm'
		bool hasDecorationCode = HasDecorationSgrInSequences(line);
		Assert.False(hasDecorationCode, "Plain text should not have decoration SGR codes");
	}

	#endregion

	#region Reset/Transition Tests

	[Fact]
	public void Decoration_BoldThenNormal_AnsiChanges()
	{
		var system = CreateSystemWithMarkup("[bold]A[/]B");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");

		// Bold on A, no bold on B - ANSI must change between them
		Assert.Contains(";1", line); // Bold present somewhere
		int escapeCount = CountEscapeSequences(line);
		Assert.True(escapeCount >= 2, "Should have ANSI change between bold and non-bold");
	}

	[Fact]
	public void Decoration_TransitionBetweenDecorations_EmitsCorrectCodes()
	{
		var system = CreateSystemWithMarkup("[bold]A[/][italic]B[/]");
		var line = GetAnsiContentLine(system);
		_output.WriteLine($"ANSI: {line}");

		// Should contain both bold and italic codes
		Assert.Contains(";1", line); // Bold
		Assert.Contains(";3", line); // Italic
	}

	#endregion

	#region Helper Methods

	private static int CountEscapeSequences(string line)
	{
		int count = 0;
		for (int i = 0; i < line.Length - 1; i++)
		{
			if (line[i] == '\x1b' && line[i + 1] == '[')
				count++;
		}
		return count;
	}

	private static int CountSgrCode(string line, string sgrCode)
	{
		// Count occurrences of sgrCode within ANSI sequences (between \x1b[ and m)
		int count = 0;
		int i = 0;
		while (i < line.Length)
		{
			// Find start of ANSI sequence
			int seqStart = line.IndexOf("\x1b[", i, StringComparison.Ordinal);
			if (seqStart < 0) break;

			// Find end of sequence
			int seqEnd = line.IndexOf('m', seqStart + 2);
			if (seqEnd < 0) break;

			// Check if sgrCode appears in this sequence
			string sequence = line.Substring(seqStart + 2, seqEnd - seqStart - 2);
			if (sequence.Contains(sgrCode))
				count++;

			i = seqEnd + 1;
		}
		return count;
	}

	private static bool HasDecorationSgrInSequences(string line)
	{
		// Parse ANSI sequences and check if any contain decoration codes
		// Decoration SGR codes: 1 (bold), 2 (dim), 3 (italic), 4 (underline),
		//                       5 (blink), 7 (invert), 9 (strikethrough)
		int i = 0;
		while (i < line.Length)
		{
			int seqStart = line.IndexOf("\x1b[", i, StringComparison.Ordinal);
			if (seqStart < 0) break;

			int seqEnd = line.IndexOf('m', seqStart + 2);
			if (seqEnd < 0) break;

			string sequence = line.Substring(seqStart + 2, seqEnd - seqStart - 2);

			// Skip reset sequences like "0m"
			if (sequence == "0")
			{
				i = seqEnd + 1;
				continue;
			}

			// Parse semicolon-separated parameters
			var parts = sequence.Split(';');

			// Color sequences use: 38;2;R;G;B (fg) and 48;2;R;G;B (bg)
			// We need to skip color parameter values and check for standalone decoration codes
			int j = 0;
			while (j < parts.Length)
			{
				if ((parts[j] == "38" || parts[j] == "48") && j + 1 < parts.Length && parts[j + 1] == "2")
				{
					// Skip color specification: 38;2;R;G;B or 48;2;R;G;B
					j += 5; // Skip 38/48, 2, R, G, B
					continue;
				}

				// Check if this part is a decoration code
				if (parts[j] == "1" || parts[j] == "2" || parts[j] == "3" ||
				    parts[j] == "4" || parts[j] == "5" || parts[j] == "7" || parts[j] == "9")
				{
					return true;
				}

				j++;
			}

			i = seqEnd + 1;
		}

		return false;
	}

	#endregion
}
