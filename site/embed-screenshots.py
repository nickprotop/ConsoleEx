"""Puts each generated screenshot into its control's documentation page.

Run through ./sync-docs.sh rather than directly; it regenerates the gallery
first, so this has something current to embed.

Inserted after the page's first paragraph, which is where a reader looks for
"what does this look like" - above the property tables, below the sentence
that says what the control is.
"""
import os, re, sys

root = sys.argv[1]
shots = os.path.join(root, "docs", "images", "controls")
pages = os.path.join(root, "docs", "controls")

MARK = "<!-- gallery -->"

done = skipped = 0

for png in sorted(os.listdir(shots)):
    if not png.endswith(".png") or "-" in png:
        continue

    name = png[:-4]
    page = os.path.join(pages, name + ".md")
    if not os.path.exists(page):
        skipped += 1
        continue

    text = open(page, encoding="utf-8").read()

    if MARK in text:
        continue

    # The second state, when the gallery took one.
    extra = [f for f in sorted(os.listdir(shots))
             if f.startswith(name + "-") and f.endswith(".png")]

    block = [MARK, "", f"![{name}](../images/controls/{png})", ""]

    for f in extra:
        label = f[len(name) + 1:-4]
        block.append(f"![{name}, {label}](../images/controls/{f})")
        block.append("")

    # After the first paragraph under the title.
    lines = text.split("\n")
    at = None

    for i, line in enumerate(lines):
        if line.startswith("# "):
            # the blank line, the sentence, then the blank after it
            j = i + 1
            while j < len(lines) and lines[j].strip() == "":
                j += 1
            while j < len(lines) and lines[j].strip() != "":
                j += 1
            at = j
            break

    if at is None:
        skipped += 1
        continue

    lines[at:at] = [""] + block
    open(page, "w", encoding="utf-8").write("\n".join(lines))
    done += 1

print(f"  embedded in {done} pages, skipped {skipped}")
