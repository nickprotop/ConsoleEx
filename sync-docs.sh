#!/bin/bash
# sync-docs.sh - Regenerate the control gallery and the site, then offer to commit.
#
# Usage: ./sync-docs.sh [--check] [--force]
#
# The work happens HERE, where you can look at it, rather than on a runner that
# commits images nobody has seen. CI verifies what this produced; it does not
# author it. That is the same division publish.sh uses for releases.

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

CHECK_ONLY=false
FORCE=false
SERVE=false
PORT=8099

while [ $# -gt 0 ]; do
    case $1 in
        --check) CHECK_ONLY=true ;;
        --force) FORCE=true ;;
        --serve) SERVE=true ;;
        --port) PORT="$2"; shift ;;
        --port=*) PORT="${1#*=}" ;;
        -h|--help)
            echo "Usage: ./sync-docs.sh [--check] [--force] [--serve [--port N]]"
            echo ""
            echo "  --check   Report what would change. Commits nothing. CI runs this."
            echo "              exit 0  everything current"
            echo "              exit 1  a screenshot is stale  (advisory)"
            echo "              exit 2  a link is broken       (blocking)"
            echo "  --force   Commit without asking."
            echo "  --serve   Build, then serve the site until you stop it."
            echo "  --port N  Which port to serve on. Default 8099."
            exit 0
            ;;
        *) echo -e "${RED}Unknown option: $1${NC}"; exit 1 ;;
    esac
    shift
done

# Serving is for looking at the result, so it skips the commit step rather than
# leaving a prompt waiting behind a server that never returns.
if [ "$SERVE" = true ]; then
    CHECK_ONLY=false
fi

cd "$(dirname "$0")"

# ── 1. The gallery ───────────────────────────────────────────────────────

echo -e "${BLUE}[1/4]${NC} Regenerating control screenshots..."

# Into a temp directory first: --check must not touch the working tree, and a
# failed run should not leave the committed images half-replaced.
SHOTS=$(mktemp -d)
trap 'rm -rf "$SHOTS"' EXIT

if ! dotnet run --project Examples/Gallery -- "$SHOTS" > "$SHOTS/.log" 2>&1; then
    echo -e "${RED}Error: the gallery failed.${NC}"
    tail -20 "$SHOTS/.log"
    exit 1
fi

echo -e "  $(grep -c 'x[0-9]' "$SHOTS/.log" || echo 0) screenshots"

# Compare against what is committed. A ".manual" marker means someone took that
# picture by hand - a terminal needs a pty, an HTML page needs images that only
# arrive on the URL path - so the gallery never overwrites it.
CHANGED=0
NEW=0

