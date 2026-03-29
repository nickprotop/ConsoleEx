#!/bin/bash
# SharpConsoleUI Rendering Benchmark Suite
# Usage: ./benchmark.sh [--quick] [--filter <pattern>]
#
# Run all benchmarks:  ./benchmark.sh
# Quick mode (1 test):  ./benchmark.sh --quick
# Filter:              ./benchmark.sh --filter BufferScaling

set -e
cd "$(dirname "$0")/.."

FILTER="Category=Benchmark|Category=Profile"
QUICK=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --quick) QUICK=true; FILTER="Bench_FullRedraw"; shift ;;
        --filter) FILTER="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Colors
BOLD='\033[1m'
DIM='\033[2m'
CYAN='\033[36m'
GREEN='\033[32m'
YELLOW='\033[33m'
RED='\033[31m'
MAGENTA='\033[35m'
RESET='\033[0m'

COMMIT=$(git log --oneline -1 2>/dev/null || echo "unknown")
BRANCH=$(git branch --show-current 2>/dev/null || echo "detached")

echo ""
echo -e "${BOLD}${CYAN}  ╔═══════════════════════════════════════════════════════════╗${RESET}"
echo -e "${BOLD}${CYAN}  ║          SharpConsoleUI Rendering Benchmarks              ║${RESET}"
echo -e "${BOLD}${CYAN}  ╚═══════════════════════════════════════════════════════════╝${RESET}"
echo ""
echo -e "  ${DIM}Branch:${RESET} ${BOLD}$BRANCH${RESET}"
echo -e "  ${DIM}Commit:${RESET} ${BOLD}$COMMIT${RESET}"
echo -e "  ${DIM}Date:${RESET}   $(date '+%Y-%m-%d %H:%M:%S')"
echo -e "  ${DIM}Host:${RESET}   $(uname -n) ($(nproc) cores)"
echo ""

# Build silently
BUILD_OUT=$(dotnet build SharpConsoleUI.Tests/SharpConsoleUI.Tests.csproj 2>&1)
if echo "$BUILD_OUT" | grep -q "Build succeeded"; then
    echo -e "  ${DIM}Build OK${RESET}"
else
    echo -e "  ${RED}Build failed:${RESET}"
    echo "$BUILD_OUT" | grep -E "error CS" | head -5
    exit 1
fi

# Run benchmarks, capture output with live progress
TMPFILE=$(mktemp)
echo -e "  ${DIM}Running benchmarks (filter: ${FILTER})...${RESET}"
echo ""

SHARPCONSOLEUI_BENCHMARK=1 dotnet test SharpConsoleUI.Tests/SharpConsoleUI.Tests.csproj \
    --filter "$FILTER" \
    --no-build \
    --logger "console;verbosity=detailed" \
    2>&1 > "$TMPFILE" &
TEST_PID=$!

# Show progress: print test names as they complete
while kill -0 $TEST_PID 2>/dev/null; do
    # Check for newly completed tests
    COMPLETED=$(grep -c "Passed\|Failed" "$TMPFILE" 2>/dev/null || echo "0")
    LAST=$(grep -oP "(?:Passed|Failed) \K[^ ]+" "$TMPFILE" 2>/dev/null | tail -1)
    if [[ -n "$LAST" ]]; then
        # Overwrite progress line
        printf "\r  ${DIM}  [%s] %s${RESET}%-20s" "$COMPLETED" "$LAST" "" >&2
    fi
    sleep 1
done
wait $TEST_PID 2>/dev/null || true
printf "\r%-80s\r" "" >&2  # clear progress line

# Parse results
parse_bench() {
    local label="$1"
    local pattern="$2"

    local block=$(sed -n "/$pattern/,/^$/p" "$TMPFILE")
    local ms=$(echo "$block" | grep "ms/frame:" | head -1 | awk '{print $2}')
    local fps=$(echo "$block" | grep "FPS:" | head -1 | awk '{print $2}')
    local bytes=$(echo "$block" | grep "bytes/f:" | head -1 | awk '{print $2}')
    local cells=$(echo "$block" | grep "cells/f:" | head -1 | awk '{print $2}')
    local budget=$(echo "$block" | grep "Budget:" | head -1 | sed 's/.*Budget: *//')

    if [[ -n "$ms" ]]; then
        # FPS bar
        local bar_len=$((${fps%.*} / 5))
        [[ $bar_len -gt 40 ]] && bar_len=40
        local bar_fill=$(printf '%*s' "$bar_len" '' | tr ' ' '█')
        local bar_empty=$(printf '%*s' "$((40 - bar_len))" '' | tr ' ' '░')

        # Color based on FPS
        local fps_color=$GREEN
        if (( ${fps%.*} < 30 )); then fps_color=$RED
        elif (( ${fps%.*} < 60 )); then fps_color=$YELLOW
        fi

        echo -e "  ${BOLD}$label${RESET}"
        echo -e "    ${fps_color}${bar_fill}${DIM}${bar_empty}${RESET}  ${BOLD}${fps_color}${fps} FPS${RESET}  ${DIM}(${ms} ms/frame)${RESET}"

        if [[ -n "$bytes" ]]; then
            echo -e "    ${DIM}bytes/frame:${RESET} $bytes  ${DIM}cells:${RESET} $cells"
        fi
        if [[ -n "$budget" ]]; then
            if echo "$budget" | grep -q "WITHIN 60fps"; then
                echo -e "    ${GREEN}$budget${RESET}"
            elif echo "$budget" | grep -q "WITHIN 30fps"; then
                echo -e "    ${YELLOW}$budget${RESET}"
            else
                echo -e "    ${RED}$budget${RESET}"
            fi
        fi
        echo ""
    fi
}

