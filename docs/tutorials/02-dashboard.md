# Tutorial 2: Live Dashboard

> **Difficulty:** Intermediate | **Prerequisites:** Tutorial 1 or familiarity with the basics | **Estimated reading time:** ~15 minutes
>
> **←** [Tutorial 1: Hello Window](01-hello-window.md) | **Next →** [Tutorial 3: Settings App](03-settings-app.md)

---

**What you'll build:** A fullscreen two-column dashboard — a stats panel with a live CPU graph on the left, a scrolling log on the right. Both update automatically every second.

This tutorial is standalone — start fresh with `dotnet new console`, no code carried over from Tutorial 1.

---

## Step 1: Create the project

Scaffold a new console project and add the SharpConsoleUI package.

```bash
dotnet new console -n Dashboard
cd Dashboard
dotnet add package SharpConsoleUI
```

This gives you a minimal `Program.cs` — replace its contents entirely with the code that follows.

---

## Step 2: Create a fullscreen window

Both alignment values are set to fill all available terminal space.

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;

var driver = new NetConsoleDriver(RenderMode.Buffer);
var windowSystem = new ConsoleWindowSystem(driver);

var window = new WindowBuilder(windowSystem)
    .WithTitle("Dashboard")
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithHorizontalAlignment(HorizontalAlignment.Stretch)
    .Build();
```

`HorizontalAlignment.Stretch` fills the width — note this is not `Fill`, which belongs to `VerticalAlignment`.

---

## Step 3: Add a two-column layout

`HorizontalGridControl` divides space into columns; `FlexFactor` controls proportional width distribution.

```csharp
using SharpConsoleUI.Controls;

var grid = new HorizontalGridControl();
grid.HorizontalAlignment = HorizontalAlignment.Stretch;
grid.VerticalAlignment = VerticalAlignment.Fill;

var leftCol  = new ColumnContainer(grid) { FlexFactor = 1 };
var rightCol = new ColumnContainer(grid) { FlexFactor = 2 };

grid.AddColumn(leftCol);
grid.AddColumn(rightCol);
window.AddControl(grid);
```

The right column gets twice the width of the left. Both grow to fill the window.

---

## Step 4: Add the stats panel

Add a bordered `ScrollablePanelControl` to the left column, with two markup labels and a graph inside.

```csharp
var statsPanel = new ScrollablePanelControl
{
    BorderStyle = BorderStyle.Rounded,
    Header = " Stats ",
    VerticalAlignment = VerticalAlignment.Fill
};

var uptimeLabel = new MarkupControl(new List<string> { "Uptime: --" }) { Name = "uptime" };
var timeLabel   = new MarkupControl(new List<string> { "Time:   --" }) { Name = "time" };

var graph = new LineGraphControl
{
    Name = "cpu",
    Height = 8,
    MinValue = 0,
    MaxValue = 100
};
graph.AddSeries("CPU", Color.Cyan1);

statsPanel.AddControl(uptimeLabel);
statsPanel.AddControl(timeLabel);
statsPanel.AddControl(graph);
leftCol.AddContent(statsPanel);
```

The `Name` properties on controls let you find them later with `FindControl<T>()`.

The graph starts empty — it fills with data points once the async thread starts.

---

## Step 5: Add the scrolling log

`AutoScroll = true` keeps the view pinned to the bottom as new controls are appended.

```csharp
var logPanel = new ScrollablePanelControl
{
    BorderStyle = BorderStyle.Rounded,
    Header = " Log ",
    AutoScroll = true,
    VerticalAlignment = VerticalAlignment.Fill
};
rightCol.AddContent(logPanel);
```

Each log entry is a new `MarkupControl` added dynamically via `logPanel.AddControl(...)`. The panel tracks total content height and scrolls automatically.

---

## Step 6: Add the async window thread

`WithAsyncWindowThread` gives the window a background task that runs until the app exits — the right place for polling or live-data loops.

**Variable declaration order is critical here.** Because `WithAsyncWindowThread` is a builder method, the lambda is created during `Build()`. Every variable the lambda captures must be declared *before* the `WindowBuilder` call. This means the control variables from Steps 4 and 5 — `statsPanel`, `uptimeLabel`, `timeLabel`, `graph`, `logPanel`, and any other captured locals — must all be moved to the top of the file, before the builder. Layout wiring (adding controls to columns, adding the grid to the window) happens after `Build()`.

The restructured declaration order looks like this:

```csharp
// These are declared before the WindowBuilder call because the async lambda captures them:
var startTime = DateTime.Now;
var random    = new Random();
// statsPanel, uptimeLabel, timeLabel, graph, logPanel — all declared above

var window = new WindowBuilder(windowSystem)
    .WithTitle("Dashboard")
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithHorizontalAlignment(HorizontalAlignment.Stretch)
    .WithAsyncWindowThread(async (win, ct) =>
    {
        // update loop goes here — see Step 7
    })
    .Build();

