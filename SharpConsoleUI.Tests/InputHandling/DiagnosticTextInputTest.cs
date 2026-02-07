using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.InputHandling;

public class DiagnosticTextInputTest
{
    private readonly ITestOutputHelper _output;

    public DiagnosticTextInputTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Diagnostic_CheckControlState()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system);
        var textbox = new MultilineEditControl();

        _output.WriteLine($"1. Before AddControl:");
        _output.WriteLine($"   - textbox.HasFocus: {textbox.HasFocus}");
        _output.WriteLine($"   - textbox.IsEnabled: {textbox.IsEnabled}");

        window.AddControl(textbox);

        _output.WriteLine($"2. After AddControl:");
        _output.WriteLine($"   - textbox.HasFocus: {textbox.HasFocus}");
        _output.WriteLine($"   - textbox.Container: {textbox.Container != null}");

        system.WindowStateService.AddWindow(window);

        _output.WriteLine($"3. After AddWindow:");
        _output.WriteLine($"   - ActiveWindow: {system.WindowStateService.ActiveWindow?.Title ?? "null"}");

        system.WindowStateService.SetActiveWindow(window);

        _output.WriteLine($"4. After SetActiveWindow:");
        _output.WriteLine($"   - ActiveWindow: {system.WindowStateService.ActiveWindow?.Title ?? "null"}");

        system.FocusStateService.SetFocus(window, textbox);

        _output.WriteLine($"5. After SetFocus:");
        _output.WriteLine($"   - textbox.HasFocus: {textbox.HasFocus}");
        _output.WriteLine($"   - FocusedControl: {system.FocusStateService.FocusedControl?.GetType().Name ?? "null"}");

        system.Render.UpdateDisplay();

        _output.WriteLine($"6. After Render:");
        _output.WriteLine($"   - textbox.ActualX: {textbox.ActualX}");
        _output.WriteLine($"   - textbox.ActualY: {textbox.ActualY}");

        // Try Enter key
        _output.WriteLine($"7. Enqueuing Enter key...");
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));

        _output.WriteLine($"8. Before ProcessInput:");
        _output.WriteLine($"   - textbox.IsEditing: {textbox.IsEditing}");

        system.Input.ProcessInput();

        _output.WriteLine($"9. After ProcessInput (Enter):");
        _output.WriteLine($"   - textbox.IsEditing: {textbox.IsEditing}");
        _output.WriteLine($"   - textbox.Content: '{textbox.Content}'");

        // Try text key
        _output.WriteLine($"10. Enqueuing 'X' key...");
        system.InputStateService.EnqueueKey(new ConsoleKeyInfo('X', ConsoleKey.X, false, false, false));
        system.Input.ProcessInput();

        _output.WriteLine($"11. After ProcessInput ('X'):");
        _output.WriteLine($"   - textbox.Content: '{textbox.Content}'");

        Assert.Equal("X", textbox.Content);
    }
}
