// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Behavior and rendering tests for <see cref="CollapsiblePanel"/>.
/// </summary>
public class CollapsiblePanelTests
{
    private static MarkupControl Label(string text) =>
        new MarkupControl(new List<string> { text });

    /// <summary>
    /// Strips ANSI escape codes from output lines to get plain text.
    /// </summary>
    private static string StripAnsiCodes(IEnumerable<string> lines)
    {
        return string.Join("\n", lines.Select(line =>
            System.Text.RegularExpressions.Regex.Replace(line, @"\x1b\[[0-9;]*m", "")));
    }

    /// <summary>
    /// Renders a control inside a test window and returns the plain-text (ANSI-stripped) lines.
    /// Mirrors the TabControlTests render path: CreateTestSystem → Window → AddControl → RenderAndGetVisibleContent.
    /// </summary>
    private static List<string> RenderToLines(IWindowControl control, int width, int height)
    {
        var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
        var window = new Window(system) { Width = width, Height = height };
        window.AddControl(control);
        var output = window.RenderAndGetVisibleContent();
        return StripAnsiCodes(output).Split('\n').ToList();
    }

    [Fact]
    public void AddControl_AddsChildToChildrenAndGetChildren()
    {
        var panel = new CollapsiblePanel { Title = "Section" };
        var child = Label("body");

        panel.AddControl(child);

        Assert.Contains(child, panel.Children);
        Assert.Contains(child, panel.GetChildren());
        Assert.Same(panel, child.Container);
    }

    [Fact]
    public void RemoveControl_RemovesChild()
    {
        var panel = new CollapsiblePanel();
        var child = Label("body");
        panel.AddControl(child);

        panel.RemoveControl(child);

        Assert.DoesNotContain(child, panel.Children);
    }

    [Fact]
    public void ClearControls_RemovesAll()
    {
        var panel = new CollapsiblePanel();
        panel.AddControl(Label("a"));
        panel.AddControl(Label("b"));

        panel.ClearControls();

        Assert.Empty(panel.Children);
    }

    [Fact]
    public void Collapse_HidesChildren_AndExpandShowsThem()
    {
        var panel = new CollapsiblePanel();
        var child = Label("body");
        panel.AddControl(child);

        panel.Collapse();
        Assert.False(panel.IsExpanded);
        Assert.False(child.Visible);

        panel.Expand();
        Assert.True(panel.IsExpanded);
        Assert.True(child.Visible);
    }

    [Fact]
    public void IsExpanded_RaisesExpandedChangedOncePerRealChange()
    {
        var panel = new CollapsiblePanel();
        int count = 0;
        bool? last = null;
        panel.ExpandedChanged += (_, v) => { count++; last = v; };

        panel.IsExpanded = false;   // real change
        panel.IsExpanded = false;   // no-op, must not fire
        panel.IsExpanded = true;    // real change

        Assert.Equal(2, count);
        Assert.True(last);
    }

    [Fact]
    public void Toggle_FlipsState()
    {
        var panel = new CollapsiblePanel();
        Assert.True(panel.IsExpanded);
        panel.Toggle();
        Assert.False(panel.IsExpanded);
    }

    [Fact]
    public void Header_RendersCollapsedIcon_AndTitle()
    {
        var panel = new CollapsiblePanel { Title = "Reasoning", IsExpanded = false, Width = 30 };
        panel.AddControl(Label("hidden body"));
        var lines = RenderToLines(panel, width: 32, height: 6);
        Assert.Contains(lines, l => l.Contains("▸") && l.Contains("Reasoning"));
        Assert.DoesNotContain(lines, l => l.Contains("hidden body"));
    }

    [Fact]
    public void Header_Expanded_ShowsExpandedIcon()
    {
        var panel = new CollapsiblePanel { Title = "Reasoning", IsExpanded = true, Width = 30 };
        var lines = RenderToLines(panel, width: 32, height: 6);
        Assert.Contains(lines, l => l.Contains("▾") && l.Contains("Reasoning"));
    }

