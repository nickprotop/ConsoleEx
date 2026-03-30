using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Themes;
using System.Text;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit;

public class DesktopBackgroundServiceTests
{
    // Helper: create a service with a ModernGrayTheme and optional dirty callback
    private static DesktopBackgroundService CreateService(
        ModernGrayTheme? theme = null,
        Action? onDirty = null)
    {
        theme ??= new ModernGrayTheme();
        return new DesktopBackgroundService(() => theme, onDirty ?? (() => { }));
    }

    #region Render_CreatesBuffer

    [Fact]
    public void Render_CreatesBuffer_HasBufferIsTrueAndBufferNotNull()
    {
        var service = CreateService();

        service.Render(80, 24);

        Assert.True(service.HasBuffer);
        Assert.NotNull(service.Buffer);
    }

    #endregion

    #region Render_FillsWithThemeColor

    [Fact]
    public void Render_FillsWithThemeColor_CellsHaveThemeBackground()
    {
        var theme = new ModernGrayTheme
        {
            DesktopBackgroundColor = Color.DarkBlue,
            DesktopBackgroundChar = ' ',
            DesktopBackgroundGradient = null
        };
        var service = CreateService(theme);

        service.Render(20, 5);

        var buffer = service.Buffer!;
        // Spot-check several cells across the buffer
        Assert.Equal(Color.DarkBlue, buffer.GetCell(0, 0).Background);
        Assert.Equal(Color.DarkBlue, buffer.GetCell(10, 2).Background);
        Assert.Equal(Color.DarkBlue, buffer.GetCell(19, 4).Background);
    }

    #endregion

    #region Render_WithGradient_AppliesGradient

    [Fact]
    public void Render_WithGradient_HorizontalGradient_LeftIsNearBlackRightIsNearWhite()
    {
        var theme = new ModernGrayTheme { DesktopBackgroundGradient = null };
        var service = CreateService(theme);

        var gradient = ColorGradient.FromColors(Color.Black, Color.White);
        service.Config = new DesktopBackgroundConfig
        {
            Gradient = new GradientBackground(gradient, GradientDirection.Horizontal)
        };

        service.Render(20, 5);

        var buffer = service.Buffer!;
        var leftCell = buffer.GetCell(0, 0).Background;
        var rightCell = buffer.GetCell(19, 0).Background;

        // Left should be near black
        Assert.InRange(leftCell.R, 0, 10);
        Assert.InRange(leftCell.G, 0, 10);
        Assert.InRange(leftCell.B, 0, 10);

        // Right should be near white
        Assert.InRange(rightCell.R, 245, 255);
        Assert.InRange(rightCell.G, 245, 255);
        Assert.InRange(rightCell.B, 245, 255);
    }

    #endregion

    #region Render_WithPattern_TilesCharacters

    [Fact]
    public void Render_WithPattern_CheckerboardTilesAcrossBuffer()
    {
        var service = CreateService();
        service.Config = DesktopBackgroundConfig.FromPattern(DesktopPatterns.Checkerboard);

        service.Render(10, 4);

        var buffer = service.Buffer!;

        // Checkerboard pattern is:
        // row 0: ['░', ' ']
        // row 1: [' ', '░']
        // Verify tile positions
        Assert.Equal(new Rune('░'), buffer.GetCell(0, 0).Character); // (0,0) -> px=0, py=0 -> '░'
        Assert.Equal(new Rune(' '), buffer.GetCell(1, 0).Character); // (1,0) -> px=1, py=0 -> ' '
        Assert.Equal(new Rune(' '), buffer.GetCell(0, 1).Character); // (0,1) -> px=0, py=1 -> ' '
        Assert.Equal(new Rune('░'), buffer.GetCell(1, 1).Character); // (1,1) -> px=1, py=1 -> '░'

        // Verify tiling repeats
        Assert.Equal(new Rune('░'), buffer.GetCell(2, 0).Character); // (2,0) -> px=0, py=0 -> '░'
        Assert.Equal(new Rune('░'), buffer.GetCell(3, 1).Character); // (3,1) -> px=1, py=1 -> '░'
    }

    #endregion

    #region Render_GradientPlusPattern_CombinesBoth

    [Fact]
    public void Render_GradientPlusPattern_PatternCharsAppliedAndBackgroundColorsVary()
    {
        var theme = new ModernGrayTheme { DesktopBackgroundGradient = null };
        var service = CreateService(theme);

        var gradient = ColorGradient.FromColors(Color.Black, Color.White);
        service.Config = new DesktopBackgroundConfig
        {
            Gradient = new GradientBackground(gradient, GradientDirection.Horizontal),
            Pattern = DesktopPatterns.Checkerboard
        };

        service.Render(20, 4);

        var buffer = service.Buffer!;

        // Pattern chars should be applied
        Assert.Equal(new Rune('░'), buffer.GetCell(0, 0).Character);
        Assert.Equal(new Rune(' '), buffer.GetCell(1, 0).Character);

        // Background colors should differ between left and right (gradient applied before pattern)
        var leftBg = buffer.GetCell(0, 0).Background;
        var rightBg = buffer.GetCell(19, 0).Background;
        Assert.NotEqual(leftBg, rightBg);
    }

