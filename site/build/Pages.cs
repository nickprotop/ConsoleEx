// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace SharpConsoleUI.SiteBuild;

/// <summary>The hand-written pages: home, controls, showcase, docs index.</summary>
/// <remarks>
/// Hand-written because these are the pages where the arrangement IS the
/// content. The guides are generated from markdown; these are not, because
/// there is no markdown that would produce them.
/// </remarks>
internal static class Pages
{
	/// <summary>A screenshot in a terminal frame.</summary>
	private static string Term(string src, string title, string alt) =>
		$"<div class=\"term\"><div class=\"term-bar\">"
		+ "<span class=\"term-dot r\"></span><span class=\"term-dot y\"></span><span class=\"term-dot g\"></span>"
		+ $"<span class=\"term-title\">{Shell.Esc(title)}</span></div>"
		+ $"<img src=\"{src}\" alt=\"{Shell.Esc(alt)}\" loading=\"lazy\"></div>";

	// ── Home ─────────────────────────────────────────────────────────────

	internal static string Home(string root)
	{
		var sb = new StringBuilder();

		sb.AppendLine("<div class=\"hero-wash\"></div>");
		sb.AppendLine("<div class=\"container\">");

		// Hero.
		sb.AppendLine("<section class=\"hero\">");
		sb.AppendLine("<div class=\"hero-text reveal\">");
		// No logo here: it is a wordmark that says the same thing the heading
		// says, and shrunk to hero size it reads as a smudge. It stays in the
		// header, where a mark belongs.
		sb.AppendLine("<span class=\"eyebrow\">SharpConsoleUI</span>");
		sb.AppendLine("<h1>A terminal GUI framework<br>for .NET.</h1>");
		sb.AppendLine("<p class=\"hero-sub\">Overlapping windows with per-cell alpha blending, a "
			+ "Measure&rarr;Arrange&rarr;Paint layout pipeline, and a compositor that only redraws "
			+ "what changed. A single UI thread owns rendering and input; work that must not "
			+ "block it goes on a per-window background task.</p>");
		sb.AppendLine("<div class=\"hero-actions\">");
		sb.AppendLine("<a class=\"btn btn-primary\" href=\"docs/tutorials/README.html\">Get started</a>");
		sb.AppendLine("<a class=\"btn btn-ghost\" href=\"controls.html\">Browse controls</a>");
		sb.AppendLine("<a class=\"btn btn-ghost\" href=\"docs/WHY.html\">Why this one?</a>");
		sb.AppendLine("</div>");
		sb.AppendLine("<p class=\"hero-install\"><code>dotnet add package SharpConsoleUI</code></p>");
		sb.AppendLine("</div>");

		sb.AppendLine("<div class=\"hero-media reveal\">");
		sb.AppendLine(Carousel());
		sb.AppendLine("</div>");
		sb.AppendLine("</section>");

		// Three claims, each with the thing that backs it.
		sb.AppendLine("<section class=\"pillars reveal\">");
		sb.AppendLine(Pillar("A real compositor",
			"Each window owns a cell buffer. The compositor merges them with occlusion culling and "
			+ "per-cell Porter-Duff alpha, so transparency and overlap are a property of the system "
			+ "rather than something each control fakes.",
			"docs/ALPHA_BLENDING.html", "Alpha blending"));
		sb.AppendLine(Pillar("Diff-based output",
			"Rendering goes to a cell buffer, not to stdout. Only changed cells are written, so the "
			+ "output is as clean over SSH as it is locally — latency follows what changed, not the "
			+ "size of the screen.",
			"docs/RENDERING_PIPELINE.html", "Rendering pipeline"));
		sb.AppendLine(Pillar("Inspectable from outside",
			"A running app answers questions over a socket: what the screen shows, what layout "
			+ "believes, why a control is the size it is. Then it takes real keys and clicks.",
			"docs/DEBUGGING.html", "The socket protocol"));
		sb.AppendLine(Pillar("Forty-seven controls",
			"Tables with virtual data sources, trees, charts with two axes, an HTML browser, a "
			+ "terminal emulator. Every one themeable, every one a page of its own.",
			"controls.html", "Browse them"));
		sb.AppendLine(Pillar("Themed by role",
			"A control declares what it IS - Primary, Success, Danger - and the theme decides what "
			+ "that looks like. One choice, coordinated colours, derived per theme.",
			"docs/THEMES.html", "Theming"));
		sb.AppendLine(Pillar("Markup everywhere",
			"Twenty-nine controls parse inline styling: colour, weight, slant, gradients across a "
			+ "run of text, and links that open. Not a separate rich-text widget.",
			"docs/MARKUP_SYNTAX.html", "Markup syntax"));
		sb.AppendLine(Pillar("A UI thread, and a task per window",
			"A single UI thread owns rendering and input; work that must not block it goes on a "
			+ "per-window background task, with marshalling back to the UI thread handled "
			+ "for you. A slow window does not freeze the others.",
			"docs/THREADING_AND_ASYNC.html", "Threading"));
		sb.AppendLine(Pillar("Runs headless",
			"No terminal, no pty. The same application serves a browser, answers a socket, or runs "
			+ "in CI with nothing attached to it.",
			"docs/DRIVERS.html", "Drivers"));
		sb.AppendLine(Pillar("NativeAOT",
			"The full control set publishes as a self-contained native binary - verified on every "
			+ "push by a gate that publishes it and runs it.",
			"docs/AOT.html", "NativeAOT"));
		sb.AppendLine("</section>");

		// The debugger, which is the thing nothing else has.
		sb.AppendLine("<section class=\"feature reveal\">");
		sb.AppendLine("<div class=\"feature-text\">");
		sb.AppendLine("<span class=\"eyebrow\">cxdbg</span>");
		sb.AppendLine("<h2>A terminal app is the one program you cannot printf-debug.</h2>");
		sb.AppendLine("<p>The screen <em>is</em> the output, and a <code>Console.WriteLine</code> corrupts "
			+ "the thing you are trying to read. So the app answers over a socket instead — and "
			+ "<code>cxdbg</code> is the debugger that speaks it.</p>");
		sb.AppendLine("<p>Ask what the layout engine believes, ask which container decided a control's "
			+ "size, then drive the app with real input through its own dispatch.</p>");
		sb.AppendLine("<p class=\"hero-install\"><code>dotnet tool install -g cxdbg</code></p>");
		sb.AppendLine("<p><a href=\"docs/DEBUGGING.html\">The wire protocol &rarr;</a></p>");
		sb.AppendLine("</div>");
		sb.AppendLine("<div class=\"feature-media\">"
			+ Term("images/cxdbg.png", "cxdbg", "cxdbg inspecting a running application") + "</div>");
		sb.AppendLine("</section>");

		// The three controls that have no business existing in a terminal.
		sb.AppendLine("<section class=\"feature reveal\">");
		sb.AppendLine("<div class=\"feature-media\">"
			+ Term("images/controls/HtmlControl.png", "HtmlControl", "A web page rendered in a terminal")
			+ "</div>");
		sb.AppendLine("<div class=\"feature-text\">");
		sb.AppendLine("<span class=\"eyebrow\">Advanced controls</span>");
		sb.AppendLine("<h2>Things a terminal is not supposed to do.</h2>");
		sb.AppendLine("<p><strong>An HTML browser.</strong> AngleSharp parses real markup, a layout "
			+ "engine handles block, inline and table, and images render as half-block pixels. Point "
			+ "it at Wikipedia and it renders Wikipedia.</p>");
		sb.AppendLine("<p><strong>A video player.</strong> FFmpeg decodes, Kitty graphics draw it "
			+ "where the terminal supports them, and half-blocks, ASCII or braille where it does "
			+ "not - picked automatically.</p>");
		sb.AppendLine("<p><strong>A real terminal.</strong> <code>TerminalControl</code> hosts another "
			+ "terminal application inside a window, each with its own pty and full ANSI. A TUI "
			+ "running inside a TUI - on Linux, and on Windows 10 1809 or newer.</p>");
		sb.AppendLine("<p><a href=\"controls.html\">All forty-seven controls &rarr;</a></p>");
		sb.AppendLine("</div>");
		sb.AppendLine("</section>");

		// Built with it.
		sb.AppendLine("<section class=\"reveal\">");
		sb.AppendLine("<div class=\"page-head\"><span class=\"eyebrow\">Built with it</span>"
			+ "<h2>Applications people actually run</h2>"
			+ "<p>Not demos. Dashboards, IDEs, system monitors and database clients, "
			+ "shipped as real tools.</p></div>");
		sb.AppendLine("<div class=\"mini-grid\">");

		foreach (var app in Examples.Read(root, "Real-World Applications").Where(a => a.Hero != null).Take(4))
			sb.AppendLine($"<a class=\"mini\" href=\"showcase.html#{Slug(app.Name)}\">"
				+ $"<img src=\"{app.Hero}\" alt=\"{Shell.Esc(app.Name)}\" loading=\"lazy\">"
				+ $"<div class=\"mini-text\"><h3>{Shell.Esc(app.Name)}</h3>"
				+ $"<p>{Shell.Esc(app.Tagline)}</p></div></a>");

		sb.AppendLine("</div>");
		sb.AppendLine("<p class=\"more\"><a href=\"showcase.html\">See the whole showcase &rarr;</a></p>");
		sb.AppendLine("</section>");

		sb.AppendLine("</div>");

		return sb.ToString();
	}

