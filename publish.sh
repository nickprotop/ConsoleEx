#!/bin/bash
# publish.sh - Automate NuGet package publishing via GitHub Actions
# Usage: ./publish.sh [major|minor|patch] [--force]

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
VERSION_TYPE="patch"
IS_RC=false
FORCE=false

# Parse arguments
for arg in "$@"; do
    case $arg in
        major|minor|patch)
            VERSION_TYPE="$arg"
            ;;
        rc)
            IS_RC=true
            ;;
        --force|-f)
            FORCE=true
            ;;
        --help|-h)
            echo "Usage: ./publish.sh [rc] [major|minor|patch] [--force]"
            echo ""
            echo "Arguments:"
            echo "  major           Increment major version (v2.4.6 -> v3.0.0)"
            echo "  minor           Increment minor version (v2.4.6 -> v2.5.0)"
            echo "  patch           Increment patch version (v2.4.6 -> v2.4.7) [default]"
            echo "  rc              Publish a release candidate (prerelease) toward the next"
            echo "                  major/minor/patch. Repeating 'rc' for the same target bumps"
            echo "                  the rc counter (-rc.1 -> -rc.2). Promote to stable by running"
            echo "                  the matching major/minor/patch without 'rc'."
            echo "  --force, -f     Skip confirmation prompt"
            echo ""
            echo "Examples:"
            echo "  ./publish.sh patch              # Stable patch (v2.4.6 -> v2.4.7)"
            echo "  ./publish.sh rc minor           # First RC toward a minor (v2.4.6 -> v2.5.0-rc.1)"
            echo "  ./publish.sh rc minor           # Again: next RC (v2.5.0-rc.1 -> v2.5.0-rc.2)"
            echo "  ./publish.sh minor              # Promote: drop the suffix (v2.5.0-rc.2 -> v2.5.0)"
            exit 0
            ;;
        *)
            echo -e "${RED}Error: Unknown argument '$arg'${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

echo -e "${BLUE}=== SharpConsoleUI NuGet Publisher ===${NC}"
echo ""

# Check if git is clean
echo -e "${BLUE}[1/5]${NC} Checking git status..."

if ! git diff-index --quiet HEAD --; then
    echo -e "${RED}Error: You have unstaged changes. Please commit or stash them first.${NC}"
    git status --short
    exit 1
fi

# Check if there are unpushed commits
LOCAL=$(git rev-parse @)
REMOTE=$(git rev-parse @{u} 2>/dev/null || echo "")

if [ -z "$REMOTE" ]; then
    echo -e "${RED}Error: No upstream branch configured. Please set up remote tracking.${NC}"
    exit 1
fi

if [ "$LOCAL" != "$REMOTE" ]; then
    AHEAD=$(git rev-list --count @{u}..HEAD)
    BEHIND=$(git rev-list --count HEAD..@{u})

    if [ "$AHEAD" -gt 0 ]; then
        echo -e "${RED}Error: You have $AHEAD unpushed commit(s). Please push first.${NC}"
        exit 1
    fi

    if [ "$BEHIND" -gt 0 ]; then
        echo -e "${RED}Error: You are $BEHIND commit(s) behind remote. Please pull first.${NC}"
        exit 1
    fi
fi

echo -e "${GREEN}✓ Git status is clean and up to date${NC}"
echo ""

# Code-quality gate — refuse to release if the library isn't clean (mirrors CI; see
# docs/CODE_QUALITY_ENFORCEMENT.md). Fails fast and locally, before any version bump or tag.
echo -e "${BLUE}[1.5/5]${NC} Code-quality gate..."

if ! dotnet format SharpConsoleUI/SharpConsoleUI.csproj --verify-no-changes --verbosity quiet >/dev/null 2>&1; then
    echo -e "${RED}Error: code is not formatted. Run: dotnet format SharpConsoleUI/SharpConsoleUI.csproj${NC}"
    exit 1
fi

HEADER_MISSING=0
while IFS= read -r f; do
    head -8 "$f" | grep -q "License: MIT" || HEADER_MISSING=$((HEADER_MISSING + 1))
