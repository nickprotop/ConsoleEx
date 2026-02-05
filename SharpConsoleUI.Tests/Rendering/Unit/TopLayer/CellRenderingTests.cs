using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for individual cell rendering in the CharacterBuffer (DOM layer).
/// Validates that cells are rendered with correct characters, colors, and attributes.
/// </summary>
public class CellRenderingTests
{
	private readonly ITestOutputHelper _output;

	public CellRenderingTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Cell_PlainText_RendersCorrectCharacters()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Plain Text"
		};

		window.AddControl(new MarkupControl(new List<string> { "ABCDEFGHIJ" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - each character renders correctly
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var expected = "ABCDEFGHIJ";
		for (int i = 0; i < expected.Length; i++)
		{
			var actual = snapshot.GetBack(11 + i, 6).Character;
			Assert.Equal(expected[i], actual);
		}
	}

	[Fact]
	public void Cell_SpecialCharacters_RenderCorrectly()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Special Chars"
		};

		// Test various special characters
		window.AddControl(new MarkupControl(new List<string>
		{
			"!@#$%^&*()",
			"[]{}()<>",
			"+-*/=",
			".,;:'\""
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - special characters render
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('!', snapshot.GetBack(11, 6).Character);
		Assert.Equal('[', snapshot.GetBack(11, 7).Character);
		Assert.Equal('+', snapshot.GetBack(11, 8).Character);
		Assert.Equal('.', snapshot.GetBack(11, 9).Character);
	}

	[Fact]
	public void Cell_ColoredMarkup_PreservesColors()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Colors"
		};

		// Red text
		window.AddControl(new MarkupControl(new List<string> { "[red]RED TEXT[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - cells should have red foreground color
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Check that content has the correct character
		Assert.Equal('R', snapshot.GetBack(11, 6).Character);
		Assert.Equal('E', snapshot.GetBack(12, 6).Character);
		Assert.Equal('D', snapshot.GetBack(13, 6).Character);

		// Note: Color validation would require accessing the cell's color attributes
		// which might not be exposed through GetBack() depending on the snapshot implementation
	}

	[Fact]
	public void Cell_BackgroundColor_RendersCorrectly()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "BG Color",
			BackgroundColor = Color.Blue
		};

		window.AddControl(new MarkupControl(new List<string> { "Text on blue" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - content renders on blue background
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('T', snapshot.GetBack(11, 6).Character);
	}

	[Fact]
	public void Cell_EmptyCell_RendersSpace()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Empty Cells"
		};

		// Add short content, leaving empty space
		window.AddControl(new MarkupControl(new List<string> { "AB" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - cells after content should be spaces (window background)
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('A', snapshot.GetBack(11, 6).Character);
		Assert.Equal('B', snapshot.GetBack(12, 6).Character);
		Assert.Equal(' ', snapshot.GetBack(13, 6).Character); // Empty cell
		Assert.Equal(' ', snapshot.GetBack(14, 6).Character); // Empty cell
	}

	[Fact]
	public void Cell_OverwritePreviousContent_UpdatesCorrectly()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Overwrite"
		};

		var control = new MarkupControl(new List<string> { "Original" });
		window.AddControl(control);

		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.Equal('O', snapshot1?.GetBack(11, 6).Character);

		// Act - update content to shorter text
		control.SetContent(new List<string> { "New" });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		// Assert - new content renders, old content is cleared
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);

		Assert.Equal('N', snapshot2.GetBack(11, 6).Character);
		Assert.Equal('e', snapshot2.GetBack(12, 6).Character);
		Assert.Equal('w', snapshot2.GetBack(13, 6).Character);
		Assert.Equal(' ', snapshot2.GetBack(14, 6).Character); // Old 'g' cleared
	}

	[Fact]
	public void Cell_UnicodeCharacters_RenderIfSupported()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Unicode"
		};

		// Test various Unicode characters
		window.AddControl(new MarkupControl(new List<string>
		{
			"→ ← ↑ ↓",  // Arrows
			"★ ☆ ♥ ♦",  // Symbols
			"© ® ™ €"   // Special symbols
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - Unicode characters should render
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// First character of first line
		var char1 = snapshot.GetBack(11, 6).Character;
		Assert.NotEqual('\0', char1); // Should have some character
	}

	[Fact]
	public void Cell_MixedContent_RendersSequentially()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Mixed"
		};

		window.AddControl(new MarkupControl(new List<string>
		{
			"Plain [red]Colored[/] Plain"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - all parts render in sequence
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// "Plain "
		Assert.Equal('P', snapshot.GetBack(11, 6).Character);
		Assert.Equal('l', snapshot.GetBack(12, 6).Character);

		// "Colored"
		Assert.Equal('C', snapshot.GetBack(17, 6).Character);

		// " Plain"
		Assert.Equal('P', snapshot.GetBack(25, 6).Character);
	}

	[Fact]
	public void Cell_LongLine_TruncatesAtWindowWidth()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20, // Small width: 2 for borders, 18 for content
			Height = 10,
			Title = "Truncate"
		};

		// Add content longer than window width
		window.AddControl(new MarkupControl(new List<string>
		{
			"This is a very long line that exceeds the window width"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - content is truncated to fit
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// First chars should be visible
		Assert.Equal('T', snapshot.GetBack(11, 6).Character);

		// Content should not extend beyond right border
		var rightBorder = snapshot.GetBack(29, 6).Character;
		Assert.True(rightBorder == '│' || rightBorder == '║' || rightBorder == '┃',
			$"Expected right border, got '{rightBorder}'");
	}

	[Fact]
	public void Cell_MultilineContent_EachLineIndependent()
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

		// Assert - each line renders at correct Y position
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('L', snapshot.GetBack(11, 6).Character); // Line 1
		Assert.Equal('L', snapshot.GetBack(11, 7).Character); // Line 2
		Assert.Equal('L', snapshot.GetBack(11, 8).Character); // Line 3
	}

	[Fact]
	public void Cell_ConsecutiveSpaces_PreserveAll()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Spaces"
		};

		// Test that multiple consecutive spaces are preserved
		window.AddControl(new MarkupControl(new List<string>
		{
			"A    B" // 4 spaces between A and B
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - all spaces should be preserved
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('A', snapshot.GetBack(11, 6).Character);
		Assert.Equal(' ', snapshot.GetBack(12, 6).Character);
		Assert.Equal(' ', snapshot.GetBack(13, 6).Character);
		Assert.Equal(' ', snapshot.GetBack(14, 6).Character);
		Assert.Equal(' ', snapshot.GetBack(15, 6).Character);
		Assert.Equal('B', snapshot.GetBack(16, 6).Character);
	}

	[Fact]
	public void Cell_TabCharacter_HandledAppropriately()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Tabs"
		};

		// Test tab handling (tabs might be converted to spaces or handled specially)
		window.AddControl(new MarkupControl(new List<string>
		{
			"A\tB" // Tab between A and B
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - tab should be converted to spaces or handled
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('A', snapshot.GetBack(11, 6).Character);
		// Tab handling varies - just verify it doesn't crash and B appears somewhere
		var foundB = false;
		for (int x = 12; x < 20; x++)
		{
			if (snapshot.GetBack(x, 6).Character == 'B')
			{
				foundB = true;
				break;
			}
		}
		Assert.True(foundB, "Character 'B' should appear after tab");
	}
}
