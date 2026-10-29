// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using SharpConsoleUI.SiteBuild;

// Builds the website into _site/. Run from the repository root, or pass it:
//
//     dotnet run --project site/build -- [repo-root] [output]
//
// The markdown under docs/ is INPUT and is never written to: it stays readable
// on GitHub, and every link that works there keeps working here.

string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
string outDir = args.Length > 1 ? args[1] : Path.Combine(root, "_site");

if (!Directory.Exists(Path.Combine(root, "docs")))
{
	Console.Error.WriteLine($"no docs/ under '{root}'. Run from the repository root.");
	return 1;
}

var pipeline = new MarkdownPipelineBuilder()
	.UseAdvancedExtensions()
	.UsePipeTables()
	.UseAutoLinks()
	.Build();

if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
Directory.CreateDirectory(outDir);

int pages = 0;

// ── Assets ───────────────────────────────────────────────────────────────

Copy(Path.Combine(root, "site", "styles"), Path.Combine(outDir, "styles"));
Copy(Path.Combine(root, "docs", "images"), Path.Combine(outDir, "images"));

// The API reference keeps its own template and its own URL. DocFX builds it
// before this runs; when it has not, the site is still complete apart from
// the API links, which is the right failure for a local preview.
string docfx = Path.Combine(root, "docs", "docfx", "_site");

if (Directory.Exists(docfx))
{
	Copy(docfx, Path.Combine(outDir, "api"));
}
else
{
	Console.WriteLine("  note: docs/docfx/_site not found - API reference not included.");
}

// ── Rendered documentation ───────────────────────────────────────────────

var placed = Nav.Placed();
var rendered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var group in Nav.Groups)
{
	foreach (var page in group.Pages)
	{
		RenderDoc(page.File, page.Title, page.Source);
		rendered.Add(page.File);
	}
}

// Everything else under docs/, so a guide is never invisible because nobody
// placed it. Control pages and tutorials come along the same way.
foreach (string md in Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories))
{
	string rel = Path.GetRelativePath(Path.Combine(root, "docs"), md).Replace('\\', '/');

	// Internal working notes are gitignored, but a local build would still
	// find them. They are not part of the site.
	if (rel.StartsWith("superpowers/", StringComparison.OrdinalIgnoreCase)) continue;

	// DocFX's own source. It builds this into _site/api/ itself, with its own
	// template and its own relative links; rendering it a second time here
	// produced a duplicate page whose api/*.html links resolved against the
	// wrong directory - every broken link on the site came from this one file.
	if (rel.StartsWith("docfx/", StringComparison.OrdinalIgnoreCase)) continue;
	if (rendered.Contains(rel)) continue;

	RenderDoc(rel, TitleFrom(md, rel));
	rendered.Add(rel);
}

// ── Hand-written pages ───────────────────────────────────────────────────

Write("index.html", Shell.Render(
	"A terminal GUI framework for .NET",
	"Overlapping windows with per-cell alpha blending, a Measure-Arrange-Paint layout pipeline, "
	+ "and a compositor that only redraws what changed.",
	Pages.Home(root), active: null, depth: 0));

Write("controls.html", Shell.Render(
	"Controls",
	"Forty-seven controls, from a button to an embedded terminal emulator.",
	Pages.Controls(root), active: "controls", depth: 0));

Write("showcase.html", Shell.Render(
	"Showcase",
	"Real applications built with SharpConsoleUI: IDEs, dashboards, system monitors.",
	Pages.Showcase(root), active: "showcase", depth: 0));

// The index gets the same sidebar as the pages it lists. Without it, the one
// page whose whole job is to show the shape of the documentation was also the
// one page that did not show it, and arriving here lost the map.
Write("docs/index.html", Shell.Render(
	"Documentation",
	"Guides, references and tutorials for SharpConsoleUI.",
	"<div class=\"container page\"><div class=\"doc-layout\">"
	+ Pages.Sidebar("index.md", 1, root)
	+ "<article class=\"prose\">" + Pages.DocsIndex(root, placed, rendered) + "</article>"
	+ "</div></div>",
	active: "docs", depth: 1));

Console.WriteLine($"built {pages} pages into {outDir}");
return 0;

// ── Helpers ──────────────────────────────────────────────────────────────

