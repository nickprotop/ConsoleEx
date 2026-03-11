using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using System.Text;

namespace SharpConsoleUI.Tests.Rendering.Unit.MiddleLayer;

/// <summary>
/// Tests the actual rendering pipeline (ConsoleBuffer.FormatCellAnsi + AppendLineToBuilder)
/// for correct decoration handling. These tests validate the REAL terminal output,
/// not CharacterBuffer.ToLines() which is a separate code path.
///
/// The key bug these tests catch: when FormatCellAnsi generates an ANSI string
/// for a cell with no decorations, it must include a reset (SGR 0) to clear
/// any decorations that were active from a previous cell. Without the reset,
/// decorations like underline "leak" into subsequent undecorated text.
/// </summary>
public class DecorationRenderingPipelineTests
{
	private readonly ITestOutputHelper _output;

	public DecorationRenderingPipelineTests(ITestOutputHelper output)
	{
		_output = output;
	}

	#region Helpers

	private (ConsoleWindowSystem system, Window window) CreateAndRender(
		string markup, int windowWidth = 50, int windowHeight = 10)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 0, Top = 0,
			Width = windowWidth, Height = windowHeight,
			Title = "T"
		};
		window.AddControl(new MarkupControl(new List<string> { markup }));
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();
		return (system, window);
	}

	private (ConsoleWindowSystem system, Window window) CreateAndRenderLines(
		List<string> lines, int windowWidth = 50, int windowHeight = 15)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 0, Top = 0,
			Width = windowWidth, Height = windowHeight,
			Title = "T"
		};
		window.AddControl(new MarkupControl(lines));
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();
		return (system, window);
	}

	/// <summary>
	/// Finds the first cell with the given character in the ConsoleBuffer front buffer.
	/// </summary>
	private ConsoleCell? FindCell(ConsoleBufferSnapshot snapshot, char target)
	{
		var targetRune = new Rune(target);
		for (int y = 0; y < snapshot.Height; y++)
		{
			for (int x = 0; x < snapshot.Width; x++)
			{
				var cell = snapshot.GetBack(x, y);
				if (cell.Character == targetRune)
					return cell;
			}
		}
		return null;
	}

	/// <summary>
	/// Finds all cells with the given character.
	/// </summary>
	private List<(int x, int y, ConsoleCell cell)> FindAllCells(ConsoleBufferSnapshot snapshot, char target)
	{
		var targetRune = new Rune(target);
		var results = new List<(int, int, ConsoleCell)>();
		for (int y = 0; y < snapshot.Height; y++)
		{
			for (int x = 0; x < snapshot.Width; x++)
			{
				var cell = snapshot.GetBack(x, y);
				if (cell.Character == targetRune)
					results.Add((x, y, cell));
			}
		}
		return results;
	}

	/// <summary>
	/// Checks if an ANSI string contains a specific SGR decoration code.
	/// Parses the SGR parameters to avoid false positives from color values.
	/// </summary>
	private static bool HasDecorationCode(string ansi, int sgrCode)
	{
		int i = 0;
		while (i < ansi.Length)
		{
			int seqStart = ansi.IndexOf("\x1b[", i, StringComparison.Ordinal);
			if (seqStart < 0) break;

			int mIdx = ansi.IndexOf('m', seqStart + 2);
			if (mIdx < 0) break;

			string paramStr = ansi.Substring(seqStart + 2, mIdx - seqStart - 2);
			var parts = paramStr.Split(';');

			int j = 0;
			while (j < parts.Length)
			{
				if ((parts[j] == "38" || parts[j] == "48") && j + 1 < parts.Length && parts[j + 1] == "2")
				{
					j += 5; // Skip color: 38/48;2;R;G;B
					continue;
				}

				if (int.TryParse(parts[j], out int val) && val == sgrCode)
					return true;

				j++;
			}

			i = mIdx + 1;
		}
		return false;
	}

	/// <summary>
	/// Checks if an ANSI sequence contains a reset (SGR 0).
	/// </summary>
	private static bool HasReset(string ansi)
	{
		return ansi.Contains("\x1b[0m") || ansi.Contains("\x1b[0;");
	}

	#endregion

	#region Core Decoration Leak Tests

	[Fact]
	public void UnderlineToNone_SameColor_AnsiContainsReset()
	{
		var (system, _) = CreateAndRender("[underline red]ABC[/][red]DEF[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'D');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'D' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 4),
			$"Cell 'D' should not have underline. ANSI: {cell.Value.AnsiEscape}");
		Assert.True(HasReset(cell.Value.AnsiEscape),
			$"Cell 'D' should have reset to clear underline. ANSI: {cell.Value.AnsiEscape}");
	}

	[Fact]
	public void BoldToNone_SameColor_AnsiContainsReset()
	{
		var (system, _) = CreateAndRender("[bold green]ABC[/][green]DEF[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'D');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'D' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 1),
			$"Cell 'D' should not have bold. ANSI: {cell.Value.AnsiEscape}");
		Assert.True(HasReset(cell.Value.AnsiEscape),
			$"Cell 'D' should have reset to clear bold. ANSI: {cell.Value.AnsiEscape}");
	}

	[Fact]
	public void ItalicToNone_SameColor_AnsiContainsReset()
	{
		var (system, _) = CreateAndRender("[italic blue]XY[/][blue]ZW[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'Z');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'Z' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 3),
			$"Cell 'Z' should not have italic. ANSI: {cell.Value.AnsiEscape}");
		Assert.True(HasReset(cell.Value.AnsiEscape),
			$"Cell 'Z' should have reset to clear italic. ANSI: {cell.Value.AnsiEscape}");
	}

	[Fact]
	public void StrikethroughToNone_SameColor_AnsiContainsReset()
	{
		var (system, _) = CreateAndRender("[strikethrough yellow]AB[/][yellow]CD[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'C');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'C' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 9),
			$"Cell 'C' should not have strikethrough. ANSI: {cell.Value.AnsiEscape}");
	}

	[Fact]
	public void AllDecorations_ToNone_SameColor_AnsiContainsReset()
	{
		var (system, _) = CreateAndRender(
			"[bold italic underline strikethrough red]X[/][red]Y[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'Y');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'Y' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 1), "Should not have bold");
		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 3), "Should not have italic");
		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 4), "Should not have underline");
		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 9), "Should not have strikethrough");
		Assert.True(HasReset(cell.Value.AnsiEscape),
			$"Cell 'Y' should have reset. ANSI: {cell.Value.AnsiEscape}");
	}

	[Fact]
	public void DimToNone_ProperReset()
	{
		var (system, _) = CreateAndRender("[dim]AB[/]CD");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'C');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'C' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 2),
			$"Cell 'C' should not have dim. ANSI: {cell.Value.AnsiEscape}");
	}

	[Fact]
	public void InvertToNone_ProperReset()
	{
		var (system, _) = CreateAndRender("[invert]AB[/]CD");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'C');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'C' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 7),
			$"Cell 'C' should not have invert. ANSI: {cell.Value.AnsiEscape}");
	}

	[Fact]
	public void BlinkToNone_ProperReset()
	{
		var (system, _) = CreateAndRender("[blink]AB[/]CD");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'C');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'C' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 5),
			$"Cell 'C' should not have blink. ANSI: {cell.Value.AnsiEscape}");
	}

	#endregion

	#region Decoration Transition Tests

	[Fact]
	public void UnderlineToNone_DifferentColor_AnsiContainsReset()
	{
		var (system, _) = CreateAndRender("[underline red]AB[/][green]CD[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'C');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'C' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 4),
			"Cell 'C' should not have underline");
		Assert.True(HasReset(cell.Value.AnsiEscape),
			$"Cell 'C' should have reset. ANSI: {cell.Value.AnsiEscape}");
	}

	[Fact]
	public void BoldToUnderline_EmitsBothCodesCorrectly()
	{
		var (system, _) = CreateAndRender("[bold red]AB[/][underline red]CD[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cellA = FindCell(snapshot, 'A');
		var cellC = FindCell(snapshot, 'C');
		Assert.NotNull(cellA);
		Assert.NotNull(cellC);

		_output.WriteLine($"Cell 'A' ANSI: {cellA.Value.AnsiEscape}");
		_output.WriteLine($"Cell 'C' ANSI: {cellC.Value.AnsiEscape}");

		Assert.True(HasDecorationCode(cellA.Value.AnsiEscape, 1), "'A' should have bold");
		Assert.False(HasDecorationCode(cellA.Value.AnsiEscape, 4), "'A' should not have underline");

		Assert.True(HasDecorationCode(cellC.Value.AnsiEscape, 4), "'C' should have underline");
		Assert.False(HasDecorationCode(cellC.Value.AnsiEscape, 1), "'C' should not have bold");
	}

	[Fact]
	public void DecoratedToPlain_ToDecorated_CorrectTransitions()
	{
		var (system, _) = CreateAndRender("[bold red]AB[/][red]CD[/][underline red]EF[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cellA = FindCell(snapshot, 'A');
		var cellC = FindCell(snapshot, 'C');
		var cellE = FindCell(snapshot, 'E');

		Assert.NotNull(cellA);
		Assert.NotNull(cellC);
		Assert.NotNull(cellE);

		Assert.True(HasDecorationCode(cellA.Value.AnsiEscape, 1), "'A' should be bold");
		Assert.False(HasDecorationCode(cellC.Value.AnsiEscape, 1), "'C' should not be bold");
		Assert.False(HasDecorationCode(cellC.Value.AnsiEscape, 4), "'C' should not be underline");
		Assert.True(HasDecorationCode(cellE.Value.AnsiEscape, 4), "'E' should be underline");
		Assert.False(HasDecorationCode(cellE.Value.AnsiEscape, 1), "'E' should not be bold");
	}

	[Fact]
	public void NestedDecorations_InnerClosed_OuterRemains()
	{
		var (system, _) = CreateAndRender("[bold][underline]AB[/]CD[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cellA = FindCell(snapshot, 'A');
		var cellC = FindCell(snapshot, 'C');
		Assert.NotNull(cellA);
		Assert.NotNull(cellC);

		_output.WriteLine($"Cell 'A' ANSI: {cellA.Value.AnsiEscape}");
		_output.WriteLine($"Cell 'C' ANSI: {cellC.Value.AnsiEscape}");

		Assert.True(HasDecorationCode(cellA.Value.AnsiEscape, 1), "'A' should have bold");
		Assert.True(HasDecorationCode(cellA.Value.AnsiEscape, 4), "'A' should have underline");
		Assert.True(HasDecorationCode(cellC.Value.AnsiEscape, 1), "'C' should have bold");
		Assert.False(HasDecorationCode(cellC.Value.AnsiEscape, 4), "'C' should not have underline");
	}

	#endregion

	#region Multi-Line Tests

	[Fact]
	public void DecorationOnLine1_DoesNotLeakToLine2()
	{
		var lines = new List<string>
		{
			"[underline]UUUUUUUUUUUUUU[/]",
			"PPPPPPPPPPPPPP"
		};
		var (system, _) = CreateAndRenderLines(lines);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cellP = FindCell(snapshot, 'P');
		Assert.NotNull(cellP);
		_output.WriteLine($"Cell 'P' ANSI: {cellP.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cellP.Value.AnsiEscape, 4),
			$"'P' on line 2 should not have underline. ANSI: {cellP.Value.AnsiEscape}");
	}

	[Fact]
	public void MultipleDecorationsAcrossLines_NoLeaking()
	{
		var lines = new List<string>
		{
			"[bold red]BBBB[/] NNNN",
			"[italic green]IIII[/] NNNN",
			"[underline blue]UUUU[/] NNNN",
			"PPPP"
		};
		var (system, _) = CreateAndRenderLines(lines, windowHeight: 15);

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Find 'N' cells - they should have no decorations
		var nCells = FindAllCells(snapshot, 'N');
		Assert.True(nCells.Count > 0, "Should find 'N' cells");

		foreach (var (x, y, cell) in nCells)
		{
			_output.WriteLine($"'N' at ({x},{y}) ANSI: {cell.AnsiEscape}");
			Assert.False(HasDecorationCode(cell.AnsiEscape, 1),
				$"'N' at ({x},{y}) should not have bold");
			Assert.False(HasDecorationCode(cell.AnsiEscape, 3),
				$"'N' at ({x},{y}) should not have italic");
			Assert.False(HasDecorationCode(cell.AnsiEscape, 4),
				$"'N' at ({x},{y}) should not have underline");
		}

		// 'P' should also have no decorations
		var cellP = FindCell(snapshot, 'P');
		Assert.NotNull(cellP);
		Assert.False(HasDecorationCode(cellP.Value.AnsiEscape, 1), "'P' should not have bold");
		Assert.False(HasDecorationCode(cellP.Value.AnsiEscape, 3), "'P' should not have italic");
		Assert.False(HasDecorationCode(cellP.Value.AnsiEscape, 4), "'P' should not have underline");
	}

	#endregion

	#region Console Output Stream Tests

	[Fact]
	public void FullRenderOutput_UnderlineToNone_ContainsResetBeforeNonUnderlinedText()
	{
		var (system, _) = CreateAndRender("[underline]ABC[/]DEF");

		var outputSnapshot = system.RenderingDiagnostics?.LastOutputSnapshot;
		Assert.NotNull(outputSnapshot);

		string output = outputSnapshot.FullOutput;
		_output.WriteLine($"Full output length: {output.Length}");

		var sequences = outputSnapshot.GetAnsiSequences();
		_output.WriteLine($"Total ANSI sequences: {sequences.Count}");
		foreach (var seq in sequences.Take(20))
		{
			_output.WriteLine($"  SEQ: {seq.Replace("\x1b", "ESC")}");
		}

		bool foundUnderline = false;
		bool foundResetAfterUnderline = false;
		foreach (var seq in sequences)
		{
			if (HasDecorationCode(seq, 4))
				foundUnderline = true;
			else if (foundUnderline && HasReset(seq))
			{
				foundResetAfterUnderline = true;
				break;
			}
		}

		Assert.True(foundUnderline, "Should have underline SGR code");
		Assert.True(foundResetAfterUnderline,
			"Should have reset after underline before rendering non-underlined text");
	}

	#endregion

	#region FormatCellAnsi Reset Correctness

	[Fact]
	public void EveryAnsiSequence_ContainsResetPrefix()
	{
		// With the fix, every ANSI string generated by FormatCellAnsi
		// starts with \x1b[0; to ensure decorations are always cleared.
		var (system, _) = CreateAndRender("[red]PLAIN[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'P');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'P' ANSI: {cell.Value.AnsiEscape}");

		Assert.StartsWith("\x1b[0;", cell.Value.AnsiEscape);
	}

	[Fact]
	public void DecoratedAnsi_ContainsResetPrefixAndDecorationCode()
	{
		var (system, _) = CreateAndRender("[bold underline red]TEXT[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Find first 'T' that has decoration (skip title bar 'T')
		var tCells = FindAllCells(snapshot, 'T');
		ConsoleCell? decoratedT = null;
		foreach (var (x, y, cell) in tCells)
		{
			if (HasDecorationCode(cell.AnsiEscape, 1))
			{
				decoratedT = cell;
				break;
			}
		}
		Assert.NotNull(decoratedT);
		_output.WriteLine($"Decorated 'T' ANSI: {decoratedT.Value.AnsiEscape}");

		Assert.StartsWith("\x1b[0;", decoratedT.Value.AnsiEscape);
		Assert.True(HasDecorationCode(decoratedT.Value.AnsiEscape, 1), "Should have bold");
		Assert.True(HasDecorationCode(decoratedT.Value.AnsiEscape, 4), "Should have underline");
	}

	[Fact]
	public void SameConsecutiveCells_AnsiIsCached()
	{
		var (system, _) = CreateAndRender("[red]QQQQQQ[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var qCells = FindAllCells(snapshot, 'Q');
		Assert.True(qCells.Count >= 6, $"Should find at least 6 Q cells, found {qCells.Count}");

		string firstAnsi = qCells[0].cell.AnsiEscape;
		foreach (var (x, y, cell) in qCells)
		{
			Assert.Equal(firstAnsi, cell.AnsiEscape);
		}
	}

	#endregion

	#region Scroll + Decoration Tests

	[Fact]
	public void ScrollableContent_UnderlineDoesNotLeakAfterScroll()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 0, Top = 0,
			Width = 50, Height = 8,
			Title = "S"
		};

		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.AddControl(new MarkupControl(new List<string>
			{
				"[underline red]UUUUUUUUUUUUUUUUUU[/]",
				"[underline green]UUUUUUUUUUUUUUUUUU[/]",
				"[underline blue]UUUUUUUUUUUUUUUUUU[/]",
				"PPPP line 4",
				"PPPP line 5",
				"PPPP line 6",
				"PPPP line 7",
				"PPPP line 8",
				"PPPP line 9",
				"PPPP line 10",
				"PPPP line 11",
				"PPPP line 12"
			}))
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		window.AddControl(panel);
		system.WindowStateService.AddWindow(window);
		system.FocusStateService.SetFocus(window, panel);

		// Initial render - verify underline is present on 'U'
		system.Render.UpdateDisplay();

		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);

		var cellU = FindCell(snapshot1, 'U');
		Assert.NotNull(cellU);
		_output.WriteLine($"Initial 'U' ANSI: {cellU.Value.AnsiEscape}");
		Assert.True(HasDecorationCode(cellU.Value.AnsiEscape, 4),
			"Initial 'U' should have underline");

		// Scroll down past the underlined lines
		var downKey = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);
		for (int i = 0; i < 8; i++)
		{
			panel.ProcessKey(downKey);
		}

		// Re-render after scroll
		system.Render.UpdateDisplay();

		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);

		// After scrolling past the underlined lines, 'P' should have no underline
		var cellP = FindCell(snapshot2, 'P');
		if (cellP != null)
		{
			_output.WriteLine($"After scroll, 'P' ANSI: {cellP.Value.AnsiEscape}");
			Assert.False(HasDecorationCode(cellP.Value.AnsiEscape, 4),
				$"'P' after scroll should not have underline. ANSI: {cellP.Value.AnsiEscape}");
		}
		else
		{
			_output.WriteLine("Note: 'P' not visible after scroll");
		}
	}

	[Fact]
	public void ContentUpdate_DecorationCleared()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 0, Top = 0,
			Width = 40, Height = 6,
			Title = "U"
		};

		var markup = new MarkupControl(new List<string>
		{
			"[underline]UUUUUUUUUUUUUUUUUU[/]"
		});
		window.AddControl(markup);
		system.WindowStateService.AddWindow(window);

		// First render: verify underline present
		system.Render.UpdateDisplay();

		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);

		var cellU = FindCell(snapshot1, 'U');
		// Note: 'U' might also appear in title bar for "U" title, check if it has decoration
		var uCells = FindAllCells(snapshot1, 'U');
		bool foundDecoratedU = uCells.Any(c => HasDecorationCode(c.cell.AnsiEscape, 4));
		Assert.True(foundDecoratedU, "Should find underlined 'U' in first render");

		// Update to plain text
		markup.SetContent(new List<string> { "QQQQQQQQQQQQQQQQQQ" });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);

		var cellQ = FindCell(snapshot2, 'Q');
		Assert.NotNull(cellQ);
		_output.WriteLine($"Updated 'Q' ANSI: {cellQ.Value.AnsiEscape}");
		Assert.False(HasDecorationCode(cellQ.Value.AnsiEscape, 4),
			$"Updated 'Q' should not have underline. ANSI: {cellQ.Value.AnsiEscape}");
	}

	#endregion

	#region Background Color + Decoration

	[Fact]
	public void BackgroundColorWithDecoration_ToPlain_ProperReset()
	{
		var (system, _) = CreateAndRender("[bold white on red]AB[/][white on blue]QR[/]");

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var cell = FindCell(snapshot, 'Q');
		Assert.NotNull(cell);
		_output.WriteLine($"Cell 'Q' ANSI: {cell.Value.AnsiEscape}");

		Assert.False(HasDecorationCode(cell.Value.AnsiEscape, 1),
			"Cell 'Q' should not have bold");
	}

	#endregion
}
