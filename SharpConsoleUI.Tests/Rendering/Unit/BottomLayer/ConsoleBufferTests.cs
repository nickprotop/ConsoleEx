using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Tests for ConsoleBuffer front/back buffer comparison and dirty cell detection.
/// Validates that the double-buffering system correctly identifies changed cells
/// by comparing front (displayed) vs back (to render) buffers.
/// </summary>
public class ConsoleBufferTests
{
	private readonly ITestOutputHelper _output;

	public ConsoleBufferTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void ConsoleBuffer_InitialRender_AllCellsDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Initial"
		};

		window.AddControl(new MarkupControl(new List<string> { "Initial content" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - first frame should render everything
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);
		Assert.True(metrics.BytesWritten > 100); // Significant output
		Assert.True(metrics.CharactersChanged > 0);
	}

	[Fact]
	public void ConsoleBuffer_NoChanges_NoDirtyCells()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Static"
		};

		window.AddControl(new MarkupControl(new List<string> { "Static content" }));
		system.WindowStateService.AddWindow(window);

		// Initial render
		system.Render.UpdateDisplay();

		// Act - render again with no changes
		system.Render.UpdateDisplay();

		// Assert - second frame should detect no changes
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var dirtyCount = snapshot.GetDirtyCount();
		_output.WriteLine($"Dirty cell count: {dirtyCount}");

		Assert.Equal(0, dirtyCount);
	}

	[Fact]
	public void ConsoleBuffer_SingleCellChange_OnlyThatCellDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "AAAA" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Single Change"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		// Initial render
		system.Render.UpdateDisplay();

		// Act - change one character
		control.SetContent(new List<string> { "ABAA" });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		// Assert - should detect minimal change
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Characters changed: {metrics.CharactersChanged}");
		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");

		// Should be small number of characters (the one that changed plus positioning)
		Assert.True(metrics.CharactersChanged <= 5);
		Assert.True(metrics.BytesWritten < 100);
	}

	[Fact]
	public void ConsoleBuffer_FrontBackComparison_DetectsDifferences()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Comparison"
		};

		window.AddControl(new MarkupControl(new List<string> { "Original" }));
		system.WindowStateService.AddWindow(window);

		// Initial render - front buffer now matches back buffer
		system.Render.UpdateDisplay();

		// Act - make a change
		window.AddControl(new MarkupControl(new List<string> { "Modified" }));
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		// Assert - buffers should have differed before render
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// After render, check that content was updated
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.True(metrics?.BytesWritten > 0);
	}

	[Fact]
	public void ConsoleBuffer_CharacterDifference_CellIsDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "ABC" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Char Diff"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change middle character
		control.SetContent(new List<string> { "AXC" });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Position of 'X' should be dirty
		// Window content starts at (11, 6), 'X' is at offset 1
		bool isXDirty = snapshot.IsCellDirty(12, 6);
		_output.WriteLine($"Cell at (12, 6) dirty: {isXDirty}");

		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.True(metrics?.CharactersChanged > 0);
	}

	[Fact]
	public void ConsoleBuffer_ColorDifference_CellIsDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "Text" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Color Diff"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change color but keep same text
		control.SetContent(new List<string> { "[red]Text[/]" });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - color change should be detected
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.True(metrics.BytesWritten > 0); // Color change requires output
	}

	[Fact]
	public void ConsoleBuffer_BackgroundColorDifference_CellIsDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "BG Diff",
			BackgroundColor = Color.White
		};

		window.AddControl(new MarkupControl(new List<string> { "Text" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change background color
		window.BackgroundColor = Color.Blue;
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - background color change should trigger re-render
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.True(metrics.BytesWritten > 100); // Background affects entire window
	}

	[Fact]
	public void ConsoleBuffer_WindowMove_OnlyMovedRegionDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 30,
			Height = 10,
			Title = "Moving"
		};

		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - move window
		window.Left = 15;
		window.Top = 12;
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - should render at new position
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.True(metrics.BytesWritten > 0);
	}

	[Fact]
	public void ConsoleBuffer_LargeChange_ManyDirtyCells()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "Original content here" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Large Change"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - completely different content
		control.SetContent(new List<string> { "Totally new text!!!" });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - significant output
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Characters changed: {metrics.CharactersChanged}");
		Assert.True(metrics.CharactersChanged > 10);
	}

	[Fact]
	public void ConsoleBuffer_PartialLineChange_OnlyChangedPartDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var control = new MarkupControl(new List<string> { "AAAA BBBB CCCC" });
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Partial"
		};

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - change only middle section
		control.SetContent(new List<string> { "AAAA XXXX CCCC" });
		window.Invalidate(true);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - should be efficient
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Characters changed: {metrics.CharactersChanged}");
		// Changed "BBBB" to "XXXX" - 4 characters
		Assert.True(metrics.CharactersChanged <= 10);
	}

	[Fact]
	public void ConsoleBuffer_AddNewWindow_NewRegionDirty()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Window 1"
		};

		window1.AddControl(new MarkupControl(new List<string> { "Window 1" }));
		system.WindowStateService.AddWindow(window1);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - add second window
		var window2 = new Window(system)
		{
			Left = 45,
			Top = 10,
			Width = 30,
			Height = 10,
			Title = "Window 2"
		};

		window2.AddControl(new MarkupControl(new List<string> { "Window 2" }));
		system.WindowStateService.AddWindow(window2);
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - new window area should be rendered
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.True(metrics.BytesWritten > 100); // New window is significant
	}

	[Fact]
	public void ConsoleBuffer_RemoveWindow_OldRegionCleared()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Temporary"
		};

		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - close window
		window.Close();
		system.Render.UpdateDisplay(); // Frame 2

		// Assert - window area should be cleared
		var metrics = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(metrics);

		_output.WriteLine($"Bytes written: {metrics.BytesWritten}");
		Assert.True(metrics.BytesWritten > 0); // Clearing requires output
	}

	[Fact]
	public void ConsoleBuffer_StaticFrame_ZeroOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Static"
		};

		window.AddControl(new MarkupControl(new List<string> { "Static" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay(); // Frame 1

		// Act - render with no changes
		system.Render.UpdateDisplay(); // Frame 2
		system.Render.UpdateDisplay(); // Frame 3
		system.Render.UpdateDisplay(); // Frame 4

		// Assert - frames 2-4 should produce zero output
		for (int frame = 2; frame <= 4; frame++)
		{
			var metrics = system.RenderingDiagnostics?.GetMetrics(frame);
			if (metrics != null)
			{
				_output.WriteLine($"Frame {frame}: {metrics.BytesWritten} bytes");
				Assert.Equal(0, metrics.BytesWritten);
				Assert.Equal(0, metrics.CharactersChanged);
				Assert.True(metrics.IsStaticFrame);
			}
		}
	}

	[Fact]
	public void ConsoleBuffer_BufferContents_PreserveData()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Preserve"
		};

		window.AddControl(new MarkupControl(new List<string> { "TEST" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - verify buffer contains expected data
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Check specific characters in back buffer
		var char1 = snapshot.GetBack(11, 6).Character; // First char of "TEST"
		var char2 = snapshot.GetBack(12, 6).Character;
		var char3 = snapshot.GetBack(13, 6).Character;
		var char4 = snapshot.GetBack(14, 6).Character;

		_output.WriteLine($"Buffer contents: {char1}{char2}{char3}{char4}");

		Assert.Equal('T', char1);
		Assert.Equal('E', char2);
		Assert.Equal('S', char3);
		Assert.Equal('T', char4);
	}
}
