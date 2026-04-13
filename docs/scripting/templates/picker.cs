#!/usr/bin/env dotnet
#:package SharpConsoleUI@2.4.58

// picker.cs — Single-select list picker for shell pipelines.
//
// stdin:    plain lines (one option per line)
// stdout:   the selected line (exactly as provided, no transformation)
// exit 0:   user selected an item
// exit 1:   user cancelled (Ctrl+Q) or stdin was empty
// exit 2:   no stdin was provided
//
// Example:
//   ls /etc | dotnet run picker.cs
//   git branch --list | dotnet run picker.cs | xargs git checkout

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

if (system.PipedLines is null)
{
    Console.Error.WriteLine("picker: no stdin. Pipe a list of lines into this command.");
    return 2;
}

var items = system.PipedLines.Where(l => l.Length > 0).ToArray();
if (items.Length == 0)
{
    Console.Error.WriteLine("picker: stdin was empty after filtering blank lines.");
    return 1;
}

string? selection = null;

var window = new WindowBuilder(system)
    .WithTitle("Pick an item")
    .WithSize(60, Math.Min(20, items.Length + 4))
    .AddControl(Controls.List()
        .AddItems(items)
        .OnItemActivated((sender, item, win) =>
        {
            selection = item.Text;
            system.Shutdown();
        })
        .Build())
    .Build();

system.AddWindow(window);
system.Run();

if (selection is null)
    return 1;

Console.Out.WriteLine(selection);
return 0;