done < <(find SharpConsoleUI -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*')
if [ "$HEADER_MISSING" -ne 0 ]; then
    echo -e "${RED}Error: $HEADER_MISSING source file(s) missing the license-header banner.${NC}"
    exit 1
fi

WARN_COUNT=$(dotnet build SharpConsoleUI/SharpConsoleUI.csproj -c Release --no-incremental 2>&1 \
    | grep -cE "warning (CS|CA|IDE)" || true)
if [ "$WARN_COUNT" -ne 0 ]; then
    echo -e "${RED}Error: build produced $WARN_COUNT code warning(s). The library must build warning-clean before release.${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Format clean, headers present, build warning-clean${NC}"
echo ""

# Fetch tags
echo -e "${BLUE}[2/5]${NC} Fetching tags from remote..."
git fetch --tags --quiet

# Get the latest tag with correct SemVer precedence. Neither `git --sort=-v:refname` nor GNU
# `sort -V` implements prerelease precedence: both rank v2.5.0-rc.2 ABOVE v2.5.0, but SemVer says a
# prerelease is LOWER than its release. Picking the rc as "latest" after a stable release would make
# the next `rc` build produce rc.N for an already-released version. So build a sortable key per tag:
#   major.minor.patch + a 4th field where stable = 9999 (sorts after any rc), rc.N = N.
# Sort numerically on that key and take the top.
LATEST_TAG=$(
    git tag | while IFS= read -r tag; do
        if [[ $tag =~ ^v([0-9]+)\.([0-9]+)\.([0-9]+)(-rc\.([0-9]+))?$ ]]; then
            rc="${BASH_REMATCH[5]}"
            # stable (no rc) ranks highest within its M.m.p
            order="${rc:-9999}"
            printf '%010d.%010d.%010d.%010d\t%s\n' \
                "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}" "${BASH_REMATCH[3]}" "$order" "$tag"
        fi
    done | sort -r | head -1 | cut -f2-
)

if [ -z "$LATEST_TAG" ]; then
    echo -e "${RED}Error: No tags found in repository${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Latest tag: $LATEST_TAG${NC}"
echo ""

# Parse version from tag. Accept stable (v2.4.6) and RC (v2.5.0-rc.1) tags.
if [[ ! $LATEST_TAG =~ ^v([0-9]+)\.([0-9]+)\.([0-9]+)(-rc\.([0-9]+))?$ ]]; then
    echo -e "${RED}Error: Latest tag '$LATEST_TAG' is not in semver format (v{major}.{minor}.{patch}[-rc.{n}])${NC}"
    exit 1
fi

MAJOR="${BASH_REMATCH[1]}"
MINOR="${BASH_REMATCH[2]}"
PATCH="${BASH_REMATCH[3]}"
LATEST_RC="${BASH_REMATCH[5]}"  # empty when the latest tag is stable

# The base (M.m.p) the next release targets. When the latest tag is already an RC, its own
# M.m.p IS the target base — a 'minor' RC of 2.4.6 produced 2.5.0-rc.1, so 2.5.0 is the base for
# both the next RC and the eventual stable promotion. When the latest tag is stable, bump from it.
echo -e "${BLUE}[3/5]${NC} Calculating new version..."

if [ -n "$LATEST_RC" ]; then
    # Latest tag is an RC (e.g. v2.5.0-rc.1): the base is its own M.m.p; do NOT bump again.
    BASE_MAJOR=$MAJOR
    BASE_MINOR=$MINOR
    BASE_PATCH=$PATCH
else
    # Latest tag is stable: bump per the requested type to get the target base.
    case $VERSION_TYPE in
        major) BASE_MAJOR=$((MAJOR + 1)); BASE_MINOR=0;            BASE_PATCH=0 ;;
        minor) BASE_MAJOR=$MAJOR;         BASE_MINOR=$((MINOR + 1)); BASE_PATCH=0 ;;
        patch) BASE_MAJOR=$MAJOR;         BASE_MINOR=$MINOR;         BASE_PATCH=$((PATCH + 1)) ;;
    esac
fi

NEW_VERSION="${BASE_MAJOR}.${BASE_MINOR}.${BASE_PATCH}"

if [ "$IS_RC" = true ]; then
    # RC build: increment the rc counter when continuing the same target, else start at 1.
    if [ -n "$LATEST_RC" ]; then
        NEW_RC=$((LATEST_RC + 1))
    else
        NEW_RC=1
    fi
    NEW_VERSION="${NEW_VERSION}-rc.${NEW_RC}"
fi

NEW_TAG="v${NEW_VERSION}"

echo -e "${GREEN}✓ Version type: ${YELLOW}${VERSION_TYPE}$([ "$IS_RC" = true ] && echo " (rc)")${NC}"
echo -e "  ${LATEST_TAG} -> ${GREEN}${NEW_TAG}${NC}"
echo ""

# Check if tag already exists
if git rev-parse "$NEW_TAG" >/dev/null 2>&1; then
    echo -e "${RED}Error: Tag $NEW_TAG already exists${NC}"
    exit 1
fi

