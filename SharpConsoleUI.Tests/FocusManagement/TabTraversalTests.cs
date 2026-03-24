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
/// Comprehensive Tab (and Shift+Tab) focus traversal tests for HorizontalGridControl,
/// ScrollablePanelControl, and various nesting combinations.
///
/// Navigation model:
///   - window.FocusManager.MoveFocus(false/true): used when controls are directly reachable
///     from the window root scope flat list (transparent containers like HGrid, or top-level SPCs).
///   - spc.ProcessKey(TabKey): used when Tab is handled by the SPC's own scope
///     (e.g. SPC wrapping HGrid columns — the SPC routes Tab to its focusable scope children).
///
/// Each test starts from the auto-focused initial state (window.AddControl triggers auto-focus).
/// </summary>
public class TabTraversalTests
{
    private static readonly ConsoleKeyInfo TabKey = new('\t', ConsoleKey.Tab, false, false, false);
    private static readonly ConsoleKeyInfo ShiftTabKey = new('\t', ConsoleKey.Tab, true, false, false);

    // ───────────────────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────────────────

    private static (Window window, ConsoleWindowSystem system) CreateWindow(int width = 80, int height = 25)
    {
        var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
        var window = new Window(system) { Width = width, Height = height };
        return (window, system);
    }

    /// <summary>Tab via FocusManager.MoveFocus (works for window-level and transparent-container controls).</summary>
    private static void Tab(Window w) => w.FocusManager.MoveFocus(backward: false);

    /// <summary>Shift+Tab via FocusManager.MoveFocus.</summary>
    private static void ShiftTab(Window w) => w.FocusManager.MoveFocus(backward: true);

    /// <summary>Tab via a container's own ProcessKey (used when buttons are inside SPC→HGrid).</summary>
    private static bool TabVia(IInteractiveControl container) => container.ProcessKey(TabKey);

    /// <summary>Shift+Tab via a container's own ProcessKey.</summary>
    private static bool ShiftTabVia(IInteractiveControl container) => container.ProcessKey(ShiftTabKey);

    private static void AssertFocused(Window w, IFocusableControl expected, string step) =>
        Assert.True(w.FocusManager.IsFocused(expected),
            $"{step}: expected {expected.GetType().Name} '{(expected as ButtonControl)?.Text ?? ""}' to be focused, " +
            $"but focused is {w.FocusManager.FocusedControl?.GetType().Name ?? "null"} " +
            $"'{(w.FocusManager.FocusedControl as ButtonControl)?.Text ?? ""}'");

    private static HorizontalGridControl MakeGrid(params ButtonControl[] buttons)
    {
        var grid = new HorizontalGridControl();
        foreach (var btn in buttons)
        {
            var col = new ColumnContainer(grid);
            col.AddContent(btn);
            grid.AddColumn(col);
        }
        return grid;
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 1. Flat HGrid at window level — 2 buttons, no splitter
    //    After AddControl: auto-focus lands on btn1
    //    MoveFocus works because HGrid is transparent (CanReceiveFocus=false).
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HGrid_TwoButtons_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        w.AddControl(MakeGrid(b1, b2));

        // AddControl triggers auto-focus onto the first focusable in the grid
        AssertFocused(w, b1, "Auto→b1");

        Tab(w); AssertFocused(w, b2, "Tab1→b2");
        Tab(w); AssertFocused(w, b1, "Tab2 wraps→b1");
        Tab(w); AssertFocused(w, b2, "Tab3→b2");
        Tab(w); AssertFocused(w, b1, "Tab4 wraps→b1");
    }

    [Fact]
    public void HGrid_TwoButtons_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        w.AddControl(MakeGrid(b1, b2));

        AssertFocused(w, b1, "Auto→b1");

        ShiftTab(w); AssertFocused(w, b2, "ShiftTab1 wraps→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab2→b1");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab3 wraps→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab4→b1");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 2. Flat HGrid at window level — 3 buttons
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HGrid_ThreeButtons_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        w.AddControl(MakeGrid(b1, b2, b3));

