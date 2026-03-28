#!/usr/bin/env bash
# quality-gate.sh — Scans SharpConsoleUI/ for CLAUDE.md rule violations
#
# Usage: ./scripts/quality-gate.sh [--help]
# Exit codes: 0 = pass, 1 = violations found

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$REPO_ROOT/SharpConsoleUI"
ERRORS=0
WARNINGS=0

# --- Output helpers ---
RED='\033[0;31m'
YELLOW='\033[0;33m'
GREEN='\033[0;32m'
BOLD='\033[1m'
DIM='\033[2m'
NC='\033[0m'

error() { echo -e "  ${RED}FAIL${NC} $1"; ((ERRORS++)); }
warn()  { echo -e "  ${YELLOW}WARN${NC} $1"; ((WARNINGS++)); }
ok()    { echo -e "  ${GREEN}OK${NC} $1"; }

section() { echo -e "\n${BOLD}[$1]${NC} $2"; }

show_help() {
    echo -e "${BOLD}SharpConsoleUI Quality Gate${NC}"
    echo ""
    echo "Scans SharpConsoleUI/ for CLAUDE.md rule violations."
    echo ""
    echo -e "${BOLD}Usage:${NC} ./scripts/quality-gate.sh [--help]"
    echo ""
    echo -e "${BOLD}Checks:${NC}"
    echo "  1. File size limits        Warns when files exceed line limits:"
    echo "                               Helpers: 300  |  Services/Core: 600"
    echo "                               Builders: 600 |  Config: 500"
    echo "                               Controls/Other: 800"
    echo "  2. No console output       Console.WriteLine/Write/Clear in library"
    echo "                               code corrupts UI rendering. Excludes"
    echo "                               Drivers/, Logging/, Diagnostics/."
    echo "  3. No TODO/HACK/FIXME      Debt markers should be tracked in issues,"
    echo "                               not left in code."
    echo "  4. No string +=            String concatenation with += creates"
    echo "                               allocations. Use StringBuilder instead."
    echo "  5. Null-coalescing <= 2    Chains of 3+ ?? operators must be"
    echo "                               extracted to helper methods."
    echo "  6. Known typo patterns     Scans for historically recurring typos"
    echo "                               in identifiers (case-sensitive)."
    echo "  7. No string.Length        string.Length returns UTF-16 code units,"
    echo "                               not display columns. Flags .Length in"
    echo "                               rendering code — use UnicodeWidth or"
    echo "                               MarkupParser.StripLength instead."
    echo "  8. Build and test          Builds Release config, runs all tests."
    echo ""
    echo -e "${BOLD}Exit codes:${NC}"
    echo "  0  All checks passed (warnings are OK)"
    echo "  1  One or more errors found"
    exit 0
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    show_help
fi

get_files() {
    find "$SRC" -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*"
}

rel() { echo "${1#$REPO_ROOT/}"; }

# ============================================================================
# 1. File size limits
# ============================================================================
check_file_sizes() {
    section "1" "File size limits"
    local count=0

    while IFS= read -r file; do
        [[ -z "$file" ]] && continue
        local lines relpath limit category
        lines=$(wc -l < "$file")
        relpath=$(rel "$file")

        case "$relpath" in
            */Helpers/*)          limit=300; category="helper" ;;
            */Core/*Service*.cs)  limit=600; category="service" ;;
            */Core/*.cs)          limit=600; category="core" ;;
            */Configuration/*)    limit=500; category="config" ;;
            */Builders/*)         limit=600; category="builder" ;;
            */Controls/*)         limit=800; category="control" ;;
            *)                    limit=800; category="other" ;;
        esac

        if (( lines > limit )); then
            warn "$relpath: ${lines} lines (limit: ${limit} for ${category})"
            ((count++))
        fi
    done < <(get_files)

    (( count == 0 )) && ok "All files within limits"
}

# ============================================================================
# 2. No Console.WriteLine/Write in library code
# ============================================================================
check_console_output() {
    section "2" "No console output in library"
    local count=0

    while IFS= read -r file; do
        [[ -z "$file" ]] && continue
        local relpath
        relpath=$(rel "$file")

        case "$relpath" in
            */Drivers/*|*/Logging/*|*/Diagnostics/*) continue ;;
        esac

        local hits
        hits=$(grep -nP 'Console\.(WriteLine|Write|Clear)\b' "$file" 2>/dev/null \
            | grep -vP ':\s*//' \
            | grep -v '[Ff]atal' \
            || true)

        if [[ -n "$hits" ]]; then
            while IFS= read -r hit; do
                error "$relpath:$hit"
                ((count++))
            done <<< "$hits"
        fi
    done < <(get_files)

    (( count == 0 )) && ok "No console output found"
}

# ============================================================================
# 3. No TODO/HACK/FIXME
# ============================================================================
check_todo_comments() {
    section "3" "No TODO/HACK/FIXME comments"
    local count=0

    while IFS= read -r file; do
        [[ -z "$file" ]] && continue
        local relpath hits
        relpath=$(rel "$file")
        hits=$(grep -nE '\b(TODO|HACK|FIXME|XXX)\b' "$file" 2>/dev/null || true)

        if [[ -n "$hits" ]]; then
            while IFS= read -r hit; do
                error "$relpath:$hit"
                ((count++))
            done <<< "$hits"
        fi
    done < <(get_files)

    (( count == 0 )) && ok "No debt markers found"
}

# ============================================================================
# 4. No string += concatenation
# ============================================================================
check_string_concat() {
    section "4" "No string += concatenation"
    local count=0

    while IFS= read -r file; do
        [[ -z "$file" ]] && continue
        local relpath
        relpath=$(rel "$file")

        local hits
        hits=$(grep -nP '^\s*_?\w*(text|string|str|label|title|content|message|line|result|output)\w*\s*\+=' "$file" 2>/dev/null \
            | grep -P '\+=\s*("|new string|\$"|string\.|String\.)' || true)

        if [[ -n "$hits" ]]; then
            while IFS= read -r hit; do
                error "$relpath:$hit"
                ((count++))
            done <<< "$hits"
        fi
    done < <(get_files)

    (( count == 0 )) && ok "No string concatenation found"
}

# ============================================================================
# 5. Null-coalescing chains max 2 deep
# ============================================================================
check_null_coalescing() {
    section "5" "Null-coalescing chains <= 2"
    local count=0

    while IFS= read -r file; do
        [[ -z "$file" ]] && continue
        local relpath
        relpath=$(rel "$file")

        case "$relpath" in
            */Helpers/ColorResolver.cs) continue ;;
        esac

        local hits
        hits=$(grep -nP '(\?\?.*){3,}' "$file" 2>/dev/null || true)

        if [[ -n "$hits" ]]; then
            while IFS= read -r hit; do
                error "$relpath:$hit"
                ((count++))
            done <<< "$hits"
        fi
    done < <(get_files)

    (( count == 0 )) && ok "No deep null-coalescing chains"
}

