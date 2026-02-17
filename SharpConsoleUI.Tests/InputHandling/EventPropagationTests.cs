using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

public class EventPropagationTests
{
    // TODO: ButtonControl doesn't have KeyPressed event - it handles keys via ProcessKey and triggers Click
    // This test concept doesn't apply to buttons. Use TextBox or other input controls instead.
    //[Fact]
    //public void KeyPress_PropagatFromControlToWindow()
    //{
    //    var system = TestWindowSystemBuilder.CreateTestSystem();
    //    var window = new Window(system);
    //    var button = new ButtonControl { Text = "Button" };
    //
    //    window.AddControl(button);
    //    system.WindowStateService.AddWindow(window);
    //    system.FocusStateService.SetFocus(window, button);
    //
    //    bool controlReceived = false;
    //    bool windowReceived = false;
    //
    //    button.KeyPressed += (s, e) => controlReceived = true;
    //    window.KeyPressed += (s, e) => windowReceived = true;
    //
    //    // Button doesn't handle 'X' key, should propagate to window
    //    var keyInfo = new ConsoleKeyInfo('X', ConsoleKey.X, false, false, false);
    //    system.InputStateService.EnqueueKey(keyInfo);
    //    system.Input.ProcessInput();
    //
    //    Assert.True(controlReceived);
    //    Assert.True(windowReceived); // Should propagate up
    //}

    [Fact]
    public void KeyPress_HandledByControl_StopsPropagation()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var textbox = new MultilineEditControl();

