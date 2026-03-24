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
/// Manages focus within a single <see cref="Window"/>. This is the single source of truth
/// for which control has focus. Use <see cref="SetFocus"/> to move focus programmatically,
/// <see cref="MoveFocus"/> to advance via Tab, and <see cref="HandleClick"/> to focus via mouse.
/// </summary>
public class FocusManager
{
    private readonly Window _window;

    /// <summary>Gets the currently focused control, or <c>null</c> if nothing has focus.</summary>
    public IFocusableControl? FocusedControl { get; private set; }

    /// <summary>Gets the ancestor chain from the window root to <see cref="FocusedControl"/>.</summary>
    public IReadOnlyList<IWindowControl> FocusPath { get; private set; } = Array.Empty<IWindowControl>();

    /// <summary>Fired whenever <see cref="FocusedControl"/> changes.</summary>
    public event EventHandler<FocusChangedEventArgs>? FocusChanged;

    /// <summary>Initializes a new <see cref="FocusManager"/> for the given window.</summary>
    public FocusManager(Window window)
    {
        _window = window;
    }

    /// <summary>
    /// Sets focus to <paramref name="control"/>. If the control is an <see cref="IFocusScope"/>,
    /// focus is delegated to its first focusable child. Pass <c>null</c> to clear focus.
    /// </summary>
    public void SetFocus(IFocusableControl? control, FocusReason reason)
    {
        // If the target is a focusable scope (CanReceiveFocus=true), enter it instead of
        // focusing it directly. This ensures SetFocus(panel, ...) focuses panel's first child
        // (if any), and falls back to focusing the panel itself when GetInitialFocus returns null
        // (scroll-only SPC) or returns a self-sentinel (NavigationView scroll-mode pattern).
        //
        // Transparent scopes (CanReceiveFocus=false, e.g. HGrid) are NOT entered here —
        // they are handled by the CanReceiveFocus guard below, which rejects them.
        // HGrid children are reached via MoveFocus/BuildFlatList traversal instead.
        if (control is IFocusScope scope && control.CanReceiveFocus)
        {
            var backward = false;
            var child = scope.GetInitialFocus(backward);
            if (child != null && !ReferenceEquals(child, control))
            {
                EnterOrFocus(child, backward);
                return;
            }
            // child == null, or child == control (self-sentinel) → fall through to focus scope itself
        }

        // Guard: controls must be focusable to receive direct focus
        if (control != null && !control.CanReceiveFocus) return;

        var previous = FocusedControl;
        if (ReferenceEquals(previous, control)) return;

        // Invalidate previous control's container to trigger repaint
        (previous as IWindowControl)?.Container?.Invalidate(true);

        FocusedControl = control;
        FocusPath = BuildFocusPath(control);

        // Scroll any IScrollableContainer ancestor to show the newly focused control.
        // Walk the focus path from the focused control upward; for each scrollable container,
        // scroll its direct child in the path into view.
        if (control is IWindowControl focusedWc)
        {
            var path = FocusPath;
            for (int i = path.Count - 1; i >= 1; i--)
            {
                if (path[i - 1] is IScrollableContainer scrollable)
                    scrollable.ScrollChildIntoView(path[i]);
            }
        }

        // Invalidate new control's container to trigger repaint
        (control as IWindowControl)?.Container?.Invalidate(true);

        FocusChanged?.Invoke(this, new FocusChangedEventArgs(previous, control, reason));
    }

    /// <summary>Returns <c>true</c> if <paramref name="control"/> is the currently focused control.</summary>
    public bool IsFocused(IFocusableControl control) =>
        ReferenceEquals(FocusedControl, control);

    /// <summary>Returns <c>true</c> if <paramref name="control"/> appears anywhere in <see cref="FocusPath"/>.</summary>
    public bool IsInFocusPath(IWindowControl control) =>
        FocusPath.Contains(control, ReferenceEqualityComparer.Instance);

    /// <summary>Moves focus to the next (or previous when <paramref name="backward"/> is <c>true</c>) focusable control.</summary>
    public void MoveFocus(bool backward)
    {
        if (FocusedControl == null)
        {
            SetFocus(_window.RootScope.GetInitialFocus(backward), FocusReason.Keyboard);
            return;
        }

        var scope = FindInnermostScope(FocusedControl);
        MoveWithinScope(scope, FocusedControl, backward);
    }

