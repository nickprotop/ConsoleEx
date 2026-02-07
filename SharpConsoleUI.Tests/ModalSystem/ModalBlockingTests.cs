using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.ModalSystem;

public class ModalBlockingTests
{
    [Fact]
    public void Modal_GetBlockingModal_ReturnsNullForNonBlockedWindow()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Title = "Window" };

        system.WindowStateService.AddWindow(window);

        var blockingModal = system.ModalStateService.GetBlockingModal(window);
        Assert.Null(blockingModal);
    }

    [Fact]
    public void Modal_OrphanModal_BlocksUnrelatedWindows()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var otherWindow = new Window(system) { Title = "Other" };
        var orphanModal = new Window(system) { Title = "Orphan Modal", IsModal = true };

        system.WindowStateService.AddWindow(otherWindow);
        system.ModalStateService.PushModal(orphanModal, null); // null parent = orphan modal

        // otherWindow should be blocked by orphan modal
        var blockingModal = system.ModalStateService.GetBlockingModal(otherWindow);
        Assert.Equal(orphanModal, blockingModal);
    }

    [Fact]
    public void Modal_IsActivationBlocked_ReturnsTrueWhenBlocked()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var otherWindow = new Window(system) { Title = "Other" };
        var orphanModal = new Window(system) { Title = "Orphan Modal", IsModal = true };

        system.WindowStateService.AddWindow(otherWindow);
        system.ModalStateService.PushModal(orphanModal, null); // null parent = blocks everything

        Assert.True(system.ModalStateService.IsActivationBlocked(otherWindow));
        Assert.False(system.ModalStateService.IsActivationBlocked(orphanModal));
    }

    [Fact]
    public void Modal_GetEffectiveActivationTarget_ReturnsModalWhenBlocked()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var otherWindow = new Window(system) { Title = "Other" };
        var orphanModal = new Window(system) { Title = "Orphan Modal", IsModal = true };

        system.WindowStateService.AddWindow(otherWindow);
        system.ModalStateService.PushModal(orphanModal, null); // null parent = blocks everything

        // Try to activate otherWindow, but should get orphan modal instead
        var effectiveTarget = system.ModalStateService.GetEffectiveActivationTarget(otherWindow);
        Assert.Equal(orphanModal, effectiveTarget);
    }

    [Fact]
    public void Modal_OnlyTopModalBlocks_InNestedOrphanModals()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var otherWindow = new Window(system) { Title = "Other" };
        var modal1 = new Window(system) { Title = "Orphan Modal 1", IsModal = true };
        var modal2 = new Window(system) { Title = "Modal 2", IsModal = true };

        system.WindowStateService.AddWindow(otherWindow);
        system.ModalStateService.PushModal(modal1, null); // orphan modal
        system.ModalStateService.PushModal(modal2, modal1); // child of orphan

        // otherWindow should be blocked by modal2 (deepest child of orphan)
        var blockingModal = system.ModalStateService.GetBlockingModal(otherWindow);
        Assert.Equal(modal2, blockingModal);

        var effectiveTarget = system.ModalStateService.GetEffectiveActivationTarget(otherWindow);
        Assert.Equal(modal2, effectiveTarget);
    }

    [Fact]
    public void Modal_ParentWindowNotBlocked_ByOwnModal()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        // parentWindow should be redirected to its modal child, not "blocked" per se
        var effectiveTarget = system.ModalStateService.GetEffectiveActivationTarget(parentWindow);
        Assert.Equal(modalWindow, effectiveTarget);
    }
}
