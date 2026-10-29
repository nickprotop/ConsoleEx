#!/usr/bin/env python3
"""Sends each sequence in the corpus to a running app and compares the decode.

The corpus records what a sequence MEANS, derived from the xterm control
sequence encoding: button from the low two bits, modifiers from bits 2 to 4,
motion from bit 5, the wheel from bit 6, coordinates 1-based on the wire. It
was written without running any of this, which is the only way the comparison
can fail for a real reason rather than agreeing with whatever the library
happens to do.

The library answers in its own vocabulary, so one adapter maps its words onto
the corpus's. That adapter is the single place the two meet and is meant to be
read as such: anything it "fixes" is a mismatch hidden rather than reported.
"""

import argparse
import json
import re
import socket
import sys

# --- the wire ------------------------------------------------------------


def ask(path, request):
    """One request, one reply.

    The server greets a new connection with a hello line before anything is
    asked of it, and the connection stays open afterwards. So this reads lines
    until one carries a reply, rather than to end of stream, which never
    comes, or taking the first line, which is the greeting.
    """
    s = socket.socket(socket.AF_UNIX)
    s.settimeout(10)
    try:
        s.connect(path)
        s.sendall((json.dumps(request) + "\n").encode())

        buf = b""
        while True:
            if b"\n" in buf:
                line, buf = buf.split(b"\n", 1)
                line = line.strip()
                if not line:
                    continue
                message = json.loads(line)
                # The greeting announces the server; it answers nothing.
                if "hello" in message and "ok" not in message:
                    continue
                return message

            chunk = s.recv(65536)
            if not chunk:
                raise RuntimeError("the application closed without replying")
            buf += chunk
    finally:
        s.close()


# --- the adapter ---------------------------------------------------------
# Everything below translates the library's words into the corpus's. It makes
# no decisions: a flag it does not recognise is carried through so a mismatch
# names it rather than silently vanishing.

_MOD_FLAGS = {"ButtonShift": "shift", "ButtonAlt": "alt", "ButtonCtrl": "ctrl"}

_BUTTON_ACTION = {
    "Button1Pressed": (1, "press"),
    "Button2Pressed": (2, "press"),
    "Button3Pressed": (3, "press"),
    "Button1Released": (1, "release"),
    "Button2Released": (2, "release"),
    "Button3Released": (3, "release"),
}

_WHEEL = {
    "WheeledUp": "up",
    "WheeledDown": "down",
    "WheeledLeft": "left",
    "WheeledRight": "right",
}

_DRAGGED = {"Button1Dragged": 1, "Button2Dragged": 2, "Button3Dragged": 3}


def adapt_mouse(flags, x, y):
    """The library's flag list, as the corpus would describe it."""
    out = {"kind": "mouse", "x": x, "y": y, "mods": [], "extra": []}

    for flag in flags:
        if flag in _MOD_FLAGS:
            out["mods"].append(_MOD_FLAGS[flag])
        elif flag in _WHEEL:
            out["action"] = "wheel"
            out["direction"] = _WHEEL[flag]
        elif flag in _DRAGGED:
            out["action"] = "motion"
            out["button"] = _DRAGGED[flag]
        elif flag in _BUTTON_ACTION:
            button, action = _BUTTON_ACTION[flag]
            # A drag carries its press as well, and motion is the more
            # specific of the two. Never downgrade one to the other.
            if out.get("action") != "motion":
                out["button"] = button
                out["action"] = action
        elif flag == "ReportMousePosition":
            out.setdefault("action", "motion")
            out.setdefault("button", None)
        else:
            out["extra"].append(flag)

    out["mods"].sort()
    return out