	private static string Pillar(string title, string body, string href, string link) =>
		$"<div class=\"pillar\"><h3>{Shell.Esc(title)}</h3><p>{body}</p>"
		+ $"<a href=\"{href}\">{Shell.Esc(link)} &rarr;</a></div>";

	private static string Carousel()
	{
		(string Img, string Alt)[] slides =
		[
			// Motion first. A still cannot show a video playing inside a window
			// over an animated desktop background with the rain visible THROUGH
			// it - which is the compositor's whole argument, in one frame. It
			// is also the lightest of the full-motion captures at 3.3 MB.
			("consoleex-matrix-background.gif",
				"A video playing in a window over an animated, alpha-blended desktop"),
			("lazydotide-intellisense.png", "LazyDotIDE — an IDE with IntelliSense, in a terminal"),
			("demoapp-windows.png", "Overlapping windows, dragged and resized"),
			("serverhub-main.png", "ServerHub — a production server dashboard"),
			("cxtop-screenshot.png", "cxtop — a system monitor"),
			("demoapp-markup.png", "Markup, gradients and dialogs"),
		];

		var sb = new StringBuilder();

		sb.AppendLine("<div class=\"term carousel-term\">");
		sb.AppendLine("<div class=\"term-bar\">"
			+ "<span class=\"term-dot r\"></span><span class=\"term-dot y\"></span><span class=\"term-dot g\"></span>"
			+ "<span class=\"term-title\">SharpConsoleUI</span></div>");
		sb.AppendLine("<div class=\"carousel\" id=\"heroCarousel\">");

		for (int i = 0; i < slides.Length; i++)
		{
			string cls = i == 0 ? "carousel-slide active" : "carousel-slide";

			// The first slide is the hero and is fetched eagerly; the rest wait,
			// so a 3.3 MB animation does not queue behind four screenshots
			// nobody has scrolled to yet.
			string load = i == 0 ? "eager\" fetchpriority=\"high" : "lazy";

			sb.AppendLine($"<img class=\"{cls}\" src=\"images/examples/{slides[i].Img}\" "
				+ $"loading=\"{load}\" alt=\"{Shell.Esc(slides[i].Alt)}\">");
		}

		sb.AppendLine("<button type=\"button\" class=\"carousel-nav prev\" "
			+ "aria-label=\"Previous screenshot\">&#8249;</button>");
		sb.AppendLine("<button type=\"button\" class=\"carousel-nav next\" "
			+ "aria-label=\"Next screenshot\">&#8250;</button>");
		sb.AppendLine("</div>");
		sb.AppendLine("<div class=\"carousel-dots\">");

		for (int i = 0; i < slides.Length; i++)
			sb.AppendLine($"<button class=\"carousel-dot{(i == 0 ? " active" : "")}\" data-index=\"{i}\" "
				+ $"type=\"button\" aria-label=\"Screenshot {i + 1}\"></button>");

		sb.AppendLine("</div>");
		sb.AppendLine("</div>");

		return sb.ToString();
	}

