# Markup Syntax Reference

> **New to SharpConsoleUI?** Start with the [Tutorials](tutorials/README.md) — markup is introduced in Tutorial 1.

SharpConsoleUI includes a native markup parser that uses Spectre-compatible `[tag]text[/]` syntax. All markup is parsed directly into typed `Cell` structs -- no ANSI intermediate format, no external dependencies required.

## Basic Syntax

```
[style]text[/]
```

- Opening tag: `[style]` where `style` is any combination of color names, hex colors, RGB values, and text decorations
- Closing tag: `[/]` closes the most recent opening tag
- Tags can be nested; closing `[/]` pops the innermost style

```csharp
"[bold red]Error:[/] Something went wrong"
"[green]Success:[/] Operation completed"
"[bold yellow underline]Important notice[/]"
```

## Colors

### Named Colors (Basic 16)

| Color | RGB | Color | RGB |
|-------|-----|-------|-----|
| `black` | (0, 0, 0) | `silver` | (192, 192, 192) |
| `maroon` | (128, 0, 0) | `grey` / `gray` | (128, 128, 128) |
| `green` | (0, 128, 0) | `red` | (255, 0, 0) |
| `olive` | (128, 128, 0) | `lime` | (0, 255, 0) |
| `navy` | (0, 0, 128) | `yellow` | (255, 255, 0) |
| `purple` | (128, 0, 128) | `blue` | (0, 0, 255) |
| `teal` | (0, 128, 128) | `fuchsia` / `magenta` | (255, 0, 255) |
| `white` | (255, 255, 255) | `aqua` / `cyan` | (0, 255, 255) |

### Aliases

| Alias | Maps To |
|-------|---------|
| `darkred` | maroon |
| `darkgreen` | green |
| `darkyellow` | olive |
| `darkblue` | navy |
| `darkmagenta` | purple |
| `darkcyan` | teal |

### Extended Colors

| Color | RGB |
|-------|-----|
| `orange1` | (255, 175, 0) |
| `orange3` | (205, 133, 0) |
| `darkorange` | (255, 140, 0) |
| `coral` | (255, 127, 80) |
| `indianred` | (205, 92, 92) |
| `hotpink` | (255, 105, 180) |
| `deeppink1` | (255, 20, 147) |
| `mediumorchid` | (186, 85, 211) |
| `darkviolet` | (148, 0, 211) |
| `blueviolet` | (138, 43, 226) |
| `royalblue1` | (65, 105, 225) |
| `cornflowerblue` | (100, 149, 237) |
| `dodgerblue1` | (30, 144, 255) |
| `dodgerblue2` | (28, 134, 238) |
| `deepskyblue1` | (0, 191, 255) |
| `steelblue` | (70, 130, 180) |
| `cadetblue` | (95, 158, 160) |
| `mediumturquoise` | (72, 209, 204) |
| `darkturquoise` | (0, 206, 209) |
| `lightseagreen` | (32, 178, 170) |
| `mediumspringgreen` | (0, 250, 154) |
| `springgreen1` | (0, 255, 127) |
| `springgreen2` | (0, 238, 118) |
| `chartreuse1` | (127, 255, 0) |
| `chartreuse2` | (118, 238, 0) |
| `greenyellow` | (173, 255, 47) |
| `lightgreen` | (144, 238, 144) |
| `palegreen1` | (152, 251, 152) |
| `darkseagreen` | (143, 188, 143) |
| `mediumseagreen` | (60, 179, 113) |
| `lightcoral` | (240, 128, 128) |
| `salmon1` | (250, 128, 114) |
| `sandybrown` | (244, 164, 96) |
| `gold1` | (255, 215, 0) |
| `gold3` | (205, 173, 0) |
| `violet` | (238, 130, 238) |
| `orchid` | (218, 112, 214) |
| `plum1` | (221, 160, 221) |
| `magenta1` | (255, 0, 255) |
| `darkolivegreen1` | (202, 255, 112) |
| `khaki1` | (240, 230, 140) |
| `darkkhaki` | (189, 183, 107) |
| `darkgoldenrod` | (184, 134, 11) |
| `wheat1` | (245, 222, 179) |
| `navajowhite1` | (255, 222, 173) |
| `mistyrose1` | (255, 228, 225) |
| `lightsalmon1` | (255, 160, 122) |
| `lightpink1` | (255, 182, 193) |
| `pink1` | (255, 192, 203) |
| `thistle1` | (216, 191, 216) |
| `tan` | (210, 180, 140) |
| `rosybrown` | (188, 143, 143) |
| `palevioletred` | (219, 112, 147) |
| `mediumvioletred` | (199, 21, 133) |
| `mediumpurple` | (147, 112, 219) |
| `mediumslateblue` | (123, 104, 238) |
| `slateblue1` | (106, 90, 205) |
| `lightsteelblue` | (176, 196, 222) |
| `lightblue` | (173, 216, 230) |
| `lightcyan1` | (224, 255, 255) |
| `paleturquoise1` | (175, 238, 238) |
| `lightskyblue1` | (135, 206, 250) |
| `skyblue1` | (135, 206, 235) |
| `lightslategrey` | (119, 136, 153) |
| `honeydew2` | (240, 255, 240) |
| `lightgoldenrodyellow` | (250, 250, 210) |
| `lightyellow1` | (255, 255, 224) |
| `darkslategray1` | (47, 79, 79) |
| `darkslategray3` | (95, 135, 135) |
| `cyan1` | (0, 255, 255) |

