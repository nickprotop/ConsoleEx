// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core;

/// <summary>
/// Top-level IFocusScope for a Window. Enumerates all directly focusable controls
/// (and children of IFocusableContainerWithHeader containers) for global Tab cycling.
/// Replaces Window.GetAllFocusableControlsFlattened().
/// </summary>
internal class WindowRootScope : IFocusScope
{
    private readonly Window _window;

    public IFocusableControl? SavedFocus { get; set; }

    public WindowRootScope(Window window) => _window = window;

    public IFocusableControl? GetInitialFocus(bool backward)
    {
        var controls = BuildFlatList();
        return backward ? controls.LastOrDefault() : controls.FirstOrDefault();
    }

    public IFocusableControl? GetNextFocus(IFocusableControl current, bool backward)
    {
        var controls = BuildFlatList();
        var index = controls.FindIndex(c => ReferenceEquals(c, current));

        if (index < 0)
        {
            // current is no longer in the focusable list (became disabled/invisible/removed),
            // or is a transparent scope (e.g. HGrid with CanReceiveFocus=false) exiting to root.
            // Find its former position in the full (unfiltered) all-controls list, then
            // return the nearest focusable control after (or before) that position.
            var all = BuildAllControlsList();
            var allIndex = all.FindIndex(c => ReferenceEquals(c, current));
            if (allIndex >= 0)
            {
                if (backward)
                {
                    // Search backward from former position
                    for (int i = allIndex - 1; i >= 0; i--)
                    {
                        var candidate = controls.Find(c => ReferenceEquals(c, all[i]));
                        if (candidate != null) return candidate;
                    }
                }
                else
                {
                    // Search forward, skipping descendants of the current control
                    // (avoids re-entering a transparent scope like HGrid)
                    var currentWc = current as IWindowControl;
                    for (int i = allIndex + 1; i < all.Count; i++)
                    {
                        if (currentWc != null && IsDescendantOf(all[i], currentWc))
                            continue;
                        var candidate = controls.Find(c => ReferenceEquals(c, all[i]));
                        if (candidate != null) return candidate;
                    }
                }
            }
            return GetInitialFocus(backward);
        }

        var nextIndex = backward ? index - 1 : index + 1;
        if (nextIndex < 0 || nextIndex >= controls.Count) return null; // wrap handled by FocusManager

        return controls[nextIndex];
    }

    /// <summary>
    /// Builds an unfiltered list of all potential focusable controls (including
    /// currently-disabled or invisible ones). Used to find the former position of a
    /// stale focused control when it has been removed from the focusable list.
    /// </summary>
    private List<IFocusableControl> BuildAllControlsList()
    {
        var result = new List<IFocusableControl>();
        foreach (var child in _window.GetTopLevelControls())
            CollectAllFocusable(child, result);
        return result;
    }

    private static void CollectAllFocusable(IWindowControl control, List<IFocusableControl> result)
    {
        if (control is IFocusableControl f)
            result.Add(f);
        if (control is IContainerControl container)
            foreach (var child in container.GetChildren())
                CollectAllFocusable(child, result);
    }

    /// <summary>
    /// Returns true if <paramref name="control"/> is a descendant of <paramref name="ancestor"/>
    /// using the same parent-resolution logic as FocusManager (handles ColumnContainer → HGrid).
    /// </summary>
    private static bool IsDescendantOf(IFocusableControl control, IWindowControl ancestor)
    {
        IWindowControl? current = FocusManager.ResolveParentWindowControl((IWindowControl)control);
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor)) return true;
            current = FocusManager.ResolveParentWindowControl(current);
        }
        return false;
    }

    /// <summary>
    /// Builds a flat, ordered list of Tab stops visible in the window.
    /// Rules:
    ///   - IFocusableContainerWithHeader containers: header is a stop, then active-tab
    ///     children are recursively included immediately after (container itself is not opaque).
    ///   - IFocusScope containers: opaque — added as a single stop, children NOT included.
    ///   - Non-focusable containers: transparent — recurse into visible children.
    ///   - Invisible controls are skipped.
    /// </summary>
    private List<IFocusableControl> BuildFlatList()
    {
        var result = new List<IFocusableControl>();
        foreach (var child in _window.GetTopLevelControls())
            CollectFocusable(child, result);
        return result;
    }

    private static void CollectFocusable(IWindowControl control, List<IFocusableControl> result)
    {
        if (!control.Visible) return;

        if (control is IFocusableContainerWithHeader)
        {
            // Header itself is a Tab stop
            if (control is IFocusableControl headerFocusable && headerFocusable.CanReceiveFocus)
                result.Add(headerFocusable);
            // Recurse into active tab content — visible children only
            if (control is IContainerControl container)
                foreach (var child in container.GetChildren())
                    CollectFocusable(child, result);
            return;
        }

        if (control is IFocusScope && control is IFocusableControl scopeFocusable)
        {
            if (scopeFocusable.CanReceiveFocus)
            {
                // Opaque scope: add only the container itself as a single Tab stop
                result.Add(scopeFocusable);
                return;
            }
            // CanReceiveFocus=false scope (e.g. HGrid): fall through to transparent container path
        }

        if (control is IFocusableControl focusable && focusable.CanReceiveFocus)
        {
            result.Add(focusable);
            return;
        }

        // Transparent container: recurse into children
        if (control is IContainerControl transparentContainer)
            foreach (var child in transparentContainer.GetChildren())
                CollectFocusable(child, result);
    }
}