# ============================================================================
# 6. Known typo patterns
# ============================================================================
check_typos() {
    section "6" "Known typo patterns"
    local count=0
    local typos=(
        'Allready' 'OnCLosing' 'Backround' 'Widnow' 'Contorl'
        'Boarder' 'Recieve' 'Occured' 'Seperate' 'Visibile'
        'Horzontal' 'Veritcal'
    )

    while IFS= read -r file; do
        [[ -z "$file" ]] && continue
        local relpath
        relpath=$(rel "$file")

        for typo in "${typos[@]}"; do
            local hits
            hits=$(grep -n "$typo" "$file" 2>/dev/null || true)
            if [[ -n "$hits" ]]; then
                while IFS= read -r hit; do
                    error "$relpath:$hit"
                    ((count++))
                done <<< "$hits"
            fi
        done
    done < <(get_files)

    (( count == 0 )) && ok "No known typos"
}

# ============================================================================
# 7. Build + Tests
# ============================================================================
# ============================================================================
# 7. No string.Length for display width
# ============================================================================
check_string_length_display() {
    section "7" "No string.Length for display width"
    local count=0

    while IFS= read -r file; do
        [[ -z "$file" ]] && continue
        local relpath
        relpath=$(rel "$file")

        # Skip non-rendering files
        case "$relpath" in
            */Parsing/*)       continue ;;
            */Plugins/*)       continue ;;
            */Configuration/*) continue ;;
            */Core/*)          continue ;;
            */Logging/*)       continue ;;
            */Diagnostics/*)   continue ;;
        esac

        # Only flag: someStringVar.Length used for width/display calculations.
        # Exclude: array.Length, span.Length, pixel.Length, collection sizes,
        #          FormatValue().Length (numeric), line.Length in editors (char index).
        local hits
        hits=$(grep -nP '\.\s*Length\b' "$file" 2>/dev/null \
            | grep -vP ':\s*//' \
            | grep -vP ':\s*///' \
            | grep -vP '(\.Length\s*[<>=!]+\s*0)' \
            | grep -vP '\b(pixels?|argb|colWidths|_renderedColumn|_filterIndexMap|_sortIndexMap|_segments|lines|visibleLine|FormatValue)\b' \
            | grep -vP '\b(bytes|buffer|_buffer|_cells|_previousCells|args|Args|span|Span)\b' \
            | grep -vP 'for\s*\(' \
            | grep -vP '(\.Length\s*[-+]\s*\d|<\s*\w+\.Length|>=?\s*\w+\.Length)' \
            | grep -vP '(StringBuilder|Capacity|Allocat|new\s+\w+\[|textLines)' \
            | grep -P '\b(text|title|content|label|prompt|input|name|display|header|status)\w*\.Length' \
            || true)

        if [[ -n "$hits" ]]; then
            while IFS= read -r hit; do
                error "$relpath:$hit"
                ((count++))
            done <<< "$hits"
        fi
    done < <(get_files)

    (( count == 0 )) && ok "No .Length misuse — use UnicodeWidth or MarkupParser.StripLength"
}

