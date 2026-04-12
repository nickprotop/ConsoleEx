using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

internal static class PromptDemoWindow
{
    private static readonly List<string> _outputLines = new();

    public static Window Create(ConsoleWindowSystem ws)
    {
        _outputLines.Clear();

        var outputLog = Controls.Markup()
            .WithName("output")
            .Build();
        outputLog.SetContent(new List<string> { "[dim]Output will appear here...[/]" });

        // Helper to escape user input for safe markup display
        static string Esc(string s) => SharpConsoleUI.Parsing.MarkupParser.Escape(s);

        // 1. Basic prompt with history
        var basicPrompt = Controls.Prompt("> ")
            .WithInputWidth(25)
            .WithHistory()
            .OnEntered((sender, text) =>
            {
                AppendOutput(outputLog, $"[green]Basic:[/] {Esc(text)}");
                ((PromptControl)sender!).SetInput("");
            })
            .UnfocusOnEnter(false)
            .Build();

        // 2. Password prompt
        var passwordPrompt = Controls.Prompt("Password: ")
            .WithInputWidth(25)
            .WithMaskCharacter('●')
            .OnEntered((sender, text) =>
            {
                AppendOutput(outputLog, $"[yellow]Password:[/] ({text.Length} chars)");
                ((PromptControl)sender!).SetInput("");
            })
            .UnfocusOnEnter(false)
            .Build();

        // 3. Command prompt with tab completion
        var commands = new[] { "help", "exit", "clear", "history", "status", "start", "stop", "restart", "settings", "search" };
        var cmdPrompt = Controls.Prompt("$ ")
            .WithInputWidth(25)
            .WithHistory()
            .WithTabCompleter((input, _) =>
                commands.Where(c => c.StartsWith(input, StringComparison.OrdinalIgnoreCase)))
            .OnEntered((sender, text) =>
            {
                AppendOutput(outputLog, $"[cyan]Command:[/] {Esc(text)}");
                ((PromptControl)sender!).SetInput("");
            })
            .UnfocusOnEnter(false)
            .Build();

        // 4. Styled prompt with custom colors
        var styledPrompt = Controls.Prompt("[bold cyan]λ[/] ")
            .WithInputWidth(25)
            .WithInputFocusedBackgroundColor(new Color(30, 40, 60))
            .WithInputFocusedForegroundColor(Color.Cyan1)
            .WithInputBackgroundColor(new Color(20, 25, 35))
            .WithInputForegroundColor(new Color(180, 180, 180))
            .WithHistory()
            .OnEntered((sender, text) =>
            {
                AppendOutput(outputLog, $"[rgb(180,140,255)]Styled:[/] {Esc(text)}");
                ((PromptControl)sender!).SetInput("");
            })
            .UnfocusOnEnter(false)
            .Build();

        var window = new WindowBuilder(ws)
            .WithTitle("Prompt Control")
            .WithSize(85, 30)
            .Centered()
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    // Only close if no prompt has focus
                    var focused = ((Window)s!).FocusManager.FocusedControl;
                    if (focused == null)
                    {
                        ws.CloseWindow((Window)s!);
                        e.Handled = true;
                    }
                }
            })
            .AddControl(Controls.Markup()
                .AddLine("[bold]PromptControl Demo[/]")
                .AddEmptyLine()
                .AddLine("[dim]Readline editing: Ctrl+A/E/K/U/W, Ctrl+Left/Right | Selection: Shift+Arrow, Ctrl+C/V/X[/]")
                .AddLine("[dim]History: Up/Down | Tab completion on command prompt | Mouse click to position cursor[/]")
                .Build())
            .AddControl(Controls.Markup().AddEmptyLine().AddLine("[bold]Basic input[/] [dim](with history — type, press Enter, then Up to recall)[/]").Build())
            .AddControl(basicPrompt)
            .AddControl(Controls.Markup().AddEmptyLine().AddLine("[bold]Password[/] [dim](masked with ●)[/]").Build())
            .AddControl(passwordPrompt)
            .AddControl(Controls.Markup().AddEmptyLine().AddLine("[bold]Command[/] [dim](Tab to complete: help, exit, clear, history, status, start, stop, restart, settings, search)[/]").Build())
            .AddControl(cmdPrompt)
            .AddControl(Controls.Markup().AddEmptyLine().AddLine("[bold]Styled[/] [dim](custom colors, markup in prompt)[/]").Build())
            .AddControl(styledPrompt)
            .AddControl(Controls.Markup().AddEmptyLine().AddLine("[bold]Output[/]").Build())
            .AddControl(outputLog)
            .BuildAndShow();

        return window;
    }

    private static void AppendOutput(MarkupControl output, string line)
    {
        _outputLines.Add(line);
        if (_outputLines.Count > 6)
            _outputLines.RemoveAt(0);
        output.SetContent(new List<string>(_outputLines));
    }
}
