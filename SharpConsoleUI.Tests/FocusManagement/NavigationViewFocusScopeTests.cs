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

public class NavigationViewFocusScopeTests
{
    [Fact]
    public void GetInitialFocus_Forward_EntersNavPane()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);

        // Nav pane contains only MarkupControl items which are not focusable.
        // Add a button directly to the nav scroll panel to make it focusable.
        var navButton = new ButtonControl { Text = "NavItem" };
        nav.NavScrollPanel.AddControl(navButton);

        var scope = (IFocusScope)nav;
        var initial = scope.GetInitialFocus(backward: false);

        // Initial focus should land inside the nav pane (left pane), not the content pane
        Assert.NotNull(initial);
        Assert.True(nav.IsInNavPane(initial!));
    }

    [Fact]
    public void GetInitialFocus_Backward_EntersContentPane()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);

        // Add a button to the content panel so it has a focusable child
        var contentButton = new ButtonControl { Text = "ContentItem" };
        nav.ContentPanel.AddControl(contentButton);

        var scope = (IFocusScope)nav;
        var initial = scope.GetInitialFocus(backward: true);

        Assert.NotNull(initial);
        Assert.True(nav.IsInContentPane(initial!));
    }

    [Fact]
    public void GetNextFocus_FromNavPane_ReturnsContentPane()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);

        var scope = (IFocusScope)nav;
        // _navScrollPanel itself is passed (the child scope that exhausted)
        var next = scope.GetNextFocus(nav.NavScrollPanel, backward: false);
        Assert.Equal(nav.ContentPanel, next);
    }

    [Fact]
    public void GetNextFocus_Backward_FromNavPane_ReturnsNull_ToExitNavigationView()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);

        var scope = (IFocusScope)nav;
        // Shift+Tab from nav pane exhausts backward — exits NavigationView
        Assert.Null(scope.GetNextFocus(nav.NavScrollPanel, backward: true));
    }

    [Fact]
    public void GetNextFocus_Backward_FromContentPane_ReturnsNavScrollPanel()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);

        var scope = (IFocusScope)nav;
        var next = scope.GetNextFocus(nav.ContentPanel, backward: true);
        Assert.Equal(nav.NavScrollPanel, next);
    }

    [Fact]
    public void NavigationView_ImplementsIFocusScope()
    {
        var nav = new NavigationView();
        Assert.IsAssignableFrom<IFocusScope>(nav);
    }

    [Fact]
    public void SavedFocus_CanBeSetAndRead()
    {
        var nav = new NavigationView();
        var scope = (IFocusScope)nav;
        var button = new ButtonControl { Text = "B" };
        scope.SavedFocus = button;
        Assert.Equal(button, scope.SavedFocus);
        // NavigationView ignores SavedFocus (does not consume it) — just verify it stores
        scope.SavedFocus = null;
        Assert.Null(scope.SavedFocus);
    }

    // --- Bug regression tests (currently failing) ---

    /// <summary>
    /// Bug: After the last control in the content panel, Tab should wrap back to the nav pane,
    /// but GetNextFocus returns null instead, causing focus to exit NavigationView entirely.
    /// </summary>
    [Fact]
    public void GetNextFocus_Forward_FromContentPane_WrapsToNavScrollPanel()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);

        var scope = (IFocusScope)nav;
        // After the content panel exhausts its children, NavigationView should wrap to nav pane
        var next = scope.GetNextFocus(nav.ContentPanel, backward: false);
        Assert.Equal(nav.NavScrollPanel, next);
    }

    /// <summary>
    /// Bug: When the content toolbar is visible (has items), Tab from the nav pane should
    /// visit the toolbar before the content panel, but GetNextFocus skips it entirely.
    /// </summary>
    [Fact]
    public void GetNextFocus_Forward_FromNavPane_WithToolbar_ReturnsContentToolbar()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);
        nav.AddContentToolbarButton("Save", null); // makes toolbar visible & focusable

        var scope = (IFocusScope)nav;
        var next = scope.GetNextFocus(nav.NavScrollPanel, backward: false);
        Assert.Equal(nav.ContentToolbar, next);
    }

    /// <summary>
    /// Bug: Tab from the toolbar should continue to the content panel.
    /// </summary>
    [Fact]
    public void GetNextFocus_Forward_FromContentToolbar_ReturnsContentPanel()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);
        nav.AddContentToolbarButton("Save", null);

        var scope = (IFocusScope)nav;
        var next = scope.GetNextFocus(nav.ContentToolbar, backward: false);
        Assert.Equal(nav.ContentPanel, next);
    }

    /// <summary>
    /// Bug: Shift+Tab from the content panel with a visible toolbar should return the toolbar,
    /// not the nav pane.
    /// </summary>
    [Fact]
    public void GetNextFocus_Backward_FromContentPane_WithToolbar_ReturnsContentToolbar()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);
        nav.AddContentToolbarButton("Save", null);

        var scope = (IFocusScope)nav;
        var next = scope.GetNextFocus(nav.ContentPanel, backward: true);
        Assert.Equal(nav.ContentToolbar, next);
    }

    /// <summary>
    /// Bug: Shift+Tab from the content toolbar should return the nav pane.
    /// </summary>
    [Fact]
    public void GetNextFocus_Backward_FromContentToolbar_ReturnsNavScrollPanel()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var nav = new NavigationView();
        window.AddControl(nav);
        nav.AddContentToolbarButton("Save", null);

        var scope = (IFocusScope)nav;
        var next = scope.GetNextFocus(nav.ContentToolbar, backward: true);
        Assert.Equal(nav.NavScrollPanel, next);
    }
}