### Grey Scale

`grey0` through `grey100` (0 = black, 100 = white). Examples:

| Color | RGB |
|-------|-----|
| `grey0` | (0, 0, 0) |
| `grey15` | (38, 38, 38) |
| `grey50` | (128, 128, 128) |
| `grey85` | (218, 218, 218) |
| `grey93` | (238, 238, 238) |
| `grey100` | (255, 255, 255) |

### Hex Colors

```
[#RRGGBB]text[/]
[#RGB]text[/]
[#RRGGBBAA]text[/]
```

```csharp
"[#FF8000]Orange text[/]"
"[#F80]Short hex orange[/]"
"[#336699]Steel blue text[/]"
"[#00DCDC80]Semi-transparent cyan[/]"   // 50 % opacity
"[#FF000000]Fully transparent red[/]"   // invisible — composites to background
```

The 8-digit form (`#RRGGBBAA`) sets the foreground alpha. The character is composited over the resolved background color of that cell using Porter-Duff "over". At `AA=00` the glyph is invisible (background shows through); at `AA=FF` it is fully opaque. This is what powers the fade-to-transparent effect in the Alpha Blending demo — `█` characters drawn with decreasing alpha dissolve smoothly into whatever gradient is underneath.

### RGB Colors

```
[rgb(r,g,b)]text[/]
```

```csharp
"[rgb(255,128,0)]Orange text[/]"
"[rgb(100,200,50)]Custom green[/]"
```

## Background Colors

Use `on` to set the background color:

```
[foreground on background]text[/]
[on background]text[/]
```

```csharp
"[white on red]Error banner[/]"
"[bold cyan on blue]Header[/]"
"[on green] Status OK [/]"
```

### Fill Background to End of Line

By default a background only colors the characters it covers, so a short line leaves a ragged
right edge. The self-closing `[fillwidth]` marker tells the renderer to extend the line's
trailing background all the way to the available width — turning a per-line tint into a solid
block (used internally by Markdown fenced code blocks, but available to any markup).

```
[on grey19] code line [/][fillwidth]
```

- `[fillwidth]` is **self-closing** — it has no `[/]` and produces no visible character.
- Place it at the **end of a line**; it flags that line so the renderer fills the remainder of
  the row with the line's last background color.
- It only affects layout-aware hosts that paint a full row (e.g. `MarkupControl`); in plain
  parsing it is simply a no-op that emits nothing.
- Escape with double brackets — `[[fillwidth]]` renders the literal text `[fillwidth]`.

```csharp
// A full-width shaded banner, regardless of text length:
"[on grey19] Build succeeded [/][fillwidth]"
```

## Text Decorations

| Decoration | Aliases | Description |
|------------|---------|-------------|
| `bold` | | Bold/bright text |
| `dim` | | Dimmed/faint text |
| `italic` | | Italic text |
| `underline` | | Underlined text |
| `strikethrough` | `strike` | Strikethrough text |
| `invert` | `reverse` | Swap foreground/background |
| `blink` | `slowblink`, `rapidblink` | Blinking text |

```csharp
"[bold]Bold text[/]"
"[italic]Italic text[/]"
"[underline]Underlined text[/]"
"[dim]Dimmed text[/]"
"[strikethrough]Deleted text[/]"
"[invert]Inverted colors[/]"
```

