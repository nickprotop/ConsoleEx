#!/usr/bin/env dotnet
#:package SharpConsoleUI@2.4.60

// table-select.cs — Pick a row from a JSON array using its property names as columns.
//
// stdin:    JSON array of objects (all objects should share the same property names)
// stdout:   the selected object as a single-line JSON blob
// exit 0:   user activated a row
// exit 1:   user cancelled
// exit 2:   stdin was not a JSON array or was empty
//
// Example:
//   Get-Service | ConvertTo-Json | dotnet run table-select.cs | ConvertFrom-Json

using System.Text.Json;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

if (string.IsNullOrWhiteSpace(system.PipedInput))
{
    Console.Error.WriteLine("table-select: no stdin. Pipe a JSON array of objects.");
    return 2;
}

JsonDocument doc;
try
{
    doc = JsonDocument.Parse(system.PipedInput);
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"table-select: invalid JSON: {ex.Message}");
    return 2;
}

if (doc.RootElement.ValueKind != JsonValueKind.Array)
{
    Console.Error.WriteLine("table-select: expected a JSON array at the root.");
    return 2;
}

var rows = doc.RootElement.EnumerateArray().ToList();
if (rows.Count == 0)
{
    Console.Error.WriteLine("table-select: JSON array was empty.");
    return 1;
}

if (rows[0].ValueKind != JsonValueKind.Object)
{
    Console.Error.WriteLine("table-select: array elements must be objects.");
    return 2;
}

var columnNames = rows[0].EnumerateObject().Select(p => p.Name).ToArray();

var tableBuilder = Controls.Table();
foreach (var col in columnNames)
    tableBuilder = tableBuilder.AddColumn(col);

foreach (var row in rows)
{
    var cells = columnNames
        .Select(name => row.TryGetProperty(name, out var v) ? ValueToString(v) : "")
        .ToArray();
    tableBuilder = tableBuilder.AddRow(cells);
}

int activatedIndex = -1;
tableBuilder = tableBuilder
    .Interactive()
    .OnRowActivated((sender, rowIdx) =>
    {
        activatedIndex = rowIdx;
        system.Shutdown();
    });

var window = new WindowBuilder(system)
    .WithTitle("Select a row (Enter to confirm, Ctrl+Q to cancel)")
    .WithSize(Math.Min(120, columnNames.Length * 20 + 4), Math.Min(25, rows.Count + 5))
    .AddControl(tableBuilder.Build())
    .Build();

system.AddWindow(window);
system.Run();

if (activatedIndex < 0 || activatedIndex >= rows.Count)
    return 1;

Console.Out.WriteLine(rows[activatedIndex].GetRawText().Replace("\n", " ").Replace("\r", ""));
return 0;

static string ValueToString(JsonElement e) => e.ValueKind switch
{
    JsonValueKind.String => e.GetString() ?? "",
    JsonValueKind.Number => e.GetRawText(),
    JsonValueKind.True => "true",
    JsonValueKind.False => "false",
    JsonValueKind.Null => "",
    _ => e.GetRawText()
};