	// ── Controls ─────────────────────────────────────────────────────────

	/// <summary>The control whose screenshot stands for a whole group.</summary>
	/// <remarks>
	/// Chosen rather than derived: "which control shows best what this kind of
	/// control is" is a judgement, and the alternatives are alphabetical (a
	/// BarGraph standing for everything that displays) or largest-file, which
	/// picks whichever shot happens to be biggest.
	/// <para>A group with no entry here, or an entry whose screenshot is not
	/// generated, simply shows no picture.</para>
	/// </remarks>
	private static string? Hero(string group) => group switch
	{
		// The catalogue drops the trailing "Controls" from a group's heading, so
		// these are the names as it reports them, not as CONTROLS.md writes them.
		"Basic Input" => "MultilineEditControl",
		"Selection" => "TableControl",
		"Display" => "ChartControl",
		"Drawing" => "CanvasControl",
		"Layout" => "GridControl",
		"Utility" => "TerminalControl",
		_ => null,
	};

	internal static string Controls(string root)
	{
		var sb = new StringBuilder();
		var groups = ControlCatalogue.Read(root);
		int total = groups.Sum(g => g.Rows.Count);

		sb.AppendLine("<div class=\"container page\">");
		sb.AppendLine("<div class=\"page-head\"><span class=\"eyebrow\">Controls</span>"
			+ $"<h1>{(total > 0 ? total : 47)} controls</h1>"
			+ "<p>From a button to an embedded terminal emulator. Every one is themeable, "
			+ "every one has a page of its own, and most of them parse inline markup.</p></div>");

		// One screenshot per group, not per control. A shot of a control is a
		// grid of glyphs, and glyphs have a legibility floor: a status bar is
		// thirty-three columns wide, so in a card-sized thumbnail each
		// character gets about three pixels and the picture is grey noise. The
		// shots also range from 0.9:1 to 6.6:1, so a row of them is either
		// distorted or ragged.
		//
		// At full width one is readable, and one per group is what a reader
		// needs: what this KIND of control looks like. The rest are on their
		// own pages, full size, where someone reading about one will look.
		sb.AppendLine("<p class=\"group-summary\">Each control has a page of its own. "
			+ "For how controls work - common properties, the interfaces, and writing "
			+ "your own - see the <a href=\"docs/CONTROLS.html\">controls reference</a>.</p>");

		// Grouped the way CONTROLS.md groups them, rather than alphabetically:
		// "what kind of control is this" is the question someone browsing has,
		// and a flat list of forty-seven names cannot answer it.
		if (groups.Count > 0)
		{
			foreach (var group in groups)
			{
				sb.AppendLine("<section class=\"doc-group\">");
				sb.AppendLine($"<h2>{Shell.Esc(group.Name)}</h2>");

				if (group.Summary.Length > 0)
					sb.AppendLine($"<p class=\"group-summary\">{Shell.Esc(group.Summary)}</p>");

				if (Hero(group.Name) is { } hero
					&& File.Exists(Path.Combine(root, "docs", "images", "controls", hero + ".png")))
				{
					// Lazy: six screenshots is real weight, and five of them are
					// below the fold.
					sb.AppendLine($"<a class=\"group-shot\" href=\"docs/controls/{hero}.html\">"
						+ $"<img src=\"images/controls/{hero}.png\" alt=\"{hero}\" loading=\"lazy\">"
						+ $"<span>{Shell.Esc(hero)}</span></a>");
				}

				sb.AppendLine("<div class=\"doc-list tight\">");

				foreach (var row in group.Rows)
				{
					string blurb = row.Detail.Length > 0 ? row.Detail : row.What;

					sb.AppendLine($"<a href=\"docs/controls/{row.Name}.html\">"
						+ $"<span class=\"t\">{Shell.Esc(row.Name)}</span>"
						+ $"<span class=\"b\">{Shell.Esc(blurb)}</span></a>");
				}

				sb.AppendLine("</div></section>");
			}

			// Pages the catalogue does not place, so a new control is never
			// invisible because nobody added it to the table.
			var placed = groups.SelectMany(g => g.Rows).Select(r => r.Name)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			var rest = Directory.Exists(Path.Combine(root, "docs", "controls"))
				? Directory.EnumerateFiles(Path.Combine(root, "docs", "controls"), "*.md")
					.Select(Path.GetFileNameWithoutExtension)
					.OfType<string>()
					.Where(n => !placed.Contains(n))
					.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
					.ToList()
				: [];

			if (rest.Count > 0)
			{
				// Only when CONTROLS.md has not caught up with a new control.
				// Titled for what a reader would call it, not for why it is here.
				sb.AppendLine("<section class=\"doc-group\"><h2>Also available</h2>");
				sb.AppendLine("<div class=\"doc-list\">");

				foreach (string n in rest)
					sb.AppendLine($"<a href=\"docs/controls/{n}.html\">"
						+ $"<span class=\"t\">{Shell.Esc(n)}</span></a>");

				sb.AppendLine("</div></section>");
			}
		}

		sb.AppendLine("</div>");

		return sb.ToString();
	}