    [Fact]
    public void Expanded_ShowsBody_Collapsed_HidesBody()
    {
        var panel = new CollapsiblePanel { Title = "S", Width = 20 };
        panel.AddControl(Label("line one"));
        panel.AddControl(Label("line two"));

        var expanded = RenderToLines(panel, width: 22, height: 8);
        Assert.Contains(expanded, l => l.Contains("line one"));
        Assert.Contains(expanded, l => l.Contains("line two"));

        panel.Collapse();
        var collapsed = RenderToLines(panel, width: 22, height: 8);
        Assert.DoesNotContain(collapsed, l => l.Contains("line one"));
        Assert.DoesNotContain(collapsed, l => l.Contains("line two"));
    }

    [Fact]
    public void HeaderClick_TogglesExpansion()
    {
        var panel = new CollapsiblePanel { Title = "S", Width = 20 };
        panel.AddControl(Label("body"));
        RenderToLines(panel, width: 22, height: 6); // establish bounds (SetActualBounds)

        bool before = panel.IsExpanded;
        var args = new MouseEventArgs(
            new List<MouseFlags> { MouseFlags.Button1Clicked },
            new Point(1, 0),  // control-relative: header row (Margin.Top == 0)
            new Point(1, 0),
            new Point(1, 0));

        bool handled = ((IMouseAwareControl)panel).ProcessMouseEvent(args);

        Assert.True(handled);
        Assert.NotEqual(before, panel.IsExpanded);
    }

    [Fact]
    public void BodyClick_NotHandled()
    {
        var panel = new CollapsiblePanel { Title = "S", Width = 20 };
        panel.AddControl(Label("body"));
        RenderToLines(panel, width: 22, height: 6);

        bool before = panel.IsExpanded;
        var args = new MouseEventArgs(
            new List<MouseFlags> { MouseFlags.Button1Clicked },
            new Point(1, 3),  // below the header row → body
            new Point(1, 3),
            new Point(1, 3));

        bool handled = ((IMouseAwareControl)panel).ProcessMouseEvent(args);

        Assert.False(handled);
        Assert.Equal(before, panel.IsExpanded); // body click must not toggle
    }

    [Fact]
    public void EnterKey_TogglesExpansion_WhenFocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem(80, 30);
        var window = new Window(system) { Width = 80, Height = 30 };
        var panel = new CollapsiblePanel { Title = "S" };
        panel.AddControl(Label("body"));
        window.AddControl(panel);
        window.FocusManager.SetFocus(panel, FocusReason.Programmatic);
        Assert.True(panel.HasFocus);

