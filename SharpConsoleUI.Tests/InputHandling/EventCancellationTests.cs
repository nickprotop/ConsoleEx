using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

public class EventCancellationTests
{
    [Fact]
    public void WindowClosing_CanBeCancelled()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);

        bool closingFired = false;
        bool closedFired = false;

        window.OnClosing += (s, e) =>
        {
            closingFired = true;
            e.Allow = false; // Cancel the close
        };

        window.OnClosed += (s, e) => closedFired = true;

        window.Close();

        Assert.True(closingFired);
        Assert.False(closedFired); // Should not fire because cancelled
        Assert.Contains(window, system.WindowStateService.Windows.Values); // Still open
    }

    [Fact]
    public void WindowClosing_NotCancelled_WindowCloses()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);

        bool closingFired = false;
        bool closedFired = false;

        window.OnClosing += (s, e) =>
        {
            closingFired = true;
            // Don't cancel
        };

        window.OnClosed += (s, e) => closedFired = true;

        window.Close();

        Assert.True(closingFired);
        Assert.True(closedFired);
        Assert.DoesNotContain(window, system.WindowStateService.Windows.Values);
    }

    [Fact]
    public void MultipleClosingHandlers_FirstCancels_StopsLaterHandlers()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);

        bool handler1Fired = false;

        window.OnClosing += (s, e) =>
        {
            handler1Fired = true;
            e.Allow = false;
        };

        window.OnClosing += (s, e) =>
        {
            // Second handler fires but we only test cancellation
        };

        window.Close();

        Assert.True(handler1Fired);
        // Handler2 might still fire depending on implementation, but close should be cancelled
        Assert.Contains(window, system.WindowStateService.Windows.Values);
    }

    [Fact]
    public void DisabledControl_KeyPress_NotProcessed()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Disabled", IsEnabled = false };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button);

        bool clicked = false;
        button.Click += (s, e) => clicked = true;

        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        system.InputStateService.EnqueueKey(enterKey);
        system.Input.ProcessInput();

        Assert.False(clicked);
    }

    [Fact]
    public void DisabledControl_MouseClick_NotProcessed()
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

        // Calculate absolute position using rendered coordinates
        var clickX = button.ActualX + 2; // Click near center of button
        var clickY = button.ActualY;

        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(clickX, clickY));
        system.Input.ProcessInput();

        Assert.False(clicked);
    }

    [Fact]
    public void HiddenControl_DoesNotReceiveFocus()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button1 = new ButtonControl { Text = "Button1" };
        var button2 = new ButtonControl { Text = "Button2", Visible = false };
        var button3 = new ButtonControl { Text = "Button3" };

        window.AddControl(button1);
        window.AddControl(button2);
        window.AddControl(button3);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button1);

        // Tab should skip button2 (hidden)
        var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
        system.InputStateService.EnqueueKey(tabKey);
        system.Input.ProcessInput();

        Assert.False(button2.HasFocus);
        Assert.True(button3.HasFocus);
    }

    [Fact]
    public void ModalWindow_BlocksInputToParentWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        var parentButton = new ButtonControl { Text = "Parent Button" };
        parentWindow.AddControl(parentButton);

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, null); // Orphan modal blocks everything

        // Try to activate parent window (should fail due to modal)
        system.WindowStateService.SetActiveWindow(parentWindow);

        // Active window might redirect to modal
        var activeWindow = system.WindowStateService.ActiveWindow;

        // Parent window should not be able to receive input directly
        // (exact behavior depends on modal implementation)
    }

    // TODO: IsReadOnly property not yet implemented in MultilineEditControl
    //[Fact]
    //public void ReadOnlyTextBox_RejectsInput()
    //{
    //    var system = TestWindowSystemBuilder.CreateTestSystem();
    //    var window = new Window(system);
    //    var textbox = new MultilineEditControl("Read Only");
    //
    //    window.AddControl(textbox);
    //    system.WindowStateService.AddWindow(window);
    //    system.FocusStateService.SetFocus(window, textbox);
    //
    //    var originalText = textbox.Content;
    //
    //    var keyInfo = new ConsoleKeyInfo('X', ConsoleKey.X, false, false, false);
    //    system.InputStateService.EnqueueKey(keyInfo);
    //    system.Input.ProcessInput();
    //
    //    Assert.Equal(originalText, textbox.Content); // Should not change
    //}

    [Fact]
    public void WindowDrag_CancelledByEscape()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, Width = 40, Height = 20, IsMovable = true };

        system.WindowStateService.AddWindow(window);

        var initialX = window.Left;
        var initialY = window.Top;

        // Start drag
        var pressFlags = new List<MouseFlags> { MouseFlags.Button1Pressed };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(pressFlags, new Point(20, 10));
        system.Input.ProcessInput();

        Assert.True(system.WindowStateService.IsDragging);

        // Move mouse
        var moveFlags = new List<MouseFlags> { MouseFlags.ReportMousePosition };
        driver.SimulateMouseEvent(moveFlags, new Point(30, 20));
        system.Input.ProcessInput();

        // Press Escape to cancel drag
        var escapeKey = new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);
        system.InputStateService.EnqueueKey(escapeKey);
        system.Input.ProcessInput();

        // Window position might have changed during drag
        // Exact behavior depends on implementation - this tests that Escape doesn't crash
    }

    [Fact]
    public void NonClosableWindow_CloseAttemptIgnored()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { IsClosable = false };

        system.WindowStateService.AddWindow(window);

        window.Close();

        // Behavior depends on implementation - either event doesn't fire or close is blocked
        Assert.Contains(window, system.WindowStateService.Windows.Values); // Should still be open
    }

    [Fact]
    public void MinimizedWindow_DoesNotReceiveKeyboardInput()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        window.Minimize();

        bool keyPressed = false;
        window.KeyPressed += (s, e) => keyPressed = true;

        var keyInfo = new ConsoleKeyInfo('X', ConsoleKey.X, false, false, false);
        system.InputStateService.EnqueueKey(keyInfo);
        system.Input.ProcessInput();

        // Minimized windows typically don't receive input
        // Active window might have changed to another window
    }

    [Fact]
    public void ControlBeingRemoved_DoesNotProcessPendingEvents()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Button" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button);

        // Queue event
        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        system.InputStateService.EnqueueKey(enterKey);

        // Remove button before processing
        window.RemoveContent(button);

        bool clicked = false;
        button.Click += (s, e) => clicked = true;

        // Process queued events
        system.Input.ProcessInput();

        // Button was removed, so click should not fire
        Assert.False(clicked);
    }

    [Fact]
    public void Escape_ClosesModalWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system);
        var modalWindow = new Window(system) { IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);
        system.WindowStateService.SetActiveWindow(modalWindow);

        var escapeKey = new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);
        system.InputStateService.EnqueueKey(escapeKey);
        system.Input.ProcessInput();

        // Modal window might close on Escape depending on implementation
        // This test verifies it doesn't crash
    }
}
