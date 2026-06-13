// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression coverage for the mouse-focus bug in <see cref="CollapsiblePanel"/>: left-clicking
/// a focusable control inside the panel's expanded body must focus that body child, exactly the
/// way Tab traversal into the body already does. These tests drive the real DOM hit-test +
/// <see cref="FocusManager.HandleClick"/> path used by the window event dispatcher.
/// </summary>
public class CollapsiblePanelMouseFocusTests
{
    /// <summary>
    /// Builds a window hosting an expanded CollapsiblePanel whose body contains a focusable button.
    /// The window is rendered so the DOM layout tree (and absolute bounds) are current.
    /// </summary>
    private static (ConsoleWindowSystem system, Window window, CollapsiblePanel panel, ButtonControl button)
        BuildPanelWithBodyButton()
    {
        const int width = 40;
        const int height = 16;

        var button = ControlsFactory.Button("Body Button").Build();

        var panel = ControlsFactory.CollapsiblePanel("Header")
            .WithWidth(30)
            .AddControl(button)
            .Build();

        var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
        var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
        window.AddControl(panel);
        system.AddWindow(window);
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        return (system, window, panel, button);
    }

    /// <summary>
    /// The DOM hit-test at the body button's content coordinates must return the BUTTON, not the
    /// panel. If it returns the panel, mouse focus can never reach the child (root cause #1).
    /// </summary>
    [Fact]
    public void HitTest_AtBodyButton_ReturnsButtonNotPanel()
    {
        var (system, window, panel, button) = BuildPanelWithBodyButton();

        // Header occupies content row y=0 (borderless, 1 row). The body button is the first body
        // child, so it lands on content row y=1.
        var hit = window.Renderer!.HitTestDOM(0, 1);

        Assert.Same(button, hit);
    }

    /// <summary>
    /// Failing-before / passing-after: a left click at the body button's coordinates must focus it.
    /// Mirrors the dispatcher path: hit-test resolves the target, then HandleClick routes focus.
    /// </summary>
    [Fact]
    public void Click_OnBodyButton_FocusesIt()
    {
        var (system, window, panel, button) = BuildPanelWithBodyButton();

        // Start from a clean focus state.
        window.FocusManager.SetFocus(null, FocusReason.Programmatic);

        // Resolve the click target the way the window dispatcher does, then route focus.
        var hit = window.Renderer!.HitTestDOM(0, 1);
        window.FocusManager.HandleClick(hit);

        Assert.True(window.FocusManager.IsFocused(button),
            "Left-clicking a focusable control in the panel body must focus that child.");
    }

    /// <summary>
    /// Tab traversal into the body already works — this asserts the keyboard path so the test file
    /// documents the contrast (keyboard focus works; the click path is what the fix restores).
    /// </summary>
    [Fact]
    public void Tab_IntoBody_FocusesButton()
    {
        var (system, window, panel, button) = BuildPanelWithBodyButton();

        window.FocusManager.SetFocus(panel, FocusReason.Keyboard);
        // Advance until the body button receives focus (header is itself a focus stop).
        for (int i = 0; i < 4 && !window.FocusManager.IsFocused(button); i++)
            window.SwitchFocus(backward: false);

        Assert.True(window.FocusManager.IsFocused(button),
            "Tab traversal into the body should reach the focusable child.");
    }

    /// <summary>
    /// Full real-dispatch repro: drives a simulated left click through the driver + window event
    /// dispatcher (not a direct HandleClick call) and asserts the body button ends up focused.
    /// This exercises HandleClickFocus AND the panel's ProcessMouseEvent return value together.
    /// </summary>
    [Fact]
    public void RealDispatch_ClickOnBodyButton_FocusesIt()
    {
        var (system, window, panel, button) = BuildPanelWithBodyButton();
        window.FocusManager.SetFocus(null, FocusReason.Programmatic);

        // The body button is at content row y=1 (header row y=0). Translate to absolute screen
        // coordinates: window border (+1) + content position. Click a couple columns in.
        int clickX = window.Left + 1 + button.ActualX + 1;
        int clickY = window.Top + 1 + button.ActualY;

        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
        system.Input.ProcessInput();

        Assert.True(window.FocusManager.IsFocused(button),
            "Full-dispatch left click on a focusable body control must focus it.");
    }

    /// <summary>
    /// The real user scenario: the body hosts a self-painting ScrollablePanel that itself contains a
    /// focusable button. Clicking that nested button must focus it (so a later mouse-wheel scroll
    /// reaches the SPC) WITHOUT needing to Tab in first.
    /// </summary>
    [Fact]
    public void RealDispatch_ClickOnButtonInNestedScrollablePanel_FocusesIt()
    {
        const int width = 44;
        const int height = 18;

        var nestedButton = ControlsFactory.Button("Nested Button").Build();
        var scroller = ControlsFactory.ScrollablePanel()
            .WithHeight(6)
            .AddControl(nestedButton)
            .Build();

        var panel = ControlsFactory.CollapsiblePanel("Header")
            .WithWidth(34)
            .AddControl(scroller)
            .Build();

        var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
        var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
        window.AddControl(panel);
        system.AddWindow(window);
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        window.FocusManager.SetFocus(null, FocusReason.Programmatic);

        // The SPC body starts at content row y=1 (header row y=0); the nested button is its first
        // child, so it renders on the SPC's first content row.
        int clickX = window.Left + 1 + nestedButton.ActualX + 1;
        int clickY = window.Top + 1 + nestedButton.ActualY;

        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
        system.Input.ProcessInput();

        Assert.True(window.FocusManager.IsFocused(nestedButton),
            "Clicking a focusable control inside a body-hosted ScrollablePanel must focus it.");
    }

