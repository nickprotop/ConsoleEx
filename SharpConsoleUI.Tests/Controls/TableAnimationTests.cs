using SharpConsoleUI.Animation;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests for TableControl row animation methods.
/// Since animations require an AnimationManager (normally obtained from the parent window),
/// these tests verify behavior when the table is not attached to a window (returns null)
/// and verify internal state tracking.
/// </summary>
public class TableAnimationTests
{
    private static TableControl CreateTableWithRows(int rowCount)
    {
        var table = new TableControl();
        table.AddColumn("Col1");
        table.AddColumn("Col2");
        for (int i = 0; i < rowCount; i++)
            table.AddRow($"R{i}C1", $"R{i}C2");
        return table;
    }

    #region FlashRow

    [Fact]
    public void FlashRow_InvalidIndex_ReturnsNull()
    {
        var table = CreateTableWithRows(3);

        Assert.Null(table.FlashRow(-1, Color.Red, TimeSpan.FromMilliseconds(300)));
        Assert.Null(table.FlashRow(3, Color.Red, TimeSpan.FromMilliseconds(300)));
        Assert.Null(table.FlashRow(100, Color.Red, TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public void FlashRow_ValidIndex_NoWindow_ReturnsNull()
    {
        // Without a parent window, no AnimationManager is available
        var table = CreateTableWithRows(3);

        var result = table.FlashRow(0, Color.Red, TimeSpan.FromMilliseconds(300));

        Assert.Null(result);
    }

    [Fact]
    public void FlashRow_WithAnimationManager_ReturnsAnimation()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        var result = table.FlashRow(1, Color.Red, TimeSpan.FromMilliseconds(300));

        Assert.NotNull(result);
        Assert.False(result!.IsComplete);
    }

    [Fact]
    public void FlashRow_TracksActiveAnimation()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        Assert.False(table.HasActiveRowAnimations);

        table.FlashRow(0, Color.Red, TimeSpan.FromMilliseconds(300));

        Assert.True(table.HasActiveRowAnimations);
    }

    #endregion

    #region FlashCell

    [Fact]
    public void FlashCell_InvalidRowIndex_ReturnsNull()
    {
        var table = CreateTableWithRows(3);

        Assert.Null(table.FlashCell(-1, 0, Color.Blue, TimeSpan.FromMilliseconds(200)));
        Assert.Null(table.FlashCell(3, 0, Color.Blue, TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void FlashCell_InvalidColumnIndex_ReturnsNull()
    {
        var table = CreateTableWithRows(3);

        Assert.Null(table.FlashCell(0, -1, Color.Blue, TimeSpan.FromMilliseconds(200)));
        Assert.Null(table.FlashCell(0, 5, Color.Blue, TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void FlashCell_WithAnimationManager_ReturnsAnimation()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        var result = table.FlashCell(1, 0, Color.Blue, TimeSpan.FromMilliseconds(200));

        Assert.NotNull(result);
        Assert.False(result!.IsComplete);
    }

    #endregion

    #region HighlightRow

    [Fact]
    public void HighlightRow_InvalidIndex_ReturnsNull()
    {
        var table = CreateTableWithRows(3);

        Assert.Null(table.HighlightRow(-1, Color.Green, TimeSpan.FromMilliseconds(400)));
        Assert.Null(table.HighlightRow(3, Color.Green, TimeSpan.FromMilliseconds(400)));
    }

    [Fact]
    public void HighlightRow_WithAnimationManager_ReturnsAnimation()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        var result = table.HighlightRow(1, Color.Green, TimeSpan.FromMilliseconds(400));

        Assert.NotNull(result);
        Assert.False(result!.IsComplete);
    }

    #endregion

    #region AnimateRowRemoval

    [Fact]
    public void AnimateRowRemoval_InvalidIndex_ReturnsNull()
    {
        var table = CreateTableWithRows(3);

        Assert.Null(table.AnimateRowRemoval(-1, TimeSpan.FromMilliseconds(300)));
        Assert.Null(table.AnimateRowRemoval(3, TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public void AnimateRowRemoval_WithAnimationManager_ReturnsAnimation()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        var result = table.AnimateRowRemoval(1, TimeSpan.FromMilliseconds(300));

        Assert.NotNull(result);
        Assert.False(result!.IsComplete);
    }

    [Fact]
    public void AnimateRowRemoval_TracksAsActiveAnimation()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        table.AnimateRowRemoval(0, TimeSpan.FromMilliseconds(300));

        Assert.True(table.HasActiveRowAnimations);
    }

    [Fact]
    public void AnimateRowRemoval_RemovesRow_OnCompletion()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        // Remove row 1 ("R1C1", "R1C2")
        var anim = table.AnimateRowRemoval(1, TimeSpan.FromMilliseconds(300));
        Assert.NotNull(anim);
        Assert.Equal(3, table.RowCount);

        // Advance past animation duration to trigger onComplete
        AdvanceByMs(manager, 400);

        Assert.Equal(2, table.RowCount);
        Assert.Equal("R0C1", table.GetCell(0, 0));
        Assert.Equal("R2C1", table.GetCell(1, 0));
    }

    #endregion

    #region AnimateRowsRemoval

    [Fact]
    public void AnimateRowsRemoval_EmptyArray_ReturnsNull()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        var result = table.AnimateRowsRemoval(Array.Empty<int>(), TimeSpan.FromMilliseconds(300));

        Assert.Null(result);
    }

    [Fact]
    public void AnimateRowsRemoval_AllInvalid_ReturnsNull()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        var result = table.AnimateRowsRemoval(new[] { -1, 10 }, TimeSpan.FromMilliseconds(300));

        Assert.Null(result);
    }

    [Fact]
    public void AnimateRowsRemoval_WithValidIndices_ReturnsAnimation()
    {
        var table = CreateTableWithRows(5);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        var result = table.AnimateRowsRemoval(new[] { 0, 2, 4 }, TimeSpan.FromMilliseconds(300));

        Assert.NotNull(result);
        Assert.True(table.HasActiveRowAnimations);
    }

    [Fact]
    public void AnimateRowsRemoval_RemovesRows_OnCompletion()
    {
        var table = CreateTableWithRows(5);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        // Remove rows 1 and 3 ("R1C1" and "R3C1")
        var anim = table.AnimateRowsRemoval(new[] { 1, 3 }, TimeSpan.FromMilliseconds(300));
        Assert.NotNull(anim);
        Assert.Equal(5, table.RowCount);

        // Advance past animation duration to trigger onComplete
        AdvanceByMs(manager, 400);

        Assert.Equal(3, table.RowCount);
        Assert.Equal("R0C1", table.GetCell(0, 0));
        Assert.Equal("R2C1", table.GetCell(1, 0));
        Assert.Equal("R4C1", table.GetCell(2, 0));
    }

    #endregion

    #region HasActiveRowAnimations

    [Fact]
    public void HasActiveRowAnimations_InitiallyFalse()
    {
        var table = CreateTableWithRows(3);

        Assert.False(table.HasActiveRowAnimations);
    }

    [Fact]
    public void HasActiveRowAnimations_TrueWhileAnimating_FalseAfterComplete()
    {
        var table = CreateTableWithRows(3);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        table.FlashRow(0, Color.Red, TimeSpan.FromMilliseconds(100));
        Assert.True(table.HasActiveRowAnimations);

        // Advance past the animation duration
        AdvanceByMs(manager, 200);

        Assert.False(table.HasActiveRowAnimations);
    }

    [Fact]
    public void MultipleAnimations_AllTracked()
    {
        var table = CreateTableWithRows(5);
        var manager = new AnimationManager();
        table.SetAnimationManagerForTesting(manager);

        table.FlashRow(0, Color.Red, TimeSpan.FromMilliseconds(300));
        table.FlashRow(1, Color.Blue, TimeSpan.FromMilliseconds(300));
        table.HighlightRow(2, Color.Green, TimeSpan.FromMilliseconds(300));

        Assert.True(table.HasActiveRowAnimations);
    }

    #endregion

    /// <summary>
    /// Advances the animation manager in small steps to respect MaxFrameDeltaMs cap.
    /// </summary>
    private static void AdvanceByMs(AnimationManager manager, double totalMs)
    {
        var step = TimeSpan.FromMilliseconds(SharpConsoleUI.Configuration.AnimationDefaults.MaxFrameDeltaMs);
        double remaining = totalMs;
        while (remaining > 0)
        {
            double tick = Math.Min(remaining, SharpConsoleUI.Configuration.AnimationDefaults.MaxFrameDeltaMs);
            manager.Update(TimeSpan.FromMilliseconds(tick));
            remaining -= tick;
        }
    }
}
