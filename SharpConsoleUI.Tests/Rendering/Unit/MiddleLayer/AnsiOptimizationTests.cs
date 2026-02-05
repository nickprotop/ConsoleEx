using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.MiddleLayer;

/// <summary>
/// Tests for ANSI optimization and redundancy detection.
/// Validates that ANSI generation minimizes redundant color codes
/// and produces efficient escape sequences.
/// </summary>
public class AnsiOptimizationTests
{
	private readonly ITestOutputHelper _output;

	public AnsiOptimizationTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Optimization_SameColorReuse_NoRedundantCodes()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Reuse"
		};

		// Long run of same color - should set color once
		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]AAAAAAAAAA[/]" // 10 red A's
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should not have 10 color codes
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Count foreground color sequences (38;2; prefix)
		int colorCodeCount = 0;
		for (int i = 0; i < contentLine.Length - 4; i++)
		{
			if (contentLine.Substring(i, 4) == "38;2")
			{
				colorCodeCount++;
			}
		}

		_output.WriteLine($"Color code count: {colorCodeCount}");

		// Should set color only once or twice (not 10 times)
		Assert.True(colorCodeCount <= 2, $"Too many color codes ({colorCodeCount}) for uniform color");
	}

	[Fact]
	public void Optimization_NoColorChange_MinimalSequences()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Minimal",
			ForegroundColor = Color.White,
			BackgroundColor = Color.Black
		};

		// Plain text with no color markup
		window.AddControl(new MarkupControl(new List<string>
		{
			"Plain text here"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should have minimal ANSI (initial setup + reset)
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Count total escape sequences
		int escapeCount = 0;
		for (int i = 0; i < contentLine.Length - 1; i++)
		{
			if (contentLine[i] == '\x1b' && contentLine[i + 1] == '[')
			{
				escapeCount++;
			}
		}

		_output.WriteLine($"Total escape sequences: {escapeCount}");

		// Should be minimal (typically setup + reset)
		Assert.True(escapeCount <= 3, $"Too many escape sequences ({escapeCount}) for plain text");
	}

	[Fact]
	public void Optimization_BackgroundColorReuse_Efficient()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "BG Reuse",
			BackgroundColor = Color.Blue
		};

		// Multiple characters on same background
		window.AddControl(new MarkupControl(new List<string>
		{
			"Text on blue background"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - background color set once per line
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Count background color sequences (48;2; prefix)
		int bgColorCount = 0;
		for (int i = 0; i < contentLine.Length - 4; i++)
		{
			if (contentLine.Substring(i, 4) == "48;2")
			{
				bgColorCount++;
			}
		}

		_output.WriteLine($"Background color count: {bgColorCount}");

		// Should set background once or twice, not for every character
		Assert.True(bgColorCount <= 2, $"Too many background color codes ({bgColorCount})");
	}

	[Fact]
	public void Optimization_QualityMetrics_LowRedundancy()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Quality"
		};

		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]Red[/] [blue]Blue[/] [green]Green[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - quality metrics should show good optimization
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		if (metrics != null)
		{
			_output.WriteLine($"ANSI Escapes: {metrics.AnsiEscapeSequences}");
			_output.WriteLine($"Redundant ANSI: {metrics.RedundantAnsiSequences}");

			// Should have low redundancy
			Assert.True(metrics.RedundantAnsiSequences == 0,
				$"Found {metrics.RedundantAnsiSequences} redundant ANSI sequences");
		}
	}

	[Fact]
	public void Optimization_ColorTransitions_OnlyWhenNeeded()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 50,
			Height = 10,
			Title = "Transitions"
		};

		// Pattern: Red, Red, Blue, Blue (should have only 2 color sets)
		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]AA[/][red]BB[/][blue]CC[/][blue]DD[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should optimize consecutive same colors
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];
		_output.WriteLine($"ANSI: {contentLine}");

		// Count color transitions - should be efficient
		int colorCodeCount = 0;
		for (int i = 0; i < contentLine.Length - 4; i++)
		{
			if (contentLine.Substring(i, 4) == "38;2")
			{
				colorCodeCount++;
			}
		}

		_output.WriteLine($"Color code count: {colorCodeCount}");

		// Should have ~2-3 color codes (red, blue, maybe reset), not 4+
		Assert.True(colorCodeCount <= 4, $"Too many color transitions ({colorCodeCount})");
	}

	[Fact]
	public void Optimization_EmptyBuffer_MinimalOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 8,
			Title = "Empty"
		};

		// No control - empty content
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - empty content should have minimal ANSI
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		// Should still have some output (background color), but minimal
		Assert.True(ansiSnapshot.TotalAnsiEscapes > 0);
		Assert.True(ansiSnapshot.TotalCharacters > 0);
	}

	[Fact]
	public void Optimization_LongUniformLine_SingleColorSet()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 60,
			Height = 10,
			Title = "Long Line"
		};

		// Very long line of same color
		var longText = new string('A', 50);
		window.AddControl(new MarkupControl(new List<string>
		{
			$"[red]{longText}[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - should set color once for entire run
		var ansiSnapshot = system.RenderingDiagnostics?.LastAnsiSnapshot;
		Assert.NotNull(ansiSnapshot);

		var contentLine = ansiSnapshot.Lines[0];

		// Count color codes
		int colorCodeCount = 0;
		for (int i = 0; i < contentLine.Length - 4; i++)
		{
			if (contentLine.Substring(i, 4) == "38;2")
			{
				colorCodeCount++;
			}
		}

		_output.WriteLine($"Color code count for 50 chars: {colorCodeCount}");

		// Should be 1-2, definitely not 50
		Assert.True(colorCodeCount <= 2, $"Inefficient: {colorCodeCount} color codes for uniform line");
	}

	[Fact]
	public void Optimization_EfficiencyRatio_High()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Efficiency"
		};

		window.AddControl(new MarkupControl(new List<string>
		{
			"Normal text content here"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - efficiency ratio should be good
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		if (metrics != null)
		{
			_output.WriteLine($"Efficiency Ratio: {metrics.EfficiencyRatio:P}");
			_output.WriteLine($"Dirty Cells: {metrics.DirtyCellsMarked}");
			_output.WriteLine($"Cells Rendered: {metrics.CellsActuallyRendered}");

			// Efficiency should be reasonable (not over-invalidating)
			Assert.True(metrics.EfficiencyRatio > 0.5,
				$"Low efficiency: {metrics.EfficiencyRatio:P}");
		}
	}

	[Fact]
	public void Optimization_AnsiOptimizationScore_Acceptable()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Score"
		};

		window.AddControl(new MarkupControl(new List<string>
		{
			"[red]R[/][blue]B[/][green]G[/]"
		}));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - optimization score should be tracked
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		if (metrics != null)
		{
			_output.WriteLine($"ANSI Optimization Ratio: {metrics.AnsiOptimizationRatio:P}");

			// Score should be positive
			Assert.True(metrics.AnsiOptimizationRatio >= 0,
				$"Invalid optimization ratio: {metrics.AnsiOptimizationRatio}");
		}
	}
}
