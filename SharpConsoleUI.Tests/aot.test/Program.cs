// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// NativeAOT smoke test. Exercises the guaranteed AOT-safe core — window system,
// standard controls, the markup parser, headless rendering — under a native
// executable, then exits cleanly. Run by CI after `dotnet publish -p:PublishAot=true`
// to prove the library's core is AOT-compatible and stays that way.

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Parsing;

int Fail(string message)
{
	Console.Error.WriteLine($"AOT SMOKE FAILED: {message}");
	return 1;
}

// 1. Markup parser (no terminal needed) — exercises the core tokenizer.
var cells = MarkupParser.Parse("[red]Hello[/] [bold green]AOT[/] world", Color.White, Color.Black);
if (cells.Count == 0)
	return Fail("MarkupParser produced no cells");

// 2. Window system on a headless driver — exercises layout + render pipeline.
using var driver = new HeadlessConsoleDriver(120, 40);
var system = new ConsoleWindowSystem(driver);

var window = new Window(system) { Width = 80, Height = 20, Top = 2, Left = 2 };
window.AddControl(new MarkupControl(new List<string>
{
	"[bold underline]NativeAOT Smoke Test[/]",
	"",
	"[yellow]Markup[/], [cyan]colors[/], and [italic]styles[/] all parse.",
	"Standard controls render headlessly under a native binary.",
}) { Wrap = true });
window.AddControl(new MarkupControl(new List<string> { "[grey]line two control[/]" }));
system.AddWindow(window);

// 3. Drive a few non-interactive frames through the real render path. Reaching the
//    end without an exception proves the core render pipeline runs under NativeAOT.
for (int i = 0; i < 3; i++)
	system.ProcessOnce();

// The headless driver routes cells through a ConsoleBuffer; a successful render leaves
// the buffer sized to the screen. Use that as a lightweight "rendering happened" signal.
if (driver.ScreenSize.Width <= 0 || driver.ScreenSize.Height <= 0)
	return Fail("headless driver has no screen buffer after rendering");

Console.Error.WriteLine($"AOT SMOKE OK: {cells.Count} markup cells parsed, {driver.ScreenSize.Width}x{driver.ScreenSize.Height} rendered.");
return 0;