    /// <summary>
    /// Routes a mouse click to the nearest focusable ancestor of <paramref name="hit"/>,
    /// skipping controls that have <see cref="IMouseAwareControl.CanFocusWithMouse"/> set to <c>false</c>.
    /// </summary>
    public void HandleClick(IWindowControl? hit)
    {
        if (hit == null) return;

        IWindowControl? current = hit;
        while (current != null)
        {
            if (current is IFocusableControl focusable && focusable.CanReceiveFocus)
            {
                if (current is IMouseAwareControl { CanFocusWithMouse: false })
                {
                    current = current.Container as IWindowControl;
                    continue;
                }
                SetFocus(focusable, FocusReason.Mouse);
                return;
            }
            current = current.Container as IWindowControl;
        }
    }

    private void MoveWithinScope(IFocusScope scope, IFocusableControl current, bool backward)
    {
        var next = scope.GetNextFocus(current, backward);

        if (next != null)
        {
            EnterOrFocus(next, backward);
            return;
        }

        // Scope exhausted — save position on forward exit only (backward entry always goes to last child)
        if (!backward) scope.SavedFocus = current;
        else scope.SavedFocus = null;

        // Exit to parent scope
        if (scope is IFocusableControl scopeControl)
        {
            var parentScope = FindInnermostScope(scopeControl);
            if (!ReferenceEquals(parentScope, scope))
            {
                MoveWithinScope(parentScope, scopeControl, backward);
                return;
            }
        }

        // No parent scope — wrap at root
        var wrapped = _window.RootScope.GetInitialFocus(backward);
        if (wrapped != null)
        {
            // Clear the entire SavedFocus chain from root so re-entry starts fresh.
            ClearSavedFocusChain(_window.RootScope);
            EnterOrFocus(wrapped, backward);
        }
    }

    private static void ClearSavedFocusChain(IFocusScope? scope)
    {
        while (scope != null)
        {
            var next = scope.SavedFocus as IFocusScope;
            scope.SavedFocus = null;
            scope = next;
        }
    }

    private void EnterOrFocus(IFocusableControl target, bool backward)
    {
        if (target is IFocusScope scope)
        {
            var child = scope.GetInitialFocus(backward);
            if (child != null)
            {
                EnterOrFocus(child, backward);
                return;
            }
        }
        SetFocus(target, FocusReason.Keyboard);
    }

    // NOTE: FindInnermostScope starts at control.Container (not the control itself).
    // This is intentional: for a leaf control, we want its containing scope.
    // For a scope control that has itself been focused (e.g. panel in scroll mode),
    // we want its PARENT scope so MoveFocus exits it correctly.
    // Uses ResolveParentWindowControl to correctly handle transparent containers like
    // ColumnContainer (which skips HGrid in its Container property).
    internal IFocusScope FindInnermostScope(IWindowControl control)
    {
        IWindowControl? current = ResolveParentWindowControl(control);
        while (current != null)
        {
            if (current is IFocusScope scope) return scope;
            current = ResolveParentWindowControl(current);
        }
        return _window.RootScope;
    }

    private static IReadOnlyList<IWindowControl> BuildFocusPath(IFocusableControl? control)
    {
        if (control == null) return Array.Empty<IWindowControl>();
        var path = new List<IWindowControl>();
        IWindowControl? current = control;
        while (current != null)
        {
            path.Insert(0, current);
            current = ResolveParentWindowControl(current);
        }
        return path;
    }

    /// <summary>
    /// Resolves the next IWindowControl ancestor of a control, correctly traversing
    /// transparent containers like ColumnContainer that skip their HGrid parent.
    /// Mirrors FocusCoordinator.ResolveParentWindowControl for consistent path building.
    /// </summary>
    internal static IWindowControl? ResolveParentWindowControl(IWindowControl control)
    {
        // SplitterControl's Container is set to the HGrid's Container (skipping the HGrid).
        if (control is Controls.SplitterControl splitter && splitter.ParentGrid != null)
            return splitter.ParentGrid;

        var container = control.Container;
        if (container == null) return null;

        // ColumnContainer.Container returns the HGrid's parent (skipping the HGrid).
        // Use HorizontalGridContent to include HGrid in the path.
        if (container is Controls.ColumnContainer cc)
        {
            var hgrid = cc.HorizontalGridContent;
            if (hgrid != null)
                return hgrid;
        }

        return container as IWindowControl;
    }

    internal void RouteKey(ConsoleKeyInfo key)
    {
        (FocusedControl as IInteractiveControl)?.ProcessKey(key);
    }
}

/// <summary>Event args for <see cref="FocusManager.FocusChanged"/>.</summary>
/// <param name="Previous">The control that lost focus, or <c>null</c>.</param>
/// <param name="Current">The control that gained focus, or <c>null</c>.</param>
/// <param name="Reason">Why the focus changed.</param>
public record FocusChangedEventArgs(
    IFocusableControl? Previous,
    IFocusableControl? Current,
    FocusReason Reason
);
