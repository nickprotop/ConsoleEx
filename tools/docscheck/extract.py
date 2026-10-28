#!/usr/bin/env python3
"""Extract fenced C# blocks from docs/**/*.md for compilation.

A block opts out with ```csharp no-compile, which is the convention for
deliberately partial snippets (a signature, a fragment, pseudo-code).
"""
import os, re, sys, json

SKIP_DIRS = ("docs/superpowers", "docs/docfx", "/bin/", "/obj/")


# A doc is prose with code in it, and a lot of that code is deliberately a
# piece rather than a program: a fluent chain shown from its second line, a
# signature, a body elided with "...". Those cannot compile and should not be
# reported as if they could. Detect the shapes mechanically so an author does
# not have to tag every one; "```csharp no-compile" stays for the cases this
# cannot see.
import re as _re

_ELLIPSIS = ("...", "\u2026", "/* ... */", "// ...")

def is_fragment(code):
    s = code.strip()
    if not s:
        return True
    if s.startswith("."):
        return True                       # .WithTitle("x") - a chain continued
    if _re.search(r"[,{(\[]\s*$", s):
        return True                       # opens something it never closes
    if s.endswith(("+", "&&", "||", "=>", ":")):
        return True
    if any(e in s for e in _ELLIPSIS):
        return True                       # a body the author elided
    if "\n" in s and not s.endswith((";", "}", ")")):
        return True                       # stops mid-statement
    if _re.match(r"^[\w<>\[\],\s\?]+\s+\w+\s*\([^)]*\)\s*;?$", s):
        return True                       # a bare signature
    return False

def blocks(root):
    for dp, _, fs in os.walk(os.path.join(root, "docs")):
        rel = dp.replace(os.sep, "/") + "/"
        if any(s in rel for s in SKIP_DIRS):
            continue
        for f in sorted(fs):
            if not f.endswith(".md"):
                continue
            path = os.path.join(dp, f)
            text = open(path, encoding="utf-8").read()
            line = 1
            for m in re.finditer(r"^```csharp([^\n]*)\n(.*?)^```", text, re.S | re.M):
                opts = m.group(1).strip()
                start = text[: m.start()].count("\n") + 1
                if "no-compile" in opts:
                    continue
                if is_fragment(m.group(2)):
                    continue
                yield path, start, m.group(2)

if __name__ == "__main__":
    root = sys.argv[1] if len(sys.argv) > 1 else "."
    out = [{"file": p, "line": l, "code": c} for p, l, c in blocks(root)]
    print(json.dumps(out))
