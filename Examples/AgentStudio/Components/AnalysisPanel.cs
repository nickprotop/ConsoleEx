using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using AgentStudio.Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace AgentStudio.Components;

/// <summary>
/// Creates visual panels for analysis results
/// </summary>
public static class AnalysisPanel
{
    /// <summary>
    /// Creates a panel control displaying analysis findings
    /// </summary>
    public static SpectreRenderableControl CreatePanel(List<Finding> findings, string title = "Analysis Results")
    {
        var table = new Table()
        {
            Border = TableBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Grey50)
        };

        table.AddColumn(new Spectre.Console.TableColumn("[grey70]Severity[/]").Width(10));
        table.AddColumn(new Spectre.Console.TableColumn("[grey70]Finding[/]"));

        foreach (var finding in findings)
        {
            var severityIcon = finding.Severity switch
            {
                Severity.Critical => "[red bold]⚠ CRIT[/]",
                Severity.High => "[red]⚠ High[/]",
                Severity.Medium => "[yellow]⚠ Med[/]",
                Severity.Low => "[grey70]⚠ Low[/]",
                _ => "[grey50]⚠ Info[/]"
            };

            var findingText = !string.IsNullOrEmpty(finding.Location)
                ? $"{finding.Title} [grey50]({finding.Location})[/]"
                : finding.Title;

            table.AddRow(severityIcon, findingText);
        }

        var panel = new Panel(table)
        {
            Header = new PanelHeader($" {title} "),
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
    /// Creates a simple text-based analysis panel (alternative lightweight version)
    /// </summary>
    public static MarkupControl CreateSimplePanel(List<Finding> findings, string title = "Analysis Results")
    {
        var lines = new List<string>
        {
            $"[grey50]┌─ {title} ─────────────────────────────────────────────┐[/]"
        };

        foreach (var finding in findings)
        {
            var severityColor = finding.Severity switch
            {
                Severity.Critical => "red bold",
                Severity.High => "red",
                Severity.Medium => "yellow",
                Severity.Low => "grey70",
                _ => "grey50"
            };

            var severityText = finding.Severity switch
            {
                Severity.Critical => "CRIT",
                Severity.High => "High",
                Severity.Medium => "Med",
                Severity.Low => "Low",
                _ => "Info"
            };

            var locationText = !string.IsNullOrEmpty(finding.Location)
                ? $" [grey50]({finding.Location})[/]"
                : "";

            lines.Add($"[grey50]│[/] [{severityColor}]⚠ {severityText}[/]: {finding.Title}{locationText}");
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
