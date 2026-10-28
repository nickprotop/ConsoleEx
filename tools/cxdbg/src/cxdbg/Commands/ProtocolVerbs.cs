// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace Cxdbg.Commands;

/// <summary>One field of a verb, and what it is for.</summary>
/// <param name="Name">Its protocol name, which is also its option name.</param>
/// <param name="Takes">What kind of value, or null for a bare flag.</param>
/// <param name="What">What it does.</param>
internal sealed record Field(string Name, string? Takes, string What);

/// <summary>
/// The protocol's verbs, what each answers, and the fields each takes.
/// </summary>
/// <remarks>
/// <para>
/// A list rather than a class each, so adding a verb is one entry. The
/// descriptions are cxdbg's own: the server reports names, and a name alone is
/// not documentation. An application can still be asked for its list at
/// runtime with <c>cxdbg commands</c> - that is the authority, and this is the
/// help text.
/// </para>
/// <para>
/// The fields are what the SERVER reads, checked against its handlers. They
/// are not a gate: a field listed nowhere here still reaches the application,
/// because the protocol decides what exists and a newer app may know more than
/// this build. What they are is the answer to "how do I change the theme",
/// which an example alone never gave.
/// </para>
/// </remarks>
internal static class ProtocolVerbs
{
	/// <summary>Every verb, what it answers, its fields, and an example.</summary>
	internal static readonly (string Verb, string What, Field[] Fields, string[] Example)[] All =
	[
		("snapshot", "What the screen shows: text, cells or markup",
			[
				new("format", "text|cells|markup", "How to render it. Text by default"),
				new("window", "title", "One window instead of the desktop"),
				new("composed", null, "The whole desktop, windows and panels together"),
				new("ansi", null, "Keep the colours, as escape sequences"),
				new("x", "n", "Left edge of a region, with y, w and h"),
				new("y", "n", "Top edge"),
				new("w", "n", "Width"),
				new("h", "n", "Height"),
			],
			["snapshot", "--format", "cells", "--y", "7", "--h", "1"]),

		("image", "What the screen looks like, as a PNG, TGA or JPEG",
			[
				new("path", "file", "Write it there, server-side, instead of returning bytes"),
				new("format", "png|tga|jpeg", "Which encoding. PNG by default"),
				new("quality", "1-100", "For jpeg"),
				new("window", "title", "One window instead of the desktop"),
				new("composed", null, "The whole desktop"),
				new("glyphs", null, "Render real glyphs rather than blocks"),
				new("font", "file", "A font to render with"),
				new("fontSource", "embedded|system|file", "Where to take the font from"),
				new("cellWidth", "n", "Pixels per cell across. From the font by default"),
				new("cellHeight", "n", "Pixels per cell down"),
				new("x", "n", "Left edge of a region, with y, w and h"),
				new("y", "n", "Top edge"),
				new("w", "n", "Width"),
				new("h", "n", "Height"),
				new("sinceFrame", "n", "Answer `unchanged` when the screen has not moved"),
			],
			["image", "--path", "/tmp/shot.png"]),

		("tree", "What layout believes: desired against arranged, per control",
			[
				new("depth", "n", "How deep to walk. Everything by default"),
				new("props", null, "Include each control's layout properties"),
				new("window", "title", "One window instead of every window"),
			],
			["tree", "--depth", "2", "--props"]),

		("desktop", "The screen itself: size, where it starts, what is on it",
			[], ["desktop"]),

		("control", "What one control is: its size, state, and what contains it",
			[
				new("control", "name|id|type", "Which control, as `tree` or `inspect` named it"),
				new("window", "title", "Which window to look in. The active one by default"),
			],
			["control", "--control", "okButton"]),

		("inspect", "What is under a point: window, region, control, cell",
			[
				new("x", "n", "Screen column"),
				new("y", "n", "Screen row"),
			],
			["inspect", "--x", "20", "--y", "10"]),

		("focus", "What currently has focus, and the chain to it", [], ["focus"]),

		("reachable", "Which controls Tab can reach, in Tab order", [], ["reachable"]),

		("windows", "The windows, their bounds and their state", [], ["windows"]),

		("metrics", "Render metrics. Needs `set --diagnostics true`", [], ["metrics"]),

		("measure", "Why a control is the size it is",
			[new("control", "name|id|type", "Which control")],
			["measure", "--control", "ChartControl#b679e5"]),

		("color", "Why a role is the colour it is",
			[
				new("role", "name", "Primary, Danger, Success, and so on"),
				new("state", "Normal|Focused|Disabled", "Which interaction state"),
				new("outline", null, "Resolve it as an outline rather than a fill"),
			],
			["color", "--role", "Danger", "--state", "Disabled"]),

		("invalidations", "Which control asked for the frame. Needs diagnostics on",
			[], ["invalidations"]),

		("events", "What input the application was told about. Needs diagnostics on",
			[], ["events"]),

		("logs", "The log buffer the application cannot print itself",
			[
				new("level", "Trace|Debug|Information|Warning|Error", "Lowest level to show"),
				new("count", "n", "How many entries, most recent last"),
				new("category", "name", "Only one category: Focus, Render, Input, …"),
				new("contains", "text", "Only entries whose message contains this"),
			],
			["logs", "--level", "Warning", "--count", "50"]),

		("frame", "The frame counter - if it stops, so has the app", [], ["frame"]),

		("settled", "Whether the app has finished reacting, and what is holding it",
			[], ["settled"]),

		("set", "Read the runtime switches, or change one",
			[
				new("diagnostics", "true|false", "Render recording and the input tape"),
				new("logging", "true|false", "Log capture"),
				new("logLevel", "Trace|Debug|Information|Warning|Error", "Lowest level kept"),
				new("clearLogs", "true", "Empty the log buffer"),
			],
			["set", "--diagnostics", "true"]),

		("theme", "Read the themes, or switch to one",
			[new("name", "theme", "Switch to it. Omit to list what there is")],
			["theme", "--name", "Ocean"]),

		("window", "Act on a window: activate, close",
			[
				new("activate", "title", "Bring that window to the front"),
				new("close", "title", "Close it"),
				new("window", "title", "Which window, when the action does not name one"),
			],
			["window", "--activate", "Settings"]),

		("size", "Read the screen size, or set it on a headless app",
			[
				new("w", "n", "New width"),
				new("h", "n", "New height"),
			],
			["size", "--w", "120", "--h", "40"]),

		("key", "Send a key, in tmux notation",
			[new("spec", "key", "Tab, Down, C-c, S-Tab, or literal text")],
			["key", "--spec", "Tab"]),

		("mouse", "Send a mouse event",
			[
				new("x", "n", "Screen column"),
				new("y", "n", "Screen row"),
				new("action", "click|doubleclick|press|release|move|drag|wheelup|wheeldown",
					"What to do there"),
				new("button", "left|right|middle", "Which button. Left by default"),
			],
			["mouse", "--x", "10", "--y", "5", "--action", "click"]),

		("focusctl", "Move focus to a control",
			[
				new("name", "name|id", "Which control"),
				new("window", "title", "Which window. The active one by default"),
			],
			["focusctl", "--name", "okButton"]),

		("invoke", "Activate a control the way a user would",
			[
				new("name", "name|id", "Which control"),
				new("window", "title", "Which window. The active one by default"),
			],
			["invoke", "--name", "okButton"]),

		("ping", "Check that an application is listening", [], ["ping"]),

		("commands", "Ask the application which verbs it knows", [], ["commands"]),
	];
}