// Layout wiring happens after Build():
// add controls to columns, add grid to window
```

The full restructured file is in the Complete Program.cs section below.

---

## Step 7: Update controls each second

Inside the async delegate, loop on a one-second delay and push fresh data to each control.

```csharp
while (!ct.IsCancellationRequested)
{
    await Task.Delay(1000, ct);

    var elapsed = DateTime.Now - startTime;
    var cpu     = random.Next(10, 90);

    win.FindControl<MarkupControl>("uptime")?.SetContent(
        new List<string> { $"Uptime: [green]{elapsed:hh\\:mm\\:ss}[/]" });

    win.FindControl<MarkupControl>("time")?.SetContent(
        new List<string> { $"Time:   [cyan]{DateTime.Now:HH:mm:ss}[/]" });

    win.FindControl<LineGraphControl>("cpu")?.AddDataPoint("CPU", cpu);

    var color = cpu > 70 ? "red" : "green";
    logPanel.AddControl(new MarkupControl(
        new List<string> { $"[dim]{DateTime.Now:HH:mm:ss}[/] CPU [{color}]{cpu}%[/]" }));
}
```

The graph fills left-to-right; old points scroll off the left edge. The log accumulates downward and scrolls automatically.

---

## Complete Program.cs

The following is the full, correctly structured file. All control variables are declared before the `WindowBuilder` call so the async lambda can capture them. Layout wiring follows `Build()`.

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;

var driver = new NetConsoleDriver(RenderMode.Buffer);
var windowSystem = new ConsoleWindowSystem(driver);

// Declare all controls before the WindowBuilder call so the async lambda can capture them.
var statsPanel = new ScrollablePanelControl
{
    BorderStyle = BorderStyle.Rounded,
    Header = " Stats ",
    VerticalAlignment = VerticalAlignment.Fill
};

var uptimeLabel = new MarkupControl(new List<string> { "Uptime: --" }) { Name = "uptime" };
var timeLabel   = new MarkupControl(new List<string> { "Time:   --" }) { Name = "time" };

var graph = new LineGraphControl
{
    Name = "cpu",
    Height = 8,
    MinValue = 0,
    MaxValue = 100
};
graph.AddSeries("CPU", Color.Cyan1);

var logPanel = new ScrollablePanelControl
{
    BorderStyle = BorderStyle.Rounded,
    Header = " Log ",
    AutoScroll = true,
    VerticalAlignment = VerticalAlignment.Fill
};

var startTime = DateTime.Now;
var random    = new Random();

var window = new WindowBuilder(windowSystem)
    .WithTitle("Dashboard")
    .WithVerticalAlignment(VerticalAlignment.Fill)
    .WithHorizontalAlignment(HorizontalAlignment.Stretch)
    .WithAsyncWindowThread(async (win, ct) =>
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);

            var elapsed = DateTime.Now - startTime;
            var cpu     = random.Next(10, 90);

            win.FindControl<MarkupControl>("uptime")?.SetContent(
                new List<string> { $"Uptime: [green]{elapsed:hh\\:mm\\:ss}[/]" });

            win.FindControl<MarkupControl>("time")?.SetContent(
                new List<string> { $"Time:   [cyan]{DateTime.Now:HH:mm:ss}[/]" });

            win.FindControl<LineGraphControl>("cpu")?.AddDataPoint("CPU", cpu);

            var color = cpu > 70 ? "red" : "green";
            logPanel.AddControl(new MarkupControl(
                new List<string> { $"[dim]{DateTime.Now:HH:mm:ss}[/] CPU [{color}]{cpu}%[/]" }));
        }
    })
    .Build();

// Wire up the layout after Build().
var grid = new HorizontalGridControl();
grid.HorizontalAlignment = HorizontalAlignment.Stretch;
grid.VerticalAlignment   = VerticalAlignment.Fill;

var leftCol  = new ColumnContainer(grid) { FlexFactor = 1 };
var rightCol = new ColumnContainer(grid) { FlexFactor = 2 };

statsPanel.AddControl(uptimeLabel);
statsPanel.AddControl(timeLabel);
statsPanel.AddControl(graph);
leftCol.AddContent(statsPanel);

rightCol.AddContent(logPanel);

grid.AddColumn(leftCol);
grid.AddColumn(rightCol);
window.AddControl(grid);

windowSystem.AddWindow(window);
windowSystem.Run();
```

---

## What you learned

- Fullscreen layout with `VerticalAlignment.Fill` + `HorizontalAlignment.Stretch`
- `HorizontalGridControl` + `ColumnContainer` with `FlexFactor` for proportional columns
- `ScrollablePanelControl` with border and header
- `AutoScroll = true` for live log panels
- `WithAsyncWindowThread` — background update loop pattern
- Variable declaration order: captured variables must precede the `WindowBuilder` call
- `FindControl<T>("name")` — look up controls by name
- `SetContent(List<string>)` — update a MarkupControl's text
- `LineGraphControl.AddSeries()` + `AddDataPoint()` — live graph updates
- Dynamic control addition (`panel.AddControl(...)`) for log entries
- Markup color tags (`[green]`, `[red]`, `[dim]`, `[cyan]`)

---

**←** [Tutorial 1: Hello Window](01-hello-window.md)
**Next →** [Tutorial 3: Settings App](03-settings-app.md) — NavigationView, gradient theming, forms, and multi-window.
