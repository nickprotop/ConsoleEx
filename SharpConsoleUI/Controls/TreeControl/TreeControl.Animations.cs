// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Animation;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

public partial class TreeControl
{
    private AnimationManager? _testAnimationManager;

    /// <summary>
    /// Sets the animation manager for testing purposes (bypasses window lookup).
    /// </summary>
    internal void SetAnimationManagerForTesting(AnimationManager manager)
    {
        _testAnimationManager = manager;
    }

    /// <summary>
    /// Gets the AnimationManager from the parent window, or the test override.
    /// </summary>
    private AnimationManager? GetAnimationManager()
    {
        if (_testAnimationManager != null)
            return _testAnimationManager;

        return (this as IWindowControl).GetParentWindow()?.GetConsoleWindowSystem?.Animations;
    }

    /// <summary>
    /// Pulses a node's text foreground color between its current color and the target color.
    /// Each pulse cycle transitions normal -> color -> normal over <paramref name="pulseDuration"/>
    /// using SinePulse easing on the fractional part of the animation progress.
    /// </summary>
    /// <param name="node">The tree node to pulse. Returns null if null.</param>
    /// <param name="color">The target pulse color.</param>
    /// <param name="pulseCount">Number of complete pulse cycles.</param>
    /// <param name="pulseDuration">Duration of each individual pulse cycle.</param>
    /// <returns>The animation, or null if node is null or no AnimationManager is available.</returns>
    public IAnimation? PulseNode(TreeNode node, Color color, int pulseCount, TimeSpan pulseDuration)
    {
        if (node == null) return null;

        var manager = GetAnimationManager();
        if (manager == null) return null;

        var originalColor = node.TextColor;
        var baseColor = originalColor ?? ForegroundColor;
        var totalDuration = pulseDuration * pulseCount;

        return manager.Animate(
            from: 0f,
            to: (float)pulseCount,
            duration: totalDuration,
            easing: EasingFunctions.Linear,
            onUpdate: value =>
            {
                // Take fractional part to get position within current cycle [0,1)
                float fraction = value - MathF.Floor(value);
                // Apply SinePulse to get blend amount (0 -> 1 -> 0)
                float blend = (float)EasingFunctions.SinePulse(fraction);
                node.TextColor = ColorBlendHelper.BlendColor(baseColor, color, blend);
                Invalidate();
            },
            onComplete: () =>
            {
                node.TextColor = originalColor;
                Invalidate();
            });
    }
}
