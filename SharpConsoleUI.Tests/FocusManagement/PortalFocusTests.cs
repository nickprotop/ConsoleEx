// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

public class PortalFocusTests
{
    [Fact]
    public void PortalChild_HasFocus_WithoutStealingWindowFocus()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var editor = new MultilineEditControl();
        window.AddControl(editor);
        window.FocusManager.SetFocus(editor, FocusReason.Programmatic);

        var portal = new PortalContentContainer
        {
            PortalBounds = new Rectangle(10, 10, 30, 10)
        };
        portal.Container = window;
        var list = new ListControl();
        list.AddItem("Item 1");
        portal.AddChild(list);

        // Set portal focus
        portal.PortalFocusedControl = list;

        // List should appear focused via portal
        Assert.True(list.HasFocus);
        // Editor should still have window focus
        Assert.True(window.FocusManager.IsFocused(editor));
    }

    [Fact]
    public void PortalContentBase_Subclass_PortalFocusViaRegistry()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };

        var portal = new TestPortalContent();
        portal.Container = window;
        var list = new ListControl();
        list.AddItem("Item 1");
        // Don't set list.Container to portal — simulates PortalContentBase subclass
        // where the child's Container chain doesn't include the portal.

        // Set portal focus (registers in static ConditionalWeakTable)
        portal.PortalFocusedControl = list;

        // List should appear focused via static registry lookup
        Assert.True(list.HasFocus);
    }

    [Fact]
    public void SettingNewPortalFocusedControl_ClearsOld()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var portal = new PortalContentContainer
        {
            PortalBounds = new Rectangle(0, 0, 30, 10)
        };
        portal.Container = window;

        var listA = new ListControl();
        listA.AddItem("A");
        portal.AddChild(listA);

        var listB = new ListControl();
        listB.AddItem("B");
        portal.AddChild(listB);

        portal.PortalFocusedControl = listA;
        Assert.True(listA.HasFocus);
        Assert.False(listB.HasFocus);

        portal.PortalFocusedControl = listB;
        Assert.False(listA.HasFocus);
        Assert.True(listB.HasFocus);
    }

    [Fact]
    public void PortalContentContainer_SetFocusOnFirstChild_NoWindowFocusSteal()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var editor = new MultilineEditControl();
        window.AddControl(editor);
        window.FocusManager.SetFocus(editor, FocusReason.Programmatic);

        var portal = new PortalContentContainer
        {
            PortalBounds = new Rectangle(10, 10, 30, 10)
        };
        portal.Container = window;
        var list = new ListControl();
        list.AddItem("Item 1");
        portal.AddChild(list);

        portal.SetFocusOnFirstChild();

        // List should have portal focus
        Assert.True(list.HasFocus);
        // Window focus should still be on editor
        Assert.True(window.FocusManager.IsFocused(editor));
    }

    [Fact]
    public void Dispose_ClearsPortalFocusRegistry()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };

        var list = new ListControl();
        list.AddItem("Item 1");

        var portal = new PortalContentContainer
        {
            PortalBounds = new Rectangle(0, 0, 30, 10)
        };
        portal.Container = window;
        portal.AddChild(list);
        portal.PortalFocusedControl = list;
        Assert.True(list.HasFocus);

        portal.Dispose();
        Assert.False(list.HasFocus);
    }

    [Fact]
    public void MultiplePortals_IndependentFocus()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };

        var portalA = new PortalContentContainer
        {
            PortalBounds = new Rectangle(0, 0, 30, 10)
        };
        portalA.Container = window;
        var listA = new ListControl();
        listA.AddItem("A");
        portalA.AddChild(listA);

        var portalB = new PortalContentContainer
        {
            PortalBounds = new Rectangle(40, 0, 30, 10)
        };
        portalB.Container = window;
        var listB = new ListControl();
        listB.AddItem("B");
        portalB.AddChild(listB);

        portalA.PortalFocusedControl = listA;
        portalB.PortalFocusedControl = listB;

        // Both lists have focus in their respective portals
        Assert.True(listA.HasFocus);
        Assert.True(listB.HasFocus);

        // Clear one portal's focus
        portalA.PortalFocusedControl = null;
        Assert.False(listA.HasFocus);
        Assert.True(listB.HasFocus);
    }

    [Fact]
    public void ContainerControl_IsInFocusPath_WhenChildHasPortalFocus()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };

        var portal = new PortalContentContainer
        {
            PortalBounds = new Rectangle(0, 0, 40, 20)
        };
        portal.Container = window;

        var panel = new ScrollablePanelControl();
        portal.AddChild(panel);

        var list = new ListControl();
        list.AddItem("Item 1");
        panel.AddControl(list);

        // Focus the list inside the panel inside the portal
        portal.PortalFocusedControl = list;

        // The panel (container) should be in focus path
        Assert.True(panel.HasFocus);
        // The list (leaf) should have direct focus
        Assert.True(list.HasFocus);
    }

    /// <summary>
    /// Test portal content subclass for registry-based focus lookup.
    /// </summary>
    private class TestPortalContent : PortalContentBase
    {
        public override Rectangle GetPortalBounds() => new(0, 0, 30, 10);

        public override bool ProcessMouseEvent(SharpConsoleUI.Events.MouseEventArgs args) => false;

        protected override void PaintPortalContent(
            SharpConsoleUI.Layout.CharacterBuffer buffer,
            SharpConsoleUI.Layout.LayoutRect bounds,
            SharpConsoleUI.Layout.LayoutRect clipRect,
            Color defaultFg, Color defaultBg)
        {
        }
    }
}