    /// <summary>
    /// The actual demo topology: the CollapsiblePanel lives inside a self-painting root
    /// ScrollablePanel (so the window DOM hit-test resolves the ROOT SPC, and the SPC must forward
    /// the click down through the panel to the body child). Clicking the body button must focus it.
    /// </summary>
    [Fact]
    public void RealDispatch_ClickBodyButton_WhenPanelInsideRootScrollablePanel_FocusesIt()
    {
        const int width = 44;
        const int height = 18;

        var bodyButton = ControlsFactory.Button("Body Button").Build();

        var panel = ControlsFactory.CollapsiblePanel("Header")
            .WithWidth(34)
            .AddControl(bodyButton)
            .Build();

        // Self-painting root host (mirrors CollapsibleDemoWindow's root ScrollablePanel).
        var root = ControlsFactory.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .AddControl(panel)
            .Build();

        var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
        var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
        window.AddControl(root);
        system.AddWindow(window);
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        window.FocusManager.SetFocus(null, FocusReason.Programmatic);

        int clickX = window.Left + 1 + bodyButton.ActualX + 1;
        int clickY = window.Top + 1 + bodyButton.ActualY;

        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
        system.Input.ProcessInput();

        Assert.True(window.FocusManager.IsFocused(bodyButton),
            "Clicking a body control of a CollapsiblePanel hosted in a root ScrollablePanel must focus it.");
    }

    /// <summary>
    /// Bubble case: a mouse wheel over an expanded panel body whose child does NOT consume scroll
    /// (a non-scrollable Markup label) must NOT be swallowed. ProcessMouseEvent must return false and
    /// leave Handled unset so the event bubbles up to an outer scroll container.
    /// </summary>
    [Fact]
    public void BodyWheel_NotConsumedByChild_Bubbles()
    {
        const int width = 40;
        const int height = 16;

        // A plain Markup label: not mouse-aware for scrolling — it never consumes a wheel event.
        var label = ControlsFactory.Label("Just some passive body text");

        var panel = ControlsFactory.CollapsiblePanel("Header")
            .WithWidth(30)
            .AddControl(label)
            .Build();

        var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
        var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
        window.AddControl(panel);
        system.AddWindow(window);
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        // Body region begins on content row y=1 (header occupies y=0, borderless).
        var wheelArgs = new MouseEventArgs(
            new List<MouseFlags> { MouseFlags.WheeledDown },
            new Point(2, 1),
            new Point(2, 1),
            new Point(2, 1),
            window);

        bool handled = ((IMouseAwareControl)panel).ProcessMouseEvent(wheelArgs);

        Assert.False(handled,
            "An unconsumed wheel over the panel body must return false so it bubbles to an outer scroll container.");
        Assert.False(wheelArgs.Handled,
            "An unconsumed wheel must leave Handled unset so the dispatcher keeps bubbling up the container chain.");
    }

    /// <summary>
    /// Consumed case: a mouse wheel over an expanded panel body whose child IS a ScrollablePanel with
    /// overflowing content must be consumed by that child. ProcessMouseEvent returns true and the inner
    /// SPC's scroll offset advances.
    /// </summary>
    [Fact]
    public void BodyWheel_ConsumedByChild_ReturnsTrue()
    {
        const int width = 44;
        const int height = 18;

        // Overflowing content so the inner SPC can actually scroll down.
        var scroller = ControlsFactory.ScrollablePanel()
            .WithHeight(4)
            .AddControl(ControlsFactory.Label("Line 1"))
            .AddControl(ControlsFactory.Label("Line 2"))
            .AddControl(ControlsFactory.Label("Line 3"))
            .AddControl(ControlsFactory.Label("Line 4"))
            .AddControl(ControlsFactory.Label("Line 5"))
            .AddControl(ControlsFactory.Label("Line 6"))
            .AddControl(ControlsFactory.Label("Line 7"))
            .AddControl(ControlsFactory.Label("Line 8"))
            .Build();

        var panel = ControlsFactory.CollapsiblePanel("Header")
            .WithWidth(34)
            .AddControl(scroller)
            .Build();

        var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
        var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
        window.AddControl(panel);
        system.AddWindow(window);
        system.Render.UpdateDisplay();
        system.Render.UpdateDisplay();

        int before = scroller.VerticalScrollOffset;

        // Body region begins on content row y=1 (header occupies y=0); aim inside the SPC viewport.
        var wheelArgs = new MouseEventArgs(
            new List<MouseFlags> { MouseFlags.WheeledDown },
            new Point(2, 2),
            new Point(2, 2),
            new Point(2, 2),
            window);

        bool handled = ((IMouseAwareControl)panel).ProcessMouseEvent(wheelArgs);

        Assert.True(handled,
            "A wheel over a body-hosted scrollable child with overflow must be consumed (return true).");
        Assert.True(scroller.VerticalScrollOffset > before,
            "The inner ScrollablePanel should have scrolled down when the wheel was forwarded to it.");
    }
}
