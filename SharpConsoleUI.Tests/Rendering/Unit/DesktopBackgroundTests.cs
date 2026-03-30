using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit;

public class DesktopBackgroundTests
{
    #region Default Config

    [Fact]
    public void Default_HasNoGradientOrPattern()
    {
        var config = DesktopBackgroundConfig.Default;

        Assert.Null(config.Gradient);
        Assert.Null(config.Pattern);
        Assert.Null(config.PaintCallback);
        Assert.Equal(100, config.AnimationIntervalMs);
    }

    #endregion

    #region FromGradient Factory

    [Fact]
    public void FromGradient_SetsGradientAndDirection()
    {
        var gradient = ColorGradient.FromColors(Color.Black, Color.White);
        var config = DesktopBackgroundConfig.FromGradient(gradient, GradientDirection.Horizontal);

        Assert.NotNull(config.Gradient);
        Assert.Equal(gradient, config.Gradient.Gradient);
        Assert.Equal(GradientDirection.Horizontal, config.Gradient.Direction);
        Assert.Null(config.Pattern);
    }

    [Fact]
    public void FromGradient_VerticalDirection_IsPreserved()
    {
        var gradient = ColorGradient.FromColors(Color.Red, Color.Blue);
        var config = DesktopBackgroundConfig.FromGradient(gradient, GradientDirection.Vertical);

        Assert.Equal(GradientDirection.Vertical, config.Gradient!.Direction);
    }

    #endregion

    #region FromPattern Factory

    [Fact]
    public void FromPattern_SetsPattern()
    {
        var pattern = DesktopPatterns.Checkerboard;
        var config = DesktopBackgroundConfig.FromPattern(pattern);

        Assert.NotNull(config.Pattern);
        Assert.Same(pattern, config.Pattern);
        Assert.Null(config.Gradient);
    }

    #endregion

    #region DesktopPattern Construction

    [Fact]
    public void DesktopPattern_DimensionsMatchCharArray()
    {
        // 2 rows, 3 columns → Height=2, Width=3
        var chars = new char[2, 3]
        {
            { 'a', 'b', 'c' },
            { 'd', 'e', 'f' }
        };
        var pattern = new DesktopPattern(chars);

        Assert.Equal(3, pattern.Width);
        Assert.Equal(2, pattern.Height);
    }

    [Fact]
    public void DesktopPattern_EmptyThrows()
    {
        var emptyChars = new char[0, 0];

        Assert.Throws<ArgumentException>(() => new DesktopPattern(emptyChars));
    }

    [Fact]
    public void DesktopPattern_ZeroRowsThrows()
    {
        var emptyChars = new char[0, 3];

        Assert.Throws<ArgumentException>(() => new DesktopPattern(emptyChars));
    }

    [Fact]
    public void DesktopPattern_ZeroColumnsThrows()
    {
        var emptyChars = new char[3, 0];

        Assert.Throws<ArgumentException>(() => new DesktopPattern(emptyChars));
    }

    #endregion

    #region Gradient and Pattern Combined

    [Fact]
    public void GradientAndPatternCanCombine()
    {
        var gradient = ColorGradient.FromColors(Color.Black, Color.White);
        var pattern = DesktopPatterns.Dots;

        var config = new DesktopBackgroundConfig
        {
            Gradient = new GradientBackground(gradient, GradientDirection.DiagonalDown),
            Pattern = pattern
        };

        Assert.NotNull(config.Gradient);
        Assert.NotNull(config.Pattern);
        Assert.Same(pattern, config.Pattern);
    }

    #endregion

    #region Animation Config

    [Fact]
    public void AnimationConfig_SetsCallbackAndInterval()
    {
        Action<CharacterBuffer, int, int, TimeSpan> callback = (buf, w, h, elapsed) => { };

        var config = new DesktopBackgroundConfig
        {
            PaintCallback = callback,
            AnimationIntervalMs = 50
        };

        Assert.NotNull(config.PaintCallback);
        Assert.Same(callback, config.PaintCallback);
        Assert.Equal(50, config.AnimationIntervalMs);
    }

    [Fact]
    public void AnimationConfig_DefaultIntervalIs100()
    {
        var config = new DesktopBackgroundConfig
        {
            PaintCallback = (buf, w, h, elapsed) => { }
        };

        Assert.Equal(100, config.AnimationIntervalMs);
    }

    #endregion
}
