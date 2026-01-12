using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using OpenCodeShowcase.Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace OpenCodeShowcase.Components;

/// <summary>
/// Creates visual panels for tool call display
/// </summary>
public static class ToolCallPanel
{
    /// <summary>
    /// Creates a panel control displaying a tool call
    /// </summary>
    public static SpectreRenderableControl CreatePanel(ToolCall toolCall)
    {
        var statusIcon = toolCall.Status switch
        {
            ToolStatus.Complete => "[green]✓[/]",
            ToolStatus.Error => "[red]✗[/]",
            _ => "[yellow]⏳[/]"
        };

        var statusText = toolCall.Status switch
        {
            ToolStatus.Complete => "Complete",
            ToolStatus.Error => "Error",
            _ => "Running"
        };

        // Build panel content
        var contentLines = new List<string>();

        // Header line with parameters and status
        contentLines.Add($"{toolCall.Parameters} | Status: {statusIcon} {statusText}");

        // Add output if present
        if (!string.IsNullOrEmpty(toolCall.Output))
        {
            contentLines.Add("");

            // Create a nested box for the output
            var outputBox = new Panel(new Markup(toolCall.Output))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(foreground: Color.Grey50),
                Padding = new Padding(1, 0, 1, 0)
            };

            // For now, we'll add output as text since we're using string list
            // In a more advanced version, we could use nested SpectreRenderableControls
            foreach (var line in toolCall.Output.Split('\n'))
            {
                contentLines.Add($"  {line}");
            }
            contentLines.Add("");
        }

        // Add execution time if present
        if (toolCall.ExecutionTime.HasValue)
        {
            contentLines.Add($"[grey50]Execution time: {toolCall.ExecutionTime.Value:F1}s[/]");
        }

        // Create the main panel
        var panel = new Panel(new Markup(string.Join("\n", contentLines)))
        {
            Header = new PanelHeader($" Tool Call: {toolCall.Name} "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Grey50),
            Padding = new Padding(1, 0, 1, 0)
        };

        return new SpectreRenderableControl(panel)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Margin(1, 0, 1, 1)
        };
    }

    /// <summary>
    /// Creates a simple text-based tool panel (alternative lightweight version)
    /// </summary>
    public static MarkupControl CreateSimplePanel(ToolCall toolCall)
    {
        var statusIcon = toolCall.Status switch
        {
            ToolStatus.Complete => "[green]✓[/]",
            ToolStatus.Error => "[red]✗[/]",
            _ => "[yellow]⏳[/]"
        };

        var lines = new List<string>
        {
            $"[grey50]┌─ Tool Call: {toolCall.Name} ────────────────────────────────────┐[/]",
            $"[grey50]│[/] {toolCall.Parameters} | {statusIcon} {toolCall.Status}",
        };

        if (!string.IsNullOrEmpty(toolCall.Output))
        {
            lines.Add($"[grey50]│[/]");
            foreach (var line in toolCall.Output.Split('\n').Take(10))
            {
                lines.Add($"[grey50]│[/]   {line}");
            }
        }

        if (toolCall.ExecutionTime.HasValue)
        {
            lines.Add($"[grey50]│[/] [grey50]Execution time: {toolCall.ExecutionTime.Value:F1}s[/]");
        }

        lines.Add($"[grey50]└────────────────────────────────────────────────────────────────┘[/]");

        return new MarkupControl(lines)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Margin(1, 0, 1, 1),
            BackgroundColor = Color.Grey15,
            ForegroundColor = Color.Grey70
        };
    }
}
