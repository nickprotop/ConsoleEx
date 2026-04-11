using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class TransparentWindowDemoWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        // Window 1: Opaque reference window with varied content for compositing testing
        var opaqueContent = Controls.Markup()
            .AddLine("[bold rgb(100,255,180)]Opaque Reference Window[/]")
            .AddEmptyLine()
            .AddLine("This window has a fully opaque background.")
            .AddLine("Text here should be visible through any")
            .AddLine("semi-transparent windows placed on top.")
            .AddEmptyLine()
            .AddLine("[bold yellow]████████████████████████████████[/]")
            .AddLine("[bold cyan]████████████████████████████████[/]")
            .AddLine("[bold magenta]████████████████████████████████[/]")
            .AddLine("[bold red]████████████████████████████████[/]")
            .AddEmptyLine()
            .AddLine("[dim]ABCDEFGHIJKLMNOPQRSTUVWXYZabcdef[/]")
            .AddLine("[dim]0123456789!@#$%^&*()-=+[]{}|;':\"[/]")
            .AddLine("[bold]The quick brown fox jumps over[/]")
            .AddLine("[bold]the lazy dog. 0123456789[/]")
            .Build();

        var opaqueWindow = new WindowBuilder(ws)
            .WithTitle("Opaque Reference")
            .WithSize(45, 20)
            .AtPosition(5, 2)
            .WithBackgroundColor(new Color(20, 40, 80))
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape) { ws.CloseWindow((Window)s!); e.Handled = true; }
            })
            .AddControl(opaqueContent)
            .BuildAndShow();

        // Window 2: Transparent control panel with alpha slider and brush buttons
        byte currentAlpha = 128;

        var alphaLabel = Controls.Markup()
            .WithName("alphaLabel")
            .Build();
        alphaLabel.SetContent(new List<string> { $"[bold]Window Alpha:[/] {currentAlpha}" });

        var alphaSlider = Controls.Slider()
            .Horizontal()
            .WithRange(0, 255)
            .WithValue(currentAlpha)
            .WithName("alphaSlider")
            .WithStep(1)
            .Build();

        Window? transparentWindow = null;

        alphaSlider.ValueChanged += (_, _) =>
        {
            currentAlpha = (byte)alphaSlider.Value;
            alphaLabel.SetContent(new List<string> { $"[bold]Window Alpha:[/] {currentAlpha}" });
            if (transparentWindow != null)
            {
                var old = transparentWindow.BackgroundColor;
                transparentWindow.BackgroundColor = new Color(old.R, old.G, old.B, currentAlpha);
            }
        };

        var heading = Controls.Markup()
            .AddLine("[bold]Transparent Windows[/]")
            .AddLine("[dim]Mica-style see-through window compositing.[/]")
            .AddLine("[dim]Semi-transparent windows show content from[/]")
            .AddLine("[dim]windows below and the desktop background.[/]")
            .AddEmptyLine()
            .AddLine("[bold]Features:[/]")
            .AddLine("[dim]  - Per-cell alpha compositing against lower windows[/]")
            .AddLine("[dim]  - Text from windows below shows through (faded)[/]")
            .AddLine("[dim]  - Brush styles: Acrylic, Mica, Tinted[/]")
            .Build();

        var defaultBtn = Controls.Button("Default (True Transparent)")
            .OnClick((_, _) =>
            {
                if (transparentWindow != null) ws.CloseWindow(transparentWindow);
                transparentWindow = SpawnOverlay(ws, "Transparent (default)", currentAlpha, null);
            })
            .Build();

        var acrylicBtn = Controls.Button("Acrylic")
            .OnClick((_, _) =>
            {
                if (transparentWindow != null) ws.CloseWindow(transparentWindow);
                transparentWindow = SpawnOverlay(ws, "Acrylic", currentAlpha, TransparencyBrush.Acrylic());
            })
            .Build();

        var micaBtn = Controls.Button("Mica")
            .OnClick((_, _) =>
            {
                if (transparentWindow != null) ws.CloseWindow(transparentWindow);
                transparentWindow = SpawnOverlay(ws, "Mica", currentAlpha, TransparencyBrush.Mica());
            })
            .Build();

        var tintedBtn = Controls.Button("Tinted")
            .OnClick((_, _) =>
            {
                if (transparentWindow != null) ws.CloseWindow(transparentWindow);
                transparentWindow = SpawnOverlay(ws, "Tinted", currentAlpha, TransparencyBrush.Tinted());
            })
            .Build();

        var controlWindow = new WindowBuilder(ws)
            .WithTitle("Transparency Controls")
            .WithSize(45, 22)
            .AtPosition(55, 2)
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape) { ws.CloseWindow((Window)s!); e.Handled = true; }
            })
            .AddControl(heading)
            .AddControl(alphaLabel)
            .AddControl(alphaSlider)
            .AddControl(defaultBtn)
            .AddControl(acrylicBtn)
            .AddControl(micaBtn)
            .AddControl(tintedBtn)
            .BuildAndShow();

        return opaqueWindow;
    }

    private static Window SpawnOverlay(ConsoleWindowSystem ws, string title, byte alpha, TransparencyBrush? brush)
    {
        var content = Controls.Markup()
            .AddLine($"[bold]{title}[/]")
            .AddEmptyLine()
            .AddLine($"[dim]α = {alpha}[/]")
            .AddLine("[dim]Drag to move over content[/]")
            .Build();

        var builder = new WindowBuilder(ws)
            .WithTitle(title)
            .WithSize(35, 12)
            .AtPosition(15, 6)
            .WithBackgroundColor(new Color(0, 20, 60, alpha))
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape) { ws.CloseWindow((Window)s!); e.Handled = true; }
            })
            .AddControl(content);

        if (brush != null)
            builder = builder.WithTransparencyBrush(brush);

        return builder.BuildAndShow();
    }
}