	// ── Showcase ─────────────────────────────────────────────────────────

	internal static string Showcase(string root)
	{
		var sb = new StringBuilder();

		var apps = Examples.Read(root, "Real-World Applications");
		var demos = Examples.Read(root, "Showcase Examples");

		sb.AppendLine("<div class=\"container page\">");
		sb.AppendLine("<div class=\"page-head\"><span class=\"eyebrow\">Showcase</span>"
			+ "<h1>Built with SharpConsoleUI</h1>"
			+ "<p>Applications people actually run, and the examples in this repository that "
			+ "show how each piece works. Every screenshot is the real thing.</p></div>");

		if (apps.Count > 0)
		{
			sb.AppendLine("<h2 class=\"show-h2\">Real applications</h2>");

			foreach (var app in apps) sb.AppendLine(Entry(app, big: true));
		}

		if (demos.Count > 0)
		{
			sb.AppendLine("<h2 class=\"show-h2\">Examples in this repository</h2>");
			sb.AppendLine("<p class=\"group-summary\">Each one runs with "
				+ "<code>dotnet run --project Examples/&lt;name&gt;</code>.</p>");
			sb.AppendLine("<div class=\"gallery\">");

			foreach (var demo in demos) sb.AppendLine(Entry(demo, big: false));

			sb.AppendLine("</div>");
		}

		sb.AppendLine("<p class=\"more\"><a href=\"docs/EXAMPLES.html\">"
			+ "The full examples guide, with install lines and feature matrix &rarr;</a></p>");
		sb.AppendLine("</div>");

		return sb.ToString();
	}

