# Unicode Width

How SharpConsoleUI decides how many terminal columns a character occupies. Everything positional —
wrapping, caret placement, column alignment, table layout, border drawing — is computed from these
numbers, so a disagreement with the terminal shows up as text that drifts a cell further out of place
with every affected character.

All measurement goes through `SharpConsoleUI.Helpers.UnicodeWidth`, which resolves widths from the
[Wcwidth](https://github.com/spectreconsole/wcwidth) tables and then applies the terminal-specific
adjustments below.

## The easy cases

Most characters have a width that is a property of the character, and every terminal agrees:

| Class | Example | Columns |
|---|---|---|
| Narrow / Halfwidth | `a`, `1`, `.` | 1 |
| Wide / Fullwidth | `中`, `한`, `あ` | 2 |
| Combining marks | ` ́ ` (U+0301) | 0 |
| Emoji | `🚀` | 2 |

## The terminal-dependent cases

Three widths are not decidable from the character alone, so they are **probed** at startup by
`TerminalCapabilities.Probe()` — the library writes a character, asks the terminal where the cursor
ended up (`ESC[6n`), and believes the answer. Each probe falls back to a safe default if the terminal
does not reply.

| Capability | Question | Default if unprobed |
|---|---|---|
| `SupportsVS16Widening` | Does emoji + VS16 (U+FE0F) render as 2 columns? | `true` |
| `SupportsZwjLigation` | Does a ZWJ sequence render as one glyph or its parts? | `true` |
| `SupportsUnicode16Widths` | Are Unicode 16.0's newly-widened characters 2 columns? | `false` |
| `AmbiguousCharactersAreWide` | Are East Asian Ambiguous characters 2 columns? | `false` |

Each has a matching setter (`SetAmbiguousCharactersAreWide`, …) for tests, or for an application that
knows its terminal ahead of time. A probe cannot see a user's terminal *preference* on every emulator,
so the setter is the escape hatch when the answer is known out of band.

## East Asian Ambiguous

This is the one width question Unicode deliberately declines to answer. Around 800 codepoints are
classified **Ambiguous** (`EAW=A`) because they were historically encoded in *both* Western and East
Asian character sets — so their width is a property of the rendering context, not the character:

```
°  ±  ×  ÷  “  ”  …  →  α  β  д  ★  ☎
```

A terminal configured for CJK draws these two columns wide. A Western one draws them narrow. **Both
are correct.** Wcwidth resolves them to 1 and offers no mode, saying so explicitly in its own source:

> Choosing single-width for these characters is easy to justify as the appropriate long-term
> solution, as the CJK practice of displaying these characters as double-width comes from historic
> implementation simplicity … and not any typographic considerations.

That is the right default, but it leaves a user whose terminal says otherwise with text that drifts.
`AmbiguousCharactersAreWide` is how the library is told. The range table lives in
`Helpers/EastAsianAmbiguous.cs`, generated from the Unicode Character Database
(`EastAsianWidth.txt`, version 15.0.0 — the same version `UnicodeWidth` resolves against).

### The chrome exclusion

Box-drawing characters are themselves East Asian Ambiguous, and SharpConsoleUI draws its own window
borders, scrollbars, progress bars, checkboxes and menu arrows from them. Four ranges are therefore
**held at width 1 even when the policy says wide**:

| Range | Block | Drawn by |
|---|---|---|
| U+2190–U+21FF | Arrows | menu and submenu indicators |
| U+2500–U+257F | Box Drawing | window borders, separators, table rules |
| U+2580–U+259F | Block Elements | scrollbar thumbs, progress bars, shading |
| U+25A0–U+25FF | Geometric Shapes | checkbox, radio, tree expanders, spinners |

**This is a deliberate deviation from the standard, and it is worth being precise about what it buys.**
On a terminal that genuinely renders these two columns wide, excluding them does *not* make the chrome
correct — the terminal draws what it draws. It keeps the chrome exactly as correct as it is today,
while letting the policy fix ordinary text. Making chrome genuinely correct means dropping the
exclusion *and* auditing every renderer that assumes one glyph fills one column.

Miscellaneous Symbols (U+2600–U+26FF) is deliberately **not** excluded. The only glyph the library
draws from that block is `⚠` (U+26A0), which is East Asian *Neutral* rather than Ambiguous, so the
policy never touches it — excluding the block would have narrowed 69 genuinely ambiguous codepoints
(`★`, `☎`) in user content to protect a character that could not be affected.

## Working with widths

```csharp
using SharpConsoleUI.Helpers;

UnicodeWidth.GetStringWidth("中文");        // 4 — display columns, not characters
UnicodeWidth.GetRuneWidth(new Rune('中'));  // 2
UnicodeWidth.IsWide('中');                  // true

// Mapping between character offsets and display columns — always use these rather than
// arithmetic on string indices, or the caret lands in the wrong cell on CJK and emoji.
UnicodeWidth.CharOffsetToColumn("a中b", 2); // 3
UnicodeWidth.ColumnToCharOffset("a中b", 3); // 2
UnicodeWidth.TakeColumns("a中b", 0, 3);     // (endChar, width) — what fits in 3 columns
```

`TakeColumns` is the primitive behind soft-wrapping (see `Helpers/TextWrapping.cs`), so wrapping
breaks where text actually renders rather than where character counting suggests.

## See Also

- [Rendering Pipeline](RENDERING_PIPELINE.md) — how measured widths become buffer cells
- [Markup Syntax](MARKUP_SYNTAX.md) — markup is stripped before measuring

[Back to Main Documentation](https://nickprotop.github.io/ConsoleEx/)
