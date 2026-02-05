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
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Comprehensive tests for window content rendering, z-order management, and window movement.
/// Validates that window content renders correctly, overlapping windows occlude properly,
/// and CRITICAL: exposed areas are redrawn when windows move.
/// </summary>
public class ContentRenderingTests
{
	[Fact]
	public void Content_SingleWindow_RendersCorrectly()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 8,
			Title = "Test"
		};

		window.AddControl(new MarkupControl(new List<string>
		{
			"XXXXXX",
			"XXXXXX",
			"XXXXXX"
		}));

		system.WindowStateService.AddWindow(window);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Content starts at Left+1, Top+1 (inside border)
		var contentCell = snapshot.GetBack(11, 6);
		Assert.Equal('X', contentCell.Character);
	}

	[Fact]
	public void Content_OverlappingWindows_TopWindowContentVisible()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Bottom window with 'O' content
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 25,
			Height = 12,
			Title = "Window 1"
		};
		window1.AddControl(new MarkupControl(new List<string>
		{
			"OOOOOOOOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOOOOOOOO"
		}));

		// Top window with 'X' content, overlaps window1
		var window2 = new Window(system)
		{
			Left = 20,
			Top = 10,
			Width = 25,
			Height = 12,
			Title = "Window 2"
		};
		window2.AddControl(new MarkupControl(new List<string>
		{
			"XXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXX"
		}));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);

		// Act
		system.Render.UpdateDisplay();

		// Assert
		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// In overlap region, window2 (top) should be visible
		// Overlap: x: 21-33, y: 11-15
		var overlapCell = snapshot.GetBack(25, 13);
		Assert.Equal('X', overlapCell.Character);

		// Outside overlap, window1 should be visible
		var window1Cell = snapshot.GetBack(15, 8);
		Assert.Equal('O', window1Cell.Character);
	}

	[Fact]
	public void Content_BringToFront_ContentBecomesVisible()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Window 1 with 'O' content (will be on top initially)
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Window 1"
		};
		window1.AddControl(new MarkupControl(new List<string>
		{
			"OOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOO"
		}));

		// Window 2 with 'X' content, same position (will be on top)
		var window2 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Window 2"
		};
		window2.AddControl(new MarkupControl(new List<string>
		{
			"XXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXX"
		}));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.Render.UpdateDisplay();

		// Window 2 on top initially
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('X', snapshot1.GetBack(15, 8).Character);

		// Act - Bring window1 to front
		system.WindowStateService.BringToFront(window1);
		system.Render.UpdateDisplay();

		// Assert - Window1 content now visible
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		Assert.Equal('O', snapshot2.GetBack(15, 8).Character);
	}

	[Fact]
	public void Content_SendToBack_ContentBecomesHidden()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Window 1 with 'O' content
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Window 1"
		};
		window1.AddControl(new MarkupControl(new List<string>
		{
			"OOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOO",
			"OOOOOOOOOOOOOOOO"
		}));

		// Window 2 with 'X' content
		var window2 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 20,
			Height = 10,
			Title = "Window 2"
		};
		window2.AddControl(new MarkupControl(new List<string>
		{
			"XXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXX"
		}));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.Render.UpdateDisplay();

		// Window 2 on top initially
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('X', snapshot1.GetBack(15, 8).Character);

		// Act - Send window2 to back
		system.WindowStateService.SendToBack(window2);
		system.Render.UpdateDisplay();

		// Assert - Window1 content now visible (window2 hidden behind)
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		Assert.Equal('O', snapshot2.GetBack(15, 8).Character);
	}

	[Fact]
	public void Content_WindowMoveLeftToRight_ExposesLeftSide()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with 'B' characters - fills screen
		var background = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 50,
			Height = 20,
			Title = "Background"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
		}));

		// Moving window with 'M' characters
		var moving = new Window(system)
		{
			Left = 15,
			Top = 10,
			Width = 20,
			Height = 8,
			Title = "Moving"
		};
		moving.AddControl(new MarkupControl(new List<string>
		{
			"MMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMM",
			"MMMMMMMMMMMMMMMM"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);
		system.Render.UpdateDisplay();

		// Verify moving window covers position (20, 13)
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		var beforeMove = snapshot1.GetBack(20, 13).Character;
		Assert.Equal('M', beforeMove);

		// Act - Move window to the right, exposing left area
		moving.Left = 30;
		moving.Invalidate(true);
		system.Render.UpdateDisplay();

		// Assert - CRITICAL: Exposed area shows background
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		var afterMove = snapshot2.GetBack(20, 13).Character;
		Assert.Equal('B', afterMove); // Background now visible
	}

	[Fact]
	public void Content_WindowMoveRightToLeft_ExposesRightSide()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with '1' characters
		var background = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 50,
			Height = 20,
			Title = "Background"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"11111111111111111111111111111111111111111111111",
			"11111111111111111111111111111111111111111111111",
			"11111111111111111111111111111111111111111111111",
			"11111111111111111111111111111111111111111111111",
			"11111111111111111111111111111111111111111111111",
			"11111111111111111111111111111111111111111111111"
		}));

		// Moving window with '2' characters
		var moving = new Window(system)
		{
			Left = 30,
			Top = 10,
			Width = 20,
			Height = 8,
			Title = "Moving"
		};
		moving.AddControl(new MarkupControl(new List<string>
		{
			"22222222222222222",
			"22222222222222222",
			"22222222222222222",
			"22222222222222222"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);
		system.Render.UpdateDisplay();

		// Verify moving window covers position (40, 11) - within both windows' content areas
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('2', snapshot1.GetBack(40, 11).Character);

		// Act - Move window to the left, exposing right area
		moving.Left = 15;
		system.Render.UpdateDisplay();

		// Assert - CRITICAL: Exposed area shows background
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		Assert.Equal('1', snapshot2.GetBack(40, 11).Character);
	}

	[Fact]
	public void Content_WindowMoveUpward_ExposesBottomArea()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with 'A' characters
		var background = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 50,
			Height = 25,
			Title = "Background"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
		}));

		// Moving window with 'Z' characters (position to overlap with background content)
		var moving = new Window(system)
		{
			Left = 20,
			Top = 10,
			Width = 25,
			Height = 10,
			Title = "Moving"
		};
		moving.AddControl(new MarkupControl(new List<string>
		{
			"ZZZZZZZZZZZZZZZZZZZZZ",
			"ZZZZZZZZZZZZZZZZZZZZZ",
			"ZZZZZZZZZZZZZZZZZZZZZ",
			"ZZZZZZZZZZZZZZZZZZZZZ",
			"ZZZZZZZZZZZZZZZZZZZZZ"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);
		system.Render.UpdateDisplay();

		// Verify moving window covers position (30, 15) - last row of background content
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('Z', snapshot1.GetBack(30, 15).Character);

		// Act - Move window upward to Top=6, exposing bottom area at y=16-19
		moving.Top = 6;
		system.Render.UpdateDisplay();

		// Assert - Check exposed area at left side (x=10 is left of moving window which starts at x=20)
		// Background content at y=10 should be visible to the left of moving window
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		Assert.Equal('A', snapshot2.GetBack(10, 10).Character);
	}

	[Fact]
	public void Content_WindowMoveDownward_ExposesTopArea()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with 'C' characters
		var background = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 50,
			Height = 25,
			Title = "Background"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
			"CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
			"CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
			"CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
			"CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
			"CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
			"CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
			"CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"
		}));

		// Moving window with 'Y' characters
		var moving = new Window(system)
		{
			Left = 20,
			Top = 8,
			Width = 25,
			Height = 10,
			Title = "Moving"
		};
		moving.AddControl(new MarkupControl(new List<string>
		{
			"YYYYYYYYYYYYYYYYYYYYY",
			"YYYYYYYYYYYYYYYYYYYYY",
			"YYYYYYYYYYYYYYYYYYYYY",
			"YYYYYYYYYYYYYYYYYYYYY"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);
		system.Render.UpdateDisplay();

		// Verify moving window covers position (30, 11)
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('Y', snapshot1.GetBack(30, 11).Character);

		// Act - Move window downward, exposing top area
		moving.Top = 16;
		moving.Invalidate(true);
		system.Render.UpdateDisplay();

		// Assert - CRITICAL: Exposed area shows background
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		Assert.Equal('C', snapshot2.GetBack(30, 11).Character);
	}

	[Fact]
	public void Content_WindowMoveDiagonal_ExposesMultipleSides()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window with 'D' characters
		var background = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 60,
			Height = 30,
			Title = "Background"
		};
		var bgLines = new List<string>();
		for (int i = 0; i < 20; i++)
		{
			bgLines.Add("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD");
		}
		background.AddControl(new MarkupControl(bgLines));

		// Moving window with 'W' characters
		var moving = new Window(system)
		{
			Left = 20,
			Top = 10,
			Width = 25,
			Height = 12,
			Title = "Moving"
		};
		moving.AddControl(new MarkupControl(new List<string>
		{
			"WWWWWWWWWWWWWWWWWWWWW",
			"WWWWWWWWWWWWWWWWWWWWW",
			"WWWWWWWWWWWWWWWWWWWWW",
			"WWWWWWWWWWWWWWWWWWWWW",
			"WWWWWWWWWWWWWWWWWWWWW",
			"WWWWWWWWWWWWWWWWWWWWW"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);
		system.Render.UpdateDisplay();

		// Check multiple positions covered by moving window (within content areas)
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('W', snapshot1.GetBack(25, 13).Character); // Left side
		Assert.Equal('W', snapshot1.GetBack(38, 13).Character); // Right side
		Assert.Equal('W', snapshot1.GetBack(30, 12).Character); // Top side
		Assert.Equal('W', snapshot1.GetBack(30, 15).Character); // Bottom side (within 6-row content)

		// Act - Move diagonally (down and right)
		moving.Left = 35;
		moving.Top = 18;
		system.Render.UpdateDisplay();

		// Assert - CRITICAL: All exposed sides show background
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		Assert.Equal('D', snapshot2.GetBack(25, 13).Character); // Left exposed
		Assert.Equal('D', snapshot2.GetBack(30, 12).Character); // Top exposed
	}

	[Fact]
	public void Content_ThreeWindowsWithMovement_ComplexOcclusion()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Window 1 (bottom) - '1' characters
		var window1 = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 30,
			Height = 15,
			Title = "Window 1"
		};
		window1.AddControl(new MarkupControl(new List<string>
		{
			"11111111111111111111111111",
			"11111111111111111111111111",
			"11111111111111111111111111",
			"11111111111111111111111111",
			"11111111111111111111111111",
			"11111111111111111111111111"
		}));

		// Window 2 (middle) - '2' characters
		var window2 = new Window(system)
		{
			Left = 20,
			Top = 10,
			Width = 30,
			Height = 15,
			Title = "Window 2"
		};
		window2.AddControl(new MarkupControl(new List<string>
		{
			"22222222222222222222222222",
			"22222222222222222222222222",
			"22222222222222222222222222",
			"22222222222222222222222222",
			"22222222222222222222222222"
		}));

		// Window 3 (top) - '3' characters
		var window3 = new Window(system)
		{
			Left = 30,
			Top = 15,
			Width = 30,
			Height = 15,
			Title = "Window 3"
		};
		window3.AddControl(new MarkupControl(new List<string>
		{
			"33333333333333333333333333",
			"33333333333333333333333333",
			"33333333333333333333333333",
			"33333333333333333333333333"
		}));

		system.WindowStateService.AddWindow(window1);
		system.WindowStateService.AddWindow(window2);
		system.WindowStateService.AddWindow(window3);
		system.Render.UpdateDisplay();

		// Check initial state - window3 on top at position within all content areas
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('3', snapshot1.GetBack(35, 16).Character); // Window 3 visible (y=16 is in window3 content)

		// Act - Move window3 away, exposing window2
		window3.Left = 5;
		window3.Top = 25;
		system.Render.UpdateDisplay();

		// Assert - Window2 now visible where window3 was
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		Assert.Equal('2', snapshot2.GetBack(35, 13).Character); // y=13 is in window2 content (11-15)

		// Act - Move window2 away, exposing window1
		window2.Left = 5;
		window2.Top = 30;
		system.Render.UpdateDisplay();

		// Assert - Window1 now visible
		var snapshot3 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot3);
		Assert.Equal('1', snapshot3.GetBack(35, 9).Character); // y=9 is in window1 content (6-11)
	}

	[Fact]
	public void Content_MultipleZOrderAndMovementChanges_CorrectVisibility()
	{
		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Window A with 'A' content
		var windowA = new Window(system)
		{
			Left = 10,
			Top = 5,
			Width = 25,
			Height = 12,
			Title = "Window A"
		};
		windowA.AddControl(new MarkupControl(new List<string>
		{
			"AAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAA",
			"AAAAAAAAAAAAAAAAAAAA"
		}));

		// Window B with 'B' content
		var windowB = new Window(system)
		{
			Left = 25,
			Top = 10,
			Width = 25,
			Height = 12,
			Title = "Window B"
		};
		windowB.AddControl(new MarkupControl(new List<string>
		{
			"BBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBB"
		}));

		system.WindowStateService.AddWindow(windowA);
		system.WindowStateService.AddWindow(windowB);
		system.Render.UpdateDisplay();

		// Initial: B on top, overlaps A at position within B's content
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('B', snapshot1.GetBack(30, 12).Character); // y=12 is in windowB content (11-14)

		// Bring A to front
		system.WindowStateService.BringToFront(windowA);
		system.Render.UpdateDisplay();

		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);
		Assert.Equal('A', snapshot2.GetBack(30, 8).Character); // y=8 is in windowA content (6-9)

		// Move A to the right
		windowA.Left = 40;
		system.Render.UpdateDisplay();

		var snapshot3 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot3);
		Assert.Equal('B', snapshot3.GetBack(30, 12).Character); // B now visible again

		// Send B to back
		system.WindowStateService.SendToBack(windowB);
		system.Render.UpdateDisplay();

		// Still B at this position (A moved away)
		var snapshot4 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot4);
		Assert.Equal('B', snapshot4.GetBack(30, 13).Character);

		// Move B to overlap with new A position
		windowB.Left = 40;
		windowB.Top = 5;
		system.Render.UpdateDisplay();

		// A on top, should be visible
		var snapshot5 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot5);
		Assert.Equal('A', snapshot5.GetBack(45, 8).Character);
	}

	[Fact]
	public void Content_WindowMove_BordersAlsoTestedThroughMovement()
	{
		// This test demonstrates that moving windows tests borders too
		// As user emphasized: "moving windows will test borders too!"

		// Arrange
		var system = TestWindowSystemBuilder.CreateTestSystem();

		// Background window
		var background = new Window(system)
		{
			Left = 5,
			Top = 5,
			Width = 50,
			Height = 25,
			Title = "Background",
			BorderStyle = BorderStyle.Single
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"BACKGROUNDBACKGROUNDBACKGROUNDBACKGROUNDBACKGR",
			"BACKGROUNDBACKGROUNDBACKGROUNDBACKGROUNDBACKGR"
		}));

		// Moving window with DoubleLine border
		var moving = new Window(system)
		{
			Left = 20,
			Top = 10,
			Width = 25,
			Height = 10,
			Title = "Moving",
			BorderStyle = BorderStyle.DoubleLine
		};
		moving.AddControl(new MarkupControl(new List<string> { "CONTENT" }));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(moving);
		system.WindowStateService.SetActiveWindow(moving); // DoubleLine shows double chars when active

		system.Render.UpdateDisplay();

		// Check moving window's border before move
		var snapshot1 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot1);
		Assert.Equal('╔', snapshot1.GetBack(20, 10).Character); // DoubleLine top-left

		// Act - Move window, exposing area underneath
		moving.Left = 35;
		system.Render.UpdateDisplay();

		// Assert - CRITICAL: Exposed area shows background window's border/content
		var snapshot2 = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot2);

		// Position (20, 6) - background window's first content row
		// Was covered by moving window, now exposed after horizontal move
		var exposedCell = snapshot2.GetBack(20, 6);
		// Should be background content (from "BACKGROUND...") or background border
		Assert.True(
			"BACKGROUND".Contains(exposedCell.Character) ||
			exposedCell.Character == '│' ||
			exposedCell.Character == '─' ||
			exposedCell.Character == '┌' ||
			exposedCell.Character == '┐' ||
			exposedCell.Character == '└' ||
			exposedCell.Character == '┘',
			$"Expected background content or border, got: '{exposedCell.Character}'"
		);

		// Moving window's new position should show its DoubleLine border
		Assert.Equal('╔', snapshot2.GetBack(35, 10).Character);
	}
}
