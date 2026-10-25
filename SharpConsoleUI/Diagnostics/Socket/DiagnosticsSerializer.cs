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
	#region Windows

	/// <summary>The window list, in z-order.</summary>
	internal static JsonArray Windows(ConsoleWindowSystem system)
	{
		var arr = new JsonArray();
		foreach (var w in system.WindowStateService.GetWindowsByZOrder())
		{
			arr.Add((JsonNode)new JsonObject
			{
				["id"] = w.Guid,
				["title"] = w.Title,
				["bounds"] = Rect(w.Left, w.Top, w.Width, w.Height),
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
	internal static JsonObject Snapshot(Window window, bool ansi, Region region)
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

		var o = Grid(clipped, ansi, (x, y) =>
		{
			var cell = buffer.GetCell(x, y);
			return (cell.Character.ToString(), AnsiFor(cell.Foreground, cell.Background));
		});

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
	private static JsonObject Grid(Region r, bool ansi, Func<int, int, (string Text, string Ansi)> at)
	{
		var lines = new JsonArray();
		var sb = new StringBuilder();

		int rx = r.X ?? 0, ry = r.Y ?? 0, rw = r.W ?? 0, rh = r.H ?? 0;

		for (int y = ry; y < ry + rh; y++)
		{
			sb.Clear();
			for (int x = rx; x < rx + rw; x++)
			{
				var (text, esc) = at(x, y);
				if (ansi) sb.Append(esc);
				sb.Append(text);
			}
			if (ansi) sb.Append("\u001b[0m");
			lines.Add((JsonNode)sb.ToString());
		}

		return new JsonObject
		{
			["x"] = rx,
			["y"] = ry,
			["width"] = rw,
			["height"] = rh,
			["ansi"] = ansi,
			["lines"] = lines,
		};
	}

	private static string AnsiFor(Color fg, Color bg) =>
		$"\u001b[38;2;{fg.R};{fg.G};{fg.B}m\u001b[48;2;{bg.R};{bg.G};{bg.B}m";

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
	internal static JsonObject ConsoleSnapshot(ConsoleWindowSystem system, bool ansi, Region region)
	{
		var shot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		if (shot == null)
		{
			return new JsonObject
			{
				// Name the switch the caller actually controls, and the way out that
				// needs no configuration at all.
				["error"] = "no composed buffer: this app runs with EnableDiagnostics off, "
					+ "or without the ConsoleBuffer layer. Snapshot a window instead - "
					+ "{\"cmd\":\"snapshot\",\"window\":\"<title>\"} - which reads the "
					+ "live render buffer and needs no diagnostics.",
			};
		}

		var clipped = region.ClampTo(shot.Width, shot.Height, out string? regionError);
		if (regionError != null) return new JsonObject { ["error"] = regionError };

		var o = Grid(clipped, ansi, (x, y) =>
		{
			var cell = shot.GetBack(x, y);
			// ConsoleCell carries the escape the renderer actually emitted, so this
			// reproduces real output rather than re-deriving and possibly disagreeing.
			return (cell.Character.ToString(), cell.AnsiEscape ?? "");
		});

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