echo -e "${BOLD}${CYAN}  ── Benchmark Results ──────────────────────────────────────${RESET}"
echo ""

parse_bench "Full Redraw (Alpha Blending, 110x38)" "Full Redraw.*Alpha"
parse_bench "Static Frame (Idle Cost, 110x38)" "Static Frame.*Idle"
parse_bench "Partial Update (Single Label, 110x38)" "Partial Update.*Single"
parse_bench "Window Overlap (3 Windows, 130x50)" "Window Overlap.*3 Windows"
parse_bench "Deep Control Tree (60 Controls, 130x42)" "Deep Control Tree"

# Scaling table
if grep -q "Buffer Size Scaling" "$TMPFILE"; then
    echo -e "${BOLD}${CYAN}  ── Buffer Size Scaling ────────────────────────────────────${RESET}"
    echo ""
    echo -e "    ${DIM}Size           Cells    ms/frame   bytes/f    ms/1K cells${RESET}"
    echo -e "    ${DIM}──────────────────────────────────────────────────────────${RESET}"

    sed -n '/Buffer Size Scaling/,/^$/p' "$TMPFILE" | grep -E "^   [0-9]" | while read -r line; do
        size=$(echo "$line" | awk '{print $1}')
        cells=$(echo "$line" | awk '{print $2}')
        ms=$(echo "$line" | awk '{print $3}')
        bytes=$(echo "$line" | awk '{print $4}')
        mspk=$(echo "$line" | awk '{print $5}')
        echo -e "    ${BOLD}$size${RESET}$(printf '%*s' $((15 - ${#size})) '')${cells}$(printf '%*s' $((9 - ${#cells})) '')${ms}$(printf '%*s' $((11 - ${#ms})) '')${bytes}$(printf '%*s' $((11 - ${#bytes})) '')${mspk}"
    done
    echo ""
fi

# Dirty ratio curve
if grep -q "Dirty Ratio Curve" "$TMPFILE"; then
    echo -e "${BOLD}${CYAN}  ── Dirty Ratio Curve ─────────────────────────────────────${RESET}"
    echo ""
    echo -e "    ${DIM}Dirty%   ms/frame   bytes/f    cells/f${RESET}"
    echo -e "    ${DIM}──────────────────────────────────────────${RESET}"

    sed -n '/Dirty Ratio Curve/,/^$/p' "$TMPFILE" | grep -E "^   [0-9~]" | while read -r line; do
        dirty=$(echo "$line" | awk '{print $1}')
        ms=$(echo "$line" | awk '{print $2}')
        bytes=$(echo "$line" | awk '{print $3}')
        cells=$(echo "$line" | awk '{print $4}')
        echo -e "    ${BOLD}$dirty${RESET}$(printf '%*s' $((9 - ${#dirty})) '')${ms}$(printf '%*s' $((11 - ${#ms})) '')${bytes}$(printf '%*s' $((11 - ${#bytes})) '')${cells}"
    done
    echo ""
fi

# Summary
total_time=$(grep "Total time:" "$TMPFILE" | tail -1 | awk '{print $3, $4}')
total_tests=$(grep "Total tests:" "$TMPFILE" | awk '{print $3}')
passed=$(grep "Passed:" "$TMPFILE" | tail -1 | awk '{print $2}')
failed=$(grep "Failed:" "$TMPFILE" | tail -1 | awk '{print $2}' 2>/dev/null || echo "0")

echo -e "${BOLD}${CYAN}  ── Summary ───────────────────────────────────────────────${RESET}"
echo ""
if [[ "$failed" == "0" || -z "$failed" ]]; then
    echo -e "    ${GREEN}${BOLD}All ${passed:-$total_tests} benchmarks passed${RESET} in ${total_time}"
else
    echo -e "    ${RED}${BOLD}${failed} benchmarks FAILED${RESET} (${passed} passed) in ${total_time}"
fi
echo ""
echo -e "  ${DIM}Tip: Run ./benchmark.sh --quick for a fast smoke test${RESET}"
echo -e "  ${DIM}     Run ./benchmark.sh --filter Profile for profiling only${RESET}"
echo ""

rm -f "$TMPFILE"
