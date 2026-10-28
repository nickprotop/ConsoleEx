// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.SiteBuild;

/// <summary>One documentation page in the navigation.</summary>
/// <param name="File">Its markdown file, relative to <c>docs/</c>.</param>
/// <param name="Title">What to call it in the navigation.</param>
/// <param name="Blurb">One line saying what it covers, for the docs index.</param>
internal sealed record Page(string File, string Title, string Blurb);

/// <summary>A named group of pages.</summary>
/// <param name="Name">The group's heading.</param>
/// <param name="Summary">One line saying what the group is for.</param>
/// <param name="Pages">Its pages, in reading order.</param>
internal sealed record Group(string Name, string Summary, Page[] Pages);

/// <summary>
/// The documentation, in six groups.
/// </summary>
/// <remarks>
/// <para>
/// Written out rather than discovered from the filesystem. A directory listing
/// gives alphabetical order and no hierarchy, which is exactly the wall of forty
/// links this replaces: the ordering and the grouping ARE the content, and
/// neither can be derived from a filename.
/// </para>
/// <para>
/// A file that is not listed here is still rendered, and appears under "More"
/// on the docs index - so adding a guide never makes it invisible, it only
/// makes it unsorted until someone places it.
/// </para>
/// </remarks>
internal static class Nav
{
	internal static readonly Group[] Groups =
	[
		new("Getting started",
			"Install it, open a window, and learn the shape of the API.",
			[
				new("tutorials/README.md", "Tutorials", "Step by step, from an empty project to a running application."),
				new("BUILDERS.md", "Builder API", "The fluent API: windows, controls, and how they compose."),
				new("CONFIGURATION.md", "Configuration", "ConsoleWindowSystemOptions and what each switch changes."),
				new("EXAMPLES.md", "Examples", "The example applications in this repository, and what each one shows."),
				new("PATTERNS.md", "Patterns", "Shapes that recur: dialogs, long work, live data."),
				new("WHY.md", "Why SharpConsoleUI?", "What it is, what it is not, and the three shapes an application can take."),
			]),

		new("Controls",
			"Forty-seven controls, from a button to an embedded terminal.",
			[
				new("CONTROLS.md", "All controls", "The full catalogue, with what each one is for."),
				new("PANELS.md", "Panels", "Grouping, borders, collapsing."),
				new("DIALOGS.md", "Dialogs", "Modal windows, file pickers, prompts."),
				new("FLOWS.md", "Composable flows", "Multi-step interactions built from controls."),
				new("FORM_XML.md", "Form XML", "Declaring a form rather than assembling one."),
				new("BINDING.md", "Data binding", "Bind a control to a value and let it track."),
			]),

		new("Layout",
			"How a control decides its size, and where the window puts it.",
			[
				new("DOM_LAYOUT_SYSTEM.md", "DOM layout", "Measure, Arrange, Paint - the pipeline every control goes through."),
				new("WINDOW_LAYOUT.md", "Window layout", "Arranging controls inside a window."),
				new("WINDOWS.md", "Windows", "Creating, moving, stacking, and closing them."),
				new("DOCKING.md", "Docking", "Edges, fills, and the body that holds the rest."),
				new("PORTAL_SYSTEM.md", "Portals", "Drawing outside the window that owns the control."),
			]),

		new("Rendering & theming",
			"Colour, compositing, and everything that reaches a cell.",
			[
				new("THEMES.md", "Theming", "Themes, palettes, and control roles."),
				new("MARKUP_SYNTAX.md", "Markup syntax", "Inline styling, understood by twenty-nine controls."),
				new("GRADIENTS.md", "Gradients", "Along a plot, by value, or both."),
				new("ALPHA_BLENDING.md", "Alpha blending", "Per-cell Porter-Duff compositing."),
				new("COMPOSITOR_EFFECTS.md", "Compositor effects", "Blur, fade, and hooks of your own."),
				new("ANIMATIONS.md", "Animations", "Moving things without tearing the frame."),
				new("DESKTOP_BACKGROUND.md", "Desktop background", "What sits behind every window."),
				new("SYNTAX_HIGHLIGHTING.md", "Syntax highlighting", "TextMate grammars, coloured by your theme."),
				new("IMAGE_RENDERING.md", "Image rendering", "Kitty graphics, with cell-based fallbacks."),
				new("VIDEO_PLAYBACK.md", "Video playback", "Frames in a terminal, via FFmpeg."),
				new("RENDERING_PIPELINE.md", "Rendering pipeline", "From a dirty cell to bytes on the wire."),
			]),

		new("Tooling & debugging",
			"A terminal application is the one program you cannot printf-debug.",
			[
				new("DEBUGGING.md", "Debugging & the socket protocol", "The wire: twenty-six verbs, what each answers."),
				new("DRIVERS.md", "Drivers & render modes", "Real terminal, headless, or one of your own."),
				new("SHELL_SCRIPTING.md", "Shell scripting", "Driving an application from a script."),
				new("SCHOST.md", "schost", "Packaging an application as a desktop app."),
				new("EXPORT.md", "Screen export", "Capturing what is on screen."),
				new("AOT.md", "NativeAOT", "Publishing as a self-contained native binary."),
			]),

		new("Reference",
			"The parts you reach for once you are building something real.",
			[
				new("STATE-SERVICES.md", "State services", "Where window, focus, modal and theme state lives."),
				new("THREADING_AND_ASYNC.md", "Threading & async", "The UI thread, and how to get work off it."),
				new("STATUS_SYSTEM.md", "Status system", "Status bars and what they report."),
				new("NOTIFICATIONS.md", "Notifications", "Transient messages, and when they dismiss."),
				new("CLIPBOARD.md", "Clipboard", "Copy and paste, across platforms."),
				new("REGISTRY.md", "Registry", "Themes and plugins, per instance."),
				new("PLUGINS.md", "Plugins", "Extending the system at runtime."),
				new("CODE_QUALITY.md", "Code quality", "The rules this codebase holds itself to."),
				new("CODE_QUALITY_ENFORCEMENT.md", "Quality enforcement", "How those rules are checked, and what blocks a merge."),
			]),
	];

	/// <summary>Every file the navigation places, for finding the ones it does not.</summary>
	internal static HashSet<string> Placed()
	{
		var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var group in Groups)
		{
			foreach (var page in group.Pages) placed.Add(page.File);
		}

		return placed;
	}
}
