#!/bin/bash
# run-benchmarks.sh - Run the SharpConsoleUI.Benchmarks suite and print the result tables.
# Usage: ./run-benchmarks.sh [--list] [--quick] [--filter '<glob>'] [-- <extra BenchmarkDotNet args>]
#
#   --list              List every available benchmark (fully-qualified names you can pass to
#                       --filter) and exit WITHOUT running anything.
#   --quick             Use the short job (3 warmup + 3 iterations) — fast, wider error bars.
#   --filter '<glob>'   Only run matching benchmarks (default '*' = the whole suite).
#                       e.g. --filter '*MarkupParsingBenchmarks*'
#   Anything after `--` is passed straight through to BenchmarkDotNet.
#
# Results: BenchmarkDotNet writes per-class reports to ./BenchmarkDotNet.Artifacts/results/
# (markdown + csv + html). That directory is gitignored. This script prints the markdown tables
# at the end. To refresh the committed baseline, copy the printed tables into
# docs/benchmarks/README.md and update its "Captured on" line.

set -e  # Exit on error

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Resolve repo root (the directory this script lives in) so it works from anywhere.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT/SharpConsoleUI.Benchmarks/SharpConsoleUI.Benchmarks.csproj"
RESULTS_DIR="$ROOT/BenchmarkDotNet.Artifacts/results"

# Default values
FILTER='*'
LIST=false
JOB_ARGS=()
PASSTHROUGH=()

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --list)
            LIST=true
            shift
            ;;
        --quick)
            JOB_ARGS+=("--job" "short")
            shift
            ;;
        --filter)
            FILTER="$2"
            shift 2
            ;;
        --)
            shift
            PASSTHROUGH=("$@")
            break
            ;;
        *)
            echo -e "${YELLOW}Unknown argument: $1${NC}" >&2
            echo "Usage: ./run-benchmarks.sh [--list] [--quick] [--filter '<glob>'] [-- <extra args>]" >&2
            exit 1
            ;;
    esac
done

# --list: print every available benchmark (a name here is a valid --filter) and exit.
if [ "$LIST" = true ]; then
    echo -e "${BLUE}Available benchmarks (pass any name, or a glob, to --filter):${NC}"
    dotnet run -c Release --project "$PROJECT" -- --list flat \
        | grep '^SharpConsoleUI\.Benchmarks\.'
    exit 0
fi

echo -e "${BLUE}Running SharpConsoleUI benchmarks (filter: ${FILTER})...${NC}"
echo -e "${YELLOW}This can take several minutes — the layout-tree macro is intentionally heavy.${NC}"

# Clear stale reports so the tables printed at the end reflect ONLY this run (otherwise a
# filtered run would also re-print leftover reports from a previous, broader run).
rm -f "$RESULTS_DIR"/*-report-github.md "$RESULTS_DIR"/*-report.csv "$RESULTS_DIR"/*-report.html 2>/dev/null || true

# BenchmarkDotNet requires a Release build for meaningful numbers.
dotnet run -c Release --project "$PROJECT" -- \
    --filter "$FILTER" "${JOB_ARGS[@]}" "${PASSTHROUGH[@]}"

echo ""
echo -e "${GREEN}==================== RESULTS ====================${NC}"

if [ -d "$RESULTS_DIR" ] && ls "$RESULTS_DIR"/*-report-github.md >/dev/null 2>&1; then
    for report in "$RESULTS_DIR"/*-report-github.md; do
        echo ""
        echo -e "${BLUE}### $(basename "$report" -report-github.md)${NC}"
        cat "$report"
    done
    echo ""
    echo -e "${GREEN}Full reports (markdown + csv + html): ${RESULTS_DIR}${NC}"
    echo -e "${YELLOW}To refresh the committed baseline, paste the tables above into docs/benchmarks/README.md.${NC}"
else
    echo -e "${YELLOW}No report files found under ${RESULTS_DIR}.${NC}"
    echo -e "${YELLOW}(Did the run match any benchmarks? Check the --filter value.)${NC}"
    exit 1
fi