/// <summary>Renders one markdown file as a documentation page.</summary>
/// <param name="rel">Where the page goes under docs/, and what links resolve against.</param>
/// <param name="title">Its title.</param>
/// <param name="from">
/// Where to read it, relative to the repository, when that is not
/// <c>docs/{rel}</c>. A tool's README stays in the tool's folder - it is what
/// the repository shows and what NuGet packs - and is rendered here so the
/// site has it too, rather than being copied into docs/ and left to drift.
/// </param>
void RenderDoc(string rel, string title, string? from = null)
{
	string src = from != null ? Path.Combine(root, from) : Path.Combine(root, "docs", rel);
	if (!File.Exists(src)) { Console.Error.WriteLine($"  missing: {from ?? "docs/" + rel}"); return; }

	string md = File.ReadAllText(src);
	string html = Markdown.ToHtml(md, pipeline);

	// Depth of the OUTPUT page, which decides how far back ../ has to go.
	string outRel = "docs/" + Path.ChangeExtension(rel, ".html");
	int depth = outRel.Count(c => c == '/');

	// Relative links in a README were written against the tool's folder, so
	// they resolve against it: expressed relative to docs/, which is the space
	// the rewriter works in, that is "../tools/cxdbg" for tools/cxdbg.
	string? sourceDir = from == null
		? null
		: Links.Normalize("../" + (Path.GetDirectoryName(from)?.Replace('\\', '/') ?? ""));

	html = Links.Rewrite(html, rel, depth, sourceDir);

	html = Callouts.Apply(html);

	html = Shots.Frame(html);

	// Each table gets a scroll container of its own, so one genuinely wide
	// table can scroll without every table being laid out as a block.
	html = html.Replace("<table>", "<div class=\"table-scroll\"><table>", StringComparison.Ordinal)
		.Replace("</table>", "</table></div>", StringComparison.Ordinal);

	// Built before the anchors go in, so the heading text in the list is the
	// heading's words rather than its words plus a hash.
	string toc = Toc.Render(html);

	html = Toc.Anchor(html);

	string body =
		"<div class=\"container page\"><div class=\"doc-layout"
		+ (toc.Length > 0 ? " has-toc" : "") + "\">"
		+ Pages.Sidebar(rel, depth, root)
		+ "<article class=\"prose\">" + html + "</article>"
		+ toc
		+ "</div></div>";

	Write(outRel, Shell.Render(title, Summary(md, title), body, "docs", depth));
}

void Write(string rel, string html)
{
	string path = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
	Directory.CreateDirectory(Path.GetDirectoryName(path)!);
	File.WriteAllText(path, html);
	pages++;
}

static void Copy(string from, string to)
{
	if (!Directory.Exists(from)) return;

	foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
	{
		string dest = Path.Combine(to, Path.GetRelativePath(from, file));
		Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
		File.Copy(file, dest, overwrite: true);
	}
}

/// <summary>A page's title: its first heading, or its filename.</summary>
static string TitleFrom(string path, string rel)
{
	foreach (string line in File.ReadLines(path))
	{
		if (line.StartsWith("# ", StringComparison.Ordinal)) return line[2..].Trim();
	}

	return Path.GetFileNameWithoutExtension(rel).Replace('_', ' ').Replace('-', ' ');
}

/// <summary>The first real sentence, for the description meta tag.</summary>
static string Summary(string md, string fallback)
{
	foreach (string line in md.Split('\n'))
	{
		string t = line.Trim();

		if (t.Length == 0 || t.StartsWith('#') || t.StartsWith('!') || t.StartsWith('[')
			|| t.StartsWith('|') || t.StartsWith("```", StringComparison.Ordinal)
			|| t.StartsWith('>') || t.StartsWith('-'))
		{
			continue;
		}

		// Strip the inline markup a meta description should not carry.
		t = Regex.Replace(t, @"\[([^\]]+)\]\([^)]+\)", "$1");
		t = t.Replace("`", "").Replace("**", "").Replace("*", "");

		return Trim(t);
	}

	return fallback + " — SharpConsoleUI documentation.";
}

/// <summary>Cuts a meta description at a sentence or word boundary.</summary>
/// <remarks>
/// A hard cut at a character count ends a shared link mid-word ("No pri..."),
/// and a lead-in like "What you'll build:" labels what follows rather than
/// describing the page, so the text after it is the description.
/// </remarks>
static string Trim(string text)
{
	const int Max = 180;

	string t = text.Trim();

	int lead = t.IndexOf(':');
	if (lead > 0 && lead < 32 && lead + 1 < t.Length)
	{
		string after = t[(lead + 1)..].Trim();
		if (after.Length > 0) t = char.ToUpperInvariant(after[0]) + after[1..];
	}

	if (t.Length <= Max) return t;

	int stop = t.LastIndexOfAny(['.', '!', '?'], Math.Min(Max, t.Length) - 1);
	if (stop > Max / 3) return t[..(stop + 1)];

	int space = t.LastIndexOf(' ', Math.Min(Max, t.Length) - 1);
	return (space > 0 ? t[..space] : t[..Max]).TrimEnd(',', ';', ':', '-') + "...";
}
