// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Animation;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls;

public partial class HorizontalGridControl
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
    /// Animates a column's width from its current value to <paramref name="targetWidth"/> over
    /// the specified <paramref name="duration"/> using an integer tween.
    /// </summary>
    /// <param name="columnIndex">Zero-based index of the column to animate.</param>
    /// <param name="targetWidth">The desired final width.</param>
    /// <param name="duration">How long the transition should take.</param>
    /// <param name="easing">Easing function. Defaults to <see cref="EasingFunctions.EaseOut"/>.</param>
    /// <returns>
    /// An <see cref="IAnimation"/> handle for the running animation, or <c>null</c> if the
    /// column index is invalid or no <see cref="AnimationManager"/> is available (in which
    /// case the width is set immediately).
    /// </returns>
    public IAnimation? AnimateColumnWidth(int columnIndex, int targetWidth, TimeSpan duration, EasingFunction? easing = null)
    {
        ColumnContainer? column;
        int currentWidth;

        lock (_gridLock)
        {
            if (columnIndex < 0 || columnIndex >= _columns.Count)
                return null;

            column = _columns[columnIndex];
            currentWidth = column.Width ?? 0;
        }

        // When animating from 0 (hidden), make visible before starting
        if (currentWidth == 0)
            column.Visible = true;

        var manager = GetAnimationManager();
        if (manager == null)
        {
            // No animation manager — apply immediately
            column.Width = targetWidth;
            if (targetWidth == 0)
                column.Visible = false;
            return null;
        }

        return manager.Animate(
            from: currentWidth,
            to: targetWidth,
            duration: duration,
            easing: easing ?? EasingFunctions.EaseOut,
            onUpdate: width =>
            {
                column.Width = width;
                Invalidate();
            },
            onComplete: () =>
            {
                if (targetWidth == 0)
                    column.Visible = false;
                Invalidate();
            });
    }
}
