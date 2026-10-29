# ansicheck

Sends terminal escape sequences to a running application and checks what the
library decoded them to.

```bash
./run.sh          # build, start, send the corpus, report, stop
./run.sh -v       # name every case, not just the failures
```

## Why it is built this way

A decoder test is easy to write and easy to make worthless. Send bytes, read
what comes back, write that down as the expectation, and the test passes
forever: it proves the decoder agrees with itself. If it reads a bit wrong, the
expectation inherits the error and nothing ever says so.

So `corpus.json` is written from the encoding, not from the output. For the SGR
form, `ESC [ < b ; x ; y` followed by `M` or `m`:

| In `b` | Means |
|---|---|
| bits 0-1 | the button: 0 is the first, 1 the second, 2 the third, 3 none |
| bit 2 (4) | shift |
| bit 3 (8) | alt |
| bit 4 (16) | control |
| bit 5 (32) | motion |
| bit 6 (64) | the wheel, the low bits then giving up, down, left, right |

`M` is a press, `m` a release, and coordinates are 1-based on the wire. The X10
form, `ESC [ M` and three bytes, carries the same button and modifier bits with
every byte offset by 32, and no `m`: there, button bits 3 IS the release, since
nothing else can express one.

The corpus records what a sequence **means** - button, action, modifiers,
coordinates - not what the library calls it. Writing `Button1Pressed|Button1Dragged`
as an expectation would be guessing at enum spellings, and then every mismatch
is noise. One adapter in `check.py` maps the library's vocabulary onto the
corpus's, and it is the only place the two meet: anything it quietly "fixes" is
a mismatch hidden rather than reported.

A failure is one of three things, and the script says plainly that it cannot
tell them apart:

- the library is wrong
- the corpus's reading of the encoding is wrong
- the behaviour is unspecified, and someone has to choose

Investigate before changing either side. Editing the corpus to match the output
is how this stops being worth running.

## Stated assumptions

Things the corpus asserts that the encoding does not decide, written down
because a mismatch against them means a convention changed, not a bug:

- **Coordinates are reported 0-based.** The wire is 1-based, so `\e[<0;1;1M`
  is expected at `0,0`. This is the library's choice, not xterm's.
- **Modifier order carries no meaning.** Both sides are sorted before
  comparison.
- **A release that names no button still names button 1.** Button bits 3 says
  no button, and `MouseFlags` has no button-agnostic release; ending a drag,
  releasing a window capture and deriving a click are all keyed on
  `Button1Released`. A release naming nothing would end none of them, so one is
  chosen. This applies to the X10 release, where the protocol cannot say, and to
  the SGR release, where it can but the consumer cannot act on it.
- **A press that names no button names nothing.** The asymmetry is deliberate:
  a press has no obligation to end anything, and inventing a button leaves a
  control believing one is held. The position is then all there is to report.

## The application

`Program.cs` is not a demo and should not become one. It is headless, so there
is no terminal to own raw mode or stdin; a fixed 100x30, so a coordinate means
the same thing on every machine and in CI; has no panels, so a window at `y 0`
really is at screen row 0; and draws nothing on its own, so every frame is one
a sequence asked for. The socket is set in code, which opens it without
prompting - a property cannot be inherited from a shell profile the way the
environment variable can.

A real application would work too, and would additionally catch a sequence that
decoded correctly but reached the wrong control. It would also move: its layout
changes as it grows, and then coordinate expectations fail for reasons that have
nothing to do with the decoder, which is the one thing this is for.

## What it does not cover

The decoder, not the read loop. Partial reads, the escape timeout that
distinguishes a lone `ESC` from the start of a sequence, and raw mode itself all
need a real terminal: use tmux, or run an application under a pty. Everything
from the decoder upward is here, and it behaves identically on all three driver
configurations - a real terminal, a terminal without raw mode, and headless -
because `seq` enters at the point where they converge.

## Adding a case

Derive it from the table above before running anything, and say in `why` what
rule it is testing. A case whose `why` is "this is what the library does" is the
failure mode this tool exists to avoid.
