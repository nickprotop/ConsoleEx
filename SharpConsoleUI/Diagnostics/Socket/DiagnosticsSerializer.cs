// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.Json.Nodes;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Surfaces;
using Dock = SharpConsoleUI.Layout.Dock;

namespace SharpConsoleUI.Diagnostics.Socket;

/// <summary>
/// Turns live engine state into the JSON the diagnostics socket serves.
/// </summary>
/// <remarks>
/// Everything here reads state the engine already holds. Nothing measures, arranges or
/// paints: a probe must never change what it is observing, and a serializer that triggered a
/// layout pass would do exactly that.
/// </remarks>
internal static class DiagnosticsSerializer
{
	#region State

	/// <summary>Collects a control's self-reported state into a JSON object.</summary>
	/// <remarks>
	/// Values are written through <see cref="JsonValue.Create(string?, JsonNodeOptions?)"/>
	/// and its overloads rather than a collection initializer, which binds to the
	/// reflective <c>JsonObject.Add&lt;T&gt;</c> and is neither trim- nor AOT-safe.
	/// </remarks>
	private sealed class JsonStateWriter : IStateWriter
	{
		public JsonObject Values { get; } = new();

		public void Write(string name, string? value) => Values[name] = JsonValue.Create(value);

		public void Write(string name, long value) => Values[name] = JsonValue.Create(value);

		public void Write(string name, double value) => Values[name] = JsonValue.Create(value);

		public void Write(string name, bool value) => Values[name] = JsonValue.Create(value);

		public void WriteIf(bool condition, string name, string? value)
		{
			if (condition) Write(name, value);
		}
	}

	/// <summary>What a control says it currently is, or null if it says nothing.</summary>
	private static JsonObject? State(IWindowControl control)
	{
		if (control is not BaseControl bc) return null;

		var writer = new JsonStateWriter();
		try
		{
			bc.DescribeState(writer);
		}
		catch (Exception ex)
		{
			// A control that throws while describing itself must not take the whole
			// probe down with it - the probe exists to diagnose broken controls.
			return new JsonObject { ["describeError"] = ex.GetType().Name };
		}

		return writer.Values.Count == 0 ? null : writer.Values;
	}

	#endregion

	#region Windows

	/// <summary>The window list, in z-order.</summary>
	internal static JsonArray Windows(ConsoleWindowSystem system)
	{
		// Window coordinates are DESKTOP coordinates, and the desktop starts below
		// the top panel. Reporting only those invites a client to click a row too
		// high - which is exactly what happened while this was being written.
		var origin = system.DesktopUpperLeft;

		var arr = new JsonArray();
		foreach (var w in system.WindowStateService.GetWindowsByZOrder())
		{
			arr.Add((JsonNode)new JsonObject
			{
				["id"] = w.Guid,
				["title"] = w.Title,
				["bounds"] = Rect(w.Left, w.Top, w.Width, w.Height),
				["absolute"] = Rect(w.Left + origin.X, w.Top + origin.Y, w.Width, w.Height),
				["modal"] = w.IsModal,
				["state"] = w.State.ToString(),
				["root"] = w.Renderer?.RootLayoutNode?.Layout?.GetType().Name,
				["active"] = ReferenceEquals(system.WindowStateService.ActiveWindow, w),
			});
		}
		return arr;
	}

	#endregion

	#region Layout tree

	/// <summary>
	/// The layout tree for a window: what each node asked for, and what it was given.
	/// </summary>
	/// <remarks>
	/// <c>desired</c> sits beside <c>bounds</c> deliberately. The gap between what a node
	/// wanted and what it got is where layout bugs live - a child that asked for 80 columns
	/// and was arranged into 20 states the problem in one line, where a screenshot only shows
	/// the symptom.
	/// </remarks>
	internal static JsonObject Tree(Window window, int maxDepth, bool includeProps)
	{
		var root = window.Renderer?.RootLayoutNode;
		return new JsonObject
		{
			["window"] = window.Title,
			["id"] = window.Guid,
			["root"] = root?.Layout?.GetType().Name,
			["node"] = root == null ? null : Node(root, 0, maxDepth, includeProps),
		};
	}

