// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Investigation repro for the "rebuild-while-focused bounces to nav" bug.
///
/// Scenario: a focusable <see cref="TableControl"/> lives inside a
/// <see cref="NavigationView"/>'s content panel. The table has keyboard focus.
/// Something re-builds the table's rows on a poll loop. The downstream app
/// (LazyCaddy) observed focus jumping from the table back to the nav sidebar
/// (<c>_navScrollPanel</c>) on every tick.
///
/// These tests pin down WHICH rebuild path actually moves focus.
/// </summary>
public class NavViewFocusReclaimTests
{
    /// <summary>
    /// Builds a window containing a NavigationView with one nav item whose content
    /// panel holds a focusable TableControl with rows. Returns the pieces under test.
    /// A render pass is driven so the panel viewport is laid out (focus + scroll
    /// resolution depends on a non-zero viewport).
    /// </summary>
    private static (ConsoleWindowSystem system, Window window, NavigationView nav, TableControl table)
        BuildNavWithFocusedTable()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem(100, 30);
        var window = new Window(system)
        {
            Title = "Test", Left = 0, Top = 0, Width = 100, Height = 30
        };

        var nav = new NavigationView();
        var table = new TableControl();

        var item = nav.AddItem("Item 1");
        nav.SetItemContent(item, panel =>
        {
            panel.AddControl(table);
        });

        window.AddControl(nav);
        system.AddWindow(window);

        // Select the item so the content factory populates the panel with the table.
        nav.SelectedIndex = 0;

        // Give the table some rows.
        table.AddColumn("Col");
        table.AddRow("a");
        table.AddRow("b");
        table.AddRow("c");

