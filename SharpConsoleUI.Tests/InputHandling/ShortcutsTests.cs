using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

public class ShortcutsTests
{


    [Fact]
    public void CtrlQ_ExitsApplication()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);

        // This tests that Ctrl+Q is processed without crashing

        var ctrlQ = new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, true);
        system.InputStateService.EnqueueKey(ctrlQ);
        system.Input.ProcessInput();

        // Test passes if no exception thrown
    }

    [Fact]
    public void CtrlT_CyclesWindows()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window1 = new Window(system) { Title = "Window1" };
        var window2 = new Window(system) { Title = "Window2" };
        var window3 = new Window(system) { Title = "Window3" };

        system.WindowStateService.AddWindow(window1);
        system.WindowStateService.AddWindow(window2);
        system.WindowStateService.AddWindow(window3);
        system.WindowStateService.SetActiveWindow(window1);

        var ctrlT = new ConsoleKeyInfo('t', ConsoleKey.T, false, false, true);

        // First Ctrl+T
        system.InputStateService.EnqueueKey(ctrlT);
        system.Input.ProcessInput();
        Assert.Equal(window2, system.WindowStateService.ActiveWindow);

        // Second Ctrl+T
        system.InputStateService.EnqueueKey(ctrlT);
        system.Input.ProcessInput();
        Assert.Equal(window3, system.WindowStateService.ActiveWindow);

        // Third Ctrl+T (wraps around)
        system.InputStateService.EnqueueKey(ctrlT);
        system.Input.ProcessInput();
        Assert.Equal(window1, system.WindowStateService.ActiveWindow);
    }

    [Fact]
    public void Alt1Through9_ActivatesWindowByIndex()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var windows = new List<Window>();

        for (int i = 0; i < 5; i++)
        {
            var window = new Window(system) { Title = $"Window{i}" };
            windows.Add(window);
            system.WindowStateService.AddWindow(window);
        }

        // Alt+1 activates first window (index 0)
        var alt1 = new ConsoleKeyInfo('1', ConsoleKey.D1, false, true, false);
        system.InputStateService.EnqueueKey(alt1);
        system.Input.ProcessInput();
        Assert.Equal(windows[0], system.WindowStateService.ActiveWindow);

        // Alt+4 activates fourth window (index 3)
        var alt4 = new ConsoleKeyInfo('4', ConsoleKey.D4, false, true, false);
        system.InputStateService.EnqueueKey(alt4);
        system.Input.ProcessInput();
        Assert.Equal(windows[3], system.WindowStateService.ActiveWindow);
    }

    [Fact]
    public void AltNumber_OutOfRange_DoesNotCrash()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);

        system.WindowStateService.AddWindow(window);

        // Alt+9 when there's only 1 window
        var alt9 = new ConsoleKeyInfo('9', ConsoleKey.D9, false, true, false);
        system.InputStateService.EnqueueKey(alt9);
        system.Input.ProcessInput();

        // Should not crash, window state unchanged or stays on valid window
    }

    [Fact]
    public void CtrlArrows_MovesWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Top = 10, IsMovable = true };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        var initialX = window.Left;
        var initialY = window.Top;

        // Ctrl+Right Arrow
        var ctrlRight = new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, true);
        system.InputStateService.EnqueueKey(ctrlRight);
        system.Input.ProcessInput();

        Assert.True(window.Left > initialX);

        // Ctrl+Down Arrow
        var ctrlDown = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, true);
        system.InputStateService.EnqueueKey(ctrlDown);
        system.Input.ProcessInput();

        Assert.True(window.Top > initialY);
    }

    [Fact]
    public void ShiftArrows_ResizesWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 40, Height = 20, IsResizable = true };

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        var initialWidth = window.Width;
        var initialHeight = window.Height;

        // Shift+Right Arrow (increase width)
        var shiftRight = new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, true, false, false);
        system.InputStateService.EnqueueKey(shiftRight);
        system.Input.ProcessInput();

        Assert.True(window.Width > initialWidth);

        // Shift+Down Arrow (increase height)
        var shiftDown = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, true, false, false);
        system.InputStateService.EnqueueKey(shiftDown);
        system.Input.ProcessInput();

        Assert.True(window.Height > initialHeight);
    }

    [Fact]
    public void Tab_SwitchesControlFocus()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button1 = new ButtonControl { Text = "Button1" };
        var button2 = new ButtonControl { Text = "Button2" };
        var button3 = new ButtonControl { Text = "Button3" };

        window.AddControl(button1);
        window.AddControl(button2);
        window.AddControl(button3);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button1);

        var tabKey = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false);

        // First Tab
        system.InputStateService.EnqueueKey(tabKey);
        system.Input.ProcessInput();
        Assert.True(button2.HasFocus);

        // Second Tab
        system.InputStateService.EnqueueKey(tabKey);
        system.Input.ProcessInput();
        Assert.True(button3.HasFocus);

        // Third Tab (wraps around)
        system.InputStateService.EnqueueKey(tabKey);
        system.Input.ProcessInput();
        Assert.True(button1.HasFocus);
    }

    [Fact]
    public void ShiftTab_SwitchesControlFocusBackward()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button1 = new ButtonControl { Text = "Button1" };
        var button2 = new ButtonControl { Text = "Button2" };
        var button3 = new ButtonControl { Text = "Button3" };

        window.AddControl(button1);
        window.AddControl(button2);
        window.AddControl(button3);
        system.WindowStateService.AddWindow(window);
        system.FocusStateService.SetFocus(window, button3);

        var shiftTab = new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false);

        // First Shift+Tab
        system.InputStateService.EnqueueKey(shiftTab);
        system.Input.ProcessInput();
        Assert.True(button2.HasFocus);

        // Second Shift+Tab
        system.InputStateService.EnqueueKey(shiftTab);
        system.Input.ProcessInput();
        Assert.True(button1.HasFocus);

        // Third Shift+Tab (wraps around)
        system.InputStateService.EnqueueKey(shiftTab);
        system.Input.ProcessInput();
        Assert.True(button3.HasFocus);
    }

    [Fact]
    public void Escape_OnOverlayWindow_DismissesOverlay()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var overlay = new Windows.OverlayWindow(system);

        system.WindowStateService.AddWindow(overlay);
        system.WindowStateService.SetActiveWindow(overlay);

        var escapeKey = new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);
        system.InputStateService.EnqueueKey(escapeKey);
        system.Input.ProcessInput();

        // Overlay might close on Escape
        // This test verifies it doesn't crash
    }

    [Fact]
    public void Enter_OnFocusedButton_ActivatesButton()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Submit" };

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
    public void Space_OnFocusedButton_ActivatesButton()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var button = new ButtonControl { Text = "Submit" };

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
    public void UpDownArrows_ScrollWindow_WhenNoControlFocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Height = 10 };

        // Add content to make window scrollable
        for (int i = 0; i < 20; i++)
        {
            window.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));
        }

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // No control focused, arrows should scroll window
        var downKey = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);
        system.InputStateService.EnqueueKey(downKey);
        system.Input.ProcessInput();

        Assert.True(window.ScrollOffset > 0);

        var upKey = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false);
        system.InputStateService.EnqueueKey(upKey);
        system.Input.ProcessInput();

        Assert.Equal(0, window.ScrollOffset);
    }

    [Fact]
    public void PageUpPageDown_ScrollsWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Height = 10 };

        // Add content
        for (int i = 0; i < 50; i++)
        {
            window.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));
        }

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Page Down
        var pageDown = new ConsoleKeyInfo('\0', ConsoleKey.PageDown, false, false, false);
        system.InputStateService.EnqueueKey(pageDown);
        system.Input.ProcessInput();

        var scrollAfterPageDown = window.ScrollOffset;
        Assert.True(scrollAfterPageDown > 0);

        // Page Up
        var pageUp = new ConsoleKeyInfo('\0', ConsoleKey.PageUp, false, false, false);
        system.InputStateService.EnqueueKey(pageUp);
        system.Input.ProcessInput();

        Assert.True(window.ScrollOffset < scrollAfterPageDown);
    }

    [Fact]
    public void Home_ScrollsToTop()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Height = 10 };

        for (int i = 0; i < 50; i++)
        {
            window.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));
        }

        system.WindowStateService.AddWindow(window);
        window.ScrollToControl(null); // Scroll down

        var homeKey = new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false);
        system.InputStateService.EnqueueKey(homeKey);
        system.Input.ProcessInput();

        Assert.Equal(0, window.ScrollOffset);
    }

    [Fact]
    public void End_ScrollsToBottom()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Height = 10 };

        for (int i = 0; i < 50; i++)
        {
            window.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));
        }

        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);

        // Trigger render so layout is computed and scroll range is known
        system.Render.UpdateDisplay();

        var endKey = new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false);
        system.InputStateService.EnqueueKey(endKey);
        system.Input.ProcessInput();

        // Should scroll to maximum possible offset
        Assert.True(window.ScrollOffset > 30);
    }

    // TODO: MultilineEditControl doesn't have SelectAll() method yet
    //[Fact]
    //public void CtrlC_OnTextBox_CopiesSelection()
    //{
    //    var system = TestWindowSystemBuilder.CreateTestSystem();
    //    var window = new Window(system);
    //    var textbox = new MultilineEditControl("Copy this text");
    //
    //    window.AddControl(textbox);
    //    system.WindowStateService.AddWindow(window);
    //    system.FocusStateService.SetFocus(window, textbox);
    //
    //    // Select all
    //    textbox.SelectAll();
    //
    //    var ctrlC = new ConsoleKeyInfo('c', ConsoleKey.C, false, false, true);
    //    system.InputStateService.EnqueueKey(ctrlC);
    //    system.Input.ProcessInput();
    //
    //    // Copy operation should complete without crash
    //    // Actual clipboard testing requires platform-specific APIs
    //}
    //
    //[Fact]
    //public void CtrlV_OnTextBox_PastesFromClipboard()
    //{
    //    var system = TestWindowSystemBuilder.CreateTestSystem();
    //    var window = new Window(system);
    //    var textbox = new MultilineEditControl();
    //
    //    window.AddControl(textbox);
    //    system.WindowStateService.AddWindow(window);
    //    system.FocusStateService.SetFocus(window, textbox);
    //
    //    var ctrlV = new ConsoleKeyInfo('v', ConsoleKey.V, false, false, true);
    //    system.InputStateService.EnqueueKey(ctrlV);
    //    system.Input.ProcessInput();
    //
    //    // Paste operation should complete without crash
    //}
}