	private static JsonObject Node(LayoutNode node, int depth, int maxDepth, bool includeProps)
	{
		var o = new JsonObject
		{
			["control"] = node.Control?.GetType().Name ?? "(root)",
			["layout"] = node.Layout?.GetType().Name,
			["visible"] = node.IsVisible,
			["desired"] = Size(node.DesiredSize),
			["bounds"] = Rect(node.Bounds),
			["absolute"] = Rect(node.AbsoluteBounds),
		};

		if (node.Control is { } c)
		{
			// The id is what another verb accepts; name stays as it was, because
			// something may already be reading it.
			o["id"] = ControlId(c);
			if (!string.IsNullOrEmpty(c.Name)) o["name"] = c.Name;
			if (c.Dock != Dock.None) o["dock"] = c.Dock.ToString();

			if (includeProps)
			{
				o["props"] = new JsonObject
				{
					["width"] = c.Width,
					["height"] = c.Height,
					["horizontal"] = c.HorizontalAlignment.ToString(),
					["vertical"] = c.VerticalAlignment.ToString(),
					["sticky"] = c.StickyPosition.ToString(),
				};
			}

			// What the control IS, as opposed to where it sits. Always present:
			// a reader looking for "why is this checkbox not responding" should not
			// have to know to ask for it.
			var state = State(c);
			if (state != null) o["state"] = state;
		}

		if (maxDepth >= 0 && depth >= maxDepth)
		{
			if (node.Children.Count > 0) o["childrenElided"] = node.Children.Count;
			return o;
		}

		var kids = new JsonArray();
		foreach (var child in node.Children)
			kids.Add((JsonNode)Node(child, depth + 1, maxDepth, includeProps));
		o["children"] = kids;

		return o;
	}

	/// <summary>
	/// The whole desktop as one tree: the system, its windows, and their controls.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The three layers really are nested, and reporting them through three separate
	/// verbs made that hard to see. In particular a window's bounds are in DESKTOP
	/// coordinates: with a top panel visible, a window at <c>y: 0</c> has its title
	/// bar on screen row 1. Reading that from a flat window list is a trap, and the
	/// desktop node states the panel rows so it cannot be misread.
	/// </para>
	/// <para>
	/// Not the default for <c>tree</c>, which stays one window: this walks every
	/// control of every window and the output grows accordingly.
	/// </para>
	/// </remarks>
	internal static JsonObject Desktop(ConsoleWindowSystem system, int maxDepth, bool includeProps)
	{
		var size = system.ConsoleDriver.ScreenSize;

		int topRows = system.PanelStateService.ShowTopPanel ? 1 : 0;
		int bottomRows = system.PanelStateService.ShowBottomPanel ? 1 : 0;

		var windows = new JsonArray();
		foreach (var w in system.WindowStateService.GetWindowsByZOrder())
			windows.Add((JsonNode)WindowNode(system, w, maxDepth, includeProps));

		return new JsonObject
		{
			["desktop"] = new JsonObject
			{
				["width"] = size.Width,
				["height"] = size.Height,

				// The offset that makes a window's y differ from its screen row. Taken
				// from DesktopUpperLeft rather than counted from the panels, because
				// that is the value the hit-testing itself uses.
				["originX"] = system.DesktopUpperLeft.X,
				["originY"] = system.DesktopUpperLeft.Y,
				["topPanelRows"] = topRows,
				["bottomPanelRows"] = bottomRows,
				["desktopBottom"] = size.Height - bottomRows - 1,

				["theme"] = system.Theme?.GetType().Name,
				["windows"] = system.WindowStateService.GetWindowsByZOrder().Count,
				["modals"] = system.ModalStateService.HasModals,
				["portals"] = system.DesktopPortalService.HasPortals,
			},
			["windows"] = windows,
		};
	}

	/// <summary>One window, its own state, and its layout tree.</summary>
	private static JsonObject WindowNode(
		ConsoleWindowSystem system, Window window, int maxDepth, bool includeProps)
	{
		var root = window.Renderer?.RootLayoutNode;

		return new JsonObject
		{
			["id"] = window.Guid,
			["title"] = window.Title,
			["bounds"] = Rect(window.Left, window.Top, window.Width, window.Height),
			["absolute"] = Rect(
				window.Left + system.DesktopUpperLeft.X,
				window.Top + system.DesktopUpperLeft.Y,
				window.Width, window.Height),

			// What the window PERMITS, which decides whether a gesture can work at
			// all. Dragging a non-movable window looks identical to aiming wrong.
			["state"] = new JsonObject
			{
				["windowState"] = window.State.ToString(),
				["active"] = ReferenceEquals(system.WindowStateService.ActiveWindow, window),
				["modal"] = window.IsModal,
				["movable"] = window.IsMovable,
				["resizable"] = window.IsResizable,
				["closable"] = window.IsClosable,
			},

			["root"] = root?.Layout?.GetType().Name,
			["node"] = root == null ? null : Node(root, 0, maxDepth, includeProps),
		};
	}

