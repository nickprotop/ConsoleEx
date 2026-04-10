using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

public class TerminalTransparencyTests
{
    [Fact]
    public void TransparentDesktop_TransparentWindow_CellEmits49()
    {
        // Full transparency stack: both desktop and window transparent
        // Cells should emit 49 (terminal default bg), not 48;2;R;G;B
        var system = TestWindowSystemBuilder.CreateTestSystem();
        ((SharpConsoleUI.Themes.ModernGrayTheme)system.Theme).DesktopBackgroundColor = Color.Transparent;
        system.DesktopBackgroundService.NeedsScreenUpdate = true;
        system.Render.DesktopNeedsRender = true;
        system.Render.UpdateDisplay();

        var window = new Window(system)
        {
            Left = 5, Top = 3, Width = 15, Height = 6,
            Title = "T",
            BackgroundColor = Color.Transparent
        };
        system.WindowStateService.AddWindow(window);
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        var cell = snapshot.GetBack(6, 4);
        Assert.Contains("49", cell.AnsiEscape);
        Assert.DoesNotContain("48;2;", cell.AnsiEscape);
    }

    [Fact]
    public void TransparentDesktop_OpaqueWindow_CellEmitsRGB()
    {
        // Transparent desktop but opaque window — window RGB should win
        var system = TestWindowSystemBuilder.CreateTestSystem();
        ((SharpConsoleUI.Themes.ModernGrayTheme)system.Theme).DesktopBackgroundColor = Color.Transparent;
        system.DesktopBackgroundService.NeedsScreenUpdate = true;
        system.Render.DesktopNeedsRender = true;
        system.Render.UpdateDisplay();

        var window = new Window(system)
        {
            Left = 5, Top = 3, Width = 15, Height = 6,
            Title = "T",
            BackgroundColor = new Color(0, 0, 128)
        };
        system.WindowStateService.AddWindow(window);
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        var cell = snapshot.GetBack(6, 4);
        Assert.Contains("48;2;0;0;128", cell.AnsiEscape);
    }

    [Fact]
    public void TransparentDesktop_ExposedArea_CellEmits49()
    {
        // Desktop area not covered by any window should emit 49
        var system = TestWindowSystemBuilder.CreateTestSystem();
        ((SharpConsoleUI.Themes.ModernGrayTheme)system.Theme).DesktopBackgroundColor = Color.Transparent;
        system.DesktopBackgroundService.NeedsScreenUpdate = true;
        system.Render.DesktopNeedsRender = true;
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        // Cell in the desktop area (no window)
        var cell = snapshot.GetBack(0, 0);
        Assert.Contains("49", cell.AnsiEscape);
        Assert.DoesNotContain("48;2;", cell.AnsiEscape);
    }

    [Fact]
    public void OpaqueDesktop_OpaqueWindow_CellEmitsRGB()
    {
        // Normal case: opaque desktop + opaque window — always explicit RGB
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var window = new Window(system)
        {
            Left = 5, Top = 3, Width = 15, Height = 6,
            Title = "T",
            BackgroundColor = new Color(100, 50, 200)
        };
        system.WindowStateService.AddWindow(window);
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        var cell = snapshot.GetBack(6, 4);
        Assert.Contains("48;2;100;50;200", cell.AnsiEscape);
    }

    [Fact]
    public void OpaqueDesktop_TransparentWindow_BlendsToDesktopColor()
    {
        // Transparent window over opaque desktop — compositor resolves to desktop color
        // Cell should emit explicit RGB (the desktop's color), NOT 49
        var system = TestWindowSystemBuilder.CreateTestSystem();

        var window = new Window(system)
        {
            Left = 5, Top = 3, Width = 15, Height = 6,
            Title = "T",
            BackgroundColor = Color.Transparent
        };
        system.WindowStateService.AddWindow(window);
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        var cell = snapshot.GetBack(6, 4);
        // Should be the theme's desktop color (opaque), not 49
        Assert.Contains("48;2;", cell.AnsiEscape);
    }

    [Fact]
    public void PreserveTerminalTransparency_SemiTransparentWindow_Emits49()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem(opts =>
            opts with { TerminalTransparencyMode = TerminalTransparencyMode.PreserveTerminalTransparency });

        ((SharpConsoleUI.Themes.ModernGrayTheme)system.Theme).DesktopBackgroundColor = Color.Transparent;
        system.DesktopBackgroundService.NeedsScreenUpdate = true;
        system.Render.DesktopNeedsRender = true;
        system.Render.UpdateDisplay();

        var window = new Window(system)
        {
            Left = 5, Top = 3, Width = 15, Height = 6,
            Title = "T",
            BackgroundColor = new Color(0, 50, 100, 128)
        };
        system.WindowStateService.AddWindow(window);
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        var cell = snapshot.GetBack(6, 4);
        Assert.Contains("49", cell.AnsiEscape);
        Assert.DoesNotContain("48;2;", cell.AnsiEscape);
    }

    [Fact]
    public void PreserveWindowColor_SemiTransparentWindow_EmitsRGB()
    {
        // Default mode: semi-transparent window blends against black, emits RGB
        var system = TestWindowSystemBuilder.CreateTestSystem();

        ((SharpConsoleUI.Themes.ModernGrayTheme)system.Theme).DesktopBackgroundColor = Color.Transparent;
        system.DesktopBackgroundService.NeedsScreenUpdate = true;
        system.Render.DesktopNeedsRender = true;
        system.Render.UpdateDisplay();

        var window = new Window(system)
        {
            Left = 5, Top = 3, Width = 15, Height = 6,
            Title = "T",
            BackgroundColor = new Color(0, 50, 100, 128)
        };
        system.WindowStateService.AddWindow(window);
        system.Render.UpdateDisplay();

        var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
        Assert.NotNull(snapshot);

        var cell = snapshot.GetBack(6, 4);
        Assert.Contains("48;2;", cell.AnsiEscape);
    }
}
