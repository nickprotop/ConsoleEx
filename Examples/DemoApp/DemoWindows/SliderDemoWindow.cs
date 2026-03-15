// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

public static class SliderDemoWindow
{
    private const int WindowWidth = 80;
    private const int WindowHeight = 32;

    public static Window Create(ConsoleWindowSystem ws)
    {
        // --- Section 1: Volume (basic horizontal slider) ---

        var volumeStatus = Controls.Markup("[dim]Volume: 50[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        var volumeSlider = Controls.Slider()
            .WithRange(0, 100)
            .WithValue(50)
            .ShowValueLabel()
            .WithMargin(1, 0, 1, 1)
            .OnValueChanged((s, val) =>
            {
                volumeStatus.SetContent(new List<string>
                {
                    $"[dim]Volume: [bold cyan]{val:F0}[/][/]"
                });
            })
            .Build();

        // --- Section 2: Brightness (Step 5) ---

        var brightnessSlider = Controls.Slider()
            .WithRange(0, 100)
            .WithValue(50)
            .WithStep(5)
            .ShowMinMaxLabels()
            .WithMargin(1, 0, 1, 1)
            .Build();

        // --- Section 3: Custom Colors ---

        var customColorSlider = Controls.Slider()
            .WithRange(0, 100)
            .WithValue(30)
            .WithTrackColor(Color.Green)
            .WithFilledTrackColor(Color.Red)
            .WithThumbColor(Color.Magenta1)
            .ShowValueLabel()
            .WithMargin(1, 0, 1, 1)
            .Build();

        // --- Section 4: Vertical Sliders (Bass / Treble) ---

        var bassSlider = Controls.Slider()
            .Vertical()
            .WithRange(0, 100)
            .WithValue(60)
            .WithHeight(10)
            .ShowValueLabel()
            .WithMargin(1, 0, 1, 0)
            .Build();

        var trebleSlider = Controls.Slider()
            .Vertical()
            .WithRange(0, 100)
            .WithValue(40)
            .WithHeight(10)
            .ShowValueLabel()
            .WithMargin(1, 0, 1, 0)
            .Build();

        var verticalGrid = Controls.HorizontalGrid()
            .Column(col => col.Flex()
                .Add(Controls.Header("Bass"))
                .Add(bassSlider))
            .Column(col => col.Flex()
                .Add(Controls.Header("Treble"))
                .Add(trebleSlider))
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 1)
            .Build();

        // --- Section 5: Price Range (RangeSlider) ---

        var priceStatus = Controls.Markup("[dim]Range: $0 - $1000[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        var priceRange = Controls.RangeSlider()
            .WithRange(0, 1000)
            .WithValues(200, 800)
            .WithStep(10)
            .ShowValueLabel()
            .WithMargin(1, 0, 1, 1)
            .OnRangeChanged((s, range) =>
            {
                priceStatus.SetContent(new List<string>
                {
                    $"[dim]Range: [bold green]${range.Low:F0}[/] - [bold green]${range.High:F0}[/][/]"
                });
            })
            .Build();

        // --- Section 6: Constrained Range ---

        var constrainedRange = Controls.RangeSlider()
            .WithRange(0, 100)
            .WithValues(30, 70)
            .WithMinRange(20)
            .ShowMinMaxLabels()
            .WithMargin(1, 0, 1, 1)
            .Build();

        // --- Layout ---

        var panel = Controls.ScrollablePanel()
            .AddControl(Controls.Header("Volume"))
            .AddControl(Controls.Markup("[dim]Basic horizontal slider 0-100[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(volumeSlider)
            .AddControl(volumeStatus)
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Brightness (Step 5)"))
            .AddControl(Controls.Markup("[dim]Slider with step=5, showing min/max labels[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(brightnessSlider)
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Custom Colors"))
            .AddControl(Controls.Markup("[dim]Track=Green, Filled=Red, Thumb=Magenta[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(customColorSlider)
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Vertical Sliders"))
            .AddControl(verticalGrid)
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Price Range"))
            .AddControl(Controls.Markup("[dim]RangeSlider 0-1000, step=10[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(priceRange)
            .AddControl(priceStatus)
            .AddControl(Controls.Rule(""))
            .AddControl(Controls.Header("Constrained Range"))
            .AddControl(Controls.Markup("[dim]MinRange=20, cannot narrow below 20 units[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(constrainedRange)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var statusBar = Controls.Markup("[dim]Arrow keys: adjust | Tab: next slider | Esc: close[/]")
            .StickyBottom()
            .Build();

        var gradient = ColorGradient.FromColors(
            new Color(10, 45, 30),
            new Color(25, 60, 55),
            new Color(15, 35, 50));

        return new WindowBuilder(ws)
            .WithTitle("Slider Controls")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
            .AddControls(panel, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }
}