        // Lay everything out (viewport must be non-zero for focus/scroll resolution).
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        return (system, window, nav, table);
    }

    /// <summary>
    /// Path A — the path the bug report describes literally:
    /// table holds focus, then <c>table.ClearRows()</c> + re-add rows, which ends
    /// in <c>Container?.Invalidate(true)</c>. A render pass is driven afterward so
    /// the invalidation is fully processed.
    ///
    /// Asserts focus STAYS on the table. If this fails (focus == nav scroll panel),
    /// the reclaim is caused by the row-rebuild → invalidate → relayout path.
    /// </summary>
    [Fact]
    public void ClearRowsAndReAdd_WhileTableFocused_KeepsFocusOnTable()
    {
        var (system, window, nav, table) = BuildNavWithFocusedTable();

        // Focus the table directly.
        window.FocusManager.SetFocus(table, FocusReason.Programmatic);
        Assert.True(window.FocusManager.IsFocused(table),
            "precondition: table should hold focus before the rebuild");

        // Simulate the 5-second poll loop: clear + re-add rows.
        table.ClearRows();
        table.AddRow("a2");
        table.AddRow("b2");
        table.AddRow("c2");

        // Drive the relayout/repaint the invalidate scheduled.
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        Assert.True(window.FocusManager.IsFocused(table),
            "after ClearRows()+re-add, focus should stay on the table, " +
            $"but FocusedControl = {window.FocusManager.FocusedControl?.GetType().Name}");
        Assert.False(nav.NavScrollPanel.HasFocus,
            "nav scroll panel should NOT have reclaimed focus");
    }

    /// <summary>
    /// Path B — re-running the nav content factory (what happens if the app rebuilds
    /// by re-selecting / re-populating the item, NOT just the table rows).
    /// This goes through <c>ScrollablePanelControl.ClearContents()</c>, which calls
    /// <c>SetFocus(_contentPanel, Programmatic)</c> when the panel is in the focus path.
    ///
    /// Asserts what focus lands on after the content rebuild.
    /// </summary>
    [Fact]
    public void RePopulateContent_WhileTableFocused_FocusOutcome()
    {
        var (system, window, nav, table) = BuildNavWithFocusedTable();

        window.FocusManager.SetFocus(table, FocusReason.Programmatic);
        Assert.True(window.FocusManager.IsFocused(table),
            "precondition: table should hold focus before the rebuild");

        // Re-populate the content panel (clears the old table, adds a fresh one).
        var newTable = new TableControl();
        nav.SetItemContent(nav.SelectedItem!, panel => panel.AddControl(newTable));
        newTable.AddColumn("Col");
        newTable.AddRow("x");

        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        // Record outcome — this assertion documents observed behavior.
        Assert.False(nav.NavScrollPanel.HasFocus,
            "nav scroll panel should NOT have reclaimed focus after content re-populate, " +
            $"but FocusedControl = {window.FocusManager.FocusedControl?.GetType().Name}");
    }

    /// <summary>
    /// Path C — table is focused via the NAV FLOW (Tab into content panel),
    /// matching how a real user reaches the table, rather than a direct
    /// <c>SetFocus(table)</c>. Then the table rebuilds its rows.
    ///
    /// Captures the full focus trajectory so we can see exactly where it lands.
    /// </summary>
    [Fact]
    public void NavFlowFocus_ThenClearRows_FocusTrajectory()
    {
        var (system, window, nav, table) = BuildNavWithFocusedTable();

        // Reach the table the way a user would: enter the scope (nav pane first),
        // then Tab forward into the content panel, then into the table.
        window.FocusManager.SetFocus(null, FocusReason.Programmatic);
        window.FocusManager.SetFocus(nav, FocusReason.Keyboard); // enters nav pane
        var afterEnter = window.FocusManager.FocusedControl?.GetType().Name;

        window.FocusManager.MoveFocus(false); // nav -> (content panel) -> table
        var afterTab1 = window.FocusManager.FocusedControl?.GetType().Name;
        var afterTab2 = "(not tabbed)";

        var beforeRebuild = window.FocusManager.FocusedControl?.GetType().Name;

        table.ClearRows();
        table.AddRow("a2");
        table.AddRow("b2");
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        var afterRebuild = window.FocusManager.FocusedControl?.GetType().Name;

        // Surface the whole trajectory in the failure message.
        Assert.True(
            ReferenceEquals(window.FocusManager.FocusedControl, table),
            $"Focus trajectory: enter={afterEnter} tab1={afterTab1} tab2={afterTab2} " +
            $"beforeRebuild={beforeRebuild} afterRebuild={afterRebuild}. " +
            $"navHasFocus={nav.NavScrollPanel.HasFocus}");
    }

    /// <summary>
    /// Path D — simulate the 5-second poll loop literally: on each tick, re-run the
    /// nav content factory (ClearContents + add a fresh table), exactly as a "rebuild
    /// the focused table" handler that re-populates content would. Records the focused
    /// control type after each of several ticks to see whether focus drifts to the nav.
    /// </summary>
    [Fact]
    public void RepeatedContentRebuild_PollLoop_DoesNotDriftToNav()
    {
        var (system, window, nav, _) = BuildNavWithFocusedTable();

        // Land focus inside the content panel the way the nav flow does.
        window.FocusManager.SetFocus(null, FocusReason.Programmatic);
        window.FocusManager.SetFocus(nav, FocusReason.Keyboard); // nav pane
        window.FocusManager.MoveFocus(false);                    // -> table (via content panel)

        var trail = new List<string>();
        trail.Add($"t0={window.FocusManager.FocusedControl?.GetType().Name} navFocus={nav.NavScrollPanel.HasFocus}");

        for (int tick = 1; tick <= 4; tick++)
        {
            var freshTable = new TableControl();
            nav.SetItemContent(nav.SelectedItem!, panel => panel.AddControl(freshTable));
            freshTable.AddColumn("Col");
            freshTable.AddRow($"row{tick}");

            system.Render.UpdateDisplay();
            system.Render.UpdateDisplay();

            trail.Add($"t{tick}={window.FocusManager.FocusedControl?.GetType().Name} navFocus={nav.NavScrollPanel.HasFocus}");
        }

        Assert.False(nav.NavScrollPanel.HasFocus,
            "after repeated content rebuilds, focus drifted to the nav scroll panel. " +
            "Trail: " + string.Join(" | ", trail));
    }

    /// <summary>
    /// Path E — content panel itself holds focus in SCROLL MODE (the state
    /// <c>FocusContentPanel()</c> leaves it in: <c>FocusedControl == _contentPanel</c>,
    /// not a child). The table inside then rebuilds its rows. We then simulate the
    /// next focus traversal (any key that advances focus) and record where it lands.
    ///
    /// This is the configuration most likely to bounce: the content panel is the
    /// focused control, and after the rows shrink the panel may no longer report
    /// scrollable content.
    /// </summary>
    [Fact]
    public void ContentPanelScrollModeFocused_ThenRebuildAndTraverse_FocusTrajectory()
    {
        var (system, window, nav, table) = BuildNavWithFocusedTable();

        // Put the content panel into scroll-mode self-focus, the way the nav flow does.
        nav.ContentPanel.SavedFocus = nav.ContentPanel;
        window.FocusManager.SetFocus(nav.ContentPanel, FocusReason.Keyboard);
        var focusedNow = window.FocusManager.FocusedControl;
        var step0 = focusedNow?.GetType().Name;

        // Rebuild the table rows (poll tick).
        table.ClearRows();
        table.AddRow("a2");
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();
        var afterRebuild = window.FocusManager.FocusedControl?.GetType().Name;
        var afterRebuildNav = nav.NavScrollPanel.HasFocus;

        // Simulate the next focus advance (e.g. a Tab the panel doesn't consume).
        window.FocusManager.MoveFocus(false);
        var afterTraverse = window.FocusManager.FocusedControl?.GetType().Name;
        var afterTraverseNav = nav.NavScrollPanel.HasFocus;

        Assert.False(afterRebuildNav,
            $"focus bounced to nav on rebuild. step0={step0} afterRebuild={afterRebuild} " +
            $"afterTraverse={afterTraverse} traverseNav={afterTraverseNav}");
    }

    /// <summary>
    /// Root-cause regression test (Option 1 fix).
    ///
    /// When the content panel itself holds focus in scroll mode
    /// (<c>FocusedControl == _contentPanel</c>) and it still owns an unvisited focusable
    /// child (the table), a forward focus traversal must DESCEND into that child rather
    /// than wrapping to the nav pane. Previously <c>FocusManager.MoveFocus</c> resolved the
    /// content panel's PARENT scope (the NavigationView) and asked it for the next stop,
    /// which is hard-wired to wrap content → nav — skipping the table entirely.
    /// </summary>
    [Fact]
    public void MoveFocus_FromScrollModeContentPanel_DescendsIntoTable_NotNav()
    {
        var (system, window, nav, table) = BuildNavWithFocusedTable();

        // Content panel focused in scroll mode (the state FocusContentPanel() leaves it in).
        nav.ContentPanel.SavedFocus = nav.ContentPanel;
        window.FocusManager.SetFocus(nav.ContentPanel, FocusReason.Keyboard);
        Assert.True(ReferenceEquals(window.FocusManager.FocusedControl, nav.ContentPanel),
            "precondition: content panel should be the focused control (scroll mode)");

        // Forward traversal should enter the panel's child (the table), not bounce to nav.
        window.FocusManager.MoveFocus(false);

        Assert.True(ReferenceEquals(window.FocusManager.FocusedControl, table),
            "MoveFocus from a scroll-mode-focused content panel should focus the table " +
            $"(its unvisited focusable child), but FocusedControl = " +
            $"{window.FocusManager.FocusedControl?.GetType().Name}");
        Assert.False(nav.NavScrollPanel.HasFocus,
            "nav scroll panel should NOT have been focused");
    }

    /// <summary>
    /// Guard: the descend-into-scope fix is FORWARD-ONLY. Shift+Tab (backward) from a
    /// scroll-mode-focused content panel must still EXIT the panel to the nav pane, by
    /// design (a scroll-mode panel is entered forward and exited backward).
    /// </summary>
    [Fact]
    public void MoveFocus_Backward_FromScrollModeContentPanel_ExitsToNav_NotIntoTable()
    {
        var (system, window, nav, table) = BuildNavWithFocusedTable();

        nav.ContentPanel.SavedFocus = nav.ContentPanel;
        window.FocusManager.SetFocus(nav.ContentPanel, FocusReason.Keyboard);
        Assert.True(ReferenceEquals(window.FocusManager.FocusedControl, nav.ContentPanel),
            "precondition: content panel should be the focused control (scroll mode)");

        // Backward traversal should exit to the nav pane, NOT descend into the table.
        window.FocusManager.MoveFocus(true);

        Assert.False(ReferenceEquals(window.FocusManager.FocusedControl, table),
            "Shift+Tab from a scroll-mode content panel should not descend into the table");
        Assert.True(nav.NavScrollPanel.HasFocus,
            "Shift+Tab from the content panel should focus the nav scroll panel, but " +
            $"FocusedControl = {window.FocusManager.FocusedControl?.GetType().Name}");
    }
}