	#endregion

	#region Focus

	/// <summary>The focused control and the container chain that leads to it.</summary>
	/// <remarks>
	/// The chain, not just the leaf: focus bugs are nearly always about which CONTAINER
	/// claimed focus, which the leaf alone cannot tell you.
	/// </remarks>
	internal static JsonObject Focus(ConsoleWindowSystem system)
	{
		var window = system.WindowStateService.ActiveWindow;
		if (window == null)
			return new JsonObject { ["focused"] = null, ["reason"] = "no active window" };

		var fm = window.FocusManager;
		var chain = new JsonArray { (JsonNode)$"Window:{window.Title}" };
		foreach (var c in fm.FocusPath)
			chain.Add((JsonNode)Describe(c));

		return new JsonObject
		{
			["window"] = window.Title,
			["focused"] = fm.FocusedControl is IWindowControl f ? Describe(f) : null,
			["chain"] = chain,
		};
	}

	/// <summary>
	/// How a caller names one control when asking another verb about it.
	/// </summary>
	/// <param name="c">The control.</param>
	/// <returns>
	/// The control's name when it has one, otherwise its type and instance -
	/// for example <c>MarkupControl#3f2a1b</c>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Names carry most of this. Every control has one, the builders expose
	/// <c>WithName</c>, and anything an app wants to reach later it has already
	/// named in order to reach it from code.
	/// </para>
	/// <para>
	/// The unnamed remainder gets its instance identity rather than a position.
	/// An ordinal would shift the moment a sibling is inserted above it, so an
	/// id captured a moment earlier would silently address a DIFFERENT control -
	/// the worst failure a selection mechanism can have, because nothing looks
	/// wrong. An instance id dies with its control instead, so a stale one
	/// matches nothing.
	/// </para>
	/// <para>
	/// Empty is not a name: <c>string.IsNullOrEmpty</c> is the same guard
	/// <see cref="Describe"/> uses, so the two agree about what counts as named.
	/// </para>
	/// </remarks>
	internal static string ControlId(IWindowControl c) =>
		string.IsNullOrEmpty(c.Name)
			? $"{c.GetType().Name}#{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c):x}"
			: c.Name!;

	private static string Describe(IWindowControl c) =>
		string.IsNullOrEmpty(c.Name) ? c.GetType().Name : $"{c.GetType().Name}#{c.Name}";

	#endregion

	#region Snapshots

	/// <summary>
	/// A window's painted buffer as text, optionally with colour.
	/// </summary>
	/// <remarks>
	/// Reads the window's own buffer, which is what <c>GoldenBuffer</c> captures in tests -
	/// deliberately the same source, so a probe and a test cannot disagree about the same
	/// frame.
	/// </remarks>
	internal static JsonObject Snapshot(Window window, bool ansi, bool markup, Region region)
	{
		var buffer = window.Renderer?.Buffer;
		if (buffer == null)
		{
			return new JsonObject
			{
				["window"] = window.Title,
				["error"] = "window has not painted yet",
			};
		}

		var clipped = region.ClampTo(buffer.Width, buffer.Height, out string? regionError);
		if (regionError != null)
			return new JsonObject { ["window"] = window.Title, ["error"] = regionError };

		var o = Grid(window.CaptureSurface(), clipped, ansi, markup);
		o["window"] = window.Title;
		return o;
	}

	/// <summary>
	/// Walks a rectangle of cells into JSON lines, with the origin recorded so a caller can
	/// map a hit back to real screen coordinates.
	/// </summary>
	/// <remarks>
	/// A region matters because the interesting part of a 200x60 terminal is usually six rows
	/// of it. Returning the whole grid to inspect a status bar wastes the round trip the
	/// socket exists to save, and buries the answer.
	/// </remarks>
	private static JsonObject Grid(Surfaces.ICellSurface surface, Region r, bool ansi, bool markup = false)
	{
		int rx = r.X ?? 0, ry = r.Y ?? 0, rw = r.W ?? 0, rh = r.H ?? 0;

		var cropped = rx == 0 && ry == 0 && rw == surface.Width && rh == surface.Height
			? surface
			: surface.Crop(new Layout.LayoutRect(rx, ry, rw, rh));

		var text = cropped.ToText(
			markup ? Surfaces.TextFormat.Markup
			: ansi ? Surfaces.TextFormat.Ansi
			: Surfaces.TextFormat.Plain);

		var lines = new JsonArray();
		foreach (string line in text.Split('\n'))
			lines.Add((JsonNode)line);

		return new JsonObject
		{
			["x"] = rx,
			["y"] = ry,
			["width"] = rw,
			["height"] = rh,
			["ansi"] = ansi,
			["format"] = markup ? "markup" : ansi ? "ansi" : "plain",
			["lines"] = lines,
		};
	}


