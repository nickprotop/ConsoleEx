# The website

Source for <https://nickprotop.github.io/ConsoleEx>.

```bash
dotnet run --project site/build -- . _site
python3 -m http.server 8099 --directory _site
```

Then open <http://localhost:8099>.

## What is here

| | |
|---|---|
| `pages/` | reserved for page fragments; the hand-written pages currently live in `build/Pages.cs` |
| `styles/site.css` | one stylesheet for every page, light and dark |
| `build/` | the generator (C#, Markdig) |

## What it builds from

- `docs/*.md` — rendered into the site's shell. **Never modified.** The markdown
  stays readable on GitHub and every link that works there keeps working here.
- `docs/images/` — copied to `_site/images/`.
- `docs/docfx/_site/` — the API reference, copied to `_site/api/` if DocFX has
  been run. Without it the site still builds; only the API links are missing.

Output goes to `_site/`, which is gitignored and is the only thing published.

## Navigation

`build/Nav.cs` holds the six groups and their order. A markdown file that is
not placed in a group is still rendered and appears under "More" on the docs
index, so adding a guide never makes it invisible.

## Keeping it current

```bash
./sync-docs.sh
```

Regenerates the control gallery, embeds anything new into the control pages,
builds the site, checks every internal link, then shows you what changed and
offers to commit it.

The work happens here, where you can look at it, rather than on a runner that
commits images nobody has seen. That is the same division `publish.sh` uses for
releases.

`./sync-docs.sh --check` does the same work and writes nothing. That is what CI
runs, in `docs.yml`, before the deploy steps. It distinguishes two failures:

| exit | means | CI |
|------|-------|-----|
| 0 | everything current | deploy |
| 1 | a screenshot is stale | ⚠ advisory — **deploys anyway** |
| 2 | an internal link is broken | ❌ blocks the deploy |

A stale screenshot makes a page worse; a broken link makes a page that 404s for
whoever clicks it. Blocking a deploy over a picture that is a day behind helps
nobody, so the stale case writes a warning annotation and a job summary naming
the files, and the site still goes out.

## When the site deploys

On push to `master` touching `docs/**`, `site/**`, `README.md` or
`SharpConsoleUI/**`, and on every `v*` tag. The library is in that list because
DocFX reads its XML comments: a doc comment is a site change.

DocFX runs on every deploy (~38s) and builds **only** the API reference. It used
to render the guides as well, so every page existed twice — at `/docs/X.html`
and `/api/X.html` — and the `/api/` copies carried broken links, because DocFX
does not rewrite links to files outside `docs/` and this generator does.
