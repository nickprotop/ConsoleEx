using SharpConsoleUI.Configuration;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.ModalSystem;

public class ModalStackTests
{
    [Fact]
    public void Modal_Push_AddsToModalStack()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        Assert.True(system.ModalStateService.HasModals);
        Assert.Equal(1, system.ModalStateService.ModalCount);
        Assert.Equal(modalWindow, system.ModalStateService.TopmostModal);
    }

    [Fact]
    public void Modal_Pop_RemovesFromStack()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        Assert.True(system.ModalStateService.HasModals);

        system.ModalStateService.PopModal(modalWindow);

        Assert.False(system.ModalStateService.HasModals);
        Assert.Equal(0, system.ModalStateService.ModalCount);
        Assert.Null(system.ModalStateService.TopmostModal);
    }

    [Fact]
    public void Modal_NestedModals_CorrectStackOrder()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modal1 = new Window(system) { Title = "Modal 1", IsModal = true };
        var modal2 = new Window(system) { Title = "Modal 2", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modal1, parentWindow);
        system.ModalStateService.PushModal(modal2, modal1);

        Assert.Equal(2, system.ModalStateService.ModalCount);
        Assert.Equal(modal2, system.ModalStateService.TopmostModal);

        system.ModalStateService.PopModal(modal2);
        Assert.Equal(1, system.ModalStateService.ModalCount);
        Assert.Equal(modal1, system.ModalStateService.TopmostModal);
    }

    [Fact]
    public void Modal_GetModalParent_ReturnsCorrectParent()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        var parent = system.ModalStateService.GetModalParent(modalWindow);
        Assert.Equal(parentWindow, parent);
    }

    [Fact]
    public void Modal_IsModal_ReturnsTrueForModalWindows()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modalWindow = new Window(system) { Title = "Modal", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modalWindow, parentWindow);

        Assert.True(system.ModalStateService.IsModal(modalWindow));
        Assert.False(system.ModalStateService.IsModal(parentWindow));
    }

    [Fact]
    public void Modal_GetModalChildren_ReturnsCorrectChildren()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modal1 = new Window(system) { Title = "Modal 1", IsModal = true };
        var modal2 = new Window(system) { Title = "Modal 2", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modal1, parentWindow);
        system.ModalStateService.PushModal(modal2, parentWindow);

        var children = system.ModalStateService.GetModalChildren(parentWindow);
        Assert.Equal(2, children.Count);
        Assert.Contains(modal1, children);
        Assert.Contains(modal2, children);
    }

    [Fact]
    public void Modal_GetDeepestModalChild_ReturnsDeepestModal()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modal1 = new Window(system) { Title = "Modal 1", IsModal = true };
        var modal2 = new Window(system) { Title = "Modal 2", IsModal = true };
        var modal3 = new Window(system) { Title = "Modal 3", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modal1, parentWindow);
        system.ModalStateService.PushModal(modal2, modal1);
        system.ModalStateService.PushModal(modal3, modal2);

        var deepest = system.ModalStateService.GetDeepestModalChild(parentWindow);
        Assert.Equal(modal3, deepest);
    }

    [Fact]
    public void Modal_PopMiddleModal_MaintainsStackIntegrity()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var parentWindow = new Window(system) { Title = "Parent" };
        var modal1 = new Window(system) { Title = "Modal 1", IsModal = true };
        var modal2 = new Window(system) { Title = "Modal 2", IsModal = true };
        var modal3 = new Window(system) { Title = "Modal 3", IsModal = true };

        system.WindowStateService.AddWindow(parentWindow);
        system.ModalStateService.PushModal(modal1, parentWindow);
        system.ModalStateService.PushModal(modal2, parentWindow);
        system.ModalStateService.PushModal(modal3, parentWindow);

        Assert.Equal(3, system.ModalStateService.ModalCount);

        // Pop middle modal
        system.ModalStateService.PopModal(modal2);

        Assert.Equal(2, system.ModalStateService.ModalCount);
        var children = system.ModalStateService.GetModalChildren(parentWindow);
        Assert.Contains(modal1, children);
        Assert.DoesNotContain(modal2, children);
        Assert.Contains(modal3, children);
    }
}
