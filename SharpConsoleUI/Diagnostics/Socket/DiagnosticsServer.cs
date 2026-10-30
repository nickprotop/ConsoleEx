// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Surfaces;
using SharpConsoleUI.Themes;

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

		// Nothing stops a process running as this user from connecting - the file
		// mode bounds other users, and no mechanism bounds this one. What is left
		// is a record: an unexplained connection should be traceable to the moment
		// it happened rather than leaving no mark at all.
		// Warning, not Information: the default minimum level is Warning, and a
		// record nobody sees by default is not a record. Someone attaching to a
		// live application is worth one line at the level that shows.
		// Debug, not Warning. A client connects on every single call, so at
		// Warning the log fills with this and buries whatever went wrong.
		_system.LogService.LogDebug("Diagnostics client connected", "Diagnostics");
		try
		{
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
		finally
		{
			// Information: the connect line is the one worth seeing by default; a
			// matching disconnect at Warning would double the noise for no gain.
			_system.LogService.LogInfo("Diagnostics client disconnected", "Diagnostics");
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

	/// <summary>JPEG quality when the request does not say.</summary>
	private const int DefaultJpegQuality = 90;

	private JsonObject DispatchVerb(JsonObject req)
	{
		string cmd = Text(req, "cmd") ?? "";

		return cmd switch
		{
			// --- Tier 1: what the screen shows ---------------------------------------
			"snapshot" => Ok(Snapshot(req)),

			// --- Tier 2: what the engine believes ------------------------------------
			"tree" => Ok(Tree(req)),
			"focus" => Ok(DiagnosticsSerializer.Focus(_system)),
			"windows" => Ok(new JsonObject { ["windows"] = DiagnosticsSerializer.Windows(_system) }),
			"metrics" => Ok(DiagnosticsSerializer.Metrics(_system)),
			"performance" => Ok(Performance(req)),

			// --- Tier 5: the log ------------------------------------------------------
			"logs" => Ok(Logs(req)),

			// --- Tier 4: interactivity -----------------------------------------------
			"key" => Ok(Key(req)),
			"mouse" => Ok(Mouse(req)),
			"seq" => Ok(Seq(req)),
			"size" => Ok(Size(req)),
			"focusctl" => Ok(FocusControl(req)),
			"invoke" => Ok(Invoke(req)),
			"theme" => Ok(Theme(req)),
			"window" => Ok(WindowCmd(req)),

			"image" => Ok(Image(req)),
			"set" => Ok(Set(req)),
			"frame" => Ok(new JsonObject { ["frame"] = _system.ConsoleDriver.FrameNumber }),
			"settled" => Ok(Settled()),
			"clock" => Ok(Clock(req)),
			"invalidations" => Ok(Invalidations()),
			"reachable" => Ok(Reachable()),
			"desktop" => Ok(DiagnosticsSerializer.DesktopInfo(_system)),
			"control" => Ok(Control(req)),
			"inspect" => Ok(Inspect(req)),
			"measure" => Ok(Measure(req)),
			"color" => Ok(ColorCascade(req)),
			"events" => Ok(Events()),
			"ping" => Ok(new JsonObject { ["pong"] = true }),
			"commands" => Ok(Commands()),

			"" => Error("missing 'cmd'", Commands()),
			_ => Error($"unknown command '{cmd}'", Commands()),
		};
	}

	/// <summary>
	/// What asked for frame work since the last call, and cleared by it.
	/// </summary>
	/// <remarks>
	/// <c>metrics</c> says a window coalesced a hundred invalidations into one
	/// frame. This says which controls those hundred came from, which is the
	/// question actually being asked: a view repainting every frame because one
	/// setter fires in a loop looks exactly like one repainting because its data
	/// changed.
	/// <para>
	/// Empty unless diagnostics are enabled, since recording sits on a
	/// lock-free hot path. They can be turned on over the socket with
	/// <c>{"cmd":"set","diagnostics":true}</c>.
	/// </para>
	/// </remarks>
	private JsonObject Invalidations()
	{
		var windows = new JsonArray();

		foreach (var window in _system.WindowStateService.GetWindowsByZOrder())
		{
			var trace = window.ConsumeInvalidationTrace();
			if (trace.Count == 0) continue;

			// Who asked, and how often - the count is what makes a runaway setter
			// obvious, and the raw sequence would mostly repeat.
			var byControl = new Dictionary<string, (int Repaints, int Relayouts)>();
			foreach (var entry in trace)
			{
				string key = entry.Control ?? "(window)";
				byControl.TryGetValue(key, out var tally);
				if (entry.Work == Invalidation.Relayout) tally.Relayouts++;
				else tally.Repaints++;
				byControl[key] = tally;
			}

			// Sorted by hand rather than with LINQ over a tuple: ordering value
			// tuples wants dynamic code, and the library is AOT-clean.
			var ranked = new List<KeyValuePair<string, (int Repaints, int Relayouts)>>(byControl);
			ranked.Sort((x, y) =>
				(y.Value.Repaints + y.Value.Relayouts).CompareTo(x.Value.Repaints + x.Value.Relayouts));

			var callers = new JsonArray();
			foreach (var (name, tally) in ranked)
			{
				// Cast to JsonNode? so this binds to the non-generic overload:
				// JsonArray.Add<T> needs reflection, and the library is AOT-clean.
				callers.Add((JsonNode?)new JsonObject
				{
					["control"] = name,
					["repaints"] = tally.Repaints,
					["relayouts"] = tally.Relayouts,
				});
			}

			windows.Add((JsonNode?)new JsonObject
			{
				["window"] = window.Title,
				["total"] = trace.Count,
				["callers"] = callers,
			});
		}

		return new JsonObject { ["windows"] = windows };
	}

	/// <summary>
	/// Every control Tab can reach in the active window, in Tab order.
	/// </summary>
	/// <remarks>
	/// <c>focus</c> says what HAS focus; this says what COULD. A control can sit
	/// in the layout tree, enabled and focusable, and still never be offered by
	/// the scope it lives in - which a snapshot cannot show and the tree cannot
	/// either. Compare this against <c>tree</c> and the controls in one and not
	/// the other are the unreachable ones.
	/// </remarks>
	private JsonObject Reachable()
	{
		var window = _system.WindowStateService.ActiveWindow;
		if (window == null)
			return new JsonObject { ["window"] = null, ["stops"] = new JsonArray() };

		var stops = new JsonArray();

		// On the UI thread: the walk moves real focus and puts it back, and doing
		// that underneath a render would be a race with the thing being measured.
		Marshal(() =>
		{
			foreach (var control in window.FocusManager.ReachableControls())
			{
				// The id, not just a name that may not exist. A Tab stop you
				// cannot name is one you cannot hand to focusctl, and most
				// controls are unnamed - which made every stop of a repeated
				// type indistinguishable from its siblings.
				stops.Add((JsonNode?)new JsonObject
				{
					["control"] = (control as IWindowControl)?.Name
						?? control.GetType().Name,
					["id"] = control is IWindowControl c
						? DiagnosticsSerializer.ControlId(c)
						: null,
					["type"] = control.GetType().Name,
				});
			}
		});

		return new JsonObject
		{
			["window"] = window.Title,
			["count"] = stops.Count,
			["stops"] = stops,
		};
	}

	/// <summary>
	/// Why a control is the size it is: the chain of offers that got it there.
	/// </summary>
	/// <remarks>
	/// <c>tree</c> reports desired against arranged, which says a control wanted
	/// one size and got another without saying what it was ALLOWED to want.
	/// This renders a frame with measures recorded and reports, per control, the
	/// containers above it and the offer each made - the layout answer to a
	/// browser's rule cascade, where the question is "why is this forty wide"
	/// and the answer is usually a container several levels up.
	/// <para>
	/// Pass <c>"control"</c> to narrow it to one, since a whole frame is every
	/// leaf in the window.
	/// </para>
	/// </remarks>
	/// <summary>
	/// What one control is, where it sits, and what contains it.
	/// </summary>
	/// <param name="req">The request, carrying <c>control</c> and optionally <c>window</c>.</param>
	/// <remarks>
	/// Four verbs took a control before this one and all four acted on it or
	/// explained one axis of it. Reading what a control IS meant asking for the
	/// whole tree with props and walking it client-side - a reply that grows
	/// with the application, to answer one question about one control.
	/// </remarks>
	private JsonObject Control(JsonNode? req)
	{
		string? name = Text(req, "control");
		if (string.IsNullOrEmpty(name))
			return ErrorBody("control needs a name or the id `tree` reports, as \"control\".");

		// The same resolution every window-scoped verb uses: a named window, or
		// the active one.
		var window = ResolveWindow(req?.AsObject() ?? new JsonObject(), out string? err);
		if (window == null) return ErrorBody(err!);

		var found = DiagnosticsSerializer.Control(window, name);

		// Named rather than silently empty: a typo and a control that is really
		// not there read the same otherwise, and the first is likelier.
		return found ?? ErrorBody(
			$"no control '{name}' in '{window.Title}'. `tree` lists what is there.");
	}

	/// <summary>
	/// What is under a point on the screen.
	/// </summary>
	/// <param name="req">The request, carrying <c>x</c> and <c>y</c>.</param>
	/// <remarks>
	/// The inspector's question. <c>control</c> needs a name; this needs only
	/// coordinates, which is what you have when you are looking at a
	/// screenshot and something is wrong there.
	/// </remarks>
	private JsonObject Inspect(JsonNode? req)
	{
		int? x = req?["x"]?.GetValue<int>();
		int? y = req?["y"]?.GetValue<int>();

		if (x == null || y == null)
			return ErrorBody("inspect needs a point: \"x\" and \"y\".");

		// Marshalled: hit-testing reads window bounds and the layout tree, and
		// the render thread writes both.
		JsonObject? answer = null;
		Marshal(() => answer = DiagnosticsSerializer.Inspect(_system, x.Value, y.Value));

		return answer ?? ErrorBody("could not read that point.");
	}

	private JsonObject Measure(JsonNode? req)
	{
		string? only = Text(req, "control");

		var window = _system.WindowStateService.ActiveWindow;
		if (window == null)
			return new JsonObject { ["window"] = null, ["controls"] = new JsonArray() };

		// A caller hands back whatever `tree` gave it, which for an unnamed
		// control is an id the trace has never heard of.
		if (only != null) only = ResolveMeasureKey(window, only);

		// One traced frame: tracing is scoped rather than left on, because measure
		// runs for every node on every relayout.
		//
		// All three steps go inside the marshal. The trace is per-thread, so that
		// it cannot hand one probe another's numbers - which means reading it from
		// the socket thread would read a buffer layout never wrote to.
		System.Collections.Generic.IReadOnlyList<Layout.LayoutNode.MeasureEntry> measures =
			Array.Empty<Layout.LayoutNode.MeasureEntry>();

		Marshal(() =>
		{
			Layout.LayoutNode.ClearMeasureTrace();
			using (Layout.LayoutNode.BeginMeasureTracing())
			{
				window.Invalidate(Invalidation.Relayout);
				window.RenderAndGetVisibleContent();
			}
			measures = Layout.LayoutNode.ConsumeMeasureTrace();
		});

		var controls = new JsonArray();
		var seen = new HashSet<string>();

		foreach (var entry in measures)
		{
			if (entry.Control == null) continue;
			if (only != null && entry.Control != only) continue;

			// The last measure of a control is the one that stuck; earlier ones
			// in the same frame are a container trying sizes out.
			if (!seen.Add(entry.Control)) continue;

			var cascade = new JsonArray();
			for (int i = 0; i < entry.Cascade.Count; i++)
			{
				var link = entry.Cascade[i];
				cascade.Add((JsonNode?)new JsonObject
				{
					["layout"] = link.Layout,
					["control"] = link.Control,
					["id"] = link.Id,
					["offeredWidth"] = link.MaxWidth,
					["offeredHeight"] = link.MaxHeight,
					["setsWidth"] = i == entry.DecidedBy.Width,
					["setsHeight"] = i == entry.DecidedBy.Height,
				});
			}

			controls.Add((JsonNode?)new JsonObject
			{
				["control"] = entry.Control,
				["id"] = entry.Id,
				["offeredWidth"] = entry.MaxWidth,
				["offeredHeight"] = entry.MaxHeight,
				["desiredWidth"] = entry.DesiredWidth,
				["desiredHeight"] = entry.DesiredHeight,
				["cascade"] = cascade,
			});
		}

		return new JsonObject
		{
			["window"] = window.Title,
			["controls"] = controls,
		};
	}

	/// <summary>
	/// Why a role is the colour it is, step by step.
	/// </summary>
	/// <remarks>
	/// A resolved role is COMPUTED, not looked up: a theme anchor seeds it,
	/// contrast against the surface may move it, focus tints it and disabled
	/// fades it. The result is one colour, and when it is not the colour the
	/// author set, the question is which step moved it - the same question a
	/// browser answers by showing the rules that applied and the one that won.
	/// <para>
	/// Takes <c>"role"</c> (default Primary), and optionally <c>"state"</c>
	/// and <c>"outline"</c>.
	/// </para>
	/// </remarks>
	private JsonObject ColorCascade(JsonNode? req)
	{
		string roleName = Text(req, "role") ?? nameof(ColorRole.Primary);
		if (!Enum.TryParse<ColorRole>(roleName, ignoreCase: true, out var role))
			return Error($"unknown role '{roleName}'", null);

		string stateName = Text(req, "state") ?? nameof(ColorRoleState.Normal);
		if (!Enum.TryParse<ColorRoleState>(stateName, ignoreCase: true, out var state))
			return Error($"unknown state '{stateName}'", null);

		bool outline = req?["outline"]?.GetValue<bool>() ?? false;

		var theme = _system.Theme;
		var steps = new JsonArray();

		foreach (var step in ColorRoleResolver.Explain(role, theme, outline, state))
		{
			steps.Add((JsonNode?)new JsonObject
			{
				["source"] = step.Source,
				["color"] = step.Color.ToString(),
			});
		}

		var resolved = ColorRoleResolver.Resolve(role, theme, outline, state);

		return new JsonObject
		{
			["theme"] = theme.Name,
			["role"] = role.ToString(),
			["state"] = state.ToString(),
			["outline"] = outline,
			["background"] = resolved.Background.ToString(),
			["text"] = resolved.Text.ToString(),
			["cascade"] = steps,
		};
	}

	/// <summary>
	/// The input events dispatched since the last call, and cleared by it.
	/// </summary>
	/// <remarks>
	/// A control that mishandles a gesture and one that never received it look
	/// identical from outside: nothing happens, or the wrong thing happens
	/// twice. Issue #92 was a double activation from one double click, and
	/// finding it took a reading of the source because nothing could show the
	/// events as they arrived.
	/// <para>
	/// Recorded after the recognizer has added its derived gestures, so a double
	/// click appears as the four events a control actually sees.
	/// </para>
	/// </remarks>
	private JsonObject Events()
	{
		// An empty tape and a tape that was never running look the same, and the
		// second is the likelier one: a probe asking an app whose recording is
		// off would otherwise read "no input happened".
		if (!_system.Input.RecordEvents)
		{
			return new JsonObject
			{
				["recording"] = false,
				["count"] = 0,
				["events"] = new JsonArray(),
				["hint"] = "turn recording on with {\"cmd\":\"set\",\"diagnostics\":true}",
			};
		}

		var events = new JsonArray();

		foreach (var entry in _system.Input.ConsumeEventTape())
		{
			var taped = new JsonObject
			{
				["kind"] = entry.Kind,
				["flags"] = entry.Flags,
			};

			// A key has no point, and 0,0 would read as the top-left corner.
			if (entry.X >= 0)
			{
				taped["x"] = entry.X;
				taped["y"] = entry.Y;
			}

			events.Add((JsonNode?)taped);
		}

		return new JsonObject
		{
			["recording"] = true,
			["count"] = events.Count,
			["events"] = events,
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

	/// <summary>
	/// Whether the application has finished reacting, and what is holding it.
	/// </summary>
	/// <remarks>
	/// The same conditions <see cref="ConsoleWindowSystem.IsSettled"/> checks,
	/// so a script polling this and a test calling WaitUntilSettledAsync agree. The
	/// blockers are reported because a bare false gives a script nothing to say when
	/// it gives up waiting.
	/// </remarks>
	private JsonObject Settled()
	{
		string blockers = _system.SettleBlockers();

		return new JsonObject
		{
			["settled"] = _system.IsSettled,
			["frame"] = _system.ConsoleDriver.FrameNumber,
			["blockers"] = blockers.Length == 0 ? null : blockers,
		};
	}

	/// <summary>
	/// Reads the clocks, or points one at a frozen clock and steps it.
	/// </summary>
	/// <remarks>
	/// Without a target it reports all three, which is what a reader wants first:
	/// a clock that is frozen when you did not freeze it explains a great deal.
	/// </remarks>
	/// <summary>The achieved frame rate and what each frame cost.</summary>
	/// <remarks>
	/// Read off a ring the render loop fills, so it needs no flag and perturbs
	/// nothing: answering is a walk of three hundred structs. <c>reset</c> empties
	/// the window first, for measuring an action rather than whatever the
	/// application happened to be doing before you attached.
	/// </remarks>
	private JsonObject Performance(JsonObject req)
	{
		if (req["reset"]?.GetValue<bool>() == true)
		{
			_system.Performance.Frames.Reset();

			return new JsonObject
			{
				["reset"] = true,
				["note"] = "window emptied; ask again once frames have rendered",
			};
		}

		return DiagnosticsSerializer.Performance(_system);
	}

	private JsonObject Clock(JsonObject req)
	{
		string? target = Text(req, "target");

		if (target == null)
			return AllClocks();

		Core.SwappableTimeProvider clock;

		try
		{
			clock = _system.ClockFor(target);
		}
		catch (ArgumentException ex)
		{
			return ErrorBody(ex.Message);
		}

		// Freeze: take over with a clock that only moves when told.
		if (req["fake"]?.GetValue<bool>() == true)
		{
			clock.Swap(new Core.ManualTimeProvider(clock.GetUtcNow(), clock.LocalTimeZone));
			return OneClock(target, clock);
		}

		// Hand it back to the real one.
		if (req["system"]?.GetValue<bool>() == true)
		{
			clock.Swap(TimeProvider.System);
			return OneClock(target, clock);
		}

		if (req["advance"] is { } advance)
		{
			if (clock.Inner is not Core.ManualTimeProvider manual)
			{
				return ErrorBody(
					$"'{target}' is on the real clock and cannot be advanced; " +
					$"freeze it first with \u0060clock --target {target} --fake\u0060");
			}

			double ms = advance.GetValue<double>();

			if (ms < 0) return ErrorBody("a clock may not run backwards");

			manual.Advance(TimeSpan.FromMilliseconds(ms));
		}

		return OneClock(target, clock);
	}

	private JsonObject AllClocks()
	{
		var clocks = new JsonObject();

		foreach (string name in ConsoleWindowSystem.ClockNames)
			clocks[name] = OneClock(name, _system.ClockFor(name));

		return new JsonObject { ["clocks"] = clocks };
	}

	private static JsonObject OneClock(string name, Core.SwappableTimeProvider clock) =>
		new()
		{
			["clock"] = name,
			["kind"] = clock.IsSystem ? "system" : "fake",
			["now"] = clock.GetUtcNow().UtcDateTime.ToString("O"),
		};

	private static readonly string[] KnownCommands =
	{
		"snapshot", "image", "tree", "focus", "windows", "metrics", "set",
		"logs", "key", "mouse", "size", "focusctl", "invoke",
		"theme", "window", "frame", "settled", "clock",
			"seq",
			"performance",
			"invalidations",
			"reachable",
			"desktop",
			"control",
			"inspect",
			"measure",
			"color",
			"events", "ping", "commands",
	};

	#endregion

	#region Tier 1-2 handlers

	private JsonObject Snapshot(JsonObject req)
	{
		bool ansi = req["ansi"]?.GetValue<bool>() ?? false;

		// "cells" answers a different question from the text formats: what is in each
		// column, rather than what the row says. Text cannot carry it - both writers
		// drop continuation cells and fold combining marks into the character before
		// them - so a caller whose columns do not add up has to ask for cells.
		string? format = Text(req, "format");

		bool cells = string.Equals(format, "cells", StringComparison.OrdinalIgnoreCase);

		// Markup is the format that parses back into cells, so it is what a
		// viewer asks for when it intends to REPAINT the screen rather than
		// read it. A text line loses the wide-glyph continuation; an escape
		// stream would need a second SGR parser at the other end.
		bool markup = string.Equals(format, "markup", StringComparison.OrdinalIgnoreCase);

		if ((cells || markup) && ansi)
		{
			return ErrorBody(
				$"ansi and format:{format} are two different answers to the same question. "
				+ "Ask for one.");
		}

		// A region, because the interesting part of a 200x60 terminal is usually six rows of
		// it. Omitted fields mean "to the edge", so {"y":20,"h":3} is a status-bar read.
		var region = new DiagnosticsSerializer.Region(
			req["x"]?.GetValue<int>(),
			req["y"]?.GetValue<int>(),
			req["w"]?.GetValue<int>(),
			req["h"]?.GetValue<int>());

		if (req["composed"]?.GetValue<bool>() == true)
		{
			return cells
				? DiagnosticsSerializer.ComposedCells(_system, region)
				: DiagnosticsSerializer.ConsoleSnapshot(_system, ansi, markup, region);
		}

		var window = ResolveWindow(req, out string? err);
		if (window == null) return ErrorBody(err!);

		return cells
			? DiagnosticsSerializer.WindowCells(window, region)
			: DiagnosticsSerializer.Snapshot(window, ansi, markup, region);
	}

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
			error = "no composed buffer: this driver retains nothing to read. Capture a "
				  + "window instead - {\"cmd\":\"png\",\"window\":\"<title>\"} - which "
				  + "needs no composed buffer.";
			return null;
		}

		return shot.AsSurface(_system.Theme);
	}

	/// <summary>
	/// The screen as an image, in the format the caller asks for.
	/// </summary>
	/// <remarks>
	/// One verb rather than one per format. The reply always names what it produced in its
	/// <c>format</c> field, so a client never has to infer it from the verb it called.
	/// </remarks>
	/// <param name="req">The request.</param>
	private JsonObject Image(JsonObject req)
	{
		// "format": "png" (default) | "tga" | "jpeg"
		Surfaces.ImageFormat format;
		string name = Text(req, "format")?.ToLowerInvariant() ?? "png";
		switch (name)
		{
			case "png": format = Surfaces.ImageFormat.Png; break;
			case "tga": format = Surfaces.ImageFormat.Tga; break;
			case "jpeg":
			case "jpg": format = Surfaces.ImageFormat.Jpeg; break;
			default:
				return ErrorBody($"unknown format '{name}'; expected png, tga or jpeg");
		}

		int quality = req["quality"]?.GetValue<int>() ?? DefaultJpegQuality;

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
		string? path = Text(req, "path");
		bool wantGlyphs = req["glyphs"]?.GetValue<bool>() ?? true;
		string? fontPath = Text(req, "font");

		// Load the font FIRST: a monospace advance decides the cell width, so the image
		// geometry depends on it. Without glyphs, fall back to a plain 8-wide cell.
		DiagnosticsFont? font = null;
		string? fontError = null;
		if (wantGlyphs)
		{
			// "fontSource": "automatic" (default) | "embedded" | "strict" | "file"
			var fontSource = Text(req, "fontSource")?.ToLowerInvariant() switch
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

		var bytes = DiagnosticsImage.Render(
			cropped, cw, ch, font, out string? renderError, format, quality);
		if (bytes == null)
		{
			font?.Dispose();
			return ErrorBody(renderError!);
		}

		var body = DiagnosticsImage.Reply(bytes, rw * cw, rh * ch, path, format);
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
		// need. Costly per frame, so it starts off unless the app asked for it - but the
		// recorder always exists, so it can be resumed and paused mid-session, which is
		// exactly what a debugging session wants and what a misbehaving app can no longer
		// refuse.
		var diag = _system.RenderingDiagnostics;
		if (diag != null && req["diagnostics"]?.GetValue<bool>() is { } wantDiag
			&& diag.IsEnabled != wantDiag)
		{
			diag.IsEnabled = wantDiag;

			// The input tape goes with it. "Diagnostics" is one switch to the
			// person flipping it, and a render recorder that came on while the
			// tape stayed off left `events` answering "not recording" and
			// pointing at the command that had just been run.
			_system.Input.RecordEvents = wantDiag;

			changed.Add((JsonNode?)JsonValue.Create("diagnostics"));
		}

		// Log capture, and its level floor. Raising the floor mid-run is the cheapest way
		// to find a noisy category without restarting.
		if (req["logging"]?.GetValue<bool>() is { } wantLog &&
			_system.LogService is Logging.LogService ls1 && ls1.IsEnabled != wantLog)
		{
			ls1.IsEnabled = wantLog;
			changed.Add((JsonNode?)JsonValue.Create("logging"));
		}

		if (Text(req, "logLevel") is { } levelName)
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
			["recordingInput"] = _system.Input.RecordEvents,
			["diagnosticsAvailable"] = diag != null,
			["layers"] = diag?.EnabledLayers.ToString(),
			["logging"] = _system.LogService is Logging.LogService l3 ? l3.IsEnabled : null,
			["logLevel"] = _system.LogService is Logging.LogService l4 ? l4.MinimumLevel.ToString() : null,
			["logCount"] = _system.LogService is Logging.LogService l5 ? l5.Count : null,
			["theme"] = DiagnosticsSerializer.ThemeName(_system),
			["changed"] = changed,
		};

		return body;
	}

	private JsonObject Tree(JsonObject req)
	{
		int depth = req["depth"]?.GetValue<int>() ?? -1;
		bool props = req["props"]?.GetValue<bool>() ?? false;

		// THE tree: desktop, its windows, their controls. A caller who wants one
		// window names it; nothing is guessed on their behalf, because a tree that
		// silently picks one of several windows looks authoritative and may be the
		// wrong one.
		string? id = Text(req, "window");
		if (string.IsNullOrEmpty(id))
			return DiagnosticsSerializer.Desktop(_system, depth, props);

		var window = ResolveWindow(req, out string? err);
		if (window == null) return ErrorBody(err!);

		return DiagnosticsSerializer.Tree(window, depth, props);
	}

	/// <summary>
	/// The log buffer, which a TUI can never print to its own screen.
	/// </summary>
	private JsonObject Logs(JsonObject req) => DiagnosticsSerializer.Logs(
		_system,
		req["count"]?.GetValue<int>() ?? 50,
		Text(req, "level"),
		Text(req, "category"),
		Text(req, "contains"));

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
		string spec = Text(req, "spec") ?? "";
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
	/// <summary>Feeds raw terminal bytes through the decoder.</summary>
	/// <remarks>
	/// <para>
	/// The only way in below <c>key</c> and <c>mouse</c>, which both send
	/// already-decoded input. Without it the decoder cannot be driven at all,
	/// and the things it alone handles - X10 mouse reports, bracketed paste,
	/// cursor-position replies, anything malformed - are unreachable. Those are
	/// where a parser goes wrong.
	/// </para>
	/// <para>
	/// Takes either <c>text</c>, with backslash escapes, or <c>raw</c>, as hex -
	/// so a byte dump captured from somebody's broken terminal replays verbatim.
	/// </para>
	/// </remarks>
	private JsonObject Seq(JsonObject req)
	{
		byte[] bytes;

		if (Text(req, "raw") is { } hex)
		{
			if (!TryHex(hex, out bytes, out string? hexError)) return ErrorBody(hexError!);
		}
		else if (Text(req, "text") is { } text)
		{
			if (!TryEscapes(text, out bytes, out string? escapeError)) return ErrorBody(escapeError!);
		}
		else
		{
			return ErrorBody("seq requires 'text' (with \\e escapes) or 'raw' (hex)");
		}

		if (bytes.Length == 0) return ErrorBody("seq was given no bytes");

		List<string> decoded = [];

		// On the UI thread, because a mouse event dispatches inline and would
		// otherwise run a control's handler on the socket's thread.
		Marshal(() => decoded = _system.Input.InjectBytes(bytes));

		var events = new JsonArray();
		foreach (string line in decoded) events.Add((JsonNode?)JsonValue.Create(line));

		return new JsonObject
		{
			["bytes"] = bytes.Length,
			["events"] = events,
		};
	}

	/// <summary>Turns a hex dump into bytes.</summary>
	/// <remarks>
	/// Spaces and a leading 0x are ignored, so a dump pasted from anywhere
	/// works. An odd number of digits is refused rather than padded: the
	/// missing nibble could belong at either end.
	/// </remarks>
	private static bool TryHex(string hex, out byte[] bytes, out string? error)
	{
		bytes = [];
		error = null;

		string clean = hex.Replace(" ", "").Replace("0x", "").Replace(",", "");

		if (clean.Length % 2 != 0)
		{
			error = $"'raw' has {clean.Length} hex digits, which is not a whole number of bytes";
			return false;
		}

		var parsed = new byte[clean.Length / 2];

		for (int i = 0; i < parsed.Length; i++)
		{
			if (!byte.TryParse(clean.AsSpan(i * 2, 2),
				System.Globalization.NumberStyles.HexNumber, null, out parsed[i]))
			{
				error = $"'{clean.Substring(i * 2, 2)}' is not a hex byte";
				return false;
			}
		}

		bytes = parsed;
		return true;
	}

	/// <summary>Turns backslash escapes into bytes.</summary>
	/// <remarks>
	/// An unknown escape is an ERROR, not a literal backslash. Typing one by
	/// mistake and having it silently sent as two characters is how a session
	/// is spent debugging the debugger.
	/// </remarks>
	private static bool TryEscapes(string text, out byte[] bytes, out string? error)
	{
		bytes = [];
		error = null;

		var buffer = new List<byte>(text.Length);

		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] != '\\')
			{
				// Non-ASCII is sent as UTF-8, the way a terminal sends it.
				buffer.AddRange(System.Text.Encoding.UTF8.GetBytes(text[i].ToString()));
				continue;
			}

			if (++i >= text.Length)
			{
				error = "the sequence ends with a lone backslash";
				return false;
			}

			switch (text[i])
			{
				case 'e': buffer.Add(0x1b); break;
				case 'n': buffer.Add((byte)'\n'); break;
				case 'r': buffer.Add((byte)'\r'); break;
				case 't': buffer.Add((byte)'\t'); break;
				case '0': buffer.Add(0); break;
				case '\\': buffer.Add((byte)'\\'); break;

				case 'x':
					if (i + 2 >= text.Length
						|| !byte.TryParse(text.AsSpan(i + 1, 2),
							System.Globalization.NumberStyles.HexNumber, null, out byte hexByte))
					{
						error = $"\\x at position {i - 1} needs two hex digits";
						return false;
					}

					buffer.Add(hexByte);
					i += 2;
					break;

				default:
					error = $"unknown escape '\\{text[i]}'; known: \\e \\n \\r \\t \\0 \\\\ \\xNN";
					return false;
			}
		}

		bytes = buffer.ToArray();
		return true;
	}

	private JsonObject Mouse(JsonObject req)
	{
		int? x = req["x"]?.GetValue<int>();
		int? y = req["y"]?.GetValue<int>();
		if (x == null || y == null) return ErrorBody("mouse requires 'x' and 'y'");

		string button = Text(req, "button") ?? "left";
		string action = Text(req, "action") ?? "click";

		var sequence = MouseSequenceFor(button, action, out string? flagError);
		if (sequence == null) return ErrorBody(flagError!);

		var point = new System.Drawing.Point(x.Value, y.Value);

		Marshal(() =>
		{
			foreach (var flags in sequence) _system.Input.InjectMouseEvent(flags, point);
		});
		return new JsonObject
		{
			["mouse"] = $"{action} {button} @ {x},{y}",
		};
	}

	/// <summary>
	/// The event or events an action sends, in order.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A click is a PRESS and then a RELEASE, which is what a terminal reports and
	/// what <c>HeadlessConsoleDriver.SimulateClick</c> sends, so the recognizer
	/// derives <c>Button1Clicked</c> the same way for both. Sending the derived
	/// flag directly would skip that derivation - and a bug in it, which is what
	/// issue #92 was, would then be invisible from here while a real terminal
	/// double-fired.
	/// </para>
	/// <para>
	/// <c>press</c>, <c>release</c> and <c>releaseclick</c> still send exactly one
	/// event each, for a caller that wants one specific thing rather than a gesture.
	/// </para>
	/// </remarks>
	private static List<List<MouseFlags>>? MouseSequenceFor(string button, string action, out string? error)
	{
		string wanted = action.ToLowerInvariant();

		// A click and a double click are SEQUENCES. Everything else is one event.
		if (wanted is "click" or "doubleclick")
		{
			var press = MouseFlagsFor(button, "press", out error);
			if (press == null) return null;

			var release = MouseFlagsFor(button, "release", out error);
			if (release == null) return null;

			return wanted == "click"
				? [press, release]
				: [press, release, press, release];
		}

		var single = MouseFlagsFor(button, action, out error);
		return single == null ? null : [single];
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

		MouseFlags pressed, released, clicked, doubleClicked, dragged;
		switch (button.ToLowerInvariant())
		{
			case "left":
				(pressed, released, clicked, doubleClicked, dragged) =
					(MouseFlags.Button1Pressed, MouseFlags.Button1Released,
					 MouseFlags.Button1Clicked, MouseFlags.Button1DoubleClicked,
					 MouseFlags.Button1Dragged);
				break;
			case "right":
				(pressed, released, clicked, doubleClicked, dragged) =
					(MouseFlags.Button3Pressed, MouseFlags.Button3Released,
					 MouseFlags.Button3Clicked, MouseFlags.Button3DoubleClicked,
					 MouseFlags.Button3Dragged);
				break;
			case "middle":
				(pressed, released, clicked, doubleClicked, dragged) =
					(MouseFlags.Button2Pressed, MouseFlags.Button2Released,
					 MouseFlags.Button2Clicked, MouseFlags.Button2DoubleClicked,
					 MouseFlags.Button2Dragged);
				break;
			default:
				error = $"unknown button '{button}'; known: left, right, middle";
				return null;
		}

		// These are the SINGLE events. A click and a double click are sequences and
		// are assembled in MouseSequenceFor, which calls this for each half.
		//
		// The comment that used to sit here said a press plus a release "produces a
		// drag that never clicks". That was true before MouseGestureRecognizer: the
		// recognizer now derives Button1Clicked from the pair, which is exactly why
		// click sends the pair rather than the derived flag.
		return action.ToLowerInvariant() switch
		{
			"click" => new List<MouseFlags> { clicked },
			"doubleclick" => new List<MouseFlags> { doubleClicked },
			"press" => new List<MouseFlags> { pressed },
			"release" => new List<MouseFlags> { released },

			// Release and click in ONE event, which is what a terminal reports: SGR carries
			// the click within the press/release group. A client that sends them as two
			// messages breaks any control that opens a portal on release - a dropdown opens
			// on the release, then the separate click hits the portal that just appeared
			// (InputCoordinator routes Clicked to portals before windows) and closes it.
			"releaseclick" => new List<MouseFlags> { released, clicked },
			"move" => new List<MouseFlags> { MouseFlags.ReportMousePosition },

			// Motion WITH the button held, which is what a terminal reports mid-drag
			// and what "move" alone cannot express. Without this a client can press
			// and release but never actually drag, so window moves, resizes,
			// splitters, sliders and text selection are all untestable over the
			// socket - every one of them needs this event in the middle.
			"drag" => new List<MouseFlags>
			{
				MouseFlags.ReportMousePosition, pressed, dragged,
			},
			_ => Unknown(action, out error),
		};

		static List<MouseFlags>? Unknown(string action, out string error)
		{
			error = $"unknown action '{action}'; known: click, doubleclick, press, release, "
				  + "releaseclick, move, drag, wheelup, wheeldown";
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
		string name = Text(req, "name") ?? "";
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
		string name = Text(req, "name") ?? "";
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
		string name = Text(req, "name") ?? "";

		// With no name it is a question rather than an instruction, which is
		// how a caller finds out what it may ask for.
		if (name.Length == 0) return DiagnosticsSerializer.Themes(_system);

		bool ok = false;
		Marshal(() => ok = _system.ThemeStateService.SwitchTheme(name));

		if (ok) return new JsonObject { ["theme"] = name };

		// Name what exists, the way an unknown verb is answered with the verbs
		// that do: an error that only says no leaves the caller guessing.
		var known = string.Join(", ", _system.ThemeRegistryService.GetAvailableThemeNames());

		return ErrorBody($"no theme named '{name}'; known: {known}");
	}

	private JsonObject WindowCmd(JsonObject req)
	{
		// The action field may NAME the window - {"activate":"Settings"} - which
		// is how this verb is documented and the only spelling that reads well
		// on a command line. It may also be a bare flag beside a 'window'
		// field. Both work; the named one wins, since it is more specific.
		string? action = Action(req, "activate") ?? Action(req, "close");

		var window = action != null
			? ResolveWindow(new JsonObject { ["window"] = action }, out string? err)
			: ResolveWindow(req, out err);

		if (window == null) return ErrorBody(err!);

		if (Asked(req, "activate"))
		{
			Marshal(() => _system.WindowStateService.SetActiveWindow(window));
			return new JsonObject { ["activated"] = window.Title };
		}

		if (Asked(req, "close"))
		{
			Marshal(() => window.Close());
			return new JsonObject { ["closed"] = window.Title };
		}

		return ErrorBody("window requires 'activate' or 'close'");
	}

	/// <summary>The window a field names, or null when it is a bare flag.</summary>
	private static string? Action(JsonObject req, string field)
	{
		string? text = Text(req, field);

		// "true"/"false" is the flag spelling arriving as text, not a window
		// anyone named. A window actually called "true" is not worth the
		// ambiguity.
		return bool.TryParse(text, out _) ? null : text;
	}

	/// <summary>Whether a field asks for its action at all.</summary>
	/// <remarks>
	/// True for a bare flag and for a field naming a window, false for an
	/// explicit false. GetValue&lt;bool&gt; THREW on the documented
	/// {"activate":"Settings"} spelling, and the handler's catch turned that
	/// into the reply - so the documented form answered with a .NET exception.
	/// </remarks>
	private static bool Asked(JsonObject req, string field)
	{
		string? text = Text(req, field);
		if (text == null) return false;

		return !bool.TryParse(text, out bool flag) || flag;
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
		string? id = Text(req, "window");

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

	/// <summary>
	/// Finds a control by the identifier a caller holds: its name, or the id
	/// <c>tree</c> reported for it.
	/// </summary>
	/// <remarks>
	/// A debugger reads an id from <c>tree</c> and hands it back here. An unnamed
	/// control has no other handle, so matching names alone would make selection
	/// work only for controls the app happened to name.
	/// <para>
	/// First match wins. Names are not enforced unique, and two controls sharing
	/// one resolve to whichever the walk reaches first - consistently, since the
	/// walk is deterministic, but arbitrarily.
	/// </para>
	/// <para>
	/// Ids do not collide with each other by accident, being instance-derived.
	/// They are not a separate namespace from names, though: a control
	/// deliberately named <c>"ButtonControl#3f2a1b"</c> would answer to that
	/// lookup if the walk reached it first. Contrived, and the same first-match
	/// rule covers it.
	/// </para>
	/// </remarks>
	private static IWindowControl? FindByName(Window window, string name)
	{
		// The LAYOUT tree, because that is what `tree` walks and therefore what a
		// caller's id came from. Walking controls instead descends only through
		// IControlHost, and NavigationView and HorizontalGridControl are not one:
		// a control inside either was reported with an id that then resolved to
		// nothing. An id that names something the tree showed must find it.
		var hit = SearchNode(window.Renderer?.RootLayoutNode, name);
		if (hit != null) return hit;

		// The control walk still runs as a fallback, for anything held outside
		// the layout tree - a portal's content, a control added since the last
		// layout pass.
		foreach (var c in window.GetControls())
		{
			var found = Search(c, name);
			if (found != null) return found;
		}
		return null;
	}

	/// <summary>Finds a control by name or id anywhere in a layout tree.</summary>
	private static IWindowControl? SearchNode(Layout.LayoutNode? node, string name)
	{
		if (node == null) return null;

		if (node.Control is { } c
			&& (string.Equals(c.Name, name, StringComparison.Ordinal)
				|| string.Equals(DiagnosticsSerializer.ControlId(c), name, StringComparison.Ordinal)))
		{
			return c;
		}

		// By index, re-reading Count each step, because this walks the LIVE tree
		// from the socket thread while the render loop may be rebuilding it. A
		// foreach over the backing list throws "Collection was modified" the
		// moment a relayout lands mid-walk, and the caller sees a spurious
		// InvalidOperationException from focusctl or measure on a busy app.
		//
		// Marshalling onto the UI thread would also fix it, at the cost of a
		// round-trip per lookup and a five-second stall whenever the app is
		// wedged - which is exactly when a debugger is most needed. Tolerating
		// the mutation is cheaper and degrades better: a walk that races a
		// relayout may miss a control that moved, and the caller retries.
		var children = node.Children;
		for (int i = 0; i < children.Count; i++)
		{
			Layout.LayoutNode child;
			try { child = children[i]; }
			catch (ArgumentOutOfRangeException) { break; }   // shrank under us

			var hit = SearchNode(child, name);
			if (hit != null) return hit;
		}
		return null;
	}

	/// <summary>
	/// Turns the identifier a caller holds into the key the measure trace stores.
	/// </summary>
	/// <param name="window">The window to resolve against.</param>
	/// <param name="nameOrId">A control's name, or the id <c>tree</c> reported.</param>
	/// <returns>
	/// What <c>MeasureEntry.Control</c> would hold for that control, or
	/// <paramref name="nameOrId"/> unchanged when nothing matches - so an
	/// unknown value filters to nothing rather than silently widening.
	/// </returns>
	/// <remarks>
	/// The trace records <c>Name ?? TypeName</c>, set in <c>LayoutNode</c>, which
	/// is not the id. Resolving here rather than changing what the trace stores
	/// keeps the wire format out of the layout engine, and keeps the existing
	/// measure tests - which match on the bare name - meaning what they meant.
	/// </remarks>
	private static string ResolveMeasureKey(Window window, string nameOrId)
	{
		var control = FindByName(window, nameOrId);
		return control == null
			? nameOrId
			: control.Name ?? control.GetType().Name;
	}

	/// <summary>Exposes <see cref="ResolveMeasureKey"/> for tests.</summary>
	internal static string ResolveMeasureKeyForTest(Window window, string nameOrId)
		=> ResolveMeasureKey(window, nameOrId);


	/// <summary>Exposes the reachable walk for tests, without a server.</summary>
	internal static JsonObject ReachableForTest(Window window)
	{
		var stops = new JsonArray();

		foreach (var control in window.FocusManager.ReachableControls())
		{
			stops.Add((JsonNode?)new JsonObject
			{
				["control"] = (control as IWindowControl)?.Name ?? control.GetType().Name,
				["id"] = control is IWindowControl c ? DiagnosticsSerializer.ControlId(c) : null,
				["type"] = control.GetType().Name,
			});
		}

		return new JsonObject { ["stops"] = stops };
	}

	/// <summary>Exposes <see cref="FindByName"/> for tests.</summary>
	/// <summary>Reads one request field as text, for a test.</summary>
	internal static string? TextForTest(JsonNode? req, string field) => Text(req, field);

	/// <summary>Whether an action field asks for its action, for a test.</summary>
	internal static bool AskedForTest(JsonObject req, string field) => Asked(req, field);

	/// <summary>The window an action field names, for a test.</summary>
	internal static string? ActionForTest(JsonObject req, string field) => Action(req, field);

	internal static IWindowControl? FindControlForTest(Window window, string nameOrId)
		=> FindByName(window, nameOrId);

	private static IWindowControl? Search(IWindowControl control, string name)
	{
		if (string.Equals(control.Name, name, StringComparison.Ordinal)) return control;

		// The id tree reported, so a caller can hand back what it was given
		// rather than needing the control to have been named.
		if (string.Equals(DiagnosticsSerializer.ControlId(control), name, StringComparison.Ordinal))
			return control;

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
		/// reading a composed desktop from a driver that keeps no buffer. Stop asking.
		/// </summary>
		internal const string WrongDriver = "wrong_driver";

		/// <summary>
		/// The app was built without the recording this verb reads. Not switchable from
		/// here, so a client should disable the feature rather than retry.
		/// </summary>
		internal const string DiagnosticsDisabled = "diagnostics_disabled";
	}

	/// <summary>One field of a request, as text, whatever JSON type it arrived as.</summary>
	/// <param name="req">The request.</param>
	/// <param name="field">Which field.</param>
	/// <returns>The value as text, or null when the field is absent or empty.</returns>
	/// <remarks>
	/// <para>
	/// GetValue&lt;string&gt; THROWS on a JSON value that is not a string, and the
	/// handler's catch turns that into the reply - so `measure --control` with the
	/// value left off answered "InvalidOperationException: An element of type 'True'
	/// cannot be converted to a 'System.String'", which tells a person nothing about
	/// the request they got wrong.
	/// </para>
	/// <para>
	/// A field is read for what it says, not for how it was typed. A bare flag that
	/// should have carried a value arrives as true and now reads as "true", which
	/// matches no control and produces the verb's own "no control matched" - the
	/// right answer, from the right place.
	/// </para>
	/// </remarks>
	private static string? Text(JsonNode? req, string field)
	{
		var value = req?[field];
		if (value is not JsonValue v) return null;

		string? text = v.TryGetValue(out string? s) ? s : v.ToJsonString().Trim('"');

		return string.IsNullOrEmpty(text) ? null : text;
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