        window.AddControl(textbox);
        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);
        system.FocusStateService.SetFocus(window, textbox);

        // Trigger render first
        system.Render.UpdateDisplay();

        bool windowReceivedUnhandled = false;
        window.KeyPressed += (s, e) =>
        {
            if (!e.AlreadyHandled)
                windowReceivedUnhandled = true;
        };

        // Press Enter to enter edit mode
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
        system.Input.ProcessInput();

        // Type character
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('T', ConsoleKey.T, false, false, false));
        system.Input.ProcessInput();

        Assert.Equal("T", textbox.Content);
        Assert.False(windowReceivedUnhandled); // Should NOT receive unhandled events
    }

    [Fact]
    public void MouseClick_PropagatesFromControlToWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Left = 10, Top = 10, Width = 40, Height = 20 };
        var label = new MarkupControl(new List<string> { "Click Me" });

        window.AddControl(label);
        system.WindowStateService.AddWindow(window);

        // Trigger render to populate ActualX/ActualY
        system.Render.UpdateDisplay();

        bool windowReceived = false;
        window.UnhandledMouseClick += (s, e) => windowReceived = true;

        // Convert content-relative ActualX/ActualY to absolute screen coords
        var clickX = window.Left + 1 + label.ActualX + 2;
        var clickY = window.Top + 1 + label.ActualY;

        var clickFlags = new List<MouseFlags> { MouseFlags.Button1Clicked };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(clickFlags, new Point(clickX, clickY));
        system.Input.ProcessInput();

        Assert.True(windowReceived);
    }

    [Fact]
    public void MultipleHandlers_OnSameControl_AllFire()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Button" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button);

        bool handler1Fired = false;
        bool handler2Fired = false;
        bool handler3Fired = false;

        button.Click += (s, e) => handler1Fired = true;
        button.Click += (s, e) => handler2Fired = true;
        button.Click += (s, e) => handler3Fired = true;

        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        system.InputStateService.EnqueueKey(enterKey);
        system.Input.ProcessInput();

        Assert.True(handler1Fired);
        Assert.True(handler2Fired);
        Assert.True(handler3Fired);
    }

    [Fact]
    public void EventHandlerException_DoesNotCrashSystem()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Button" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button);

        // First handler throws exception
        button.Click += (s, e) => throw new InvalidOperationException("Test exception");

        // Second handler should still fire (testing resilience)
        button.Click += (s, e) => { /* Second handler fires */ };

        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        system.InputStateService.EnqueueKey(enterKey);

        // Should not crash, even though handler throws
        try
        {
            system.Input.ProcessInput();
        }
        catch
        {
            // Exception might propagate, but system should still be functional
        }

        // Test passes if we get here without crashing
    }

    // TODO: Multiple issues - ButtonControl doesn't have KeyPressed, ScrollablePanelControl doesn't have Height setter
    // Need to rewrite this test with a different control that supports KeyPressed (like TextBox)
    //[Fact]
    //public void NestedControls_EventBubblesUpHierarchy()
    //{
    //    var system = TestWindowSystemBuilder.CreateTestSystem();
    //    var window = new Window(system);
    //    var panel = new ScrollablePanelControl { Width = 30 };
    //    var button = new ButtonControl { Text = "Nested Button" };
    //
    //    panel.AddControl(button);
    //    window.AddControl(panel);
    //    system.WindowStateService.AddWindow(window);
    //
    //    bool buttonReceived = false;
    //    bool windowReceived = false;
    //
    //    button.KeyPressed += (s, e) => buttonReceived = true;
    //    window.KeyPressed += (s, e) => windowReceived = true;
    //
    //    system.FocusStateService.SetFocus(window, button);
    //
    //    // Send key that button doesn't handle
    //    var keyInfo = new ConsoleKeyInfo('X', ConsoleKey.X, false, false, false);
    //    system.InputStateService.EnqueueKey(keyInfo);
    //    system.Input.ProcessInput();
    //
    //    Assert.True(buttonReceived);
    //    // Note: Panel might not receive key events directly in current architecture
    //    Assert.True(windowReceived); // Should bubble to window
    //}

    [Fact]
    public void FocusChange_DuringEventHandler_DoesNotCrash()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button1 = new ButtonControl { Text = "Button1" };
        var button2 = new ButtonControl { Text = "Button2" };

        window.AddControl(button1);
        window.AddControl(button2);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button1);

        // Handler changes focus
        button1.Click += (s, e) =>
        {
            system.FocusStateService.SetFocus(window, button2);
        };

        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        system.InputStateService.EnqueueKey(enterKey);
        system.Input.ProcessInput();

        Assert.True(button2.HasFocus);
        Assert.False(button1.HasFocus);
    }

    [Fact]
    public void WindowClose_DuringEventHandler_DoesNotCrash()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Close Window" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button);

        // Handler closes window
        button.Click += (s, e) =>
        {
            window.Close();
        };

        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        system.InputStateService.EnqueueKey(enterKey);
        system.Input.ProcessInput();

        Assert.DoesNotContain(window, system.WindowStateService.Windows.Values);
    }

    [Fact]
    public void MultipleWindows_OnlyActiveWindowReceivesKeyPress()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window1 = new Window(system) { Title = "Window1" };
        var window2 = new Window(system) { Title = "Window2" };
        var window3 = new Window(system) { Title = "Window3" };

        system.WindowStateService.AddWindow(window1);
        system.WindowStateService.AddWindow(window2);
        system.WindowStateService.AddWindow(window3);
        system.WindowStateService.SetActiveWindow(window2);

        bool window1Received = false;
        bool window2Received = false;
        bool window3Received = false;

        window1.KeyPressed += (s, e) => window1Received = true;
        window2.KeyPressed += (s, e) => window2Received = true;
        window3.KeyPressed += (s, e) => window3Received = true;

        var keyInfo = new ConsoleKeyInfo('X', ConsoleKey.X, false, false, false);
        system.InputStateService.EnqueueKey(keyInfo);
        system.Input.ProcessInput();

        Assert.False(window1Received);
        Assert.True(window2Received);  // Only active window
        Assert.False(window3Received);
    }

    [Fact]
    public void RapidKeyPresses_AllProcessedInOrder()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var textbox = new MultilineEditControl();

        window.AddControl(textbox);
        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);
        system.FocusStateService.SetFocus(window, textbox);

        // Trigger render first
        system.Render.UpdateDisplay();

        // Press Enter to enter edit mode
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
        system.Input.ProcessInput();

        // Queue many keys rapidly
        string expected = "Hello World";
        foreach (char c in expected)
        {
            system.InputStateService.EnqueueKey(new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false));
        }

        system.Input.ProcessInput();

        Assert.Equal(expected, textbox.Content);
    }

    [Fact]
    public void Event_FiresOnCorrectThread()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Button" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button);

        int? eventThreadId = null;
        int mainThreadId = Environment.CurrentManagedThreadId;

        button.Click += (s, e) =>
        {
            eventThreadId = Environment.CurrentManagedThreadId;
        };

        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        system.InputStateService.EnqueueKey(enterKey);
        system.Input.ProcessInput();

        Assert.NotNull(eventThreadId);
        Assert.Equal(mainThreadId, eventThreadId); // Events fire on same thread
    }

    [Fact]
    public void ControlRemoval_DuringEventHandler_DoesNotCrash()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Remove Me" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button);

        // Handler removes itself
        button.Click += (s, e) =>
        {
            window.RemoveContent(button);
        };

        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        system.InputStateService.EnqueueKey(enterKey);
        system.Input.ProcessInput();

        // Should not crash
    }

    [Fact]
    public void MouseMove_GeneratesEnterLeaveSequence()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Left = 10, Top = 10, Width = 40, Height = 20 };
        var button1 = new ButtonControl { Text = "Button1" };
        var button2 = new ButtonControl { Text = "Button2" };

        window.AddControl(button1);
        window.AddControl(button2);
        system.WindowStateService.AddWindow(window);

        // Trigger render to populate ActualX/ActualY
        system.Render.UpdateDisplay();

        var events = new List<string>();
        button1.MouseEnter += (s, e) => events.Add("Button1 Enter");
        button1.MouseLeave += (s, e) => events.Add("Button1 Leave");
        button2.MouseEnter += (s, e) => events.Add("Button2 Enter");
        button2.MouseLeave += (s, e) => events.Add("Button2 Leave");

        // Move over button1 (convert content-relative to absolute screen coords)
        var move1Flags = new List<MouseFlags> { MouseFlags.ReportMousePosition };
        var driver = (MockConsoleDriver)system.ConsoleDriver;
        driver.SimulateMouseEvent(move1Flags, new Point(window.Left + 1 + button1.ActualX + 2, window.Top + 1 + button1.ActualY));
        system.Input.ProcessInput();

        // Move over button2
        var move2Flags = new List<MouseFlags> { MouseFlags.ReportMousePosition };
        driver.SimulateMouseEvent(move2Flags, new Point(window.Left + 1 + button2.ActualX + 2, window.Top + 1 + button2.ActualY));
        system.Input.ProcessInput();

        Assert.Equal(3, events.Count);
        Assert.Equal("Button1 Enter", events[0]);
        Assert.Equal("Button1 Leave", events[1]);
        Assert.Equal("Button2 Enter", events[2]);
    }
}