## Spinner (animated)

Embed an animated spinner glyph inline in any markup text. It animates wherever markup is rendered — labels, status bars, titles, table cells, tree nodes — with no separate control.

```
[yellow]Saving [spinner][/]
[spinner circle] connecting...
[red]Failed [spinner dots][/]
```

| Tag | Style |
|-----|-------|
| `[spinner]` | Braille (default) |
| `[spinner braille]` | Braille |
| `[spinner circle]` | Quarter-circle rotation |
| `[spinner dots]` | ASCII dots (`.` / `..` / `...`) |
| `[spinner line]` | ASCII `- \ | /` |
| `[spinner arc]` | Arc rotation |
| `[spinner bounce]` | Bouncing braille dot |
| `[spinner star]` | Twinkling star ✶✸✹✺ |
| `[spinner growvertical]` | Pulsing vertical bar ▁▃▄▅▆▇ |
| `[spinner growhorizontal]` | Pulsing horizontal bar ▏▎▍▌▋▊▉ |
| `[spinner toggle]` | Empty/filled square blink □■ |
| `[spinner arrow]` | Rotating arrow ←↑→↓ |
| `[spinner bouncingbar]` | ASCII bouncing bar `[==  ]` |
| `[spinner aestheticbar]` | Progress bar ▰▰▰▱▱▱ |
| `[spinner brailledots]` | Classic braille throbber ⠋⠙⠹⠸ |
| `[spinner dotsbounce]` | Bouncing ASCII dots `.  ` → `...` → ` ..` |

**Speed:** each style animates at a sensible per-style default. Override inline with a trailing millisecond value — `[spinner dots 250]`. A missing or invalid value falls back to the style default; the interval never affects the reserved width.

The glyph inherits the surrounding color scope (`[yellow][spinner][/]` is yellow). A spinner reserves a fixed column width per style, so surrounding text never reflows as it animates. Animation requires a running `ConsoleWindowSystem` with animations enabled; when parsed without one (e.g. in tests) it renders a static glyph. Escape with double brackets — `[[spinner]]` renders the literal text `[spinner]`.

For a standalone, placeable spinner control (rather than inline text), see [SpinnerControl](controls/SpinnerControl.md).

## Markdown