	/// <summary>One showcase entry.</summary>
	/// <param name="e">What EXAMPLES.md said about it.</param>
	/// <param name="big">
	/// A real application gets a full-width row with its feature list; an
	/// in-repository example gets a card, because there are sixteen of them
	/// and the detail is one click away.
	/// </param>
	private static string Entry(Example e, bool big)
	{
		var sb = new StringBuilder();

		if (!big)
		{
			// The whole card is the link. Every one of these has a section in
			// the examples guide - taglines, what it shows, how to run it - and
			// a card that showed a screenshot and then went nowhere left the
			// reader to find that section by name.
			string href = $"docs/EXAMPLES.html#{e.Anchor}";

			sb.Append($"<a class=\"shot\" href=\"{href}\">");

			if (e.Hero != null) sb.Append(Term(e.Hero, e.Name, e.Name));

			sb.Append("<div><h3>").Append(Shell.Esc(e.Name)).Append("</h3>");
			sb.Append("<p>").Append(Shell.Esc(e.Tagline)).Append("</p></div></a>");

			return sb.ToString();
		}

		sb.AppendLine($"<section id=\"{Slug(e.Name)}\" class=\"show-row reveal\">");

		sb.AppendLine("<div class=\"show-media\">");

		if (e.Hero != null)
		{
			// The hero is swappable: the thumbnails below replace it rather
			// than opening anything. They are different views of one
			// application, so reading them in place is the point - a lightbox
			// would take the reader off the row to show the same thing larger.
			sb.AppendLine("<div class=\"term\"><div class=\"term-bar\">"
				+ "<span class=\"term-dot r\"></span><span class=\"term-dot y\"></span>"
				+ "<span class=\"term-dot g\"></span>"
				+ $"<span class=\"term-title\">{Shell.Esc(e.Name)}</span></div>"
				+ $"<img class=\"show-hero\" src=\"{e.Hero}\" alt=\"{Shell.Esc(e.Name)}\"></div>");
		}

		if (e.More.Count > 0 && e.Hero != null)
		{
			sb.AppendLine("<div class=\"show-more\">");

			// The hero is the first thumbnail, so switching away from it can
			// be switched back.
			sb.AppendLine($"<button type=\"button\" class=\"show-thumb is-on\" "
				+ $"data-src=\"{e.Hero}\" aria-label=\"Main view of {Shell.Esc(e.Name)}\">"
				+ $"<img src=\"{e.Hero}\" alt=\"\" loading=\"lazy\"></button>");

			int n = 2;

			foreach (string m in e.More.Take(3))
			{
				sb.AppendLine($"<button type=\"button\" class=\"show-thumb\" data-src=\"{m}\" "
					+ $"aria-label=\"View {n} of {Shell.Esc(e.Name)}\">"
					+ $"<img src=\"{m}\" alt=\"\" loading=\"lazy\"></button>");
				n++;
			}

			sb.AppendLine("</div>");
		}

		sb.AppendLine("</div>");

		sb.AppendLine("<div class=\"show-text\">");
		sb.AppendLine($"<h3>{Shell.Esc(e.Name)}</h3>");
		sb.AppendLine($"<p class=\"show-tag\">{Shell.Esc(e.Tagline)}</p>");

		foreach (string para in e.Body.Take(1))
			sb.AppendLine($"<p>{Shell.Esc(para)}</p>");

		if (e.Shows.Count > 0)
		{
			sb.AppendLine("<p class=\"show-label\">What it shows off</p>");
			sb.AppendLine("<ul class=\"show-list\">");

			foreach (string f in e.Shows.Take(4))
				sb.AppendLine($"<li>{Shell.Esc(f)}</li>");

			sb.AppendLine("</ul>");
		}

		if (e.Project != null)
			sb.AppendLine($"<p><a class=\"btn btn-ghost\" href=\"{e.Project}\">Project &rarr;</a></p>");

		sb.AppendLine("</div>");
		sb.AppendLine("</section>");

		return sb.ToString();
	}

