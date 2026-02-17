using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

public class KeyboardEventTests
{
    [Fact]
    public void KeyPress_RoutesToActiveWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window1 = new Window(system) { Title = "Window 1" };
        var window2 = new Window(system) { Title = "Window 2" };

        system.WindowStateService.AddWindow(window1);
        system.WindowStateService.AddWindow(window2);
        system.WindowStateService.SetActiveWindow(window2);

        bool window1KeyPressed = false;
        bool window2KeyPressed = false;

        window1.KeyPressed += (s, e) => window1KeyPressed = true;
        window2.KeyPressed += (s, e) => window2KeyPressed = true;

        // Simulate key press via InputCoordinator
        var keyInfo = new ConsoleKeyInfo('A', ConsoleKey.A, false, false, false);
        system.InputStateService.EnqueueKey(keyInfo);
        system.Input.ProcessInput();

        Assert.False(window1KeyPressed); // Inactive window doesn't get event
        Assert.True(window2KeyPressed);  // Active window gets event
    }

    [Fact]
    public void KeyPress_RoutesToFocusedControl()
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

        // Type character
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('X', ConsoleKey.X, false, false, false));
        system.Input.ProcessInput();

        Assert.Equal("X", textbox.Content);
    }

    [Fact]
    public void KeyPress_HandledByControl_DoesNotPropagateToWindow()
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
        Assert.False(windowReceivedUnhandled); // Should not receive unhandled events
    }

    [Fact]
    public void KeyPress_NotHandledByControl_PropagatesToWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Button" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button);

        bool windowKeyPressed = false;
        ConsoleKeyInfo? capturedKey = null;
        window.KeyPressed += (s, e) =>
        {
            windowKeyPressed = true;
            capturedKey = e.KeyInfo;
        };

        // Button doesn't handle 'A' key, should propagate
        var keyInfo = new ConsoleKeyInfo('A', ConsoleKey.A, false, false, false);
        system.InputStateService.EnqueueKey(keyInfo);
        system.Input.ProcessInput();

        Assert.True(windowKeyPressed);
        Assert.NotNull(capturedKey);
        Assert.Equal(ConsoleKey.A, capturedKey.Value.Key);
    }

    [Fact]
    public void Tab_SwitchesFocusToNextControl()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button1 = new ButtonControl { Text = "Button1" };
        var button2 = new ButtonControl { Text = "Button2" };

        window.AddControl(button1);
        window.AddControl(button2);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button1);

        var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);
        system.InputStateService.EnqueueKey(tabKey);
        system.Input.ProcessInput();

        Assert.True(button2.HasFocus);
        Assert.False(button1.HasFocus);
    }

    [Fact]
    public void ShiftTab_SwitchesFocusToPreviousControl()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button1 = new ButtonControl { Text = "Button1" };
        var button2 = new ButtonControl { Text = "Button2" };

        window.AddControl(button1);
        window.AddControl(button2);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button2);

        var shiftTabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false);
        system.InputStateService.EnqueueKey(shiftTabKey);
        system.Input.ProcessInput();

        Assert.True(button1.HasFocus);
        Assert.False(button2.HasFocus);
    }

    [Fact]
    public void CtrlT_CyclesActiveWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window1 = new Window(system) { Title = "Window1" };
        var window2 = new Window(system) { Title = "Window2" };

        system.WindowStateService.AddWindow(window1);
        system.WindowStateService.AddWindow(window2);
        system.WindowStateService.SetActiveWindow(window1);

        var ctrlT = new ConsoleKeyInfo('t', ConsoleKey.T, false, false, true);
        system.InputStateService.EnqueueKey(ctrlT);
        system.Input.ProcessInput();

        Assert.Equal(window2, system.WindowStateService.ActiveWindow);
    }

    [Fact]
    public void AltNumber_ActivatesWindowByIndex()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window1 = new Window(system) { Title = "Window1" };
        var window2 = new Window(system) { Title = "Window2" };
        var window3 = new Window(system) { Title = "Window3" };

        system.WindowStateService.AddWindow(window1);
        system.WindowStateService.AddWindow(window2);
        system.WindowStateService.AddWindow(window3);
        system.WindowStateService.SetActiveWindow(window1);

        // Alt+2 should activate window2 (0-indexed)
        var alt2 = new ConsoleKeyInfo('2', ConsoleKey.D2, false, true, false);
        system.InputStateService.EnqueueKey(alt2);
        system.Input.ProcessInput();

        Assert.Equal(window2, system.WindowStateService.ActiveWindow);
    }

    [Fact]
    public void UpArrow_ScrollsWindowContent()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Height = 10 };

        // Add enough content to make window scrollable
        for (int i = 0; i < 20; i++)
        {
            window.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));
        }

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Trigger render so layout is computed
        system.Render.UpdateDisplay();

        // Scroll down first by setting offset directly
        window.ScrollOffset = 10;
        var initialScroll = window.ScrollOffset;

        // Press Up arrow
        var upKey = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false);
        system.InputStateService.EnqueueKey(upKey);
        system.Input.ProcessInput();

        Assert.True(window.ScrollOffset < initialScroll);
    }

    [Fact]
    public void DownArrow_ScrollsWindowContent()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Height = 10 };

        // Add enough content to make window scrollable
        for (int i = 0; i < 20; i++)
        {
            window.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));
        }

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        var initialScroll = window.ScrollOffset;

        // Press Down arrow
        var downKey = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);
        system.InputStateService.EnqueueKey(downKey);
        system.Input.ProcessInput();

        Assert.True(window.ScrollOffset > initialScroll);
    }

    [Fact]
    public void ModifierKeys_Ctrl_DetectedCorrectly()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);

        ConsoleModifiers? capturedModifiers = null;
        window.KeyPressed += (s, e) =>
        {
            capturedModifiers = e.KeyInfo.Modifiers;
        };

        var ctrlA = new ConsoleKeyInfo('a', ConsoleKey.A, false, false, true);
        system.InputStateService.EnqueueKey(ctrlA);
        system.Input.ProcessInput();

        Assert.NotNull(capturedModifiers);
        Assert.True(capturedModifiers.Value.HasFlag(ConsoleModifiers.Control));
    }

    [Fact]
    public void ModifierKeys_Alt_DetectedCorrectly()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);

        ConsoleModifiers? capturedModifiers = null;
        window.KeyPressed += (s, e) =>
        {
            capturedModifiers = e.KeyInfo.Modifiers;
        };

        var altA = new ConsoleKeyInfo('a', ConsoleKey.A, false, true, false);
        system.InputStateService.EnqueueKey(altA);
        system.Input.ProcessInput();

        Assert.NotNull(capturedModifiers);
        Assert.True(capturedModifiers.Value.HasFlag(ConsoleModifiers.Alt));
    }

    [Fact]
    public void ModifierKeys_Shift_DetectedCorrectly()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);

        ConsoleModifiers? capturedModifiers = null;
        window.KeyPressed += (s, e) =>
        {
            capturedModifiers = e.KeyInfo.Modifiers;
        };

        var shiftA = new ConsoleKeyInfo('A', ConsoleKey.A, true, false, false);
        system.InputStateService.EnqueueKey(shiftA);
        system.Input.ProcessInput();

        Assert.NotNull(capturedModifiers);
        Assert.True(capturedModifiers.Value.HasFlag(ConsoleModifiers.Shift));
    }

    [Fact]
    public void ModifierKeys_MultipleModifiers_DetectedCorrectly()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);

        ConsoleModifiers? capturedModifiers = null;
        window.KeyPressed += (s, e) =>
        {
            capturedModifiers = e.KeyInfo.Modifiers;
        };

        // Ctrl+Shift+A
        var ctrlShiftA = new ConsoleKeyInfo('A', ConsoleKey.A, true, false, true);
        system.InputStateService.EnqueueKey(ctrlShiftA);
        system.Input.ProcessInput();

        Assert.NotNull(capturedModifiers);
        Assert.True(capturedModifiers.Value.HasFlag(ConsoleModifiers.Control));
        Assert.True(capturedModifiers.Value.HasFlag(ConsoleModifiers.Shift));
    }

    [Fact]
    public void NoActiveWindow_KeyPress_DoesNotCrash()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();

        // No windows added, no active window
        var keyInfo = new ConsoleKeyInfo('A', ConsoleKey.A, false, false, false);
        system.InputStateService.EnqueueKey(keyInfo);

        // Should not crash
        system.Input.ProcessInput();

        // Test passes if no exception thrown
    }

    [Fact]
    public void Enter_OnButton_TriggersClick()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Click Me" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);
        system.FocusStateService.SetFocus(window, button);

        bool clicked = false;
        button.Click += (s, e) => clicked = true;

        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        system.InputStateService.EnqueueKey(enterKey);
        system.Input.ProcessInput();

        Assert.True(clicked);
    }

    [Fact]
    public void Space_OnButton_TriggersClick()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Click Me" };

        window.AddControl(button);
        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);
        system.FocusStateService.SetFocus(window, button);

        bool clicked = false;
        button.Click += (s, e) => clicked = true;

        var spaceKey = new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false);
        system.InputStateService.EnqueueKey(spaceKey);
        system.Input.ProcessInput();

        Assert.True(clicked);
    }

    [Fact]
    public void MultipleKeysQueued_ProcessedInOrder()
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

        // Queue multiple keys
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('H', ConsoleKey.H, false, false, false));
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('i', ConsoleKey.I, false, false, false));

        system.Input.ProcessInput();

        Assert.Equal("Hi", textbox.Content);
    }
}
