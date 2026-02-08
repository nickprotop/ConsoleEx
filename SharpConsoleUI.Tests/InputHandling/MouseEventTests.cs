using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

public class MouseEventTests
{
    [Fact]
    public void MouseClick_ActivatesWindowAtPosition()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window1 = new Window(system) { Top = 10, Width = 30, Height = 20, Title = "Window1" };
        var window2 = new Window(system) { Top = 10, Width = 30, Height = 20, Title = "Window2" };

        system.WindowStateService.AddWindow(window1);
        system.WindowStateService.AddWindow(window2);
        system.WindowStateService.SetActiveWindow(window1);

        // Click on window2
        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(60, 15));
        system.Input.ProcessInput();

        Assert.Equal(window2, system.WindowStateService.ActiveWindow);
    }

    [Fact]
    public void MouseClick_OnButton_TriggersClickEvent()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20 };
        var button = new ButtonControl { Text = "Click Me" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Trigger render to populate ActualX/ActualY
        system.Render.UpdateDisplay();

        // Set focus to the button so it can receive mouse events
        system.FocusStateService.SetFocus(window, button);

        bool clicked = false;
        button.Click += (s, e) => clicked = true;

        // Calculate absolute position using rendered coordinates
        var clickX = button.ActualX + 2; // Click near center of button
        var clickY = button.ActualY;

        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(clickX, clickY));
        system.Input.ProcessInput();

        Assert.True(clicked);
    }

    // TODO: ListControl doesn't have ItemDoubleClick event yet
    //[Fact]
    //public void MouseDoubleClick_OnControl_TriggersDoubleClickEvent()
    //{
    //    var system = TestWindowSystemBuilder.CreateTestSystem();
    //    var window = new Window(system) { Top = 10, Width = 40, Height = 20 };
    //    var list = new ListControl();
    //    list.AddItem("Item 1");
    //    list.AddItem("Item 2");
    //
    //    window.AddControl(list);
    //    system.WindowStateService.AddWindow(window);
    //
    //    // Trigger render to populate ActualX/ActualY
    //    system.Render.UpdateDisplay();
    //
    //    bool doubleClicked = false;
    //    list.ItemDoubleClick += (s, e) => doubleClicked = true;
    //
    //    var clickX = list.ActualX + 2;
    //    var clickY = list.ActualY;
    //
    //    // First click
    //    var click1Flags = new List<MouseFlags> { MouseFlags.Button1Clicked };
    //    var driver = (MockConsoleDriver)system.ConsoleDriver;
    //    driver.SimulateMouseEvent(click1Flags, new Point(clickX, clickY));
    //    system.Input.ProcessInput();
    //
    //    // Second click within threshold (simulated by MouseFlags.Button1DoubleClicked)
    //    var doubleClickFlags = new List<MouseFlags> { MouseFlags.Button1DoubleClicked };
    //    driver.SimulateMouseEvent(doubleClickFlags, new Point(clickX, clickY));
    //    system.Input.ProcessInput();
    //
    //    Assert.True(doubleClicked);
    //}

    [Fact]
    public void MouseClick_OnTitleBar_StartsDrag()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, IsMovable = true };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Click on title bar (Y = window.Top, which is the title bar)
        var pressFlags = new List<MouseFlags> { MouseFlags.Button1Pressed };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(pressFlags, new Point(20, 10));
        system.Input.ProcessInput();

        Assert.True(system.WindowStateService.IsDragging);
        Assert.Equal(window, system.WindowStateService.CurrentDrag?.Window);
    }

    [Fact]
    public void MouseDrag_MovesWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, IsMovable = true };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Start drag on title bar
        var pressFlags = new List<MouseFlags> { MouseFlags.Button1Pressed };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(pressFlags, new Point(20, 10));
        system.Input.ProcessInput();

        var initialX = window.Left;
        var initialY = window.Top;

        // Move mouse while dragging
        var moveFlags = new List<MouseFlags> { MouseFlags.ReportMousePosition };
        driver.SimulateMouseEvent(moveFlags, new Point(30, 20));
        system.Input.ProcessInput();

        // Release
        var releaseFlags = new List<MouseFlags> { MouseFlags.Button1Released };
        driver.SimulateMouseEvent(releaseFlags, new Point(30, 20));
        system.Input.ProcessInput();

        // Window should have moved
        Assert.NotEqual(initialX, window.Left);
        Assert.NotEqual(initialY, window.Top);
    }

    [Fact]
    public void MouseClick_OnCloseButton_ClosesWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, ShowCloseButton = true };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        bool closingFired = false;
        window.OnClosing += (s, e) => closingFired = true;

        // Close button is at top-right of window
        // X = window.Left + window.Width - 2 (close button position)
        var closeButtonX = 10 + 40 - 2;
        var closeButtonY = 10; // Title bar

        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(closeButtonX, closeButtonY));
        system.Input.ProcessInput();

        Assert.True(closingFired);
    }

    [Fact]
    public void MouseClick_OnMaximizeButton_MaximizesWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, IsMaximizable = true };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Maximize button is left of close button
        var maximizeButtonX = 10 + 40 - 4;
        var maximizeButtonY = 10;

        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(maximizeButtonX, maximizeButtonY));
        system.Input.ProcessInput();

        Assert.Equal(WindowState.Maximized, window.State);
    }

    [Fact]
    public void MouseClick_OnMinimizeButton_MinimizesWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, IsMinimizable = true };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Minimize button is left of maximize button
        var minimizeButtonX = 10 + 40 - 6;
        var minimizeButtonY = 10;

        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(minimizeButtonX, minimizeButtonY));
        system.Input.ProcessInput();

        Assert.Equal(WindowState.Minimized, window.State);
    }

    [Fact]
    public void MouseClick_OnBorder_StartsResize()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, IsResizable = true };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Click on bottom-right corner (resize handle)
        var cornerX = 10 + 40 - 1;
        var cornerY = 10 + 20 - 1;

        var pressFlags = new List<MouseFlags> { MouseFlags.Button1Pressed };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(pressFlags, new Point(cornerX, cornerY));
        system.Input.ProcessInput();

        Assert.True(system.WindowStateService.IsResizing);
        Assert.Equal(window, system.WindowStateService.CurrentResize?.Window);
    }

    [Fact]
    public void MouseResize_ChangesWindowDimensions()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, IsResizable = true };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        var initialWidth = window.Width;
        var initialHeight = window.Height;

        // Start resize from bottom-right corner
        var pressFlags = new List<MouseFlags> { MouseFlags.Button1Pressed };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(pressFlags, new Point(49, 29));
        system.Input.ProcessInput();

        // Move mouse to resize
        var moveFlags = new List<MouseFlags> { MouseFlags.ReportMousePosition };
        driver.SimulateMouseEvent(moveFlags, new Point(55, 35));
        system.Input.ProcessInput();

        // Release
        var releaseFlags = new List<MouseFlags> { MouseFlags.Button1Released };
        driver.SimulateMouseEvent(releaseFlags, new Point(55, 35));
        system.Input.ProcessInput();

        Assert.True(window.Width > initialWidth);
        Assert.True(window.Height > initialHeight);
    }

    [Fact]
    public void MouseClick_OnOverlappingWindows_TopmostReceivesEvent()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window1 = new Window(system) { Top = 10, Width = 30, Height = 20, Title = "Window1" };
        var window2 = new Window(system) { Top = 10, Width = 30, Height = 20, Title = "Window2" };

        system.WindowStateService.AddWindow(window1);
        system.WindowStateService.AddWindow(window2); // window2 is on top
        system.WindowStateService.SetActiveWindow(window2);

        bool window1Clicked = false;
        bool window2Clicked = false;

        window1.UnhandledMouseClick += (s, e) => window1Clicked = true;
        window2.UnhandledMouseClick += (s, e) => window2Clicked = true;

        // Click in overlapping area (X=25)
        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(25, 15));
        system.Input.ProcessInput();

        Assert.False(window1Clicked);
        Assert.True(window2Clicked); // Topmost window gets the click
    }

    [Fact]
    public void MouseClick_OnEmptyDesktop_DeactivatesActiveWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 30, Height = 20 };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Click on empty desktop area
        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(100, 100));
        system.Input.ProcessInput();

        Assert.Null(system.WindowStateService.ActiveWindow);
    }

    [Fact]
    public void MouseEnter_OnControl_FiresEnterEvent()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20 };
        var button = new ButtonControl { Text = "Hover Me" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Trigger render to populate ActualX/ActualY
        system.Render.UpdateDisplay();

        bool mouseEntered = false;
        button.MouseEnter += (s, e) => mouseEntered = true;

        // Move mouse over button
        var clickX = button.ActualX + 2;
        var clickY = button.ActualY;

        var moveFlags = new List<MouseFlags> { MouseFlags.ReportMousePosition };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(moveFlags, new Point(clickX, clickY));
        system.Input.ProcessInput();

        Assert.True(mouseEntered);
    }

    [Fact]
    public void MouseLeave_OnControl_FiresLeaveEvent()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20 };
        var button = new ButtonControl { Text = "Hover Me" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Trigger render to populate ActualX/ActualY
        system.Render.UpdateDisplay();

        bool mouseLeft = false;
        button.MouseLeave += (s, e) => mouseLeft = true;

        var clickX = button.ActualX + 2;
        var clickY = button.ActualY;

        // Enter button
        var enterFlags = new List<MouseFlags> { MouseFlags.ReportMousePosition };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(enterFlags, new Point(clickX, clickY));
        system.Input.ProcessInput();

        // Leave button
        var leaveFlags = new List<MouseFlags> { MouseFlags.ReportMousePosition };
        driver.SimulateMouseEvent(leaveFlags, new Point(clickX + 20, clickY));
        system.Input.ProcessInput();

        Assert.True(mouseLeft);
    }

    [Fact]
    public void MouseClick_OnDisabledButton_DoesNotTriggerClick()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20 };
        var button = new ButtonControl { Text = "Disabled", IsEnabled = false };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);

        // Trigger render to populate ActualX/ActualY
        system.Render.UpdateDisplay();

        bool clicked = false;
        button.Click += (s, e) => clicked = true;

        var clickX = button.ActualX + 2;
        var clickY = button.ActualY;

        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(clickX, clickY));
        system.Input.ProcessInput();

        Assert.False(clicked);
    }

    [Fact]
    public void MouseClick_OnNonMovableWindow_DoesNotStartDrag()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, IsMovable = false };

        system.WindowStateService.AddWindow(window);

        // Click on title bar
        var pressFlags = new List<MouseFlags> { MouseFlags.Button1Pressed };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(pressFlags, new Point(20, 10));
        system.Input.ProcessInput();

        Assert.False(system.WindowStateService.IsDragging);
    }

    [Fact]
    public void MouseClick_OnNonResizableWindow_DoesNotStartResize()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, IsResizable = false };

        system.WindowStateService.AddWindow(window);

        // Click on corner
        var pressFlags = new List<MouseFlags> { MouseFlags.Button1Pressed };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(pressFlags, new Point(49, 29));
        system.Input.ProcessInput();

        Assert.False(system.WindowStateService.IsResizing);
    }

    [Fact]
    public void MouseClick_DuringModalWindow_BlockedForParentWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Top = 10, Width = 40, Height = 20 };
        var modalWindow = new Window(system) { Top = 15, Width = 30, Height = 15, IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, null); // Orphan modal blocks everything

        bool parentClicked = false;
        parentWindow.UnhandledMouseClick += (s, e) => parentClicked = true;

        // Try to click on parent window
        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(20, 15));
        system.Input.ProcessInput();

        // Parent window should not receive click because modal is active
        Assert.False(parentClicked);
    }
}