        AssertFocused(w, b1, "Auto→b1");
        Tab(w); AssertFocused(w, b2, "Tab1→b2");
        Tab(w); AssertFocused(w, b3, "Tab2→b3");
        Tab(w); AssertFocused(w, b1, "Tab3 wraps→b1");
        Tab(w); AssertFocused(w, b2, "Tab4→b2");
        Tab(w); AssertFocused(w, b3, "Tab5→b3");
        Tab(w); AssertFocused(w, b1, "Tab6 wraps→b1");
    }

    [Fact]
    public void HGrid_ThreeButtons_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        w.AddControl(MakeGrid(b1, b2, b3));

        AssertFocused(w, b1, "Auto→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab1 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab2→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab3→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab4 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab5→b2");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 3. HGrid with splitter between two columns
    //    Splitter container is the HGrid (IWindowControl+IFocusScope), so
    //    FindInnermostScope(splitter) returns HGrid, not RootScope.
    //    The flat list is [btn1, splitter, btn2] because HGrid is transparent.
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HGrid_WithSplitter_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var grid = new HorizontalGridControl();
        var col1 = new ColumnContainer(grid);
        var col2 = new ColumnContainer(grid);
        col1.AddContent(b1);
        col2.AddContent(b2);
        grid.AddColumn(col1);
        var splitter = grid.AddColumnWithSplitter(col2)!;
        w.AddControl(grid);

        AssertFocused(w, b1, "Auto→b1");
        Tab(w); AssertFocused(w, splitter, "Tab1→splitter");
        Tab(w); AssertFocused(w, b2, "Tab2→b2");
        Tab(w); AssertFocused(w, b1, "Tab3 wraps→b1");
        Tab(w); AssertFocused(w, splitter, "Tab4→splitter");
        Tab(w); AssertFocused(w, b2, "Tab5→b2");
    }

    [Fact]
    public void HGrid_WithSplitter_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var grid = new HorizontalGridControl();
        var col1 = new ColumnContainer(grid);
        var col2 = new ColumnContainer(grid);
        col1.AddContent(b1);
        col2.AddContent(b2);
        grid.AddColumn(col1);
        var splitter = grid.AddColumnWithSplitter(col2)!;
        w.AddControl(grid);

        AssertFocused(w, b1, "Auto→b1");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab1 wraps→b2");
        ShiftTab(w); AssertFocused(w, splitter, "ShiftTab2→splitter");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab3→b1");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab4 wraps→b2");
        ShiftTab(w); AssertFocused(w, splitter, "ShiftTab5→splitter");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 4. HGrid with 3 columns and 2 splitters
    //    col1[b1] | splitter1 | col2[b2] | splitter2 | col3[b3]
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HGrid_ThreeColumns_TwoSplitters_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        var grid = new HorizontalGridControl();
        var col1 = new ColumnContainer(grid);
        var col2 = new ColumnContainer(grid);
        var col3 = new ColumnContainer(grid);
        col1.AddContent(b1);
        col2.AddContent(b2);
        col3.AddContent(b3);
        grid.AddColumn(col1);
        var splitter1 = grid.AddColumnWithSplitter(col2)!;
        var splitter2 = grid.AddColumnWithSplitter(col3)!;
        w.AddControl(grid);

        AssertFocused(w, b1, "Auto→b1");
        Tab(w); AssertFocused(w, splitter1, "Tab1→splitter1");
        Tab(w); AssertFocused(w, b2, "Tab2→b2");
        Tab(w); AssertFocused(w, splitter2, "Tab3→splitter2");
        Tab(w); AssertFocused(w, b3, "Tab4→b3");
        Tab(w); AssertFocused(w, b1, "Tab5 wraps→b1");
        Tab(w); AssertFocused(w, splitter1, "Tab6→splitter1");
        Tab(w); AssertFocused(w, b2, "Tab7→b2");
    }

    [Fact]
    public void HGrid_ThreeColumns_TwoSplitters_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        var grid = new HorizontalGridControl();
        var col1 = new ColumnContainer(grid);
        var col2 = new ColumnContainer(grid);
        var col3 = new ColumnContainer(grid);
        col1.AddContent(b1);
        col2.AddContent(b2);
        col3.AddContent(b3);
        grid.AddColumn(col1);
        var splitter1 = grid.AddColumnWithSplitter(col2)!;
        var splitter2 = grid.AddColumnWithSplitter(col3)!;
        w.AddControl(grid);

        AssertFocused(w, b1, "Auto→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab1 wraps→b3");
        ShiftTab(w); AssertFocused(w, splitter2, "ShiftTab2→splitter2");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab3→b2");
        ShiftTab(w); AssertFocused(w, splitter1, "ShiftTab4→splitter1");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab5→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab6 wraps→b3");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 5. Multiple top-level controls at window level (HGrid, Button)
    //    window[grid[b1,b2], b3]
    //    HGrid is transparent so b1,b2 appear individually in flat list.
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Window_HGridAndButton_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        w.AddControl(MakeGrid(b1, b2));
        w.AddControl(b3);

        AssertFocused(w, b1, "Auto→b1");
        Tab(w); AssertFocused(w, b2, "Tab1→b2");
        Tab(w); AssertFocused(w, b3, "Tab2→b3");
        Tab(w); AssertFocused(w, b1, "Tab3 wraps→b1");
        Tab(w); AssertFocused(w, b2, "Tab4→b2");
        Tab(w); AssertFocused(w, b3, "Tab5→b3");
    }

    [Fact]
    public void Window_HGridAndButton_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        w.AddControl(MakeGrid(b1, b2));
        w.AddControl(b3);

        AssertFocused(w, b1, "Auto→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab1 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab2→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab3→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab4 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab5→b2");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 6. window[b1, grid[b2, b3], b4]
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Window_ButtonHGridButton_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        var b4 = new ButtonControl { Text = "B4" };
        w.AddControl(b1);
        w.AddControl(MakeGrid(b2, b3));
        w.AddControl(b4);

        AssertFocused(w, b1, "Auto→b1");
        Tab(w); AssertFocused(w, b2, "Tab1→b2");
        Tab(w); AssertFocused(w, b3, "Tab2→b3");
        Tab(w); AssertFocused(w, b4, "Tab3→b4");
        Tab(w); AssertFocused(w, b1, "Tab4 wraps→b1");
        Tab(w); AssertFocused(w, b2, "Tab5→b2");
        Tab(w); AssertFocused(w, b3, "Tab6→b3");
        Tab(w); AssertFocused(w, b4, "Tab7→b4");
    }

    [Fact]
    public void Window_ButtonHGridButton_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        var b4 = new ButtonControl { Text = "B4" };
        w.AddControl(b1);
        w.AddControl(MakeGrid(b2, b3));
        w.AddControl(b4);

        AssertFocused(w, b1, "Auto→b1");
        ShiftTab(w); AssertFocused(w, b4, "ShiftTab1 wraps→b4");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab2→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab3→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab4→b1");
        ShiftTab(w); AssertFocused(w, b4, "ShiftTab5 wraps→b4");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab6→b3");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 7. Two HGrids at window level
    //    window[grid1[b1,b2], grid2[b3,b4]]
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Window_TwoHGrids_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        var b4 = new ButtonControl { Text = "B4" };
        w.AddControl(MakeGrid(b1, b2));
        w.AddControl(MakeGrid(b3, b4));

        AssertFocused(w, b1, "Auto→b1");
        Tab(w); AssertFocused(w, b2, "Tab1→b2");
        Tab(w); AssertFocused(w, b3, "Tab2→b3");
        Tab(w); AssertFocused(w, b4, "Tab3→b4");
        Tab(w); AssertFocused(w, b1, "Tab4 wraps→b1");
        Tab(w); AssertFocused(w, b2, "Tab5→b2");
        Tab(w); AssertFocused(w, b3, "Tab6→b3");
        Tab(w); AssertFocused(w, b4, "Tab7→b4");
    }

    [Fact]
    public void Window_TwoHGrids_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        var b4 = new ButtonControl { Text = "B4" };
        w.AddControl(MakeGrid(b1, b2));
        w.AddControl(MakeGrid(b3, b4));

        AssertFocused(w, b1, "Auto→b1");
        ShiftTab(w); AssertFocused(w, b4, "ShiftTab1 wraps→b4");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab2→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab3→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab4→b1");
        ShiftTab(w); AssertFocused(w, b4, "ShiftTab5 wraps→b4");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab6→b3");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 8. SPC at window level with direct button children (no HGrid)
    //    window[SPC[b1, b2, b3]]
    //    btn.Container = SPC (IWindowControl+IFocusScope) so MoveFocus uses SPC scope.
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SPC_DirectButtons_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var panel = new ScrollablePanelControl { Height = 10 };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        panel.AddControl(b1);
        panel.AddControl(b2);
        panel.AddControl(b3);
        w.AddControl(panel);

        AssertFocused(w, b1, "Auto→b1");
        Tab(w); AssertFocused(w, b2, "Tab1→b2");
        Tab(w); AssertFocused(w, b3, "Tab2→b3");
        Tab(w); AssertFocused(w, b1, "Tab3 wraps→b1");
        Tab(w); AssertFocused(w, b2, "Tab4→b2");
        Tab(w); AssertFocused(w, b3, "Tab5→b3");
    }

    [Fact]
    public void SPC_DirectButtons_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var panel = new ScrollablePanelControl { Height = 10 };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        panel.AddControl(b1);
        panel.AddControl(b2);
        panel.AddControl(b3);
        w.AddControl(panel);

        AssertFocused(w, b1, "Auto→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab1 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab2→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab3→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab4 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab5→b2");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 9. HGrid columns each containing an SPC
    //    window[HGrid[col1[SPC1[b1,b2]], col2[SPC2[b3,b4]]]]
    //    SPC1/SPC2 are in the flat list (opaque scopes inside transparent HGrid).
    //    Navigation between SPCs uses RootScope; navigation within each SPC uses SPC's scope.
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HGrid_TwoSPCColumns_Forward_CrossColumnTraversal()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        var b4 = new ButtonControl { Text = "B4" };

        var spc1 = new ScrollablePanelControl { Height = 10 };
        spc1.AddControl(b1);
        spc1.AddControl(b2);
        var spc2 = new ScrollablePanelControl { Height = 10 };
        spc2.AddControl(b3);
        spc2.AddControl(b4);

        var grid = new HorizontalGridControl();
        var col1 = new ColumnContainer(grid);
        var col2 = new ColumnContainer(grid);
        col1.AddContent(spc1);
        col2.AddContent(spc2);
        grid.AddColumn(col1);
        grid.AddColumn(col2);
        w.AddControl(grid);

        // Auto-focus enters spc1 → b1
        AssertFocused(w, b1, "Auto→b1");

        // Within spc1: b1→b2 (spc1 scope via FindInnermostScope)
        Tab(w); AssertFocused(w, b2, "Tab1→b2");

        // Exit spc1 → spc2 → b3 (spc1 exhausted, RootScope moves to spc2)
        Tab(w); AssertFocused(w, b3, "Tab2→b3");

        // Within spc2: b3→b4
        Tab(w); AssertFocused(w, b4, "Tab3→b4");

        // Exit spc2 → wrap to spc1 → restores spc1.SavedFocus=b2
        Tab(w); AssertFocused(w, b2, "Tab4 wraps→b2 (saved)");

        // From b2 (last in spc1) → exit spc1 → spc2 → restores spc2.SavedFocus=b4
        Tab(w); AssertFocused(w, b4, "Tab5→b4 (saved)");
    }

    [Fact]
    public void HGrid_TwoSPCColumns_Backward_CrossColumnTraversal()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        var b4 = new ButtonControl { Text = "B4" };

        var spc1 = new ScrollablePanelControl { Height = 10 };
        spc1.AddControl(b1);
        spc1.AddControl(b2);
        var spc2 = new ScrollablePanelControl { Height = 10 };
        spc2.AddControl(b3);
        spc2.AddControl(b4);

        var grid = new HorizontalGridControl();
        var col1 = new ColumnContainer(grid);
        var col2 = new ColumnContainer(grid);
        col1.AddContent(spc1);
        col2.AddContent(spc2);
        grid.AddColumn(col1);
        grid.AddColumn(col2);
        w.AddControl(grid);

        AssertFocused(w, b1, "Auto→b1");

        // ShiftTab from b1 (first in spc1) → wraps: RootScope wraps to spc2 → enters last child b4
        ShiftTab(w); AssertFocused(w, b4, "ShiftTab1 wraps→b4");

        // Within spc2 backward: b4→b3
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab2→b3");

        // Exit spc2 backward → spc1 → enters last child b2
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab3→b2");

        // Within spc1 backward: b2→b1
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab4→b1");

        // Exit spc1 backward → spc2 → restores saved b3 or enters last b4
        ShiftTab(w); Assert.True(
            w.FocusManager.IsFocused(b4) || w.FocusManager.IsFocused(b3),
            "ShiftTab5: should be in spc2");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 10. SPC wrapping HGrid — Tab via FocusManager.MoveFocus
    //     window[SPC[HGrid[b1, b2, b3]]]
    //     In the new architecture, Tab is always routed through FocusManager.MoveFocus.
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SPC_WrapsHGrid_TabViaProcessKey_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var panel = new ScrollablePanelControl { Height = 15 };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        panel.AddControl(MakeGrid(b1, b2, b3));
        w.AddControl(panel);

        AssertFocused(w, b1, "Auto→b1");

        Tab(w); AssertFocused(w, b2, "Tab1→b2");
        Tab(w); AssertFocused(w, b3, "Tab2→b3");
        Tab(w); AssertFocused(w, b1, "Tab3 wraps→b1");
        Tab(w); AssertFocused(w, b2, "Tab4→b2");
        Tab(w); AssertFocused(w, b3, "Tab5→b3");
        Tab(w); AssertFocused(w, b1, "Tab6 wraps→b1");
    }

    [Fact]
    public void SPC_WrapsHGrid_TabViaProcessKey_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var panel = new ScrollablePanelControl { Height = 15 };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        panel.AddControl(MakeGrid(b1, b2, b3));
        w.AddControl(panel);

        AssertFocused(w, b1, "Auto→b1");

        ShiftTab(w); AssertFocused(w, b3, "ShiftTab1 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab2→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab3→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab4 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab5→b2");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 11. SPC wrapping HGrid with splitter — Tab via FocusManager.MoveFocus
    //     window[SPC[HGrid[b1 | splitter | b2]]]
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SPC_WrapsHGridWithSplitter_TabViaProcessKey_Forward()
    {
        var (w, _) = CreateWindow();
        var panel = new ScrollablePanelControl { Height = 10 };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var grid = new HorizontalGridControl();
        var col1 = new ColumnContainer(grid);
        var col2 = new ColumnContainer(grid);
        col1.AddContent(b1);
        col2.AddContent(b2);
        grid.AddColumn(col1);
        var splitter = grid.AddColumnWithSplitter(col2)!;
        panel.AddControl(grid);
        w.AddControl(panel);

        AssertFocused(w, b1, "Auto→b1");

        Tab(w); AssertFocused(w, splitter, "Tab1→splitter");
        Tab(w); AssertFocused(w, b2, "Tab2→b2");
        Tab(w); AssertFocused(w, b1, "Tab3 wraps→b1");
        Tab(w); AssertFocused(w, splitter, "Tab4→splitter");
        Tab(w); AssertFocused(w, b2, "Tab5→b2");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 12. Deep nesting: SPC[HGrid mixed content with buttons and inner SPC]
    //     window[SPC[HGrid[col1[b_top], col2[innerSPC[b1, b2]]]]]
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SPC_HGrid_MixedColumns_TabViaProcessKey_Forward()
    {
        var (w, _) = CreateWindow();
        var panel = new ScrollablePanelControl { Height = 15 };
        var bTop = new ButtonControl { Text = "Top" };    // direct button in col1
        var b1 = new ButtonControl { Text = "B1" };       // in inner SPC
        var b2 = new ButtonControl { Text = "B2" };       // in inner SPC

        var innerSPC = new ScrollablePanelControl { Height = 8 };
        innerSPC.AddControl(b1);
        innerSPC.AddControl(b2);

        var grid = new HorizontalGridControl();
        var col1 = new ColumnContainer(grid);
        var col2 = new ColumnContainer(grid);
        col1.AddContent(bTop);
        col2.AddContent(innerSPC);
        grid.AddColumn(col1);
        grid.AddColumn(col2);
        panel.AddControl(grid);
        w.AddControl(panel);

        AssertFocused(w, bTop, "Auto→bTop");

        Tab(w); AssertFocused(w, b1, "Tab1→b1 (enters innerSPC)");
        Tab(w); AssertFocused(w, b2, "Tab2→b2");
        Tab(w); AssertFocused(w, bTop, "Tab3 wraps→bTop");
        Tab(w); AssertFocused(w, b1, "Tab4→b1 (re-enters)");
    }

    [Fact]
    public void SPC_HGrid_MixedColumns_TabViaProcessKey_Backward()
    {
        var (w, _) = CreateWindow();
        var panel = new ScrollablePanelControl { Height = 15 };
        var bTop = new ButtonControl { Text = "Top" };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };

        var innerSPC = new ScrollablePanelControl { Height = 8 };
        innerSPC.AddControl(b1);
        innerSPC.AddControl(b2);

        var grid = new HorizontalGridControl();
        var col1 = new ColumnContainer(grid);
        var col2 = new ColumnContainer(grid);
        col1.AddContent(bTop);
        col2.AddContent(innerSPC);
        grid.AddColumn(col1);
        grid.AddColumn(col2);
        panel.AddControl(grid);
        w.AddControl(panel);

        AssertFocused(w, bTop, "Auto→bTop");

        ShiftTab(w); AssertFocused(w, b2, "ShiftTab1→b2 (backward into innerSPC)");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab2→b1");
        ShiftTab(w); AssertFocused(w, bTop, "ShiftTab3 wraps→bTop");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab4→b2 again");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 13. Outer SPC with mixed content: buttons + HGrid
    //     window[SPC[b_before, HGrid[b1,b2], b_after]]
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SPC_ButtonHGridButton_TabViaProcessKey_Forward()
    {
        var (w, _) = CreateWindow();
        var panel = new ScrollablePanelControl { Height = 15 };
        var bBefore = new ButtonControl { Text = "Before" };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var bAfter = new ButtonControl { Text = "After" };
        panel.AddControl(bBefore);
        panel.AddControl(MakeGrid(b1, b2));
        panel.AddControl(bAfter);
        w.AddControl(panel);

        AssertFocused(w, bBefore, "Auto→bBefore");
        Tab(w); AssertFocused(w, b1, "Tab1→b1");
        Tab(w); AssertFocused(w, b2, "Tab2→b2");
        Tab(w); AssertFocused(w, bAfter, "Tab3→bAfter");
        Tab(w); AssertFocused(w, bBefore, "Tab4 wraps→bBefore");
        Tab(w); AssertFocused(w, b1, "Tab5→b1");
        Tab(w); AssertFocused(w, b2, "Tab6→b2");
        Tab(w); AssertFocused(w, bAfter, "Tab7→bAfter");
    }

    [Fact]
    public void SPC_ButtonHGridButton_TabViaProcessKey_Backward()
    {
        var (w, _) = CreateWindow();
        var panel = new ScrollablePanelControl { Height = 15 };
        var bBefore = new ButtonControl { Text = "Before" };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var bAfter = new ButtonControl { Text = "After" };
        panel.AddControl(bBefore);
        panel.AddControl(MakeGrid(b1, b2));
        panel.AddControl(bAfter);
        w.AddControl(panel);

        AssertFocused(w, bBefore, "Auto→bBefore");
        ShiftTab(w); AssertFocused(w, bAfter, "ShiftTab1 wraps→bAfter");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab2→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab3→b1");
        ShiftTab(w); AssertFocused(w, bBefore, "ShiftTab4→bBefore");
        ShiftTab(w); AssertFocused(w, bAfter, "ShiftTab5 wraps→bAfter");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab6→b2");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 14. Nested HGrids at window level: outerGrid[col1[innerGrid[b1,b2]], col2[b3]]
    //     Both HGrids are transparent, all buttons in flat list.
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NestedHGrids_AtWindowLevel_Forward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };

        var innerGrid = MakeGrid(b1, b2);
        var outerGrid = new HorizontalGridControl();
        var outerCol1 = new ColumnContainer(outerGrid);
        var outerCol2 = new ColumnContainer(outerGrid);
        outerCol1.AddContent(innerGrid);
        outerCol2.AddContent(b3);
        outerGrid.AddColumn(outerCol1);
        outerGrid.AddColumn(outerCol2);
        w.AddControl(outerGrid);

        // HGrid transparent → all buttons in flat list → [b1, b2, b3]
        AssertFocused(w, b1, "Auto→b1");
        Tab(w); AssertFocused(w, b2, "Tab1→b2");
        Tab(w); AssertFocused(w, b3, "Tab2→b3");
        Tab(w); AssertFocused(w, b1, "Tab3 wraps→b1");
        Tab(w); AssertFocused(w, b2, "Tab4→b2");
        Tab(w); AssertFocused(w, b3, "Tab5→b3");
    }

    [Fact]
    public void NestedHGrids_AtWindowLevel_Backward_FullCycle()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };

        var innerGrid = MakeGrid(b1, b2);
        var outerGrid = new HorizontalGridControl();
        var outerCol1 = new ColumnContainer(outerGrid);
        var outerCol2 = new ColumnContainer(outerGrid);
        outerCol1.AddContent(innerGrid);
        outerCol2.AddContent(b3);
        outerGrid.AddColumn(outerCol1);
        outerGrid.AddColumn(outerCol2);
        w.AddControl(outerGrid);

        AssertFocused(w, b1, "Auto→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab1 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab2→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab3→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab4 wraps→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab5→b2");
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 15. Mixed forward+backward alternating navigation
    // ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HGrid_ThreeButtons_MixedNavigation()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        w.AddControl(MakeGrid(b1, b2, b3));

        AssertFocused(w, b1, "Auto→b1");
        Tab(w);      AssertFocused(w, b2, "Tab→b2");
        Tab(w);      AssertFocused(w, b3, "Tab→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab→b2");
        Tab(w);      AssertFocused(w, b3, "Tab→b3 again");
        Tab(w);      AssertFocused(w, b1, "Tab wraps→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab wraps→b3");
        Tab(w);      AssertFocused(w, b1, "Tab→b1");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab→b1");
    }

    [Fact]
    public void Window_TwoHGrids_MixedNavigation()
    {
        var (w, _) = CreateWindow();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        var b3 = new ButtonControl { Text = "B3" };
        var b4 = new ButtonControl { Text = "B4" };
        w.AddControl(MakeGrid(b1, b2));
        w.AddControl(MakeGrid(b3, b4));

        AssertFocused(w, b1, "Auto→b1");
        Tab(w);      AssertFocused(w, b2, "Tab→b2");
        ShiftTab(w); AssertFocused(w, b1, "ShiftTab→b1");
        Tab(w);      AssertFocused(w, b2, "Tab→b2 again");
        Tab(w);      AssertFocused(w, b3, "Tab→b3");
        ShiftTab(w); AssertFocused(w, b2, "ShiftTab→b2");
        Tab(w);      AssertFocused(w, b3, "Tab→b3");
        Tab(w);      AssertFocused(w, b4, "Tab→b4");
        ShiftTab(w); AssertFocused(w, b3, "ShiftTab→b3");
        Tab(w);      AssertFocused(w, b4, "Tab→b4");
        Tab(w);      AssertFocused(w, b1, "Tab wraps→b1");
        ShiftTab(w); AssertFocused(w, b4, "ShiftTab wraps→b4");
    }
}
