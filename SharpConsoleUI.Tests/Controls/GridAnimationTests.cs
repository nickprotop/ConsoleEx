using SharpConsoleUI.Animation;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests for HorizontalGridControl column width animation.
/// </summary>
public class GridAnimationTests
{
    private static HorizontalGridControl CreateGridWithColumns(params int?[] widths)
    {
        var grid = new HorizontalGridControl();
        foreach (var w in widths)
        {
            var col = new ColumnContainer(grid) { Width = w };
            grid.AddColumn(col);
        }
        return grid;
    }

    #region AnimateColumnWidth — valid index

    [Fact]
    public void AnimateColumnWidth_ValidIndex_WithManager_ReturnsAnimation()
    {
        var grid = CreateGridWithColumns(40, 60);
        var manager = new AnimationManager();
        grid.SetAnimationManagerForTesting(manager);

        var result = grid.AnimateColumnWidth(0, 80, TimeSpan.FromMilliseconds(300));

        Assert.NotNull(result);
        Assert.False(result!.IsComplete);
    }

    #endregion

    #region AnimateColumnWidth — invalid index

    [Fact]
    public void AnimateColumnWidth_NegativeIndex_ReturnsNull()
    {
        var grid = CreateGridWithColumns(40);
        var manager = new AnimationManager();
        grid.SetAnimationManagerForTesting(manager);

        var result = grid.AnimateColumnWidth(-1, 80, TimeSpan.FromMilliseconds(300));

        Assert.Null(result);
    }

    [Fact]
    public void AnimateColumnWidth_IndexOutOfRange_ReturnsNull()
    {
        var grid = CreateGridWithColumns(40);
        var manager = new AnimationManager();
        grid.SetAnimationManagerForTesting(manager);

        var result = grid.AnimateColumnWidth(5, 80, TimeSpan.FromMilliseconds(300));

        Assert.Null(result);
    }

    #endregion

    #region AnimateColumnWidth — targetWidth 0 hides column on completion

    [Fact]
    public void AnimateColumnWidth_TargetZero_SetsVisibleFalse_OnCompletion()
    {
        var grid = CreateGridWithColumns(40);
        var manager = new AnimationManager();
        grid.SetAnimationManagerForTesting(manager);

        var col = grid.Columns[0];
        Assert.True(col.Visible);

        grid.AnimateColumnWidth(0, 0, TimeSpan.FromMilliseconds(200));

        // Column is still visible while animating
        Assert.True(col.Visible);

        // Advance past animation duration to trigger onComplete
        AdvanceByMs(manager, 300);

        Assert.False(col.Visible);
    }

    #endregion

    #region AnimateColumnWidth — from width 0 sets Visible true before animation

    [Fact]
    public void AnimateColumnWidth_FromZero_SetsVisibleTrue_BeforeAnimation()
    {
        var grid = CreateGridWithColumns(0);
        var col = grid.Columns[0];
        col.Visible = false;

        var manager = new AnimationManager();
        grid.SetAnimationManagerForTesting(manager);

        var result = grid.AnimateColumnWidth(0, 60, TimeSpan.FromMilliseconds(200));

        // Visible should be set true immediately, before animation completes
        Assert.True(col.Visible);
        Assert.NotNull(result);
    }

    #endregion

    #region AnimateColumnWidth — no AnimationManager sets width immediately

    [Fact]
    public void AnimateColumnWidth_NoManager_SetsWidthImmediately_ReturnsNull()
    {
        var grid = CreateGridWithColumns(40);
        var col = grid.Columns[0];

        var result = grid.AnimateColumnWidth(0, 80, TimeSpan.FromMilliseconds(300));

        Assert.Null(result);
        Assert.Equal(80, col.Width);
    }

    [Fact]
    public void AnimateColumnWidth_NoManager_TargetZero_SetsVisibleFalse()
    {
        var grid = CreateGridWithColumns(40);
        var col = grid.Columns[0];

        var result = grid.AnimateColumnWidth(0, 0, TimeSpan.FromMilliseconds(300));

        Assert.Null(result);
        Assert.Equal(0, col.Width);
        Assert.False(col.Visible);
    }

    [Fact]
    public void AnimateColumnWidth_NoManager_FromZero_SetsVisibleTrue()
    {
        var grid = CreateGridWithColumns(0);
        var col = grid.Columns[0];
        col.Visible = false;

        var result = grid.AnimateColumnWidth(0, 60, TimeSpan.FromMilliseconds(300));

        Assert.Null(result);
        Assert.True(col.Visible);
        Assert.Equal(60, col.Width);
    }

    #endregion

    #region AnimateColumnWidth — width updates during animation

    [Fact]
    public void AnimateColumnWidth_UpdatesWidth_DuringAnimation()
    {
        var grid = CreateGridWithColumns(10);
        var manager = new AnimationManager();
        grid.SetAnimationManagerForTesting(manager);

        var col = grid.Columns[0];
        grid.AnimateColumnWidth(0, 100, TimeSpan.FromMilliseconds(200));

        // Advance partway — width should have changed from initial
        AdvanceByMs(manager, 100);
        var midWidth = col.Width;
        Assert.NotNull(midWidth);
        Assert.True(midWidth > 10, $"Expected width > 10 during animation, got {midWidth}");

        // Advance to completion
        AdvanceByMs(manager, 200);
        Assert.Equal(100, col.Width);
    }

    #endregion

    /// <summary>
    /// Advances the animation manager in small steps to respect MaxFrameDeltaMs cap.
    /// </summary>
    private static void AdvanceByMs(AnimationManager manager, double totalMs)
    {
        var maxStep = SharpConsoleUI.Configuration.AnimationDefaults.MaxFrameDeltaMs;
        double remaining = totalMs;
        while (remaining > 0)
        {
            double tick = Math.Min(remaining, maxStep);
            manager.Update(TimeSpan.FromMilliseconds(tick));
            remaining -= tick;
        }
    }
}