# Show what will happen
echo -e "${BLUE}[4/5]${NC} Ready to publish:"
echo -e "  Tag to create: ${GREEN}${NEW_TAG}${NC}"
echo -e "  Remote: $(git remote get-url origin)"
echo -e "  Branch: $(git branch --show-current)"
echo ""
echo -e "${YELLOW}This will trigger GitHub Actions to:${NC}"
echo -e "  1. Build the solution"
echo -e "  2. Run tests"
echo -e "  3. Create NuGet packages (SharpConsoleUI + SharpConsoleUI.Templates + SharpConsoleUI.Host ${NEW_VERSION})"
if [ "$IS_RC" = true ]; then
    echo -e "  4. Publish to NuGet.org ${YELLOW}as a prerelease${NC}"
    echo ""
    echo -e "  Testers install it with:"
    echo -e "    ${BLUE}dotnet add package SharpConsoleUI --version ${NEW_VERSION}${NC}"
    echo -e "    ${BLUE}dotnet add package SharpConsoleUI --prerelease${NC}  (latest preview)"
else
    echo -e "  4. Publish to NuGet.org"
fi
echo ""

# Confirmation (unless --force)
if [ "$FORCE" = false ]; then
    read -p "$(echo -e ${YELLOW}Continue? [y/N]:${NC} )" -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${RED}Aborted by user${NC}"
        exit 1
    fi
fi

# Update template SharpConsoleUI dependency to new version (after confirmation).
# Skipped for RC builds: templates pin a concrete dependency for `dotnet new` users, and we don't
# want a `dotnet new` app to depend on a prerelease — templates stay on the last stable version.
TEMPLATES_UPDATED=false

if [ "$IS_RC" = true ]; then
    echo -e "${BLUE}ℹ${NC} RC build — leaving template dependencies on the last stable version."
fi

# 1. csproj-based templates (dotnet-new templates, schost templates)
for TEMPLATE_CSPROJ in templates/content/*/*.csproj tools/schost/templates/*/*.csproj; do
    [ "$IS_RC" = true ] && break
    if [ -f "$TEMPLATE_CSPROJ" ]; then
        sed -i "s|<PackageReference Include=\"SharpConsoleUI\" Version=\"[^\"]*\"|<PackageReference Include=\"SharpConsoleUI\" Version=\"${NEW_VERSION}\"|" "$TEMPLATE_CSPROJ"
        TEMPLATES_UPDATED=true
    fi
done

# 2. .NET 10 file-based app templates under docs/scripting/templates/
for TEMPLATE_CS in docs/scripting/templates/*.cs; do
    [ "$IS_RC" = true ] && break
    if [ -f "$TEMPLATE_CS" ] && grep -q '^#:package SharpConsoleUI@' "$TEMPLATE_CS"; then
        sed -i "s|^#:package SharpConsoleUI@.*|#:package SharpConsoleUI@${NEW_VERSION}|" "$TEMPLATE_CS"
        TEMPLATES_UPDATED=true
    fi
done

if [ "$TEMPLATES_UPDATED" = true ] && ! git diff --quiet templates/ tools/schost/templates/ docs/scripting/templates/; then
    git add templates/ tools/schost/templates/ docs/scripting/templates/
    git commit -m "Update template SharpConsoleUI dependency to ${NEW_VERSION}"
    git push origin "$(git branch --show-current)"
    echo -e "${GREEN}✓ Updated template dependencies to ${NEW_VERSION}${NC}"
fi

# Create and push tag
echo -e "${BLUE}[5/5]${NC} Creating and pushing tag..."

if git tag "$NEW_TAG"; then
    echo -e "${GREEN}✓ Tag created: $NEW_TAG${NC}"
else
    echo -e "${RED}Error: Failed to create tag${NC}"
    exit 1
fi

if git push origin "$NEW_TAG"; then
    echo -e "${GREEN}✓ Tag pushed to remote${NC}"
else
    echo -e "${RED}Error: Failed to push tag${NC}"
    echo -e "${YELLOW}Cleaning up local tag...${NC}"
    git tag -d "$NEW_TAG"
    exit 1
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✓ Successfully published version $NEW_TAG${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "Monitor the build at:"
echo -e "${BLUE}https://github.com/nickprotop/ConsoleEx/actions${NC}"
echo ""
echo -e "GitHub Release will be created at:"
echo -e "${BLUE}https://github.com/nickprotop/ConsoleEx/releases/tag/$NEW_TAG${NC}"
echo ""
echo -e "NuGet package will be available at:"
echo -e "${BLUE}https://www.nuget.org/packages/SharpConsoleUI/${NC}"
echo ""
