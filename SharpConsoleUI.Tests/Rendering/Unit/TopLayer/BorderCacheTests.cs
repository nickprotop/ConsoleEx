using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for BorderRenderer's CharacterBuffer caching mechanism.
/// Validates that border caches are rebuilt when window properties change
/// (title, width, active state) and reused when nothing changes.
/// </summary>
public class BorderCacheTests
{
	private readonly ITestOutputHelper _output;

	public BorderCacheTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void BorderCache_StaticWindow_CacheIsReused()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Static",
			BorderStyle = BorderStyle.DoubleLine
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Frame 1: cache is built
		system.Render.UpdateDisplay();

		// Capture border content after first render
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		var topLeftChar1 = snapshot1.GetBack(5, 5).Character;
		var topLeftAnsi1 = snapshot1.GetBack(5, 5).AnsiEscape;

		// Frame 2: cache should be reused (no property changes)
		system.Render.UpdateDisplay();

		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		var topLeftChar2 = snapshot2.GetBack(5, 5).Character;
		var topLeftAnsi2 = snapshot2.GetBack(5, 5).AnsiEscape;

		// Assert - same border content
		Assert.Equal(topLeftChar1, topLeftChar2);
		Assert.Equal(topLeftAnsi1, topLeftAnsi2);
	}

	[Fact]
	public void BorderCache_TitleChange_CacheIsRebuilt()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 40,
			Height = 10,
			Title = "Original Title",
			BorderStyle = BorderStyle.DoubleLine
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Frame 1: renders with "Original Title"
		system.Render.UpdateDisplay();

		// Capture title characters from top border
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		bool foundOriginal = FindTextInTopBorder(snapshot1, 5, 44, 5, "Original");

		// Act - change title
		window.Title = "New Title";
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		// Assert - new title should appear
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		bool foundNew = FindTextInTopBorder(snapshot2, 5, 44, 5, "New");

		Assert.True(foundOriginal, "Original title should be present in first render");
		Assert.True(foundNew, "New title should be present after title change");
	}

	[Fact]
	public void BorderCache_ActiveStateChange_CacheIsRebuilt()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Window",
			BorderStyle = BorderStyle.DoubleLine
		};
		var window2 = new Window(system)
		{
			Left = 40,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Other"
		};
		window1.AddControl(new MarkupControl(new List<string> { "Content" }));
		window2.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Window1 is active → double-line border
		system.WindowStateService.SetActiveWindow(window1);
		system.Render.UpdateDisplay();

		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		var cornerWhenActive = snapshot1.GetBack(5, 5).Character;
		var ansiWhenActive = snapshot1.GetBack(5, 5).AnsiEscape;

		// Act - make window2 active (window1 becomes inactive)
		system.WindowStateService.SetActiveWindow(window2);
		system.Render.UpdateDisplay();

		// Assert - border colors should change (cache rebuilt)
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		var ansiWhenInactive = snapshot2.GetBack(5, 5).AnsiEscape;

		_output.WriteLine($"Active ANSI: {ansiWhenActive}");
		_output.WriteLine($"Inactive ANSI: {ansiWhenInactive}");

		// Border color should differ between active and inactive
		Assert.NotEqual(ansiWhenActive, ansiWhenInactive);
	}

	[Fact]
	public void BorderCache_WidthChange_CacheIsRebuilt()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Resizable",
			BorderStyle = BorderStyle.Single,
			IsResizable = false
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Frame 1: width=30, top-right corner at x=34
		system.Render.UpdateDisplay();
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('┐', snapshot1.GetBack(34, 5).Character);

		// Act - resize window wider
		window.Width = 40;
		window.Invalidate(true);
		system.Render.UpdateDisplay();

		// Assert - top-right corner should move to new position
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		Assert.Equal('┐', snapshot2.GetBack(44, 5).Character);

		// Old corner position should no longer be a corner
		Assert.NotEqual('┐', snapshot2.GetBack(34, 5).Character);
	}

	[Fact]
	public void BorderCache_TopBorderContainsButtons()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 50,
			Height = 15,
			Title = "Buttons",
			BorderStyle = BorderStyle.Single
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - extract top border and verify buttons
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var topBorder = ExtractRow(snapshot, 5, 54, 5);
		_output.WriteLine($"Top border: {topBorder}");

		Assert.Contains("[_]", topBorder);
		Assert.Contains("[X]", topBorder);
	}

	[Fact]
	public void BorderCache_BottomBorderWithResizeHandle()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 30,
			Height = 10,
			BorderStyle = BorderStyle.DoubleLine,
			IsResizable = true
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Bottom-left corner
		Assert.Equal('╚', snapshot.GetBack(5, 14).Character);
		// Bottom horizontal
		Assert.Equal('═', snapshot.GetBack(6, 14).Character);
		// Resize handle at bottom-right
		Assert.Equal('◢', snapshot.GetBack(34, 14).Character);
	}

	[Fact]
	public void BorderCache_BottomBorderWithoutResizeHandle()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 30,
			Height = 10,
			BorderStyle = BorderStyle.DoubleLine,
			IsResizable = false
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Normal corner (not resize handle)
		Assert.Equal('╝', snapshot.GetBack(34, 14).Character);
	}

	[Fact]
	public void BorderCache_VerticalBordersUseSetCell()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 20,
			Height = 10,
			BorderStyle = BorderStyle.DoubleLine,
			IsResizable = false
		};
		window.AddControl(new MarkupControl(new List<string> { "Test" }));
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - vertical borders rendered via SetCell
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		for (int y = 6; y < 14; y++)
		{
			Assert.Equal('║', snapshot.GetBack(5, y).Character);
			Assert.Equal('║', snapshot.GetBack(24, y).Character);
		}
	}

	[Fact]
	public void BorderCache_ClippedBorder_OnlyVisiblePortionRendered()
	{
		// Arrange - window partially off-screen (left edge clipped)
		var system = TestWindowSystemBuilder.CreateTestSystem(); // 200x50
		var window = new Window(system)
		{
			Left = 0,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Clipped",
			BorderStyle = BorderStyle.Single,
			IsResizable = false
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert - top-left corner should still be at x=0
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		Assert.Equal('┌', snapshot.GetBack(0, 5).Character);
		Assert.Equal('┐', snapshot.GetBack(29, 5).Character);
	}

	#region Helper Methods

	private static bool FindTextInTopBorder(
		Diagnostics.Snapshots.ConsoleBufferSnapshot snapshot,
		int startX, int endX, int y, string text)
	{
		var row = ExtractRow(snapshot, startX, endX, y);
		return row.Contains(text);
	}

	private static string ExtractRow(
		Diagnostics.Snapshots.ConsoleBufferSnapshot snapshot,
		int startX, int endX, int y)
	{
		var chars = new List<char>();
		for (int x = startX; x <= endX && x < snapshot.Width; x++)
		{
			chars.Add(snapshot.GetBack(x, y).Character);
		}
		return new string(chars.ToArray());
	}

	#endregion
}
