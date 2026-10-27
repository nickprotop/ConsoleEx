// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace Cxdbg.Commands;

/// <summary>
/// The protocol's verbs, and what each one answers.
/// </summary>
/// <remarks>
/// A list rather than a class each, so adding a verb is one line. The
/// descriptions are cxdbg's own: the server reports names, and a name alone is
/// not documentation. An application can still be asked for its list at
/// runtime with <c>cxdbg commands</c> - that is the authority, and this is the
/// help text.
/// </remarks>
internal static class ProtocolVerbs
{
	/// <summary>
	/// Each verb, what it answers, and an example showing its own options.
	/// </summary>
	/// <remarks>
	/// The example is what <c>cxdbg &lt;verb&gt; --help</c> shows, which is the
	/// only place a reader learns that <c>tree</c> takes a depth or that
	/// <c>color</c> takes a role. The protocol names the fields; this names them
	/// where someone will look.
	/// </remarks>
	internal static readonly (string Verb, string What, string[] Example)[] All =
	[
		("snapshot", "What the screen shows: text, or --format cells", ["snapshot", "--format", "cells", "--y", "7", "--h", "1"]),
		("image", "What the screen looks like, as a PNG", ["image", "--window", "Settings"]),
		("tree", "What layout believes: desired against arranged, per control", ["tree", "--depth", "2", "--props", "true"]),
		("focus", "What currently has focus, and the chain to it", ["focus"]),
		("reachable", "Which controls Tab can reach, in Tab order", ["reachable"]),
		("windows", "The windows, their bounds and their state", ["windows"]),
		("metrics", "Render metrics", ["metrics"]),
		("measure", "Why a control is the size it is", ["measure", "--control", "ChartControl#b679e5"]),
		("color", "Why a role is the colour it is", ["color", "--role", "Danger", "--state", "Disabled"]),
		("invalidations", "Which control asked for the frame", ["invalidations"]),
		("events", "What input the application was told about", ["events"]),
		("logs", "The log buffer the application cannot print itself", ["logs", "--level", "Warning"]),
		("frame", "The frame counter — if it stops, so has the app", ["frame"]),
		("set", "Read the runtime switches, or change one", ["set", "logLevel", "Trace"]),
		("theme", "Read or switch the theme", ["theme"]),
		("window", "Act on a window: activate, close", ["window", "--activate", "Settings"]),
		("size", "Read the screen size, or set it on a headless app", ["size", "--w", "120", "--h", "40"]),
		("key", "Send a key, in tmux notation", ["key", "--spec", "Tab"]),
		("mouse", "Send a mouse event", ["mouse", "--x", "10", "--y", "5", "--action", "click"]),
		("focusctl", "Move focus to a control", ["focusctl", "--name", "okButton"]),
		("invoke", "Activate a control the way a user would", ["invoke", "--name", "okButton"]),
		("ping", "Check that an application is listening", ["ping"]),
		("commands", "Ask the application which verbs it knows", ["commands"]),
	];
}