    #endregion

    #region Invalidate_SignalsDirty

    [Fact]
    public void Invalidate_AfterInitialRender_InvokesDirtyCallback()
    {
        int dirtyCount = 0;
        var service = CreateService(onDirty: () => dirtyCount++);

        // First do a render to set width/height
        service.Render(20, 5);
        int countAfterRender = dirtyCount;

        service.Invalidate();

        Assert.True(dirtyCount > countAfterRender, "Dirty callback should have been invoked by Invalidate");
    }

    [Fact]
    public void Invalidate_BeforeRender_DoesNotInvokeDirtyCallback()
    {
        int dirtyCount = 0;
        var service = CreateService(onDirty: () => dirtyCount++);

        // Invalidate without prior render — width/height are 0, so callback should not fire
        service.Invalidate();

        Assert.Equal(0, dirtyCount);
    }

    #endregion

    #region Config_Set_InvalidatesCache

    [Fact]
    public void Config_Set_TriggersDirtyCallback()
    {
        int dirtyCount = 0;
        var service = CreateService(onDirty: () => dirtyCount++);

        // Give it known dimensions first
        service.Render(20, 5);
        int countAfterRender = dirtyCount;

        service.Config = DesktopBackgroundConfig.Default;

        Assert.True(dirtyCount > countAfterRender, "Setting Config should trigger dirty callback via Invalidate");
    }

    #endregion

    #region Render_WithPaintCallback_CallsCallback

    [Fact]
    public void Render_WithPaintCallback_CallbackReceivesBufferAndPaintsIt()
    {
        bool callbackInvoked = false;
        var service = CreateService();

        service.Config = new DesktopBackgroundConfig
        {
            PaintCallback = (buffer, width, height, elapsed) =>
            {
                callbackInvoked = true;
                // Paint a recognizable color into the top-left cell
                buffer.SetNarrowCell(0, 0, 'X', Color.Red, Color.Green);
            },
            AnimationIntervalMs = 10000 // long interval so timer doesn't fire during test
        };

        service.Render(20, 5);

        Assert.True(callbackInvoked, "PaintCallback should have been called during Render");

        var cell = service.Buffer!.GetCell(0, 0);
        Assert.Equal(new Rune('X'), cell.Character);
        Assert.Equal(Color.Green, cell.Background);
    }

    #endregion

    #region Render_ResizeCreatesNewBuffer

    [Fact]
    public void Render_ResizeCreatesNewBuffer_DifferentBufferInstances()
    {
        var service = CreateService();

        service.Render(10, 5);
        var firstBuffer = service.Buffer;

        service.Render(20, 10);
        var secondBuffer = service.Buffer;

        Assert.NotSame(firstBuffer, secondBuffer);
        Assert.Equal(20, secondBuffer!.Width);
        Assert.Equal(10, secondBuffer.Height);
    }

    [Fact]
    public void Render_SameSizeDoesNotReplaceBuffer()
    {
        var service = CreateService();

        service.Render(10, 5);
        var firstBuffer = service.Buffer;

        service.Render(10, 5);
        var secondBuffer = service.Buffer;

        Assert.Same(firstBuffer, secondBuffer);
    }

    #endregion

    #region Dispose_StopsAnimation

    [Fact]
    public void Dispose_WithAnimationConfig_DoesNotThrow()
    {
        var service = CreateService();
        service.Config = new DesktopBackgroundConfig
        {
            PaintCallback = (buffer, width, height, elapsed) => { },
            AnimationIntervalMs = 50
        };
        service.Render(10, 5);

        // Dispose should not throw
        service.Dispose();
    }

    [Fact]
    public void Dispose_StopsAnimationTimer_DirtyNotCalledAfterDispose()
    {
        int dirtyCount = 0;
        var service = CreateService(onDirty: () => dirtyCount++);

        service.Render(10, 5);
        service.Config = new DesktopBackgroundConfig
        {
            PaintCallback = (buffer, width, height, elapsed) => { },
            AnimationIntervalMs = 30
        };

        // Let it tick a couple of times
        Thread.Sleep(100);
        service.Dispose();

        int countAtDispose = dirtyCount;

        // Wait to confirm no more callbacks fire after dispose
        Thread.Sleep(100);

        Assert.Equal(countAtDispose, dirtyCount);
    }

    #endregion
}
