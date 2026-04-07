using SharpConsoleUI.Animation;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests for RuleControl gradient progress rendering.
/// </summary>
public class RuleProgressTests
{
    private static RuleControl CreateRule()
    {
        return new RuleControl { Width = 80 };
    }

    #region SetProgress

    [Fact]
    public void SetProgress_StoresRatioAndActivatesProgress()
    {
        var rule = CreateRule();
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetProgress(0.5f, gradient);

        Assert.Equal(0.5f, rule.ProgressRatio);
        Assert.True(rule.IsProgressActive);
        Assert.False(rule.IsIndeterminate);
    }

    [Fact]
    public void SetProgress_ClampsAboveOne()
    {
        var rule = CreateRule();
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetProgress(1.5f, gradient);

        Assert.Equal(1.0f, rule.ProgressRatio);
    }

    [Fact]
    public void SetProgress_ClampsBelowZero()
    {
        var rule = CreateRule();
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetProgress(-0.5f, gradient);

        Assert.Equal(0.0f, rule.ProgressRatio);
    }

    [Fact]
    public void SetProgress_ClampsExactBoundaries()
    {
        var rule = CreateRule();
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetProgress(0.0f, gradient);
        Assert.Equal(0.0f, rule.ProgressRatio);

        rule.SetProgress(1.0f, gradient);
        Assert.Equal(1.0f, rule.ProgressRatio);
    }

    #endregion

    #region ClearProgress

    [Fact]
    public void ClearProgress_ResetsState()
    {
        var rule = CreateRule();
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetProgress(0.75f, gradient);
        Assert.True(rule.IsProgressActive);

        rule.ClearProgress(TimeSpan.Zero);

        Assert.Equal(0.0f, rule.ProgressRatio);
        Assert.False(rule.IsProgressActive);
        Assert.False(rule.IsIndeterminate);
    }

    [Fact]
    public void ClearProgress_NoManager_ResetsImmediately()
    {
        var rule = CreateRule();
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetProgress(0.5f, gradient);
        rule.ClearProgress(); // default fade, but no manager

        Assert.Equal(0.0f, rule.ProgressRatio);
        Assert.False(rule.IsProgressActive);
    }

    [Fact]
    public void ClearProgress_WithManager_AnimatesDown()
    {
        var rule = CreateRule();
        var manager = new AnimationManager();
        rule.SetAnimationManagerForTesting(manager);
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetProgress(0.8f, gradient);
        rule.ClearProgress(TimeSpan.FromMilliseconds(300));

        // Animation started - ratio hasn't reached 0 yet
        Assert.True(manager.HasActiveAnimations);

        // Advance past the duration (delta is capped at 33ms per frame)
        for (int i = 0; i < 20; i++)
            manager.Update(TimeSpan.FromMilliseconds(33));

        Assert.Equal(0.0f, rule.ProgressRatio);
        Assert.False(rule.IsProgressActive);
    }

    #endregion

    #region SetIndeterminate

    [Fact]
    public void SetIndeterminate_ActivatesShimmerMode()
    {
        var rule = CreateRule();
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetIndeterminate(gradient);

        Assert.True(rule.IsIndeterminate);
        Assert.True(rule.IsProgressActive);
        Assert.Equal(0.0f, rule.ProgressRatio);
    }

    [Fact]
    public void SetIndeterminate_WithManager_StartsAnimation()
    {
        var rule = CreateRule();
        var manager = new AnimationManager();
        rule.SetAnimationManagerForTesting(manager);
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetIndeterminate(gradient);

        Assert.True(rule.IsIndeterminate);
        Assert.True(manager.HasActiveAnimations);
    }

    #endregion

    #region Mode Switching

    [Fact]
    public void SetProgress_AfterSetIndeterminate_SwitchesToDeterminate()
    {
        var rule = CreateRule();
        var manager = new AnimationManager();
        rule.SetAnimationManagerForTesting(manager);
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetIndeterminate(gradient);
        Assert.True(rule.IsIndeterminate);

        rule.SetProgress(0.5f, gradient);

        Assert.False(rule.IsIndeterminate);
        Assert.Equal(0.5f, rule.ProgressRatio);
        Assert.True(rule.IsProgressActive);
    }

    [Fact]
    public void SetIndeterminate_AfterSetProgress_SwitchesToIndeterminate()
    {
        var rule = CreateRule();
        var gradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

        rule.SetProgress(0.5f, gradient);
        Assert.False(rule.IsIndeterminate);

        rule.SetIndeterminate(gradient);

        Assert.True(rule.IsIndeterminate);
        Assert.Equal(0.0f, rule.ProgressRatio);
    }

    #endregion

    #region Initial State

    [Fact]
    public void InitialState_NoProgressActive()
    {
        var rule = CreateRule();

        Assert.Equal(0.0f, rule.ProgressRatio);
        Assert.False(rule.IsProgressActive);
        Assert.False(rule.IsIndeterminate);
    }

    #endregion
}
