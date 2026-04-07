using SharpConsoleUI.Animation;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests for TreeControl node pulse animation.
/// </summary>
public class TreeAnimationTests
{
    private static TreeControl CreateTreeWithNodes()
    {
        var tree = new TreeControl();
        var root = tree.AddRootNode("Root");
        root.AddChild("Child1");
        root.AddChild("Child2");
        return tree;
    }

    #region PulseNode

    [Fact]
    public void PulseNode_NullNode_ReturnsNull()
    {
        var tree = CreateTreeWithNodes();

        var result = tree.PulseNode(null!, Color.Red, 3, TimeSpan.FromMilliseconds(200));

        Assert.Null(result);
    }

    [Fact]
    public void PulseNode_NoAnimationManager_ReturnsNull()
    {
        var tree = CreateTreeWithNodes();
        var node = tree.RootNodes[0];

        var result = tree.PulseNode(node, Color.Red, 3, TimeSpan.FromMilliseconds(200));

        Assert.Null(result);
    }

    [Fact]
    public void PulseNode_WithAnimationManager_ReturnsAnimation()
    {
        var tree = CreateTreeWithNodes();
        var manager = new AnimationManager();
        tree.SetAnimationManagerForTesting(manager);
        var node = tree.RootNodes[0];

        var result = tree.PulseNode(node, Color.Red, 3, TimeSpan.FromMilliseconds(200));

        Assert.NotNull(result);
        Assert.False(result!.IsComplete);
    }

    [Fact]
    public void PulseNode_RestoresOriginalColor_OnComplete()
    {
        var tree = CreateTreeWithNodes();
        var manager = new AnimationManager();
        tree.SetAnimationManagerForTesting(manager);
        var node = tree.RootNodes[0];
        var originalColor = new Color(100, 150, 200);
        node.TextColor = originalColor;

        tree.PulseNode(node, Color.Red, 2, TimeSpan.FromMilliseconds(100));

        // Advance past total duration (pulseCount * pulseDuration = 200ms)
        AdvanceByMs(manager, 300);

        Assert.Equal(originalColor, node.TextColor);
    }

    [Fact]
    public void PulseNode_RestoresNullColor_WhenOriginallyNull()
    {
        var tree = CreateTreeWithNodes();
        var manager = new AnimationManager();
        tree.SetAnimationManagerForTesting(manager);
        var node = tree.RootNodes[0];
        node.TextColor = null;

        tree.PulseNode(node, Color.Red, 1, TimeSpan.FromMilliseconds(100));

        // Advance past total duration
        AdvanceByMs(manager, 200);

        Assert.Null(node.TextColor);
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