	// ── Docs index ───────────────────────────────────────────────────────

	internal static string DocsIndex(string root, HashSet<string> placed, HashSet<string> rendered)
	{
		var sb = new StringBuilder();

		// No wrapping container: the caller supplies the doc layout this sits in,
		// and a second one nested inside it indents the page against itself.
		sb.AppendLine("<div class=\"page-head\"><span class=\"eyebrow\">Documentation</span>"
			+ "<h1>Guides and reference</h1>"
			+ "<p>Start with why, then the tutorials. Come back for the reference.</p></div>");

		foreach (var group in Nav.Groups)
		{
			sb.AppendLine("<section class=\"doc-group\">");
			sb.AppendLine($"<h2>{Shell.Esc(group.Name)}</h2>");
			sb.AppendLine($"<p class=\"group-summary\">{Shell.Esc(group.Summary)}</p>");
			sb.AppendLine("<div class=\"doc-list\">");

			foreach (var page in group.Pages)
			{
				string href = Path.ChangeExtension(page.File, ".html");
				sb.AppendLine($"<a href=\"{href}\"><span class=\"t\">{Shell.Esc(page.Title)}</span>"
					+ $"<span class=\"b\">{Shell.Esc(page.Blurb)}</span></a>");
			}

			sb.AppendLine("</div></section>");
		}

		// No "unplaced pages" section. A reader does not care which documents
		// an editor has got round to filing - that is the site's bookkeeping,
		// and putting it on the page makes the page about itself. Nav.cs is
		// where a new guide gets placed; until it is, it is reachable by link
		// and by search, just not listed here.

		return sb.ToString();
	}

	// ── Sidebar, on every rendered doc ───────────────────────────────────

	internal static string Sidebar(string current, int depth) => Sidebar(current, depth, null);

