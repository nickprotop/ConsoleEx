// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Gallery;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;

// Photographs every control in Shots.All and writes one PNG each.
//
//     dotnet run --project Examples/Gallery -- [output-directory]
//
// Headless, so it runs in CI with no terminal; Slate, because the gallery is
// the one place the library's look is chosen rather than inherited; and
// fontSource "embedded", because that is what makes a rebuild produce the same
// bytes instead of a diff.

// TerminalControl re-launches THIS executable as its PTY shim, so the flag has
// to be handled before anything reads args - otherwise the child falls through
// to normal startup and takes "--pty-shim" for an output directory, which is
// exactly how a folder of that name once appeared in the repo root.
if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

// Pin the culture before any control is built. TimePicker and DatePicker read
// CultureInfo.CurrentCulture - the time picker decides 12h vs 24h from the
// locale's ShortTimePattern - so the same code photographs differently on a
// machine set to el_GR than on a CI runner set to en_US: different text,
// different widths, a different picture. Invariant is 24-hour, so the shot is
// the same everywhere and a byte comparison means what it claims to.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

const string Theme = "Slate";

string outDir = args.Length > 0
	? args[0]
	: Path.Combine(FindRepoRoot(), "docs", "images", "controls");

Directory.CreateDirectory(outDir);

// The widest and tallest shot decides the screen; each window is positioned at
// the top left and the picture is cropped to it, so one screen serves them all.
int screenW = Shots.All.Max(x => x.Width) + 4;
int screenH = Shots.All.Max(x => x.Height) + 4;

string socket = Path.Combine(Path.GetTempPath(), $"gallery-{Guid.NewGuid():N}.sock");

var options = ConsoleWindowSystemOptions.Create() with
{
	DiagnosticsSocket = socket,
	PipedInput = new PipedInputOptions(Enabled: false),

	// No desktop furniture. A control's photograph should contain the control,
	// not an application title bar, a clock and a taskbar.
	ShowTopPanel = false,
	ShowBottomPanel = false,
};

var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(screenW, screenH), options: options);
system.ThemeStateService.SwitchTheme(Theme);

// ONE system for every shot. Starting a whole window system per control -
// construct, open a socket, wait for it, render, tear down - cost almost all
// of the running time and bought nothing: the windows do not interact.
using var session = system.RunInBackground();

int written = 0;
int failed = 0;

try
{
	WaitForSocket(socket);

	foreach (var shot in Shots.All)
	{
		try
		{
			Capture(system, socket, shot, outDir);
			written++;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"  {shot.Control}: {ex.Message}");
			failed++;
		}
	}
}
finally
{
	session.Dispose();
	try { File.Delete(socket); } catch { /* gone already */ }
}

Console.WriteLine($"{written} written to {outDir}" + (failed > 0 ? $", {failed} failed" : ""));
return failed == 0 ? 0 : 1;

// ── One screenshot ───────────────────────────────────────────────────────

static void Capture(ConsoleWindowSystem system, string socket, Shot shot, string outDir)
{
	// Borderless and untitled: on a control's own page the subject is the
	// CONTROL, and a title bar with buttons nobody can press is chrome around
	// a picture of something else.
	var window = new WindowBuilder(system)
		.WithSize(shot.Width + 2, shot.Height + 2)
		.WithPosition(1, 1)
		.Borderless()
		.HideTitle()
		.HideTitleButtons()
		.Resizable(false)
		.Movable(false)
		.AddControl(shot.Build())
		.Build();

	system.AddWindow(window);

	try
	{
		// The window is painted before anything is asked of it.
		WaitForFrames(socket, 2);

		// A control that starts a PROCESS cannot be timed by frames: the pty
		// runs in another process and the window paints happily while it is
		// still empty. Wait for the screen to actually say something instead,
		// which is both faster when it is quick and safe when it is not.
		if (shot.SettleMs > 0) WaitForSettled(socket, shot.SettleMs);

		// Cropped to the window: one screen is shared by every shot, so the
		// picture has to say which part of it is this control.
		var region = (X: 1, Y: 1, W: shot.Width + 2, H: shot.Height + 2);

		// Keys with no suffix set up the ONE picture - a dropdown opened, a
		// calendar showing - so they are sent before it. With a suffix they
		// produce a second state, and the first picture is taken before them.
		if (shot.Then is { Length: > 0 } keys)
		{
			if (shot.Suffix != null) Shoot(socket, outDir, shot.Control, region);

			foreach (string key in keys)
			{
				Ask(socket, new JsonObject { ["cmd"] = "key", ["spec"] = key });
			}

			WaitForFrames(socket, 3);

			Shoot(socket, outDir,
				shot.Suffix != null ? $"{shot.Control}-{shot.Suffix}" : shot.Control,
				region);
		}
		else
		{
			Shoot(socket, outDir, shot.Control, region);
		}
	}
	finally
	{
		system.CloseWindow(window, force: true);
		WaitForFrames(socket, 1);
	}
}

