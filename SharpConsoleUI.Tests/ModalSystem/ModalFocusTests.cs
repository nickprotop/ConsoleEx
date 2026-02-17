using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.ModalSystem;

public class ModalFocusTests
{
    [Fact]
    public void Modal_HasOrphanModals_ReturnsTrueForOrphanModal()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        // Push modal with null parent (orphan modal)
        system.ModalStateService.PushModal(modalWindow, null);

        Assert.True(system.ModalStateService.HasOrphanModals());
    }

    [Fact]
    public void Modal_HasOrphanModals_ReturnsFalseForParentedModal()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        Assert.False(system.ModalStateService.HasOrphanModals());
    }

    [Fact]
    public void Modal_OrphanModal_BlocksAllOtherWindows()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window1 = new Window(system) { Title = "Window 1" };
        var window2 = new Window(system) { Title = "Window 2" };
        var orphanModal = new Window(system) { Title = "Orphan Modal", IsModal = true };

        system.WindowStateService.AddWindow(window1);
        system.WindowStateService.AddWindow(window2);
        system.ModalStateService.PushModal(orphanModal, null);

        // Both windows should be blocked by orphan modal
        Assert.True(system.ModalStateService.IsActivationBlocked(window1));
        Assert.True(system.ModalStateService.IsActivationBlocked(window2));

        var effectiveTarget1 = system.ModalStateService.GetEffectiveActivationTarget(window1);
        var effectiveTarget2 = system.ModalStateService.GetEffectiveActivationTarget(window2);

        Assert.Equal(orphanModal, effectiveTarget1);
        Assert.Equal(orphanModal, effectiveTarget2);
    }

    [Fact]
    public void Modal_ModalStateChanged_EventFiredOnPush()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        using var eventSignal = new System.Threading.ManualResetEventSlim(false);
        system.ModalStateService.StateChanged += (sender, args) =>
        {
            eventSignal.Set();
        };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        // Wait for ThreadPool-queued event with generous timeout
        Assert.True(eventSignal.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Modal_ModalOpened_EventFiredOnPush()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        using var eventSignal = new System.Threading.ManualResetEventSlim(false);
        system.ModalStateService.ModalOpened += (sender, args) =>
        {
            eventSignal.Set();
        };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        // Wait for ThreadPool-queued event with generous timeout
        Assert.True(eventSignal.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Modal_ModalClosed_EventFiredOnPop()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        using var eventSignal = new System.Threading.ManualResetEventSlim(false);
        system.ModalStateService.ModalClosed += (sender, args) =>
        {
            eventSignal.Set();
        };

        system.ModalStateService.PopModal(modalWindow);

        // Wait for ThreadPool-queued event with generous timeout
        Assert.True(eventSignal.Wait(TimeSpan.FromSeconds(5)));
    }
}
