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

public class ScrollablePanelFocusScopeTests
{
    [Fact]
    public void GetInitialFocus_ReturnsFirstChild_WhenForward()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var panel = new ScrollablePanelControl();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        panel.AddControl(b1);
        panel.AddControl(b2);
        window.AddControl(panel);

        var scope = (IFocusScope)panel;
        Assert.Equal(b1, scope.GetInitialFocus(backward: false));
    }

    [Fact]
    public void GetInitialFocus_ReturnsLastChild_WhenBackward()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var panel = new ScrollablePanelControl();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        panel.AddControl(b1);
        panel.AddControl(b2);
        window.AddControl(panel);

        var scope = (IFocusScope)panel;
        Assert.Equal(b2, scope.GetInitialFocus(backward: true));
    }

    [Fact]
    public void GetInitialFocus_ReturnsSavedFocus_WhenSet()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var panel = new ScrollablePanelControl();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        panel.AddControl(b1);
        panel.AddControl(b2);
        window.AddControl(panel);

        var scope = (IFocusScope)panel;
        scope.SavedFocus = b2;
        Assert.Equal(b2, scope.GetInitialFocus(backward: false));
        Assert.Null(scope.SavedFocus); // consumed after one use
    }

    [Fact]
    public void GetNextFocus_ReturnsNextChild()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var panel = new ScrollablePanelControl();
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        panel.AddControl(b1);
        panel.AddControl(b2);
        window.AddControl(panel);

        var scope = (IFocusScope)panel;
        Assert.Equal(b2, scope.GetNextFocus(b1, backward: false));
    }

    [Fact]
    public void GetNextFocus_ReturnsNull_WhenAtEnd()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var panel = new ScrollablePanelControl();
        var b1 = new ButtonControl { Text = "B1" };
        panel.AddControl(b1);
        window.AddControl(panel);

        var scope = (IFocusScope)panel;
        Assert.Null(scope.GetNextFocus(b1, backward: false));
    }

    [Fact]
    public void Escape_EntersScrollMode_ByFocusingPanelItself()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var panel = new ScrollablePanelControl();
        var b1 = new ButtonControl { Text = "B1" };
        panel.AddControl(b1);
        window.AddControl(panel);

        window.FocusManager.SetFocus(b1, FocusReason.Keyboard);
        panel.ProcessKey(new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false));

        Assert.Equal(panel, window.FocusManager.FocusedControl);
        Assert.Equal(b1, ((IFocusScope)panel).SavedFocus);
    }
}
