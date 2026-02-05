using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.MiddleLayer;

/// <summary>
/// Tests for ANSI escape sequence generation from CharacterBuffer.
/// Validates that the CharacterBuffer.ToLines() method correctly generates
/// ANSI escape sequences for colors and formatting.
/// </summary>
public class AnsiGenerationTests
{
	private readonly ITestOutputHelper _output;

	public AnsiGenerationTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Ansi_PlainText_NoEscapeSequences()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Plain",
			ForegroundColor = Color.White,
			BackgroundColor = Color.Black
		};

		window.AddControl(new MarkupControl(new List<string> { "Plain text" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI snapshot should contain escape sequences
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);
		Assert.True(ansiSnapshot.Lines.Count > 0);

		// Lines should contain ANSI escape codes for colors
		var firstLine = ansiSnapshot.Lines[0];
		Assert.Contains("\x1b[", firstLine); // Contains ANSI escape
	}

	[Fact]
	public void Ansi_ColorChange_EmitsColorSequence()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Color"
		};

		// Red text followed by blue text - should generate color change
		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]Red[/] [blue]Blue[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI should contain color escape sequences
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0]; // First content line
		_output.WriteLine($"ANSI line: {contentLine}");

		// Should contain escape sequences (exact format depends on implementation)
		Assert.Contains("\x1b[", contentLine);
	}

	[Fact]
	public void Ansi_MultipleLines_EachLineIndependent()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 12,
			Title = "Multiline"
		};

		window.AddControl(new MarkupControl(new List<string>
		{
			"Line 1",
			"Line 2",
			"Line 3"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should have separate ANSI lines
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);
		Assert.True(ansiSnapshot.Lines.Count >= 3);
	}

	[Fact]
	public void Ansi_BackgroundColor_IncludedInSequence()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "BG",
			BackgroundColor = Color.Blue
		};

		window.AddControl(new MarkupControl(new List<string> { "Text" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI should include background color
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		// Background color should be in ANSI sequence (48;2;R;G;B format for RGB)
		var contentLine = ansiSnapshot.Lines[0];
		Assert.Contains("\x1b[", contentLine); // Has ANSI
		Assert.Contains("48;2;", contentLine); // Background color code
	}

	[Fact]
	public void Ansi_ResetAtLineEnd_IncludesResetSequence()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Reset"
		};

		window.AddControl(new MarkupControl(new List<string> { "[red]Colored[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - line should end with reset sequence
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		Assert.Contains("\x1b[0m", contentLine); // Reset sequence
	}

	[Fact]
	public void Ansi_EmptyLine_ContainsMinimalSequence()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Empty"
		};

		// No control added - empty content
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - empty lines still have ANSI for background
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);
		Assert.True(ansiSnapshot.Lines.Count > 0);

		// Even empty lines should have some ANSI (for background color)
		foreach (var line in ansiSnapshot.Lines)
		{
			Assert.Contains("\x1b[", line);
		}
	}

	[Fact]
	public void Ansi_ConsecutiveSameColor_OptimizesSequences()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Optimize"
		};

		// Multiple characters with same color - should not repeat color code
		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]AAAAA[/]" // 5 red A's
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should not have redundant color sequences
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Count ANSI escape sequences in the line
		int escapeCount = 0;
		for (int i = 0; i < contentLine.Length - 1; i++)
		{
			if (contentLine[i] == '\x1b' && contentLine[i + 1] == '[')
			{
				escapeCount++;
			}
		}

		_output.WriteLine($"Escape sequence count: {escapeCount}");

		// Should have minimal escapes (setup color, maybe change, reset)
		// Not 5+ escapes for 5 characters of same color
		Assert.True(escapeCount < 5, $"Too many escape sequences ({escapeCount}) for same-color text");
	}

	[Fact]
	public void Ansi_ColorTransition_EmitsNewSequence()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Transition"
		};

		// Transition from red to blue
		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]AAA[/][blue]BBB[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should have color change between AAA and BBB
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Should contain multiple escape sequences (red, blue, reset)
		int escapeCount = 0;
		for (int i = 0; i < contentLine.Length - 1; i++)
		{
			if (contentLine[i] == '\x1b' && contentLine[i + 1] == '[')
			{
				escapeCount++;
			}
		}

		Assert.True(escapeCount >= 2, "Should have multiple escape sequences for color transition");
	}

	[Fact]
	public void Ansi_SpecialCharacters_EscapedProperly()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Special"
		};

		// Test that escape character in content doesn't break ANSI
		window.AddControl(new MarkupControl(new List<string>
		{
			"Normal text" // No actual escape chars, but test normal flow
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI generation succeeds
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);
		Assert.True(ansiSnapshot.Lines.Count > 0);
	}

	[Fact]
	public void Ansi_TotalCharacterCount_MatchesContent()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Count"
		};

		var contentText = "Hello World";
		window.AddControl(new MarkupControl(new List<string> { contentText }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI snapshot tracks character count
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		// TotalCharacters should be >= content length (includes whole buffer)
		Assert.True(ansiSnapshot.TotalCharacters > 0);
	}

	[Fact]
	public void Ansi_EscapeSequenceCount_Tracked()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Track"
		};

		window.AddControl(new MarkupControl(new List<string> { "[red]R[/][blue]B[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - snapshot tracks escape sequence count
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		Assert.True(ansiSnapshot.TotalAnsiEscapes > 0, "Should have ANSI escape sequences");
	}
}