	/// <summary>
	/// The themes this application has, and which one is on.
	/// </summary>
	/// <param name="system">The window system.</param>
	/// <remarks>
	/// A verb that only switches is one a caller can use when it already knows
	/// the answer. Anything building a list - a debugger's theme picker, a
	/// script trying each in turn - has to ask, and the socket is the only
	/// thing there is to ask.
	/// </remarks>
	internal static JsonObject Themes(ConsoleWindowSystem system)
	{
		var themes = new JsonArray();
		string? current = null;

		string typeName = system.Theme.GetType().Name;

		foreach (var info in system.ThemeRegistryService.GetAvailableThemes())
		{
			themes.Add((JsonNode?)new JsonObject
			{
				["name"] = info.Name,
				["description"] = info.Description,
			});

			// The registry's name, not the type's: the registry calls it
			// "ModernGray" and the class is ModernGrayTheme, so reporting the
			// type leaves a picker unable to match the current theme against
			// the list it was given.
			if (typeName.Equals(info.Name, StringComparison.Ordinal)
				|| typeName.Equals(info.Name + "Theme", StringComparison.Ordinal))
			{
				current = info.Name;
			}
		}

		return new JsonObject
		{
			["theme"] = current ?? typeName,
			["themes"] = themes,
		};
	}

	/// <summary>A window's painted buffer as cells.</summary>
	/// <param name="window">The window to read.</param>
	/// <param name="region">The rectangle, or <see cref="Region.All"/>.</param>
	internal static JsonObject WindowCells(Window window, Region region)
	{
		if (window.Renderer?.Buffer == null)
		{
			return new JsonObject
			{
				["window"] = window.Title,
				["error"] = "window has not painted yet",
			};
		}

		var o = CellGrid(window.CaptureSurface(), region);
		o["window"] = window.Title;
		return o;
	}

	/// <summary>The composed desktop as cells.</summary>
	/// <param name="system">The window system.</param>
	/// <param name="region">The rectangle, or <see cref="Region.All"/>.</param>
	/// <remarks>
	/// The composed source is the one that can report a background the application never
	/// chose, because it is the only one whose colour was stored as an escape and parsed
	/// back. A window's cells are typed throughout, so their colours are always measured.
	/// </remarks>
	internal static JsonObject ComposedCells(ConsoleWindowSystem system, Region region)
	{
		var shot = system.ConsoleDriver.CaptureComposed()
			?? system.RenderingDiagnostics?.LastConsoleSnapshot;

		if (shot == null)
		{
			return new JsonObject
			{
				["code"] = DiagnosticsServer.ErrorCode.WrongDriver,
				["error"] = "no composed buffer: this driver retains nothing to read. "
					+ "Snapshot a window instead - "
					+ "{\"cmd\":\"snapshot\",\"window\":\"<title>\",\"format\":\"cells\"} "
					+ "- which reads the live render buffer.",
			};
		}

		var o = CellGrid(shot.AsSurface(system.Theme), region);
		o["composed"] = true;
		return o;
	}