for f in "$SHOTS"/*.png; do
    [ -f "$f" ] || continue
    name=$(basename "$f")

    if [ -f "docs/images/controls/$name.manual" ]; then
        continue
    fi

    if [ ! -f "docs/images/controls/$name" ]; then
        echo -e "  ${GREEN}new${NC}     $name"
        NEW=$((NEW + 1))
    elif ! cmp -s "$f" "docs/images/controls/$name"; then
        echo -e "  ${YELLOW}changed${NC} $name"
        CHANGED=$((CHANGED + 1))
    fi
done

# Two different failures, so two different exit codes. A stale screenshot is a
# worse page; a broken link is a broken page. The workflow treats the first as
# advisory and the second as blocking, and it can only do that if this script
# tells them apart.
STALE=0

if [ "$CHECK_ONLY" = true ]; then
    if [ $((CHANGED + NEW)) -gt 0 ]; then
        STALE=$((CHANGED + NEW))
        echo ""
        echo -e "${YELLOW}$STALE screenshot(s) out of date.${NC} Run ./sync-docs.sh"

        # GitHub renders these inline on the commit, so it is not only a line
        # in a log nobody opens.
        if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
            {
                echo "## Control screenshots (advisory)"
                echo ""
                echo "$STALE screenshot(s) no longer match the controls that produce them:"
                echo ""
            } >> "$GITHUB_STEP_SUMMARY"

            for f in "$SHOTS"/*.png; do
                name=$(basename "$f")
                [ -f "docs/images/controls/$name.manual" ] && continue
                if [ ! -f "docs/images/controls/$name" ] || ! cmp -s "$f" "docs/images/controls/$name"; then
                    echo "::warning file=docs/images/controls/$name::out of date - run ./sync-docs.sh"
                    echo "- \`docs/images/controls/$name\`" >> "$GITHUB_STEP_SUMMARY"
                fi
            done

            {
                echo ""
                echo 'Fix with `./sync-docs.sh`, which regenerates them and offers to commit.'
                echo ""
                echo "The site still deployed; the pages just show the older pictures."
            } >> "$GITHUB_STEP_SUMMARY"
        fi
    else
        echo -e "${GREEN}✓ Screenshots are current${NC}"

        if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
            echo "## Control screenshots" >> "$GITHUB_STEP_SUMMARY"
            echo "✅ All current." >> "$GITHUB_STEP_SUMMARY"
        fi
    fi
fi

# Nothing to copy in check mode; the temp copies are discarded.
if [ "$CHECK_ONLY" = false ] && [ $((CHANGED + NEW)) -gt 0 ]; then
    for f in "$SHOTS"/*.png; do
        name=$(basename "$f")
        [ -f "docs/images/controls/$name.manual" ] && continue
        cp "$f" "docs/images/controls/$name"
    done
fi

# ── 2. Embed them in the control pages ───────────────────────────────────

echo -e "${BLUE}[2/4]${NC} Embedding screenshots in control pages..."

if [ "$CHECK_ONLY" = false ]; then
    python3 site/embed-screenshots.py . || {
        echo -e "${RED}Error: embedding failed.${NC}"
        exit 1
    }
fi

# ── 3. Build the site ────────────────────────────────────────────────────

echo -e "${BLUE}[3/4]${NC} Building the site..."

rm -rf _site
if ! dotnet run --project site/build -- . _site > /dev/null 2>&1; then
    echo -e "${RED}Error: the site build failed.${NC}"
    exit 1
fi

PAGES=$(find _site -name '*.html' | wc -l)

# Every internal link, resolved against the output. A rename that breaks one is
# invisible until someone clicks it, which is usually after it has shipped.
BROKEN=$(python3 - <<'PY'
import os, re
bad = 0
for dp, _, fs in os.walk("_site"):
    for f in fs:
        if not f.endswith(".html"):
            continue
        html = open(os.path.join(dp, f), encoding="utf-8", errors="replace").read()
        for m in re.finditer(r'(?:href|src)="([^"]+)"', html):
            u = m.group(1)
            if u.startswith(("http", "//", "mailto:", "#", "data:")):
                continue
            if not os.path.exists(os.path.normpath(os.path.join(dp, u.split("#")[0]))):
                bad += 1
print(bad)
PY
)

if [ "$BROKEN" -ne 0 ]; then
    echo -e "${RED}Error: $BROKEN broken internal link(s).${NC}"

    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
        {
            echo "## Broken links"
            echo ""
            echo "❌ $BROKEN internal link(s) resolve to nothing."
            echo ""
            echo "This one blocks: a stale screenshot is a worse page, a broken link is a"
            echo "page that 404s for whoever clicks it."
        } >> "$GITHUB_STEP_SUMMARY"
    fi

    # 2, not 1: the workflow tolerates a stale screenshot and must not tolerate
    # this.
    exit 2
fi

# What a reader SEES, which well-formed markdown can still get wrong: a link
# labelled with a filename, a description cut mid-word, a table cell an
# unescaped pipe split in half.
LINT=$(python3 -I tools/docscheck/lint.py _site 2>&1)
LINT_CODE=$?

if [ $LINT_CODE -ne 0 ]; then
    echo -e "${RED}Error: docs lint${NC}"
    echo "$LINT" | head -20

    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
        {
            echo "## Docs lint"
            echo ""
            echo "Findings a reader would see on the page:"
            echo ""
            echo '```'
            echo "$LINT" | head -40
            echo '```'
        } >> "$GITHUB_STEP_SUMMARY"
    fi

    # Same class as a broken link: the page is wrong for whoever reads it.
    exit 2
fi

echo -e "  ${GREEN}$PAGES pages, 0 broken links, lint clean${NC}"

if [ "$CHECK_ONLY" = true ]; then
    if [ "$STALE" -gt 0 ]; then
        echo -e "${YELLOW}⚠ $STALE screenshot(s) out of date; links are fine.${NC}"
        exit 1
    fi

    echo -e "${GREEN}✓ Everything is current${NC}"
    exit 0
fi

# ── 4. Serve, or commit ──────────────────────────────────────────────────

if [ "$SERVE" = true ]; then
    # The API reference is copied from docs/docfx/_site, which is gitignored and
    # so absent until DocFX has run at least once. Saying so beats letting
    # someone wonder why every /api/ link is a 404.
    if [ ! -d docs/docfx/_site ]; then
        echo -e "  ${YELLOW}note:${NC} no API reference in this build."
        echo -e "        Run ${BLUE}docfx docs/docfx/docfx.json${NC} once to include it."
    fi

    echo ""
    echo -e "${GREEN}Serving${NC} http://localhost:${PORT}  ${YELLOW}(ctrl-c to stop)${NC}"
    echo ""

    exec python3 -m http.server "$PORT" -d _site --bind 127.0.0.1
fi

echo -e "${BLUE}[4/4]${NC} Changes..."

if git diff --quiet docs/ 2>/dev/null && [ -z "$(git ls-files --others --exclude-standard docs/)" ]; then
    echo -e "${GREEN}✓ Nothing changed${NC}"
    exit 0
fi

git status --short docs/ | head -30
echo ""

if [ "$FORCE" = false ]; then
    read -p "$(echo -e "${YELLOW}Commit these? [y/N]:${NC} ")" -n 1 -r
    echo
    [[ $REPLY =~ ^[Yy]$ ]] || { echo "Left in the working tree."; exit 0; }
fi

git add docs/
git commit -q -m "Regenerate the control gallery

$((CHANGED + NEW)) screenshot(s) changed, from ./sync-docs.sh."

echo -e "${GREEN}✓ Committed${NC}  — push when you are ready."
