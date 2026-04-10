using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for per-cell alpha compositing of transparent windows (Mica-style).
/// Validates that semi-transparent window backgrounds composite correctly
/// against windows below and the desktop background.
/// </summary>
public class TransparentWindowCompositingTests
{
    /// <summary>
    /// Parses RGB background color from an ANSI escape sequence.
    /// Format: \x1b[0;38;2;R;G;B;48;2;R;G;Bm
    /// </summary>
    private static Color ParseBackgroundColor(string ansi)
    {
        var match = Regex.Match(ansi, @"48;2;(\d+);(\d+);(\d+)");
        Assert.True(match.Success, $"Could not parse background color from ANSI: {ansi}");
        return new Color(byte.Parse(match.Groups[1].Value),
                         byte.Parse(match.Groups[2].Value),
                         byte.Parse(match.Groups[3].Value));
    }

    /// <summary>
    /// Parses RGB foreground color from an ANSI escape sequence.
    /// </summary>
    private static Color ParseForegroundColor(string ansi)
    {
        var match = Regex.Match(ansi, @"38;2;(\d+);(\d+);(\d+)");
        Assert.True(match.Success, $"Could not parse foreground color from ANSI: {ansi}");
        return new Color(byte.Parse(match.Groups[1].Value),
                         byte.Parse(match.Groups[2].Value),
                         byte.Parse(match.Groups[3].Value));
    }

    [Fact]
    public void OpaqueWindow_RendersIdenticallyToBeforeChange()
    {
        // Arrange: fully opaque window should use the fast path
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 8,
            Title = "Opaque",
            BackgroundColor = new Color(0, 0, 128) // fully opaque (A=255)
        };
        window.AddControl(new MarkupControl(new List<string> { "Hello" }));
        system.WindowStateService.AddWindow(window);

        // Act
        system.Render.UpdateDisplay();

        // Assert: content renders, background is opaque blue
        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // Check 'H' is at expected position (Left+1, Top+1)
        var cell = snapshot.GetBack(6, 4);
        Assert.Equal(new Rune('H'), cell.Character);

