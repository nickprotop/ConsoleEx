// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

public class FocusManagerTests
{
    [Fact]
    public void SetFocus_UpdatesFocusedControl()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var button = new ButtonControl { Text = "B" };
        window.AddControl(button);

        window.FocusManager.SetFocus(button, FocusReason.Programmatic);

        Assert.Equal(button, window.FocusManager.FocusedControl);
    }

    [Fact]
    public void SetFocus_BuildsFocusPath()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var button = new ButtonControl { Text = "B" };
        window.AddControl(button);

        window.FocusManager.SetFocus(button, FocusReason.Programmatic);

        Assert.Contains(button, window.FocusManager.FocusPath);
    }

    [Fact]
    public void IsFocused_ReturnsTrueForFocusedControl()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        window.AddControl(b1);
        window.AddControl(b2);

        window.FocusManager.SetFocus(b1, FocusReason.Programmatic);

        Assert.True(window.FocusManager.IsFocused(b1));
        Assert.False(window.FocusManager.IsFocused(b2));
    }

    [Fact]
    public void SetFocus_FiresFocusChangedEvent()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        // Add a placeholder so it takes auto-focus, leaving button unfocused
        var placeholder = new ButtonControl { Text = "Placeholder" };
        window.AddControl(placeholder);
        var button = new ButtonControl { Text = "B" };
        window.AddControl(button);

        FocusChangedEventArgs? received = null;
        window.FocusManager.FocusChanged += (_, args) => received = args;

        window.FocusManager.SetFocus(button, FocusReason.Mouse);

        Assert.NotNull(received);
        Assert.Equal(button, received!.Current);
        Assert.Equal(FocusReason.Mouse, received.Reason);
    }

    [Fact]
    public void IsInFocusPath_ReturnsTrueForAncestorOfFocusedControl()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var panel = new ScrollablePanelControl();
        var button = new ButtonControl { Text = "B" };
        panel.AddControl(button);
        window.AddControl(panel);

        window.FocusManager.SetFocus(button, FocusReason.Programmatic);

        Assert.True(window.FocusManager.IsInFocusPath(panel));
        Assert.True(window.FocusManager.IsInFocusPath(button));
    }

    [Fact]
    public void HandleClick_SetsFocusedControl_WhenClickingLeafControl()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var b1 = new ButtonControl { Text = "B1" };
        var b2 = new ButtonControl { Text = "B2" };
        window.AddControl(b1);
        window.AddControl(b2);
        window.FocusManager.SetFocus(b1, FocusReason.Programmatic);

        // Simulate clicking b2
        window.FocusManager.HandleClick(b2);

        Assert.Equal(b2, window.FocusManager.FocusedControl);
        Assert.True(window.FocusManager.IsFocused(b2));
        Assert.False(window.FocusManager.IsFocused(b1));
    }
}
