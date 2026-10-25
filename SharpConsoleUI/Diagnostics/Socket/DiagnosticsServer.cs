// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Diagnostics.Socket;

/// <summary>
/// A Unix-domain socket that lets another program on this machine inspect and drive a running
/// SharpConsoleUI application.
/// </summary>
/// <remarks>
/// <para>
/// One request per line, one response per line, JSON. Deliberately consumable with
/// <c>socat</c> and <c>jq</c> alone: a protocol that needs its own client to be legible is a
/// protocol that will rot.
/// </para>
/// <para>
/// Opened only after <see cref="DiagnosticsConsent"/> allows it. Everything that mutates
/// state marshals through <c>EnqueueOnUIThread</c> and waits for the next render, so
/// <c>key</c> followed by <c>snapshot</c> is coherent rather than racing.
/// </para>
/// </remarks>
internal sealed class DiagnosticsServer : IDisposable
{
	private readonly ConsoleWindowSystem _system;
	private readonly string _path;
	private readonly CancellationTokenSource _cts = new();
	private System.Net.Sockets.Socket? _listener;
	private Task? _acceptLoop;

	internal DiagnosticsServer(ConsoleWindowSystem system, string path)
	{
		_system = system;
		_path = path;
	}

	internal void Start()
	{
		// A stale socket file from a crashed run would make Bind fail.
		try { if (File.Exists(_path)) File.Delete(_path); } catch { /* reported by Bind */ }

		_listener = new System.Net.Sockets.Socket(
			AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
		_listener.Bind(new UnixDomainSocketEndPoint(_path));
		_listener.Listen(4);

		// Owner-only. A channel that exposes screen content and accepts control commands has
		// no business being group- or world-reachable.
		try { File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
		catch { /* best effort; not all filesystems support it */ }

		_acceptLoop = Task.Run(AcceptLoopAsync);
		_system.LogService.LogInfo($"Diagnostics socket listening at {_path}", "Diagnostics");
	}

	private async Task AcceptLoopAsync()
	{
		while (!_cts.IsCancellationRequested)
		{
			System.Net.Sockets.Socket client;
			try
			{
				client = await _listener!.AcceptAsync(_cts.Token);
			}
			catch (OperationCanceledException) { return; }
			catch (ObjectDisposedException) { return; }
			catch (SocketException) { return; }

			// One client at a time is deliberate: concurrent probes mutating the same app
			// would make every answer ambiguous.
			_ = Task.Run(() => ServeAsync(client));
		}
	}

	private async Task ServeAsync(System.Net.Sockets.Socket client)
	{
		using var _ = client;
		using var stream = new NetworkStream(client, ownsSocket: false);
		using var reader = new StreamReader(stream, Encoding.UTF8);
		using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

		await writer.WriteLineAsync(new JsonObject
		{
			["hello"] = "sharpconsoleui",
			["protocol"] = 1,
			["pid"] = Environment.ProcessId,
		}.ToJsonString());

		while (!_cts.IsCancellationRequested)
		{
			string? line;
			try { line = await reader.ReadLineAsync(_cts.Token); }
			catch { return; }

			if (line == null) return;
			if (string.IsNullOrWhiteSpace(line)) continue;

			string response;
			try
			{
				response = Dispatch(line).ToJsonString();
			}
			catch (Exception ex)
			{
				// A probe must never be able to crash the app it is probing.
				response = Error($"{ex.GetType().Name}: {ex.Message}").ToJsonString();
			}

			try { await writer.WriteLineAsync(response); }
			catch { return; }
		}
	}

	#region Dispatch

	private JsonObject Dispatch(string line)
	{
		JsonObject req;
		try
		{
			req = JsonNode.Parse(line)?.AsObject()
				?? throw new FormatException("request must be a JSON object");
		}
		catch (JsonException ex)
		{
			return Error($"malformed JSON: {ex.Message}");
		}

		string cmd = req["cmd"]?.GetValue<string>() ?? "";

		return cmd switch
		{
			// --- Tier 1: what the screen shows ---------------------------------------
			"snapshot" => Ok(Snapshot(req)),

			// --- Tier 2: what the engine believes ------------------------------------
			"tree" => Ok(Tree(req)),
			"focus" => Ok(DiagnosticsSerializer.Focus(_system)),
			"windows" => Ok(new JsonObject { ["windows"] = DiagnosticsSerializer.Windows(_system) }),
			"metrics" => Ok(DiagnosticsSerializer.Metrics(_system)),

			// --- Tier 5: the log ------------------------------------------------------
			"logs" => Ok(Logs(req)),

			// --- Tier 3: watching over time ------------------------------------------
			"settle" => Ok(Settle(req)),

			// --- Tier 4: interactivity -----------------------------------------------
			"key" => Ok(Key(req)),
			"mouse" => Ok(Mouse(req)),
			"size" => Ok(Size(req)),
			"focusctl" => Ok(FocusControl(req)),
			"invoke" => Ok(Invoke(req)),
			"theme" => Ok(Theme(req)),
			"window" => Ok(WindowCmd(req)),

			"png" => Ok(Png(req)),
			"set" => Ok(Set(req)),
			"ping" => Ok(new JsonObject { ["pong"] = true }),
			"commands" => Ok(Commands()),

			"" => Error("missing 'cmd'", Commands()),
			_ => Error($"unknown command '{cmd}'", Commands()),
		};
	}

	/// <summary>The command names, for an unknown-command reply.</summary>
	/// <remarks>
	/// Built through the non-generic <c>ICollection&lt;JsonNode&gt;.Add</c> rather than a
	/// collection initializer. The initializer binds to <c>JsonArray.Add&lt;T&gt;</c>, which
	/// creates values reflectively and is therefore neither trim- nor AOT-safe; going through
	/// <see cref="JsonValue.Create(string?, JsonNodeOptions?)"/> keeps this whole file clean
	/// for NativeAOT. Same reason for the <c>(JsonNode)</c> casts elsewhere - they are not
	/// noise, they pick the safe overload.
	/// </remarks>
	private static JsonObject Commands()
	{
		var known = new JsonArray();
		foreach (string name in KnownCommands)
			known.Add((JsonNode?)JsonValue.Create(name));

		return new JsonObject { ["known"] = known };
	}

	private static readonly string[] KnownCommands =
	{
		"snapshot", "png", "tree", "focus", "windows", "metrics", "set",
		"settle", "logs", "key", "mouse", "size", "focusctl", "invoke",
		"theme", "window", "ping", "commands",
	};

	#endregion

	#region Tier 1-2 handlers

	private JsonObject Snapshot(JsonObject req)
	{
		bool ansi = req["ansi"]?.GetValue<bool>() ?? false;

		// A region, because the interesting part of a 200x60 terminal is usually six rows of
		// it. Omitted fields mean "to the edge", so {"y":20,"h":3} is a status-bar read.
		var region = new DiagnosticsSerializer.Region(
			req["x"]?.GetValue<int>(),
			req["y"]?.GetValue<int>(),
			req["w"]?.GetValue<int>(),
			req["h"]?.GetValue<int>());

		if (req["composed"]?.GetValue<bool>() == true)
			return DiagnosticsSerializer.ConsoleSnapshot(_system, ansi, region);

		var window = ResolveWindow(req, out string? err);
		if (window == null) return ErrorBody(err!);

		return DiagnosticsSerializer.Snapshot(window, ansi, region);
	}

	/// <summary>
	/// The screen as an image: one solid block of colour per cell, no glyphs.
	/// </summary>
	/// <remarks>
	/// Answers the question text cannot - whether the layout LOOKS right. Colour, borders,
	/// focus and window stacking survive; characters do not (see <see cref="DiagnosticsPng"/>
	/// for why drawing them would be a lie). Returns the bytes inline unless a path is given.
	/// </remarks>
	private JsonObject Png(JsonObject req)
	{
		int ch = req["cellHeight"]?.GetValue<int>() ?? 16;
		int cw = req["cellWidth"]?.GetValue<int>() ?? 0;   // 0 = take the font's advance
		string? path = req["path"]?.GetValue<string>();
		bool wantGlyphs = req["glyphs"]?.GetValue<bool>() ?? true;
		string? fontPath = req["font"]?.GetValue<string>();

		// Load the font FIRST: a monospace advance decides the cell width, so the image
		// geometry depends on it. Without glyphs, fall back to a plain 8-wide cell.
		DiagnosticsFont? font = null;
		string? fontError = null;
		if (wantGlyphs)
		{
			font = DiagnosticsFont.Load(fontPath, ch, cw, out fontError);
			if (font == null && fontPath != null)
				return ErrorBody(fontError!);   // an explicit font that cannot load is an error
		}

		if (font != null) cw = font.CellWidth;
		if (cw <= 0) cw = 8;

		var region = new DiagnosticsSerializer.Region(
			req["x"]?.GetValue<int>(),
			req["y"]?.GetValue<int>(),
			req["w"]?.GetValue<int>(),
			req["h"]?.GetValue<int>());

		bool composed = req["composed"]?.GetValue<bool>() == true;

		Func<int, int, DiagnosticsPng.CellColor> at;
		DiagnosticsSerializer.Region clipped;
		string? regionError;
		int defaulted = 0;

		if (composed)
		{
			var diag = _system.RenderingDiagnostics;

			// Composed mode needs the renderer's console-buffer snapshot. When the app was
			// built with EnableDiagnostics: false the diagnostics object is never created
			// at all, so there is nothing to switch on from here - capturing a window is
			// the answer, and it needs no diagnostics. Recording is also a per-frame deep
			// copy, which a probe should not impose on a running app behind its back.
			if (diag != null && !diag.IsEnabled &&
				diag.EnabledLayers.HasFlag(Configuration.DiagnosticsLayers.ConsoleBuffer))
			{
				// Note the frame we are on BEFORE enabling. Recording may have been paused
				// with an old snapshot still in hand, and returning that would be a
				// screenshot of the past presented as the present - the one failure a
				// capture tool must not have. So wait for a frame captured after this
				// point, not merely for a snapshot to exist.
				int before = diag.CurrentFrameNumber;
				diag.IsEnabled = true;

				bool captured = false;
				for (int i = 0; i < 40; i++)
				{
					if (diag.LastConsoleSnapshot != null && diag.CurrentFrameNumber > before)
					{
						captured = true;
						break;
					}
					Thread.Sleep(25);
				}

				// Falling through to whatever snapshot happens to be in hand would hand
				// back a picture of the past as if it were the present. An image that is
				// silently out of date is worse than no image, so this fails instead.
				if (!captured)
				{
					return ErrorBody(
						"diagnostics recording was enabled, but no new frame was captured " +
						"within 1s - the app may be idle or blocked. Retry, or capture a " +
						"window instead, which reads the live buffer directly.");
				}
			}

			var shot = diag?.LastConsoleSnapshot;
			if (shot == null)
			{
				// Composed mode reads the renderer's console-buffer snapshot, which only
				// exists when the app opted into that layer at construction. It is not
				// switchable from here by design, so say what to change rather than
				// leaving the caller to guess.
				return ErrorBody(
					"no composed buffer: this app runs with EnableDiagnostics off, or " +
					"without the ConsoleBuffer layer, so the renderer records nothing. " +
					"Capture a window instead - {\"cmd\":\"png\",\"window\":\"<title>\"} - " +
					"which needs no diagnostics. To enable composed capture, start the app " +
					"with ConsoleWindowSystemOptions.EnableDiagnostics: true.");
			}

			clipped = region.ClampTo(shot.Width, shot.Height, out regionError);
			if (regionError != null) return ErrorBody(regionError);

			// The composed buffer stores the EMITTED escape, which may say "terminal
			// default" rather than a colour. Fall back to the theme and count it.
			var fallback = _system.Theme.DesktopBackgroundColor ?? _system.Theme.WindowBackgroundColor;
			int ox = clipped.X ?? 0, oy = clipped.Y ?? 0;
			var themeFg = _system.Theme.WindowForegroundColor;
			at = (cx, cy) =>
			{
				var raw = shot.GetBack(ox + cx, oy + cy);
				var c = DiagnosticsPng.ParseBackground(raw.AnsiEscape, fallback);
				if (c.DefaultedBackground) defaulted++;
				return c with
				{
					Glyph = raw.Character,
					Foreground = DiagnosticsPng.ParseForeground(raw.AnsiEscape, themeFg),
				};
			};
		}
		else
		{
			var window = ResolveWindow(req, out string? err);
			if (window == null) return ErrorBody(err!);

			var buffer = window.Renderer?.Buffer;
			if (buffer == null) return ErrorBody("window has not painted yet");

			clipped = region.ClampTo(buffer.Width, buffer.Height, out regionError);
			if (regionError != null) return ErrorBody(regionError);

			int ox = clipped.X ?? 0, oy = clipped.Y ?? 0;
			at = (cx, cy) =>
			{
				var c = buffer.GetCell(ox + cx, oy + cy);
				// A wide glyph is drawn from its base cell and overhangs; the continuation
				// cell must not draw it again.
				return new DiagnosticsPng.CellColor(
					c.Background, false,
					c.IsWideContinuation ? null : c.Character,
					c.Foreground);
			};
		}

		int rw = clipped.W ?? 0, rh = clipped.H ?? 0;
		var bytes = DiagnosticsPng.Render(rw, rh, cw, ch, at, font, out string? renderError);
		if (bytes == null) return ErrorBody(renderError!);

		var body = DiagnosticsPng.Reply(bytes, rw * cw, rh * ch, path);
		body["x"] = clipped.X ?? 0;
		body["y"] = clipped.Y ?? 0;
		body["cols"] = rw;
		body["rows"] = rh;
		body["glyphs"] = font != null;
		if (font != null) body["font"] = font.Path;
		else if (fontError != null) body["fontNote"] = fontError;
		body["cell"] = $"{cw}x{ch}";
		body["composed"] = composed;

		font?.Dispose();

		// Say so rather than letting a substituted colour pass for a measured one.
		if (defaulted > 0)
		{
			body["defaultedCells"] = defaulted;
			body["note"] = "cells using the terminal's default background were filled from the theme";
		}

		return body;
	}

	/// <summary>
	/// Reads, and optionally changes, the runtime switches worth flipping mid-session.
	/// </summary>
	/// <remarks>
	/// <para>
	/// One verb rather than one per switch: they are all "read the value, maybe write it",
	/// and a caller that wants to know the current state should not have to know which of a
	/// dozen verbs to ask. Sent with no fields it is a pure read, so <c>{"cmd":"set"}</c>
	/// answers "what is this app doing right now".
	/// </para>
	/// <para>
	/// Deliberately NOT exposed: <c>BlockWhenIdle</c>, which is host-integration wiring - a
	/// remote flip to false would spin the render loop on whatever thread the host owns.
	/// A probe may change what is OBSERVED, never how the app is driven.
	/// </para>
	/// </remarks>
	private JsonObject Set(JsonObject req)
	{
		var changed = new JsonArray();

		// Render diagnostics: the per-frame deep copy that composed snapshots and metrics
		// need. Costly, so it stays opt-in - but once the object exists it can be paused
		// and resumed, which is exactly what a debugging session wants.
		var diag = _system.RenderingDiagnostics;
		if (req["diagnostics"]?.GetValue<bool>() is { } wantDiag)
		{
			if (diag == null)
			{
				return ErrorBody(
					"this app was built with EnableDiagnostics: false, so no diagnostics " +
					"recorder exists to switch on. It is fixed at construction. Per-window " +
					"snapshot and png need no diagnostics and work regardless.");
			}

			if (diag.IsEnabled != wantDiag)
			{
				diag.IsEnabled = wantDiag;
				changed.Add((JsonNode?)JsonValue.Create("diagnostics"));
			}
		}

		// Log capture, and its level floor. Raising the floor mid-run is the cheapest way
		// to find a noisy category without restarting.
		if (req["logging"]?.GetValue<bool>() is { } wantLog &&
			_system.LogService is Logging.LogService ls1 && ls1.IsEnabled != wantLog)
		{
			ls1.IsEnabled = wantLog;
			changed.Add((JsonNode?)JsonValue.Create("logging"));
		}

		if (req["logLevel"]?.GetValue<string>() is { } levelName)
		{
			if (!Enum.TryParse<Logging.LogLevel>(levelName, true, out var level))
			{
				return ErrorBody(
					$"unknown log level '{levelName}'; known: " +
					string.Join(", ", Enum.GetNames<Logging.LogLevel>()));
			}

			if (_system.LogService is Logging.LogService ls2 && ls2.MinimumLevel != level)
			{
				ls2.MinimumLevel = level;
				changed.Add((JsonNode?)JsonValue.Create("logLevel"));
			}
		}

		if (req["clearLogs"]?.GetValue<bool>() == true)
		{
			_system.LogService.ClearLogs();
			changed.Add((JsonNode?)JsonValue.Create("clearLogs"));
		}

		// Report the resulting state, always - a write that reads back is self-verifying,
		// and a bare {"cmd":"set"} is then a state dump.
		var body = new JsonObject
		{
			["diagnostics"] = diag?.IsEnabled,
			["diagnosticsAvailable"] = diag != null,
			["layers"] = diag?.EnabledLayers.ToString(),
			["logging"] = _system.LogService is Logging.LogService l3 ? l3.IsEnabled : null,
			["logLevel"] = _system.LogService is Logging.LogService l4 ? l4.MinimumLevel.ToString() : null,
			["logCount"] = _system.LogService is Logging.LogService l5 ? l5.Count : null,
			["theme"] = _system.Theme.GetType().Name,
			["changed"] = changed,
		};

		return body;
	}

	private JsonObject Tree(JsonObject req)
	{
		var window = ResolveWindow(req, out string? err);
		if (window == null) return ErrorBody(err!);

		int depth = req["depth"]?.GetValue<int>() ?? -1;
		bool props = req["props"]?.GetValue<bool>() ?? false;
		return DiagnosticsSerializer.Tree(window, depth, props);
	}

	/// <summary>
	/// The log buffer, which a TUI can never print to its own screen.
	/// </summary>
	private JsonObject Logs(JsonObject req) => DiagnosticsSerializer.Logs(
		_system,
		req["count"]?.GetValue<int>() ?? 50,
		req["level"]?.GetValue<string>(),
		req["category"]?.GetValue<string>(),
		req["contains"]?.GetValue<string>());

	#endregion

	#region Tier 3: settle

	/// <summary>
	/// Waits until a render changes nothing, or the timeout expires.
	/// </summary>
	/// <remarks>
	/// Exists because of a real bug class: a deferred scroll applies on the frame AFTER the
	/// one that triggered it, so a single snapshot catches a transient state and looks right
	/// or wrong depending on timing. Asking "when did it stop changing?" beats guessing a
	/// sleep.
	/// </remarks>
	private JsonObject Settle(JsonObject req)
	{
		int timeoutMs = req["timeoutMs"]?.GetValue<int>() ?? 2000;
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		int frames = 0;

		while (DateTime.UtcNow < deadline)
		{
			var before = _system.RenderingDiagnostics?.CurrentFrameNumber ?? 0;
			Thread.Sleep(32);
			var after = _system.RenderingDiagnostics?.CurrentFrameNumber ?? 0;
			frames += Math.Max(0, after - before);

			var m = _system.RenderingDiagnostics?.LastMetrics;
			if (m != null && m.IsStaticFrame)
				return new JsonObject { ["settled"] = true, ["framesWaited"] = frames };

			// No diagnostics layers enabled: fall back to "the frame counter stopped moving".
			if (m == null && after == before)
				return new JsonObject { ["settled"] = true, ["framesWaited"] = frames, ["basis"] = "frameCounter" };
		}

		return new JsonObject { ["settled"] = false, ["framesWaited"] = frames, ["timedOut"] = true };
	}

	#endregion

	#region Tier 4: interactivity

	/// <summary>
	/// Injects a keystroke through the same path real input takes.
	/// </summary>
	/// <remarks>
	/// Goes through <c>InputStateService.EnqueueKey</c> - the entry point the test suite also
	/// uses - so injected input traverses exactly the code real input does. A second path
	/// would be a second set of bugs, and this codebase has already paid for parallel
	/// implementations twice (the dual layout engine, the three scrollbars).
	/// </remarks>
	private JsonObject Key(JsonObject req)
	{
		string spec = req["spec"]?.GetValue<string>() ?? "";
		if (spec.Length == 0) return ErrorBody("key requires 'spec', e.g. \"Down\", \"C-c\", \"abc\"");

		var keys = KeySpec.Parse(spec, out string? parseError);
		if (keys == null) return ErrorBody(parseError!);

		foreach (var k in keys)
			_system.InputStateService.EnqueueKey(k);

		return WithSettle(req, new JsonObject { ["sent"] = keys.Count });
	}

	/// <summary>
	/// Reports the terminal size. Resizing is NOT offered: the driver's ScreenSize is
	/// read-only and owned by the real terminal, so a synthetic resize would set a value the
	/// next real resize event silently contradicts. Use the terminal, or tmux, to resize.
	/// </summary>
	/// <summary>
	/// Injects a mouse event at screen coordinates, through the ordinary dispatch path.
	/// </summary>
	/// <remarks>
	/// Routes into <c>InputCoordinator.InjectMouseEvent</c>, which is the exact method the
	/// driver's own MouseEvent lands on. Hit-testing, window z-order and capture all run as
	/// they would for a real click - a probe that reached past dispatch could report hits a
	/// user could never produce.
	/// </remarks>
	private JsonObject Mouse(JsonObject req)
	{
		int? x = req["x"]?.GetValue<int>();
		int? y = req["y"]?.GetValue<int>();
		if (x == null || y == null) return ErrorBody("mouse requires 'x' and 'y'");

		string button = req["button"]?.GetValue<string>() ?? "left";
		string action = req["action"]?.GetValue<string>() ?? "click";

		var flags = MouseFlagsFor(button, action, out string? flagError);
		if (flags == null) return ErrorBody(flagError!);

		Marshal(() => _system.Input.InjectMouseEvent(flags, new System.Drawing.Point(x.Value, y.Value)));
		return WithSettle(req, new JsonObject
		{
			["mouse"] = $"{action} {button} @ {x},{y}",
		});
	}

	private static List<MouseFlags>? MouseFlagsFor(string button, string action, out string? error)
	{
		error = null;

		// Wheel first: it has no button, and asking for "wheel" with a button would be a
		// contradiction worth reporting rather than quietly resolving.
		switch (action.ToLowerInvariant())
		{
			case "wheelup": return new List<MouseFlags> { MouseFlags.WheeledUp };
			case "wheeldown": return new List<MouseFlags> { MouseFlags.WheeledDown };
		}

		MouseFlags pressed, released, clicked;
		switch (button.ToLowerInvariant())
		{
			case "left":
				(pressed, released, clicked) =
					(MouseFlags.Button1Pressed, MouseFlags.Button1Released, MouseFlags.Button1Clicked);
				break;
			case "right":
				(pressed, released, clicked) =
					(MouseFlags.Button3Pressed, MouseFlags.Button3Released, MouseFlags.Button3Clicked);
				break;
			case "middle":
				(pressed, released, clicked) =
					(MouseFlags.Button2Pressed, MouseFlags.Button2Released, MouseFlags.Button2Clicked);
				break;
			default:
				error = $"unknown button '{button}'; known: left, right, middle";
				return null;
		}

		return action.ToLowerInvariant() switch
		{
			"click" => new List<MouseFlags> { clicked },
			"press" => new List<MouseFlags> { pressed },
			"release" => new List<MouseFlags> { released },
			"move" => new List<MouseFlags> { MouseFlags.ReportMousePosition },
			_ => Unknown(action, out error),
		};

		static List<MouseFlags>? Unknown(string action, out string error)
		{
			error = $"unknown action '{action}'; known: click, press, release, move, wheelup, wheeldown";
			return null;
		}
	}

	private JsonObject Size(JsonObject req)
	{
		var size = _system.ConsoleDriver.ScreenSize;
		return new JsonObject { ["w"] = size.Width, ["h"] = size.Height };
	}

	/// <summary>
	/// Focuses a control by name - stating the intent, instead of pressing Tab and hoping.
	/// </summary>
	private JsonObject FocusControl(JsonObject req)
	{
		string name = req["name"]?.GetValue<string>() ?? "";
		if (name.Length == 0) return ErrorBody("focusctl requires 'name'");

		var window = ResolveWindow(req, out string? err);
		if (window == null) return ErrorBody(err!);

		var target = FindByName(window, name);
		if (target == null) return ErrorBody($"no control named '{name}' in '{window.Title}'");
		if (target is not IFocusableControl focusable)
			return ErrorBody($"'{name}' is a {target.GetType().Name} and is not focusable");

		Marshal(() => window.FocusManager.SetFocus(focusable, FocusReason.Programmatic));
		return WithSettle(req, new JsonObject { ["focused"] = name });
	}

	/// <summary>
	/// Activates a control by name, through the path a real user takes.
	/// </summary>
	/// <remarks>
	/// <c>Click</c> is an EVENT, not a method, so there is no "just raise it" shortcut - and
	/// raising it directly would skip the focus and enabled checks a real click goes through,
	/// making the probe lie about what a user could actually do. So this focuses the control
	/// and sends Enter down the ordinary input pipeline instead.
	/// </remarks>
	private JsonObject Invoke(JsonObject req)
	{
		string name = req["name"]?.GetValue<string>() ?? "";
		if (name.Length == 0) return ErrorBody("invoke requires 'name'");

		var window = ResolveWindow(req, out string? err);
		if (window == null) return ErrorBody(err!);

		var target = FindByName(window, name);
		if (target == null) return ErrorBody($"no control named '{name}' in '{window.Title}'");
		if (target is not IFocusableControl focusable)
			return ErrorBody($"'{name}' is a {target.GetType().Name} and cannot be activated");

		Marshal(() => window.FocusManager.SetFocus(focusable, FocusReason.Programmatic));
		_system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));

		return WithSettle(req, new JsonObject { ["invoked"] = name });
	}