        // Background should be opaque (0, 0, 128)
        var bg = ParseBackgroundColor(cell.AnsiEscape);
        Assert.Equal(0, bg.R);
        Assert.Equal(0, bg.G);
        Assert.Equal(128, bg.B);
    }

    [Fact]
    public void TransparentWindow_OverDesktop_CompositesBackground()
    {
        // Arrange: transparent window over desktop
        var system = TestWindowSystemBuilder.CreateTestSystem();

        // Render the desktop background first to populate the desktop buffer
        system.Render.UpdateDisplay();

        var window = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 8,
            Title = "Trans",
            BackgroundColor = new Color(255, 0, 0, 128) // 50% red
        };
        window.AddControl(new MarkupControl(new List<string> { " " }));
        system.WindowStateService.AddWindow(window);

        // Act
        system.Render.UpdateDisplay();

        // Assert: background should be composited (not raw alpha)
        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // An empty cell inside the transparent window (inside borders)
        var cell = snapshot.GetBack(6, 4);
        var bg = ParseBackgroundColor(cell.AnsiEscape);

        // The result should be A=255 (fully opaque after compositing)
        // and the red channel should be > 0 (blended with desktop)
        Assert.True(bg.R > 0, $"Expected red channel > 0 after compositing, got {bg.R}");
    }

    [Fact]
    public void TransparentWindow_OverOpaqueWindow_CharacterBubblesUpWithFadedForeground()
    {
        // Arrange: opaque bottom window with text, transparent top window
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 10,
            Title = "Bottom",
            BackgroundColor = new Color(0, 100, 0) // opaque green
        };
        bottomWindow.AddControl(new MarkupControl(new List<string>
        {
            "ABCDEFGHIJKLMNOPQR",
            "ABCDEFGHIJKLMNOPQR",
            "ABCDEFGHIJKLMNOPQR",
            "ABCDEFGHIJKLMNOPQR",
            "ABCDEFGHIJKLMNOPQR",
        }));
        system.WindowStateService.AddWindow(bottomWindow);

        // First render to establish bottom window
        system.Render.UpdateDisplay();

        // Top window overlaps, semi-transparent
        var topWindow = new Window(system)
        {
            Left = 7, Top = 4, Width = 16, Height = 6,
            Title = "Top",
            BackgroundColor = new Color(0, 0, 255, 128), // 50% blue
            ZIndex = 10
        };
        // No controls added — window is full of empty space cells
        system.WindowStateService.AddWindow(topWindow);

        // Act
        system.Render.UpdateDisplay();

        // Assert
        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // The character from below should bubble up through the transparent overlay
        var cell = snapshot.GetBack(8, 5);
        Assert.Equal(new Rune('C'), cell.Character);

        // Background should be composited: blue(128) over green → greenish-blue
        var bg = ParseBackgroundColor(cell.AnsiEscape);
        Assert.True(bg.B > 0, "Expected blue contribution from transparent overlay");
        Assert.True(bg.G > 0, "Expected green contribution from window below");

        // Foreground should be faded toward the background (tinted glass effect)
        // At 50% overlay, the fg should be significantly shifted toward the bg
        var fg = ParseForegroundColor(cell.AnsiEscape);
        // The original fg was white (255,255,255). After fading toward a blue-green bg,
        // all channels should still be present but shifted
        Assert.True(fg.B > 0, "Expected some blue in faded foreground");
    }

    [Fact]
    public void TransparentWindow_WithContent_KeepsOwnCharacters()
    {
        // Arrange: transparent window with actual text content
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 8,
            Title = "Bottom",
            BackgroundColor = new Color(0, 100, 0) // opaque green
        };
        bottomWindow.AddControl(new MarkupControl(new List<string> { "XXXXXXXXXXXX" }));
        system.WindowStateService.AddWindow(bottomWindow);

        var topWindow = new Window(system)
        {
            Left = 7, Top = 4, Width = 16, Height = 6,
            Title = "Top",
            BackgroundColor = new Color(255, 0, 0, 128), // 50% red
            ZIndex = 10
        };
        topWindow.AddControl(new MarkupControl(new List<string> { "HELLO" }));
        system.WindowStateService.AddWindow(topWindow);

        // Act
        system.Render.UpdateDisplay();

        // Assert: top window's own text should remain
        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // 'H' should be at top window content start: Left+1=8, Top+1=5
        var cell = snapshot.GetBack(8, 5);
        Assert.Equal(new Rune('H'), cell.Character);
    }

    [Fact]
    public void DoubleCompositing_GlassOverMicaOverOpaque()
    {
        // Arrange: three overlapping windows: opaque → semi-transparent → semi-transparent
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var opaqueWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 25, Height = 10,
            Title = "Opaque",
            BackgroundColor = new Color(200, 0, 0), // opaque red
            ZIndex = 1
        };
        opaqueWindow.AddControl(new MarkupControl(new List<string>
        {
            "AAAAAAAAAAAAAAAAAAAAAA",
            "AAAAAAAAAAAAAAAAAAAAAA",
            "AAAAAAAAAAAAAAAAAAAAAA",
        }));
        system.WindowStateService.AddWindow(opaqueWindow);

        var micaWindow = new Window(system)
        {
            Left = 8, Top = 5, Width = 20, Height = 8,
            Title = "Mica",
            BackgroundColor = new Color(0, 0, 200, 128), // 50% blue
            ZIndex = 5
        };
        system.WindowStateService.AddWindow(micaWindow);

        var glassWindow = new Window(system)
        {
            Left = 11, Top = 7, Width = 15, Height = 6,
            Title = "Glass",
            BackgroundColor = new Color(255, 255, 255, 128), // 50% white
            ZIndex = 10
        };
        system.WindowStateService.AddWindow(glassWindow);

        // Act
        system.Render.UpdateDisplay();

        // Assert: glass window content area should show composited result
        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // Glass content at (12, 8): composited white(128) → blue(128) → red(200)
        // The chain: glass bg blends over mica bg, which blends over opaque red bg
        var cell = snapshot.GetBack(12, 8);
        var bg = ParseBackgroundColor(cell.AnsiEscape);

        // All channels should have some value (white + blue + red mixed)
        // The exact values depend on blending math, but R and B should both be present
        Assert.True(bg.R > 30, $"Expected red contribution from opaque window, got {bg.R}");
        Assert.True(bg.B > 30, $"Expected blue contribution from mica window, got {bg.B}");
    }

    [Fact]
    public void TransparentWindow_NullDesktopBuffer_FallsBackToThemeColor()
    {
        // This test verifies no crash when DesktopBackgroundService has no buffer.
        // The test system by default may not have a populated desktop buffer,
        // so a transparent window should fall back to the theme's desktop color.
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var window = new Window(system)
        {
            Left = 5, Top = 3, Width = 15, Height = 6,
            Title = "Trans",
            BackgroundColor = new Color(128, 128, 128, 128)
        };
        system.WindowStateService.AddWindow(window);

        // Should not throw
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // Just verify we got a valid composited cell with no crash
        var cell = snapshot.GetBack(6, 4);
        var bg = ParseBackgroundColor(cell.AnsiEscape);
        Assert.Equal(255, bg.A);
    }

    [Fact]
    public void SetCellDirect_BypassesBlending()
    {
        // Arrange: verify SetCellDirect writes exact colors without blending
        var buffer = new CharacterBuffer(10, 5);
        buffer.Clear(new Color(100, 100, 100)); // base grey

        var cell = new Cell('X', new Color(255, 0, 0), new Color(0, 255, 0));

        // Act
        buffer.SetCellDirect(3, 2, cell);

        // Assert: colors should be exactly what we wrote, not blended
        var result = buffer.GetCell(3, 2);
        Assert.Equal(new Rune('X'), result.Character);
        Assert.Equal(new Color(255, 0, 0), result.Foreground);
        Assert.Equal(new Color(0, 255, 0), result.Background);
    }

    [Fact]
    public void SetCellDirect_PreservesDecorations()
    {
        var buffer = new CharacterBuffer(10, 5);
        buffer.Clear(Color.Black);

        var cell = new Cell('Z', Color.White, Color.Blue, TextDecoration.Bold | TextDecoration.Underline);
        cell.IsWideContinuation = false;
        cell.Combiners = null;

        buffer.SetCellDirect(0, 0, cell);

        var result = buffer.GetCell(0, 0);
        Assert.Equal(TextDecoration.Bold | TextDecoration.Underline, result.Decorations);
    }

    [Fact]
    public void SetCellDirect_OutOfBounds_DoesNotThrow()
    {
        var buffer = new CharacterBuffer(5, 5);
        var cell = new Cell('X', Color.White, Color.Black);

        // Should not throw for out-of-bounds
        buffer.SetCellDirect(-1, 0, cell);
        buffer.SetCellDirect(0, -1, cell);
        buffer.SetCellDirect(5, 0, cell);
        buffer.SetCellDirect(0, 5, cell);
    }

    [Fact]
    public void TransparentWindow_FillRectSkipped_NoFlicker()
    {
        // Verify that FillRect is skipped for transparent windows
        // by checking that the first render of a transparent window
        // produces composited output without intermediate un-composited fill
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 8,
            Title = "Bottom",
            BackgroundColor = new Color(0, 200, 0), // bright green
            ZIndex = 1
        };
        bottomWindow.AddControl(new MarkupControl(new List<string> { "TEST_CONTENT" }));
        system.WindowStateService.AddWindow(bottomWindow);

        // First render to establish bottom window
        system.Render.UpdateDisplay();

        var topWindow = new Window(system)
        {
            Left = 7, Top = 4, Width = 16, Height = 6,
            Title = "Top",
            BackgroundColor = new Color(0, 0, 255, 64), // 25% blue
            ZIndex = 10
        };
        system.WindowStateService.AddWindow(topWindow);

        // Act
        system.Render.UpdateDisplay();

        // Assert: the composited background should have green contribution from below
        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);
        var cell = snapshot.GetBack(8, 5);
        var bg = ParseBackgroundColor(cell.AnsiEscape);
        Assert.True(bg.G > 100, $"Expected strong green from window below (FillRect should not have painted over it), got G={bg.G}");
    }

    [Fact]
    public void TransparentWindow_TopBorder_CompositesBackground()
    {
        // Border cells should also be composited for transparent windows
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 10,
            Title = "Bottom",
            BackgroundColor = new Color(0, 200, 0), // bright green
            ZIndex = 1
        };
        bottomWindow.AddControl(new MarkupControl(new List<string>
        {
            "AAAAAAAAAAAAAAAAAA",
            "AAAAAAAAAAAAAAAAAA",
            "AAAAAAAAAAAAAAAAAA",
            "AAAAAAAAAAAAAAAAAA",
            "AAAAAAAAAAAAAAAAAA",
        }));
        system.WindowStateService.AddWindow(bottomWindow);

        system.Render.UpdateDisplay();

        // Transparent window overlapping — its top border row overlaps bottom window's content
        var topWindow = new Window(system)
        {
            Left = 8, Top = 5, Width = 16, Height = 6,
            Title = "Top",
            BackgroundColor = new Color(0, 0, 255, 128), // 50% blue
            ZIndex = 10
        };
        system.WindowStateService.AddWindow(topWindow);

        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // Top border is at (window.Left, window.Top) = (8, 5)
        // A border cell in the middle of the top border row
        var borderCell = snapshot.GetBack(12, 5);
        var bg = ParseBackgroundColor(borderCell.AnsiEscape);

        // Background should be composited: blue(128) over green → has both components
        Assert.True(bg.G > 30, $"Expected green contribution from window below in border cell, got G={bg.G}");
        Assert.True(bg.B > 30, $"Expected blue contribution from transparent border bg, got B={bg.B}");
    }

    [Fact]
    public void TransparentWindow_VerticalBorder_CompositesBackground()
    {
        // Left/right vertical border cells should also composite
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 10,
            Title = "Bottom",
            BackgroundColor = new Color(200, 0, 0), // red
            ZIndex = 1
        };
        bottomWindow.AddControl(new MarkupControl(new List<string>
        {
            "BBBBBBBBBBBBBBBBBB",
            "BBBBBBBBBBBBBBBBBB",
            "BBBBBBBBBBBBBBBBBB",
            "BBBBBBBBBBBBBBBBBB",
            "BBBBBBBBBBBBBBBBBB",
        }));
        system.WindowStateService.AddWindow(bottomWindow);

        system.Render.UpdateDisplay();

        var topWindow = new Window(system)
        {
            Left = 8, Top = 5, Width = 14, Height = 6,
            Title = "Top",
            BackgroundColor = new Color(0, 255, 0, 128), // 50% green
            ZIndex = 10
        };
        system.WindowStateService.AddWindow(topWindow);

        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // Left vertical border at (8, 6) — inside the window rows
        var leftBorder = snapshot.GetBack(8, 6);
        var bg = ParseBackgroundColor(leftBorder.AnsiEscape);

        // Should be composited: green(128) over red → both present
        Assert.True(bg.R > 30, $"Expected red contribution from window below in left border, got R={bg.R}");
        Assert.True(bg.G > 30, $"Expected green contribution from transparent border, got G={bg.G}");
    }

    [Fact]
    public void TransparentWindow_OverUnderlyingWindowBorder_CompositesCorrectly()
    {
        // When a transparent window's content area overlaps a border cell of the
        // window below, the border cell's background should be used for compositing
        // (not the desktop fallback).
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 10,
            Title = "Bottom",
            BackgroundColor = new Color(200, 0, 0), // opaque red
            ZIndex = 1
        };
        bottomWindow.AddControl(new MarkupControl(new List<string> { "TEXT" }));
        system.WindowStateService.AddWindow(bottomWindow);

        system.Render.UpdateDisplay();

        // Top window positioned so its content area overlaps bottom window's right border
        var topWindow = new Window(system)
        {
            Left = 20, Top = 5, Width = 12, Height = 5,
            Title = "Top",
            BackgroundColor = new Color(0, 0, 255, 128), // 50% blue
            ZIndex = 10
        };
        system.WindowStateService.AddWindow(topWindow);

        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // Bottom window's right border is at x = 5 + 20 - 1 = 24
        // Top window's content area starts at x = 20 + 1 = 21
        // So positions 21-23 overlap bottom window's content, and x=24 overlaps the right border
        // At screen position (24, 6): bottom window has right border (red bg),
        // top window has content cell (blue 50%)
        // The composited bg should have both red and blue
        var cell = snapshot.GetBack(24, 6);
        var bg = ParseBackgroundColor(cell.AnsiEscape);
        Assert.True(bg.R > 30, $"Expected red from bottom window's border, got R={bg.R}");
        Assert.True(bg.B > 30, $"Expected blue from transparent overlay, got B={bg.B}");
    }

    [Fact]
    public void OpaqueWindow_Borders_NotComposited()
    {
        // Opaque window borders should not be affected by compositing
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 10,
            Title = "Bottom",
            BackgroundColor = new Color(0, 200, 0),
            ZIndex = 1
        };
        system.WindowStateService.AddWindow(bottomWindow);

        system.Render.UpdateDisplay();

        var opaqueWindow = new Window(system)
        {
            Left = 8, Top = 5, Width = 14, Height = 6,
            Title = "Opq",
            BackgroundColor = new Color(0, 0, 200), // fully opaque blue
            ZIndex = 10
        };
        system.WindowStateService.AddWindow(opaqueWindow);

        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // Top border cell background should be pure blue (0, 0, 200), no green mixed in
        var borderCell = snapshot.GetBack(12, 5);
        var bg = ParseBackgroundColor(borderCell.AnsiEscape);
        Assert.Equal(0, bg.R);
        Assert.Equal(0, bg.G);
        Assert.Equal(200, bg.B);
    }

    [Fact]
    public void TransparentWindow_FullyOpaqueCell_NotComposited()
    {
        // A cell within a transparent window that has been painted with fully opaque
        // colors (e.g., from a control with explicit background) should pass through
        // without compositing.
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 8,
            Title = "Bottom",
            BackgroundColor = new Color(0, 200, 0),
            ZIndex = 1
        };
        bottomWindow.AddControl(new MarkupControl(new List<string> { "XXXXXXXXXXXX" }));
        system.WindowStateService.AddWindow(bottomWindow);

        var topWindow = new Window(system)
        {
            Left = 7, Top = 4, Width = 16, Height = 6,
            Title = "Top",
            BackgroundColor = new Color(0, 0, 255, 128), // semi-transparent
            ZIndex = 10
        };
        // Control with explicit opaque background overrides window bg
        var markup = new MarkupControl(new List<string> { "[on #FF0000]OPAQUE[/]" });
        topWindow.AddControl(markup);
        system.WindowStateService.AddWindow(topWindow);

        // Act
        system.Render.UpdateDisplay();

        // Assert: the 'O' character should have the opaque red background, no compositing
        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);
        var cell = snapshot.GetBack(8, 5);
        Assert.Equal(new Rune('O'), cell.Character);
        var bg = ParseBackgroundColor(cell.AnsiEscape);
        Assert.Equal(255, bg.R);
        Assert.Equal(0, bg.G);
        Assert.Equal(0, bg.B);
    }

    [Fact]
    public void MicaBrush_NoCharacterBubbleUp()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 10,
            Title = "Bottom",
            BackgroundColor = new Color(0, 100, 0),
            ZIndex = 1
        };
        bottomWindow.AddControl(new MarkupControl(new List<string>
        {
            "ABCDEFGHIJKLMNOPQR",
            "ABCDEFGHIJKLMNOPQR",
            "ABCDEFGHIJKLMNOPQR",
        }));
        system.WindowStateService.AddWindow(bottomWindow);
        system.Render.UpdateDisplay();

        var topWindow = new Window(system)
        {
            Left = 7, Top = 4, Width = 16, Height = 6,
            Title = "Mica",
            BackgroundColor = new Color(0, 0, 255, 128),
            ZIndex = 10,
            TransparencyBrush = SharpConsoleUI.Rendering.TransparencyBrush.Mica()
        };
        system.WindowStateService.AddWindow(topWindow);
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // Mica: no character bubble-up
        var cell = snapshot.GetBack(8, 5);
        Assert.Equal(new Rune(' '), cell.Character);

        var bg = ParseBackgroundColor(cell.AnsiEscape);
        Assert.True(bg.B > 0, "Expected blue from Mica overlay");
    }

    [Fact]
    public void TintedBrush_NoBubbleUp()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 10,
            Title = "Bottom",
            BackgroundColor = new Color(0, 200, 0),
            ZIndex = 1
        };
        bottomWindow.AddControl(new MarkupControl(new List<string>
        {
            "ABCDEFGHIJKLMNOPQR",
            "ABCDEFGHIJKLMNOPQR",
        }));
        system.WindowStateService.AddWindow(bottomWindow);
        system.Render.UpdateDisplay();

        var topWindow = new Window(system)
        {
            Left = 7, Top = 4, Width = 16, Height = 6,
            Title = "Tinted",
            BackgroundColor = new Color(0, 0, 255, 128),
            ZIndex = 10,
            TransparencyBrush = SharpConsoleUI.Rendering.TransparencyBrush.Tinted()
        };
        system.WindowStateService.AddWindow(topWindow);
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        var cell = snapshot.GetBack(8, 5);
        Assert.Equal(new Rune(' '), cell.Character);

        var bg = ParseBackgroundColor(cell.AnsiEscape);
        Assert.True(bg.B > 0, "Expected blue from tinted overlay");
        Assert.True(bg.G > 0, "Expected green from window below");
    }

    [Fact]
    public void AcrylicBrush_CharacterBubblesUp()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 10,
            Title = "Bottom",
            BackgroundColor = new Color(0, 100, 0),
            ZIndex = 1
        };
        bottomWindow.AddControl(new MarkupControl(new List<string>
        {
            "ABCDEFGHIJKLMNOPQR",
            "ABCDEFGHIJKLMNOPQR",
            "ABCDEFGHIJKLMNOPQR",
        }));
        system.WindowStateService.AddWindow(bottomWindow);
        system.Render.UpdateDisplay();

        var topWindow = new Window(system)
        {
            Left = 7, Top = 4, Width = 16, Height = 6,
            Title = "Acrylic",
            BackgroundColor = new Color(0, 0, 255, 128),
            ZIndex = 10,
            TransparencyBrush = SharpConsoleUI.Rendering.TransparencyBrush.Acrylic()
        };
        system.WindowStateService.AddWindow(topWindow);
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        var cell = snapshot.GetBack(8, 5);
        Assert.Equal(new Rune('C'), cell.Character);
    }

    [Fact]
    public void CustomBrush_DelegateIsCalled()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var bottomWindow = new Window(system)
        {
            Left = 5, Top = 3, Width = 20, Height = 8,
            Title = "Bottom",
            BackgroundColor = new Color(0, 200, 0),
            ZIndex = 1
        };
        system.WindowStateService.AddWindow(bottomWindow);
        system.Render.UpdateDisplay();

        bool delegateCalled = false;
        var customBrush = SharpConsoleUI.Rendering.TransparencyBrush.WithCustom((top, below, alpha) =>
        {
            delegateCalled = true;
            var bg = Color.Blend(top.Background, below.Background);
            return new Cell('*', Color.White, bg);
        });

        var topWindow = new Window(system)
        {
            Left = 7, Top = 4, Width = 16, Height = 6,
            Title = "Custom",
            BackgroundColor = new Color(0, 0, 255, 128),
            ZIndex = 10,
            TransparencyBrush = customBrush
        };
        system.WindowStateService.AddWindow(topWindow);
        system.Render.UpdateDisplay();

        Assert.True(delegateCalled, "Custom brush delegate should have been called");

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);
        var cell = snapshot.GetBack(8, 5);
        Assert.Equal(new Rune('*'), cell.Character);
    }
}
