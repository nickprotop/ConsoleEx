# Syntax Highlighting

SharpConsoleUI ships with a set of built-in syntax highlighters and a central registry that maps language names (and aliases) to highlighter instances. The registry is the single source of truth for the language→highlighter mapping, shared by every consumer so the mapping is never duplicated.

> **Scope:** This is *lexical* (token-based) highlighting — keywords, strings, comments, numbers, punctuation. The library does **not** include LSP/semantic highlighting; that is intentionally out of scope. For LSP-powered IntelliSense, see external projects such as [LazyDotIDE](https://github.com/nickprotop/lazydotide), which builds on top of SharpConsoleUI.

## Built-in Highlighters

Thirteen highlighters live in the `SharpConsoleUI.Highlighting` namespace, each named `<Lang>SyntaxHighlighter`:

| Language | Highlighter | Aliases |
|----------|-------------|---------|
| C# | `CSharpSyntaxHighlighter` | `csharp`, `cs` |
| Bash | `BashSyntaxHighlighter` | `bash`, `sh`, `shell`, `zsh` |
| JSON | `JsonSyntaxHighlighter` | `json` |
| JavaScript | `JsSyntaxHighlighter` | `javascript`, `js`, `node`, `mjs`, `cjs` |
| CSS | `CssSyntaxHighlighter` | `css` |
| HTML | `HtmlSyntaxHighlighter` | `html`, `htm` |
| XML | `XmlSyntaxHighlighter` | `xml` |
| YAML | `YamlSyntaxHighlighter` | `yaml`, `yml` |
| Razor | `RazorSyntaxHighlighter` | `razor`, `cshtml` |
| Dockerfile | `DockerfileSyntaxHighlighter` | `dockerfile`, `docker` |
| Solution | `SlnSyntaxHighlighter` | `sln` |
| Diff | `DiffSyntaxHighlighter` | `diff`, `patch` |
| Markdown | `MarkdownSyntaxHighlighter` | `markdown`, `md` |

All highlighters implement `ISyntaxHighlighter`. They are stateless across `Tokenize` calls (per-line parser state lives in `SyntaxLineState`), so a single shared instance per language is reused safely.

## The `SyntaxHighlighters` Registry

`SharpConsoleUI.Highlighting.SyntaxHighlighters` is a static registry seeded with all thirteen built-ins (and their aliases) at startup.

| Member | Description |
|--------|-------------|
| `For(string? language)` | Returns the `ISyntaxHighlighter?` for a language name/alias (case-insensitive, trimmed). Returns `null` for an unknown, null, or empty language. |
| `Register(string language, ISyntaxHighlighter highlighter)` | Registers (or overrides) a highlighter for a name/alias. Additive — built-ins remain registered. |
| `Has(string? language)` | `true` if a highlighter is registered for the language/alias. |

```csharp
using SharpConsoleUI.Highlighting;

ISyntaxHighlighter? hl = SyntaxHighlighters.For("cs");   // CSharpSyntaxHighlighter
bool yes = SyntaxHighlighters.Has("dockerfile");          // true
ISyntaxHighlighter? none = SyntaxHighlighters.For("toml"); // null (until registered)
```

## Consumers

The same registry feeds two places in the library.

### 1. Markdown fenced code blocks

A fenced code block with a **language hint** is auto-highlighted. Resolution order for a given hint:

1. The per-style `MarkdownStyle.CodeHighlighters` override (keyed by language hint), if present.
2. `SyntaxHighlighters.For(lang)` — the global registry.
3. A flat, shaded code block — used when the language is unknown or no hint is given.

````
```csharp
var control = Controls.Markdown("# Report").Build();
```
````

See [Markup Syntax → Syntax Highlighting in Code Blocks](MARKUP_SYNTAX.md#syntax-highlighting-in-code-blocks) for the Markdown-specific details and the `CodeHighlighters` override.

### 2. MultilineEditControl

`MultilineEditControl` colorizes its content with any `ISyntaxHighlighter`. Resolve one from the registry instead of newing it up directly:

```csharp
using SharpConsoleUI.Highlighting;

var editor = Controls.MultilineEdit()
    .WithContent(sourceCode)
    .WithLineNumbers()
    .WithSyntaxHighlighter(SyntaxHighlighters.For("csharp"))
    .Build();
```

You can also assign the `SyntaxHighlighter` property directly, or pass a concrete instance (`new CSharpSyntaxHighlighter()`). The editor's token cache is invalidated automatically on content changes, re-tokenizing only affected lines and their successors. See [MultilineEditControl → Syntax Highlighting](controls/MultilineEditControl.md#syntax-highlighting) for the `ISyntaxHighlighter` contract and multi-line state handling.

## Registering a Custom Highlighter

Implement `ISyntaxHighlighter` and register it under one or more names. Once registered, it is available everywhere the registry is consulted — Markdown code blocks and any `SyntaxHighlighters.For(...)` lookup.

```csharp
using SharpConsoleUI.Highlighting;

public sealed class TomlSyntaxHighlighter : ISyntaxHighlighter
{
    public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
        Tokenize(string line, int lineIndex, SyntaxLineState startState)
    {
        // Produce colored token spans for this line.
        // Use a SyntaxLineState subclass to carry multi-line parser state if needed.
        ...
    }
}

SyntaxHighlighters.Register("toml", new TomlSyntaxHighlighter());

// Now both consumers can use it:
var editor = Controls.MultilineEdit()
    .WithSyntaxHighlighter(SyntaxHighlighters.For("toml"))
    .Build();
```

To override a highlighter for **Markdown only** without touching the global registry, use `MarkdownStyle.CodeHighlighters` (consulted before the registry) — see [Markup Syntax → Markdown](MARKUP_SYNTAX.md#markdown).

## See Also

- [Markup Syntax Reference](MARKUP_SYNTAX.md) - The `[markdown]` tag and code-block highlighting
- [MultilineEditControl](controls/MultilineEditControl.md) - The text editor that consumes highlighters
- [Controls Reference](CONTROLS.md) - All built-in controls

---

[Back to Main Documentation](../README.md)