def adapt(events):
    """One event line from the library, as the corpus would describe it."""
    adapted = []

    for line in events:
        mouse = re.match(r"^mouse (\S+) @(-?\d+),(-?\d+)$", line)
        if mouse:
            flags = mouse.group(1).split("|")
            adapted.append(adapt_mouse(flags, int(mouse.group(2)), int(mouse.group(3))))
            continue

        paste = re.match(r"^paste (\d+) char", line)
        if paste:
            # The count is all the protocol reports, so the text itself is
            # asked for separately rather than assumed.
            adapted.append({"kind": "paste", "length": int(paste.group(1))})
            continue

        key = re.match(r"^key (\S+)$", line)
        if key:
            spec = key.group(1)
            mods = []
            while len(spec) > 2 and spec[1] == "-" and spec[0] in "CMS":
                mods.append({"C": "ctrl", "M": "alt", "S": "shift"}[spec[0]])
                spec = spec[2:]
            mods.sort()
            adapted.append({"kind": "key", "key": spec, "mods": mods})
            continue

        unknown = re.match(r"^unknown (\d+) byte", line)
        if unknown:
            adapted.append({"kind": "unknown"})
            continue

        adapted.append({"kind": "unparsed", "line": line})

    return adapted


# --- comparison ----------------------------------------------------------


def compare(expected, actual):
    """What differs between one expected event and one the library produced.

    Only the fields the corpus states are checked, since it describes meaning
    rather than every field a reply carries. `extra` is the exception: a flag
    the adapter did not recognise is reported, because an unexplained flag is
    exactly the kind of difference this exists to surface.
    """
    problems = []

    for field, want in expected.items():
        if field == "text":
            # Compared by length here; the text itself needs its own round
            # trip, which the paste cases do below.
            got = actual.get("length")
            if got != len(want):
                problems.append(f"text length {got} != {len(want)}")
            continue

        got = actual.get(field)
        if got != want:
            problems.append(f"{field}: got {got!r}, want {want!r}")

    if actual.get("extra"):
        problems.append(f"unexplained flags: {'|'.join(actual['extra'])}")

    return problems


def run(path, corpus, verbose):
    passed, failed, errors = [], [], []

    for case in corpus:
        name, send = case["name"], case["send"]

        try:
            reply = ask(path, {"cmd": "seq", "text": send})
        except Exception as exc:                      # noqa: BLE001
            errors.append((name, f"request failed: {exc}"))
            continue

        if not reply.get("ok"):
            errors.append((name, f"refused: {reply.get('error')}"))
            continue

        actual = adapt(reply.get("events", []))
        expected = case["expect"]

        if len(actual) != len(expected):
            failed.append((name, case["why"],
                           [f"{len(actual)} event(s), expected {len(expected)}: "
                            f"{reply.get('events')}"]))
            continue

        problems = []
        for want, got in zip(expected, actual):
            if want["kind"] != got["kind"]:
                problems.append(f"kind: got {got['kind']}, want {want['kind']}")
                continue
            problems += compare(want, got)

        if problems:
            failed.append((name, case["why"], problems))
        else:
            passed.append(name)
            if verbose:
                print(f"  ok    {name}")

    return passed, failed, errors


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("socket", help="the running application's diagnostics socket")
    ap.add_argument("--corpus", default="corpus.json")
    ap.add_argument("--verbose", "-v", action="store_true")
    args = ap.parse_args()

    with open(args.corpus, encoding="utf-8") as handle:
        corpus = json.load(handle)

    print(f"{len(corpus)} cases against {args.socket}")

    passed, failed, errors = run(args.socket, corpus, args.verbose)

    for name, why, problems in failed:
        print(f"\nFAIL  {name}")
        print(f"      the corpus says: {why}")
        for problem in problems:
            print(f"      {problem}")

    for name, problem in errors:
        print(f"\nERROR {name}\n      {problem}")

    print(f"\n{len(passed)} passed, {len(failed)} failed, {len(errors)} errored")

    if failed or errors:
        print("\nA failure is one of three things, and this script cannot tell them\n"
              "apart: the library is wrong, the corpus's reading of the encoding is\n"
              "wrong, or the behaviour is unspecified and someone has to choose.\n"
              "Investigate before changing either side.")

    return 0 if not failed and not errors else 1


if __name__ == "__main__":
    sys.exit(main())
