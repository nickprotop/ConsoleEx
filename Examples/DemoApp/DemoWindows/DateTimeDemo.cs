// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

public static class DateTimeDemo
{
    private const int WindowWidth = 100;
    private const int WindowHeight = 35;
    private const int LeftColumnWidth = 50;

    public static Window Create(ConsoleWindowSystem ws)
    {
        var statusMarkup = Controls.Markup()
            .AddLines("[bold cyan]Status[/]", "", "[dim]Select a date or time to see values here[/]")
            .WithMargin(1, 1, 1, 1)
            .Build();

        // --- Date Pickers ---

        var dateIso = Controls.DatePicker("ISO:")
            .WithFormat("yyyy-MM-dd")
            .WithSelectedDate(DateTime.Today)
            .WithMargin(1, 0, 1, 1)
            .Build();

        var dateUs = Controls.DatePicker("US:")
            .WithFormat("MM/dd/yyyy")
            .WithCulture(new CultureInfo("en-US"))
            .WithSelectedDate(DateTime.Today)
            .WithMargin(1, 0, 1, 1)
            .Build();

        var dateEu = Controls.DatePicker("EU:")
            .WithFormat("dd.MM.yyyy")
            .WithCulture(new CultureInfo("de-DE"))
            .WithSelectedDate(DateTime.Today)
            .WithMargin(1, 0, 1, 1)
            .Build();

        var dateConstrained = Controls.DatePicker("Range:")
            .WithFormat("yyyy-MM-dd")
            .WithMinDate(new DateTime(2020, 1, 1))
            .WithMaxDate(new DateTime(2030, 12, 31))
            .WithSelectedDate(DateTime.Today)
            .WithMargin(1, 0, 1, 1)
            .Build();

        // --- Time Pickers ---

        var time24 = Controls.TimePicker("24h:")
            .With24HourFormat()
            .WithSelectedTime(new TimeSpan(14, 30, 0))
            .WithMargin(1, 0, 1, 1)
            .Build();

        var time24Sec = Controls.TimePicker("24h+s:")
            .With24HourFormat()
            .WithSeconds()
            .WithSelectedTime(new TimeSpan(14, 30, 45))
            .WithMargin(1, 0, 1, 1)
            .Build();

        var time12 = Controls.TimePicker("12h:")
            .With12HourFormat()
            .WithCulture(new CultureInfo("en-US"))
            .WithSelectedTime(new TimeSpan(14, 30, 0))
            .WithMargin(1, 0, 1, 1)
            .Build();

        var time12Sec = Controls.TimePicker("12h+s:")
            .With12HourFormat()
            .WithCulture(new CultureInfo("en-US"))
            .WithSeconds()
            .WithSelectedTime(new TimeSpan(9, 15, 30))
            .WithMargin(1, 0, 1, 1)
            .Build();

        var timeConstrained = Controls.TimePicker("Work:")
            .With24HourFormat()
            .WithMinTime(new TimeSpan(9, 0, 0))
            .WithMaxTime(new TimeSpan(17, 0, 0))
            .WithSelectedTime(new TimeSpan(9, 0, 0))
            .WithMargin(1, 0, 1, 1)
            .Build();

        // --- Combined Example ---

        var eventStartDate = Controls.DatePicker("Start:")
            .WithFormat("yyyy-MM-dd")
            .WithSelectedDate(DateTime.Today)
            .WithMargin(1, 0, 1, 0)
            .Build();

        var eventStartTime = Controls.TimePicker("")
            .With24HourFormat()
            .WithSelectedTime(new TimeSpan(10, 0, 0))
            .WithMargin(1, 0, 1, 1)
            .Build();

        var eventEndDate = Controls.DatePicker("End:")
            .WithFormat("yyyy-MM-dd")
            .WithSelectedDate(DateTime.Today)
            .WithMargin(1, 0, 1, 0)
            .Build();

        var eventEndTime = Controls.TimePicker("")
            .With24HourFormat()
            .WithSelectedTime(new TimeSpan(11, 0, 0))
            .WithMargin(1, 0, 1, 1)
            .Build();

        // --- Update status on any change ---

        void UpdateStatus(object? s, object? e)
        {
            string FormatDate(DatePickerControl dp) =>
                dp.SelectedDate?.ToString("yyyy-MM-dd") ?? "[dim]none[/]";

            string FormatTime(TimePickerControl tp) =>
                tp.SelectedTime.HasValue
                    ? $"{tp.SelectedTime.Value.Hours:D2}:{tp.SelectedTime.Value.Minutes:D2}:{tp.SelectedTime.Value.Seconds:D2}"
                    : "[dim]none[/]";

            statusMarkup.SetContent(new List<string>
            {
                "[bold cyan]Current Values[/]",
                "",
                "[bold]Date Pickers[/]",
                $"  [dim]ISO:[/]   {FormatDate(dateIso)}",
                $"  [dim]US:[/]    {FormatDate(dateUs)}",
                $"  [dim]EU:[/]    {FormatDate(dateEu)}",
                $"  [dim]Range:[/] {FormatDate(dateConstrained)}",
                "",
                "[bold]Time Pickers[/]",
                $"  [dim]24h:[/]   {FormatTime(time24)}",
                $"  [dim]24h+s:[/] {FormatTime(time24Sec)}",
                $"  [dim]12h:[/]   {FormatTime(time12)}",
                $"  [dim]12h+s:[/] {FormatTime(time12Sec)}",
                $"  [dim]Work:[/]  {FormatTime(timeConstrained)}",
                "",
                "[bold]Event[/]",
                $"  [dim]Start:[/] {FormatDate(eventStartDate)} {FormatTime(eventStartTime)}",
                $"  [dim]End:[/]   {FormatDate(eventEndDate)} {FormatTime(eventEndTime)}",
            });
        }

        dateIso.SelectedDateChanged += (s, _) => UpdateStatus(s, null);
        dateUs.SelectedDateChanged += (s, _) => UpdateStatus(s, null);
        dateEu.SelectedDateChanged += (s, _) => UpdateStatus(s, null);
        dateConstrained.SelectedDateChanged += (s, _) => UpdateStatus(s, null);
        time24.SelectedTimeChanged += (s, _) => UpdateStatus(s, null);
        time24Sec.SelectedTimeChanged += (s, _) => UpdateStatus(s, null);
        time12.SelectedTimeChanged += (s, _) => UpdateStatus(s, null);
        time12Sec.SelectedTimeChanged += (s, _) => UpdateStatus(s, null);
        timeConstrained.SelectedTimeChanged += (s, _) => UpdateStatus(s, null);
        eventStartDate.SelectedDateChanged += (s, _) => UpdateStatus(s, null);
        eventStartTime.SelectedTimeChanged += (s, _) => UpdateStatus(s, null);
        eventEndDate.SelectedDateChanged += (s, _) => UpdateStatus(s, null);
        eventEndTime.SelectedTimeChanged += (s, _) => UpdateStatus(s, null);

        UpdateStatus(null, null);

        // --- Layout ---

        var leftPanel = Controls.ScrollablePanel()
            .AddControl(Controls.Rule("Date Pickers"))
            .AddControl(Controls.Markup("[dim]ISO format (yyyy-MM-dd)[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(dateIso)
            .AddControl(Controls.Markup("[dim]US format (MM/dd/yyyy)[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(dateUs)
            .AddControl(Controls.Markup("[dim]European format (dd.MM.yyyy)[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(dateEu)
            .AddControl(Controls.Markup("[dim]Constrained 2020-2030[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(dateConstrained)
            .AddControl(Controls.Rule("Time Pickers"))
            .AddControl(Controls.Markup("[dim]24-hour (HH:MM)[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(time24)
            .AddControl(Controls.Markup("[dim]24-hour with seconds[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(time24Sec)
            .AddControl(Controls.Markup("[dim]12-hour AM/PM[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(time12)
            .AddControl(Controls.Markup("[dim]12-hour with seconds[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(time12Sec)
            .AddControl(Controls.Markup("[dim]Work hours (09:00-17:00)[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(timeConstrained)
            .AddControl(Controls.Rule("Combined Example"))
            .AddControl(Controls.Markup("[dim]Event scheduling[/]").WithMargin(1, 0, 1, 0).Build())
            .AddControl(eventStartDate)
            .AddControl(eventStartTime)
            .AddControl(eventEndDate)
            .AddControl(eventEndTime)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var rightPanel = Controls.ScrollablePanel()
            .AddControl(statusMarkup)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var grid = Controls.HorizontalGrid()
            .Column(col => col.Width(LeftColumnWidth).Add(leftPanel))
            .Column(col => col.Flex().Add(rightPanel))
            .WithSplitterAfter(0)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var header = Controls.Markup("[bold yellow]  Date & Time Controls[/]")
            .StickyTop()
            .Build();

        var statusBar = Controls.Markup("[dim]Tab: next field | Digits: enter value | Esc: close[/]")
            .StickyBottom()
            .Build();

        var gradient = ColorGradient.FromColors(
            new Color(20, 30, 60),
            new Color(10, 10, 25));

        return new WindowBuilder(ws)
            .WithTitle("Date & Time")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
            .AddControls(header, grid, statusBar)
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
