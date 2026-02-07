using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.InputHandling;

public class DiagnosticKeyPropagationTest
{
    private readonly ITestOutputHelper _output;

    public DiagnosticKeyPropagationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Diagnostic_CheckKeyPropagation()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var textbox = new MultilineEditControl();

        window.AddControl(textbox);
        system.WindowStateService.AddWindow(window);
        system.WindowStateService.SetActiveWindow(window);
        system.FocusStateService.SetFocus(window, textbox);
        system.Render.UpdateDisplay();

        int eventCount = 0;
        window.KeyPressed += (s, e) =>
        {
            eventCount++;
            _output.WriteLine($"Event #{eventCount}: Key={e.KeyInfo.Key}, Char='{e.KeyInfo.KeyChar}', AlreadyHandled={e.AlreadyHandled}");
        };

        // Press Enter to enter edit mode
        _output.WriteLine("Pressing Enter...");
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
        system.Input.ProcessInput();

        _output.WriteLine($"After Enter: textbox.IsEditing={textbox.IsEditing}");

        // Type character
        _output.WriteLine("Pressing 'T'...");
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('T', ConsoleKey.T, false, false, false));
        system.Input.ProcessInput();

        _output.WriteLine($"After 'T': textbox.Content='{textbox.Content}'");

        Assert.Equal("T", textbox.Content);
    }
}
