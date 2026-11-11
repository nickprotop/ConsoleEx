// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// KeyProbe — shows every key the application is told about, and whether the
// global shortcuts for Ctrl+O and Ctrl+Shift+O fired.
//
// It exists to answer one question by eye: does this terminal let the library
// tell Ctrl+Shift+O apart from Ctrl+O? (issue #95)
//
// A control byte carries no case, so Ctrl+O and Ctrl+Shift+O are the same byte
// (0x0F) on a legacy stream, and conhost zeroes the modifier state once
// ENABLE_VIRTUAL_TERMINAL_INPUT is set. The driver asks Windows terminals for
// win32-input-mode, which reports the console record instead and keeps the
// modifier. Windows Terminal and WezTerm honour it; classic conhost ignores it.
//
// WHAT TO LOOK FOR
//   Press Ctrl+O, then Ctrl+Shift+O.
//   Working : two different lines, and two different shortcuts firing.
//   Not     : both say "Mods=Control" and both fire Ctrl+O.
//
// Press Ctrl+Q to quit.

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

var system = new ConsoleWindowSystem(RenderMode.Buffer);

var log = new List<string>();
var view = new MarkupControl(new List<string> { "[grey]press a key[/]" });

void Show(string line)
{
	log.Add(line);
	if (log.Count > 18) log.RemoveAt(0);
	// Newest last, so the eye follows it down the pane.
	view.SetContent(new List<string>(log));
}

// The two bindings the issue is about. Registering both is the thing that was
// impossible before: Ctrl+O's handler swallowed Ctrl+Shift+O as well.
//
// Both DECLINE (return false) so the key carries on to the logger below. A
// shortcut that consumed it would hide the raw Key/Mods for the two keystrokes
// this probe exists to inspect — and on a terminal where neither shortcut
// fires, that detail is the whole diagnosis.
system.RegisterGlobalShortcut(ConsoleModifiers.Control, ConsoleKey.O,
	() => { Show("[bold green]SHORTCUT  Ctrl+O[/]"); return false; });

system.RegisterGlobalShortcut(ConsoleModifiers.Control | ConsoleModifiers.Shift, ConsoleKey.O,
	() => { Show("[bold magenta]SHORTCUT  Ctrl+Shift+O[/]"); return false; });

// Which mechanism is in play, since two very different ones give the same
// outcome: with VT input OFF the console reports the modifier itself, with it
// ON only win32-input-mode can carry it.
if (system.ConsoleDriver is NetConsoleDriver net)
{
	Show(OperatingSystem.IsWindows()
		? $"[grey]VT input: {(net.VirtualTerminalInputEnabled ? "ON (modifier needs win32-input-mode)" : "OFF (console reports the modifier itself)")}[/]"
		: "[grey]VT input: n/a (not Windows)[/]");
}

var window = new WindowBuilder(system)
	.WithTitle("KeyProbe - Ctrl+O vs Ctrl+Shift+O - Ctrl+Q quits")
	.Maximized()
	.AddControl(view)
	// Every key, whether or not a shortcut claimed it: a key that fires nothing
	// and a key that never arrived look identical otherwise.
	.OnKeyPressed((_, e) =>
	{
		var k = e.KeyInfo;
		string mods = k.Modifiers == 0 ? "none" : k.Modifiers.ToString();
		string ch = k.KeyChar < ' '
			? $"0x{(int)k.KeyChar:X2}"
			: $"'{k.KeyChar}' 0x{(int)k.KeyChar:X2}";

		Show($"key  Key=[cyan]{k.Key}[/]  Mods=[yellow]{mods}[/]  Char={ch}");
	})
	.BuildAndShow();

system.Run();