The `[markdown]…[/]` tag parses its inner content as **Markdown** (via [Markdig](https://github.com/xoofx/markdig)) and renders it as native markup. Because it is just a markup tag, it works anywhere markup is accepted — labels, status bars, table cells, tree nodes — and can be mixed with ordinary markup in the same string.

```
[markdown]# Heading

**Bold**, *italic*, `code`, and a list:

- one
- two
[/]
```

### Supported Constructs

| Construct | Notes |
|-----------|-------|
| Headings | `#` through `######` (H1–H6) |
| Emphasis | bold, italic, bold+italic, strikethrough |
| Inline code | `` `code` `` |
| Links | `[text](url)` — text is shown, URL is dropped |
| Lists | bullet, numbered, and nested |
| Blockquotes | `> quoted` with a vertical bar glyph |
| Horizontal rules | `---` |
| Code blocks | fenced (```` ``` ````) and indented |
| Tables | GitHub-style pipe tables, rendered with box-drawing borders |

**Copied text stays plain.** Markdown is rendered down to native markup, so selecting and copying (`Ctrl+C`) from a `MarkupControl` yields plain text with the markup stripped — exactly as with any other markup.

### Region Behavior

- **Non-nesting:** a `[markdown]` region ends at the *first* `[/]`. It does not nest, so a `[/]` inside the Markdown content closes the region rather than popping an inner tag.
- **Unclosed:** a `[markdown]` with no matching `[/]` renders everything after the tag, to the end of the string, as Markdown.
- **Escaped:** `[[markdown]]` (doubled brackets) renders the literal text `[markdown]` instead of opening a region.

### Syntax Highlighting in Code Blocks

Fenced code blocks with a **language hint** are automatically syntax-highlighted using SharpConsoleUI's built-in highlighters. The hint is the text immediately after the opening fence:

````
```csharp
var control = Controls.Markdown("# Report").Build();
```
````

The following languages (and aliases) ship with the library:

| Language | Aliases |
|----------|---------|
| C# | `csharp`, `cs` |
| Bash | `bash`, `sh`, `shell`, `zsh` |
| JSON | `json` |
| JavaScript | `javascript`, `js`, `node`, `mjs`, `cjs` |
| CSS | `css` |
| HTML | `html`, `htm` |
| XML | `xml` |
| YAML | `yaml`, `yml` |
| Razor | `razor`, `cshtml` |
| Dockerfile | `dockerfile`, `docker` |
| Solution | `sln` |
| Diff | `diff`, `patch` |
| Markdown | `markdown`, `md` |

A fenced block with **no language hint** — or one whose hint matches no registered highlighter — falls back to a flat, shaded code block (no token coloring).

> The same highlighters power `MultilineEditControl`. See the [Syntax Highlighting](SYNTAX_HIGHLIGHTING.md) guide for the registry, the full list of built-ins, and how to register your own.

**Custom highlighters.** Register a highlighter **globally** so it applies everywhere Markdown is rendered:

```csharp
using SharpConsoleUI.Highlighting;

SyntaxHighlighters.Register("toml", new MyTomlHighlighter());
```

Or override **per style** via `MarkdownStyle.CodeHighlighters` (keyed by language hint), which is consulted before the global registry:

```csharp
var control = Controls.Markdown(markdown)
    .WithMarkdownStyle(s => s with
    {
        CodeHighlighters = new Dictionary<string, ISyntaxHighlighter>
        {
            ["csharp"] = new MyCustomCSharpHighlighter()
        }
    })
    .Build();
```

**Precedence** for a given language hint: per-style `CodeHighlighters` override → global `SyntaxHighlighters` registry → flat shaded block.

### Styling — `MarkdownStyle`

Markdown is *structural*: emphasis and headings emit colorless tags that inherit the surrounding color scope, so only the "chrome" components (code, quotes, links, table borders) carry colors. Styling is controlled by the `SharpConsoleUI.Configuration.MarkdownStyle` record. This is intentionally **not** part of the global theme — the parser is static and theme-agnostic.

| Property | Purpose |
|----------|---------|
| `CodeForeground` / `CodeBackground` | Inline code and code blocks |
| `QuoteColor` | Blockquote text and the quote bar glyph |
| `LinkColor` | Link text |
| `BorderColor` | Table border (box-drawing) characters |
| `TableRowSeparators` | `false` (default): a table draws a rule under the header only, like GitHub. `true`: draws a rule between every body row (full spreadsheet-style grid) |
| `BulletGlyph` | Bullet list marker (default `•`) |
| `ListIndent` | Spaces of indentation per nested level (default `2`) |
| `QuoteGlyph` | Blockquote vertical bar (default `│`) |
| `H1Color` … `H6Color` | Optional per-heading color; `null` = colorless (structural weight only) |

Table rows are grouped under the header by default (the compact Markdown look). For a fully gridded
table, opt in to a rule between every body row:

```csharp
var control = Controls.Markdown("| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |")
    .WithMarkdownStyle(s => s with { TableRowSeparators = true })
    .Build();
```

A `<br>` (or `<br/>` / `<br />`) inside a table cell renders as a hard line break within that cell,
so a cell can span multiple lines.

Override **globally** by assigning `MarkdownStyle.Default`:

```csharp
using SharpConsoleUI.Configuration;

MarkdownStyle.Default = MarkdownStyle.Default with
{
    LinkColor = Color.Cyan1,
    CodeBackground = new Color(20, 20, 30),
    H1Color = Color.Gold1,
};
```

Override **per build** with `.WithMarkdownStyle(...)`:

```csharp
var control = Controls.Markdown("# Report")
    .WithMarkdownStyle(s => s with { LinkColor = Color.HotPink })
    .Build();
```

### Fluent Helpers

| Helper | Description |
|--------|-------------|
| `Controls.Markdown(text)` | Creates a `MarkupBuilder` seeded with a Markdown block |
| `.AddMarkdown(text)` | Appends a Markdown block to a `MarkupBuilder` |
| `.WithMarkdown(text)` | Alias for `AddMarkdown` |
| `.WithMarkdownStyle(s => s with { … })` | Per-build style override |
| `MarkupControl.SetMarkdown(text)` | Replaces the control's content with rendered Markdown |
| `MarkupControl.MarkdownStyle` | Per-control style property |

```csharp
var c = Controls.Markdown(
    "# Report\n\n**Status:** OK\n\n- item one\n- item two\n\n| A | B |\n|---|---|\n| 1 | 2 |")
    .Build();
window.AddControl(c);
```

## Combined Styles

Multiple decorations and colors can be combined in a single tag, separated by spaces:

```csharp
"[bold red]Bold red text[/]"
"[italic underline blue]Fancy blue text[/]"
"[bold yellow on darkblue]Warning banner[/]"
"[dim italic grey]Subtle note[/]"
```

## Nested Tags

Tags can be nested. Each `[/]` closes the most recent tag, and the style reverts to the previous level:

```csharp
"[bold]Bold [red]bold+red[/] just bold again[/]"
"[green]Green [underline]green+underline[/] green[/]"
"[on blue]Blue bg [bold yellow]bold yellow on blue[/] back to default on blue[/]"
```

## Escaping Brackets

Use doubled brackets to display literal `[` and `]` characters:

| Input | Output |
|-------|--------|
| `[[` | `[` |
| `]]` | `]` |

```csharp
"Use [[bold]] for bold text"       // Displays: Use [bold] for bold text
"Array index: items[[0]]"          // Displays: Array index: items[0]
```

## Programmatic API

### Updating MarkupControl Content

A `MarkupControl` can be updated live. The append API follows the .NET convention you already
know from `StringBuilder` / `Console`:

| Method | Behavior |
|--------|----------|
| `Append(text)` | Inline append — joins onto the current last line, like `StringBuilder.Append` / `Console.Write`. A new line begins only at each embedded `\n`. |
| `AppendLine(text)` | Appends `text` as its own new line, like `Console.WriteLine`. |
| `AppendLines(lines)` | Appends each item as its own line. |
| `SetContent(lines)` | Replaces all content. |

```csharp
var c = new MarkupControl(new List<string>());
c.Append("[green]●[/] ");      // inline
c.Append("all healthy");        // -> "● all healthy"  (same line)
c.AppendLine("[grey]done[/]");  // -> next line
```

`Append`/`AppendLine` are the recommended pair. The earlier `AppendText(text, bool inline = false)`
(line-per-call by default; `inline: true` for the same behavior as `Append`) and `AppendInline(text)`
remain as aliases. On the builder, `Controls.Markup().Append(...)` mirrors the same inline behavior,
alongside `.AddLine(...)`.

### Parsing API

The `MarkupParser` class in `SharpConsoleUI.Parsing` provides the full parsing API:

### MarkupParser.Parse()

Parses markup into a list of `Cell` structs (character + foreground + background + decoration):

```csharp
using SharpConsoleUI.Parsing;

List<Cell> cells = MarkupParser.Parse("[bold red]Hello[/] world", defaultFg, defaultBg);
// cells[0] = Cell('H', red, defaultBg, Bold)
// cells[5] = Cell(' ', defaultFg, defaultBg, None)
```

`[spinner]` tags render the current animation frame at parse time; repeated calls will advance through the frame sequence as the animation manager ticks.

### MarkupParser.StripLength()

Returns the visible character count of a markup string (all tags stripped):

```csharp
int len = MarkupParser.StripLength("[bold red]Hello[/]");
// len = 5
```

Handles multi-line strings by returning the maximum line length.

### MarkupParser.Truncate()

Truncates a markup string to a maximum visible length, preserving and properly closing all tags:

```csharp
string result = MarkupParser.Truncate("[bold]Hello World[/]", 5);
// result = "[bold]Hello[/]"
```

### MarkupParser.Escape()

Escapes brackets in plain text so they are not interpreted as markup:

```csharp
string safe = MarkupParser.Escape("array[0]");
// safe = "array[[0]]"
```

### MarkupParser.Remove()

Strips all markup tags, returning only the plain text. Escaped brackets become single brackets:

```csharp
string plain = MarkupParser.Remove("[bold red]Hello[/] [[world]]");
// plain = "Hello [world]"
```

### MarkupParser.ParseLines()

Parses markup with word-wrapping into multiple lines of cells. The active style stack carries across line breaks:

```csharp
List<List<Cell>> lines = MarkupParser.ParseLines("[bold]Long text that wraps...[/]", width: 40, defaultFg, defaultBg);
```

## See Also

- [MarkupControl](controls/MarkupControl.md) - Display control for markup text
- [Controls Reference](CONTROLS.md) - All built-in controls
- [Theme System](THEMES.md) - Color themes and customization
- [Rendering Pipeline](RENDERING_PIPELINE.md) - How markup flows through the render pipeline

---

[Back to Main Documentation](../README.md)
