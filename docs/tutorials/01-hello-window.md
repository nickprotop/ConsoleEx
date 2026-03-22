# Tutorial 1: Hello Window

> **Difficulty:** Beginner | **Prerequisites:** .NET 8+ installed | **Estimated reading time:** ~5 minutes
>
> **←** [Tutorials overview](../README.md#tutorials) | **Next →** [Tutorial 2: Live Dashboard](02-dashboard.md)

---

**What you'll build:** A single window with a title, a label, and a Quit button — the minimal complete SharpConsoleUI application.

Most terminal programs use immediate-mode output: you call `Console.Write`, the characters land on screen, and the terminal holds them — there is no concept of redrawing or responding to resize. SharpConsoleUI uses a retained-mode model instead: you declare a tree of controls, and the framework owns rendering, redraws, and input routing for the lifetime of the application. This is the same model used by WPF, Avalonia, and WinForms, just targeting the terminal rather than a graphical display. The result is that your code describes *what* the UI should look like, not *when* to paint each pixel.

## Step 1: Create the project

Create a new console project and add the SharpConsoleUI package:

```bash
dotnet new console -n HelloWindow
cd HelloWindow
dotnet add package SharpConsoleUI
```

Open `Program.cs` — replace its contents as we go through each step.

## Step 2: Create the window system

The `ConsoleWindowSystem` owns all windows; `NetConsoleDriver` abstracts terminal output and enables headless testing.

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Drivers;

var driver = new NetConsoleDriver(RenderMode.Buffer);
var windowSystem = new ConsoleWindowSystem(driver);
```

Nothing visible yet — the system is set up but no windows have been added.

## Step 3: Create a window

`WindowBuilder` gives you a fluent API for configuring a window before it's added to the system.

```csharp
using SharpConsoleUI.Builders;

var window = new WindowBuilder(windowSystem)
    .WithTitle("Hello World")
    .WithSize(50, 12)
    .Centered()
    .Build();
```

Run `dotnet run` after adding `windowSystem.AddWindow(window); windowSystem.Run();` (covered in Step 6) — you'll see a bordered window centred in the terminal.

## Step 4: Add a label

`MarkupControl` displays styled text using the `[tag]text[/]` markup syntax — controls stack vertically inside the window by default.

```csharp
using SharpConsoleUI.Controls;

window.AddControl(new MarkupControl(new List<string>
{
    "[bold cyan]Hello, SharpConsoleUI![/]"
}));
```

The label appears on the first line of the window's content area.

See [Markup Syntax](../MARKUP_SYNTAX.md) for the full tag reference.

## Step 5: Add a Quit button

`ButtonControl` is focusable and interactive — pressing Enter or clicking it fires the `Click` event.

```csharp
window.AddControl(Controls.Button("Quit")
    .OnClick((sender, e, win) =>
    {
        windowSystem.Shutdown();
    })
    .Build());
```

Tab to the button and press Enter (or click it) to exit. The `win` parameter in `OnClick` lets you call `FindControl<T>()` to reach other controls in the same window.

## Step 6: Add the window and run

`AddWindow` registers the window with the system; `Run` starts the render and input loop and blocks until `Shutdown()` is called.

```csharp
windowSystem.AddWindow(window);
windowSystem.Run();
```

The full app is now running. Press Tab to switch focus, Enter to click the button.

## Complete Program.cs

```csharp
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

var driver = new NetConsoleDriver(RenderMode.Buffer);
var windowSystem = new ConsoleWindowSystem(driver);

var window = new WindowBuilder(windowSystem)
    .WithTitle("Hello World")
    .WithSize(50, 12)
    .Centered()
    .Build();

window.AddControl(new MarkupControl(new List<string>
{
    "[bold cyan]Hello, SharpConsoleUI![/]"
}));

window.AddControl(Controls.Button("Quit")
    .OnClick((sender, e, win) => windowSystem.Shutdown())
    .Build());

windowSystem.AddWindow(window);
windowSystem.Run();
```

## What you learned

- Window system and driver — `ConsoleWindowSystem` + `NetConsoleDriver`
- `WindowBuilder` — fluent API for size, title, position
- `window.AddControl()` — controls stack vertically by default
- `MarkupControl` — styled text with `[bold]`, `[cyan]`, etc.
- `ButtonControl` with `OnClick` handler
- `windowSystem.Shutdown()` — stops the run loop
- `windowSystem.AddWindow()` + `windowSystem.Run()` — the startup sequence

---

**Next →** [Tutorial 2: Live Dashboard](02-dashboard.md) — async window threads, live-updating panels, and a graph control.
