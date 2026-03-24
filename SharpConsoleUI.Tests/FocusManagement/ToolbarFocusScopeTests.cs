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

public class ToolbarFocusScopeTests
{
    [Fact]
    public void GetNextFocus_AlwaysReturnsNull_TabExitsImmediately()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var toolbar = new ToolbarControl();
        var item1 = new ButtonControl { Text = "File" };
        var item2 = new ButtonControl { Text = "Edit" };
        toolbar.AddItem(item1);
        toolbar.AddItem(item2);
        window.AddControl(toolbar);

        var scope = (IFocusScope)toolbar;
        Assert.Null(scope.GetNextFocus(item1, backward: false));
        Assert.Null(scope.GetNextFocus(item2, backward: false));
        Assert.Null(scope.GetNextFocus(item1, backward: true));
    }

    [Fact]
    public void GetInitialFocus_ReturnsSavedFocus_WhenSet()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var toolbar = new ToolbarControl();
        var item1 = new ButtonControl { Text = "File" };
        var item2 = new ButtonControl { Text = "Edit" };
        toolbar.AddItem(item1);
        toolbar.AddItem(item2);
        window.AddControl(toolbar);

        var scope = (IFocusScope)toolbar;
        scope.SavedFocus = item2;
        Assert.Equal(item2, scope.GetInitialFocus(backward: false));
        Assert.Null(scope.SavedFocus); // consumed after one use
    }

    [Fact]
    public void GetInitialFocus_ReturnsFirstItem_WhenNoSavedFocus()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var toolbar = new ToolbarControl();
        var item1 = new ButtonControl { Text = "File" };
        toolbar.AddItem(item1);
        window.AddControl(toolbar);

        var scope = (IFocusScope)toolbar;
        Assert.Equal(item1, scope.GetInitialFocus(backward: false));
    }

    [Fact]
    public void GetInitialFocus_ReturnsLastItem_WhenBackwardAndNoSavedFocus()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var toolbar = new ToolbarControl();
        var item1 = new ButtonControl { Text = "File" };
        var item2 = new ButtonControl { Text = "Edit" };
        toolbar.AddItem(item1);
        toolbar.AddItem(item2);
        window.AddControl(toolbar);

        var scope = (IFocusScope)toolbar;
        Assert.Equal(item2, scope.GetInitialFocus(backward: true));
    }

    [Fact]
    public void GetInitialFocus_ReturnsSavedFocus_IgnoresBackwardDirection()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var toolbar = new ToolbarControl();
        var item1 = new ButtonControl { Text = "File" };
        var item2 = new ButtonControl { Text = "Edit" };
        toolbar.AddItem(item1);
        toolbar.AddItem(item2);
        window.AddControl(toolbar);

        var scope = (IFocusScope)toolbar;
        scope.SavedFocus = item2;
        // Even with backward=true, SavedFocus is returned (direction doesn't matter when saved)
        Assert.Equal(item2, scope.GetInitialFocus(backward: true));
        Assert.Null(scope.SavedFocus); // consumed after one use
    }
}
