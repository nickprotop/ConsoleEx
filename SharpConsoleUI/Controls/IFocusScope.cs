namespace SharpConsoleUI.Controls;

/// <summary>
/// Implemented by container controls that manage Tab navigation within themselves.
/// Replaces IDirectionalFocusControl and IFocusTrackingContainer.
/// </summary>
public interface IFocusScope
{
    /// <summary>
    /// Returns the first child to focus when Tab enters this scope.
    /// backward=true means Shift+Tab entered from the right — return last child.
    /// </summary>
    IFocusableControl? GetInitialFocus(bool backward);

    /// <summary>
    /// Returns the next child to focus after Tab from 'current'.
    /// Returns null when Tab should exit this scope.
    /// </summary>
    IFocusableControl? GetNextFocus(IFocusableControl current, bool backward);

    /// <summary>
    /// Saved focus position. FocusManager sets this before exiting the scope (if scope opts in).
    /// GetInitialFocus should return this when set, then clear it.
    /// </summary>
    IFocusableControl? SavedFocus { get; set; }
}
