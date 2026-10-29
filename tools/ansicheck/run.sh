#!/usr/bin/env bash
# Builds the application, starts it, sends the corpus, reports, stops it.
#
# The application exists only to be sent bytes: headless, fixed size, socket
# set in code so it opens without asking. Nothing about it needs a terminal,
# so this runs the same way in CI.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$here"

socket="${ANSICHECK_SOCKET:-/tmp/ansicheck-$$.sock}"
app="$here/bin/Debug/net9.0/ansicheck"

echo "building"
dotnet build -v q --nologo || exit 1

rm -f "$socket"
"$app" "$socket" >"$here/app.log" 2>&1 &
pid=$!

# Started when it says so, rather than after a sleep that is either wasted or
# too short. A socket that never appears means the app died; its log says why.
for _ in $(seq 1 100); do
    [ -S "$socket" ] && break
    kill -0 "$pid" 2>/dev/null || { echo "the application exited:"; cat "$here/app.log"; exit 1; }
    sleep 0.1
done

if [ ! -S "$socket" ]; then
    echo "no socket after 10s:"; cat "$here/app.log"
    kill "$pid" 2>/dev/null
    exit 1
fi

python3 -I check.py "$socket" "$@"
status=$?

kill "$pid" 2>/dev/null
wait "$pid" 2>/dev/null
rm -f "$socket"

exit $status