	/// <summary>
	/// A rectangle of cells, one entry per column, with nothing folded away.
	/// </summary>
	/// <param name="surface">The painted surface to read.</param>
	/// <param name="region">The rectangle, or <see cref="Region.All"/>.</param>
	/// <returns>The grid, or an error when the region does not fit.</returns>
	/// <remarks>
	/// <para>
	/// The text formats answer "what does this say"; this answers "what is in each
	/// column". They are different questions, and text cannot carry the second: both
	/// text writers skip continuation cells and append combining marks to the character
	/// before them, so a wide glyph arrives as one character with no width and the second
	/// column it occupies is gone. A caller whose columns do not add up is asking about
	/// exactly the information text threw away.
	/// </para>
	/// <para>
	/// Continuation cells are PRESENT here, so index equals column and a caller can
	/// trust arithmetic over the row. Colour arrives as numbers rather than escapes,
	/// because a consumer that wanted to parse SGR could have asked for ANSI.
	/// </para>
	/// <para>
	/// Fields a cell does not have are omitted rather than sent as null: most cells carry
	/// no decoration and no combining marks, and a thousand nulls per frame is noise.
	/// </para>
	/// </remarks>
	internal static JsonObject CellGrid(Surfaces.ICellSurface surface, Region region)
	{
		var r = region.ClampTo(surface.Width, surface.Height, out string? regionError);
		if (regionError != null) return new JsonObject { ["error"] = regionError };

		int rx = r.X ?? 0, ry = r.Y ?? 0, rw = r.W ?? 0, rh = r.H ?? 0;

		var rows = new JsonArray();

		for (int y = ry; y < ry + rh; y++)
		{
			var row = new JsonArray();

			for (int x = rx; x < rx + rw; x++)
			{
				var c = surface[x, y];
				var cell = new JsonObject();

				// A continuation cell has no character of its own: it is the second
				// column of the glyph before it. Saying so and stopping is clearer
				// than repeating that glyph twice.
				if (c.IsWideContinuation)
				{
					cell["cont"] = true;
				}
				else
				{
					cell["ch"] = c.Character.ToString();
					cell["w"] = Helpers.UnicodeWidth.GetRuneWidth(c.Character);
					if (c.Combiners != null) cell["comb"] = c.Combiners;
				}

				cell["fg"] = Rgb(c.Foreground);
				cell["bg"] = Rgb(c.Background);

				if (c.Decorations != TextDecoration.None)
					cell["dec"] = c.Decorations.ToString();

				row.Add((JsonNode?)cell);
			}

			rows.Add((JsonNode?)row);
		}

		return new JsonObject
		{
			["x"] = rx,
			["y"] = ry,
			["width"] = rw,
			["height"] = rh,
			["format"] = "cells",
			["rows"] = rows,
		};
	}

	/// <summary>A colour as three numbers, which is what a consumer wants to compare.</summary>
	private static JsonArray Rgb(Color c) =>
		new() { (JsonNode?)(int)c.R, (JsonNode?)(int)c.G, (JsonNode?)(int)c.B };

	/// <summary>A requested rectangle, before it is known whether it fits.</summary>
	internal readonly record struct Region(int? X, int? Y, int? W, int? H)
	{
		internal static readonly Region All = new(null, null, null, null);

		internal bool IsAll => X == null && Y == null && W == null && H == null;

		/// <summary>
		/// Resolves to a concrete rectangle inside a buffer, or explains why it cannot.
		/// </summary>
		/// <remarks>
		/// A region entirely outside the buffer is an ERROR, not an empty result: silently
		/// returning nothing would read as "that area is blank", which is a different and
		/// misleading answer.
		/// </remarks>
		internal Region ClampTo(int width, int height, out string? error)
		{
			error = null;
			if (IsAll) return new Region(0, 0, width, height);

			int x = Math.Max(0, X ?? 0);
			int y = Math.Max(0, Y ?? 0);

			if (x >= width || y >= height)
			{
				error = $"region origin ({x},{y}) is outside the {width}x{height} buffer";
				return default;
			}

			int w = Math.Min(W ?? (width - x), width - x);
			int h = Math.Min(H ?? (height - y), height - y);

			if (w <= 0 || h <= 0)
			{
				error = $"region {w}x{h} is empty";
				return default;
			}

			return new Region(x, y, w, h);
		}
	}

	/// <summary>The whole desktop as the user sees it, composed, from the render diagnostics.</summary>
	internal static JsonObject ConsoleSnapshot(ConsoleWindowSystem system, bool ansi, bool markup, Region region)
	{
		// Live front buffer first - the composed desktop, always current, no diagnostics
		// needed. The recorded snapshot is only a fallback for a driver that has no buffer.
		var shot = system.ConsoleDriver.CaptureComposed()
			?? system.RenderingDiagnostics?.LastConsoleSnapshot;
		if (shot == null)
		{
			return new JsonObject
			{
				// Name the switch the caller actually controls, and the way out that
				// needs no configuration at all.
				["code"] = DiagnosticsServer.ErrorCode.WrongDriver,
				["error"] = "no composed buffer: this driver retains nothing to read. "
					+ "Snapshot a window instead - "
					+ "{\"cmd\":\"snapshot\",\"window\":\"<title>\"} - which reads the "
					+ "live render buffer.",
			};
		}

		var clipped = region.ClampTo(shot.Width, shot.Height, out string? regionError);
		if (regionError != null) return new JsonObject { ["error"] = regionError };

		var o = Grid(shot.AsSurface(system.Theme), clipped, ansi, markup);
		o["composed"] = true;
		return o;
	}

