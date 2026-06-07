using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

[Collection("EnvSerial")]
public class PasteRoutingTests
{
    [Fact]
    public void BracketedPaste_ReachesFocusedMultilineEdit()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var editor = new MultilineEditControl();
        window.AddControl(editor);
        system.WindowStateService.AddWindow(window);
        window.FocusManager.SetFocus(editor, FocusReason.Programmatic);

        window.EventDispatcher!.ProcessPaste("alpha\nbeta");

        Assert.Contains("alpha", editor.GetContent());
        Assert.Contains("beta", editor.GetContent());
    }

    [Fact]
    public void CtrlV_RoutesThroughPasteTarget()
    {
        SharpConsoleUI.Helpers.ClipboardHelper.ForceBackendForTests(
            SharpConsoleUI.Helpers.ClipboardBackend.InternalFallback);
        SharpConsoleUI.Helpers.ClipboardHelper.SetText("pasted-via-ctrlv");

        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var editor = new MultilineEditControl();
        window.AddControl(editor);
        system.WindowStateService.AddWindow(window);
        window.FocusManager.SetFocus(editor, FocusReason.Programmatic);

        var ctrlV = new ConsoleKeyInfo('\u0016', ConsoleKey.V, false, false, true);
        window.EventDispatcher!.ProcessInput(ctrlV);

        Assert.Contains("pasted-via-ctrlv", editor.GetContent());
    }
}
