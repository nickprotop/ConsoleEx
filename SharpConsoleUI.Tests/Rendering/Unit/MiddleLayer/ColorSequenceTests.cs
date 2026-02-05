using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.MiddleLayer;

/// <summary>
/// Tests for ANSI color sequence encoding (RGB format).
/// Validates that colors are correctly encoded as ANSI escape sequences
/// in the format: ESC[38;2;R;G;Bm for foreground, ESC[48;2;R;G;Bm for background.
/// </summary>
public class ColorSequenceTests
{
	private readonly ITestOutputHelper _output;

	public ColorSequenceTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void ColorSequence_RedForeground_CorrectRgbEncoding()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Red Text"
		};

		window.AddControl(new MarkupControl(new List<string> { "[red]RED[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI should contain RGB encoding for red
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Red is (255, 0, 0) - should appear as 38;2;255;0;0
		Assert.Contains("38;2;255;0;0", contentLine); // Foreground red
	}

	[Fact]
	public void ColorSequence_BlueBackground_CorrectRgbEncoding()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Blue BG",
			BackgroundColor = Color.Blue
		};

		window.AddControl(new MarkupControl(new List<string> { "Text" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI should contain RGB encoding for blue background
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Blue is (0, 0, 255) - should appear as 48;2;0;0;255
		Assert.Contains("48;2;0;0;255", contentLine); // Background blue
	}

	[Fact]
	public void ColorSequence_CustomRgbColor_CorrectEncoding()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Custom Color"
		};

		// Use a custom RGB color (127, 63, 200)
		window.AddControl(new MarkupControl(new List<string> { "[rgb(127,63,200)]Custom[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI should encode custom RGB values
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Should contain RGB encoding for custom color
		Assert.Contains("38;2;127;63;200", contentLine);
	}

	[Fact]
	public void ColorSequence_GreenBackground_CorrectRgbEncoding()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Green BG"
		};

		window.AddControl(new MarkupControl(new List<string> { "[white on green]Text[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI should contain RGB encoding for green background
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Green is (0, 128, 0) - should appear as 48;2;0;128;0
		Assert.Contains("48;2;0;128;0", contentLine); // Background green
	}

	[Fact]
	public void ColorSequence_WhiteForeground_CorrectRgbEncoding()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "White Text"
		};

		window.AddControl(new MarkupControl(new List<string> { "[white]WHITE[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI should contain RGB encoding for white
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// White is (255, 255, 255) - should appear as 38;2;255;255;255
		Assert.Contains("38;2;255;255;255", contentLine); // Foreground white
	}

	[Fact]
	public void ColorSequence_BlackBackground_CorrectRgbEncoding()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Black BG",
			BackgroundColor = Color.Black
		};

		window.AddControl(new MarkupControl(new List<string> { "Text" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI should contain RGB encoding for black background
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Black is (0, 0, 0) - should appear as 48;2;0;0;0
		Assert.Contains("48;2;0;0;0", contentLine); // Background black
	}

	[Fact]
	public void ColorSequence_ForegroundAndBackground_BothEncoded()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "FG+BG"
		};

		// Yellow text on blue background
		window.AddControl(new MarkupControl(new List<string> { "[yellow on blue]Text[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - ANSI should contain both foreground and background
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Yellow is (255, 255, 0), Blue is (0, 0, 255)
		Assert.Contains("38;2;255;255;0", contentLine); // Foreground yellow
		Assert.Contains("48;2;0;0;255", contentLine);   // Background blue
	}

	[Fact]
	public void ColorSequence_MultipleColors_AllEncoded()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 50,
			Height = 10,
			Title = "Rainbow"
		};

		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]R[/][green]G[/][blue]B[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should contain RGB codes for all colors
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Should have red, green, and blue foreground codes
		Assert.Contains("38;2;255;0;0", contentLine);   // Red
		Assert.Contains("38;2;0;128;0", contentLine);   // Green
		Assert.Contains("38;2;0;0;255", contentLine);   // Blue
	}

	[Fact]
	public void ColorSequence_ResetSequence_IncludesReset()
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

		// Assert - line should end with reset
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Should end with reset sequence
		Assert.Contains("\x1b[0m", contentLine);
	}

	[Fact]
	public void ColorSequence_SameColorTwice_OptimizesSequences()
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

		// Same color applied multiple times
		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]A[/][red]B[/][red]C[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should not repeat color code unnecessarily
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Count occurrences of red color code
		int redCount = 0;
		int searchIndex = 0;
		string redCode = "38;2;255;0;0";

		while ((searchIndex = contentLine.IndexOf(redCode, searchIndex)) != -1)
		{
			redCount++;
			searchIndex += redCode.Length;
		}

		_output.WriteLine($"Red color code count: {redCount}");

		// Should optimize and not repeat 3 times
		Assert.True(redCount <= 2, $"Found {redCount} red color codes, expected optimization");
	}

	[Fact]
	public void ColorSequence_GrayscaleColors_CorrectEncoding()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Grayscale"
		};

		// Gray color (128, 128, 128)
		window.AddControl(new MarkupControl(new List<string> { "[rgb(128,128,128)]Gray[/]" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should encode grayscale color correctly
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Should have equal RGB values for gray
		Assert.Contains("38;2;128;128;128", contentLine);
	}

	[Fact]
	public void ColorSequence_ColorTransition_BothColorsEncoded()
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

		// Color changes from red to blue
		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]AAA[/][blue]BBB[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should have both color codes
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Both colors should be encoded
		Assert.Contains("38;2;255;0;0", contentLine);   // Red
		Assert.Contains("38;2;0;0;255", contentLine);   // Blue
	}
}