# ============================================================================
# 8. Build + Tests
# ============================================================================
check_build_and_tests() {
    section "8" "Build and test"

    echo -e "  ${DIM}Building...${NC}"
    if ! dotnet build "$SRC/SharpConsoleUI.csproj" --configuration Release -v quiet 2>&1 | tail -3; then
        error "Build failed"
        return
    fi
    ok "Build succeeded"

    local test_proj="$REPO_ROOT/SharpConsoleUI.Tests/SharpConsoleUI.Tests.csproj"
    if [[ -f "$test_proj" ]]; then
        echo -e "  ${DIM}Running tests...${NC}"
        local test_output
        if test_output=$(dotnet test "$test_proj" --configuration Release -v quiet --no-restore 2>&1); then
            local passed failed
            passed=$(echo "$test_output" | grep -oP 'Passed:\s*\K\d+' || echo "?")
            failed=$(echo "$test_output" | grep -oP 'Failed:\s*\K\d+' || echo "0")
            if [[ "$failed" != "0" ]]; then
                error "Tests: $passed passed, $failed FAILED"
            else
                ok "Tests: $passed passed"
            fi
        else
            error "Test run failed"
            echo "$test_output" | tail -10
        fi
    else
        warn "Test project not found at $test_proj"
    fi
}

# ============================================================================
# RUN
# ============================================================================
echo -e "${BOLD}SharpConsoleUI Quality Gate${NC}"
echo -e "Source: $SRC"

check_file_sizes
check_console_output
check_todo_comments
check_string_concat
check_null_coalescing
check_typos
check_string_length_display
check_build_and_tests

# --- Summary ---
echo ""
echo -e "${BOLD}=== Summary ===${NC}"
if (( ERRORS > 0 )); then
    echo -e "${RED}${ERRORS} error(s)${NC}, ${YELLOW}${WARNINGS} warning(s)${NC}"
    exit 1
elif (( WARNINGS > 0 )); then
    echo -e "${GREEN}0 errors${NC}, ${YELLOW}${WARNINGS} warning(s)${NC}"
    exit 0
else
    echo -e "${GREEN}All checks passed${NC}"
    exit 0
fi