/// <summary>Takes one picture and says what it got.</summary>
static void Shoot(string socket, string outDir, string name, (int X, int Y, int W, int H) region)
{
	string path = Path.Combine(outDir, name + ".png");

	// A screenshot someone took by hand is never overwritten. Some controls
	// cannot photograph themselves here - a terminal needs a pty with something
	// running in it, an HTML page needs images that only arrive on the URL path -
	// and a hand-taken capture of the real thing beats a generated picture of an
	// empty rectangle. Drop a ".manual" file beside the image to claim it.
	if (File.Exists(path + ".manual"))
	{
		Console.WriteLine($"  {name,-30} kept (manual)");
		return;
	}

	var request = new JsonObject
	{
		["cmd"] = "image",
		["composed"] = true,
		["glyphs"] = true,
		["fontSource"] = "embedded",
		["path"] = path,
		["x"] = region.X,
		["y"] = region.Y,
		["w"] = region.W,
		["h"] = region.H,
	};

	var reply = Ask(socket, request);

	if (reply["ok"]?.GetValue<bool>() != true)
		throw new InvalidOperationException(reply["error"]?.GetValue<string>() ?? "refused");

	Console.WriteLine($"  {name,-30} {reply["w"]}x{reply["h"]}");
}

/// <summary>Waits until the screen has more than a handful of characters on it.</summary>
/// <remarks>
/// An empty terminal paints as a black rectangle, and a frame counter says
/// that frame was rendered perfectly happily. The screen's own text is the
/// only thing that says the process inside it has started.
/// </remarks>
static void WaitForSettled(string socket, int budgetMs)
{
	var until = DateTime.UtcNow.AddMilliseconds(budgetMs);

	while (DateTime.UtcNow < until)
	{
		var reply = Ask(socket, new JsonObject { ["cmd"] = "snapshot" });

		if (reply["lines"] is JsonArray lines)
		{
			string screen = string.Join("\n", lines.Select(l => l?.GetValue<string>() ?? ""));

			// A control that is still fetching says so on its own screen. The
			// banner is the only honest signal that the picture is not final -
			// a frame counter ticks happily while an image is still coming.
			bool busy = screen.Contains("Loading", StringComparison.OrdinalIgnoreCase)
				|| screen.Contains("Fetching", StringComparison.OrdinalIgnoreCase)
				|| screen.Contains("Rendering", StringComparison.OrdinalIgnoreCase);

			int ink = screen.Count(c => !char.IsWhiteSpace(c));

			if (!busy && ink > 40) { WaitForFrames(socket, 4); return; }
		}

		Thread.Sleep(100);
	}
}

/// <summary>Waits until the application has painted n more frames.</summary>
/// <remarks>
/// Input is asynchronous: sending a key and photographing immediately catches
/// the screen before the key has been acted on. The frame counter is the only
/// honest way to know the result is on screen.
/// </remarks>
static void WaitForFrames(string socket, int count)
{
	long From() => Ask(socket, new JsonObject { ["cmd"] = "frame" })["frame"]!.GetValue<long>();

	long target = From() + count;
	var until = DateTime.UtcNow.AddSeconds(5);

	while (DateTime.UtcNow < until)
	{
		if (From() >= target) return;
		Thread.Sleep(30);
	}
}

/// <summary>Waits for the application to start serving.</summary>
static void WaitForSocket(string path)
{
	var until = DateTime.UtcNow.AddSeconds(20);

	while (DateTime.UtcNow < until)
	{
		if (File.Exists(path)) return;
		Thread.Sleep(40);
	}

	throw new TimeoutException($"no socket at {path} after 20s");
}

/// <summary>One request, one reply, over the diagnostics socket.</summary>
/// <remarks>
/// The public path, the same one cxdbg uses. Reaching into the library's
/// internals would photograph a control through a door nobody else can open.
/// </remarks>
static JsonObject Ask(string path, JsonObject request)
{
	using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
	client.Connect(new UnixDomainSocketEndPoint(path));

	client.Send(Encoding.UTF8.GetBytes(request.ToJsonString() + "\n"));

	var buffer = new byte[64 * 1024];
	var sb = new StringBuilder();

	// The server greets, then answers. The last complete line is the reply.
	while (true)
	{
		int read = client.Receive(buffer);
		if (read == 0) break;

		sb.Append(Encoding.UTF8.GetString(buffer, 0, read));

		// A line is only complete once its newline has arrived. Taking the last
		// chunk instead parsed a half-written reply the moment one arrived in
		// more than one packet - which a big one always does, and which read as
		// "Expected end of string, but instead reached end of data".
		string text = sb.ToString();

		int end = text.LastIndexOf('\n');
		if (end < 0) continue;

		string[] lines = text[..end].Split('\n', StringSplitOptions.RemoveEmptyEntries);

		// The greeting, then the reply.
		if (lines.Length < 2) continue;

		return JsonNode.Parse(lines[^1])!.AsObject();
	}

	throw new InvalidOperationException("the application closed without answering");
}

/// <summary>The repository root, found by walking up from here.</summary>
static string FindRepoRoot()
{
	var dir = new DirectoryInfo(AppContext.BaseDirectory);

	while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "docs")))
		dir = dir.Parent;

	return dir?.FullName
		?? throw new InvalidOperationException("could not find the repository root");
}
