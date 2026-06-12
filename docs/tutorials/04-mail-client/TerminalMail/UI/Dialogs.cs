using SharpConsoleUI;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Rendering;
using TerminalMail.ViewModels;

namespace TerminalMail.UI;

/// <summary>Lightweight modal dialogs — no service layer.</summary>
public static class Dialogs
{
    /// <summary>Shows the compose dialog as an alpha-blended modal over the mailbox.</summary>
    public static void ShowCompose(ConsoleWindowSystem ws)
    {
        var vm = new ComposeViewModel();

        // NOTE: PromptControl exposes .Input, not .Text — bind against that property.
        var to = Controls.Prompt("To:      ").Build();
        to.BindTwoWay(vm, v => v.To, c => c.Input);

        var subject = Controls.Prompt("Subject: ").Build();
        subject.BindTwoWay(vm, v => v.Subject, c => c.Input);

        var body = Controls.Prompt("Body:    ").Build();
        body.BindTwoWay(vm, v => v.Body, c => c.Input);

        Window modal = null!;

        var send = Controls.Button("[grey93] Send [/]")
            .OnClick((_, _) => modal.Close())
            .Build();
        var cancel = Controls.Button("[grey93] Cancel [/]")
            .OnClick((_, _) => modal.Close())
            .Build();

        var buttons = Controls.HorizontalGrid()
            .Column(col => col.Add(send))
            .Column(col => col.Add(cancel))
            .Build();

        modal = new WindowBuilder(ws)
            .WithTitle("Compose")
            .AsModal()
            .WithSize(64, 16)
            .Centered()
            .WithBorderStyle(BorderStyle.Rounded)
            .WithActiveBorderColor(ColorScheme.ActiveBorder)
            .WithBackgroundGradient(ColorScheme.WindowGradient, GradientDirection.Vertical)
            .WithTransparencyBrush(TransparencyBrush.Acrylic())
            .AddControl(to)
            .AddControl(subject)
            .AddControl(body)
            .AddControl(buttons)
            .OnKeyPressed((_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    modal.Close();
                    e.Handled = true;
                }
            })
            .BuildAndShow();

        // Fade-in animation on open.
        // FadeIn returns IAnimation but we don't need to track it.
        WindowAnimations.FadeIn(modal, TimeSpan.FromMilliseconds(180));
    }
}
