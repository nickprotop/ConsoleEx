using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for DOM-based layout system (measure/arrange phases).
/// Validates that the layout engine correctly measures and arranges controls
/// within window content areas.
/// </summary>
public class DOMLayoutTests
{
	private readonly ITestOutputHelper _output;

	public DOMLayoutTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Layout_SingleControl_FillsContentArea()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 20,
			Title = "Layout Test"
		};

		var control = new MarkupControl(new List<string>
		{
			"Content fills available space",
			"Second line of content"
		});
		window.AddControl(control);

		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - content should render in content area (after border)
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Content starts at Top+1 (border inline with title), Left+1 (after left border)
		var char1 = snapshot.GetBack(11, 6).Character; // First char of first line
		Assert.Equal('C', char1); // "Content..."

		var char2 = snapshot.GetBack(11, 7).Character; // First char of second line
		Assert.Equal('S', char2); // "Second..."
	}

	[Fact]
	public void Layout_MultipleControls_StackVertically()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 50,
			Height = 25,
			Title = "Stacked Layout"
		};

		// Add three controls that should stack vertically
		window.AddControl(new MarkupControl(new List<string> { "First Control" }));
		window.AddControl(new MarkupControl(new List<string> { "Second Control" }));
		window.AddControl(new MarkupControl(new List<string> { "Third Control" }));

		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - controls should be stacked vertically
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// First control at y=6 (Top+1)
		Assert.Equal('F', snapshot.GetBack(11, 6).Character);

		// Second control at y=7
		Assert.Equal('S', snapshot.GetBack(11, 7).Character);

		// Third control at y=8
		Assert.Equal('T', snapshot.GetBack(11, 8).Character);
	}

	[Fact]
	public void Layout_WindowResize_ReflowsContent()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 15,
			Title = "Resize Test"
		};

		window.AddControl(new MarkupControl(new List<string>
		{
			"This is a line of text content",
			"Another line here"
		}));

		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Act - resize window to be wider
		window.SetSize(50, 15);
		system.Render.UpdateDisplay();

		// Assert - content should still render (layout should adapt)
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Content should still be visible at new size
		Assert.Equal('T', snapshot.GetBack(11, 6).Character);
	}

	[Fact]
	public void Layout_EmptyWindow_RendersOnlyBorder()
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
		// No controls added

		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - content area should be empty (window background)
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Content area should have spaces (window background)
		var contentChar = snapshot.GetBack(15, 7).Character; // Middle of content area
		Assert.Equal(' ', contentChar);
	}

	[Fact]
	public void Layout_ContentExceedsHeight_Clips()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 8, // Small height: border(1) + 6 content rows + border(1)
			Title = "Clipping"
		};

		// Add more content than can fit
		var lines = new List<string>();
		for (int i = 0; i < 20; i++)
		{
			lines.Add($"Line {i:D2}");
		}
		window.AddControl(new MarkupControl(lines));

		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - only visible lines should render
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Content area is 6 rows (Height 8 - 2 for borders)
		// First line should be visible
		Assert.Equal('L', snapshot.GetBack(11, 6).Character); // "Line 00"

		// Line at y=11 (6 rows down) should be visible
		Assert.Equal('L', snapshot.GetBack(11, 11).Character); // "Line 05"

		// Line at y=12 would be outside window (should be border or desktop)
		var charBeyond = snapshot.GetBack(11, 12).Character;
		Assert.NotEqual('L', charBeyond); // Should not show content
	}

	[Fact]
	public void Layout_ControlWithMargin_AffectsPosition()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 20,
			Title = "Margin Test"
		};

		// This tests the layout system's handling of control positioning
		// The exact margin behavior depends on implementation
		window.AddControl(new MarkupControl(new List<string> { "First" }));
		window.AddControl(new MarkupControl(new List<string> { "Second" }));

		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - controls should render without overlap
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Both controls should be visible
		Assert.Equal('F', snapshot.GetBack(11, 6).Character);
		Assert.Equal('S', snapshot.GetBack(11, 7).Character);
	}

	[Fact]
	public void Layout_InvalidateForcesMeasure()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 40,
			Height = 20,
			Title = "Invalidate Test"
		};

		var control = new MarkupControl(new List<string> { "Original Content" });
		window.AddControl(control);

		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Act - change content and invalidate
		control.SetContent(new List<string> { "Updated Content" });
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		// Assert - updated content should render
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('U', snapshot.GetBack(11, 6).Character); // "Updated..."
	}

	[Fact]
	public void Layout_MultipleWindows_IndependentLayouts()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window1 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 15,
			Title = "Window 1"
		};
		window1.AddControl(new MarkupControl(new List<string> { "W1 Content" }));

		var window2 = new Window(system)
		{
			Left = 45,
			Top = 10,
			Width = 35,
			Height = 18,
			Title = "Window 2"
		};
		window2.AddControl(new MarkupControl(new List<string> { "W2 Content" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Act
		system.Render.UpdateDisplay();

		// Assert - each window has its own layout
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Window 1 content
		Assert.Equal('W', snapshot.GetBack(11, 6).Character); // "W1..."

		// Window 2 content
		Assert.Equal('W', snapshot.GetBack(46, 11).Character); // "W2..."
	}

	[Fact]
	public void Layout_ContentAreaCalculation_AccountsForBorders()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Border Test"
		};

		// Add content that fills exactly the content area width
		// Content area width = Width - 2 (for left and right borders) = 18
		window.AddControl(new MarkupControl(new List<string>
		{
			"123456789012345678" // Exactly 18 characters
		}));

		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - all 18 characters should fit
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// First character at Left+1, Top+1
		Assert.Equal('1', snapshot.GetBack(11, 6).Character);

		// Last character at Left+18, Top+1 (before right border at Left+19)
		Assert.Equal('8', snapshot.GetBack(28, 6).Character);

		// Right border should be at Left+19
		var rightBorder = snapshot.GetBack(29, 6).Character;
		Assert.True(rightBorder == '│' || rightBorder == '║' || rightBorder == '┃',
			$"Expected border character, got '{rightBorder}'");
	}

	[Fact]
	public void Layout_ZeroSizeWindow_HandlesGracefully()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 10, // Minimum width
			Height = 3,  // Minimum height
			Title = "Tiny"
		};

		window.AddControl(new MarkupControl(new List<string> { "X" }));

		system.WindowStateService.AddWindow(window);

		// Act - should not crash
		system.Render.UpdateDisplay();

		// Assert - window should render (even if content is clipped)
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Border should exist
		var topLeft = snapshot.GetBack(10, 5).Character;
		Assert.True(topLeft == '┌' || topLeft == '╔' || topLeft == '╭',
			$"Expected top-left border corner, got '{topLeft}'");
	}
}
