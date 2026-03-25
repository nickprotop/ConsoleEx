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

/// <summary>
/// Verifies that controls which override Container properly delegate to
/// base.Container, ensuring SubscribeToFocusManager() runs and
/// GotFocus/LostFocus events fire.
/// </summary>
public class ContainerFocusSubscriptionTests
{
    [Fact]
    public void MultilineEditControl_GotFocus_Fires_WhenFocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        // Add a placeholder first so it gets auto-focus, not the edit control
        var placeholder = new ButtonControl { Text = "Placeholder" };
        window.AddControl(placeholder);

        var edit = new MultilineEditControl();
        window.AddControl(edit);

        bool gotFocusFired = false;
        edit.GotFocus += (_, _) => gotFocusFired = true;

        window.FocusManager.SetFocus(edit, FocusReason.Programmatic);

        Assert.True(gotFocusFired, "GotFocus should fire for MultilineEditControl after Container fix");
    }

    [Fact]
    public void MultilineEditControl_LostFocus_Fires_WhenUnfocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var placeholder = new ButtonControl { Text = "Placeholder" };
        window.AddControl(placeholder);

        var edit = new MultilineEditControl();
        window.AddControl(edit);

        window.FocusManager.SetFocus(edit, FocusReason.Programmatic);

        bool lostFocusFired = false;
        edit.LostFocus += (_, _) => lostFocusFired = true;

        window.FocusManager.SetFocus(placeholder, FocusReason.Programmatic);

        Assert.True(lostFocusFired, "LostFocus should fire for MultilineEditControl after Container fix");
    }

    [Fact]
    public void SliderControl_GotFocus_Fires_WhenFocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var placeholder = new ButtonControl { Text = "Placeholder" };
        window.AddControl(placeholder);

        var slider = new SliderControl();
        window.AddControl(slider);

        bool gotFocusFired = false;
        slider.GotFocus += (_, _) => gotFocusFired = true;

        window.FocusManager.SetFocus(slider, FocusReason.Programmatic);

        Assert.True(gotFocusFired, "GotFocus should fire for SliderControl after Container fix");
    }

    [Fact]
    public void SliderControl_LostFocus_Fires_WhenUnfocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var placeholder = new ButtonControl { Text = "Placeholder" };
        window.AddControl(placeholder);

        var slider = new SliderControl();
        window.AddControl(slider);

        window.FocusManager.SetFocus(slider, FocusReason.Programmatic);

        bool lostFocusFired = false;
        slider.LostFocus += (_, _) => lostFocusFired = true;

        window.FocusManager.SetFocus(placeholder, FocusReason.Programmatic);

        Assert.True(lostFocusFired, "LostFocus should fire for SliderControl after Container fix");
    }

    [Fact]
    public void RangeSliderControl_GotFocus_Fires_WhenFocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var placeholder = new ButtonControl { Text = "Placeholder" };
        window.AddControl(placeholder);

        var slider = new RangeSliderControl();
        window.AddControl(slider);

        bool gotFocusFired = false;
        slider.GotFocus += (_, _) => gotFocusFired = true;

        window.FocusManager.SetFocus(slider, FocusReason.Programmatic);

        Assert.True(gotFocusFired, "GotFocus should fire for RangeSliderControl after Container fix");
    }

    [Fact]
    public void RangeSliderControl_LostFocus_Fires_WhenUnfocused()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var placeholder = new ButtonControl { Text = "Placeholder" };
        window.AddControl(placeholder);

        var slider = new RangeSliderControl();
        window.AddControl(slider);

        window.FocusManager.SetFocus(slider, FocusReason.Programmatic);

        bool lostFocusFired = false;
        slider.LostFocus += (_, _) => lostFocusFired = true;

        window.FocusManager.SetFocus(placeholder, FocusReason.Programmatic);

        Assert.True(lostFocusFired, "LostFocus should fire for RangeSliderControl after Container fix");
    }

    [Fact]
    public void SeparatorControl_Container_DelegatesToBase()
    {
        // SeparatorControl is non-interactive/non-focusable, but verify
        // its Container override delegates to base without errors.
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var separator = new SeparatorControl();
        window.AddControl(separator);

        Assert.NotNull(separator.Container);
    }
}
