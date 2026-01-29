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
FORCE=false

# Parse arguments
for arg in "$@"; do
    case $arg in
        major|minor|patch)
            VERSION_TYPE="$arg"
            ;;
        --force|-f)
            FORCE=true
            ;;
        --help|-h)
            echo "Usage: ./publish.sh [major|minor|patch] [--force]"
            echo ""
            echo "Arguments:"
            echo "  major           Increment major version (v2.4.6 -> v3.0.0)"
            echo "  minor           Increment minor version (v2.4.6 -> v2.5.0)"
            echo "  patch           Increment patch version (v2.4.6 -> v2.4.7) [default]"
            echo "  --force, -f     Skip confirmation prompt"
            echo ""
            echo "Example:"
            echo "  ./publish.sh patch          # Interactive, increment patch"
            echo "  ./publish.sh minor --force  # No prompt, increment minor"
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

# Fetch tags
echo -e "${BLUE}[2/5]${NC} Fetching tags from remote..."
git fetch --tags --quiet

# Get the latest tag
LATEST_TAG=$(git tag --sort=-v:refname | head -1)

if [ -z "$LATEST_TAG" ]; then
    echo -e "${RED}Error: No tags found in repository${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Latest tag: $LATEST_TAG${NC}"
echo ""

# Parse version from tag (v2.4.6 -> 2.4.6)
if [[ ! $LATEST_TAG =~ ^v([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
    echo -e "${RED}Error: Latest tag '$LATEST_TAG' is not in semver format (v{major}.{minor}.{patch})${NC}"
    exit 1
fi

MAJOR="${BASH_REMATCH[1]}"
MINOR="${BASH_REMATCH[2]}"
PATCH="${BASH_REMATCH[3]}"

# Increment version based on type
echo -e "${BLUE}[3/5]${NC} Calculating new version..."

case $VERSION_TYPE in
    major)
        NEW_MAJOR=$((MAJOR + 1))
        NEW_MINOR=0
        NEW_PATCH=0
        ;;
    minor)
        NEW_MAJOR=$MAJOR
        NEW_MINOR=$((MINOR + 1))
        NEW_PATCH=0
        ;;
    patch)
        NEW_MAJOR=$MAJOR
        NEW_MINOR=$MINOR
        NEW_PATCH=$((PATCH + 1))
        ;;
esac

NEW_TAG="v${NEW_MAJOR}.${NEW_MINOR}.${NEW_PATCH}"

echo -e "${GREEN}✓ Version type: ${YELLOW}$VERSION_TYPE${NC}"
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
echo -e "  3. Create NuGet package (SharpConsoleUI ${NEW_MAJOR}.${NEW_MINOR}.${NEW_PATCH})"
echo -e "  4. Publish to NuGet.org"
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
echo -e "NuGet package will be available at:"
echo -e "${BLUE}https://www.nuget.org/packages/SharpConsoleUI/${NC}"
echo ""
