using System.ComponentModel;
using ScHost.Models;
using ScHost.Packaging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScHost.Commands;

public sealed class InitCommandSettings : CommandSettings
{
    [Description("Path to .csproj file or directory")]
    [CommandArgument(0, "[project]")]
    public string? Project { get; set; }

    [Description("Window title")]
    [CommandOption("--title")]
    public string? Title { get; set; }

    [Description("Font face")]
    [CommandOption("--font")]
    public string? Font { get; set; }

    [Description("Font size")]
    [CommandOption("--font-size")]
    public int? FontSize { get; set; }

    [Description("Terminal columns")]
    [CommandOption("--columns")]
    public int? Columns { get; set; }

    [Description("Terminal rows")]
    [CommandOption("--rows")]
    public int? Rows { get; set; }

    [Description("Color scheme (WT name)")]
    [CommandOption("--color-scheme")]
    public string? ColorScheme { get; set; }

    [Description("Skip interactive prompts, use defaults")]
    [CommandOption("-y|--yes")]
    public bool NonInteractive { get; set; }
}

public sealed class InitCommand : Command<InitCommandSettings>
{
    protected override int Execute(CommandContext context, InitCommandSettings settings, CancellationToken ct)
    {
        try
        {
            var csprojPath = ProjectReader.FindCsproj(settings.Project);
            var (assemblyName, version) = ProjectReader.ParseCsproj(csprojPath);
            var projectDir = Path.GetDirectoryName(csprojPath)!;
            var configPath = Path.Combine(projectDir, SchostConfig.FileName);

            if (File.Exists(configPath) && !settings.NonInteractive)
            {
                if (!AnsiConsole.Confirm($"[yellow]{SchostConfig.FileName} already exists. Overwrite?[/]", defaultValue: false))
                    return 0;
            }

            AnsiConsole.MarkupLine($"[blue]Project:[/] {Path.GetFileName(csprojPath)}");
            AnsiConsole.MarkupLine($"[blue]Assembly:[/] {assemblyName}  [blue]Version:[/] {version}");
            AnsiConsole.WriteLine();

            // If any CLI flags are set or --yes is used, skip interactive prompts
            var hasFlags = settings.Title is not null || settings.Font is not null
                           || settings.FontSize.HasValue || settings.Columns.HasValue
                           || settings.Rows.HasValue || settings.ColorScheme is not null;

            SchostConfig config;

            if (settings.NonInteractive || hasFlags)
            {
                config = new SchostConfig
                {
                    Title = settings.Title ?? assemblyName,
                    Font = settings.Font,
                    FontSize = settings.FontSize,
                    Columns = settings.Columns,
                    Rows = settings.Rows,
                    ColorScheme = settings.ColorScheme,
                };
            }
            else
            {
                config = PromptForConfig(assemblyName);
            }

            config.Save(configPath);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Created:[/] {configPath}");
            AnsiConsole.WriteLine();

            var panel = new Panel(
                new Rows(
                    new Markup("[blue]schost run[/]           Launch in configured terminal"),
                    new Markup("[blue]schost pack[/]          Package for distribution"),
                    new Markup("[blue]schost pack --installer[/]  Create Inno Setup installer")))
            {
                Header = new PanelHeader("Next Steps"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(panel);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static SchostConfig PromptForConfig(string assemblyName)
    {
        var title = AnsiConsole.Prompt(
            new TextPrompt<string>("Window title:")
                .DefaultValue(assemblyName));

        var font = AnsiConsole.Prompt(
            new TextPrompt<string>("Font face (empty = terminal default):")
                .AllowEmpty());

        int? fontSize = null;
        if (!string.IsNullOrWhiteSpace(font))
        {
            fontSize = AnsiConsole.Prompt(
                new TextPrompt<int>("Font size:")
                    .DefaultValue(14));
        }

        var columnsStr = AnsiConsole.Prompt(
            new TextPrompt<string>("Columns (empty = terminal default):")
                .AllowEmpty());
        int? columns = int.TryParse(columnsStr, out var c) ? c : null;

        var rowsStr = AnsiConsole.Prompt(
            new TextPrompt<string>("Rows (empty = terminal default):")
                .AllowEmpty());
        int? rows = int.TryParse(rowsStr, out var r) ? r : null;

        var colorScheme = AnsiConsole.Prompt(
            new TextPrompt<string>("Color scheme (WT name, empty = terminal default):")
                .AllowEmpty());

        return new SchostConfig
        {
            Title = title,
            Font = string.IsNullOrWhiteSpace(font) ? null : font,
            FontSize = fontSize,
            Columns = columns,
            Rows = rows,
            ColorScheme = string.IsNullOrWhiteSpace(colorScheme) ? null : colorScheme,
        };
    }
}
