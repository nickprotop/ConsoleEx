using System.Runtime.InteropServices;
using SharpConsoleUI;
using SharpConsoleUI.Builders;

namespace DemoApp.DemoWindows;

internal static class SystemInfoWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var gcInfo = GC.GetGCMemoryInfo();

        var info = Controls.Markup()
            .AddLine("[bold yellow]System Information[/]")
            .AddEmptyLine()
            .AddLine($"[bold]OS:[/]            {RuntimeInformation.OSDescription}")
            .AddLine($"[bold]Architecture:[/]  {RuntimeInformation.OSArchitecture}")
            .AddLine($"[bold]Runtime:[/]       {RuntimeInformation.FrameworkDescription}")
            .AddLine($"[bold]Process Arch:[/]  {RuntimeInformation.ProcessArchitecture}")
            .AddEmptyLine()
            .AddLine($"[bold]Processors:[/]    {Environment.ProcessorCount}")
            .AddLine($"[bold]Machine:[/]       {Environment.MachineName}")
            .AddLine($"[bold]User:[/]          {Environment.UserName}")
            .AddEmptyLine()
            .AddLine("[bold yellow]Memory[/]")
            .AddEmptyLine()
            .AddLine($"[bold]GC Heap:[/]       {GC.GetTotalMemory(false) / 1024:N0} KB")
            .AddLine($"[bold]GC Gen0:[/]       {GC.CollectionCount(0)} collections")
            .AddLine($"[bold]GC Gen1:[/]       {GC.CollectionCount(1)} collections")
            .AddLine($"[bold]GC Gen2:[/]       {GC.CollectionCount(2)} collections")
            .AddLine($"[bold]Heap Size:[/]     {gcInfo.HeapSizeBytes / 1024:N0} KB")
            .AddEmptyLine()
            .AddLine("[dim]Press [bold]ESC[/] to close[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("System Info")
            .WithSize(60, 22)
            .Centered()
            .AddControl(info)
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)s!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }
}
