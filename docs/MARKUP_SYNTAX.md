# Markup Syntax Reference

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
| `darkorange` | (255, 140, 0) |
| `indianred` | (205, 92, 92) |
| `hotpink` | (255, 105, 180) |
| `deeppink1` | (255, 20, 147) |
| `mediumorchid` | (186, 85, 211) |
| `darkviolet` | (148, 0, 211) |
| `blueviolet` | (138, 43, 226) |
| `royalblue1` | (65, 105, 225) |
| `cornflowerblue` | (100, 149, 237) |
| `dodgerblue1` | (30, 144, 255) |
| `deepskyblue1` | (0, 191, 255) |
| `steelblue` | (70, 130, 180) |
| `cadetblue` | (95, 158, 160) |
| `mediumturquoise` | (72, 209, 204) |
| `darkturquoise` | (0, 206, 209) |
| `lightseagreen` | (32, 178, 170) |
| `mediumspringgreen` | (0, 250, 154) |
| `springgreen1` | (0, 255, 127) |
| `chartreuse1` | (127, 255, 0) |
| `greenyellow` | (173, 255, 47) |
| `lightgreen` | (144, 238, 144) |
| `palegreen1` | (152, 251, 152) |
| `darkseagreen` | (143, 188, 143) |
| `mediumseagreen` | (60, 179, 113) |
| `lightcoral` | (240, 128, 128) |
| `salmon1` | (250, 128, 114) |
| `sandybrown` | (244, 164, 96) |
| `gold1` | (255, 215, 0) |
| `violet` | (238, 130, 238) |
| `orchid` | (218, 112, 214) |
| `plum1` | (221, 160, 221) |

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
```

```csharp
"[#FF8000]Orange text[/]"
"[#F80]Short hex orange[/]"
"[#336699]Steel blue text[/]"
```

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

The `MarkupParser` class in `SharpConsoleUI.Parsing` provides the full parsing API:

### MarkupParser.Parse()

Parses markup into a list of `Cell` structs (character + foreground + background + decoration):

```csharp
using SharpConsoleUI.Parsing;

List<Cell> cells = MarkupParser.Parse("[bold red]Hello[/] world", defaultFg, defaultBg);
// cells[0] = Cell('H', red, defaultBg, Bold)
// cells[5] = Cell(' ', defaultFg, defaultBg, None)
```

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
