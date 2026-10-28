#!/usr/bin/env python3
"""Lint the generated site for defects a reader would see.

Runs against _site, not the markdown, because the question is what the page
shows - a label the generator failed to rewrite, a table cell an unescaped
pipe cut in half, a description that stops mid-word. The markdown can be
perfectly well-formed and still produce any of those.

Exit 0 clean, 1 findings.
"""
import os, re, sys, html as _html

SKIP = ("/api/",)

def pages(site):
    for dp, _, fs in os.walk(site):
        for f in fs:
            if not f.endswith(".html"):
                continue
            p = os.path.join(dp, f)
            if any(s in p.replace(os.sep, "/") for s in SKIP):
                continue
            yield p, open(p, encoding="utf-8", errors="replace").read()

def main():
    site = sys.argv[1] if len(sys.argv) > 1 else "_site"
    bad = []

    for path, h in pages(site):
        name = os.path.relpath(path, site)

        # A link whose VISIBLE TEXT is a filename the reader cannot use.
        for m in re.finditer(r'<a [^>]*href="([^"]+)"[^>]*>(.*?)</a>', h, re.S):
            href = m.group(1)

            # A link INTO the repository on GitHub ends in .md because that is
            # the file being linked. Only a link meant for this site is wrong.
            if href.startswith(("http://", "https://", "mailto:")):
                continue

            text = re.sub(r"<[^>]+>", "", m.group(2)).strip()
            if text.endswith(".md"):
                bad.append((name, "link label is a .md filename", text))
            if href.endswith(".md"):
                bad.append((name, "link href still points at .md", href))

        # A description cut mid-word reads as a bug wherever it is shared.
        for m in re.finditer(r'<meta [^>]*(?:name="description"|property="og:description")'
                             r'[^>]*content="([^"]*)"', h):
            c = _html.unescape(m.group(1)).strip()
            if c.endswith("...") and not re.search(r"[.!?]\s*\.\.\.$", c):
                tail = c[:-3].rstrip()
                if tail and not tail.endswith((".", "!", "?")) and " " in tail[-14:] is False:
                    bad.append((name, "meta description cut mid-word", c[-40:]))
            if re.match(r"^[A-Z][^.!?]{0,30}:\s", c):
                bad.append((name, "meta description is a lead-in label", c[:40]))

        # A table cell an unescaped pipe split, leaving a stray backslash.
        for m in re.finditer(r"<td>([^<]*\\[|])", h):
            bad.append((name, "backslash visible in a table cell", m.group(1)[:40]))

        # Markdown that reached the page as text.
        for m in re.finditer(r"<(?:p|td|li)>[^<]*(\[\[[^\]]+\]\([^)]+\))", h):
            bad.append((name, "unrendered markdown link", m.group(1)[:50]))

    for name, kind, detail in bad:
        print(f"{name}: {kind}: {detail}")
        if os.environ.get("GITHUB_STEP_SUMMARY"):
            with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as s:
                s.write(f"- `{name}` {kind}: {detail}\n")

    print(f"\n{len(bad)} finding(s)")
    return 1 if bad else 0

if __name__ == "__main__":
    sys.exit(main())
