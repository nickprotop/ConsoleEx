using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Rendering;

namespace TerminalMail.UI;

/// <summary>Settings modal whose body is a WinUI-style NavigationView.</summary>
public static class SettingsView
{
    public static void Show(ConsoleWindowSystem ws)
    {
        var nav = Controls.NavigationView()
            .AddItem("Account", icon: "\U0001F464", content: panel =>
            {
                panel.AddControl(Controls.Markup("[bold grey85]Account[/]").Build());
                panel.AddControl(Controls.Prompt("Display name: ").WithInput("Nikolaos").Build());
                panel.AddControl(Controls.Prompt("Email:        ").WithInput("me@example.com").Build());
            })
            .AddItem("Appearance", icon: "\U0001F3A8", content: panel =>
            {
                panel.AddControl(Controls.Markup("[bold grey85]Appearance[/]").Build());
                panel.AddControl(Controls.Markup("[grey70]Accent[/]  [steelblue1]████[/] SteelBlue").Build());
                panel.AddControl(Controls.Markup("[grey70]Gradient backdrop:[/] [green]On[/]").Build());
            })
            .AddItem("About", icon: "ℹ", content: panel =>
            {
                panel.AddControl(Controls.Markup("[bold grey85]TerminalMail[/]").Build());
                panel.AddControl(Controls.Markup("[grey70]A SharpConsoleUI showcase.[/]").Build());
            })
            .WithSelectedIndex(0)
            .Fill()
            .Build();

        Window modal = null!;
        var close = Controls.Button("[grey93] Close [/]")
            .OnClick((_, _) => modal.Close())
            .Build();

        modal = new WindowBuilder(ws)
            .WithTitle("Settings")
            .AsModal()
            .WithSize(74, 22)
            .Centered()
            .WithBorderStyle(BorderStyle.Rounded)
            .WithActiveBorderColor(ColorScheme.ActiveBorder)
            .WithBackgroundGradient(ColorScheme.WindowGradient, GradientDirection.Vertical)
            .WithTransparencyBrush(TransparencyBrush.Acrylic())
            .AddControl(nav)
            .AddControl(close)
            .OnKeyPressed((_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    modal.Close();
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }
}