	private JsonObject Theme(JsonObject req)
	{
		string name = req["name"]?.GetValue<string>() ?? "";
		if (name.Length == 0) return ErrorBody("theme requires 'name'");

		bool ok = false;
		Marshal(() => ok = _system.ThemeStateService.SwitchTheme(name));
		return ok
			? WithSettle(req, new JsonObject { ["theme"] = name })
			: ErrorBody($"no theme named '{name}'");
	}

	private JsonObject WindowCmd(JsonObject req)
	{
		var window = ResolveWindow(req, out string? err);
		if (window == null) return ErrorBody(err!);

		if (req["activate"]?.GetValue<bool>() == true)
		{
			Marshal(() => _system.WindowStateService.SetActiveWindow(window));
			return WithSettle(req, new JsonObject { ["activated"] = window.Title });
		}

		if (req["close"]?.GetValue<bool>() == true)
		{
			Marshal(() => window.Close());
			return WithSettle(req, new JsonObject { ["closed"] = window.Title });
		}

		return ErrorBody("window requires 'activate' or 'close'");
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Runs an action on the UI thread and waits for it, per CLAUDE.md rule 13: the socket
	/// handler is NOT the UI thread, and the render loop reads this state concurrently.
	/// </summary>
	private void Marshal(Action action)
	{
		using var done = new ManualResetEventSlim(false);
		Exception? failure = null;

		_system.EnqueueOnUIThread(() =>
		{
			try { action(); }
			catch (Exception ex) { failure = ex; }
			finally { done.Set(); }
		}, "diagnostics-socket");

		if (!done.Wait(TimeSpan.FromSeconds(5)))
			throw new TimeoutException("UI thread did not run the action within 5s");

		if (failure != null) throw failure;
	}

	/// <summary>
	/// Folds "do it, then wait until the screen stops changing" into one call - the most
	/// common UI-test pattern, and the one most often got wrong with sleeps.
	/// </summary>
	private JsonObject WithSettle(JsonObject req, JsonObject body)
	{
		if (req["settle"]?.GetValue<bool>() != true) return body;

		var settled = Settle(req);
		body["settled"] = settled["settled"]?.GetValue<bool>() ?? false;
		body["framesWaited"] = settled["framesWaited"]?.GetValue<int>() ?? 0;
		return body;
	}

	private Window? ResolveWindow(JsonObject req, out string? error)
	{
		error = null;
		string? id = req["window"]?.GetValue<string>();

		if (string.IsNullOrEmpty(id))
		{
			var active = _system.WindowStateService.ActiveWindow;
			if (active == null) error = "no active window; pass 'window'";
			return active;
		}

		foreach (var w in _system.WindowStateService.GetWindowsByZOrder())
		{
			if (w.Guid == id || string.Equals(w.Title, id, StringComparison.OrdinalIgnoreCase))
				return w;
		}

		var known = new JsonArray();
		foreach (var w in _system.WindowStateService.GetWindowsByZOrder())
			known.Add((JsonNode?)JsonValue.Create(w.Title));
		error = $"unknown window '{id}'; known: {known.ToJsonString()}";
		return null;
	}

	private static IWindowControl? FindByName(Window window, string name)
	{
		foreach (var c in window.GetControls())
		{
			var hit = Search(c, name);
			if (hit != null) return hit;
		}
		return null;
	}

	private static IWindowControl? Search(IWindowControl control, string name)
	{
		if (string.Equals(control.Name, name, StringComparison.Ordinal)) return control;

		if (control is IControlHost host)
		{
			foreach (var child in host.Children)
			{
				var hit = Search(child, name);
				if (hit != null) return hit;
			}
		}
		return null;
	}

	/// <summary>Stamps a handler's reply as success, or as the failure it already is.</summary>
	/// <remarks>
	/// A body carrying "error" is a FAILURE and must say so with <c>ok: false</c>. Returning
	/// it bare would leave no <c>ok</c> field at all, and a client testing
	/// <c>ok !== false</c> would read a misconfigured app's empty answer as a real one -
	/// "no metrics" would look like "zero metrics". A verb that cannot do its job must be
	/// distinguishable from a verb that did it and found nothing.
	/// </remarks>
	private static JsonObject Ok(JsonObject body)
	{
		if (body.ContainsKey("error"))
		{
			if (!body.ContainsKey("ok")) body["ok"] = false;
			return body;
		}

		var o = new JsonObject { ["ok"] = true };
		foreach (var kv in body.ToList())
		{
			body.Remove(kv.Key);
			o[kv.Key] = kv.Value;
		}
		return o;
	}

	private static JsonObject ErrorBody(string message) => new() { ["ok"] = false, ["error"] = message };

	private static JsonObject Error(string message, JsonObject? extra = null)
	{
		var o = new JsonObject { ["ok"] = false, ["error"] = message };
		if (extra != null)
		{
			foreach (var kv in extra.ToList())
			{
				extra.Remove(kv.Key);
				o[kv.Key] = kv.Value;
			}
		}
		return o;
	}

	#endregion

	public void Dispose()
	{
		_cts.Cancel();
		try { _listener?.Close(); } catch { }
		try { if (File.Exists(_path)) File.Delete(_path); } catch { }
		_cts.Dispose();
	}
}
