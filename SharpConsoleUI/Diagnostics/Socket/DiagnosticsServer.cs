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
using SharpConsoleUI.Surfaces;

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
			// No id to echo: the request did not parse, so there is nothing to read one
			// from. A client's correlation is broken precisely when it most wants an
			// answer, which is a real gap and is documented rather than worked around -
			// recovering an id by pre-scanning unparseable text would be guesswork.
			return Error($"malformed JSON: {ex.Message}");
		}

		return Echo(req, DispatchVerb(req));
	}

	/// <summary>
	/// Copies a request's <c>id</c> onto its reply, so a client can match the two.
	/// </summary>
	/// <param name="req">The request.</param>
	/// <param name="reply">The reply to stamp.</param>
	/// <returns>The reply.</returns>
	/// <remarks>
	/// <para>
	/// Optional, and absent unless asked for: a request without an <c>id</c> produces a
	/// byte-identical reply to before, so a shell one-liner is unaffected.
	/// </para>
	/// <para>
	/// A string or a number only, and capped - the value is reflected straight back, so an
	/// unbounded one would let a client inflate a reply at the server's expense, and an
	/// object would let it choose the reply's shape. Anything else is ignored rather than
	/// rejected: an id is a client-side convenience, and failing a working request over its
	/// label would be a poor trade.
	/// </para>
	/// </remarks>
	private static JsonObject Echo(JsonObject req, JsonObject reply)
	{
		if (!req.TryGetPropertyValue("id", out var id) || id is not JsonValue value)
			return reply;

		object? raw = value.GetValue<object>();
		if (raw is JsonElement e)
		{
			switch (e.ValueKind)
			{
				case JsonValueKind.String when e.GetString() is { Length: <= MaxIdLength } str:
					reply["id"] = (JsonNode)JsonValue.Create(str)!;
					break;
				case JsonValueKind.Number when e.TryGetInt64(out long n):
					reply["id"] = (JsonNode)JsonValue.Create(n)!;
					break;
			}
		}

		return reply;
	}

	/// <summary>Longest echoed id. Long enough for a GUID, short enough not to be a lever.</summary>
	private const int MaxIdLength = 64;

	private JsonObject DispatchVerb(JsonObject req)
	{
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
			"frame" => Ok(new JsonObject { ["frame"] = _system.ConsoleDriver.FrameNumber }),
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
		"logs", "key", "mouse", "size", "focusctl", "invoke",
		"theme", "window", "frame", "ping", "commands",
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
	/// <summary>
	/// Readies the composed desktop for capture, turning recording on if it is merely paused.
	/// </summary>
	/// <param name="error">Why no composed surface could be produced.</param>
	/// <param name="code"></param>
	/// <returns>The surface, or null.</returns>
	/// <remarks>
	/// Recording is a per-frame deep copy, so it is off by default and a probe should not
	/// impose it behind the app's back - but when the layer exists and is simply paused,
	/// turning it on and waiting for a FRESH frame is right. Serving the snapshot already in
	/// hand would be a picture of the past presented as the present.
	/// </remarks>
	private ICellSurface? PrepareComposedSurface(out string? error, out string? code)
	{
		error = null;
		code = null;

		// The driver's live front buffer first. It is the composed desktop, it is always
		// current, and reading it needs no diagnostics - which makes composed capture work
		// in the common configuration rather than only when the app opted in.
		var live = _system.ConsoleDriver.CaptureComposed();
		if (live != null) return live.AsSurface(_system.Theme);

		var diag = _system.RenderingDiagnostics;

		if (diag != null && !diag.IsEnabled &&
			diag.EnabledLayers.HasFlag(Configuration.DiagnosticsLayers.ConsoleBuffer))
		{
			// Note the frame BEFORE enabling, and wait for one captured after this point -
			// not merely for a snapshot to exist.
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

			if (!captured)
			{
				error = "diagnostics recording was enabled, but no new frame was captured "
					  + "within 1s - the app may be idle or blocked. Retry, or capture a "
					  + "window instead, which reads the live buffer directly.";
				return null;
			}
		}

		var shot = diag?.LastConsoleSnapshot;
		if (shot == null)
		{
			code = ErrorCode.WrongDriver;
			error = "no composed buffer: this driver retains nothing to read, which means it "
				  + "is rendering in RenderMode.Direct. Capture a window instead - "
				  + "{\"cmd\":\"png\",\"window\":\"<title>\"} - which needs no composed "
				  + "buffer, or run the app with RenderMode.Buffer.";
			return null;
		}

		return shot.AsSurface(_system.Theme);
	}

	private JsonObject Png(JsonObject req)
	{
		// Cheapest possible answer first. A client streaming the screen asks many times a
		// second and the screen is usually unchanged, so returning "still frame N" before
		// loading a font or touching a buffer is the difference between a few bytes and
		// 130KB of identical PNG.
		long currentFrame = _system.ConsoleDriver.FrameNumber;
		long? since = req["sinceFrame"]?.GetValue<long>();
		if (since != null && currentFrame != 0 && currentFrame <= since.Value)
			return new JsonObject { ["unchanged"] = true, ["frame"] = currentFrame };

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
			// "fontSource": "automatic" (default) | "embedded" | "strict" | "file"
			var fontSource = req["fontSource"]?.GetValue<string>()?.ToLowerInvariant() switch
			{
				"embedded" => Surfaces.FontSource.Embedded,
				"strict" => Surfaces.FontSource.Strict,
				"file" => Surfaces.FontSource.File,
				_ => Surfaces.FontSource.Automatic,
			};

			font = DiagnosticsFont.Load(fontPath, ch, cw, out fontError, fontSource);
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

		// One surface, whatever the source. Both branches below end at an ICellSurface,
		// so cropping and rendering are written once instead of once per source.
		Surfaces.ICellSurface source;

		if (composed)
		{
			var prepared = PrepareComposedSurface(out string? composeError, out string? composeCode);
			if (prepared == null) return ErrorBody(composeError!, composeCode);
			source = prepared;
		}
		else
		{
			var window = ResolveWindow(req, out string? err);
			if (window == null) return ErrorBody(err!);
			if (window.Buffer == null) return ErrorBody("window has not painted yet");

			source = window.CaptureSurface();
		}

		var clipped = region.ClampTo(source.Width, source.Height, out string? regionError);
		if (regionError != null) return ErrorBody(regionError);

		int rx = clipped.X ?? 0, ry = clipped.Y ?? 0;
		int rw = clipped.W ?? 0, rh = clipped.H ?? 0;

		var cropped = rw == source.Width && rh == source.Height && rx == 0 && ry == 0
			? source
			: source.Crop(new Layout.LayoutRect(rx, ry, rw, rh));

		// Count substituted backgrounds so the reply can say the image contains them.
		int defaulted = 0;
		for (int y = 0; y < cropped.Height; y++)
			for (int x = 0; x < cropped.Width; x++)
				if (cropped[x, y].BackgroundWasDefaulted) defaulted++;

		var bytes = DiagnosticsPng.Render(cropped, cw, ch, font, out string? renderError);
		if (bytes == null) return ErrorBody(renderError!);

		var body = DiagnosticsPng.Reply(bytes, rw * cw, rh * ch, path);
		body["x"] = clipped.X ?? 0;
		body["y"] = clipped.Y ?? 0;
		body["cols"] = rw;
		body["rows"] = rh;
		body["glyphs"] = font != null;
		if (font != null)
		{
			body["font"] = font.Path;
			if (font.FallbacksUsed.Count > 0)
			{
				var used = new JsonArray();
				foreach (string f in font.FallbacksUsed)
					used.Add((JsonNode?)JsonValue.Create(f));
				body["fallbackFonts"] = used;
			}

			// Say which characters no available face could draw, rather than letting a
			// row of boxes pass for a rendering.
			if (font.MissingCodepoints.Count > 0)
			{
				var missing = new JsonArray();
				foreach (int cp in font.MissingCodepoints)
					missing.Add((JsonNode?)JsonValue.Create($"U+{cp:X4}"));
				body["missingGlyphs"] = missing;
			}
		}
		else if (fontError != null) body["fontNote"] = fontError;
		body["cell"] = $"{cw}x{ch}";
		body["composed"] = composed;
		body["frame"] = currentFrame;

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

		return new JsonObject { ["sent"] = keys.Count };
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
		return new JsonObject
		{
			["mouse"] = $"{action} {button} @ {x},{y}",
		};
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

	/// <summary>
	/// Reports the screen size, and resizes it when the driver allows.
	/// </summary>
	/// <remarks>
	/// Only a headless driver can be resized: a real terminal's size belongs to the terminal,
	/// and the library cannot and should not change it. That makes this the verb that turns a
	/// headless app into a responsive-layout test rig - narrow and short sizes are exactly
	/// where layout bugs hide, and reaching them otherwise means restarting the app.
	/// Asking a real driver to resize is an ERROR, not a silent no-op, so a test cannot
	/// quietly assert against a size it never got.
	/// </remarks>
	private JsonObject Size(JsonObject req)
	{
		int? w = req["w"]?.GetValue<int>();
		int? h = req["h"]?.GetValue<int>();

		if (w != null || h != null)
		{
			var current = _system.ConsoleDriver.ScreenSize;
			int newW = w ?? current.Width;
			int newH = h ?? current.Height;

			if (newW < 1 || newH < 1)
				return ErrorBody($"size must be at least 1x1; got {newW}x{newH}");

			if (_system.ConsoleDriver is not Drivers.HeadlessConsoleDriver headless)
			{
				return ErrorBody(
					$"this app runs on {_system.ConsoleDriver.GetType().Name}, whose size is "
					+ "owned by the terminal and cannot be set from here. Resize the terminal, "
					+ "or run the app on HeadlessConsoleDriver to drive size from tests.",
					ErrorCode.WrongDriver);
			}

			Marshal(() => headless.SimulateScreenResize(newW, newH));
		}

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
		return new JsonObject { ["focused"] = name };
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

		return new JsonObject { ["invoked"] = name };
	}

	private JsonObject Theme(JsonObject req)
	{
		string name = req["name"]?.GetValue<string>() ?? "";
		if (name.Length == 0) return ErrorBody("theme requires 'name'");

		bool ok = false;
		Marshal(() => ok = _system.ThemeStateService.SwitchTheme(name));
		return ok
			? new JsonObject { ["theme"] = name }
			: ErrorBody($"no theme named '{name}'");
	}

	private JsonObject WindowCmd(JsonObject req)
	{
		var window = ResolveWindow(req, out string? err);
		if (window == null) return ErrorBody(err!);

		if (req["activate"]?.GetValue<bool>() == true)
		{
			Marshal(() => _system.WindowStateService.SetActiveWindow(window));
			return new JsonObject { ["activated"] = window.Title };
		}

		if (req["close"]?.GetValue<bool>() == true)
		{
			Marshal(() => window.Close());
			return new JsonObject { ["closed"] = window.Title };
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

	/// <summary>
	/// A machine-readable name for the few failures a program must act on differently.
	/// </summary>
	/// <remarks>
	/// Deliberately short. Most errors here say "your request was wrong" - a missing field,
	/// an unknown control name - and a developer reads the sentence and fixes it; a code
	/// would add a second thing to keep accurate for no gain, and a vocabulary nothing
	/// enforces drifts out of date as verbs are added.
	/// <para>These three are different: each describes a CONDITION rather than a mistake,
	/// and a client has to behave differently when it sees one. Everything else is left to
	/// the message, which is then free to get clearer without breaking anybody.</para>
	/// </remarks>
	internal static class ErrorCode
	{
		/// <summary>
		/// This driver cannot do it, and no retry will help - resizing a real terminal, or
		/// reading the composed desktop in RenderMode.Direct. A client should stop asking.
		/// </summary>
		internal const string WrongDriver = "wrong_driver";

		/// <summary>
		/// The app was built without the recording this verb reads. Not switchable from
		/// here, so a client should disable the feature rather than retry.
		/// </summary>
		internal const string DiagnosticsDisabled = "diagnostics_disabled";
	}

	private static JsonObject ErrorBody(string message, string? code = null)
	{
		var o = new JsonObject { ["ok"] = false, ["error"] = message };
		if (code != null) o["code"] = code;
		return o;
	}

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
