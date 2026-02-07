using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.ModalSystem;

/// <summary>
/// Tests for modal lifecycle and state after modal operations.
/// </summary>
public class ModalResultTests
{
    [Fact]
    public void Modal_AfterPop_ParentBecomesEffectiveTarget()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        // While modal is open, parent should redirect to modal
        var effectiveWhileOpen = system.ModalStateService.GetEffectiveActivationTarget(parentWindow);
        Assert.Equal(modalWindow, effectiveWhileOpen);

        // Pop the modal
        system.ModalStateService.PopModal(modalWindow);

        // After pop, parent should be the effective target
        var effectiveAfterPop = system.ModalStateService.GetEffectiveActivationTarget(parentWindow);
        Assert.Equal(parentWindow, effectiveAfterPop);
    }

    [Fact]
    public void Modal_AfterPopNested_ParentModalBecomesEffectiveTarget()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modal1 = new Window(system) { Title = "Modal 1", IsModal = true };
        var modal2 = new Window(system) { Title = "Modal 2", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modal1, parentWindow);
        system.ModalStateService.PushModal(modal2, modal1);

        // While modal2 is open, parent should redirect to modal2
        var effectiveWhileOpen = system.ModalStateService.GetEffectiveActivationTarget(parentWindow);
        Assert.Equal(modal2, effectiveWhileOpen);

        // Pop modal2
        system.ModalStateService.PopModal(modal2);

        // After pop, parent should redirect to modal1
        var effectiveAfterPop = system.ModalStateService.GetEffectiveActivationTarget(parentWindow);
        Assert.Equal(modal1, effectiveAfterPop);
    }

    [Fact]
    public void Modal_StateHistory_RecordsModalOperations()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);

        // Clear history to start fresh
        system.ModalStateService.ClearHistory();

        system.ModalStateService.PushModal(modalWindow, parentWindow);
        system.ModalStateService.PopModal(modalWindow);

        var history = system.ModalStateService.GetHistory();

        // Should have at least 2 state changes (push and pop)
        Assert.True(history.Count >= 2);
    }

    [Fact]
    public void Modal_CurrentState_ReflectsModalStack()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modal1 = new Window(system) { Title = "Modal 1", IsModal = true };
        var modal2 = new Window(system) { Title = "Modal 2", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modal1, parentWindow);
        system.ModalStateService.PushModal(modal2, modal1);

        var state = system.ModalStateService.CurrentState;

        Assert.Equal(2, state.ModalCount);
        Assert.Equal(modal2, state.TopmostModal);
        Assert.Equal(2, state.ModalStack.Count);
        Assert.Contains(modal1, state.ModalStack);
        Assert.Contains(modal2, state.ModalStack);
    }

    [Fact]
    public void Modal_GetDebugInfo_ReturnsFormattedState()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal Window", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        var debugInfo = system.ModalStateService.GetDebugInfo();

        // Should contain count, stack info, and topmost modal
        Assert.Contains("Count=1", debugInfo);
        Assert.Contains("Modal Window", debugInfo);
        Assert.Contains("Topmost=", debugInfo);
    }
}
