# docscheck

Two tools that check the documentation the way a reader meets it.

## `lint.py` — run by CI

Runs against the **generated site**, not the markdown, because the question is
what the page shows. Well-formed markdown still produces a link labelled
`DOCKING.md`, a description cut mid-word, or a table cell an unescaped pipe
split in half.

```bash
python3 tools/docscheck/lint.py _site
```

Exit 0 clean, 1 findings. `sync-docs.sh` runs it and treats a finding the same
way it treats a broken link: blocking, because both are wrong for whoever
reads the page.

What it catches:

| Finding | Why it matters |
|---|---|
| link label ending in `.md` | the reader sees a filename, not a page name |
| `href` still pointing at `.md` | 404 on the site (links to GitHub are exempt) |
| meta description cut mid-word | a shared link reads as broken |
| meta description that is a lead-in label | "What you'll build:" describes nothing |
| backslash visible in a table cell | an escape that leaked |
| unrendered markdown link | a link that never became one |

## `extract.py` — fenced C# blocks

Pulls ```csharp blocks out of `docs/**/*.md`.

Most sample code is deliberately a *piece* rather than a program: a fluent
chain shown from its second line, a bare signature, a body elided with `...`.
`is_fragment()` detects those shapes mechanically so nobody has to tag them.

For a block that is genuinely partial but looks whole, opt out explicitly:

````
```csharp no-compile
// a fragment that would otherwise look compilable
```
````

There is no compile step in CI. It was tried: of 945 blocks that survived
fragment filtering, 118 failed and **every failure was an intentional
fragment**, not a defect. Catching real defects needs each page's blocks
stitched into one cumulative program with per-page context, which is a
different and much larger tool. `extract.py` stays because that tool would
start here.