        bool before = panel.IsExpanded;
        bool handled = ((IInteractiveControl)panel).ProcessKey(
            new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));

        Assert.True(handled);
        Assert.NotEqual(before, panel.IsExpanded);
    }

    [Fact]
    public void Spacebar_TogglesExpansion_WhenFocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem(80, 30);
        var window = new Window(system) { Width = 80, Height = 30 };
        var panel = new CollapsiblePanel { Title = "S" };
        panel.AddControl(Label("body"));
        window.AddControl(panel);
        window.FocusManager.SetFocus(panel, FocusReason.Programmatic);

        bool before = panel.IsExpanded;
        bool handled = ((IInteractiveControl)panel).ProcessKey(
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false));

        Assert.True(handled);
        Assert.NotEqual(before, panel.IsExpanded);
    }

    [Fact]
    public void EnterKey_Ignored_WhenNotFocused()
    {
        var panel = new CollapsiblePanel { Title = "S" };
        panel.AddControl(Label("body"));

        bool before = panel.IsExpanded;
        bool handled = ((IInteractiveControl)panel).ProcessKey(
            new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));

        Assert.False(handled);
        Assert.Equal(before, panel.IsExpanded);
    }

    /// <summary>
    /// Paints the panel's header directly into a CharacterBuffer and returns the foreground
    /// color of the first cell whose character matches <paramref name="titleChar"/> on the header row.
    /// </summary>
    private static Color HeaderCharForeground(CollapsiblePanel panel, char titleChar)
    {
        const int width = 40;
        const int height = 6;
        var buffer = new CharacterBuffer(width, height);
        var bounds = new LayoutRect(0, 0, width, height);
        panel.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

        // The header is the top row (Margin defaults to 0). Scan it for the title glyph.
        for (int x = 0; x < width; x++)
        {
            var cell = buffer.GetCell(x, 0);
            if (cell.Character.ToString() == titleChar.ToString())
                return cell.Foreground;
        }

        throw new Xunit.Sdk.XunitException($"Title character '{titleChar}' not found on header row.");
    }

    [Fact]
    public void Header_WhenFocused_UsesFocusedForegroundColor()
    {
        // A distinctive per-instance focused foreground makes the assertion unambiguous.
        var system = TestWindowSystemBuilder.CreateTestSystem(80, 30);
        var window = new Window(system) { Width = 80, Height = 30 };
        var panel = new CollapsiblePanel
        {
            Title = "X",
            FocusedForegroundColor = Color.Red
        };
        // A second focusable control so focus can be moved AWAY from the panel header.
        var sibling = new CheckboxControl { Label = "sibling" };
        window.AddControl(panel);
        window.AddControl(sibling);

        // Focus the header via the real focus API (mirrors the keyboard-activation tests).
        window.FocusManager.SetFocus(panel, FocusReason.Programmatic);
        Assert.True(panel.HasFocus);
        Color focusedFg = HeaderCharForeground(panel, 'X');
        Assert.Equal(Color.Red, focusedFg);

        // Move focus away: the header must NOT use the focused foreground color anymore.
        window.FocusManager.SetFocus(sibling, FocusReason.Programmatic);
        Assert.False(panel.HasFocus);
        Color unfocusedFg = HeaderCharForeground(panel, 'X');
        Assert.NotEqual(Color.Red, unfocusedFg);

        // The focused render must differ from the unfocused render.
        Assert.NotEqual(unfocusedFg, focusedFg);
    }

    [Fact]
    public void Expanded_FocusableChild_IsReachableByTabTraversal()
    {
        // CollapsiblePanel implements IFocusableContainerWithHeader, so its header is a Tab
        // stop AND its visible body children participate in Tab traversal. When collapsed,
        // children are Visible=false and must be skipped by focus collection.
        var system = TestWindowSystemBuilder.CreateTestSystem(80, 25);
        var window = new Window(system) { Width = 80, Height = 25 };

        var button = new ButtonControl { Text = "X" };
        var panel = new CollapsiblePanel { Title = "Section" };
        panel.AddControl(button);

        window.AddControl(panel);
        system.AddWindow(window);
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        // --- Expanded: the button must be reachable by forward Tab traversal. ---
        Assert.True(panel.IsExpanded);

        bool buttonReachedExpanded = window.FocusManager.IsFocused(button);
        // Cycle through every focus stop at least twice to guarantee we visit the whole list.
        for (int i = 0; i < 8 && !buttonReachedExpanded; i++)
        {
            window.SwitchFocus(backward: false);
            if (window.FocusManager.IsFocused(button))
                buttonReachedExpanded = true;
        }

        Assert.True(buttonReachedExpanded,
            "Expanded panel: focusable child button should be reachable via Tab traversal.");

        // --- Collapsed: the button is hidden and must NOT be reachable. ---
        panel.Collapse();
        Assert.False(panel.IsExpanded);
        Assert.False(button.Visible);

        // Move focus back to the panel header as a known starting point.
        window.FocusManager.SetFocus(panel, FocusReason.Programmatic);

        bool buttonReachedCollapsed = window.FocusManager.IsFocused(button);
        for (int i = 0; i < 8 && !buttonReachedCollapsed; i++)
        {
            window.SwitchFocus(backward: false);
            if (window.FocusManager.IsFocused(button))
                buttonReachedCollapsed = true;
        }

        Assert.False(buttonReachedCollapsed,
            "Collapsed panel: hidden child button must NOT be reachable via Tab traversal.");
    }

    [Fact]
    public void NoAnimation_Default_IsInstant_AnimatedHeightNull()
    {
        var panel = new CollapsiblePanel { Title = "S" };
        panel.AddControl(Label("body"));

        panel.Collapse();

        Assert.Null(panel.AnimatedBodyHeight);
        Assert.False(panel.IsExpanded);
    }

    [Fact]
    public void HeightAnimation_WhenEnabled_TweensAndSettlesClosed()
    {
        var panel = new CollapsiblePanel { Title = "S", Width = 20 };
        panel.AddControl(Label("body line"));
        var anim = new SharpConsoleUI.Animation.AnimationManager();
        panel.SetAnimationManagerForTesting(anim);
        panel.AnimationMode = CollapsibleAnimationMode.Height;
        RenderToLines(panel, 22, 8); // establish bounds + measured body

        panel.Collapse();
        // Advance the manager to completion in steps (respects MaxFrameDeltaMs cap).
        AdvanceByMs(anim, SharpConsoleUI.Configuration.ControlDefaults.CollapsiblePanelAnimationDurationMs * 2);

        Assert.False(panel.IsExpanded);
        Assert.Equal(0, panel.AnimatedBodyHeight); // settled fully closed
    }

    [Fact]
    public void Builder_BuildsConfiguredPanel()
    {
        var panel = SharpConsoleUI.Builders.Controls.CollapsiblePanel("Title")
            .Collapsed()
            .WithHeaderStyle(CollapsibleHeaderStyle.Bordered)
            .WithHeaderSeparator()
            .WithMaxContentHeight(5)
            .WithIcons(expanded: "[-]", collapsed: "[+]")
            .AddControl(Label("body"))
            .Build();

        Assert.Equal("Title", panel.Title);
        Assert.False(panel.IsExpanded);
        Assert.Equal(CollapsibleHeaderStyle.Bordered, panel.HeaderStyle);
        Assert.True(panel.ShowHeaderSeparator);
        Assert.Equal(5, panel.MaxContentHeight);
        Assert.Single(panel.Children);
    }

    /// <summary>
    /// Bug A: a nested ScrollablePanel in the body must scroll when the window cannot show the
    /// whole panel. Previously the layout arranged the body child at its FULL content height
    /// (viewport == content → CanScrollDown == false). The body is now bounded to the on-screen
    /// content viewport, so a 30-line ScrollablePanel inside a 12-row window gets a viewport
    /// smaller than its content and scrolls.
    /// </summary>
    [Fact]
    public void NestedScrollablePanel_InBody_Scrolls_WhenWindowConstrained()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem(40, 12);
        var window = new Window(system) { Width = 40, Height = 12 };

        var lines = Enumerable.Range(0, 30).Select(i => $"content line {i:00}").ToList();
        var spc = SharpConsoleUI.Builders.Controls.ScrollablePanel().WithScrollbar(true).Build();
        spc.AddControl(new MarkupControl(lines));

        var panel = new CollapsiblePanel { Title = "Sec", Width = 36 };
        panel.AddControl(spc);
        window.AddControl(panel);

        window.RenderAndGetVisibleContent();

        Assert.True(spc.TotalContentHeight >= 30,
            $"Sanity: content should be ~30 lines, was {spc.TotalContentHeight}.");
        Assert.True(spc.ViewportHeight < spc.TotalContentHeight,
            $"Body ScrollablePanel viewport ({spc.ViewportHeight}) must be smaller than its " +
            $"content ({spc.TotalContentHeight}) so it can scroll. The body was not bounded to " +
            $"the on-screen viewport.");
        Assert.True(spc.CanScrollDown,
            "Body ScrollablePanel must be able to scroll down when space-constrained.");
    }

    /// <summary>
    /// Bug B: in the Bordered style the box must frame the BODY, not just the header. The body
    /// content must be inset inside the left/right border columns (not painted over column 0),
    /// and a bottom border row must appear below the body.
    /// </summary>
    [Fact]
    public void BorderedStyle_FramesBody_BodyIsInset_AndBottomBorderPresent()
    {
        var panel = new CollapsiblePanel
        {
            Title = "B",
            Width = 20,
            HeaderStyle = CollapsibleHeaderStyle.Bordered,
        };
        panel.AddControl(Label("INNERTEXT"));

        var lines = RenderToLines(panel, width: 24, height: 8);

        // Locate the body line holding the label.
        var bodyLine = lines.FirstOrDefault(l => l.Contains("INNERTEXT"));
        Assert.NotNull(bodyLine);

        // The body text must be INSET: there is a left vertical border char before it, so the
        // text does not start in the panel's left column.
        int textStart = bodyLine!.IndexOf("INNERTEXT", StringComparison.Ordinal);
        int leftBorder = bodyLine.IndexOf('│');
        Assert.True(leftBorder >= 0, $"Expected a left vertical border '│' on the body row: '{bodyLine}'");
        Assert.True(leftBorder < textStart,
            $"Body text must be inset to the right of the left border. " +
            $"border@{leftBorder}, text@{textStart} in '{bodyLine}'");

        // There must be a right vertical border to the right of the body text too.
        int rightBorder = bodyLine.IndexOf('│', textStart);
        Assert.True(rightBorder > textStart,
            $"Body text must be enclosed by a right vertical border: '{bodyLine}'");

        // A bottom border row (corners + horizontals) must appear below the body line.
        int bodyIdx = lines.FindIndex(l => l.Contains("INNERTEXT"));
        bool bottomBorderBelow = lines.Skip(bodyIdx + 1)
            .Any(l => l.Contains('└') && l.Contains('┘'));
        Assert.True(bottomBorderBelow,
            "A bottom border row (└ … ┘) must appear below the body.");
    }

    /// <summary>
    /// Bug E: a collapsed Bordered panel must render its header as a flat titled horizontal rule
    /// with NO corner glyphs (no dangling ┌/┐ with no box beneath them).
    /// </summary>
    [Fact]
    public void BorderedCollapsed_RendersFlatRule_NoCorners()
    {
        var panel = new CollapsiblePanel { Title = "Sec", HeaderStyle = CollapsibleHeaderStyle.Bordered, IsExpanded = false, Width = 24 };
        panel.AddControl(Label("body"));
        var lines = RenderToLines(panel, 26, 6);
        var headerLine = lines.First(l => l.Contains("Sec"));
        Assert.DoesNotContain('┌', headerLine);   // no corners when collapsed
        Assert.DoesNotContain('┐', headerLine);
        Assert.Contains('─', headerLine);          // flat rule
    }

    /// <summary>
    /// Bug E: an expanded Bordered panel must keep its corner glyphs (full box, unchanged).
    /// </summary>
    [Fact]
    public void BorderedExpanded_StillHasCorners()
    {
        var panel = new CollapsiblePanel { Title = "Sec", HeaderStyle = CollapsibleHeaderStyle.Bordered, IsExpanded = true, Width = 24 };
        panel.AddControl(Label("body"));
        var lines = RenderToLines(panel, 26, 6);
        Assert.Contains(lines, l => l.Contains('┌') && l.Contains("Sec")); // top border keeps corners when expanded
        Assert.Contains(lines, l => l.Contains('└')); // bottom border present
    }

    [Fact]
    public void GetLogicalCursorPosition_ForwardsToFocusedBodyChild_WhenExpanded()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem(80, 30);
        var window = new Window(system) { Width = 80, Height = 30 };
        var panel = new CollapsiblePanel { Title = "Section" };
        var prompt = new PromptControl { Prompt = "Name: " };
        panel.AddControl(prompt);
        window.AddControl(panel);

        // Focus the cursor-bearing body child (not the panel header) so the prompt
        // reports a real cursor position.
        window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
        window.RenderAndGetVisibleContent(); // establish bounds + cursor

        Assert.True(prompt.HasFocus);

        var childCursor = ((ILogicalCursorProvider)prompt).GetLogicalCursorPosition();
        Assert.NotNull(childCursor);

        // The panel reports the focused body child's cursor in the PANEL's own content space:
        // the child cursor plus the child's offset within the panel (the header row above it, plus
        // any margin/side inset). For this single borderless body child the only offset is the
        // one-row header, so the panel cursor is shifted down by exactly HeaderHeight (1).
        // Reporting it verbatim (the old behavior) placed the terminal cursor on the header row and
        // hid it once the panel was nested in a scroll container.
        var panelCursor = ((ILogicalCursorProvider)panel).GetLogicalCursorPosition();
        Assert.NotNull(panelCursor);
        Assert.Equal(childCursor!.Value.X, panelCursor!.Value.X);
        Assert.Equal(childCursor!.Value.Y + panel.HeaderHeight, panelCursor!.Value.Y);

        // When collapsed the body is hidden, so the panel reports no cursor.
        panel.IsExpanded = false;
        Assert.Null(((ILogicalCursorProvider)panel).GetLogicalCursorPosition());
    }

    /// <summary>
    /// Advances the animation manager in small steps to respect MaxFrameDeltaMs cap.
    /// </summary>
    private static void AdvanceByMs(SharpConsoleUI.Animation.AnimationManager manager, double totalMs)
    {
        double remaining = totalMs;
        while (remaining > 0)
        {
            double tick = Math.Min(remaining, SharpConsoleUI.Configuration.AnimationDefaults.MaxFrameDeltaMs);
            manager.Update(TimeSpan.FromMilliseconds(tick));
            remaining -= tick;
        }
    }
}
