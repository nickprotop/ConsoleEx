# Syntax Highlighting

SharpConsoleUI highlights code with [TextMate grammars](https://macromates.com/manual/en/language_grammars) — the same grammar format Visual Studio Code uses — coloured by **your application's theme** rather than a bundled editor theme. Roughly **64 languages** resolve out of the box with no initialization call.

> **Scope:** This is *lexical* (grammar-based) highlighting — keywords, strings, comments, numbers, types, functions, variables. The library does **not** include LSP/semantic highlighting; that is intentionally out of scope. For LSP-powered IntelliSense, see external projects such as [LazyDotIDE](https://github.com/nickprotop/lazydotide), which builds on top of SharpConsoleUI.

## Quick start

Nothing to register — ask the registry for a language and use the result:

```csharp
using SharpConsoleUI.Highlighting;

var editor = Controls.MultilineEdit()
    .WithContent(sourceCode)
    .WithSyntaxHighlighter(SyntaxHighlighters.For("csharp"))
    .Build();
```

Markdown fenced code blocks highlight automatically from their language hint:

````csharp
var doc = Controls.Markdown("""
```rust
fn main() { println!("hi"); }
```
""").Build();
````

## Languages

All languages shipped by [TextMateSharp.Grammars](https://www.nuget.org/packages/TextMateSharp.Grammars) are available, including C#, JavaScript, TypeScript, Python, Rust, Go, Java, C/C++, Ruby, PHP, SQL, HTML, CSS, JSON, YAML, XML, Markdown, shell scripts, Dockerfiles, diffs, and many more.

Resolution accepts language ids, aliases, and file extensions, case-insensitively:

```csharp
SyntaxHighlighters.For("csharp");   // id
SyntaxHighlighters.For("cs");       // extension
SyntaxHighlighters.For("sh");       // alias -> shellscript
SyntaxHighlighters.For("toml");     // null - no grammar ships for it
```

Grammars load **lazily on first use**, so an application that never highlights anything pays no startup cost.

## Theming

Code colours come from the active theme, so highlighted code matches the rest of your UI and follows theme switches — including light themes, where a fixed editor palette would look wrong.

A theme supplies colours through `ITheme.SyntaxColors`, a `SyntaxPalette` with one colour per semantic role:

| Role | Applies to |
|------|-----------|
| `Default` | text matching no other role |
| `Keyword` | `if`, `class`, `return`, storage modifiers |
| `Operator` | `=`, `+`, `=>` |
| `String` | string and character literals |
| `Number` | numeric literals |
| `Comment` | comments, including their delimiters |
| `Type` | type, class, struct, and interface names |
| `Function` | function and method names |
| `Variable` | variables, fields, parameters |
| `Constant` | `true`, `null`, other language constants |
| `Tag` | HTML/XML element names |
| `Attribute` | markup attribute names |
| `Punctuation` | braces, semicolons, commas |
| `Invalid` | text the grammar marks invalid |

`ITheme.SyntaxColors` returns `null` by default, in which case `SyntaxPalette.DeriveFrom(theme)` generates a readable palette from the theme's base colours. To customise, return your own:

```csharp
public override SyntaxPalette? SyntaxColors => new()
{
    Keyword = Color.DodgerBlue2,
    String  = Color.DarkSeaGreen,
    Comment = Color.Grey,
    // unset roles fall back to the derived defaults
};
```

Every resolved colour passes through a contrast floor against the code background, so a low-contrast palette cannot render unreadable code.

### Using a bundled VS Code theme instead

To render code in Visual Studio Code's own colours rather than your app's:

```csharp
using SharpConsoleUI.Highlighting.TextMate;
using TextMateSharp.Grammars;

TextMateHighlighting.RegisterAll(ThemeName.DarkPlus);
```

## Registering a custom highlighter

An explicit registration always wins over the TextMate fallback:

```csharp
SyntaxHighlighters.Register("mylang", new MyHighlighter());
```

Implement `ISyntaxHighlighter` to supply your own tokenizer. `Tokenize` receives the line, its index, and the parser state from the previous line, and returns coloured spans plus the state to carry forward — that state is what makes multi-line constructs such as block comments work.

## How resolution works

The registry is the single source of truth, consulted by every consumer:

```
SyntaxHighlighters.For(lang)
  1. explicit Register(lang, ...)        -> that highlighter
  2. a TextMate grammar for lang         -> a cached TextMate highlighter
  3. otherwise                           -> null
```

### Markdown fenced code blocks

A fenced block with a language hint is highlighted automatically. Resolution order:

1. The per-style `MarkdownStyle.CodeHighlighters` override, keyed by language hint.
2. `SyntaxHighlighters.For(lang)`.
3. A flat, shaded code block — when the language is unknown or no hint is given.

Indented code blocks carry no language hint and always render flat.

See [Markup Syntax → Syntax Highlighting in Code Blocks](MARKUP_SYNTAX.md#syntax-highlighting-in-code-blocks) for the `CodeHighlighters` override.

### MultilineEditControl

The editor colours its content with any `ISyntaxHighlighter`, assigned through the builder or the `SyntaxHighlighter` property. Its token cache is invalidated on content changes, re-tokenizing only affected lines and their successors. See [MultilineEditControl → Syntax Highlighting](controls/MultilineEditControl.md#syntax-highlighting).

## Upgrading

Code blocks **change appearance** after upgrading to the TextMate engine: colours now come from your theme, more languages highlight than before, and grammars distinguish variables and function names that the previous regex highlighters could not. No API changed.

The thirteen former regex highlighter classes (`CSharpSyntaxHighlighter`, `JsonSyntaxHighlighter`, and so on) still exist and still work, but are `[Obsolete]`: they now delegate to the TextMate engine. Replace `new CSharpSyntaxHighlighter()` with `SyntaxHighlighters.For("csharp")`.

`SlnSyntaxHighlighter` is the exception — no TextMate grammar covers `.sln` files, so it keeps its original implementation and is not obsolete.

## Performance

Tokenized lines are cached, so repainting unchanged content does no grammar work — a repaint of a
50-line viewport drops from roughly 66 ms to well under 1 ms. Results are keyed by the line text
and the incoming parser state, and are invalidated automatically when the theme changes.

`MultilineEditControl` keeps its own per-line cache on top of this, invalidated on edits.

If you mutate a highlighter **in place** — the usual case being an `ISyntaxHighlighter` decorator
that layers LSP semantic tokens over a lexical one — the editor cannot know its output changed.
Tell it explicitly:

```csharp
myDecorator.UpdateTokens(semanticTokens, legend);
editor.RefreshSyntaxHighlighting();
```

Assigning `editor.SyntaxHighlighter` the instance it already holds is a no-op and will **not**
refresh; without the change guard, every such assignment would discard the whole file's tokens and
re-tokenize on the next paint.

## Thread safety

`SyntaxHighlighters.For(...)` and the highlighters it returns are safe to use from multiple threads; tokenization is serialized per grammar internally. Highlighter instances are shared per language, so caching one and reusing it across windows is fine.
