using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Panel;
using SharpConsoleUI.Rendering;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace DemoApp.DemoWindows;

public static class PanelConfigWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var nav = Ctl.NavigationView()
            .WithNavWidth(28)
            .WithPaneHeader("[bold rgb(180,140,255)]  Panel Config[/]")
            .WithSelectedColors(Color.White, new Color(80, 50, 140))
            .WithSelectionIndicator('\u25b8')
            .WithContentBorder(BorderStyle.Rounded)
            .WithContentBorderColor(new Color(80, 60, 120))
            .WithContentBackground(new Color(22, 18, 35))
            .WithContentPadding(1, 0, 1, 0)
            .WithContentHeader(true)
            .AddHeader("Panels", new Color(180, 140, 255), header => header
                .AddItem("Visibility", icon: "\u25cf", subtitle: "Show/hide top and bottom panels",
                    content: panel => BuildVisibilityPage(panel, ws))
                .AddItem("Elements", icon: "\u2756", subtitle: "Add and remove panel elements",
                    content: panel => BuildElementsPage(panel, ws))
                .AddItem("Appearance", icon: "\u2726", subtitle: "Panel colors and styling",
                    content: panel => BuildAppearancePage(panel, ws)))
            .AddHeader("Elements", new Color(120, 200, 160), header => header
                .AddItem("Clock", icon: "\u25f7", subtitle: "Clock element settings",
                    content: panel => BuildClockPage(panel, ws))
                .AddItem("Performance", icon: "\u25a3", subtitle: "Performance metrics display",
                    content: panel => BuildPerformancePage(panel, ws))
                .AddItem("Task Bar", icon: "\u2261", subtitle: "Window list configuration",
                    content: panel => BuildTaskBarPage(panel, ws)))
            .WithAlignment(HorizontalAlignment.Stretch)
            .Fill()
            .Build();

        var window = new WindowBuilder(ws)
            .WithTitle("Panel Configuration")
            .WithSize(80, 28)
            .Centered()
            .WithBackgroundGradient(
                ColorGradient.FromColors(new Color(20, 15, 40), new Color(10, 8, 20)),
                GradientDirection.Vertical)
            .AddControl(nav)
            .BuildAndShow();

        return window;
    }

    private static void BuildVisibilityPage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(180,140,255)]Panel Visibility[/]")
            .AddEmptyLine()
            .AddLine("[dim]Toggle the top and bottom desktop panels on and off.[/]")
            .AddLine("[dim]Panels replace the legacy status bars with pluggable elements.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Desktop Panels")
            .WithColor(new Color(80, 60, 140))
            .Build());

        bool topVisible = ws.TopPanel?.Visible ?? ws.PanelStateService.ShowTopPanel;
        panel.AddControl(Ctl.Checkbox("Show top panel")
            .Checked(topVisible)
            .OnCheckedChanged((_, isChecked) =>
            {
                ws.PanelStateService.ShowTopPanel = isChecked;
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        bool bottomVisible = ws.BottomPanel?.Visible ?? ws.PanelStateService.ShowBottomPanel;
        panel.AddControl(Ctl.Checkbox("Show bottom panel")
            .Checked(bottomVisible)
            .OnCheckedChanged((_, isChecked) =>
            {
                ws.PanelStateService.ShowBottomPanel = isChecked;
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddEmptyLine()
            .AddLine("[dim]Tip: hiding both panels gives you a full-screen desktop area.[/]")
            .Build());
    }

    private static void BuildElementsPage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(180,140,255)]Panel Elements[/]")
            .AddEmptyLine()
            .AddLine("[dim]Add or remove elements from the top and bottom panels at runtime.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Top Panel")
            .WithColor(new Color(80, 60, 140))
            .Build());

        // Add clock to top panel
        panel.AddControl(Ctl.Button("  Add Clock to Top Right  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                if (ws.TopPanel != null && ws.TopPanel.FindElement<ClockElement>("topClock") == null)
                {
                    ws.TopPanel.AddRight(Elements.Clock()
                        .WithFormat("HH:mm:ss")
                        .WithName("topClock")
                        .Build());
                }
            })
            .Build());

        panel.AddControl(Ctl.Button("  Remove Clock from Top  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.TopPanel?.Remove("topClock");
            })
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Bottom Panel")
            .WithColor(new Color(80, 60, 140))
            .Build());

        // Add custom status text to bottom
        panel.AddControl(Ctl.Button("  Add Status to Bottom Right  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                if (ws.BottomPanel != null && ws.BottomPanel.FindElement<StatusTextElement>("customStatus") == null)
                {
                    ws.BottomPanel.AddRight(Elements.StatusText("[green]Ready[/]")
                        .WithName("customStatus")
                        .Build());
                }
            })
            .Build());

        panel.AddControl(Ctl.Button("  Remove Status from Bottom  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.BottomPanel?.Remove("customStatus");
            })
            .Build());

        panel.AddControl(Ctl.Button("  Add Separator + Text to Top  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                if (ws.TopPanel != null && ws.TopPanel.FindElement<StatusTextElement>("dynamicInfo") == null)
                {
                    ws.TopPanel.AddLeft(
                        Elements.Separator().WithName("dynamicSep").Build(),
                        Elements.StatusText($"[yellow]Win:{ws.Windows.Count}[/]")
                            .WithName("dynamicInfo")
                            .Build());
                }
            })
            .Build());

        panel.AddControl(Ctl.Button("  Remove Dynamic Elements  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .WithMargin(0, 1, 0, 0)
            .OnClick((_, _, _) =>
            {
                ws.TopPanel?.Remove("dynamicSep");
                ws.TopPanel?.Remove("dynamicInfo");
            })
            .Build());
    }

    private static void BuildAppearancePage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(180,140,255)]Panel Appearance[/]")
            .AddEmptyLine()
            .AddLine("[dim]Customize panel colors. Changes apply immediately.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Top Panel Colors")
            .WithColor(new Color(80, 60, 140))
            .Build());

        var topColorPresets = new (string name, Color bg, Color fg)[]
        {
            ("Theme Default", default, default),
            ("Dark Blue", new Color(15, 20, 50), new Color(180, 200, 240)),
            ("Dark Green", new Color(10, 30, 15), new Color(140, 220, 160)),
            ("Dark Red", new Color(40, 10, 10), new Color(240, 160, 160)),
            ("Midnight", new Color(8, 8, 20), new Color(120, 120, 180)),
        };

        var topDropdown = Ctl.Dropdown("Color Preset")
            .WithMargin(0, 1, 0, 0);
        foreach (var (name, _, _) in topColorPresets)
            topDropdown.AddItem(name);
        topDropdown.SelectedIndex(0);
        topDropdown.OnSelectionChanged((_, idx) =>
        {
            if (ws.TopPanel == null || idx < 0 || idx >= topColorPresets.Length) return;
            var (_, bg, fg) = topColorPresets[idx];
            if (idx == 0)
            {
                ws.TopPanel.BackgroundColor = null;
                ws.TopPanel.ForegroundColor = null;
            }
            else
            {
                ws.TopPanel.BackgroundColor = bg;
                ws.TopPanel.ForegroundColor = fg;
            }
            ws.TopPanel.MarkDirty();
        });
        panel.AddControl(topDropdown.Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Bottom Panel Colors")
            .WithColor(new Color(80, 60, 140))
            .Build());

        var bottomColorPresets = new (string name, Color bg, Color fg)[]
        {
            ("Theme Default", default, default),
            ("Dark Blue", new Color(15, 20, 50), new Color(180, 200, 240)),
            ("Dark Purple", new Color(25, 12, 35), new Color(200, 170, 240)),
            ("Charcoal", new Color(25, 25, 25), new Color(200, 200, 200)),
            ("Steel", new Color(30, 35, 45), new Color(170, 190, 210)),
        };

        var bottomDropdown = Ctl.Dropdown("Color Preset")
            .WithMargin(0, 1, 0, 0);
        foreach (var (name, _, _) in bottomColorPresets)
            bottomDropdown.AddItem(name);
        bottomDropdown.SelectedIndex(0);
        bottomDropdown.OnSelectionChanged((_, idx) =>
        {
            if (ws.BottomPanel == null || idx < 0 || idx >= bottomColorPresets.Length) return;
            var (_, bg, fg) = bottomColorPresets[idx];
            if (idx == 0)
            {
                ws.BottomPanel.BackgroundColor = null;
                ws.BottomPanel.ForegroundColor = null;
            }
            else
            {
                ws.BottomPanel.BackgroundColor = bg;
                ws.BottomPanel.ForegroundColor = fg;
            }
            ws.BottomPanel.MarkDirty();
        });
        panel.AddControl(bottomDropdown.Build());
    }

    private static void BuildClockPage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(120,200,160)]Clock Element[/]")
            .AddEmptyLine()
            .AddLine("[dim]Configure the clock element in the bottom panel.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Format")
            .WithColor(new Color(60, 120, 80))
            .Build());

        var formats = new[] { "HH:mm", "HH:mm:ss", "hh:mm tt", "hh:mm:ss tt", "yyyy-MM-dd HH:mm" };
        var formatDropdown = Ctl.Dropdown("Time Format")
            .WithMargin(0, 1, 0, 0);
        for (int i = 0; i < formats.Length; i++)
            formatDropdown.AddItem(formats[i], i.ToString());
        formatDropdown.SelectedIndex(1); // HH:mm:ss default

        formatDropdown.OnSelectionChanged((_, idx) =>
        {
            if (idx >= 0 && idx < formats.Length)
            {
                var clock = ws.BottomPanel?.FindElement<ClockElement>("clock")
                    ?? ws.TopPanel?.FindElement<ClockElement>("topClock");
                if (clock != null)
                    clock.Format = formats[idx];
            }
        });
        panel.AddControl(formatDropdown.Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Update Interval")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Ctl.Slider()
            .WithRange(100, 5000)
            .WithValue(1000)
            .WithStep(100)
            .ShowValueLabel()
            .WithValueFormat("0ms")
            .WithTrackColor(new Color(40, 80, 60))
            .WithFilledTrackColor(new Color(80, 180, 120))
            .WithThumbColor(new Color(120, 200, 160))
            .OnValueChanged((_, value) =>
            {
                var clock = ws.BottomPanel?.FindElement<ClockElement>("clock")
                    ?? ws.TopPanel?.FindElement<ClockElement>("topClock");
                if (clock != null)
                    clock.UpdateIntervalMs = (int)value;
            })
            .WithMargin(0, 1, 0, 0)
            .Stretch()
            .Build());
    }

    private static void BuildPerformancePage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(120,200,160)]Performance Metrics[/]")
            .AddEmptyLine()
            .AddLine("[dim]Control the performance metrics element in the top panel.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Metrics")
            .WithColor(new Color(60, 120, 80))
            .Build());

        panel.AddControl(Ctl.Checkbox("Enable performance metrics")
            .Checked(ws.Performance.IsPerformanceMetricsEnabled)
            .OnCheckedChanged((_, isChecked) =>
            {
                ws.Performance.SetPerformanceMetrics(isChecked);
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Display Options")
            .WithColor(new Color(60, 120, 80))
            .Build());

        var perf = ws.TopPanel?.FindElement<PerformanceElement>("performance");

        panel.AddControl(Ctl.Checkbox("Show FPS")
            .Checked(perf?.ShowFPS ?? true)
            .OnCheckedChanged((_, isChecked) =>
            {
                var p = ws.TopPanel?.FindElement<PerformanceElement>("performance");
                if (p != null) p.ShowFPS = isChecked;
                ws.TopPanel?.MarkDirty();
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.Checkbox("Show dirty characters")
            .Checked(perf?.ShowDirtyChars ?? true)
            .OnCheckedChanged((_, isChecked) =>
            {
                var p = ws.TopPanel?.FindElement<PerformanceElement>("performance");
                if (p != null) p.ShowDirtyChars = isChecked;
                ws.TopPanel?.MarkDirty();
            })
            .WithMargin(0, 1, 0, 0)
            .Build());
    }

    private static void BuildTaskBarPage(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(120,200,160)]Task Bar[/]")
            .AddEmptyLine()
            .AddLine("[dim]Configure the window list in the bottom panel.[/]")
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Display")
            .WithColor(new Color(60, 120, 80))
            .Build());

        var taskBar = ws.BottomPanel?.FindElement<TaskBarElement>("taskbar")
            ?? ws.BottomPanel?.FindElement<TaskBarElement>("legacyTaskbar");

        panel.AddControl(Ctl.Checkbox("Dim minimized windows")
            .Checked(taskBar?.MinimizedDim ?? true)
            .OnCheckedChanged((_, isChecked) =>
            {
                var tb = ws.BottomPanel?.FindElement<TaskBarElement>("taskbar")
                    ?? ws.BottomPanel?.FindElement<TaskBarElement>("legacyTaskbar");
                if (tb != null) tb.MinimizedDim = isChecked;
                ws.BottomPanel?.MarkDirty();
            })
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Ctl.Markup()
            .AddEmptyLine()
            .AddLine("[dim]The task bar shows all top-level windows.[/]")
            .AddLine("[dim]Click a window entry to switch to it.[/]")
            .AddLine("[dim]Click a minimized window to restore it.[/]")
            .Build());
    }
}
