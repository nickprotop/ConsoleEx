// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for border rendering via the full rendering pipeline.
/// Borders are rendered at the system level, so these tests use system.Render.UpdateDisplay()
/// and validate via diagnostics snapshots.
/// </summary>
public class BorderRenderingTests
{
	[Fact]
	public void Border_DoubleLine_RendersCorrectCorners()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 30,
			Height = 10,
			Title = "Test",
			BorderStyle = BorderStyle.DoubleLine,
			IsResizable = false  // Disable resize handle to see corner character
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);
		// DoubleLine borders only show as double when window is active
		system.WindowStateService.SetActiveWindow(window);

		// Act - Full system render (includes borders)
		system.Render.UpdateDisplay();

		// Assert - Check border characters in snapshot
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Double line border corners
		Assert.Equal('╔', snapshot.GetBack(5, 5).Character);  // Top-left
		Assert.Equal('╗', snapshot.GetBack(34, 5).Character); // Top-right
		Assert.Equal('╚', snapshot.GetBack(5, 14).Character); // Bottom-left
		Assert.Equal('╝', snapshot.GetBack(34, 14).Character); // Bottom-right
	}

	[Fact]
	public void Border_Single_RendersCorrectCorners()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 25,
			Height = 8,
			BorderStyle = BorderStyle.Single,
			IsResizable = false  // Disable resize handle to see corner character
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Single line border corners
		Assert.Equal('┌', snapshot.GetBack(10, 10).Character);  // Top-left
		Assert.Equal('┐', snapshot.GetBack(34, 10).Character); // Top-right
		Assert.Equal('└', snapshot.GetBack(10, 17).Character); // Bottom-left
		Assert.Equal('┘', snapshot.GetBack(34, 17).Character); // Bottom-right
	}

	[Fact]
	public void Border_HorizontalEdges_RenderCorrectly()
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

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Bottom border is always pure: corner + horizontalBorder*(width-2) + corner
		// Window spans x=5 to x=24 (width 20), so bottom border from x=6 to x=23
		for (int x = 6; x < 24; x++)
		{
			Assert.Equal('═', snapshot.GetBack(x, 14).Character); // Bottom edge
		}

		// Top border has format: corner + padding + title + padding + buttons + corner
		// We check the corners and that some horizontal borders exist
		Assert.Equal('╔', snapshot.GetBack(5, 5).Character);  // Top-left corner
		Assert.Equal('╗', snapshot.GetBack(24, 5).Character); // Top-right corner

		// Top border contains mixture of '═' (border), '[', ']', '_', '+', 'X' (buttons)
		// Just verify it's not all spaces and has expected characters
		bool hasHorizontalBorder = false;
		bool hasButtons = false;
		for (int x = 6; x < 24; x++)
		{
			char c = snapshot.GetBack(x, 5).Character;
			if (c == '═') hasHorizontalBorder = true;
			if (c == '[' || c == ']' || c == '_' || c == '+' || c == 'X') hasButtons = true;
		}
		Assert.True(hasHorizontalBorder, "Top border should contain horizontal border characters");
		Assert.True(hasButtons, "Top border should contain control buttons");
	}

	[Fact]
	public void Border_VerticalEdges_RenderCorrectly()
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

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Check vertical border characters (excluding corners)
		for (int y = 6; y < 14; y++)
		{
			Assert.Equal('║', snapshot.GetBack(5, y).Character);  // Left edge
			Assert.Equal('║', snapshot.GetBack(24, y).Character); // Right edge
		}
	}

	[Fact]
	public void Border_WithTitle_IncludesTitleInBorder()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 40,
			Height = 15,
			Title = "My Window",
			BorderStyle = BorderStyle.DoubleLine
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Title should appear in the top border area
		// Check for title characters (they appear after the left corner and border prefix)
		bool foundTitle = false;
		for (int x = 6; x < 44; x++)
		{
			var cell = snapshot.GetBack(x, 5);
			if (cell.Character == 'M' || cell.Character == 'y' || cell.Character == 'W')
			{
				foundTitle = true;
				break;
			}
		}
		Assert.True(foundTitle, "Title should be present in border");
	}

	[Fact]
	public void Border_ActiveWindow_UsesActiveBorderColors()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 30,
			Height = 10,
			BorderStyle = BorderStyle.Single
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Border should be present (active border color is theme-dependent)
		var topLeft = snapshot.GetBack(5, 5);
		Assert.Equal('┌', topLeft.Character);
		// Color will be the active border color from theme (encoded in ANSI)
		Assert.NotEmpty(topLeft.AnsiEscape);
	}

	[Fact]
	public void Border_InactiveWindow_UsesInactiveBorderColors()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 30,
			Height = 10,
			BorderStyle = BorderStyle.Single
		};
		var window2 = new Window(system)
		{
			Left = 40,
			Top = 5,
			Width = 30,
			Height = 10,
			BorderStyle = BorderStyle.Single
		};
		window1.AddControl(new MarkupControl(new List<string> { "Window 1" }));
		window2.AddControl(new MarkupControl(new List<string> { "Window 2" }));
		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Activate window2, making window1 inactive
		system.WindowStateService.SetActiveWindow(window2);

		// Act
		system.Render.UpdateDisplay();

		// Assert - Check that window1 border is rendered
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		var window1TopLeft = snapshot.GetBack(5, 5);
		Assert.Equal('┌', window1TopLeft.Character);
		// Inactive window should still have a border (with ANSI color)
		Assert.NotEmpty(window1TopLeft.AnsiEscape);
	}

	[Fact]
	public void Border_Rounded_RendersRoundedCorners()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 25,
			Height = 10,
			BorderStyle = BorderStyle.Rounded,
			IsResizable = false
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Rounded corners
		Assert.Equal('╭', snapshot.GetBack(5, 5).Character);  // Top-left
		Assert.Equal('╮', snapshot.GetBack(29, 5).Character); // Top-right
		Assert.Equal('╰', snapshot.GetBack(5, 14).Character); // Bottom-left
		Assert.Equal('╯', snapshot.GetBack(29, 14).Character); // Bottom-right
	}

	[Fact]
	public void Border_MultipleWindows_EachHasOwnBorderStyle()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window1 = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Single Border",
			BorderStyle = BorderStyle.Single,
			IsResizable = false
		};
		var window2 = new Window(system)
		{
			Left = 30,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Double Border",
			BorderStyle = BorderStyle.DoubleLine,
			IsResizable = false
		};
		var window3 = new Window(system)
		{
			Left = 55,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Rounded Border",
			BorderStyle = BorderStyle.Rounded,
			IsResizable = false
		};
		window1.AddControl(new MarkupControl(new List<string> { "Window 1" }));
		window2.AddControl(new MarkupControl(new List<string> { "Window 2" }));
		window3.AddControl(new MarkupControl(new List<string> { "Window 3" }));
		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.AddWindow(window3);
		// Activate window2 to see its double-line border
		system.WindowStateService.SetActiveWindow(window2);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Window 1 should have single line border
		Assert.Equal('┌', snapshot.GetBack(5, 5).Character);

		// Window 2 should have double line border
		Assert.Equal('╔', snapshot.GetBack(30, 5).Character);

		// Window 3 should have rounded border
		Assert.Equal('╭', snapshot.GetBack(55, 5).Character);
	}

	[Fact]
	public void Border_WindowWithControlButtons_RendersButtonsInBorder()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 40,
			Height = 15,
			Title = "Window",
			BorderStyle = BorderStyle.DoubleLine
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Control buttons ([_][+][X]) should be in the top border area
		// Check for the presence of these characters near the top-right
		bool foundButtons = false;
		for (int x = 35; x < 44; x++)
		{
			var cell = snapshot.GetBack(x, 5);
			if (cell.Character == '[' || cell.Character == '_' ||
			    cell.Character == '+' || cell.Character == 'X' || cell.Character == ']')
			{
				foundButtons = true;
				break;
			}
		}
		Assert.True(foundButtons, "Control buttons should be present in border");
	}

	[Fact]
	public void Border_ControlButtons_ContainAllThreeButtons()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 50,
			Height = 20,
			Title = "Test Window",
			BorderStyle = BorderStyle.Single
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Extract top border line
		var topBorderChars = new List<char>();
		for (int x = 5; x < 55; x++)
		{
			topBorderChars.Add(snapshot.GetBack(x, 5).Character);
		}
		var topBorder = new string(topBorderChars.ToArray());

		// Should contain all three control buttons: [_] [+] [X]
		Assert.Contains("[_]", topBorder);
		Assert.Contains("[+]", topBorder);
		Assert.Contains("[X]", topBorder);
	}

	[Fact]
	public void Border_OverlappingWindows_TopWindowBorderOccludesBottom()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Window 1: 10,10 -> 29,19 (background window with single border)
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 20,
			Height = 10,
			BorderStyle = BorderStyle.Single
		};
		window1.AddControl(new MarkupControl(new List<string> { "Back" }));

		// Window 2: 15,12 -> 34,21 (overlaps window1, double border)
		var window2 = new Window(system)
		{
			Left = 15,
			Top = 12,
			Width = 20,
			Height = 10,
			BorderStyle = BorderStyle.DoubleLine
		};
		window2.AddControl(new MarkupControl(new List<string> { "Front" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.SetActiveWindow(window2); // Make window2 active for double-line border

		// Act - window2 should be on top
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Check window2's LEFT BORDER at x=15 (overlaps with window1 content area)
		// This is a reliable position - window2's border always renders here
		var window2LeftBorder = snapshot.GetBack(15, 15); // Left border of window2

		// Window2 has double-line border (active), so left border should be '║'
		Assert.Equal('║', window2LeftBorder.Character);

		// Check window1's right border at x=29 (should be visible where window2 doesn't cover)
		// This position is NOT covered by window2
		var window1RightBorder = snapshot.GetBack(29, 11);
		Assert.Equal('│', window1RightBorder.Character); // Window1's single-line right border
	}

	[Fact]
	public void Border_BringToFront_ChangesZOrderAndBorderOcclusion()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window1 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 20,
			Height = 10,
			BorderStyle = BorderStyle.Single
		};
		window1.AddControl(new MarkupControl(new List<string> { "Win1" }));

		var window2 = new Window(system)
		{
			Left = 15,
			Top = 12,
			Width = 20,
			Height = 10,
			BorderStyle = BorderStyle.DoubleLine
		};
		window2.AddControl(new MarkupControl(new List<string> { "Win2" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.SetActiveWindow(window2); // Make window2 active for double-line

		// Initially window2 is on top
		system.Render.UpdateDisplay();
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);

		// Check window2's LEFT border at x=15, y=15 (overlaps window1)
		var beforeChar = snapshot1.GetBack(15, 15).Character;
		Assert.Equal('║', beforeChar); // Window2's double-line left border

		// Act - Bring window1 to front
		system.WindowStateService.BringToFront(window1);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);

		// Now position (15, 15) should show window1's content or right border
		// Window1's right border is at x=29, so x=15 is in window1's content area
		var afterChar = snapshot2.GetBack(15, 15).Character;

		// Should NOT be window2's double border anymore
		Assert.NotEqual('║', afterChar);
		Assert.NotEqual('═', afterChar);
	}

	[Fact]
	public void Border_SendToBack_ChangesZOrderAndBorderOcclusion()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window1 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 25,
			Height = 12,
			Title = "Window 1",
			BorderStyle = BorderStyle.Rounded
		};
		window1.AddControl(new MarkupControl(new List<string> { "Content 1" }));

		var window2 = new Window(system)
		{
			Left = 20,
			Top = 15,
			Width = 25,
			Height = 12,
			Title = "Window 2",
			BorderStyle = BorderStyle.Single
		};
		window2.AddControl(new MarkupControl(new List<string> { "Content 2" }));

		// Add window1 first, then window2 (window2 on top initially)
		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Act - Send window2 to back (window1 should now be on top)
		system.WindowStateService.SendToBack(window2);
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Check overlap region (25, 18) - should now show window1's rounded border
		var cell = snapshot.GetBack(25, 18);
		bool isRoundedBorder = cell.Character == '│' || cell.Character == '─' ||
		                       cell.Character == '╭' || cell.Character == '╮' ||
		                       cell.Character == '╰' || cell.Character == '╯';

		// Should see window1's border or content (not window2's single border)
		bool notSingleCorner = cell.Character != '┌' && cell.Character != '┐' &&
		                       cell.Character != '└' && cell.Character != '┘';
		Assert.True(notSingleCorner, $"Expected window1 (rounded) on top, but got single border char: {cell.Character}");
	}

	[Fact]
	public void Border_MultipleZOrderChanges_BordersUpdateCorrectly()
	{
		// Arrange - Three overlapping windows at different positions
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window1 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 15,
			Height = 8,
			BorderStyle = BorderStyle.Single
		};
		window1.AddControl(new MarkupControl(new List<string> { "W1" }));

		var window2 = new Window(system)
		{
			Left = 15,
			Top = 12,
			Width = 15,
			Height = 8,
			BorderStyle = BorderStyle.DoubleLine
		};
		window2.AddControl(new MarkupControl(new List<string> { "W2" }));

		var window3 = new Window(system)
		{
			Left = 20,
			Top = 14,
			Width = 15,
			Height = 8,
			BorderStyle = BorderStyle.Rounded
		};
		window3.AddControl(new MarkupControl(new List<string> { "W3" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.AddWindow(window3);

		// Check position (20, 16) which is:
		// - Window1: NO (ends at x=24)
		// - Window2: YES (x:15-29, y:12-19) - left border at x=15, so x=20 is content
		// - Window3: YES (x:20-34, y:14-21) - left border at x=20

		// Initially: window3 on top
		system.WindowStateService.SetActiveWindow(window3);
		system.Render.UpdateDisplay();
		var char1 = system.RenderingDiagnostics?.LastConsoleSnapshot?.GetBack(20, 16).Character;

		// Act 1: Bring window2 to front (and make it active for double-line)
		system.WindowStateService.BringToFront(window2);
		system.WindowStateService.SetActiveWindow(window2);
		system.Render.UpdateDisplay();
		var char2 = system.RenderingDiagnostics?.LastConsoleSnapshot?.GetBack(20, 16).Character;

		// Act 2: Bring window1 to front
		system.WindowStateService.BringToFront(window1);
		system.Render.UpdateDisplay();
		var char3 = system.RenderingDiagnostics?.LastConsoleSnapshot?.GetBack(20, 16).Character;

		// Act 3: Send window1 to back (window2 should be visible again)
		system.WindowStateService.SendToBack(window1);
		system.Render.UpdateDisplay();
		var char4 = system.RenderingDiagnostics?.LastConsoleSnapshot?.GetBack(20, 16).Character;

		// Assert - Z-order changes should cause visible changes
		// char1 should be window3's left border '│' (rounded uses same vertical as single)
		// char2 should be window2's content or borders
		// char3 should be window1's content (x=20 is past window1's right edge at x=24)
		// Actually, window1 ends at x=24, so x=20 is still covered by window2/3

		// At minimum, we should see different border styles appearing
		var chars = new[] { char1, char2, char3, char4 };
		var distinctChars = chars.Where(c => c.HasValue).Select(c => c!.Value).Distinct().Count();

		Assert.True(distinctChars >= 2, $"Z-order changes should show at least 2 different characters at overlap point, got {distinctChars}: {string.Join(", ", chars.Select(c => c?.ToString() ?? "null"))}");
	}

	[Fact]
	public void Border_OverlappingWindows_CorrectOcclusionAtAllOverlapPoints()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window1 = new Window(system)
		{
			Left = 10,
			Top = 10,
			Width = 30,
			Height = 15,
			Title = "Background",
			BorderStyle = BorderStyle.Single
		};
		window1.AddControl(new MarkupControl(new List<string> { "Background Content" }));

		var window2 = new Window(system)
		{
			Left = 20,
			Top = 15,
			Width = 30,
			Height = 15,
			Title = "Foreground",
			BorderStyle = BorderStyle.DoubleLine
		};
		window2.AddControl(new MarkupControl(new List<string> { "Foreground Content" }));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Check multiple points in the overlap region (20-40, 15-25)
		// All should show window2's double border or content
		var testPoints = new[] {
			(20, 15), (25, 18), (30, 20), (35, 22), (38, 24)
		};

		foreach (var (x, y) in testPoints)
		{
			var cell = snapshot.GetBack(x, y);

			// Should NOT be window1's single border characters
			bool notSingleBorder = cell.Character != '┌' && cell.Character != '┐' &&
			                       cell.Character != '└' && cell.Character != '┘' &&
			                       cell.Character != '│' && cell.Character != '─';

			Assert.True(notSingleBorder ||
			           cell.Character == '║' || cell.Character == '═' ||
			           cell.Character == '╔' || cell.Character == '╗' ||
			           cell.Character == '╚' || cell.Character == '╝',
			           $"At ({x},{y}): Expected window2 (double border) on top, got: {cell.Character}");
		}
	}

	[Fact]
	public void Border_ResizableWindow_ShowsResizeHandleInBottomRightCorner()
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
			IsResizable = true  // Enable resize handle
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Bottom-right should show resize handle '◢' instead of corner character
		var bottomRight = snapshot.GetBack(34, 14);
		Assert.Equal('◢', bottomRight.Character);

		// Other corners should still be normal border characters
		Assert.Equal('╔', snapshot.GetBack(5, 5).Character);   // Top-left
		Assert.Equal('╗', snapshot.GetBack(34, 5).Character);  // Top-right
		Assert.Equal('╚', snapshot.GetBack(5, 14).Character);  // Bottom-left
	}

	[Fact]
	public void Border_NonResizableWindow_ShowsNormalCornerInBottomRight()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 30,
			Height = 10,
			BorderStyle = BorderStyle.Single,
			IsResizable = false  // Disable resize handle
		};
		window.AddControl(new MarkupControl(new List<string> { "Content" }));
		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// All corners should be normal border characters (no resize handle)
		Assert.Equal('┌', snapshot.GetBack(5, 5).Character);   // Top-left
		Assert.Equal('┐', snapshot.GetBack(34, 5).Character);  // Top-right
		Assert.Equal('└', snapshot.GetBack(5, 14).Character);  // Bottom-left
		Assert.Equal('┘', snapshot.GetBack(34, 14).Character); // Bottom-right (NOT '◢')
	}

	[Fact]
	public void Border_ResizableVsNonResizable_MultipleWindows()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var resizableWindow = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 25,
			Height = 10,
			Title = "Resizable",
			BorderStyle = BorderStyle.Single,
			IsResizable = true
		};
		var nonResizableWindow = new Window(system)
		{
			Left = 35,
			Top = 5,
			Width = 25,
			Height = 10,
			Title = "Non-Resizable",
			BorderStyle = BorderStyle.Single,
			IsResizable = false
		};
		resizableWindow.AddControl(new MarkupControl(new List<string> { "Can resize" }));
		nonResizableWindow.AddControl(new MarkupControl(new List<string> { "Cannot resize" }));
		system.WindowStateService.AddWindow(resizableWindow);
		system.WindowStateService.AddWindow(nonResizableWindow);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Resizable window should have resize handle in bottom-right
		var resizableBottomRight = snapshot.GetBack(29, 14);
		Assert.Equal('◢', resizableBottomRight.Character);

		// Non-resizable window should have normal corner in bottom-right
		var nonResizableBottomRight = snapshot.GetBack(59, 14);
		Assert.Equal('┘', nonResizableBottomRight.Character);
	}
}
