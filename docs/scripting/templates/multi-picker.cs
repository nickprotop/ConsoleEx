#!/usr/bin/env dotnet
#:package SharpConsoleUI@2.4.55

// multi-picker.cs — Multi-select checklist for shell pipelines.
//
// stdin:    plain lines (one option per line)
// stdout:   selected lines, newline-separated (may be empty)
// exit 0:   user confirmed (pressed "Done"); at least one item was selected
// exit 1:   user cancelled or nothing was selected
// exit 2:   no stdin was provided
//
// Usage:
//   Space toggles an item. Tab moves focus to "Done" button. Enter confirms.
//
// Example:
//   ls /etc/*.conf | dotnet run multi-picker.cs | xargs -I{} sudo cp {} /tmp/backup/

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

if (system.PipedLines is null)
{
    Console.Error.WriteLine("multi-picker: no stdin. Pipe a list of lines into this command.");
    return 2;
}

var items = system.PipedLines.Where(l => l.Length > 0).ToArray();
if (items.Length == 0)
{
    Console.Error.WriteLine("multi-picker: stdin was empty after filtering blank lines.");
    return 1;
}

List<string> selected = new();
bool confirmed = false;

var listControl = Controls.List()
    .AddItems(items)
    .WithCheckboxMode(true)
    .Build();

var window = new WindowBuilder(system)
    .WithTitle("Select items (Space to toggle, Tab to Done, Enter to confirm)")
    .WithSize(70, Math.Min(22, items.Length + 6))
    .AddControl(listControl)
    .AddControl(Controls.Button()
        .WithText("Done")
        .Centered()
        .OnClick((sender, btn, win) =>
        {
            selected = listControl.Items
                .Where(i => i.IsChecked)
                .Select(i => i.Text)
                .ToList();
            confirmed = true;
            system.Shutdown();
        })
        .Build())
    .Build();

system.AddWindow(window);
system.Run();

if (!confirmed || selected.Count == 0)
    return 1;

foreach (var line in selected)
    Console.Out.WriteLine(line);
return 0;
