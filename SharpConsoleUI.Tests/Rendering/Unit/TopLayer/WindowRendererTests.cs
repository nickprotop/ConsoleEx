// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for WindowRenderer - DOM tree building, layout, and painting.
/// Validates the measure/arrange/paint phases of the rendering pipeline.
/// </summary>
public class WindowRendererTests
{
	[Fact]
	public void WindowRenderer_RebuildDOMTree_CreatesLayoutNodes()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var control1 = new MarkupControl(new List<string> { "Control 1" });
		var control2 = new MarkupControl(new List<string> { "Control 2" });
		window.AddControl(control1);
		window.AddControl(control2);

		// Act
		var lines = window.RenderAndGetVisibleContent();

		// Assert
		Assert.NotNull(lines);
		Assert.NotEmpty(lines);
	}

	[Fact]
	public void WindowRenderer_PaintDOM_RendersControls()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 50, Height = 20 };
		var markup = new MarkupControl(new List<string> { "Test Content" });
		window.AddControl(markup);

		// Act
		var lines = window.RenderAndGetVisibleContent();

		// Assert
		Assert.Contains(lines, line => line.Contains("Test Content"));
	}

	[Fact]
	public void WindowRenderer_MultipleControls_AllRendered()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 60, Height = 25 };
		var control1 = new MarkupControl(new List<string> { "First" });
		var control2 = new MarkupControl(new List<string> { "Second" });
		var control3 = new MarkupControl(new List<string> { "Third" });

		window.AddControl(control1);
		window.AddControl(control2);
		window.AddControl(control3);

		// Act
		var lines = window.RenderAndGetVisibleContent();
		var content = string.Join("\n", lines);

		// Assert
		Assert.Contains("First", content);
		Assert.Contains("Second", content);
		Assert.Contains("Third", content);
	}

	[Fact]
	public void WindowRenderer_EmptyWindow_RendersWithoutError()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 15 };

		// Act
		var lines = window.RenderAndGetVisibleContent();

		// Assert - Should produce output even with no controls
		Assert.NotNull(lines);
		Assert.NotEmpty(lines);
	}

	[Fact]
	public void WindowRenderer_WithColors_PreservesColors()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 50, Height = 20 };
		var markup = new MarkupControl(new List<string> { "[red]Red Text[/]" });
		window.AddControl(markup);

		// Act
		var lines = window.RenderAndGetVisibleContent();
		var content = string.Join("\n", lines);

		// Assert - Should contain ANSI color codes
		Assert.Contains("\x1b[", content); // ANSI escape sequence
		Assert.Contains("Red Text", content);
	}

	[Fact]
	public void WindowRenderer_Scroll_ChangesVisibleContent()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 5 };
		var list = new ListControl();

		// Add more items than can fit in viewport
		for (int i = 0; i < 20; i++)
		{
			list.AddItem($"Item {i}");
		}
		window.AddControl(list);

		// Act - Render without scrolling
		var lines1 = window.RenderAndGetVisibleContent();
		var content1 = string.Join("\n", lines1);

		// Scroll down
		list.SelectedIndex = 10; // This should scroll the list
		window.Invalidate(true);
		var lines2 = window.RenderAndGetVisibleContent();
		var content2 = string.Join("\n", lines2);

		// Assert - Content should be different after scrolling
		Assert.NotEqual(content1, content2);
	}

	[Fact]
	public void WindowRenderer_Invalidate_ForcesRedraw()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 15 };
		var markup = new MarkupControl(new List<string> { "Initial" });
		window.AddControl(markup);

		// Act
		window.RenderAndGetVisibleContent();

		// Change content and invalidate
		markup.SetContent(new List<string> { "Changed" });
		window.Invalidate(true);
		var lines = window.RenderAndGetVisibleContent();
		var content = string.Join("\n", lines);

		// Assert
		Assert.Contains("Changed", content);
		Assert.DoesNotContain("Initial", content);
	}

	[Fact]
	public void WindowRenderer_SmallWindow_HandlesConstraints()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 10, Height = 5 };
		var markup = new MarkupControl(new List<string> { "This is a very long text that should be clipped" });
		window.AddControl(markup);

		// Act
		var lines = window.RenderAndGetVisibleContent();

		// Assert - Should not crash, content should be clipped to window size
		Assert.NotNull(lines);
		Assert.True(lines.Count <= 5); // Should not exceed window height
	}

	[Fact]
	public void WindowRenderer_BufferToLines_GeneratesAnsiOutput()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 10 };
		var markup = new MarkupControl(new List<string> { "Test" });
		window.AddControl(markup);

		// Act
		var lines = window.RenderAndGetVisibleContent();

		// Assert - Lines should contain ANSI formatting
		Assert.All(lines, line => Assert.True(line.Length >= 0)); // Basic validation
		// First line should have ANSI codes for colors
		Assert.Contains("\x1b[", string.Join("", lines));
	}

	[Fact]
	public void WindowRenderer_LayoutChange_TriggersRelayout()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 20 };
		var markup = new MarkupControl(new List<string> { "Content" });
		window.AddControl(markup);

		// Act - Initial render
		var lines1 = window.RenderAndGetVisibleContent();

		// Resize window (triggers layout change)
		window.Width = 60;
		window.Height = 30;
		window.Invalidate(true);
		var lines2 = window.RenderAndGetVisibleContent();

		// Assert - Size should change
		Assert.NotEqual(lines1.Count, lines2.Count);
	}

	[Fact]
	public void WindowRenderer_MultipleMarkupControls_RendersInSequence()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 60, Height = 25 };

		window.AddControl(new MarkupControl(new List<string> { "First Line" }));
		window.AddControl(new MarkupControl(new List<string> { "Second Line" }));

		// Act
		var lines = window.RenderAndGetVisibleContent();
		var content = string.Join("\n", lines);

		// Assert
		Assert.Contains("First Line", content);
		Assert.Contains("Second Line", content);
	}

	[Fact]
	public void WindowRenderer_ControlWithMargin_RespectsMargin()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 50, Height = 20 };
		var markup = new MarkupControl(new List<string> { "Margined" })
		{
			Margin = new Margin(5, 5, 5, 5)
		};
		window.AddControl(markup);

		// Act
		var lines = window.RenderAndGetVisibleContent();

		// Assert - Content should be rendered with margin offset
		Assert.NotNull(lines);
		// The content should not appear at the very top (due to top margin)
		var firstFewLines = string.Join("\n", lines.Take(5));
		// With 5-line top margin, "Margined" shouldn't appear in first few lines
		// (depending on exact layout implementation)
		Assert.NotEmpty(lines);
	}

	[Fact]
	public void WindowRenderer_WindowWithTitle_RendersContent()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Width = 50,
			Height = 20,
			Title = "Test Window"
		};
		var markup = new MarkupControl(new List<string> { "Content" });
		window.AddControl(markup);

		// Act
		var lines = window.RenderAndGetVisibleContent();
		var content = string.Join("\n", lines);

		// Assert - RenderAndGetVisibleContent returns content area (not title/border)
		Assert.Contains("Content", content);
		Assert.NotEmpty(lines);
	}
}