	#endregion

	#region Metrics

	/// <summary>The frame metrics the renderer already records.</summary>
	internal static JsonObject Metrics(ConsoleWindowSystem system)
	{
		var m = system.RenderingDiagnostics?.LastMetrics;
		if (m == null)
			return new JsonObject
			{
				["code"] = DiagnosticsServer.ErrorCode.DiagnosticsDisabled,
				["error"] = "no metrics: this app runs with EnableDiagnostics off, so the "
					+ "renderer records nothing. Start it with "
					+ "ConsoleWindowSystemOptions.EnableDiagnostics: true, or use "
					+ "{\"cmd\":\"set\",\"diagnostics\":true} if the recorder exists but is paused.",
			};

		return new JsonObject
		{
			["frame"] = m.FrameNumber,
			["bytesWritten"] = m.BytesWritten,
			["charactersChanged"] = m.CharactersChanged,
			["ansiSequences"] = m.AnsiEscapeSequences,
			["cursorMovements"] = m.CursorMovements,
			["dirtyCellsMarked"] = m.DirtyCellsMarked,
			["cellsRendered"] = m.CellsActuallyRendered,
			["efficiency"] = m.EfficiencyRatio,
			["staticFrame"] = m.IsStaticFrame,
			["layoutMs"] = m.DomLayoutTimeMs,
			["ansiMs"] = m.AnsiGenerationTimeMs,
			["outputMs"] = m.ConsoleOutputTimeMs,
		};
	}

	#endregion

	#region Logs

	/// <summary>
	/// Recent log entries, newest last, optionally filtered by level and category.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The log is the one diagnostic a TUI cannot show you by itself: console output would
	/// corrupt the render, so SharpConsoleUI routes everything to a buffer and a file. That
	/// buffer is right here in the process and, until now, unreachable while the app runs -
	/// you had to exit and read the file, by which point the state that produced the entry is
	/// gone.
	/// </para>
	/// <para>
	/// Exceptions are included as type and message. The stack trace is not: it would dwarf
	/// every other field in a line-oriented protocol, and the file log already has it.
	/// </para>
	/// </remarks>
	internal static JsonObject Logs(ConsoleWindowSystem system, int count, string? minLevel,
		string? category, string? contains)
	{
		LogLevel floor = LogLevel.Trace;
		if (!string.IsNullOrEmpty(minLevel) && !Enum.TryParse(minLevel, true, out floor))
		{
			return new JsonObject
			{
				["error"] = $"unknown level '{minLevel}'; known: " +
					string.Join(", ", Enum.GetNames<LogLevel>()),
			};
		}

		// Ask for extra, because filtering happens after the buffer hands them over - a
		// request for 20 Errors should not return 3 just because the last 20 entries were
		// mostly Debug.
		int fetch = string.IsNullOrEmpty(minLevel) && string.IsNullOrEmpty(category)
			&& string.IsNullOrEmpty(contains)
			? count
			: Math.Min(count * 20, 2000);

		var entries = system.LogService.GetRecentLogs(fetch);
		var arr = new JsonArray();

		foreach (var e in entries)
		{
			if (e.Level < floor) continue;
			if (!string.IsNullOrEmpty(category)
				&& !string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase)) continue;
			if (!string.IsNullOrEmpty(contains)
				&& e.Message.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0) continue;

			var o = new JsonObject
			{
				["time"] = e.Timestamp.ToString("O"),
				["level"] = e.Level.ToString(),
				["message"] = e.Message,
			};
			if (!string.IsNullOrEmpty(e.Category)) o["category"] = e.Category;
			if (e.Exception != null)
				o["exception"] = $"{e.Exception.GetType().Name}: {e.Exception.Message}";

			arr.Add((JsonNode)o);
		}

		// Trim to the newest `count` after filtering.
		while (arr.Count > count) arr.RemoveAt(0);

		return new JsonObject { ["count"] = arr.Count, ["entries"] = arr };
	}

	#endregion

	#region Primitives

	private static JsonObject Rect(LayoutRect r) => Rect(r.X, r.Y, r.Width, r.Height);

	private static JsonObject Rect(int x, int y, int w, int h) =>
		new() { ["x"] = x, ["y"] = y, ["w"] = w, ["h"] = h };

	private static JsonObject Size(LayoutSize s) =>
		new() { ["w"] = s.Width, ["h"] = s.Height };

	#endregion
}