	/// <summary>The navigation beside a rendered page.</summary>
	/// <param name="current">The page being shown, under <c>docs/</c>.</param>
	/// <param name="depth">How deep it sits, for relative links.</param>
	/// <param name="root">The repository, when the page needs its siblings listed.</param>
	internal static string Sidebar(string current, int depth, string? root)
	{
		string up = depth == 0 ? "" : string.Concat(Enumerable.Repeat("../", depth));
		var sb = new StringBuilder();

		// A control's page is not in Nav, so nothing in the sidebar matched it
		// and the reader had no idea where they were. It belongs under the
		// controls catalogue, with its siblings listed beneath.
		bool inControls = current.StartsWith("controls/", StringComparison.OrdinalIgnoreCase);

		sb.AppendLine("<nav class=\"doc-side\">");

		foreach (var group in Nav.Groups)
		{
			sb.AppendLine("<div class=\"grp\">");
			sb.AppendLine($"<div class=\"grp-name\">{Shell.Esc(group.Name)}</div>");

			foreach (var page in group.Pages)
			{
				bool here = string.Equals(page.File, current, StringComparison.OrdinalIgnoreCase);

				bool catalogue = string.Equals(page.File, "CONTROLS.md",
					StringComparison.OrdinalIgnoreCase);

				// The catalogue stands in for whichever control is being read.
				bool stands = inControls && catalogue;

				string href = up + "docs/" + Path.ChangeExtension(page.File, ".html");

				sb.AppendLine($"<a href=\"{href}\"{(here || stands ? " class=\"active\"" : "")}>"
					+ $"{Shell.Esc(page.Title)}</a>");

				// Every control, directly under the catalogue it belongs to, so
				// a reader can move between them without going back up. Listed on
				// the catalogue page as well, not only on a control's own page:
				// the list is how a reader gets from the catalogue to a control,
				// and it was the one place that showed them no way in.
				if ((!stands && !(here && catalogue)) || root == null) continue;

				string dir = Path.Combine(root, "docs", "controls");
				if (!Directory.Exists(dir)) continue;

				sb.AppendLine("<div class=\"grp-sub\">");

				foreach (string f in Directory.EnumerateFiles(dir, "*.md")
					.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
				{
					string name = Path.GetFileNameWithoutExtension(f);
					bool self = string.Equals("controls/" + Path.GetFileName(f), current,
						StringComparison.OrdinalIgnoreCase);

					sb.AppendLine($"<a href=\"{up}docs/controls/{name}.html\""
						+ $"{(self ? " class=\"active\"" : "")}>{Shell.Esc(name)}</a>");
				}

				sb.AppendLine("</div>");
			}

			sb.AppendLine("</div>");
		}

		sb.AppendLine("</nav>");

		return sb.ToString();
	}

	/// <summary>The first sentence of a markdown file, for a blurb.</summary>
	private static string FirstLine(string path)
	{
		bool seenHeading = false;

		foreach (string line in File.ReadLines(path))
		{
			string t = line.Trim();

			if (t.StartsWith("# ", StringComparison.Ordinal)) { seenHeading = true; continue; }
			if (!seenHeading || t.Length == 0) continue;
			if (t.StartsWith('#') || t.StartsWith('!') || t.StartsWith('|')
				|| t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith('>'))
			{
				continue;
			}

			t = System.Text.RegularExpressions.Regex.Replace(t, @"\[([^\]]+)\]\([^)]+\)", "$1");
			t = t.Replace("`", "").Replace("**", "").Replace("*", "");

			return Blurb(t);
		}

		return "";
	}

	/// <summary>An id for an application, so a card can link straight to its row.</summary>
	private static string Slug(string name)
	{
		var sb = new System.Text.StringBuilder(name.Length);

		foreach (char c in name)
		{
			if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
			else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
		}

		return sb.ToString().Trim('-');
	}

	/// <summary>Shortens a blurb at a sentence or word boundary, never mid-word.</summary>
	/// <remarks>
	/// A hard cut at a character count produces "No pri..." and "windows on a",
	/// which reads as a bug rather than a summary. A lead-in like
	/// "What you'll build:" is a label for what follows, not a description, so
	/// the text after it is the blurb.
	/// </remarks>
	private static string Blurb(string text)
	{
		const int Max = 160;

		string t = text.Trim();

		// "What you'll build: a window with a button" -> the part worth showing.
		int lead = t.IndexOf(':');
		if (lead > 0 && lead < 32 && lead + 1 < t.Length)
		{
			string after = t[(lead + 1)..].Trim();
			if (after.Length > 0) t = char.ToUpperInvariant(after[0]) + after[1..];
		}

		if (t.Length <= Max) return t;

		// A whole sentence if one ends in range, otherwise the last whole word.
		int stop = t.LastIndexOfAny(['.', '!', '?'], Math.Min(Max, t.Length) - 1);
		if (stop > Max / 3) return t[..(stop + 1)];

		int space = t.LastIndexOf(' ', Math.Min(Max, t.Length) - 1);
		return (space > 0 ? t[..space] : t[..Max]).TrimEnd(',', ';', ':', '-') + "...";
	}
}
